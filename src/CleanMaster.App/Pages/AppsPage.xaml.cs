using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CleanMaster.Core.Apps;
using CleanMaster.Core.Store;
using CleanMaster.Core.Utils;

namespace CleanMaster.App.Pages;

public partial class AppsPage : Page, IParamPage
{
    private readonly MainWindow _main;
    private readonly AppInventoryService _inventory = new();
    private List<InstalledApp> _apps = new();
    private string _filter = "";
    private bool _loaded;

    public AppsPage(MainWindow main)
    {
        InitializeComponent();
        _main = main;
        Loaded += (s, e) =>
        {
            if (!_loaded) { _loaded = true; _ = LoadAsync(); }
        };
    }

    public void OnNavigated(object? param) => _ = LoadAsync();

    private async Task LoadAsync()
    {
        LoadingPanel.Visibility = Visibility.Visible;
        LoadingText.Text = "正在读取已安装应用…";
        AppList.Children.Clear();
        try
        {
            _apps = await Task.Run(() => _inventory.EnumerateWin32());
        }
        catch (Exception ex)
        {
            _main.ShowToast("读取应用列表失败：" + ex.Message, ToastType.Warning);
        }
        LoadingPanel.Visibility = Visibility.Collapsed;
        Render();
    }

    private void Render()
    {
        AppList.Children.Clear();
        var q = _apps.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(_filter))
            q = q.Where(a => a.DisplayName.Contains(_filter, StringComparison.OrdinalIgnoreCase) ||
                             a.Publisher.Contains(_filter, StringComparison.OrdinalIgnoreCase));
        var sort = (SortSelector.SelectedItem as ComboBoxItem)?.Tag as string ?? "name";
        q = sort switch
        {
            "size" => q.OrderByDescending(a => a.EstimatedSize),
            "date" => q.OrderByDescending(a => a.InstallDate),
            _ => q.OrderBy(a => a.DisplayName, StringComparer.CurrentCultureIgnoreCase),
        };
        var list = q.ToList();
        foreach (var app in list)
            AppList.Children.Add(BuildRow(app));
        EmptyText.Visibility = list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private Border BuildRow(InstalledApp app)
    {
        var row = new Border { Padding = new Thickness(14, 9, 14, 9), CornerRadius = new CornerRadius(9) };
        var grid = new Grid();
        for (int i = 0; i < 5; i++) grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions[0].Width = new GridLength(2.6, GridUnitType.Star);
        grid.ColumnDefinitions[1].Width = new GridLength(1.2, GridUnitType.Star);
        grid.ColumnDefinitions[2].Width = new GridLength(0.9, GridUnitType.Star);
        grid.ColumnDefinitions[3].Width = new GridLength(1.1, GridUnitType.Star);
        grid.ColumnDefinitions[4].Width = GridLength.Auto;

        // 图标 + 名称
        var namePanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var icon = new Border
        {
            Width = 34, Height = 34, CornerRadius = new CornerRadius(9),
            Background = (Brush)FindResource("PrimarySoftBrush"),
        };
        var img = TryLoadIcon(app.IconPath);
        if (img != null)
        {
            icon.Child = new Image { Source = img, Width = 24, Height = 24 };
        }
        else
        {
            icon.Child = new TextBlock
            {
                Text = app.DisplayName.Length > 0 ? app.DisplayName[..1].ToUpperInvariant() : "?",
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = (Brush)FindResource("PrimaryBrush"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
        }
        namePanel.Children.Add(icon);
        var texts = new StackPanel { Margin = new Thickness(11, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };        var nameLine = new StackPanel { Orientation = Orientation.Horizontal };
        nameLine.Children.Add(new TextBlock
        {
            Text = app.DisplayName,
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)FindResource("TextPrimaryBrush"),
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 300,
        });
        if (!string.IsNullOrEmpty(app.DisplayVersion))
        {
            nameLine.Children.Add(new TextBlock
            {
                Text = " " + app.DisplayVersion,
                FontSize = 10.5,
                Foreground = (Brush)FindResource("TextTertiaryBrush"),
                VerticalAlignment = VerticalAlignment.Center,
            });
        }
        texts.Children.Add(nameLine);
        texts.Children.Add(new TextBlock
        {
            Text = app.IsStoreApp ? "Microsoft Store 应用" : (app.InstallLocation is { Length: > 0 } ? app.InstallLocation : ""),
            FontSize = 10.5,
            Foreground = (Brush)FindResource("TextTertiaryBrush"),
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 320,
            Margin = new Thickness(0, 2, 0, 0),
        });
        namePanel.Children.Add(texts);
        Grid.SetColumn(namePanel, 0);

        // 发布者
        var pub = new TextBlock
        {
            Text = app.Publisher,
            FontSize = 12,
            Foreground = (Brush)FindResource("TextSecondaryBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        Grid.SetColumn(pub, 1);

        // 大小
        var size = new TextBlock
        {
            Text = app.SizeDisplay,
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)FindResource("TextPrimaryBrush"),
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(size, 2);

        // 日期
        var date = new TextBlock
        {
            Text = app.InstallDate == default ? "—" : app.InstallDate.ToString("yyyy-MM-dd"),
            FontSize = 12,
            Foreground = (Brush)FindResource("TextSecondaryBrush"),
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(date, 3);

        // 卸载按钮
        var btn = new Button
        {
            Content = "卸载",
            Style = (Style)FindResource("SmallSecondaryButton"),
            Margin = new Thickness(10, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        btn.Click += (s, e) => _ = UninstallAsync(app);
        Grid.SetColumn(btn, 4);

        grid.Children.Add(namePanel);
        grid.Children.Add(pub);
        grid.Children.Add(size);
        grid.Children.Add(date);
        grid.Children.Add(btn);
        row.Child = grid;
        row.MouseEnter += (s, e) => row.Background = (Brush)FindResource("ItemHoverBrush");
        row.MouseLeave += (s, e) => row.Background = Brushes.Transparent;
        return row;
    }

    private static BitmapSource? TryLoadIcon(string exePath)
    {
        try
        {
            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath)) return null;
            using var icon = Icon.ExtractAssociatedIcon(exePath);
            if (icon == null) return null;
            return Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
        }
        catch { return null; }
    }

    private async Task UninstallAsync(InstalledApp app)
    {
        if (!Services.DialogService.Confirm(_main, $"卸载 {app.DisplayName}？",
            $"应用大小：{app.SizeDisplay}\n\n将启动应用自带的卸载程序，请在卸载向导中完成操作。", "开始卸载"))
            return;

        var err = _inventory.LaunchUninstall(app);
        if (err != null)
        {
            _main.ShowToast("无法启动卸载程序：" + err, ToastType.Warning);
            return;
        }

        // 等待卸载完成（轮询注册表条目是否消失，最多 5 分钟）
        LoadingPanel.Visibility = Visibility.Visible;
        LoadingText.Text = $"等待「{app.DisplayName}」卸载完成…\n完成卸载向导后会自动继续";
        bool gone = false;
        await Task.Run(async () =>
        {
            for (int i = 0; i < 100; i++)
            {
                await Task.Delay(3000);
                try { if (!_inventory.IsStillInstalled(app)) { gone = true; break; } }
                catch { }
            }
        });
        LoadingPanel.Visibility = Visibility.Collapsed;

        if (!gone)
        {
            _main.ShowToast("未检测到卸载完成，可能已取消", ToastType.Info);
            return;
        }

        HistoryService.Add("Uninstall", $"卸载了应用「{app.DisplayName}」", app.Publisher, app.EstimatedSize);
        _main.ShowToast($"「{app.DisplayName}」已卸载");
        await LoadAsync();

        // 残留扫描
        var leftovers = await Task.Run(() => _inventory.FindLeftovers(app));
        leftovers.RemoveAll(l => !Directory.Exists(l.Path) && !l.IsRegistry);
        if (leftovers.Count > 0)
        {
            ShowLeftoverDialog(app, leftovers);
        }
    }

    private void ShowLeftoverDialog(InstalledApp app, List<LeftoverItem> leftovers)
    {
        var dlg = new Windows.LeftoverDialog(app.DisplayName, leftovers) { Owner = _main };
        dlg.ShowDialog();
        if (dlg.Confirmed)
        {
            var (ok, fail) = _inventory.RemoveLeftovers(leftovers.Where(l => l.Selected).ToList());
            _main.ShowToast($"已清理 {ok} 项残留" + (fail > 0 ? $"，{fail} 项失败" : ""),
                fail > 0 ? ToastType.Warning : ToastType.Success);
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _filter = SearchBox.Text;
        if (_loaded) Render();
    }

    private void Sort_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loaded) Render();
    }

    private void BtnRefresh_Click(object sender, RoutedEventArgs e) => _ = LoadAsync();
}
