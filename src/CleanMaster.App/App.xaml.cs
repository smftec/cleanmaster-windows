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

        // —— 单实例(语言先行,提示语随界面语言) ——
        SettingsService.Load();
        _singleInstanceMutex = new Mutex(true, "CleanMaster_SingleInstance_2F1A", out var isNew);
        if (!isNew)
        {
            ThemeManager.ApplyFromSettings();
            Loc.ApplyFromSettings();
            MessageBox.Show(Loc.T("app.alreadyrunning"), Loc.T("app.title"),
                MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown(0);
            return;
        }

        QuarantineService.Instance.PurgeExpired();
        ThemeManager.ApplyFromSettings();
        Loc.ApplyFromSettings();
        // Core 层用户可见文案(历史记录等)走应用内语言
        Core.Store.CoreLocalizer.Translate = (key, fallback) =>
        {
            var v = TryFindResource(key) as string;
            return string.IsNullOrEmpty(v) ? fallback : v;
        };

        // 全局异常兜底：记录并提示，不闪退
        DispatcherUnhandledException += (s, args) =>
        {
            try { HistoryService.Add("Error", Loc.T("app.error.msg"), args.Exception.Message); } catch { }
            MessageBox.Show($"发生了一个内部错误：\n{args.Exception.Message}", "清理优化大师",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            args.Handled = true;
        };

        // --page <key> [--param <value>] 深链接（用于自动化验收 / 快捷入口）
        // 必须在窗口创建前解析并交给 MainWindow.Loaded 统一导航:
        // 向导 ShowDialog 会泵消息使 Loaded 提前触发,晚赋值会被默认首页导航覆盖
        PageKey? initialPage = null;
        object? initialParam = null;
        var pageIdx = Array.IndexOf(e.Args, "--page");
        if (pageIdx >= 0 && pageIdx + 1 < e.Args.Length &&
            Enum.TryParse<PageKey>(e.Args[pageIdx + 1], ignoreCase: true, out var pk))
        {
            var paramIdx = Array.IndexOf(e.Args, "--param");
            initialPage = pk;
            initialParam = paramIdx >= 0 && paramIdx + 1 < e.Args.Length ? e.Args[paramIdx + 1] : null;
        }

        var win = new MainWindow { InitialPage = initialPage, InitialParam = initialParam };
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

        // 开机自启注册
        StartupRegistration.Sync(SettingsService.Current.LaunchAtStartup);

        // 自动清理调度
        AutoCleanScheduler.Instance.Initialize(win);

        // 静默检查更新（每日最多一次，失败不打扰）
        _ = RunUpdateCheckAsync(win);
    }

    private static async System.Threading.Tasks.Task RunUpdateCheckAsync(MainWindow win)
    {
        try
        {
            if (!Services.UpdateService.ShouldAutoCheck()) return;
            var info = await Services.UpdateService.CheckAsync();
            if (info.HasUpdate)
                win.ShowToast(string.Format(Services.Loc.T("update.available"), info.Latest), ToastType.Info);
        }
        catch { }
    }

    protected void OnExit(object sender, ExitEventArgs e)
    {
        _tray?.Dispose();
        // 设置仅在用户修改时落盘，避免旧快照覆盖新配置
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
    }
}
