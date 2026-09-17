using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using CleanMaster.App.Services;
using CleanMaster.Core.Store;

namespace CleanMaster.App.Pages;

public partial class SettingsPage : Page
{
    private readonly MainWindow _main;
    private readonly AppSettings _s = SettingsService.Current;

    public SettingsPage(MainWindow main)
    {
        InitializeComponent();
        _main = main;
        BuildUi();
    }

    private void Save()
    {
        SettingsService.Save();
        StartupRegistration.Sync(_s.LaunchAtStartup);
    }

    private StackPanel Row(string title, string? desc, UIElement control)
    {
        var row = new Grid { Margin = new Thickness(0, 6, 0, 6) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        sp.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 13,
            Foreground = (Brush)FindResource("TextPrimaryBrush"),
        });
        if (!string.IsNullOrEmpty(desc))
            sp.Children.Add(new TextBlock
            {
                Text = desc,
                FontSize = 11.5,
                Foreground = (Brush)FindResource("TextTertiaryBrush"),
                Margin = new Thickness(0, 3, 0, 0),
                TextWrapping = TextWrapping.Wrap,
            });
        Grid.SetColumn(sp, 0);
        Grid.SetColumn(control, 1);
        control.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
        row.Children.Add(sp);
        row.Children.Add(control);
        return new StackPanel { Children = { row } };
    }

    private ToggleButton Toggle(bool value, Action<bool> onChanged)
    {
        var t = new ToggleButton { Style = (Style)FindResource("ToggleSwitch"), IsChecked = value };
        t.Click += (s, e) => { onChanged(t.IsChecked == true); Save(); };
        return t;
    }

    private void BuildUi()
    {
        // —— 通用 ——
        GeneralSection.Children.Add(Row("开机自动运行清理优化大师", "开机后只在后台托盘待命，不执行任何操作",
            Toggle(_s.LaunchAtStartup, v => _s.LaunchAtStartup = v)));
        GeneralSection.Children.Add(Row("最小化到系统托盘", "点击最小化按钮时隐藏到托盘",
            Toggle(_s.MinimizeToTray, v => _s.MinimizeToTray = v)));
        GeneralSection.Children.Add(Row("关闭按钮最小化到托盘", "点击关闭按钮时询问并最小化，而非退出",
            Toggle(_s.CloseToTray, v => _s.CloseToTray = v)));

        var themeBox = new ComboBox { MinWidth = 130 };
        foreach (var (label, val) in new[] { ("浅色", "light"), ("深色", "dark"), ("跟随系统", "system") })
        {
            var item = new ComboBoxItem { Content = label, Tag = val };
            themeBox.Items.Add(item);
            if (_s.Theme == val) item.IsSelected = true;
        }
        themeBox.SelectionChanged += (s, e) =>
        {
            if (themeBox.SelectedItem is ComboBoxItem it)
            {
                _s.Theme = (string)it.Tag!;
                Save();
                Services.ThemeManager.ApplyFromSettings();
            }
        };
        GeneralSection.Children.Add(Row("主题", "", themeBox));

        // —— 清理 ——
        CleanSection.Children.Add(Row("清理后进入隔离区", "非缓存类文件先进入隔离区，可随时恢复（推荐开启）",
            Toggle(_s.QuarantineEnabled, v => _s.QuarantineEnabled = v)));

        var daysBox = new ComboBox { MinWidth = 130 };
        foreach (var d in new[] { 7, 15, 30 })
        {
            var item = new ComboBoxItem { Content = $"{d} 天", Tag = d };
            daysBox.Items.Add(item);
            if (_s.QuarantineDays == d) item.IsSelected = true;
        }
        daysBox.SelectionChanged += (s, e) =>
        {
            if (daysBox.SelectedItem is ComboBoxItem it) { _s.QuarantineDays = (int)it.Tag!; Save(); }
        };
        CleanSection.Children.Add(Row("隔离区保留时间", "到期后自动永久删除", daysBox));

        CleanSection.Children.Add(Row("自动清理", "按计划自动清理临时文件与缓存等安全项目",
            Toggle(_s.AutoCleanEnabled, v => _s.AutoCleanEnabled = v)));

        var freqBox = new ComboBox { MinWidth = 130 };
        foreach (var (label, val) in new[] { ("每天", "daily"), ("每周", "weekly"), ("每月", "monthly") })
        {
            var item = new ComboBoxItem { Content = label, Tag = val };
            freqBox.Items.Add(item);
            if (_s.AutoCleanFrequency == val) item.IsSelected = true;
        }
        freqBox.SelectionChanged += (s, e) =>
        {
            if (freqBox.SelectedItem is ComboBoxItem it) { _s.AutoCleanFrequency = (string)it.Tag!; Save(); }
        };
        CleanSection.Children.Add(Row("自动清理频率", "应用运行期间在后台执行", freqBox));

        // —— 排除目录与白名单 ——
        RenderExclusions();

        // —— 通知 ——
        NotifySection.Children.Add(Row("磁盘空间不足提醒", "系统盘剩余不足 10% 时提醒（低频，不打扰）",
            Toggle(_s.NotifyDiskLow, v => _s.NotifyDiskLow = v)));
        NotifySection.Children.Add(Row("自动清理结果通知", "",
            Toggle(_s.NotifyAutoCleanResult, v => _s.NotifyAutoCleanResult = v)));
        NotifySection.Children.Add(Row("每周空间报告", "",
            Toggle(_s.NotifyWeeklyReport, v => _s.NotifyWeeklyReport = v)));
        NotifySection.Children.Add(Row("产品消息", "新功能与活动通知，默认关闭",
            Toggle(_s.NotifyProductNews, v => _s.NotifyProductNews = v)));

        // —— 高级 ——
        var threadsBox = new ComboBox { MinWidth = 130 };
        for (int i = 2; i <= Math.Max(8, Environment.ProcessorCount); i += 2)
        {
            var item = new ComboBoxItem { Content = $"{i} 线程", Tag = i };
            threadsBox.Items.Add(item);
            if (_s.ScanThreads == i) item.IsSelected = true;
        }
        threadsBox.SelectionChanged += (s, e) =>
        {
            if (threadsBox.SelectedItem is ComboBoxItem it) { _s.ScanThreads = (int)it.Tag!; Save(); }
        };
        AdvancedSection.Children.Add(Row("扫描线程数", "线程越多扫描越快，但机械硬盘建议调低", threadsBox));
        AdvancedSection.Children.Add(Row("扫描隐藏文件", "",
            Toggle(_s.ScanHiddenFiles, v => _s.ScanHiddenFiles = v)));
        AdvancedSection.Children.Add(Row("跟随目录链接 (Junction)", "跟随符号链接扫描可能造成重复统计，默认关闭",
            Toggle(_s.FollowJunctions, v => _s.FollowJunctions = v)));

        // —— 隐私 ——
        PrivacySection.Children.Add(Row("匿名使用统计", "仅上报功能使用次数与错误码，不含任何文件信息；当前版本完全不上传数据",
            Toggle(_s.AnonymousStats, v => _s.AnonymousStats = v)));
        PrivacySection.Children.Add(Row("崩溃报告", "",
            Toggle(_s.CrashReports, v => _s.CrashReports = v)));
    }

    private void RenderExclusions()
    {
        ExclusionList.Children.Clear();
        void AddEntryRow(string path, bool isWhitelist)
        {
            var row = new Border
            {
                Background = (Brush)FindResource("ItemHoverBrush"),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10, 7, 10, 7),
                Margin = new Thickness(0, 4, 0, 0),
            };
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var chip = new Border
            {
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(7, 2, 7, 3),
                VerticalAlignment = VerticalAlignment.Center,
                Background = isWhitelist
                    ? (Brush)FindResource("SuccessSoftBrush")
                    : (Brush)FindResource("PrimarySoftBrush"),
            };
            chip.Child = new TextBlock
            {
                Text = isWhitelist ? "白名单" : "排除",
                FontSize = 10.5,
                FontWeight = FontWeights.Bold,
                Foreground = isWhitelist
                    ? (Brush)FindResource("SuccessBrush")
                    : (Brush)FindResource("PrimaryBrush"),
            };
            Grid.SetColumn(chip, 0);

            var pathText = new TextBlock
            {
                Text = path,
                FontSize = 12,
                Foreground = (Brush)FindResource("TextPrimaryBrush"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 8, 0),
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            Grid.SetColumn(pathText, 1);

            var del = new Button
            {
                Content = "移除",
                Style = (Style)FindResource("LinkButton"),
                Foreground = (Brush)FindResource("DangerBrush"),
            };
            del.Click += (s, e) =>
            {
                if (isWhitelist) _s.Whitelist.Remove(path);
                else _s.ExcludedPaths.Remove(path);
                Save();
                RenderExclusions();
            };
            Grid.SetColumn(del, 2);

            grid.Children.Add(chip);
            grid.Children.Add(pathText);
            grid.Children.Add(del);
            row.Child = grid;
            ExclusionList.Children.Add(row);
        }

        foreach (var p in _s.ExcludedPaths) AddEntryRow(p, isWhitelist: false);
        foreach (var p in _s.Whitelist) AddEntryRow(p, isWhitelist: true);
        if (_s.ExcludedPaths.Count == 0 && _s.Whitelist.Count == 0)
        {
            ExclusionList.Children.Add(new TextBlock
            {
                Text = "暂无排除目录与白名单",
                FontSize = 12,
                Foreground = (Brush)FindResource("TextTertiaryBrush"),
            });
        }
    }

    private void BtnAddExclude_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new System.Windows.Forms.FolderBrowserDialog { Description = "选择要在扫描清理中排除的目录" };
        if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
        if (!_s.ExcludedPaths.Contains(dlg.SelectedPath))
        {
            _s.ExcludedPaths.Add(dlg.SelectedPath);
            Save();
            RenderExclusions();
        }
    }

    private void BtnAddWhitelist_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Title = "选择要加入白名单的文件" };
        if (dlg.ShowDialog() != true) return;
        if (!_s.Whitelist.Contains(dlg.FileName))
        {
            _s.Whitelist.Add(dlg.FileName);
            Save();
            RenderExclusions();
        }
    }

    private void BtnClearLocal_Click(object sender, RoutedEventArgs e)
    {
        if (!Services.DialogService.Confirm(_main, "清除本地历史？", "清理历史与活动记录将被删除（隔离区文件不受影响）。", "清除"))
            return;
        HistoryService.Clear();
        _main.ShowToast("本地历史已清除");
    }
}
