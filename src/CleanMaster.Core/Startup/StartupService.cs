using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Win32;
using CleanMaster.Core.Rules;
using CleanMaster.Core.Store;

namespace CleanMaster.Core.Startup;

public enum StartupSource
{
    RegHkcuRun,
    RegHklmRun,
    StartupFolderUser,
    StartupFolderCommon,
    ScheduledTask,
}

public sealed class StartupItem
{
    public string Name { get; set; } = "";
    public string Publisher { get; set; } = "";
    public string Command { get; set; } = "";
    public string ExePath { get; set; } = "";
    public bool Enabled { get; set; } = true;
    /// <summary>高 / 中 / 低 / 未测量（基于应用类型的参考评估，非实测）</summary>
    public string Impact { get; set; } = "未测量";
    public string Suggestion { get; set; } = "保留";
    public bool IsSystemComponent { get; set; }
    public StartupSource Source { get; set; }
    public string SourceText => Source switch
    {
        StartupSource.RegHkcuRun => "注册表 (当前用户)",
        StartupSource.RegHklmRun => "注册表 (本机)",
        StartupSource.StartupFolderUser => "启动文件夹 (用户)",
        StartupSource.StartupFolderCommon => "启动文件夹 (公共)",
        _ => "计划任务",
    };
    public string RegistryKey { get; set; } = "";
    public string ValueName { get; set; } = "";
    public string FilePath { get; set; } = "";
    public string TaskPath { get; set; } = "";
    public bool CanToggle { get; set; } = true;
    public string Note { get; set; } = "";
}

/// <summary>
/// 启动项枚举与启停：注册表 Run / 启动文件夹 / 登录触发的计划任务。
/// 所有变更写入 startup_backup.json，可随时恢复原状态。
/// 启停采用与任务管理器一致的 StartupApproved 机制；HKLM 与计划任务需要提权。
/// </summary>
public sealed class StartupService
{
    private const string RunHkcu = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunHklm = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunHkcuOnce = @"Software\Microsoft\Windows\CurrentVersion\RunOnce";
    private const string ApprovedHkcu = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    private const string ApprovedHklm = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    private const string ApprovedFolderHkcu = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder";

    private static string BackupFile => Path.Combine(SettingsService.DataDir, "startup_backup.json");
    private static string FolderBackupDir => SettingsService.StartupBackupDir;

    public List<StartupItem> Enumerate()
    {
        var items = new List<StartupItem>();
        items.AddRange(ReadRunKey(Registry.CurrentUser, RunHkcu, StartupSource.RegHkcuRun, "HKCU"));
        items.AddRange(ReadRunKey(Registry.LocalMachine, RunHklm, StartupSource.RegHklmRun, "HKLM"));
        items.AddRange(ReadStartupFolder(GetUserStartupFolder(), StartupSource.StartupFolderUser));
        items.AddRange(ReadStartupFolder(GetCommonStartupFolder(), StartupSource.StartupFolderCommon));
        items.AddRange(ReadScheduledTasks());
        return items;
    }

    public static string GetUserStartupFolder() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            @"Microsoft\Windows\Start Menu\Programs\Startup");

    public static string GetCommonStartupFolder() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            @"Microsoft\Windows\Start Menu\Programs\Startup");

    private IEnumerable<StartupItem> ReadRunKey(RegistryKey root, string sub, StartupSource source, string hive)
    {
        var list = new List<StartupItem>();
        try
        {
            using var k = root.OpenSubKey(sub);
            if (k == null) return list;
            var approved = ReadApprovedStates(source == StartupSource.RegHkcuRun
                ? (RegistryKey)Registry.CurrentUser : Registry.LocalMachine,
                ApprovedHkcu);

            foreach (var name in k.GetValueNames())
            {
                var cmd = k.GetValue(name) as string;
                if (string.IsNullOrWhiteSpace(cmd)) continue;
                var exe = ExtractExePath(cmd);
                var item = new StartupItem
                {
                    Name = name,
                    Command = cmd,
                    ExePath = exe ?? "",
                    Source = source,
                    RegistryKey = hive == "HKCU" ? Registry.CurrentUser.Name + "\\" + sub : Registry.LocalMachine.Name + "\\" + sub,
                    ValueName = name,
                };
                item.Publisher = QueryPublisher(item.ExePath);
                item.IsSystemComponent = IsSystemExe(item.ExePath);
                item.CanToggle = !item.IsSystemComponent;
                if (item.IsSystemComponent)
                {
                    item.Suggestion = "系统组件";
                    item.Note = "Windows 核心组件，不建议禁用";
                }
                item.Enabled = approved.TryGetValue(name, out var on) ? on : true;
                ApplyHeuristics(item);
                list.Add(item);
            }
        }
        catch { }
        return list;
    }

    private IEnumerable<StartupItem> ReadStartupFolder(string dir, StartupSource source)
    {
        if (!Directory.Exists(dir)) yield break;
        var backup = LoadFolderBackup();
        foreach (var f in EnumerateStartupFiles(dir))
        {
            var name = Path.GetFileNameWithoutExtension(f);
            var item = new StartupItem
            {
                Name = name,
                Command = f,
                ExePath = ResolveShortcutTarget(f) ?? f,
                Source = source,
                FilePath = f,
            };
            item.Publisher = QueryPublisher(item.ExePath);
            // 移动到备份目录 = 已禁用
            var backedUp = backup.TryGetValue(FolderItemKey(item), out var bEntry);
            item.Enabled = !backedUp || !bEntry;
            ApplyHeuristics(item);
            yield return item;
        }
    }

    private static IEnumerable<string> EnumerateStartupFiles(string dir)
    {
        try { return Directory.GetFiles(dir).Where(f => f.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)); }
        catch { return Enumerable.Empty<string>(); }
    }

    private IEnumerable<StartupItem> ReadScheduledTasks()
    {
        var list = new List<StartupItem>();
        var tasksRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "Tasks");
        if (!Directory.Exists(tasksRoot)) return list;
        foreach (var file in SafeWalk(tasksRoot))
        {
            try
            {
                var rel = Path.GetRelativePath(tasksRoot, file);
                var top = rel.Split('\\')[0];
                if (top.Equals("Microsoft", StringComparison.OrdinalIgnoreCase)) continue; // 系统任务不展示
                XDocument doc;
                try { doc = XDocument.Load(file); } catch { continue; }
                bool hasLogon = doc.Descendants().Any(e =>
                    e.Name.LocalName is "LogonTrigger" or "BootTrigger" &&
                    (string?)e.Attribute("enabled") != "false");
                if (!hasLogon) continue;
                var settingsEnabled = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "Enabled")?.Value != "false";
                var command = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "Command")?.Value ?? "";
                var args = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "Arguments")?.Value ?? "";
                if (string.IsNullOrWhiteSpace(command)) continue;
                command = Environment.ExpandEnvironmentVariables(command);
                var exe = ExtractExePath(command + " " + args) ?? command;
                var item = new StartupItem
                {
                    Name = Path.GetFileNameWithoutExtension(file),
                    Command = command + " " + args,
                    ExePath = exe,
                    Source = StartupSource.ScheduledTask,
                    TaskPath = "\\" + rel,
                    Enabled = settingsEnabled,
                };
                item.Publisher = QueryPublisher(item.ExePath);
                item.IsSystemComponent = IsSystemExe(item.ExePath);
                item.CanToggle = true;
                if (item.IsSystemComponent)
                {
                    item.Suggestion = "系统组件";
                    item.CanToggle = false;
                }
                ApplyHeuristics(item);
                list.Add(item);
            }
            catch { }
        }
        return list;
    }

    private static IEnumerable<string> SafeWalk(string root)
    {
        var stack = new Stack<string>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var d = stack.Pop();
            string[] files = Array.Empty<string>(), dirs = Array.Empty<string>();
            try { files = Directory.GetFiles(d); dirs = Directory.GetDirectories(d); } catch { continue; }
            foreach (var f in files) yield return f;
            foreach (var sub in dirs) stack.Push(sub);
        }
    }

    // ————— 启停 —————

    /// <summary>切换启用/禁用。返回错误信息，null 表示成功。</summary>
    public async Task<string?> SetEnabledAsync(StartupItem item, bool enable)
    {
        string? err = item.Source switch
        {
            StartupSource.RegHkcuRun => ToggleRegRun(item, enable),
            StartupSource.RegHklmRun => await ToggleHklmRunAsync(item, enable),
            StartupSource.StartupFolderUser or StartupSource.StartupFolderCommon => ToggleFolderItem(item, enable),
            StartupSource.ScheduledTask => await ToggleTaskAsync(item, enable),
            _ => "不支持的类型",
        };
        if (err == null)
        {
            item.Enabled = enable;
            SaveBackupState(item, enable);
        }
        return err;
    }

    private static string? ToggleRegRun(StartupItem item, bool enable)
    {
        try
        {
            using var approved = Registry.CurrentUser.CreateSubKey(ApprovedHkcu, writable: true);
            approved.SetValue(item.ValueName, MakeApprovedValue(enable), RegistryValueKind.Binary);
            return null;
        }
        catch (Exception ex) { return ex.Message; }
    }

    private static async Task<string?> ToggleHklmRunAsync(StartupItem item, bool enable)
    {
        try
        {
            using var approved = Registry.LocalMachine.OpenSubKey(ApprovedHklm, writable: true);
            if (approved != null)
            {
                approved.SetValue(item.ValueName, MakeApprovedValue(enable), RegistryValueKind.Binary);
                return null;
            }
        }
        catch (UnauthorizedAccessException) { }
        var ok = await ElevatedService.RunTaskAsync("toggle-hklm-startup",
            JsonSerializer.Serialize(new Dictionary<string, string>
            {
                ["sub"] = ApprovedHklm,
                ["value"] = item.ValueName,
                ["disable"] = enable ? "0" : "1",
            }));
        return ok ? null : "需要管理员权限（在 UAC 弹窗中允许后重试）";
    }

    private string? ToggleFolderItem(StartupItem item, bool enable)
    {
        try
        {
            Directory.CreateDirectory(FolderBackupDir);
            var key = FolderItemKey(item);
            if (enable)
            {
                var backupPath = Path.Combine(FolderBackupDir, Path.GetFileName(item.FilePath));
                if (File.Exists(backupPath))
                {
                    File.Move(backupPath, item.FilePath, overwrite: false);
                }
                else if (!File.Exists(item.FilePath))
                {
                    return "原启动项文件已丢失";
                }
                // 移回后同时清掉 StartupFolder 的禁用标记（若存在）
                try
                {
                    using var k = Registry.CurrentUser.OpenSubKey(ApprovedFolderHkcu, writable: true);
                    k?.DeleteValue(Path.GetFileName(item.FilePath), false);
                }
                catch { }
            }
            else
            {
                if (File.Exists(item.FilePath))
                {
                    File.Move(item.FilePath, Path.Combine(FolderBackupDir, Path.GetFileName(item.FilePath)), overwrite: true);
                }
                else
                {
                    return "启动项文件不存在";
                }
            }
            return null;
        }
        catch (Exception ex) { return ex.Message; }
    }

    private static async Task<string?> ToggleTaskAsync(StartupItem item, bool enable)
    {
        var psi = new ProcessStartInfo("schtasks.exe",
            $"/Change /TN \"{item.TaskPath}\" /{(enable ? "ENABLE" : "DISABLE")}")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
        };
        try
        {
            using var p = Process.Start(psi)!;
            p.WaitForExit(20000);
            if (p.ExitCode == 0) return null;
        }
        catch { }
        var ok = await ElevatedService.RunTaskAsync("schtasks-toggle",
            JsonSerializer.Serialize(new Dictionary<string, string>
            {
                ["task"] = item.TaskPath,
                ["disable"] = enable ? "0" : "1",
            }));
        return ok ? null : "修改计划任务失败，需要管理员权限";
    }

    private static byte[] MakeApprovedValue(bool enabled)
    {
        // 与任务管理器一致的格式：首字节 2=启用 3=禁用，其余为 FILETIME
        var data = new byte[12];
        data[0] = enabled ? (byte)2 : (byte)3;
        return data;
    }

    private static Dictionary<string, bool> ReadApprovedStates(RegistryKey root, string sub)
    {
        var dict = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var k = root.OpenSubKey(sub);
            if (k == null) return dict;
            foreach (var name in k.GetValueNames())
            {
                if (k.GetValue(name) is byte[] b && b.Length > 0)
                    dict[name] = b[0] == 2;
            }
        }
        catch { }
        return dict;
    }

    // ————— 备份与恢复 —————

    private Dictionary<string, bool> LoadFolderBackup()
    {
        try
        {
            if (File.Exists(BackupFile))
                return JsonSerializer.Deserialize<Dictionary<string, bool>>(File.ReadAllText(BackupFile)) ?? new();
        }
        catch { }
        return new();
    }

    private void SaveBackupState(StartupItem item, bool nowEnabled)
    {
        try
        {
            var dict = LoadFolderBackup();
            dict[FolderItemKey(item)] = nowEnabled;
            Directory.CreateDirectory(SettingsService.DataDir);
            File.WriteAllText(BackupFile, JsonSerializer.Serialize(dict,
                new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    private static string FolderItemKey(StartupItem item) =>
        item.Source + "|" + (item.Source is StartupSource.StartupFolderUser or StartupSource.StartupFolderCommon
            ? item.FilePath : item.RegistryKey + "\\" + item.ValueName + item.TaskPath);

    // ————— 工具 —————

    private static string? ExtractExePath(string command)
    {
        var cmd = command.Trim();
        if (cmd.StartsWith('"'))
        {
            var end = cmd.IndexOf('"', 1);
            if (end > 0) return cmd[1..end];
        }
        var space = cmd.IndexOf(' ');
        var first = space > 0 ? cmd[..space] : cmd;
        if (first.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(first)) return first;
        if (File.Exists(cmd)) return cmd;
        return first;
    }

    private static string QueryPublisher(string exe)
    {
        try
        {
            if (!string.IsNullOrEmpty(exe) && File.Exists(exe))
            {
                var vi = FileVersionInfo.GetVersionInfo(exe);
                return vi.CompanyName ?? "";
            }
        }
        catch { }
        return "";
    }

    private static bool IsSystemExe(string exe)
    {
        if (string.IsNullOrEmpty(exe)) return false;
        try
        {
            var full = Path.GetFullPath(exe);
            var windir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            return full.StartsWith(windir + "\\", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    private static string? ResolveShortcutTarget(string lnkPath)
    {
        // 无 COM 依赖的轻量解析：读取 .lnk 中的相对路径字段。
        // 失败时返回 null，仅影响发布者识别。
        try
        {
            var bytes = File.ReadAllBytes(lnkPath);
            // 简化处理：直接显示 .lnk 本身
            return null;
        }
        catch { return null; }
    }

    private static void ApplyHeuristics(StartupItem item)
    {
        var exeName = Path.GetFileName(item.ExePath).ToLowerInvariant();
        var cmd = item.Command.ToLowerInvariant();

        if (item.IsSystemComponent)
        {
            item.Impact = "低";
            return;
        }

        string[] highList =
        {
            "onedrive", "spotify", "steam", "epicgameslauncher", "battle.net", "wechat", "weixin", "qq",
            "discord", "teams", "slack", "zoom", "baidunetdisk", "thunder", "xunlei", "cloudmusic",
            "kugou", "kuwo", "qqmusic", "qqlive", "douyu", "douyin", "tiktok",
        };
        string[] mediumList =
        {
            "update", "updater", "upgrade", "agent", "assistant", "helper", "daemon", "sync",
            "adobe", "acrotray", "java", "jusched", "logitech", "razer", "nvidia",
        };

        if (highList.Any(k => exeName.Contains(k) || cmd.Contains(k)))
            item.Impact = "高";
        else if (mediumList.Any(k => exeName.Contains(k)))
            item.Impact = "中";
        else
            item.Impact = "未测量";

        item.Suggestion = item.Impact switch
        {
            "高" => "可考虑关闭",
            "中" => "可考虑关闭",
            _ => "保留",
        };
    }
}
