using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace CleanMaster.App.Controls;

public sealed class CommandItem
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string Subtitle { get; init; } = "";
    public required string IconKey { get; init; }
}

/// <summary>Ctrl+K 命令面板:全局功能搜索与快速执行。</summary>
public partial class CommandPalette : System.Windows.Controls.UserControl
{
    private List<(CommandItem Item, int Rank)> _filtered = new();

    public event Action<CommandItem>? ActionInvoked;

    public bool IsOpen => Visibility == Visibility.Visible;

    public CommandPalette()
    {
        InitializeComponent();
        BuildList("");
        List.SelectionChanged += (s, e) => { };
    }

    // ————— 开关 —————

    public void Open()
    {
        Visibility = Visibility.Visible;
        Input.Text = "";
        BuildList("");
        Input.Focus();
        Keyboard.Focus(Input);
    }

    public void Close() => Visibility = Visibility.Collapsed;

    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    // ————— 数据源 —————

    private static string T(string key) => Services.Loc.T(key);

    private static List<CommandItem> AllActions() =>
    [
        new() { Id = "page.Home", Title = T("nav.home"), Subtitle = T("cmd.cat.nav"), IconKey = "HomeIcon" },
        new() { Id = "page.SmartScan", Title = T("nav.scan"), Subtitle = T("cmd.cat.nav"), IconKey = "ScanIcon" },
        new() { Id = "page.CleanSpace", Title = T("nav.clean"), Subtitle = T("cmd.cat.nav"), IconKey = "CleanIcon" },
        new() { Id = "page.SpaceAnalysis", Title = T("nav.space"), Subtitle = T("cmd.cat.nav"), IconKey = "PieIcon" },
        new() { Id = "page.Startup", Title = T("nav.startup"), Subtitle = T("cmd.cat.nav"), IconKey = "PowerIcon" },
        new() { Id = "page.Apps", Title = T("nav.apps"), Subtitle = T("cmd.cat.nav"), IconKey = "AppsIcon" },
        new() { Id = "page.Toolbox", Title = T("nav.toolbox"), Subtitle = T("cmd.cat.nav"), IconKey = "ToolboxIcon" },
        new() { Id = "page.Settings", Title = T("nav.settings"), Subtitle = T("cmd.cat.nav"), IconKey = "GearIcon" },
        new() { Id = "act.quickscan", Title = T("cmd.quickscan"), Subtitle = T("cmd.cat.action"), IconKey = "BroomIcon" },
        new() { Id = "act.analyze", Title = T("cmd.analyze"), Subtitle = T("cmd.cat.action"), IconKey = "PieIcon" },
        new() { Id = "act.dupscan", Title = T("cmd.dupscan"), Subtitle = T("cmd.cat.action"), IconKey = "RefreshIcon" },
        new() { Id = "act.emptyscan", Title = T("cmd.emptyscan"), Subtitle = T("cmd.cat.action"), IconKey = "FolderIcon" },
        new() { Id = "act.quarantine", Title = T("cmd.quarantine"), Subtitle = T("cmd.cat.action"), IconKey = "UndoIcon" },
        new() { Id = "act.history", Title = T("cmd.history"), Subtitle = T("cmd.cat.action"), IconKey = "ClockIcon" },
        new() { Id = "act.dns", Title = T("cmd.dnsflush"), Subtitle = T("cmd.cat.action"), IconKey = "GlobeIcon" },
        new() { Id = "act.theme", Title = T("cmd.theme.toggle"), Subtitle = T("cmd.cat.action"), IconKey = "GearIcon" },
        new() { Id = "act.update", Title = T("cmd.checkupdate"), Subtitle = T("cmd.cat.action"), IconKey = "InfoIcon" },
    ];

    private void BuildList(string query)
    {
        var all = AllActions();
        _filtered = all
            .Select(it => (it, Rank: MatchRank(it.Title, query)))
            .Where(t => query.Length == 0 || t.Rank > 0)
            .OrderByDescending(t => t.Rank)
            .ThenBy(t => all.IndexOf(t.it))
            .Take(9)
            .ToList();

        List.Items.Clear();
        foreach (var (item, _) in _filtered)
        {
            var row = new Border { CornerRadius = new CornerRadius(9), Padding = new Thickness(12, 8, 12, 8) };
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var icon = new Border
            {
                Width = 30, Height = 30, CornerRadius = new CornerRadius(9),
                Background = (Brush)FindResource("PrimarySoftBrush"),
                VerticalAlignment = VerticalAlignment.Center,
            };
            icon.Child = new System.Windows.Shapes.Path
            {
                Data = (Geometry)FindResource(item.IconKey),
                Stroke = (Brush)FindResource("PrimaryBrush"),
                StrokeThickness = 1.7,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round,
                Width = 15, Height = 15, Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };

            var mid = new StackPanel { Margin = new Thickness(12, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
            mid.Children.Add(new TextBlock
            {
                Text = item.Title,
                FontSize = 13.5,
                Foreground = (Brush)FindResource("TextPrimaryBrush"),
            });
            mid.Children.Add(new TextBlock
            {
                Text = item.Subtitle,
                FontSize = 10.5,
                Foreground = (Brush)FindResource("TextTertiaryBrush"),
                Margin = new Thickness(0, 2, 0, 0),
            });

            var chevron = new System.Windows.Shapes.Path
            {
                Data = (Geometry)FindResource("ChevronRightIcon"),
                Stroke = (Brush)FindResource("TextTertiaryBrush"),
                StrokeThickness = 2,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Width = 9, Height = 9, Stretch = Stretch.None,
                VerticalAlignment = VerticalAlignment.Center,
            };

            Grid.SetColumn(icon, 0);
            Grid.SetColumn(mid, 1);
            Grid.SetColumn(chevron, 2);
            grid.Children.Add(icon);
            grid.Children.Add(mid);
            grid.Children.Add(chevron);
            row.Child = grid;
            List.Items.Add(new ListBoxItem
            {
                Content = row,
                Tag = item,
                Padding = new Thickness(0),
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
            });
        }

        EmptyText.Visibility = _filtered.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (_filtered.Count > 0) List.SelectedIndex = 0;
    }

    /// <summary>前缀 100 / 包含 60 / 子序列 30 / 不匹配 0。</summary>
    private static int MatchRank(string title, string query)
    {
        if (query.Length == 0) return 1;
        if (title.StartsWith(query, StringComparison.OrdinalIgnoreCase)) return 100;
        var idx = title.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        if (idx >= 0) return 60;
        // 子序列
        int t = 0;
        foreach (var c in query)
        {
            t = title.IndexOf(char.ToString(c), t, StringComparison.OrdinalIgnoreCase);
            if (t < 0) return 0;
            t++;
        }
        return 30;
    }

    // ————— 事件 —————

    private void Input_TextChanged(object sender, TextChangedEventArgs e)
    {
        Placeholder.Visibility = Input.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        BuildList(Input.Text.Trim());
    }

    private void Input_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
        else if (e.Key == Key.Down)
        {
            if (List.SelectedIndex < List.Items.Count - 1) List.SelectedIndex++;
            List.ScrollIntoView(List.SelectedItem);
            e.Handled = true;
        }
        else if (e.Key == Key.Up)
        {
            if (List.SelectedIndex > 0) List.SelectedIndex--;
            List.ScrollIntoView(List.SelectedItem);
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            InvokeSelected();
            e.Handled = true;
        }
    }

    private void List_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        // 点击项立即执行
        if (e.OriginalSource is DependencyObject src)
        {
            var lbi = FindParent<ListBoxItem>(src);
            if (lbi?.Tag is CommandItem item)
            {
                ActionInvoked?.Invoke(item);
                Close();
                e.Handled = true;
            }
        }
    }

    private void InvokeSelected()
    {
        if (List.SelectedItem is ListBoxItem { Tag: CommandItem item })
        {
            ActionInvoked?.Invoke(item);
            Close();
        }
    }

    private static T? FindParent<T>(DependencyObject node) where T : DependencyObject
    {
        while (node != null)
        {
            if (node is T t) return t;
            node = System.Windows.Media.VisualTreeHelper.GetParent(node);
        }
        return null;
    }

    private void Dim_MouseDown(object sender, MouseButtonEventArgs e) => Close();
}
