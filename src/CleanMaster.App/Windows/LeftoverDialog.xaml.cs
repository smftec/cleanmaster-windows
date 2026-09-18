using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CleanMaster.Core.Apps;
using CleanMaster.Core.Utils;

namespace CleanMaster.App.Windows;

public partial class LeftoverDialog : Window
{
    public bool Confirmed { get; private set; }
    private readonly List<LeftoverItem> _items;

    public LeftoverDialog(string appName, List<LeftoverItem> items)
    {
        InitializeComponent();
        _items = items;
        TitleText.Text = string.Format(Services.Loc.T("leftover.title.f"), appName);
        MouseLeftButtonDown += (s, e) => { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); };
        foreach (var item in items)
        {
            ItemList.Children.Add(BuildRow(item));
        }
    }

    private UIElement BuildRow(LeftoverItem item)
    {
        var row = new Border
        {
            Padding = new Thickness(10, 8, 10, 8),
            CornerRadius = new CornerRadius(9),
            Background = (Brush)FindResource("ItemHoverBrush"),
            Margin = new Thickness(0, 4, 0, 0),
        };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var cb = new CheckBox
        {
            Style = (Style)FindResource("RoundCheckOnly"),
            IsChecked = item.Selected,
            VerticalAlignment = VerticalAlignment.Center,
        };
        cb.Checked += (s, e) => item.Selected = true;
        cb.Unchecked += (s, e) => item.Selected = false;
        Grid.SetColumn(cb, 0);

        var sp = new StackPanel { Margin = new Thickness(12, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
        sp.Children.Add(new TextBlock
        {
            Text = item.Path,
            FontSize = 12,
            Foreground = (Brush)FindResource("TextPrimaryBrush"),
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        ToolTipService.SetToolTip(sp, item.Path);
        sp.Children.Add(new TextBlock
        {
            Text = item.IsRegistry ? Services.Loc.T("leftover.reg") : LeftoverDesc.Of(item.Description),
            FontSize = 11,
            Foreground = (Brush)FindResource("TextTertiaryBrush"),
            Margin = new Thickness(0, 2, 0, 0),
        });
        Grid.SetColumn(sp, 1);

        var size = new TextBlock
        {
            Text = item.IsRegistry ? "—" : SizeText.OfBytes(item.Size),
            FontSize = 12.5,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)FindResource("TextPrimaryBrush"),
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(size, 2);

        grid.Children.Add(cb);
        grid.Children.Add(sp);
        grid.Children.Add(size);
        row.Child = grid;
        return row;
    }

    private void BtnClean_Click(object sender, RoutedEventArgs e)
    {
        if (_items.All(i => !i.Selected))
        {
            Confirmed = false;
            Close();
            return;
        }
        Confirmed = true;
        Close();
    }

    private void BtnSkip_Click(object sender, RoutedEventArgs e)
    {
        Confirmed = false;
        Close();
    }
}
