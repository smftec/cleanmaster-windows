using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using CleanMaster.Core.Models;

namespace CleanMaster.Core.Store;

public sealed class AppSettings
{
    // —— 通用 ——
    public bool LaunchAtStartup { get; set; }
    public bool MinimizeToTray { get; set; } = true;
    public bool CloseToTray { get; set; } = true;
    /// <summary>light / dark / system</summary>
    public string Theme { get; set; } = "light";

    // —— 清理 ——
    public bool QuarantineEnabled { get; set; } = true;
    public int QuarantineDays { get; set; } = 30;
    public bool AutoCleanEnabled { get; set; }
    /// <summary>daily / weekly / monthly</summary>
    public string AutoCleanFrequency { get; set; } = "weekly";
    public bool AutoCleanIncludeBrowser { get; set; } = true;
    public int SkipRecentMinutes { get; set; } = 10;

    // —— 通知 ——
    public bool NotifyDiskLow { get; set; } = true;
    public bool NotifyAutoCleanResult { get; set; } = true;
    public bool NotifyWeeklyReport { get; set; }
    public bool NotifyProductNews { get; set; }

    // —— 高级 ——
    public int ScanThreads { get; set; } = Math.Max(2, Environment.ProcessorCount / 2);
    public bool ScanHiddenFiles { get; set; } = true;
    public bool FollowJunctions { get; set; }
    public bool ShowSystemFiles { get; set; }
    public string LogLevel { get; set; } = "info";

    // —— 隐私 ——
    public bool AnonymousStats { get; set; }
    public bool CrashReports { get; set; } = true;

    // —— 路径 ——
    public List<string> ExcludedPaths { get; set; } = new();
    public List<string> Whitelist { get; set; } = new();
    public List<string> DuplicateScanDirs { get; set; } = new();

    [JsonIgnore]
    public static AppSettings Default { get; } = new();
}

public sealed class SettingsService
{
    private static readonly object Lock = new();
    private static AppSettings? _current;

    public static string DataDir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CleanMaster");

    public static string QuarantineDir => Path.Combine(DataDir, "Quarantine");
    public static string StartupBackupDir => Path.Combine(DataDir, "StartupBackup");

    private static string SettingsFile => Path.Combine(DataDir, "settings.json");

    private static readonly JsonSerializerOptions JsonOpt = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static AppSettings Current
    {
        get
        {
            if (_current == null) Load();
            return _current!;
        }
    }

    public static void Load()
    {
        lock (Lock)
        {
            try
            {
                if (File.Exists(SettingsFile))
                    _current = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsFile)) ?? new AppSettings();
                else
                    _current = new AppSettings();
            }
            catch
            {
                _current = new AppSettings();
            }
            Directory.CreateDirectory(DataDir);
        }
    }

    public static void Save()
    {
        lock (Lock)
        {
            try
            {
                Directory.CreateDirectory(DataDir);
                File.WriteAllText(SettingsFile, JsonSerializer.Serialize(_current ?? new AppSettings(), JsonOpt));
            }
            catch { }
        }
    }
}

public sealed class HistoryService
{
    private static readonly object Lock = new();
    private static string File => Path.Combine(SettingsService.DataDir, "history.json");

    private static readonly JsonSerializerOptions JsonOpt = new()
    {
        WriteIndented = false,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static void Add(string type, string title, string detail = "", long bytes = 0)
    {
        var list = LoadAll();
        list.Insert(0, new ActivityRecord { Type = type, Title = title, Detail = detail, Bytes = bytes });
        if (list.Count > 500) list.RemoveRange(500, list.Count - 500);
        lock (Lock)
        {
            try { System.IO.File.WriteAllText(File, JsonSerializer.Serialize(list, JsonOpt)); } catch { }
        }
    }

    public static List<ActivityRecord> LoadAll()
    {
        lock (Lock)
        {
            try
            {
                if (System.IO.File.Exists(File))
                    return JsonSerializer.Deserialize<List<ActivityRecord>>(System.IO.File.ReadAllText(File)) ?? new();
            }
            catch { }
            return new();
        }
    }

    public static void Clear()
    {
        lock (Lock)
        {
            try { if (System.IO.File.Exists(File)) System.IO.File.Delete(File); } catch { }
        }
    }
}
