using System.Windows;
using CleanMaster.App.Services;
using CleanMaster.App.Windows;
using CleanMaster.Core.Rules;
using CleanMaster.Core.SelfTest;
using CleanMaster.Core.Store;

namespace CleanMaster.App;

public partial class App : Application
{
    private static Mutex? _singleInstanceMutex;
    private NotifyIconService? _tray;

    public static new App Current => (App)Application.Current;

    protected async void OnStartup(object sender, StartupEventArgs e)
    {
        // —— 提权辅助实例：执行管理任务后立即退出，不进入 UI ——
        if (e.Args.Length >= 1 && e.Args[0] == "--elevated")
        {
            var taskId = e.Args.Length > 1 ? e.Args[1] : "";
            var argsJson = e.Args.Length > 2 ? e.Args[2] : "{}";
            var resultFile = e.Args.Length > 3 ? e.Args[3] : Path.Combine(Path.GetTempPath(), "CleanMaster_elev_result.json");
            ElevatedService.ExecuteElevatedTask(taskId, argsJson, resultFile);
            Shutdown(0);
            return;
        }

        // —— 命令行安全自测 ——
        if (e.Args.Any(a => a == "--selftest"))
        {
            SettingsService.Load();
            // 在工作线程运行，避免 UI 线程同步上下文与异步等待死锁
            var code = System.Threading.Tasks.Task.Run(() => SelfTest.RunAll()).GetAwaiter().GetResult();
            Shutdown(code);
            return;
        }

        // —— 单实例 ——
        _singleInstanceMutex = new Mutex(true, "CleanMaster_SingleInstance_2F1A", out var isNew);
        if (!isNew)
        {
            MessageBox.Show("清理优化大师已经在运行了。", "清理优化大师",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown(0);
            return;
        }

        SettingsService.Load();
        QuarantineService.Instance.PurgeExpired();
        ThemeManager.ApplyFromSettings();
        Loc.ApplyFromSettings();

        // 全局异常兜底：记录并提示，不闪退
        DispatcherUnhandledException += (s, args) =>
        {
            try { HistoryService.Add("Error", "发生了一个内部错误", args.Exception.Message); } catch { }
            MessageBox.Show($"发生了一个内部错误：\n{args.Exception.Message}", "清理优化大师",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            args.Handled = true;
        };

        var win = new MainWindow();
        MainWindow = win;
        win.Show();

        _tray = new NotifyIconService(win);
        _tray.Initialize();

        // 首次启动引导
        if (!Services.FirstRunWizard.WasWelcomed())
        {
            var wizard = new Windows.FirstRunWizard { Owner = win };
            wizard.ShowDialog();
        }

        // --page <key> [--param <value>] 深链接（用于自动化验收 / 快捷入口）
        var pageIdx = Array.IndexOf(e.Args, "--page");
        if (pageIdx >= 0 && pageIdx + 1 < e.Args.Length &&
            Enum.TryParse<PageKey>(e.Args[pageIdx + 1], ignoreCase: true, out var pk))
        {
            var paramIdx = Array.IndexOf(e.Args, "--param");
            var navParam = paramIdx >= 0 && paramIdx + 1 < e.Args.Length ? e.Args[paramIdx + 1] : null;
            win.Navigate(pk, navParam);
        }

        // 开机自启注册
        StartupRegistration.Sync(SettingsService.Current.LaunchAtStartup);

        // 自动清理调度
        AutoCleanScheduler.Instance.Initialize(win);
    }

    protected void OnExit(object sender, ExitEventArgs e)
    {
        _tray?.Dispose();
        // 设置仅在用户修改时落盘，避免旧快照覆盖新配置
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
    }
}
