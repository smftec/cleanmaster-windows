using System.Windows;
using CleanMaster.Core.Store;

namespace CleanMaster.App.Services;

/// <summary>系统托盘图标与右键菜单。</summary>
public sealed class NotifyIconService : IDisposable
{
    private readonly MainWindow _main;
    private System.Windows.Forms.NotifyIcon? _icon;

    public NotifyIconService(MainWindow main) => _main = main;

    public void Initialize()
    {
        _icon = new System.Windows.Forms.NotifyIcon
        {
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!),
            Text = "CleanMaster",
            Visible = true,
        };
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add(Services.Loc.T("tray.open"), null, (s, e) => _main.RestoreFromTray());
        menu.Items.Add(Services.Loc.T("tray.quickscan"), null, (s, e) =>
        {
            _main.RestoreFromTray();
            _main.Navigate(PageKey.SmartScan);
            if (_main.PageHost.Content is Pages.SmartScanPage p) p.StartScanFromUi();
        });
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        var autoItem = new System.Windows.Forms.ToolStripMenuItem("自动清理：关")
        {
            Checked = SettingsService.Current.AutoCleanEnabled,
        };
        autoItem.Click += (s, e) =>
        {
            SettingsService.Current.AutoCleanEnabled = !SettingsService.Current.AutoCleanEnabled;
            autoItem.Checked = SettingsService.Current.AutoCleanEnabled;
            autoItem.Text = AutoCleanText();
            autoItem.Text = AutoCleanText();
            SettingsService.Save();
        };
        menu.Items.Add(autoItem);
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add(Services.Loc.T("tray.exit"), null, (s, e) => { _main.ForceClose(); });
        _icon.ContextMenuStrip = menu;
        _icon.DoubleClick += (s, e) => _main.RestoreFromTray();

        _main.StateChanged += (s, e) => { };
    }

    internal static string AutoCleanText() =>
        Services.Loc.T("tray.autoclean") + (SettingsService.Current.AutoCleanEnabled ? Services.Loc.T("tray.on") : Services.Loc.T("tray.off"));

    public void Dispose()
    {
        if (_icon != null)
        {
            _icon.Visible = false;
            _icon.Dispose();
            _icon = null;
        }
    }
}
