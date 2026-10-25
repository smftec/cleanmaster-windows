using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using CleanMaster.Core.Store;

namespace CleanMaster.App.Services;

/// <summary>主题管理：浅色 / 深色 / 跟随系统。</summary>
public static class ThemeManager
{
    public static void ApplyFromSettings()
    {
        Apply(SettingsService.Current.Theme);
    }

    public static void Apply(string theme)
    {
        var dict = new ResourceDictionary();
        if (theme == "dark")
            dict.Source = new Uri("Themes/Dark.xaml", UriKind.Relative);
        else if (theme == "system")
        {
            bool dark = IsSystemDark();
            dict.Source = new Uri(dark ? "Themes/Dark.xaml" : "Themes/Light.xaml", UriKind.Relative);
        }
        else
            dict.Source = new Uri("Themes/Light.xaml", UriKind.Relative);

        var merged = Application.Current.Resources.MergedDictionaries;
        // 替换第一个（颜色字典），Controls.xaml 保持不变
        if (merged.Count > 0)
            merged[0] = dict;
        else
            merged.Insert(0, dict);
    }

    public static bool IsSystemDark()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int v && v == 0;
        }
        catch { return false; }
    }
}

/// <summary>开机自启（HKCU Run，无需管理员）。</summary>
public static class StartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "CleanMaster";

    public static void Sync(bool enable)
    {
        try
        {
            using var k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
            if (enable)
            {
                var exe = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exe))
                    k.SetValue(ValueName, $"\"{exe}\"");
            }
            else
            {
                k.DeleteValue(ValueName, false);
            }
        }
        catch { }
    }
}

/// <summary>上次扫描摘要缓存（供首页展示）。</summary>
public sealed class LastScanSummary
{
    public DateTime Time { get; set; } = DateTime.Now;
    public long TotalBytes { get; set; }
    public int HighImpactStartup { get; set; }
    public List<RuleSummary> Rules { get; set; } = new();

    public sealed class RuleSummary
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public int Group { get; set; }
        public int Risk { get; set; }
        public long Size { get; set; }
        public int Count { get; set; }
    }
}

public static class ScanCache
{
    private static string File => Path.Combine(SettingsService.DataDir, "lastscan.json");
    private static readonly JsonSerializerOptions JsonOpt = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static LastScanSummary? Load()
    {
        try
        {
            if (System.IO.File.Exists(File))
                return JsonSerializer.Deserialize<LastScanSummary>(System.IO.File.ReadAllText(File), JsonOpt);
        }
        catch { }
        return null;
    }

    public static void Save(LastScanSummary s)
    {
        try
        {
            Directory.CreateDirectory(SettingsService.DataDir);
            System.IO.File.WriteAllText(File, JsonSerializer.Serialize(s, JsonOpt));
        }
        catch { }
    }
}

/// <summary>上次磁盘分析摘要缓存。</summary>
public sealed class DiskSummary
{
    public string Drive { get; set; } = "C:\\";
    public long TotalBytes { get; set; }
    public long UsedBytes { get; set; }
    public long FreeBytes { get; set; }
    public DateTime Time { get; set; }
    public List<CatItem> Categories { get; set; } = new();

    public sealed class CatItem
    {
        public string Name { get; set; } = "";
        public long Bytes { get; set; }
        public double Percent { get; set; }
        public string Hex { get; set; } = "#94A3B8";
    }
}

public static class DiskCache
{
    private static string File(string drive) =>
        Path.Combine(SettingsService.DataDir, $"disk_{Sanitize(drive)}.json");

    private static string Sanitize(string d) =>
        new(d.Where(char.IsLetterOrDigit).ToArray());

    private static readonly JsonSerializerOptions JsonOpt = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static DiskSummary? Load(string drive)
    {
        try
        {
            if (System.IO.File.Exists(File(drive)))
                return JsonSerializer.Deserialize<DiskSummary>(System.IO.File.ReadAllText(File(drive)), JsonOpt);
        }
        catch { }
        return null;
    }

    public static void Save(DiskSummary s)
    {
        try
        {
            Directory.CreateDirectory(SettingsService.DataDir);
            System.IO.File.WriteAllText(File(s.Drive), JsonSerializer.Serialize(s, JsonOpt));
        }
        catch { }
    }
}

/// <summary>自动清理调度：应用运行期间按设置周期在后台执行安全清理。</summary>
public sealed class AutoCleanScheduler
{
    public static AutoCleanScheduler Instance { get; } = new();
    private System.Windows.Threading.DispatcherTimer? _timer;
    private Window? _owner;
    private static string LastRunFile => Path.Combine(SettingsService.DataDir, "lastautoclean.json");

    public event Action<long, int>? AutoCleanCompleted; // (释放字节, 项目数)

    public void Initialize(Window owner)
    {
        _owner = owner;
        _timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMinutes(20) };
        _timer.Tick += async (s, e) =>
        {
            await CheckAndRunAsync();
            CheckDiskLow();
        };
        _timer.Start();
        _ = CheckAndRunAsync();
    }

    // ————— 磁盘空间不足提醒（PRD §22，每天最多一次） —————

    private DateTime _lastDiskNotify;

    private static string SystemDriveRoot()
    {
        try { return Path.GetTempPath().Substring(0, 3); }
        catch { return "C:\\"; }
    }

    /// <summary>系统盘剩余不足 10% 时提醒;每天最多一次,避免打扰。</summary>
    private void CheckDiskLow()
    {
        var s = SettingsService.Current;
        if (!s.NotifyDiskLow) return;
        if (DateTime.Now - _lastDiskNotify < TimeSpan.FromDays(1)) return;
        try
        {
            var drive = DriveInfo.GetDrives().FirstOrDefault(d =>
                d.IsReady && d.DriveType == DriveType.Fixed &&
                d.Name.Equals(SystemDriveRoot(), StringComparison.OrdinalIgnoreCase));
            if (drive == null || drive.TotalSize == 0) return;
            var freePct = drive.AvailableFreeSpace * 100.0 / drive.TotalSize;
            if (freePct >= 10) return;
            _lastDiskNotify = DateTime.Now;
            var freeText = Core.Utils.SizeText.OfBytes(drive.AvailableFreeSpace, 0);
            _owner?.Dispatcher.BeginInvoke(() =>
                (_owner as MainWindow)?.ShowToast(string.Format(Loc.T("notify.disklow"), freeText), ToastType.Warning));
            HistoryService.Add("Notify", Loc.T("notify.disklow.log"), freeText, 0);
        }
        catch { }
    }

    public static DateTime? LoadLastRun()
    {
        try
        {
            if (System.IO.File.Exists(LastRunFile))
                return JsonSerializer.Deserialize<DateTime>(System.IO.File.ReadAllText(LastRunFile));
        }
        catch { }
        return null;
    }

    private static void SaveLastRun()
    {
        try
        {
            Directory.CreateDirectory(SettingsService.DataDir);
            System.IO.File.WriteAllText(LastRunFile, JsonSerializer.Serialize(DateTime.Now));
        }
        catch { }
    }

    public bool IsDue()
    {
        var s = SettingsService.Current;
        if (!s.AutoCleanEnabled) return false;
        var last = LoadLastRun();
        if (last == null) return true;
        var span = s.AutoCleanFrequency switch
        {
            "daily" => TimeSpan.FromDays(1),
            "monthly" => TimeSpan.FromDays(30),
            _ => TimeSpan.FromDays(7),
        };
        return DateTime.Now - last >= span;
    }

    public async Task CheckAndRunAsync()
    {
        if (!IsDue()) return;
        var (bytes, count) = await RunOnceAsync();
        SaveLastRun();
        if (bytes > 0)
        {
            AutoCleanCompleted?.Invoke(bytes, count);
        }
    }

    /// <summary>执行一次静默安全清理（仅 L0/L1 默认规则，不含隐私与回收站）。</summary>
    public async Task<(long bytes, int count)> RunOnceAsync()
    {
        var scanner = new Core.Rules.ScannerService();
        var ctx = scanner.BuildContext(CancellationToken.None);
        var rules = scanner.Rules.Where(r =>
            r.Group is Core.Models.RuleGroup.SystemJunk or Core.Models.RuleGroup.BrowserCache or Core.Models.RuleGroup.AppCache
            && r.DefaultSelected).ToList();
        var results = await scanner.ScanAsync(rules, ctx, null);
        var svc = new Core.Rules.CleanService();
        var requests = results
            .Zip(rules, (res, rule) => (res, rule))
            .Where(t => t.res.Error == null && t.res.Items.Count > 0)
            .Select(t => new Core.Rules.CleanRequestRule
            {
                Rule = t.rule,
                Result = t.res,
                Selected = t.res.Items.ToList(),
            })
            .ToList();
        if (requests.Count == 0) return (0, 0);
        var summary = await svc.CleanAsync(requests, CancellationToken.None, null);
        HistoryService.Add("AutoClean",
            string.Format(Loc.T("history.autoclean.title"), Core.Utils.SizeText.OfBytes(summary.FreedBytes)),
            string.Format(Loc.T("history.autoclean.detail"), summary.SuccessCount, summary.SkipCount),
            summary.FreedBytes);
        return (summary.FreedBytes, summary.SuccessCount + summary.QuarantineCount);
    }
}
