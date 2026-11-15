using System.Windows;
using CleanMaster.App.Windows;

namespace CleanMaster.App.Services;

/// <summary>主题化对话框（替代系统 MessageBox）。图标随严重级变化。</summary>
public static class DialogService
{
    public static void Info(Window? owner, string title, string message) =>
        Show(owner, title, message, [(Loc.T("btn.ok"), false, true)], DialogSeverity.Info);

    public static bool Confirm(Window? owner, string title, string message,
        string? confirmText = null, string? cancelText = null, bool danger = false) =>
        Show(owner, title, message,
        [
            (cancelText ?? Loc.T("btn.cancel"), false, false),
            (confirmText ?? Loc.T("btn.ok"), danger, true),
        ], danger ? DialogSeverity.Danger : DialogSeverity.Question);

    public static bool Show(Window? owner, string title, string message,
        IReadOnlyList<(string text, bool danger, bool isPrimary)> buttons,
        DialogSeverity severity = DialogSeverity.Question)
    {
        var dlg = new DialogWindow(owner, title, message, buttons, severity);
        dlg.ShowDialog();
        return dlg.ResultIndex >= 0 && dlg.ResultIndex == buttons.Count - 1;
    }
}
