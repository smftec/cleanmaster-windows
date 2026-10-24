using CleanMaster.Core.Apps;
using CleanMaster.Core.Startup;

namespace CleanMaster.App.Services;

/// <summary>
/// 注册表枚举结果缓存(10 分钟 TTL):应用清单与启动项枚举较重,
/// 首页/应用页/启动项页共用,避免每次导航重复扫描注册表。
/// </summary>
public static class EnumerationCache
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);
    private static readonly object Lock = new();

    private static List<InstalledApp>? _apps;
    private static DateTime _appsAt;

    private static List<StartupItem>? _startup;
    private static DateTime _startupAt;

    public static List<InstalledApp> GetApps()
    {
        lock (Lock)
        {
            if (_apps == null || DateTime.Now - _appsAt > Ttl)
            {
                _apps = new AppInventoryService().EnumerateWin32();
                _appsAt = DateTime.Now;
            }
            return _apps;
        }
    }

    public static void InvalidateApps()
    {
        lock (Lock) { _apps = null; }
    }

    public static List<StartupItem> GetStartup()
    {
        lock (Lock)
        {
            if (_startup == null || DateTime.Now - _startupAt > Ttl)
            {
                _startup = new StartupService().Enumerate();
                _startupAt = DateTime.Now;
            }
            return _startup;
        }
    }

    public static void InvalidateStartup()
    {
        lock (Lock) { _startup = null; }
    }
}
