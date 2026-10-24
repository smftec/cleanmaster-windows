using System.Diagnostics;
using System.Windows;
using CleanMaster.Core.Store;

namespace CleanMaster.App.Services;

/// <summary>命令面板 / 工具箱共用的快捷动作。</summary>
public static class QuickActions
{
    public static async void FlushDns(MainWindow main)
    {
        try
        {
            var psi = new ProcessStartInfo("ipconfig", "/flushdns")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
            };
            var p = Process.Start(psi);
            if (p != null) await p.WaitForExitAsync();
            main.ShowToast(Loc.T("tool.win.dns.ok"), ToastType.Success);
        }
        catch (Exception ex)
        {
            main.ShowToast(Loc.T("tool.win.dns.fail") + ex.Message, ToastType.Warning);
        }
    }

    public static async void CheckUpdate(MainWindow main)
    {
        main.ShowToast(Loc.T("update.checking"), ToastType.Info);
        var info = await UpdateService.CheckAsync();
        if (info.HasUpdate)
        {
            main.ShowToast(string.Format(Loc.T("update.available"), info.Latest), ToastType.Info);
            var open = DialogService.Confirm(main,
                string.Format(Loc.T("update.dlg.title"), info.Latest),
                string.Format(Loc.T("update.dlg.msg"), info.Current, info.Latest),
                Loc.T("update.dlg.open"), Loc.T("btn.cancel"));
            if (open)
                Process.Start(new ProcessStartInfo(info.DownloadUrl) { UseShellExecute = true });
        }
        else if (info.Error.Length > 0)
        {
            main.ShowToast(Loc.T("update.failed") + " " + info.Error, ToastType.Warning);
        }
        else
        {
            main.ShowToast(Loc.T("update.latest"), ToastType.Success);
        }
    }

    /// <summary>浅色 ↔ 深色互切(system 视为浅色)。</summary>
    public static void ToggleTheme(MainWindow main)
    {
        var s = SettingsService.Current;
        s.Theme = s.Theme == "dark" ? "light" : "dark";
        SettingsService.Save();
        ThemeManager.ApplyFromSettings();
        main.ShowToast(Loc.T("toast.themeapplied"), ToastType.Info);
    }
}
