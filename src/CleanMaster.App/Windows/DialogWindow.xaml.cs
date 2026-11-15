using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using CleanMaster.App.Services;

namespace CleanMaster.App.Windows;

public enum DialogSeverity { Info, Question, Success, Warning, Danger }

public partial class DialogWindow : Window
{
    public int ResultIndex { get; private set; } = -1;

    public DialogWindow(Window? owner, string title, string message,
        IReadOnlyList<(string text, bool danger, bool isPrimary)> buttons,
        DialogSeverity severity = DialogSeverity.Question)
    {
        InitializeComponent();
        if (owner != null && owner.IsLoaded) Owner = owner;
        TitleText.Text = title;
        MessageText.Text = message;
        ApplySeverity(severity, danger: buttons.Any(b => b.danger));

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
                // 回车确认非危险主按钮;危险操作必须显式点击
                IsDefault = isPrimary && !danger,
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

    /// <summary>图形与颜色随严重级变化(软底圆 + 图标),颜色走 StatusColors 单一阈值。</summary>
    private void ApplySeverity(DialogSeverity severity, bool danger)
    {
        (string glyph, string bg, string fg) cfg = severity switch
        {
            DialogSeverity.Success => ("CheckIcon", "SuccessSoftBrush", "SuccessBrush"),
            DialogSeverity.Warning => ("WarningIcon", "WarningSoftBrush", "WarningBrush"),
            DialogSeverity.Danger => ("WarningIcon", "DangerSoftBrush", "DangerBrush"),
            DialogSeverity.Question => danger ? ("WarningIcon", "WarningSoftBrush", "WarningBrush")
                                              : ("InfoIcon", "PrimarySoftBrush", "PrimaryBrush"),
            _ => ("InfoIcon", "PrimarySoftBrush", "PrimaryBrush"),
        };
        IconBg.Fill = (Brush)FindResource(cfg.bg);
        IconGlyph.Stroke = (Brush)FindResource(cfg.fg);
        IconGlyph.Data = (Geometry)FindResource(cfg.glyph);
    }
}
