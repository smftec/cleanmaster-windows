using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using CleanMaster.App.Controls;
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
    private readonly SystemMonitorService _monitor = new();
    private DispatcherTimer? _monitorTimer;
    private RingGauge? _diskRing;
    private RingGauge? _cpuRing;
    private RingGauge? _ramRing;
    private TextBlock? _diskCenter;
    private TextBlock? _cpuCenter;
    private TextBlock? _ramCenter;
    private TextBlock? _cpuDetail;
    private TextBlock? _ramDetail;
    private TextBlock? _uptimeText;
    private RingGauge? _healthRing;
    private TextBlock? _healthScoreText;

    public HomePage(MainWindow main)
    {
        InitializeComponent();
        _main = main;
        BuildDiskRing();
        BuildMonitor();
        BuildHealth();
        Loaded += (s, e) => { _ = RefreshAsync(); StartMonitor(); };
        Unloaded += (s, e) => StopMonitor();
    }

    private bool _scrollToStats;

    public void OnNavigated(object? param)
    {
        _scrollToStats = param as string == "stats";
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
                if (_diskRing != null)
                {
                    _diskRing.ArcBrush = pct >= 90 ? (Brush)FindResource("DangerBrush")
                        : pct >= 75 ? (Brush)FindResource("WarningBrush")
                        : (Brush)FindResource("PrimaryBrush");
                    _diskRing.AnimateTo(pct);
                }
                if (_diskCenter != null) _diskCenter.Text = pct.ToString("F0") + "%";
                DiskUsedText.Text = string.Format(Services.Loc.T("home.disk.used.f"), SizeText.OfBytes(used, 0), pct);
                DiskFreeText.Text = string.Format(Services.Loc.T("home.disk.free.f"), SizeText.OfBytes(drive.AvailableFreeSpace, 0), 100 - pct);
            }
        }
        catch { }

        // —— 缓存数据 ——
        var last = ScanCache.Load();
        var disk = DiskCache.Load(SystemDrive());

        // —— 启动项 + 已装应用（缓存枚举,并行） ——
        int highImpact = 0, startupCount = 0, appCount = 0;
        try
        {
            var startupTask = Task.Run(() => Services.EnumerationCache.GetStartup());
            var appsTask = Task.Run(() => Services.EnumerationCache.GetApps());
            await Task.WhenAll(startupTask, appsTask);
            var items = startupTask.Result;
            startupCount = items.Count(i => i.Enabled && i.CanToggle);
            highImpact = items.Count(i => i.Enabled && i.Impact == "高");
            appCount = appsTask.Result.Count;
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

        // —— 健康评分 ——
        ApplyHealth(junkBytes, highImpact, freePct);

        // —— 卡片数字（滚动动画） ——
        if (last != null && junkBytes > 0)
        {
            CountUp.Animate(JunkSizeText, junkBytes, v => SizeText.OfBytes((long)v));
            JunkNoteText.Text = string.Format(Services.Loc.T("home.card.junk.lastscan"), FormatTime(last.Time));
        }
        if (highImpact > 0)
        {
            CountUp.Animate(StartupCountText, highImpact,
                v => string.Format(Services.Loc.T("common.n.items"), (int)v));
            StartupNoteText.Text = Services.Loc.T("home.card.startup.note.high");
        }
        else
        {
            CountUp.Animate(StartupCountText, startupCount, v => ((int)v).ToString());
            StartupNoteText.Text = Services.Loc.T("home.card.startup.note");
        }
        if (disk != null && disk.Categories.Count > 0)
        {
            LargeCountText.Text = Services.Loc.T("home.card.large.analyzed");
            LargeNoteText.Text = Services.Loc.T("home.card.large.analyzed.note");
        }
        else
        {
            LargeCountText.Text = Services.Loc.T("home.card.large.pending");
        }
        if (appCount > 0)
            CountUp.Animate(AppsCountText, appCount, v => ((int)v).ToString());
        else
            AppsCountText.Text = "—";

        // —— 空间分析色块 ——
        ApplyDiskCategories(disk);

        // —— 最近活动 ——
        var acts = HistoryService.LoadAll().Take(4).Select(a => new
        {
            a.Title,
            TimeText = FormatTime(a.Time),
        }).ToList();
        ActivityList.ItemsSource = acts;
        NoActivityPanel.Visibility = acts.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        // —— 清理统计（累计 + 近 7 日） ——
        BuildStats();
        if (_scrollToStats)
        {
            _scrollToStats = false;
            await Task.Delay(100);
            StatsBars.BringIntoView();
        }

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

    // ————— 环形仪表 + 实时监控 —————

    private void BuildDiskRing()
    {
        _diskRing = new RingGauge(108, 10);
        _diskCenter = new TextBlock
        {
            FontSize = 19,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)FindResource("TextPrimaryBrush"),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        var center = new StackPanel();
        center.Children.Add(_diskCenter);
        center.Children.Add(new TextBlock
        {
            Text = Services.Loc.T("home.disk.center"),
            FontSize = 10.5,
            Foreground = (Brush)FindResource("TextTertiaryBrush"),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 1, 0, 0),
        });
        _diskRing.Center = center;
        DiskRingHost.Children.Add(_diskRing);
    }

    private void BuildMonitor()
    {
        Brush Primary(string key) => (Brush)FindResource(key);

        StackPanel MetricBlock(RingGauge ring, TextBlock center, string label, out TextBlock detail)
        {
            ring.Center = center;
            detail = new TextBlock
            {
                FontSize = 11.5,
                Foreground = (Brush)FindResource("TextTertiaryBrush"),
                Margin = new Thickness(0, 3, 0, 0),
            };
            var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            sp.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 12.5,
                FontWeight = FontWeights.Bold,
                Foreground = (Brush)FindResource("TextPrimaryBrush"),
            });
            sp.Children.Add(detail);
            var block = new StackPanel { Orientation = Orientation.Horizontal };
            block.Children.Add(ring);
            block.Children.Add(new Border { Width = 12 });
            block.Children.Add(sp);
            return block;
        }

        _cpuRing = new RingGauge(58, 7) { ArcBrush = Primary("PrimaryBrush") };
        _cpuCenter = new TextBlock
        {
            FontSize = 12.5,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)FindResource("TextPrimaryBrush"),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        CpuHost.Children.Add(MetricBlock(_cpuRing, _cpuCenter, Services.Loc.T("home.mon.cpu"), out _cpuDetail));

        _ramRing = new RingGauge(58, 7) { ArcBrush = Primary("TealBrush") };
        _ramCenter = new TextBlock
        {
            FontSize = 12.5,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)FindResource("TextPrimaryBrush"),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        RamHost.Children.Add(MetricBlock(_ramRing, _ramCenter, Services.Loc.T("home.mon.ram"), out _ramDetail));

        var up = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        up.Children.Add(new TextBlock
        {
            Text = Services.Loc.T("home.mon.uptime"),
            FontSize = 12.5,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)FindResource("TextPrimaryBrush"),
        });
        _uptimeText = new TextBlock
        {
            FontSize = 11.5,
            Foreground = (Brush)FindResource("TextTertiaryBrush"),
            Margin = new Thickness(0, 3, 0, 0),
        };
        up.Children.Add(_uptimeText);
        UptimeHost.Children.Add(up);

        // 实时呼吸点
        var pulse = new DoubleAnimation(0.35, 1, TimeSpan.FromSeconds(1.6))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut },
        };
        LiveDot.BeginAnimation(OpacityProperty, pulse);
    }

    // ————— 健康评分 —————

    private void BuildHealth()
    {
        _healthRing = new RingGauge(118, 12);
        _healthScoreText = new TextBlock
        {
            FontSize = 30,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)FindResource("TextPrimaryBrush"),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        var center = new StackPanel();
        center.Children.Add(_healthScoreText);
        center.Children.Add(new TextBlock
        {
            Text = Services.Loc.T("home.health.unit"),
            FontSize = 10.5,
            Foreground = (Brush)FindResource("TextTertiaryBrush"),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 1, 0, 0),
        });
        _healthRing.Center = center;
        HealthRingHost.Children.Add(_healthRing);
    }

    private void ApplyHealth(long junkBytes, int highImpact, double freePct)
    {
        long binBytes = 0;
        try { (binBytes, _) = ShellUtil.QueryRecycleBin(); } catch { }
        var r = Core.Analysis.HealthScoreService.Compute(junkBytes, highImpact, freePct, binBytes);

        if (_healthRing != null && _healthScoreText != null)
        {
            _healthRing.ArcBrush = (Brush)FindResource(r.BrushKey);
            _healthRing.AnimateTo(r.Score);
            CountUp.Animate(_healthScoreText, r.Score, v => ((int)v).ToString());
        }
        HealthLevelText.Text = Services.Loc.T(r.LevelKey);
        HealthLevelText.Foreground = (Brush)FindResource(r.BrushKey);

        HealthChips.Children.Clear();
        void Chip(string text)
        {
            var bd = new Border
            {
                Background = (Brush)FindResource("CardBgBrush"),
                BorderBrush = (Brush)FindResource("CardBorderBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(9, 3, 9, 4),
                Margin = new Thickness(0, 4, 6, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            bd.Child = new TextBlock
            {
                Text = text,
                FontSize = 10.5,
                Foreground = (Brush)FindResource("TextSecondaryBrush"),
            };
            HealthChips.Children.Add(bd);
        }
        Chip(string.Format(Services.Loc.T("home.health.junk"), SizeText.OfBytes(junkBytes)));
        Chip(string.Format(Services.Loc.T("home.health.high"), highImpact));
        Chip(string.Format(Services.Loc.T("home.health.free"), freePct));
    }

    private void Health_Click(object sender, RoutedEventArgs e) => _main.Navigate(PageKey.SmartScan, "start");

    // ————— 清理统计 —————

    private void BuildStats()
    {
        var hist = HistoryService.LoadAll();
        long totalFreed = hist.Where(h => h.Bytes > 0).Sum(h => h.Bytes);
        int cleanCount = hist.Count(h => h.Type is "Clean" or "AutoClean");
        StatsTotalText.Text = string.Format(Services.Loc.T("home.stats.total"), SizeText.OfBytes(totalFreed, 0), cleanCount);

        StatsBars.Children.Clear();
        var days = new (DateTime day, long bytes)[7];
        for (int i = 0; i < 7; i++)
        {
            var d = DateTime.Today.AddDays(i - 6);
            days[i] = (d, hist.Where(h => h.Time.Date == d && h.Bytes > 0).Sum(h => h.Bytes));
        }
        long max = Math.Max(1, days.Max(d => d.bytes));
        foreach (var (day, bytes) in days)
        {
            var cell = new Grid { Margin = new Thickness(0, 0, 7, 0) };
            cell.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            cell.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            var bar = new Border
            {
                Height = Math.Max(3, 46.0 * bytes / max),
                CornerRadius = new CornerRadius(3),
                Background = bytes > 0
                    ? (day == DateTime.Today ? (Brush)FindResource("PrimaryBrush") : BrushWithOpacity("PrimaryBrush", 0.45))
                    : (Brush)FindResource("TrackBrush"),
                VerticalAlignment = VerticalAlignment.Bottom,
                ToolTip = $"{day:MM-dd} · {(bytes > 0 ? SizeText.OfBytes(bytes, 0) : "0 B")}",
            };
            cell.Children.Add(bar);
            StatsBars.Children.Add(cell);
        }
    }

    private Brush BrushWithOpacity(string key, double opacity)
    {
        var b = ((Brush)FindResource(key)).Clone();
        b.Opacity = opacity;
        return b;
    }

    private void StartMonitor()
    {
        StopMonitor();
        _monitorTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.2) };
        _monitorTimer.Tick += (s, e) => UpdateMonitor();
        _monitorTimer.Start();
        UpdateMonitor();
    }

    private void StopMonitor() => _monitorTimer?.Stop();

    private void UpdateMonitor()
    {
        try
        {
            var snap = _monitor.Sample();
            var cpu = snap.CpuPercent;
            if (_cpuRing != null && _cpuCenter != null)
            {
                // CPU 环随负载变色,与内存环状态语义一致
                if (cpu is >= 90) _cpuRing.ArcBrush = (Brush)FindResource("DangerBrush");
                else if (cpu is >= 75) _cpuRing.ArcBrush = (Brush)FindResource("WarningBrush");
                else _cpuRing.ArcBrush = (Brush)FindResource("PrimaryBrush");
                _cpuRing.SetPercent(cpu ?? 0);
                _cpuCenter.Text = cpu.HasValue ? cpu.Value.ToString("F0") + "%" : "—";
            }
            if (_cpuDetail != null)
                _cpuDetail.Text = string.Format(Services.Loc.T("home.mon.cores"), Environment.ProcessorCount);

            var ramPct = snap.RamPercent;
            if (_ramRing != null && _ramCenter != null && _ramDetail != null)
            {
                _ramRing.ArcBrush = ramPct >= 90 ? (Brush)FindResource("DangerBrush")
                    : ramPct >= 75 ? (Brush)FindResource("WarningBrush")
                    : (Brush)FindResource("TealBrush");
                _ramRing.SetPercent(ramPct);
                _ramCenter.Text = ramPct.ToString("F0") + "%";
                _ramDetail.Text = string.Format(Services.Loc.T("home.mon.ram.f"),
                    SizeText.OfBytes(snap.RamUsedBytes, 0), SizeText.OfBytes(snap.RamTotalBytes, 0));
            }
            if (_uptimeText != null)
                _uptimeText.Text = FormatUptime(snap.Uptime);
        }
        catch { }
    }

    private static string FormatUptime(TimeSpan d)
    {
        if (d.TotalDays >= 1) return string.Format(Services.Loc.T("home.mon.uptime.d"), (int)d.TotalDays, d.Hours);
        if (d.TotalHours >= 1) return string.Format(Services.Loc.T("home.mon.uptime.h"), (int)d.TotalHours, d.Minutes);
        return string.Format(Services.Loc.T("home.mon.uptime.m"), Math.Max(1, (int)d.TotalMinutes));
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
