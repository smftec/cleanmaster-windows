using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using CleanMaster.Core.Models;
using CleanMaster.Core.Rules;
using CleanMaster.Core.Utils;

namespace CleanMaster.App.Pages;

public partial class CleanSpacePage : Page, IParamPage
{
    private readonly MainWindow _main;
    private readonly ScannerService _scanner = new();
    private readonly CleanService _cleaner = new();
    private int _tab;
    private CancellationTokenSource? _scanCts;

    /// <summary>tab -> 已扫描的行。</summary>
    private readonly Dictionary<int, List<ScanRowVM>> _tabRows = new();
    private readonly Dictionary<int, List<(IScanRule rule, RuleResult res)>> _tabResults = new();

    private static readonly string[] TabNotes =
    [
        "系统与应用产生的临时文件、日志与崩溃报告，默认只勾选安全项目",
        "聊天与办公软件的缓存和日志，不会触碰聊天记录与文档",
        "浏览器为加速上网产生的缓存，不影响登录状态、密码与历史",
        "涉及个人记录，默认全部不勾选，请仔细确认影响后再清理",
        "清空回收站前请确认里面的文件不再需要",
    ];

    public CleanSpacePage(MainWindow main)
    {
        InitializeComponent();
        _main = main;
    }

    public void OnNavigated(object? param)
    {
        if (param is int t) SetTab(t);
        else if (!_tabResults.ContainsKey(_tab) || !_tabRows.ContainsKey(_tab)) SetTab(_tab);
    }

    private void Tab_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && int.TryParse((string)rb.Tag, out var t) && IsLoaded)
            SetTab(t);
    }

    private async void SetTab(int tab)
    {
        _tab = tab;
        PrivacyBanner.Visibility = tab == 3 ? Visibility.Visible : Visibility.Collapsed;
        RecycleCard.Visibility = tab == 4 ? Visibility.Visible : Visibility.Collapsed;
        RulesHost.Visibility = tab == 4 ? Visibility.Collapsed : Visibility.Visible;
        FooterBar.Visibility = tab == 4 ? Visibility.Collapsed : Visibility.Visible;
        TabNoteText.Text = TabNotes[tab];

        if (tab == 4)
        {
            await RefreshRecycleAsync();
            return;
        }
        if (!_tabRows.ContainsKey(tab))
        {
            await ScanGroupAsync(tab);
        }
        else
        {
            RenderRows(tab);
        }
    }

    private IEnumerable<IScanRule> RulesOfTab(int tab) => tab switch
    {
        0 => _scanner.Rules.Where(r => r.Group == RuleGroup.SystemJunk),
        1 => _scanner.Rules.Where(r => r.Group == RuleGroup.AppCache),
        2 => _scanner.Rules.Where(r => r.Group == RuleGroup.BrowserCache),
        3 => _scanner.Rules.Where(r => r.Group == RuleGroup.Privacy),
        _ => Enumerable.Empty<IScanRule>(),
    };

    private async Task ScanGroupAsync(int tab)
    {
        _scanCts?.Cancel();
        _scanCts = new CancellationTokenSource();
        var ct = _scanCts.Token;
        GroupScanPanel.Visibility = Visibility.Visible;
        RulesHost.Visibility = Visibility.Collapsed;
        GroupScanText.Text = "正在扫描…";
        try
        {
            var rules = RulesOfTab(tab).ToList();
            var ctx = _scanner.BuildContext(ct);
            var results = await _scanner.ScanAsync(rules, ctx, null);
            ct.ThrowIfCancellationRequested();
            var pairs = rules.Zip(results, (rule, res) => (rule, res))
                             .Where(p => p.res.Error == null && p.res.TotalSize > 0)
                             .ToList();
            _tabResults[tab] = pairs;
            _tabRows[tab] = pairs.Select(p => new ScanRowVM(p.res, p.rule)).ToList();
        }
        catch (OperationCanceledException) { return; }
        catch (Exception ex)
        {
            _main.ShowToast("扫描失败：" + ex.Message, ToastType.Warning);
        }
        GroupScanPanel.Visibility = Visibility.Collapsed;
        RulesHost.Visibility = Visibility.Visible;
        if (_tab == tab) RenderRows(tab);
    }

    private void RenderRows(int tab)
    {
        RuleList.Children.Clear();
        var rows = _tabRows.GetValueOrDefault(tab) ?? new();
        bool found = rows.Count > 0;
        foreach (var vm in rows)
        {
            var handle = RuleRowFactory.Create(vm, this, UpdateSelectionSummary);
            RuleList.Children.Add(handle.Row);
        }
        EmptyGroupText.Visibility = found ? Visibility.Collapsed : Visibility.Visible;
        EmptyGroupText.Text = _tabResults.ContainsKey(tab)
            ? "这一类很干净，没有发现可清理的项目"
            : "点击下方「扫描本组」开始检查这一类项目";
        UpdateSelectionSummary();
    }

    private void UpdateSelectionSummary()
    {
        var rows = _tabRows.GetValueOrDefault(_tab) ?? new();
        int count = rows.Count(r => r.Selected);
        long bytes = rows.Where(r => r.Selected).Sum(r => r.Res.TotalSize);
        SelCountText.Text = $"{count} 项";
        SelSizeText.Text = SizeText.OfBytes(bytes);
    }

    private async void BtnScanGroup_Click(object sender, RoutedEventArgs e)
    {
        await ScanGroupAsync(_tab);
    }

    private async void BtnCleanSel_Click(object sender, RoutedEventArgs e)
    {
        var rows = (_tabRows.GetValueOrDefault(_tab) ?? new()).Where(r => r.Selected).ToList();
        if (rows.Count == 0)
        {
            _main.ShowToast("请先勾选要清理的项目", ToastType.Info);
            return;
        }
        string confirmMsg = $"即将清理 {rows.Count} 类项目，共 {SizeText.OfBytes(rows.Sum(r => r.Res.TotalSize))}。";
        if (_tab == 3) confirmMsg += "\n\n隐私项清理后无法恢复，请确认已了解各项影响。";
        confirmMsg += "\n可恢复类文件将进入隔离区。";
        if (!Services.DialogService.Confirm(_main, "确认清理所选项？", confirmMsg, "开始清理"))
            return;

        BtnCleanSel.IsEnabled = false;
        try
        {
            var svc = new CleanService();
            var requests = rows.Select(r => new CleanRequestRule
            {
                Rule = r.Rule,
                Result = r.Res,
                Selected = r.Res.Items.ToList(),
            }).ToList();
            var summary = await svc.CleanAsync(requests, CancellationToken.None, null);
            _main.ShowToast($"清理完成，释放 {SizeText.OfBytes(summary.FreedBytes)}" +
                            (summary.SkipCount > 0 ? $"（跳过 {summary.SkipCount} 项）" : ""),
                            summary.FailCount > 0 ? ToastType.Warning : ToastType.Success);
            _tabRows.Remove(_tab);
            _tabResults.Remove(_tab);
            await ScanGroupAsync(_tab);
        }
        catch (Exception ex)
        {
            _main.ShowToast("清理失败：" + ex.Message, ToastType.Warning);
        }
        finally
        {
            BtnCleanSel.IsEnabled = true;
        }
    }

    private async Task RefreshRecycleAsync()
    {
        RecycleInfoText.Text = "正在读取…";
        var (size, count) = await Task.Run(() => Core.Utils.ShellUtil.QueryRecycleBin());
        RecycleInfoText.Text = count > 0
            ? $"{count} 个项目，共占用 {SizeText.OfBytes(size)}"
            : "回收站是空的";
        BtnEmptyBin.IsEnabled = count > 0;
    }

    private async void BtnEmptyBin_Click(object sender, RoutedEventArgs e)
    {
        var (size, count) = Core.Utils.ShellUtil.QueryRecycleBin();
        if (count == 0) return;
        if (!Services.DialogService.Confirm(_main, "清空回收站？",
            $"回收站中的 {count} 个文件（共 {SizeText.OfBytes(size)}）将被永久删除，无法恢复。", "永久删除", "取消", danger: true))
            return;
        BtnEmptyBin.IsEnabled = false;
        var ok = await Task.Run(() => Core.Utils.ShellUtil.EmptyRecycleBin());
        Core.Store.HistoryService.Add("Clean", $"清空了回收站（{count} 个项目）", "", size);
        _main.ShowToast(ok ? "回收站已清空" : "清空失败", ok ? ToastType.Success : ToastType.Warning);
        await RefreshRecycleAsync();
    }

    private void BtnOpenBin_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start("explorer.exe", "shell:RecycleBinFolder");
        }
        catch { }
    }
}
