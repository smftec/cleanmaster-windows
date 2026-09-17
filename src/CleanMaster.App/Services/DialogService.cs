using System.Windows;
using System.Windows.Media;
using CleanMaster.App.Windows;

namespace CleanMaster.App.Services;

/// <summary>主题化对话框（替代系统 MessageBox）。</summary>
public static class DialogService
{
    public static void Info(Window? owner, string title, string message) =>
        Show(owner, title, message, [("知道了", false, true)]);

    public static bool Confirm(Window? owner, string title, string message,
        string confirmText = "确定", string cancelText = "取消", bool danger = false) =>
        Show(owner, title, message,
        [
            (cancelText, false, false),
            (confirmText, danger, true),
        ]);

    public static bool Show(Window? owner, string title, string message,
        IReadOnlyList<(string text, bool danger, bool isPrimary)> buttons)
    {
        var dlg = new DialogWindow(owner, title, message, buttons);
        dlg.ShowDialog();
        return dlg.ResultIndex >= 0 && dlg.ResultIndex == buttons.Count - 1;
    }
}
