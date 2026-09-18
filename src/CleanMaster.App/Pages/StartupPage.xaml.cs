using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CleanMaster.Core.Store;
using CleanMaster.Core.Startup;
using CleanMaster.Core.Utils;

namespace CleanMaster.App.Pages;

public partial class StartupPage : Page, IParamPage
{
    private readonly MainWindow _main;
    private readonly StartupService _service = new();
    private List<StartupItem> _items = new();
    private string _filter = "";
    private bool _loaded;

    public StartupPage(MainWindow main)
    {
        InitializeComponent();
        _main = main;
        Loaded += (s, e) =>
        {
            if (!_loaded)
            {
                _loaded = true;
                _ = LoadAsync();
            }
        };
    }

    public void OnNavigated(object? param) => _ = LoadAsync();

    private async Task LoadAsync()
    {
        LoadingPanel.Visibility = Visibility.Visible;
        ItemList.Children.Clear();
        EmptyText.Visibility = Visibility.Collapsed;
        try
        {
            _items = await Task.Run(() => _service.Enumerate());
        }
        catch (Exception ex)
        {
            _main.ShowToast(Services.Loc.T("toast.readfailed") + ex.Message, ToastType.Warning);
        }
        LoadingPanel.Visibility = Visibility.Collapsed;
        Render();
    }

    private void Render()
    {
        ItemList.Children.Clear();
        var items = _items.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(_filter))
            items = items.Where(i => i.Name.Contains(_filter, StringComparison.OrdinalIgnoreCase) ||
                                     i.Publisher.Contains(_filter, StringComparison.OrdinalIgnoreCase));
        var list = items.OrderBy(i => !i.Enabled).ThenBy(i => i.Impact switch
        {
            "高" or "High" => 0,
            "中" or "Medium" => 1,
            "未测量" => 2,
            _ => 3,
        }).ToList();

        foreach (var item in list)
            ItemList.Children.Add(BuildRow(item));
        EmptyText.Visibility = list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private Border BuildRow(StartupItem item)
    {
        var row = new Border { Padding = new Thickness(14, 11, 14, 11), CornerRadius = new CornerRadius(9) };
        var grid = new Grid();
        for (int i = 0; i < 5; i++) grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions[0].Width = new GridLength(56);
        grid.ColumnDefinitions[1].Width = new GridLength(2.2, GridUnitType.Star);
        grid.ColumnDefinitions[2].Width = new GridLength(0.8, GridUnitType.Star);
        grid.ColumnDefinitions[3].Width = new GridLength(1.3, GridUnitType.Star);
        grid.ColumnDefinitions[4].Width = new GridLength(1.1, GridUnitType.Star);

        // 开关
        var toggle = new ToggleButton
        {
            Style = (Style)FindResource("ToggleSwitch"),
            IsChecked = item.Enabled,
            IsEnabled = item.CanToggle,
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = item.CanToggle ? (item.Enabled ? "点击禁用开机启动" : "点击启用开机启动") : item.Note,
        };
        Grid.SetColumn(toggle, 0);
        toggle.Click += async (s, e) =>
        {
            var enable = toggle.IsChecked == true;
            toggle.IsEnabled = false;
            var err = await _service.SetEnabledAsync(item, enable);
            toggle.IsEnabled = true;
            if (err != null)
            {
                toggle.IsChecked = !enable;
                _main.ShowToast(err, ToastType.Warning);
            }
            else
            {
                HistoryService.Add("StartupToggle",
                    string.Format(Services.Loc.T("startup.toggled"), (enable ? Services.Loc.T("startup.enabled") : Services.Loc.T("startup.disabled")), item.Name));
                _main.ShowToast(string.Format("{0} \"{1}\"", enable ? Services.Loc.T("startup.enabled") : Services.Loc.T("startup.disabled"), item.Name));
            }
        };

        // 名称
        var namePanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var nameLine = new StackPanel { Orientation = Orientation.Horizontal };
        nameLine.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(item.Publisher) ? item.Name : $"{item.Name}",
            FontSize = 13.5,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)FindResource("TextPrimaryBrush"),
        });
        if (item.IsSystemComponent)
        {
            nameLine.Children.Add(RuleRowFactory.Chip(Services.Loc.T("startup.systemchip"), (Brush)FindResource("PrimaryBrush"), new Thickness(8, 0, 0, 0)));
        }
        namePanel.Children.Add(nameLine);
        var pub = item.Publisher;
        var cmd = new TextBlock
        {
            Text = string.IsNullOrEmpty(item.Command) ? item.FilePath : item.Command,
            FontSize = 11,
            Foreground = (Brush)FindResource("TextTertiaryBrush"),
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 3, 0, 0),
        };
        ToolTipService.SetToolTip(cmd, $"{item.Command}\n发布者：{(string.IsNullOrEmpty(pub) ? "未知" : pub)}");
        namePanel.Children.Add(cmd);
        if (!string.IsNullOrWhiteSpace(item.Publisher))
        {
            namePanel.Children.Add(new TextBlock
            {
                Text = pub,
                FontSize = 10.5,
                Foreground = (Brush)FindResource("TextTertiaryBrush"),
                Margin = new Thickness(0, 2, 0, 0),
            });
        }
        Grid.SetColumn(namePanel, 1);

        // 影响
        var impactBorder = new Border
        {
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(9, 3, 9, 4),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Background = item.Impact switch
            {
                "高" or "High" => (Brush)FindResource("DangerSoftBrush"),
                "中" or "Medium" => (Brush)FindResource("WarningSoftBrush"),
                _ => (Brush)FindResource("PrimarySoftBrush"),
            },
        };
        impactBorder.Child = new TextBlock
        {
            Text = StartupImpactText.Of(item.Impact),
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            Foreground = item.Impact switch
            {
                "高" or "High" => (Brush)FindResource("DangerBrush"),
                "中" or "Medium" => (Brush)FindResource("OrangeBrush"),
                _ => (Brush)FindResource("TextSecondaryBrush"),
            },
        };
        ToolTipService.SetToolTip(impactBorder, "基于应用类型的参考评估（类似任务管理器的启动影响），非实测数据");
        Grid.SetColumn(impactBorder, 2);

        // 来源
        var source = new TextBlock
        {
            Text = StartupTexts.Of(item.Source),
            FontSize = 12,
            Foreground = (Brush)FindResource("TextSecondaryBrush"),
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(source, 3);

        // 建议
        var suggest = new TextBlock
        {
            Text = StartupSuggestText.Of(item.Suggestion),
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = item.Suggestion switch
            {
                "可考虑关闭" => (Brush)FindResource("WarningBrush"),
                "系统组件" => (Brush)FindResource("TextTertiaryBrush"),
                _ => (Brush)FindResource("TextSecondaryBrush"),
            },
        };
        Grid.SetColumn(suggest, 4);

        grid.Children.Add(toggle);
        grid.Children.Add(namePanel);
        grid.Children.Add(impactBorder);
        grid.Children.Add(source);
        grid.Children.Add(suggest);
        row.Child = grid;
        row.MouseEnter += (s, e) => row.Background = (Brush)FindResource("ItemHoverBrush");
        row.MouseLeave += (s, e) => row.Background = Brushes.Transparent;
        return row;
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _filter = SearchBox.Text;
        if (_loaded) Render();
    }

    private void BtnRefresh_Click(object sender, RoutedEventArgs e) => _ = LoadAsync();
}
