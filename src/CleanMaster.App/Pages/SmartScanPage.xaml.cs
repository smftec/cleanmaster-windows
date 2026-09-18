using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using CleanMaster.App.Services;
using CleanMaster.Core.Models;
using CleanMaster.Core.Rules;
using CleanMaster.Core.Store;
using CleanMaster.Core.Utils;

namespace CleanMaster.App.Pages;

public partial class SmartScanPage : Page, IParamPage
{
    private readonly MainWindow _main;
    private CancellationTokenSource? _scanCts;
    private CancellationTokenSource? _cleanCts;
    private List<ScanRowVM>? _rows;
    private bool _scanning;
    private bool _cleaning;

    public SmartScanPage(MainWindow main)
    {
        InitializeComponent();
        _main = main;
        var spin = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(1.1));
        spin.RepeatBehavior = RepeatBehavior.Forever;
        SpinTransform.BeginAnimation(RotateTransform.AngleProperty, spin);
        var spin2 = new DoubleAnimation(0, -360, TimeSpan.FromSeconds(1.6));
        spin2.RepeatBehavior = RepeatBehavior.Forever;
        CleanSpinTransform.BeginAnimation(RotateTransform.AngleProperty, spin2);
    }

    public void OnNavigated(object? param)
    {
        if (param as string == "start" && !_scanning && !_cleaning)
            StartScanFromUi();
    }

    public void StartScanFromUi() => StartScan();

    // ————— 扫描 —————

    private async void StartScan()
    {
        if (_scanning || _cleaning) return;
        _scanning = true;
        _scanCts = new CancellationTokenSource();
        Show(PanelScanning);
        ScanStageText.Text = Services.Loc.T("scan.preparing");
        ScanDetailText.Text = "";
        ScanProgressBar.Value = 0;
        ScanPercentText.Text = "0%";
        ScanFoundText.Text = Services.Loc.T("scan.found");

        var scanner = new ScannerService();
        var ctx = scanner.BuildContext(_scanCts.Token, detail =>
        {
            Dispatcher.Invoke(() => ScanDetailText.Text = detail);
        });

        var lastBytes = 0L;
        var progress = new Progress<ScanProgress>(p =>
        {
            ScanStageText.Text = Services.Loc.RuleName(ctx != null ? "" : "", p.Stage) == p.Stage ? p.Stage : p.Stage;
            ScanDetailText.Text = p.Stage switch
            {
                "应用缓存" => "正在分析应用缓存目录",
                _ => p.Detail,
            };
            ScanProgressBar.Value = p.RuleTotal > 0 ? p.RuleIndex * 100.0 / p.RuleTotal : 0;
            ScanPercentText.Text = $"{(int)ScanProgressBar.Value}%";
            if (p.FoundBytes != lastBytes)
            {
                lastBytes = p.FoundBytes;
                ScanFoundText.Text = $"已发现 {SizeText.OfBytes(p.FoundBytes)}";
            }
        });

        List<RuleResult> results;
        List<IScanRule> scannedRules;
        try
        {
            scannedRules = scanner.Rules.Where(r =>
                r.Group is RuleGroup.SystemJunk or RuleGroup.BrowserCache or RuleGroup.AppCache
                or RuleGroup.RecycleBin).ToList();
            results = await scanner.ScanAsync(scannedRules, ctx, progress);
        }
        catch (OperationCanceledException)
        {
            _scanning = false;
            Show(PanelIdle);
            _main.ShowToast(Services.Loc.T("toast.scanstopped"), ToastType.Info);
            return;
        }
        catch (Exception ex)
        {
            _scanning = false;
            Show(PanelIdle);
            _main.ShowToast(Services.Loc.T("toast.scanfailed") + ex.Message, ToastType.Warning);
            return;
        }
        _scanning = false;

        BuildResultView(scannedRules, results);
        Show(PanelResults);
    }

    private void BuildResultView(List<IScanRule> rules, List<RuleResult> results)
    {
        ResultGroups.Children.Clear();
        _rows = new List<ScanRowVM>();
        var pairs = rules.Zip(results, (rule, res) => (rule, res)).Where(p => p.res.Error == null && p.res.TotalSize > 0).ToList();

        // 缓存摘要供首页使用
        ScanCache.Save(new LastScanSummary
        {
            Time = DateTime.Now,
            TotalBytes = results.Sum(r => r.TotalSize),
            Rules = results.Where(r => r.Error == null).Select(r => new LastScanSummary.RuleSummary
            {
                Id = r.RuleId, Name = r.Name, Group = (int)r.Group, Risk = (int)r.Risk,
                Size = r.TotalSize, Count = r.FileCount,
            }).ToList(),
        });

        foreach (var (title, note, predicate) in new (string, string, Func<(IScanRule rule, RuleResult res), bool>)[]
        {
            (Services.Loc.T("scan.group.safe"), Services.Loc.T("scan.group.safe.note"),
                p => p.res.Group == RuleGroup.SystemJunk),
            (Services.Loc.T("scan.group.cache"), Services.Loc.T("scan.group.cache.note"),
                p => p.res.Group is RuleGroup.BrowserCache or RuleGroup.AppCache),
            (Services.Loc.T("scan.group.check"), Services.Loc.T("scan.group.check.note"),
                p => p.res.Group == RuleGroup.RecycleBin),
        })
        {
            var groupRules = pairs.Where(predicate).ToList();
            if (groupRules.Count == 0) continue;

            var section = BuildSection(title, note, groupRules);
            ResultGroups.Children.Add(section);
        }

        long total = results.Where(r => r.Error == null).Sum(r => r.TotalSize);
        ResultTitleText.Text = total > 0 ? string.Format(Services.Loc.T("result.title.found"), SizeText.OfBytes(total)) : Services.Loc.T("result.title.clean");
        ResultSubText.Text = string.Format(Services.Loc.T("result.sub"), DateTime.Now.ToString("yyyy-MM-dd HH:mm"), results.Count);
        SelCountText.Text = "0";
        SelSizeText.Text = "0 B";
    }

    private StackPanel BuildSection(string title, string note, List<(IScanRule rule, RuleResult res)> rules)
    {
        var sp = new StackPanel { Margin = new Thickness(0, 0, 0, 6) };
        sp.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 14.5,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)FindResource("TextPrimaryBrush"),
        });
        sp.Children.Add(new TextBlock
        {
            Text = note,
            FontSize = 11.5,
            Foreground = (Brush)FindResource("TextTertiaryBrush"),
            Margin = new Thickness(0, 3, 0, 8),
        });
        var card = new Border
        {
            Style = (Style)FindResource("CardBorder"),
            Padding = new Thickness(8, 6, 8, 6),
        };
        var list = new StackPanel();
        foreach (var (rule, res) in rules)
        {
            var vm = new ScanRowVM(res, rule);
            _rows!.Add(vm);
            list.Children.Add(BuildRuleRow(vm));
        }
        card.Child = list;
        sp.Children.Add(card);
        return sp;
    }

    private Border BuildRuleRow(ScanRowVM vm)
    {
        var row = new Border { Padding = new Thickness(12, 10, 12, 10), CornerRadius = new CornerRadius(9) };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var cb = new CheckBox
        {
            Style = (Style)FindResource("RoundCheckOnly"),
            IsChecked = vm.Selected,
            VerticalAlignment = VerticalAlignment.Center,
        };
        cb.Checked += (s, e) => { vm.Selected = true; UpdateSelectionSummary(); };
        cb.Unchecked += (s, e) => { vm.Selected = false; UpdateSelectionSummary(); };
        vm.CheckBox = cb;
        Grid.SetColumn(cb, 0);

        var mid = new StackPanel { Margin = new Thickness(12, 0, 8, 0) };
        var line1 = new StackPanel { Orientation = Orientation.Horizontal };
        line1.Children.Add(new TextBlock
        {
            Text = vm.Name,
            FontSize = 13.5,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)FindResource("TextPrimaryBrush"),
        });
        line1.Children.Add(BuildChip(vm.RiskText, vm.RiskBrush, new Thickness(10, 0, 0, 0)));
        if (vm.RunningCount > 0)
        {
            line1.Children.Add(BuildChip(string.Format(Services.Loc.T("row.running"), vm.RunningCount), (Brush)FindResource("WarningBrush"),
                new Thickness(8, 0, 0, 0), soft: true));
        }
        mid.Children.Add(line1);
        var reason = new TextBlock
        {
            Text = vm.Reason + (vm.CanRestore ? " · " + Services.Loc.T("row.restore") : " · " + Services.Loc.T("row.direct")),
            FontSize = 11.5,
            Foreground = (Brush)FindResource("TextTertiaryBrush"),
            Margin = new Thickness(0, 3, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        ToolTipService.SetToolTip(reason, vm.Reason + "\n\n" + string.Format(Services.Loc.T("row.impact.tip"), vm.Impact));
        mid.Children.Add(reason);
        Grid.SetColumn(mid, 1);

        var size = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        size.Children.Add(new TextBlock
        {
            Text = vm.SizeText,
            FontSize = 14.5,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)FindResource("TextPrimaryBrush"),
            HorizontalAlignment = HorizontalAlignment.Right,
        });
        size.Children.Add(new TextBlock
        {
            Text = vm.CountText,
            FontSize = 11,
            Foreground = (Brush)FindResource("TextTertiaryBrush"),
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 2, 0, 0),
        });
        Grid.SetColumn(size, 2);

        var detailsHost = new StackPanel { Visibility = Visibility.Collapsed, Margin = new Thickness(30, 4, 6, 6) };
        vm.DetailsHost = detailsHost;

        var toggle = new ToggleButton
        {
            Style = (Style)FindResource("ToggleSwitch"),
            IsChecked = vm.Expanded,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(14, 0, 0, 0),
            ToolTip = "查看文件明细",
        };
        toggle.Click += (s, e) =>
        {
            vm.Expanded = toggle.IsChecked == true;
            detailsHost.Visibility = vm.Expanded ? Visibility.Visible : Visibility.Collapsed;
            if (vm.Expanded) BuildDetails(vm);
        };
        Grid.SetColumn(toggle, 3);

        grid.Children.Add(cb);
        grid.Children.Add(mid);
        grid.Children.Add(size);
        grid.Children.Add(toggle);

        var outer = new StackPanel();
        outer.Children.Add(grid);

        outer.Children.Add(detailsHost);

        row.Child = outer;
        row.MouseEnter += (s, e) => row.Background = (Brush)FindResource("ItemHoverBrush");
        row.MouseLeave += (s, e) => row.Background = Brushes.Transparent;
        return row;
    }

    private void BuildDetails(ScanRowVM vm)
    {
        var host = vm.DetailsHost;
        if (host == null) return;
        host.Children.Clear();
        var items = vm.Res.Items.Take(100).ToList();
        foreach (var it in items)
        {
            var line = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var path = new TextBlock
            {
                Text = it.Path + (it.IsDirectory ? "  (目录)" : ""),
                FontSize = 11,
                Foreground = (Brush)FindResource("TextSecondaryBrush"),
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            ToolTipService.SetToolTip(path, it.Path);
            Grid.SetColumn(path, 0);
            var sz = new TextBlock
            {
                Text = SizeText.OfBytes(it.Size),
                FontSize = 11,
                Foreground = (Brush)FindResource("TextTertiaryBrush"),
                Margin = new Thickness(14, 0, 0, 0),
            };
            Grid.SetColumn(sz, 1);
            line.Children.Add(path);
            line.Children.Add(sz);
            host.Children.Add(line);
        }
        if (vm.Res.Items.Count > 100)
        {
            host.Children.Add(new TextBlock
            {
                Text = $"… 还有 {vm.Res.Items.Count - 100} 个项目未显示",
                FontSize = 11,
                Foreground = (Brush)FindResource("TextTertiaryBrush"),
                Margin = new Thickness(0, 3, 0, 0),
            });
        }
    }

    private static Border BuildChip(string text, Brush brush, Thickness margin, bool soft = false)
    {
        var bd = new Border
        {
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8, 2, 8, 3),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = margin,
            Background = soft
                ? new SolidColorBrush(OpacityBlend(brush))
                : (Brush)System.Windows.Application.Current.FindResource("PrimarySoftBrush"),
        };
        if (!soft) bd.Background = new SolidColorBrush(Color.FromArgb(26, 39, 116, 240));
        bd.Child = new TextBlock
        {
            Text = text,
            FontSize = 10.5,
            FontWeight = FontWeights.Bold,
            Foreground = brush,
        };
        return bd;
    }

    private static Color OpacityBlend(Brush b)
    {
        if (b is SolidColorBrush sc) return Color.FromArgb(28, sc.Color.R, sc.Color.G, sc.Color.B);
        return Color.FromArgb(28, 0, 0, 0);
    }

    private void UpdateSelectionSummary()
    {
        if (_rows == null) return;
        int count = 0;
        long bytes = 0;
        foreach (var r in _rows.Where(r => r.Selected))
        {
            count += r.Res.FileCount;
            bytes += r.Res.TotalSize;
        }
        SelCountText.Text = string.Format(Services.Loc.T("common.n.items"), count);
        SelSizeText.Text = SizeText.OfBytes(bytes);
    }

    // ————— 清理 —————

    private async void BtnCleanSelected_Click(object sender, RoutedEventArgs e)
    {
        if (_rows == null || _cleaning || _scanning) return;
        var selected = _rows.Where(r => r.Selected).ToList();
        if (selected.Count == 0)
        {
            _main.ShowToast(Services.Loc.T("toast.noselect"), ToastType.Info);
            return;
        }

        // 高风险确认（L2 默认未勾选，此处兜底提示）
        var confirmRules = selected.Where(r => r.Risk == RiskLevel.Confirm).ToList();
        string confirmMsg = string.Format(Services.Loc.T("dlg.clean.amount"), SizeText.OfBytes(selected.Sum(r => r.Res.TotalSize)));
        if (confirmRules.Count > 0)
        {
            confirmMsg += Services.Loc.T("dlg.clean.confirmitems") + "\n" +
                          string.Join("\n", confirmRules.Select(r => "· " + Services.Loc.RuleName(r.Res.RuleId, r.Name) + "（" + r.SizeText + "）"));
        }
        confirmMsg += "\n\n" + Services.Loc.T("dlg.clean.quarantinehint");
        if (!DialogService.Confirm(_main, Services.Loc.T("dlg.clean.selected.title"), confirmMsg, Services.Loc.T("btn.clean.start"), Services.Loc.T("btn.cancel")))
            return;

        _cleaning = true;
        _cleanCts = new CancellationTokenSource();
        Show(PanelCleaning);
        CleanProgressBar.Value = 0;
        CleanFreedText.Text = Services.Loc.T("clean.freed");
        CleanTitleText.Text = Services.Loc.T("clean.title");

        var svc = new CleanService();
        var requests = selected.Select(r => new CleanRequestRule
        {
            Rule = r.Rule,
            Result = r.Res,
            Selected = r.Res.Items.ToList(),
        }).ToList();

        int total = requests.Sum(q => q.Selected.Count);
        int done = 0;
        var progress = new Progress<CleanProgress>(p =>
        {
            done++;
            CleanProgressBar.Value = total > 0 ? done * 100.0 / total : 100;
            CleanDetailText.Text = p.CurrentPath;
            CleanFreedText.Text = $"已释放 {SizeText.OfBytes(p.FreedBytes)}";
        });

        CleanSummary summary;
        try
        {
            summary = await svc.CleanAsync(requests, _cleanCts.Token, progress);
        }
        catch (OperationCanceledException)
        {
            _cleaning = false;
            Show(PanelResults);
            _main.ShowToast(Services.Loc.T("toast.cleancancelled"), ToastType.Info);
            return;
        }
        catch (Exception ex)
        {
            _cleaning = false;
            Show(PanelResults);
            _main.ShowToast(Services.Loc.T("toast.cleanfailed") + ex.Message, ToastType.Warning);
            return;
        }
        _cleaning = false;

        DoneTitleText.Text = summary.Cancelled ? Services.Loc.T("done.cancelled") : summary.FailCount == 0 && summary.SkipCount == 0 ? Services.Loc.T("done.title") : Services.Loc.T("done.partial");
        DoneFreedText.Text = string.Format(Services.Loc.T("done.freed.total"), SizeText.OfBytes(summary.FreedBytes));
        DoneOkText.Text = summary.SuccessCount.ToString();
        DoneQuarText.Text = summary.QuarantineCount.ToString();
        DoneSkipText.Text = summary.SkipCount.ToString();
        DoneFailText.Text = summary.FailCount.ToString();
        DoneNoteText.Text = summary.QuarantineCount > 0
            ? string.Format(Services.Loc.T("done.note.quarantine"), SettingsService.Current.QuarantineDays)
            : Services.Loc.T("done.note.skip");
        Show(PanelDone);

        // 后台重扫以刷新缓存
        _ = Task.Run(async () =>
        {
            try
            {
                var scanner = new ScannerService();
                var ctx = scanner.BuildContext(CancellationToken.None);
                var rules = scanner.Rules.Where(r =>
                    r.Group is RuleGroup.SystemJunk or RuleGroup.BrowserCache or RuleGroup.AppCache
                    or RuleGroup.RecycleBin).ToList();
                var results = await scanner.ScanAsync(rules, ctx, null);
                ScanCache.Save(new LastScanSummary
                {
                    Time = DateTime.Now,
                    TotalBytes = results.Sum(r => r.TotalSize),
                    Rules = results.Where(r => r.Error == null).Select(r => new LastScanSummary.RuleSummary
                    {
                        Id = r.RuleId, Name = r.Name, Group = (int)r.Group, Risk = (int)r.Risk,
                        Size = r.TotalSize, Count = r.FileCount,
                    }).ToList(),
                });
            }
            catch { }
        });
    }

    private void BtnDoneHome_Click(object sender, RoutedEventArgs e) => _main.Navigate(PageKey.Home);
    private void BtnDoneQuarantine_Click(object sender, RoutedEventArgs e) => _main.Navigate(PageKey.Toolbox, "quarantine");

    private void BtnStartScan_Click(object sender, RoutedEventArgs e) => StartScan();
    private void BtnStopScan_Click(object sender, RoutedEventArgs e) => _scanCts?.Cancel();
    private void BtnStopClean_Click(object sender, RoutedEventArgs e) => _cleanCts?.Cancel();

    private void BtnReselectAll_Click(object sender, RoutedEventArgs e)
    {
        if (_rows == null) return;
        foreach (var r in _rows)
        {
            bool newVal = r.Risk <= RiskLevel.Low;
            r.Selected = newVal;
            r.CheckBox!.IsChecked = newVal;
        }
        UpdateSelectionSummary();
    }

    private void Show(UIElement panel)
    {
        PanelIdle.Visibility = panel == PanelIdle ? Visibility.Visible : Visibility.Collapsed;
        PanelScanning.Visibility = panel == PanelScanning ? Visibility.Visible : Visibility.Collapsed;
        PanelResults.Visibility = panel == PanelResults ? Visibility.Visible : Visibility.Collapsed;
        PanelCleaning.Visibility = panel == PanelCleaning ? Visibility.Visible : Visibility.Collapsed;
        PanelDone.Visibility = panel == PanelDone ? Visibility.Visible : Visibility.Collapsed;
    }
}

/// <summary>结果行视图模型。</summary>
public sealed class ScanRowVM : INotifyPropertyChanged
{
    public IScanRule Rule { get; }
    public RuleResult Res { get; }

    public string Name => Services.Loc.RuleName(Res.RuleId, Res.Name);
    public string SizeText => CleanMaster.Core.Utils.SizeText.OfBytes(Res.TotalSize);
    public string CountText => string.Format(Services.Loc.T("row.files"), Res.FileCount);
    public string Reason => Services.Loc.RuleReason(Res.RuleId, Res.Reason);
    public string RestoreText => Services.Loc.T("row.restore");
    public string DirectText => Services.Loc.T("row.direct");
    public string Impact => Services.Loc.RuleImpact(Res.RuleId, Res.Impact);
    public bool CanRestore => Res.CanRestore;
    public string RiskText => Res.Risk switch { RiskLevel.Safe => Services.Loc.T("risk.safe"), RiskLevel.Low => Services.Loc.T("risk.low"), RiskLevel.Confirm => Services.Loc.T("risk.confirm"), _ => Services.Loc.T("risk.high") };
    public System.Windows.Media.Brush RiskBrush => Res.Risk switch
    {
        RiskLevel.Confirm => System.Windows.Media.Brushes.Orange,
        RiskLevel.High => System.Windows.Media.Brushes.Red,
        _ => new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x31, 0xA3, 0x54)),
    };
    public int RunningCount => Res.RunningProcesses.Count;
    public RiskLevel Risk => Res.Risk;

    private bool _selected;
    public bool Selected
    {
        get => _selected;
        set { _selected = value; PropertyChanged?.Invoke(this, new(nameof(Selected))); }
    }
    public bool Expanded { get; set; }
    public CheckBox? CheckBox { get; set; }
    public StackPanel? DetailsHost { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ScanRowVM(RuleResult res, IScanRule rule)
    {
        Res = res;
        Rule = rule;
        _selected = res.DefaultSelected && res.TotalSize > 0;
    }
}
