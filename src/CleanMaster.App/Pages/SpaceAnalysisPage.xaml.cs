using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CleanMaster.App.Services;
using CleanMaster.Core.Analysis;
using CleanMaster.Core.Store;
using CleanMaster.Core.Utils;

namespace CleanMaster.App.Pages;

public partial class SpaceAnalysisPage : Page, IParamPage
{
    private readonly MainWindow _main;
    private readonly SpaceAnalysisService _analyzer = new();
    private CancellationTokenSource? _cts;
    private DriveAnalysis? _lastAnalysis;
    private bool _onLargeTab;

    public SpaceAnalysisPage(MainWindow main)
    {
        InitializeComponent();
        _main = main;
        FolderTree.AddHandler(TreeViewItem.ExpandedEvent, new RoutedEventHandler(FolderTree_Expanded));
        Loaded += (s, e) =>
        {
            if (DriveSelector.Items.Count == 0)
            {
                foreach (var d in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
                    DriveSelector.Items.Add(d.Name);
                if (DriveSelector.Items.Count > 0)
                    DriveSelector.SelectedIndex = 0;
            }
        };
    }

    public async void OnNavigated(object? param)
    {
        if (param as string == "large")
        {
            TabLarge.IsChecked = true;
        }
        else if (param as string == "analyze")
        {
            TabOverview.IsChecked = true;
            await RunAnalyzeAsync();
        }
        else if (param as string == "analyze-large")
        {
            _switchToLargeAfterAnalyze = true;
            await RunAnalyzeAsync();
        }
    }

    private bool _switchToLargeAfterAnalyze;

    private async Task RunAnalyzeAsync()
    {
        try { await AnalyzeAsync(); }
        catch (Exception ex) { Services.AppLog.Error("OnNavigated.analyze", ex); }
        if (_switchToLargeAfterAnalyze)
        {
            _switchToLargeAfterAnalyze = false;
            TabLarge.IsChecked = true;
        }
    }

    private string CurrentDrive => DriveSelector.SelectedItem as string ?? "C:\\";

    // ————— 标签切换 —————

    private void OverviewTab_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        _onLargeTab = false;
        LargeCard.Visibility = Visibility.Collapsed;
        TreeCard.Visibility = _lastAnalysis != null ? Visibility.Visible : Visibility.Collapsed;
        UpdateAnalyzePanel();
    }

    private void LargeTab_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        _onLargeTab = true;
        TreeCard.Visibility = Visibility.Collapsed;
        LargeCard.Visibility = Visibility.Visible;
        UpdateAnalyzePanel();
        RenderLargeFiles();
    }

    private void UpdateAnalyzePanel()
    {
        AnalyzePanel.Visibility = _cts != null ? Visibility.Visible : Visibility.Collapsed;
        if (_cts == null)
        {
            OverviewCard.Visibility = Visibility.Visible;
        }
    }

    // ————— 分析 —————

    private async void BtnAnalyze_Click(object sender, RoutedEventArgs e) => await AnalyzeAsync();

    private async Task AnalyzeAsync()
    {
        if (_cts != null) return;
        var drive = CurrentDrive;
        _cts = new CancellationTokenSource();
        AnalyzePanel.Visibility = Visibility.Visible;
        OverviewCard.Visibility = Visibility.Collapsed;
        TreeCard.Visibility = Visibility.Collapsed;
        LargeCard.Visibility = _onLargeTab ? Visibility.Visible : Visibility.Collapsed;
        BtnAnalyze.IsEnabled = false;
        AnalyzeFilesText.Text = "已扫描 0 个文件";
        AnalyzeBytesText.Text = "共 0 B";

        var threshold = 500L * 1024 * 1024;
        var progress = new Progress<SpaceAnalysisService.ProgressInfo>(p =>
        {
            AnalyzeDirText.Text = p.CurrentDir;
            AnalyzeFilesText.Text = $"已扫描 {p.ScannedFiles:N0} 个文件";
            AnalyzeBytesText.Text = $"共 {SizeText.OfBytes(p.ScannedBytes)}";
        });

        DriveAnalysis analysis;
        try
        {
            analysis = await _analyzer.AnalyzeAsync(drive, threshold, _cts.Token, progress);
        }
        catch (OperationCanceledException)
        {
            FinishAnalyzeUi();
            _main.ShowToast("已取消分析", ToastType.Info);
            return;
        }
        catch (Exception ex)
        {
            FinishAnalyzeUi();
            Services.AppLog.Error("AnalyzeAsync", ex);
            _main.ShowToast("分析失败：" + ex.Message, ToastType.Warning);
            return;
        }
        FinishAnalyzeUi();
        _lastAnalysis = analysis;
        // 渲染与保存必须互不影响，异常记录日志
        try
        {
            RenderOverview(analysis);
            RenderTree(analysis);
            RenderLargeFiles();
            _main.ShowToast($"分析完成，用时 {analysis.Duration.TotalSeconds:F0} 秒");
        }
        catch (Exception ex)
        {
            Services.AppLog.Error("RenderAnalysis", ex);
            _main.ShowToast("分析结果渲染异常，详情见日志", ToastType.Warning);
        }
        try
        {
            DiskCache.Save(new DiskSummary
            {
                Drive = drive,
                TotalBytes = analysis.TotalBytes,
                UsedBytes = analysis.UsedBytes,
                FreeBytes = analysis.FreeBytes,
                Time = DateTime.Now,
                Categories = analysis.Categories.Take(6).Select(c => new DiskSummary.CatItem
                {
                    Name = CategoryText.Of(c.Category),
                    Bytes = c.Bytes,
                    Percent = c.Percent,
                    Hex = CategoryText.HexOf(c.Category),
                }).ToList(),
            });
        }
        catch (Exception ex)
        {
            Services.AppLog.Error("SaveDiskSummary", ex);
        }
    }

    private void FinishAnalyzeUi()
    {
        _cts = null;
        AnalyzePanel.Visibility = Visibility.Collapsed;
        OverviewCard.Visibility = Visibility.Visible;
        BtnAnalyze.IsEnabled = true;
    }

    private void BtnCancelAnalyze_Click(object sender, RoutedEventArgs e) => _cts?.Cancel();

    private void RenderOverview(DriveAnalysis a)
    {
        OverviewDriveText.Text = a.DriveName;
        OverviewUsedText.Text = $"已使用 {SizeText.OfBytes(a.UsedBytes)} / {SizeText.OfBytes(a.TotalBytes)}（{a.UsedPercent:F1}%），" +
                                $"可用 {SizeText.OfBytes(a.FreeBytes)}";
        OverviewTimeText.Text = $"分析于 {DateTime.Now:HH:mm} · {a.ScannedFiles:N0} 个文件 · {a.Duration.TotalSeconds:F0} 秒";
        DiskUsageBar.Value = Math.Clamp(a.UsedPercent, 0, 100);
        CategoryRow.Children.Clear();
        foreach (var c in a.Categories.Take(6))
        {
            var chip = new Border
            {
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(14, 8, 14, 8),
                Margin = new Thickness(0, 0, 10, 6),
                Background = (Brush)new BrushConverter().ConvertFromString(CategoryText.HexOf(c.Category))!,
                MinWidth = 110,
            };
            var sp = new StackPanel();
            sp.Children.Add(new TextBlock
            {
                Text = $"{CategoryText.Of(c.Category)}  {c.Percent:F1}%",
                FontSize = 11.5,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.White),
            });
            sp.Children.Add(new TextBlock
            {
                Text = SizeText.OfBytes(c.Bytes, 0),
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.White),
                Margin = new Thickness(0, 2, 0, 0),
            });
            chip.Child = sp;
            CategoryRow.Children.Add(chip);
        }
        OverviewEmptyText.Visibility = Visibility.Collapsed;
        TreeCard.Visibility = _onLargeTab ? Visibility.Collapsed : Visibility.Visible;
    }

    // ————— 目录树 —————

    private void RenderTree(DriveAnalysis a)
    {
        FolderTree.Items.Clear();
        var root = new FolderNodeVM(a.DriveName, a.Root.Size, a.Root.FileCount, a.DriveName);
        foreach (var child in a.Root.Children)
            root.Children.Add(MakeNode(child, a.Root.Size));
        FolderTree.Items.Add(root);
    }

    private FolderNodeVM MakeNode(Core.Analysis.FolderNode node, long parentSize)
    {
        var vm = new FolderNodeVM(node.Name, node.Size, node.FileCount, node.FullPath)
        {
            Percent = parentSize > 0 ? node.Size * 100.0 / parentSize : 0,
        };
        // 预置占位子节点以显示展开箭头，展开时再真实加载
        if (node.Size > 1024 * 1024)
            vm.Children.Add(new FolderNodeVM("…", 0, 0, ""));
        return vm;
    }

    private void FolderTree_Expanded(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not TreeViewItem { Header: FolderNodeVM vm }) return;
        if (_lastAnalysis == null) return;
        if (vm.Children.Count != 1 || vm.Children[0].FullPath != "") return; // 已加载
        vm.Children.Clear();
        foreach (var c in _lastAnalysis.GetChildren(vm.FullPath))
        {
            var child = MakeNode(c, vm.SizeBytes);
            vm.Children.Add(child);
        }
        if (vm.Children.Count == 0)
            vm.Children.Add(new FolderNodeVM("(无子目录)", 0, 0, "done"));
    }

    // ————— 大文件 —————

    private void LargeThreshold_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded) RenderLargeFiles();
    }

    private void DriveSelector_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        _lastAnalysis = null;
        FolderTree.Items.Clear();
        TreeCard.Visibility = Visibility.Collapsed;
        LargeList.Children.Clear();
        CategoryRow.Children.Clear();
        OverviewDriveText.Text = CurrentDrive;
        OverviewTimeText.Text = "";
        var cached = DiskCache.Load(CurrentDrive);
        if (cached != null)
        {
            OverviewUsedText.Text = $"已使用 {SizeText.OfBytes(cached.UsedBytes)} / {SizeText.OfBytes(cached.TotalBytes)}，" +
                                    $"可用 {SizeText.OfBytes(cached.FreeBytes)}";
            OverviewTimeText.Text = $"上次分析：{cached.Time:yyyy-MM-dd HH:mm}";
            DiskUsageBar.Value = cached.TotalBytes > 0
                ? Math.Clamp(cached.UsedBytes * 100.0 / cached.TotalBytes, 0, 100)
                : 0;
            foreach (var c in cached.Categories)
            {
                var chip = new Border
                {
                    CornerRadius = new CornerRadius(9),
                    Padding = new Thickness(14, 8, 14, 8),
                    Margin = new Thickness(0, 0, 10, 6),
                    Background = (Brush)new BrushConverter().ConvertFromString(c.Hex)!,
                    MinWidth = 110,
                };
                var sp = new StackPanel();
                sp.Children.Add(new TextBlock
                {
                    Text = $"{c.Name}  {c.Percent:F1}%",
                    FontSize = 11.5,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Colors.White),
                });
                sp.Children.Add(new TextBlock
                {
                    Text = SizeText.OfBytes(c.Bytes, 0),
                    FontSize = 13,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Colors.White),
                    Margin = new Thickness(0, 2, 0, 0),
                });
                chip.Child = sp;
                CategoryRow.Children.Add(chip);
            }
            OverviewEmptyText.Visibility = Visibility.Collapsed;
        }
        else
        {
            OverviewUsedText.Text = "这块磁盘还没有分析过";
            OverviewEmptyText.Visibility = Visibility.Visible;
        }
        RenderLargeFiles();
    }

    private void RenderLargeFiles()
    {
        LargeList.Children.Clear();
        double mb = 500;
        if (LargeThreshold.SelectedItem is ComboBoxItem it && it.Tag is string tag)
            mb = double.Parse(tag);
        var threshold = (long)(mb * 1024 * 1024);

        var disk = DiskCache.Load(CurrentDrive);
        OverviewDriveText.Text = disk?.Drive ?? CurrentDrive;
        var files = _lastAnalysis?.LargeFiles
            ?? Enumerable.Empty<LargeFileInfo>();

        var list = files.Where(f => f.Size >= threshold).OrderByDescending(f => f.Size).Take(300).ToList();
        LargeEmptyText.Visibility = list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (_lastAnalysis == null && disk == null)
            LargeEmptyText.Text = "还没有分析过这块磁盘，点击右上角「开始分析」";
        else if (list.Count == 0)
            LargeEmptyText.Text = $"没有找到大于 {mb:F0} MB 的文件";

        long shownBytes = list.Sum(f => f.Size);
        LargeSummaryText.Text = list.Count > 0 ? $"{list.Count} 个文件 · 共 {SizeText.OfBytes(shownBytes)}" : "";

        foreach (var f in list)
        {
            LargeList.Children.Add(BuildLargeRow(f));
        }
    }

    private Border BuildLargeRow(LargeFileInfo f)
    {
        var row = new Border
        {
            Padding = new Thickness(10, 8, 10, 8),
            CornerRadius = new CornerRadius(9),
        };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var cat = SpaceAnalysisService.ClassifyFile(f.Path);
        var iconTile = new Border
        {
            Width = 36, Height = 36, CornerRadius = new CornerRadius(10),
            Background = (Brush)new BrushConverter().ConvertFromString(CategoryText.HexOf(cat))!,
            VerticalAlignment = VerticalAlignment.Center,
        };
        iconTile.Child = new TextBlock
        {
            Text = System.IO.Path.GetExtension(f.Path).TrimStart('.').ToUpperInvariant() is { Length: > 0 and <= 4 } ext ? ext : "文件",
            FontSize = 9.5,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(iconTile, 0);

        var mid = new StackPanel { Margin = new Thickness(12, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
        mid.Children.Add(new TextBlock
        {
            Text = System.IO.Path.GetFileName(f.Path),
            FontSize = 13,
            Foreground = (Brush)FindResource("TextPrimaryBrush"),
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        var pathTb = new TextBlock
        {
            Text = f.Path,
            FontSize = 11,
            Foreground = (Brush)FindResource("TextTertiaryBrush"),
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 2, 0, 0),
        };
        ToolTipService.SetToolTip(pathTb, f.Path);
        mid.Children.Add(pathTb);
        mid.Children.Add(new TextBlock
        {
            Text = $"修改于 {f.ModifiedTime:yyyy-MM-dd HH:mm}",
            FontSize = 10.5,
            Foreground = (Brush)FindResource("TextTertiaryBrush"),
            Margin = new Thickness(0, 2, 0, 0),
        });
        Grid.SetColumn(mid, 1);

        var size = new TextBlock
        {
            Text = SizeText.OfBytes(f.Size),
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)FindResource("TextPrimaryBrush"),
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(size, 2);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
        Grid.SetColumn(actions, 3);
        void AddAction(string text, Style style, RoutedEventHandler handler)
        {
            var b = new Button { Content = text, Style = style, Margin = new Thickness(6, 0, 0, 0) };
            b.Click += handler;
            actions.Children.Add(b);
        }
        AddAction("打开位置", (Style)FindResource("SmallSecondaryButton"), (s, e) =>
        {
            try { ShellUtil.ShowInExplorer(f.Path); } catch { }
        });
        AddAction("移入回收站", (Style)FindResource("SmallPrimaryButton"), (s, e) => RecycleLargeFile(f));
        AddAction("加入白名单", (Style)FindResource("SmallSecondaryButton"), (s, e) =>
        {
            var s2 = SettingsService.Current;
            if (!s2.Whitelist.Contains(f.Path))
            {
                s2.Whitelist.Add(f.Path);
                SettingsService.Save();
                _main.ShowToast("已加入白名单，扫描与清理将跳过该文件", ToastType.Info);
            }
        });

        grid.Children.Add(iconTile);
        grid.Children.Add(mid);
        grid.Children.Add(size);
        grid.Children.Add(actions);
        row.Child = grid;
        row.MouseEnter += (s, e) => row.Background = (Brush)FindResource("ItemHoverBrush");
        row.MouseLeave += (s, e) => row.Background = Brushes.Transparent;
        return row;
    }

    private void RecycleLargeFile(LargeFileInfo f)
    {
        if (!Services.DialogService.Confirm(_main, "移入回收站？",
            $"{System.IO.Path.GetFileName(f.Path)}（{SizeText.OfBytes(f.Size)}）将移入回收站，可随时还原。", "移入回收站"))
            return;
        var ok = ShellUtil.RecycleToBin(f.Path);
        if (ok)
        {
            HistoryService.Add("Clean", $"清理了大文件 {System.IO.Path.GetFileName(f.Path)}", f.Path, f.Size);
            _main.ShowToast($"已移入回收站，释放 {SizeText.OfBytes(f.Size)}");
            _lastAnalysis?.LargeFiles.Remove(f);
            RenderLargeFiles();
        }
        else
        {
            _main.ShowToast("移入回收站失败，文件可能被占用", ToastType.Warning);
        }
    }
}

/// <summary>目录树节点 VM。</summary>
public sealed class FolderNodeVM
{
    public string Name { get; }
    public string FullPath { get; }
    public string SizeText { get; }
    public string CountText { get; }
    public long SizeBytes { get; }
    public double Percent { get; init; }
    public ObservableCollection<FolderNodeVM> Children { get; } = new();

    public FolderNodeVM(string name, long size, int files, string fullPath)
    {
        Name = name;
        FullPath = fullPath;
        SizeBytes = size;
        SizeText = size > 0 ? CleanMaster.Core.Utils.SizeText.OfBytes(size) : "";
        CountText = files > 0 ? $"{files:N0} 文件" : "";
    }
}
