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
                DiskTitle.Text = $"磁盘空间 ({drive.Name.TrimEnd('\\')})";
                DiskTotalText.Text = $"总容量 {SizeText.OfBytes(drive.TotalSize, 0)}";
                DiskBar.Value = pct;
                DiskUsedText.Text = $"已使用 {SizeText.OfBytes(used, 0)} ({pct:F1}%)";
                DiskFreeText.Text = $"可用 {SizeText.OfBytes(drive.AvailableFreeSpace, 0)} ({100 - pct:F1}%)";
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
            StatusTitle.Text = "磁盘空间紧张";
            StatusEllipse.Fill = (Brush)FindResource("DangerSoftBrush");
            StatusIcon.Stroke = (Brush)FindResource("DangerBrush");
            LastScanText.Text = "系统盘剩余空间不足 10%，建议立即清理";
        }
        else if (junkBytes > 3L * 1024 * 1024 * 1024 || highImpact > 0)
        {
            StatusTitle.Text = "建议清理一下";
            StatusEllipse.Fill = (Brush)FindResource("WarningSoftBrush");
            StatusIcon.Data = (Geometry)FindResource("WarningIcon");
            StatusIcon.Stroke = (Brush)FindResource("WarningBrush");
            LastScanText.Text = $"发现 {SizeText.OfBytes(junkBytes)} 可清理垃圾与 {highImpact} 个高影响启动项";
        }
        else
        {
            StatusTitle.Text = "设备状态良好";
            StatusEllipse.Fill = (Brush)FindResource("SuccessSoftBrush");
            StatusIcon.Data = (Geometry)FindResource("CheckIcon");
            StatusIcon.Stroke = (Brush)FindResource("SuccessBrush");
            LastScanText.Text = last != null
                ? $"上次扫描：{FormatTime(last.Time)}"
                : "还没有扫描过，点击「立即扫描」开始";
        }

        // —— 卡片数字 ——
        if (last != null && junkBytes > 0)
        {
            JunkSizeText.Text = SizeText.OfBytes(junkBytes);
            JunkNoteText.Text = $"上次扫描 {FormatTime(last.Time)}";
        }
        StartupCountText.Text = highImpact > 0 ? $"{highImpact} 项" : startupCount.ToString();
        StartupNoteText.Text = highImpact > 0 ? "高影响启动项，可能拖慢开机" : "个启动项随开机运行";
        if (disk != null && disk.Categories.Count > 0)
        {
            LargeCountText.Text = "已分析";
            LargeNoteText.Text = "点击查看大文件详情";
        }
        else
        {
            LargeCountText.Text = "待分析";
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
                    Name = r.Name,
                    Note = "可安全清理，不影响系统使用",
                    SizeText = "约 " + SizeText.OfBytes(r.Size),
                    ActionText = "立即清理",
                    RuleId = r.Id,
                });
            }
        }
        if (highImpact > 0)
        {
            suggests.Add(new SuggestItem
            {
                Name = "开机启动建议",
                Note = $"发现 {highImpact} 个高影响启动项，建议优化",
                SizeText = $"{highImpact} 项",
                ActionText = "去优化",
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
                    Text = c.Name,
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
        if (d.TotalMinutes < 1) return "刚刚";
        if (d.TotalHours < 1) return $"{(int)d.TotalMinutes} 分钟前";
        if (d.TotalDays < 1) return $"今天 {t:HH:mm}";
        if (d.TotalDays < 2) return $"昨天 {t:HH:mm}";
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
        AddItem("清理空间", PageKey.CleanSpace);
        AddItem("空间分析", PageKey.SpaceAnalysis);
        AddItem("启动项管理", PageKey.Startup);
        AddItem("应用管理", PageKey.Apps);
        AddItem("工具箱", PageKey.Toolbox);
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
        var ok = DialogService.Confirm(_main, $"清理{rule.Name}？",
            $"预计可释放 {item.SizeText}。\n可恢复类文件会进入隔离区。", "立即清理");
        if (!ok) return;
        BtnScan.IsEnabled = false;
        try
        {
            var svc = new QuickCleanHelper(_main);
            var freed = await svc.CleanSingleRuleAsync(rule);
            if (freed >= 0)
            {
                _main.ShowToast($"已清理，释放了 {SizeText.OfBytes(freed)}");
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
            _main.ShowToast("没有发现可清理的内容", ToastType.Info);
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
