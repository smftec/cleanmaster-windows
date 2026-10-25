using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using CleanMaster.App.Pages;
using CleanMaster.Core.Store;

namespace CleanMaster.App;

public enum PageKey { Home, SmartScan, CleanSpace, SpaceAnalysis, Startup, Apps, Toolbox, Settings }

public partial class MainWindow : Window
{
    private readonly Dictionary<PageKey, Page> _pages = new();
    private PageKey _currentKey;
    private DispatcherTimer? _toastTimer;
    private bool _dragMoving;

    /// <summary>启动深链指定的初始页(未设置则首页),由 Loaded 统一导航,避免竞态覆盖。</summary>
    public PageKey? InitialPage { get; set; }
    public object? InitialParam { get; set; }

    public MainWindow()
    {
        InitializeComponent();
        Loaded += (s, e) =>
        {
            var v = System.Reflection.Assembly.GetEntryAssembly()?
                .GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>()?
                .InformationalVersion ?? "1.0.0";
            VersionText.Text = "v" + v.Split('+')[0];
            ApplyWindowChromePadding();
            if (InitialPage is PageKey key) Navigate(key, InitialParam);
            else Navigate(PageKey.Home);
        };
        StateChanged += (s, e) => ApplyWindowChromePadding();
        SourceInitialized += (s, e) => RestoreWindowBounds();
        Closing += (s, e) => SaveWindowBounds();
        PreviewKeyDown += MainWindow_PreviewKeyDown;
    }

    // ————— 窗口位置记忆 —————

    private void RestoreWindowBounds()
    {
        var s = SettingsService.Current;
        try
        {
            if (s.WindowW is >= 1120 && s.WindowH is >= 700 &&
                s.WindowX is double x && s.WindowY is double y)
            {
                // 仅当落点在可见虚拟屏幕内才恢复,避免恢复到已拔掉的显示器
                var wa = SystemParameters.WorkArea;
                bool onScreen = x < SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 100 &&
                                y < SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - 100 &&
                                x + 100 > SystemParameters.VirtualScreenLeft &&
                                y + 100 > SystemParameters.VirtualScreenTop;
                if (onScreen)
                {
                    WindowStartupLocation = WindowStartupLocation.Manual;
                    Left = x; Top = y;
                    Width = s.WindowW.Value; Height = s.WindowH.Value;
                }
            }
            if (s.WindowMaximized) WindowState = WindowState.Maximized;
        }
        catch { }
    }

    private void SaveWindowBounds()
    {
        try
        {
            var s = SettingsService.Current;
            if (WindowState == WindowState.Minimized) return;
            s.WindowMaximized = WindowState == WindowState.Maximized;
            if (!s.WindowMaximized)
            {
                s.WindowX = Left; s.WindowY = Top;
                s.WindowW = Width; s.WindowH = Height;
            }
            SettingsService.Save();
        }
        catch { }
    }

    // ————— 全局快捷键：Ctrl+1..8 切页 / Ctrl+K 命令面板 / F5 刷新 / Esc 停止扫描或清理 —————

    private void MainWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.K && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            Palette.Toggle();
            e.Handled = true;
        }
        else if (Palette.IsOpen && e.Key == Key.Escape)
        {
            Palette.Close();
            e.Handled = true;
        }
        else if (e.Key >= Key.D1 && e.Key <= Key.D8 &&
            (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            var key = (PageKey)(e.Key - Key.D1);
            if (Enum.IsDefined(key) && key != _currentKey)
            {
                Navigate(key);
                e.Handled = true;
            }
        }
        else if (e.Key == Key.F5)
        {
            if (_pages.TryGetValue(_currentKey, out var p) && p is IParamPage pp)
                pp.OnNavigated(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            if (_pages.TryGetValue(PageKey.SmartScan, out var p) && p is SmartScanPage sp)
                sp.TryCancelCurrent();
            if (_pages.TryGetValue(PageKey.CleanSpace, out var c) && c is CleanSpacePage cp)
                cp.TryCancelCurrent();
        }
    }

    // ————— 命令面板动作分发 —————

    private void Palette_ActionInvoked(Controls.CommandItem item)
    {
        if (item.Id.StartsWith("page.") && Enum.TryParse<PageKey>(item.Id["page.".Length..], out var key))
        {
            Navigate(key);
            return;
        }
        switch (item.Id)
        {
            case "act.quickscan":
                Navigate(PageKey.SmartScan, "start");
                break;
            case "act.analyze":
                Navigate(PageKey.SpaceAnalysis, "analyze");
                break;
            case "act.dupscan":
                Navigate(PageKey.Toolbox, "dup");
                break;
            case "act.emptyscan":
                Navigate(PageKey.Toolbox, "empty");
                break;
            case "act.quarantine":
                Navigate(PageKey.Toolbox, "quarantine");
                break;
            case "act.history":
                Navigate(PageKey.Toolbox, "history");
                break;
            case "act.dns":
                Services.QuickActions.FlushDns(this);
                break;
            case "act.theme":
                Services.QuickActions.ToggleTheme(this);
                break;
            case "act.update":
                Services.QuickActions.CheckUpdate(this);
                break;
        }
    }

    private void ApplyWindowChromePadding()
    {
        // 无边框窗口最大化时留出任务栏边缘
        Padding = WindowState == WindowState.Maximized ? new Thickness(7) : new Thickness(0);
        var g = FindName("glyph") as System.Windows.Shapes.Path;
        if (g != null)
            g.Data = (Geometry)FindResource(WindowState == WindowState.Maximized ? "RestoreGlyph" : "MaxGlyph");
    }

    // ————— 导航 —————

    public void Navigate(PageKey key, object? param = null)
    {
        if (!_pages.TryGetValue(key, out var page))
        {
            page = key switch
            {
                PageKey.Home => new HomePage(this),
                PageKey.SmartScan => new SmartScanPage(this),
                PageKey.CleanSpace => new CleanSpacePage(this),
                PageKey.SpaceAnalysis => new SpaceAnalysisPage(this),
                PageKey.Startup => new StartupPage(this),
                PageKey.Apps => new AppsPage(this),
                PageKey.Toolbox => new ToolboxPage(this),
                PageKey.Settings => new SettingsPage(this),
                _ => new HomePage(this),
            };
            _pages[key] = page;
        }
        PageHost.NavigationService.RemoveBackEntry();
        PageHost.Content = page;
        AnimatePageIn(page);
        // 先挂载内容再通知页面（保证 BringIntoView 等操作生效）
        if (page is IParamPage pp) pp.OnNavigated(param);
        // 深链：--param palette 打开命令面板（自动化验收用）
        if (param as string == "palette")
            Dispatcher.BeginInvoke(() => Palette.Open(), System.Windows.Threading.DispatcherPriority.Loaded);
        _currentKey = key;
        WindowTitleText.Text = key switch
        {
            PageKey.Home => Services.Loc.T("nav.home"),
            PageKey.SmartScan => Services.Loc.T("nav.scan"),
            PageKey.CleanSpace => Services.Loc.T("nav.clean"),
            PageKey.SpaceAnalysis => Services.Loc.T("nav.space"),
            PageKey.Startup => Services.Loc.T("nav.startup"),
            PageKey.Apps => Services.Loc.T("nav.apps"),
            PageKey.Toolbox => Services.Loc.T("nav.toolbox"),
            PageKey.Settings => Services.Loc.T("nav.settings"),
            _ => "",
        };
        SyncNavChecked(key);
    }

    /// <summary>页面入场过渡：淡入 + 上移。</summary>
    private static void AnimatePageIn(Page page)
    {
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var tt = new TranslateTransform(0, 16);
        page.RenderTransform = tt;
        page.BeginAnimation(OpacityProperty,
            new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200)) { EasingFunction = ease });
        tt.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(16, 0, TimeSpan.FromMilliseconds(260)) { EasingFunction = ease });
    }

    private void SyncNavChecked(PageKey key)
    {
        foreach (var rb in new[] { NavHome, NavScan, NavClean, NavSpace, NavStartup, NavApps, NavToolbox, NavSettings })
        {
            if ((string)rb.Tag == key.ToString())
            {
                rb.IsChecked = true;
            }
        }
    }

    private void Nav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && Enum.TryParse<PageKey>((string)rb.Tag, out var key) && key != _currentKey)
            Navigate(key);
    }

    // ————— Toast —————

    public void ShowToast(string message, ToastType type = ToastType.Success)
    {
        ToastText.Text = message;
        ToastIcon.Data = (Geometry)FindResource(
            type == ToastType.Success ? "CheckIcon" :
            type == ToastType.Warning ? "WarningIcon" : "InfoIcon");
        ToastIcon.Stroke = type switch
        {
            ToastType.Success => (Brush)FindResource("SuccessBrush"),
            ToastType.Warning => (Brush)FindResource("WarningBrush"),
            _ => (Brush)FindResource("PrimaryBrush"),
        };
        Toast.Visibility = Visibility.Visible;
        Toast.Opacity = 0;
        var slide = new TranslateTransform(0, -10);
        Toast.RenderTransform = slide;
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        Toast.BeginAnimation(OpacityProperty,
            new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200)) { EasingFunction = ease });
        slide.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(-10, 0, TimeSpan.FromMilliseconds(240)) { EasingFunction = ease });

        _toastTimer?.Stop();
        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3.6) };
        _toastTimer.Tick += (s, e) =>
        {
            _toastTimer!.Stop();
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300));
            fadeOut.Completed += (s2, e2) => Toast.Visibility = Visibility.Collapsed;
            Toast.BeginAnimation(OpacityProperty, fadeOut);
        };
        _toastTimer.Start();
    }

    // ————— 标题栏 —————

    private bool _downInDragArea;
    private Point _downPoint;

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximize();
            return;
        }
        _downInDragArea = true;
        _downPoint = e.GetPosition(this);
    }

    private void TitleBar_MouseUp(object sender, MouseButtonEventArgs e)
    {
        _downInDragArea = false;
        _dragMoving = false;
    }

    private void TitleBar_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_downInDragArea || e.LeftButton != MouseButtonState.Pressed) { _dragMoving = false; return; }
        // 离开阈值后开始拖动（避免吞掉按钮点击）
        var pos = e.GetPosition(this);
        if (!_dragMoving && Math.Abs(pos.X - _downPoint.X) + Math.Abs(pos.Y - _downPoint.Y) < 6) return;
        _dragMoving = true;
        if (WindowState == WindowState.Maximized)
        {
            // 从最大化拖出：恢复并跟随鼠标
            var pctX = pos.X / ActualWidth;
            ToggleMaximize();
            Left = pos.X - ActualWidth * pctX;
            Top = pos.Y - 26;
        }
        try { DragMove(); } catch { }
        _downInDragArea = false;
    }

    private void ToggleMaximize()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void BtnMin_Click(object sender, RoutedEventArgs e)
    {
        if (SettingsService.Current.MinimizeToTray)
            HideToTray();
        else
            WindowState = WindowState.Minimized;
    }

    public void HideToTray() => Hide();

    public void RestoreFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void BtnMax_Click(object sender, RoutedEventArgs e) => ToggleMaximize();

    private void BtnPaletteHint_Click(object sender, RoutedEventArgs e) => Palette.Open();

    private bool _allowClose;

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        var s = SettingsService.Current;
        if (s.CloseToTray)
        {
            HideToTray();
            ShowToast(Services.Loc.T("toast.traymin"), ToastType.Info);
            return;
        }
        if (!_allowClose)
        {
            var exit = Services.DialogService.Confirm(this, Services.Loc.T("dlg.exit.title"), Services.Loc.T("dlg.exit.msg"), Services.Loc.T("btn.exit"), Services.Loc.T("btn.cancel"));
            if (!exit) return;
            _allowClose = true;
        }
        Close();
    }

    internal void ForceClose()
    {
        _allowClose = true;
        Close();
    }
}

public enum ToastType { Success, Warning, Info }

public interface IParamPage
{
    void OnNavigated(object? param);
}
