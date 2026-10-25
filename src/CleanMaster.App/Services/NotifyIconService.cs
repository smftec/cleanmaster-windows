using System.Reflection;
using System.Windows;
using CleanMaster.Core.Store;

namespace CleanMaster.App.Services;

/// <summary>系统托盘图标与右键菜单；语言切换时重建菜单。</summary>
public sealed class NotifyIconService : IDisposable
{
    private readonly MainWindow _main;
    private System.Windows.Forms.NotifyIcon? _icon;
    private System.Windows.Forms.ContextMenuStrip? _menu;

    public NotifyIconService(MainWindow main) => _main = main;

    public void Initialize()
    {
        _icon = new System.Windows.Forms.NotifyIcon
        {
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!),
            Text = ToolTipText(),
            Visible = true,
        };
        BuildMenu();
        _icon.DoubleClick += (s, e) => _main.RestoreFromTray();

        Loc.LanguageChanged += RebuildOnLanguageChanged;
    }

    private static string ToolTipText()
    {
        var v = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion.Split('+')[0];
        return string.IsNullOrWhiteSpace(v) ? "CleanMaster" : $"CleanMaster v{v}";
    }

    private void BuildMenu()
    {
        var old = _menu;
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add(Loc.T("tray.open"), null, (s, e) => _main.RestoreFromTray());
        menu.Items.Add(Loc.T("tray.quickscan"), null, (s, e) =>
        {
            _main.RestoreFromTray();
            _main.Navigate(PageKey.SmartScan);
            if (_main.PageHost.Content is Pages.SmartScanPage p) p.StartScanFromUi();
        });
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        var autoItem = new System.Windows.Forms.ToolStripMenuItem(AutoCleanText())
        {
            Checked = SettingsService.Current.AutoCleanEnabled,
        };
        autoItem.Click += (s, e) =>
        {
            SettingsService.Current.AutoCleanEnabled = !SettingsService.Current.AutoCleanEnabled;
            autoItem.Checked = SettingsService.Current.AutoCleanEnabled;
            autoItem.Text = AutoCleanText();
            SettingsService.Save();
        };
        menu.Items.Add(autoItem);
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add(Loc.T("tray.exit"), null, (s, e) => _main.ForceClose());

        _menu = menu;
        if (_icon != null) _icon.ContextMenuStrip = menu;
        if (_icon != null) _icon.Text = ToolTipText();
        old?.Dispose();
    }

    private void RebuildOnLanguageChanged()
    {
        if (_icon == null) return;
        // 托盘与主窗口同线程,语言切换事件即在 UI 线程触发
        BuildMenu();
    }

    internal static string AutoCleanText() =>
        Loc.T("tray.autoclean") + (SettingsService.Current.AutoCleanEnabled ? Loc.T("tray.on") : Loc.T("tray.off"));

    public void Dispose()
    {
        Loc.LanguageChanged -= RebuildOnLanguageChanged;
        if (_icon != null)
        {
            _icon.Visible = false;
            _icon.Dispose();
            _icon = null;
        }
        _menu?.Dispose();
    }
}
