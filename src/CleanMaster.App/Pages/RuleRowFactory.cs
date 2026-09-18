using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using CleanMaster.Core.Models;
using CleanMaster.Core.Utils;

namespace CleanMaster.App.Pages;

/// <summary>规则行 UI 工厂：智能扫描页与清理空间页共用。</summary>
public static class RuleRowFactory
{
    public sealed class Handle
    {
        public ScanRowVM Vm { get; init; } = null!;
        public CheckBox Box { get; init; } = null!;
        public ToggleButton Toggle { get; init; } = null!;
        public StackPanel Details { get; init; } = null!;
        public Border Row { get; init; } = null!;
    }

    public static Handle Create(ScanRowVM vm, FrameworkElement resources, Action onSelectionChanged)
    {
        var row = new Border { Padding = new Thickness(12, 10, 12, 10), CornerRadius = new CornerRadius(9) };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var cb = new CheckBox
        {
            Style = (Style)resources.FindResource("RoundCheckOnly"),
            IsChecked = vm.Selected,
            VerticalAlignment = VerticalAlignment.Center,
        };
        cb.Checked += (s, e) => { vm.Selected = true; onSelectionChanged(); };
        cb.Unchecked += (s, e) => { vm.Selected = false; onSelectionChanged(); };
        vm.CheckBox = cb;
        Grid.SetColumn(cb, 0);

        var mid = new StackPanel { Margin = new Thickness(12, 0, 8, 0) };
        var line1 = new StackPanel { Orientation = Orientation.Horizontal };
        line1.Children.Add(new TextBlock
        {
            Text = vm.Name,
            FontSize = 13.5,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)resources.FindResource("TextPrimaryBrush"),
        });
        if (vm.Risk == RiskLevel.Confirm || vm.Risk == RiskLevel.High)
            line1.Children.Add(Chip(vm.RiskText, vm.Risk == RiskLevel.High
                ? (Brush)resources.FindResource("DangerBrush")
                : (Brush)resources.FindResource("OrangeBrush"), new Thickness(10, 0, 0, 0)));
        if (vm.RunningCount > 0)
            line1.Children.Add(Chip(string.Format(Services.Loc.T("row.running"), vm.RunningCount), (Brush)resources.FindResource("OrangeBrush"),
                new Thickness(8, 0, 0, 0)));
        mid.Children.Add(line1);

        var reason = new TextBlock
        {
            Text = vm.Reason + (vm.CanRestore ? " · " + vm.RestoreText : " · " + vm.DirectText),
            FontSize = 11.5,
            Foreground = (Brush)resources.FindResource("TextTertiaryBrush"),
            Margin = new Thickness(0, 3, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        ToolTipService.SetToolTip(reason, $"{vm.Reason}\n\n清理影响：{vm.Impact}");
        mid.Children.Add(reason);
        Grid.SetColumn(mid, 1);

        var size = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        size.Children.Add(new TextBlock
        {
            Text = vm.SizeText,
            FontSize = 14.5,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)resources.FindResource("TextPrimaryBrush"),
            HorizontalAlignment = HorizontalAlignment.Right,
        });
        size.Children.Add(new TextBlock
        {
            Text = vm.CountText,
            FontSize = 11,
            Foreground = (Brush)resources.FindResource("TextTertiaryBrush"),
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 2, 0, 0),
        });
        Grid.SetColumn(size, 2);

        var details = new StackPanel { Visibility = Visibility.Collapsed, Margin = new Thickness(30, 4, 6, 6) };
        var toggle = new ToggleButton
        {
            Style = (Style)resources.FindResource("ToggleSwitch"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(14, 0, 0, 0),
            ToolTip = "查看文件明细",
        };
        toggle.Click += (s, e) =>
        {
            details.Visibility = toggle.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            if (toggle.IsChecked == true) BuildDetails(vm, details, resources);
        };
        Grid.SetColumn(toggle, 3);

        grid.Children.Add(cb);
        grid.Children.Add(mid);
        grid.Children.Add(size);
        grid.Children.Add(toggle);

        var outer = new StackPanel();
        outer.Children.Add(grid);
        outer.Children.Add(details);

        row.Child = outer;
        row.MouseEnter += (s, e) => row.Background = (Brush)resources.FindResource("ItemHoverBrush");
        row.MouseLeave += (s, e) => row.Background = Brushes.Transparent;

        return new Handle { Vm = vm, Box = cb, Toggle = toggle, Details = details, Row = row };
    }

    private static void BuildDetails(ScanRowVM vm, StackPanel host, FrameworkElement resources)
    {
        host.Children.Clear();
        foreach (var it in vm.Res.Items.Take(100))
        {
            var line = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var path = new TextBlock
            {
                Text = it.Path + (it.IsDirectory ? "  (目录)" : ""),
                FontSize = 11,
                Foreground = (Brush)resources.FindResource("TextSecondaryBrush"),
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            ToolTipService.SetToolTip(path, it.Path);
            Grid.SetColumn(path, 0);
            var sz = new TextBlock
            {
                Text = SizeText.OfBytes(it.Size),
                FontSize = 11,
                Foreground = (Brush)resources.FindResource("TextTertiaryBrush"),
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
                Foreground = (Brush)resources.FindResource("TextTertiaryBrush"),
                Margin = new Thickness(0, 3, 0, 0),
            });
        }
    }

    public static Border Chip(string text, Brush brush, Thickness margin)
    {
        var bd = new Border
        {
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8, 2, 8, 3),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = margin,
        };
        bd.Child = new TextBlock
        {
            Text = text,
            FontSize = 10.5,
            FontWeight = FontWeights.Bold,
            Foreground = brush,
        };
        return bd;
    }
}
