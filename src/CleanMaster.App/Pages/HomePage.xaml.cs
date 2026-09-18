using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using CleanMaster.App.Services;
using CleanMaster.Core.Analysis;
using CleanMaster.Core.Apps;
using CleanMaster.Core.Models;
using CleanMaster.Core.Rules;
using CleanMaster.Core.Store;
using CleanMaster.Core.Startup;
using CleanMaster.Core.Utils;

namespace CleanMaster.App.Pages;

public partial class HomePage : Page, IParamPage
{
    private readonly MainWindow _main;

    public HomePage(MainWindow main)
    {
        InitializeComponent();
        _main = main;
        Loaded += (s, e) => _ = RefreshAsync();
    }

    public void OnNavigated(object? param)
    {
        _ = RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        // —— 磁盘 ——
        try
        {
            var sysDrive = DriveInfo.GetDrives()
                .FirstOrDefault(d => d.IsReady && d.DriveType == DriveType.Fixed &&
                                     d.RootDirectory.FullName.StartsWith(Path.GetTempPath().Substring(0, 3)));
            var all = DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed).ToList();
            var drive = sysDrive ?? all.FirstOrDefault();
            if (drive != null)
            {
                var used = drive.TotalSize - drive.AvailableFreeSpace;
                var pct = drive.TotalSize > 0 ? used * 100.0 / drive.TotalSize : 0;
                DiskTitle.Text = string.Format(Services.Loc.T("home.disk.title.disk"), drive.Name.TrimEnd('\\'));
                DiskTotalText.Text = string.Format(Services.Loc.T("home.disk.total.f"), SizeText.OfBytes(drive.TotalSize, 0));
                DiskBar.Value = pct;
                DiskUsedText.Text = string.Format(Services.Loc.T("home.disk.used.f"), SizeText.OfBytes(used, 0), pct);
                DiskFreeText.Text = string.Format(Services.Loc.T("home.disk.free.f"), SizeText.OfBytes(drive.AvailableFreeSpace, 0), 100 - pct);
            }
        }
        catch { }

        // —— 缓存数据 ——
        var last = ScanCache.Load();
        var disk = DiskCache.Load(SystemDrive());

        // —— 启动项（轻量枚举） ——
        int highImpact = 0, startupCount = 0;
        try
        {
            var items = await Task.Run(() => new StartupService().Enumerate());
            startupCount = items.Count(i => i.Enabled && i.CanToggle);
            highImpact = items.Count(i => i.Enabled && i.Impact == "高");
        }
        catch { }

        // —— 大文件（上次分析缓存） ——
        // —— 已装应用 ——
        int appCount = 0;
        try
        {
            appCount = await Task.Run(() => new AppInventoryService().EnumerateWin32().Count);
        }
        catch { }

        // —— 状态标题 ——
        long junkBytes = last?.TotalBytes ?? 0;
        double freePct = 100;
        try
        {
            var drive = DriveInfo.GetDrives().First(d => d.IsReady && d.Name.StartsWith(Path.GetTempPath().Substring(0, 3)));
            freePct = drive.AvailableFreeSpace * 100.0 / drive.TotalSize;
        }
        catch { }

        if (freePct < 10)
        {
            StatusTitle.Text = Services.Loc.T("home.status.tight");
            StatusEllipse.Fill = (Brush)FindResource("DangerSoftBrush");
            StatusIcon.Stroke = (Brush)FindResource("DangerBrush");
            LastScanText.Text = Services.Loc.T("home.status.tight.detail");
        }
        else if (junkBytes > 3L * 1024 * 1024 * 1024 || highImpact > 0)
        {
            StatusTitle.Text = Services.Loc.T("home.status.suggest");
            StatusEllipse.Fill = (Brush)FindResource("WarningSoftBrush");
            StatusIcon.Data = (Geometry)FindResource("WarningIcon");
            StatusIcon.Stroke = (Brush)FindResource("WarningBrush");
            LastScanText.Text = string.Format(Services.Loc.T("home.status.suggest.detail"), SizeText.OfBytes(junkBytes), highImpact);
        }
        else
        {
            StatusTitle.Text = Services.Loc.T("home.status.good");
            StatusEllipse.Fill = (Brush)FindResource("SuccessSoftBrush");
            StatusIcon.Data = (Geometry)FindResource("CheckIcon");
            StatusIcon.Stroke = (Brush)FindResource("SuccessBrush");
            LastScanText.Text = last != null
                ? string.Format(Services.Loc.T("home.status.lastscan"), FormatTime(last.Time))
                : Services.Loc.T("home.noscan.detail");
        }

        // —— 卡片数字 ——
        if (last != null && junkBytes > 0)
        {
            JunkSizeText.Text = SizeText.OfBytes(junkBytes);
            JunkNoteText.Text = string.Format(Services.Loc.T("home.card.junk.lastscan"), FormatTime(last.Time));
        }
        StartupCountText.Text = highImpact > 0 ? string.Format(Services.Loc.T("common.n.items"), highImpact) : startupCount.ToString();
        StartupNoteText.Text = highImpact > 0 ? Services.Loc.T("home.card.startup.note.high") : Services.Loc.T("home.card.startup.note");
        if (disk != null && disk.Categories.Count > 0)
        {
            LargeCountText.Text = Services.Loc.T("home.card.large.analyzed");
            LargeNoteText.Text = Services.Loc.T("home.card.large.analyzed.note");
        }
        else
        {
            LargeCountText.Text = Services.Loc.T("home.card.large.pending");
        }
        AppsCountText.Text = appCount > 0 ? appCount.ToString() : "—";

        // —— 空间分析色块 ——
        ApplyDiskCategories(disk);

        // —— 最近活动 ——
        var acts = HistoryService.LoadAll().Take(4).Select(a => new
        {
            a.Title,
            TimeText = FormatTime(a.Time),
        }).ToList();
        ActivityList.ItemsSource = acts;
        NoActivityText.Visibility = acts.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        // —— 智能建议 ——
        var suggests = new List<SuggestItem>();
        if (last != null)
        {
            foreach (var r in last.Rules.Where(r => r.Size > 200 * 1024 * 1024).OrderByDescending(r => r.Size).Take(2))
            {
                suggests.Add(new SuggestItem
                {
                    Name = Services.Loc.RuleName(r.Id, r.Name),
                    Note = Services.Loc.T("home.suggest.note.safe"),
                    SizeText = Services.Loc.T("common.approx") + SizeText.OfBytes(r.Size),
                    ActionText = Services.Loc.T("home.suggest.clean"),
                    RuleId = r.Id,
                });
            }
        }
        if (highImpact > 0)
        {
            suggests.Add(new SuggestItem
            {
                Name = Services.Loc.T("home.suggest.startup.title"),
                Note = string.Format(Services.Loc.T("home.suggest.startup.note"), highImpact),
                SizeText = string.Format(Services.Loc.T("common.n.items"), highImpact),
                ActionText = Services.Loc.T("home.suggest.optimize"),
                IsStartup = true,
            });
        }
        SuggestList.ItemsSource = suggests;
        NoSuggestText.Visibility = suggests.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private static string SystemDrive()
    {
        var tmp = Path.GetTempPath();
        return tmp.Substring(0, 3);
    }

    private void ApplyDiskCategories(DiskSummary? disk)
    {
        var borders = new[] { Cat0, Cat1, Cat2, Cat3, Cat4, Cat5 };
        if (disk == null || disk.Categories.Count == 0)
        {
            AnalysisEmpty.Visibility = Visibility.Visible;
            foreach (var b in borders) b.Opacity = 0.12;
            return;
        }
        AnalysisEmpty.Visibility = Visibility.Collapsed;
        var top = disk.Categories.Take(6).ToList();
        for (int i = 0; i < borders.Length; i++)
        {
            var bd = borders[i];
            bd.Opacity = 1;
            bd.Child = null;
            if (i < top.Count)
            {
                var c = top[i];
                var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 8, 8, 8) };
                sp.Children.Add(new TextBlock
                {
                    Text = Services.Loc.CategoryDisplay(c.Name),
                    FontSize = 12.5,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Colors.White),
                });
                sp.Children.Add(new TextBlock
                {
                    Text = SizeText.OfBytes(c.Bytes, 0),
                    FontSize = 15,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Colors.White),
                    Margin = new Thickness(0, 3, 0, 0),
                });
                sp.Children.Add(new TextBlock
                {
                    Text = $"{c.Percent:F1}%",
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromArgb(210, 255, 255, 255)),
                    Margin = new Thickness(0, 2, 0, 0),
                });
                bd.Background = (Brush)new BrushConverter().ConvertFromString(c.Hex)!;
                bd.Child = sp;
            }
            else
            {
                bd.Opacity = 0.12;
            }
        }
    }

    internal static string FormatTime(DateTime t)
    {
        var d = DateTime.Now - t;
        if (d.TotalMinutes < 1) return Services.Loc.T("time.now");
        if (d.TotalHours < 1) return string.Format(Services.Loc.T("time.minago"), (int)d.TotalMinutes);
        if (d.TotalDays < 1) return string.Format(Services.Loc.T("time.today"), t.ToString("HH:mm"));
        if (d.TotalDays < 2) return string.Format(Services.Loc.T("time.yesterday"), t.ToString("HH:mm"));
        return t.ToString("MM-dd HH:mm");
    }

    // ————— 事件 —————

    private void BtnScan_Click(object sender, RoutedEventArgs e)
    {
        _main.Navigate(PageKey.SmartScan, "start");
    }

    private void BtnMore_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu();
        void AddItem(string text, PageKey key)
        {
            var mi = new MenuItem { Header = text };
            mi.Click += (s, a) => _main.Navigate(key);
            menu.Items.Add(mi);
        }
        AddItem(Services.Loc.T("nav.clean"), PageKey.CleanSpace);
        AddItem(Services.Loc.T("nav.space"), PageKey.SpaceAnalysis);
        AddItem(Services.Loc.T("nav.startup"), PageKey.Startup);
        AddItem(Services.Loc.T("nav.apps"), PageKey.Apps);
        AddItem(Services.Loc.T("nav.toolbox"), PageKey.Toolbox);
        menu.PlacementTarget = BtnMore;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private void CardJunk_Click(object sender, RoutedEventArgs e) => _main.Navigate(PageKey.CleanSpace);
    private void CardStartup_Click(object sender, RoutedEventArgs e) => _main.Navigate(PageKey.Startup);
    private void CardLarge_Click(object sender, RoutedEventArgs e) => _main.Navigate(PageKey.SpaceAnalysis, "large");
    private void CardApps_Click(object sender, RoutedEventArgs e) => _main.Navigate(PageKey.Apps);
    private void BtnAnalyze_Click(object sender, RoutedEventArgs e) => _main.Navigate(PageKey.SpaceAnalysis, "analyze");
    private void BtnAnalyze2_Click(object sender, RoutedEventArgs e) => _main.Navigate(PageKey.SpaceAnalysis);
    private void BtnHistory_Click(object sender, RoutedEventArgs e) => _main.Navigate(PageKey.Toolbox, "history");

    private async void SuggestAction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: SuggestItem item }) return;
        if (item.IsStartup)
        {
            _main.Navigate(PageKey.Startup);
            return;
        }
        if (item.RuleId == null) return;

        // 单规则快速清理（确认后执行）
        var scanner = new ScannerService();
        var rule = scanner.Rules.FirstOrDefault(r => r.Id == item.RuleId);
        if (rule == null) return;
        var ok = DialogService.Confirm(_main, string.Format(Services.Loc.T("dlg.quickclean.title"), Services.Loc.RuleName(rule.Id, rule.Name)),
            string.Format(Services.Loc.T("dlg.quickclean.msg"), item.SizeText), Services.Loc.T("home.suggest.clean"));
        if (!ok) return;
        BtnScan.IsEnabled = false;
        try
        {
            var svc = new QuickCleanHelper(_main);
            var freed = await svc.CleanSingleRuleAsync(rule);
            if (freed >= 0)
            {
                _main.ShowToast(string.Format(Services.Loc.T("toast.cleaned"), SizeText.OfBytes(freed)));
                await RefreshAsync();
            }
        }
        finally
        {
            BtnScan.IsEnabled = true;
        }
    }
}

public sealed class SuggestItem
{
    public string Name { get; set; } = "";
    public string Note { get; set; } = "";
    public string SizeText { get; set; } = "";
    public string ActionText { get; set; } = "立即清理";
    public string? RuleId { get; set; }
    public bool IsStartup { get; set; }
}

/// <summary>单规则快速清理（首页建议 / 清理空间页共用）。</summary>
public sealed class QuickCleanHelper
{
    private readonly MainWindow _main;
    public QuickCleanHelper(MainWindow main) => _main = main;

    /// <summary>返回释放字节；取消返回 -1。</summary>
    public async Task<long> CleanSingleRuleAsync(IScanRule rule)
    {
        var scanner = new ScannerService();
        var ctx = scanner.BuildContext(CancellationToken.None);
        var results = await scanner.ScanAsync(new[] { rule }, ctx, null);
        var res = results[0];
        if (res.Items.Count == 0)
        {
            _main.ShowToast(Services.Loc.T("toast.nothing"), ToastType.Info);
            return -1;
        }
        var svc = new CleanService();
        var progress = new Progress<CleanProgress>(p => { });
        var summary = await svc.CleanAsync(
        [
            new CleanRequestRule { Rule = rule, Result = res, Selected = res.Items.ToList() },
        ], CancellationToken.None, progress);
        if (summary.FailCount == 0 && summary.SkipCount == 0)
            return summary.FreedBytes;
        return summary.FreedBytes;
    }
}
