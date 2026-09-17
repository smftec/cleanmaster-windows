using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace CleanMaster.App.Windows;

public partial class DialogWindow : Window
{
    public int ResultIndex { get; private set; } = -1;

    public DialogWindow(Window? owner, string title, string message,
        IReadOnlyList<(string text, bool danger, bool isPrimary)> buttons)
    {
        InitializeComponent();
        if (owner != null && owner.IsLoaded) Owner = owner;
        TitleText.Text = title;
        MessageText.Text = message;

        int idx = 0;
        foreach (var (text, danger, isPrimary) in buttons)
        {
            var captured = idx;
            var btn = new Button
            {
                Content = text,
                Margin = new Thickness(10, 0, 0, 0),
                Style = danger ? (Style)FindResource("DangerButton")
                      : isPrimary ? (Style)FindResource("PrimaryButton")
                      : (Style)FindResource("SecondaryButton"),
                MinWidth = 96,
            };
            btn.Click += (s, e) => { ResultIndex = captured; Close(); };
            ButtonRow.Children.Add(btn);
            idx++;
        }

        PreviewKeyDown += (s, e) =>
        {
            if (e.Key == Key.Escape) Close();
        };

        MouseLeftButtonDown += (s, e) =>
        {
            if (e.ButtonState == MouseButtonState.Pressed) DragMove();
        };
    }
}
