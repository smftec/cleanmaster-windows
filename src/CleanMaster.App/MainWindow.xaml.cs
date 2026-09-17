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

    public MainWindow()
    {
        InitializeComponent();
        Loaded += (s, e) =>
        {
            ApplyWindowChromePadding();
            Navigate(PageKey.Home);
        };
        StateChanged += (s, e) => ApplyWindowChromePadding();
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
        // 先挂载内容再通知页面（保证 BringIntoView 等操作生效）
        if (page is IParamPage pp) pp.OnNavigated(param);
        _currentKey = key;
        WindowTitleText.Text = key switch
        {
            PageKey.Home => "首页",
            PageKey.SmartScan => "智能扫描",
            PageKey.CleanSpace => "清理空间",
            PageKey.SpaceAnalysis => "空间分析",
            PageKey.Startup => "启动项",
            PageKey.Apps => "应用管理",
            PageKey.Toolbox => "工具箱",
            PageKey.Settings => "设置",
            _ => "",
        };
        SyncNavChecked(key);
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
        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180));
        Toast.BeginAnimation(OpacityProperty, fadeIn);

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

    private bool _allowClose;

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        var s = SettingsService.Current;
        if (s.CloseToTray)
        {
            HideToTray();
            ShowToast("已最小化到托盘，右键托盘图标可退出", ToastType.Info);
            return;
        }
        if (!_allowClose)
        {
            var exit = Services.DialogService.Confirm(this, "退出清理优化大师？", "退出后将不再提供自动清理与磁盘空间监控。", "退出", "取消");
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
