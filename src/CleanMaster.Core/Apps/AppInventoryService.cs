using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;
using CleanMaster.Core.Rules;
using CleanMaster.Core.Store;
using CleanMaster.Core.Utils;

namespace CleanMaster.Core.Apps;

public sealed class InstalledApp
{
    public string DisplayName { get; set; } = "";
    public string DisplayVersion { get; set; } = "";
    public string Publisher { get; set; } = "";
    public string InstallLocation { get; set; } = "";
    public string UninstallString { get; set; } = "";
    public string QuietUninstallString { get; set; } = "";
    public long EstimatedSize { get; set; }
    public DateTime InstallDate { get; set; }
    public bool IsStoreApp { get; set; }
    public string StorePackageFullName { get; set; } = "";
    /// <summary>用于图标提取的 exe 路径。</summary>
    public string IconPath { get; set; } = "";
    public string RegistryKey { get; set; } = "";
    public bool IsSystemComponent { get; set; }

    public string SizeDisplay => EstimatedSize > 0 ? SizeText.OfBytes(EstimatedSize) : "未知";
}

/// <summary>应用卸载后可能残留的文件目录 / 注册表键。</summary>
public sealed class LeftoverItem
{
    public string Path { get; set; } = "";
    public long Size { get; set; }
    public bool IsRegistry { get; set; }
    public bool Selected { get; set; } = true;
    public string Description { get; set; } = "";
}

/// <summary>
/// 已安装应用清单：注册表卸载表（含 WOW6432Node）+ Microsoft Store 应用（PowerShell 按需枚举）。
/// </summary>
public sealed class AppInventoryService
{
    private List<InstalledApp>? _storeCache;
    private DateTime _storeCacheTime = DateTime.MinValue;

    private static readonly string[] UninstallKeys =
    [
        @"Software\Microsoft\Windows\CurrentVersion\Uninstall",
        @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
    ];

    public List<InstalledApp> EnumerateWin32()
    {
        var apps = new Dictionary<string, InstalledApp>(StringComparer.OrdinalIgnoreCase);
        void ReadRoot(RegistryKey root, string sub)
        {
            try
            {
                using var k = root.OpenSubKey(sub);
                if (k == null) return;
                foreach (var keyName in k.GetSubKeyNames())
                {
                    try
                    {
                        using var app = k.OpenSubKey(keyName);
                        if (app == null) continue;
                        var name = app.GetValue("DisplayName") as string;
                        if (string.IsNullOrWhiteSpace(name)) continue;
                        if ((app.GetValue("SystemComponent") as int?) == 1) continue;
                        var releaseType = app.GetValue("ReleaseType") as string;
                        if (releaseType is "Hotfix" or "Security Update" or "Update Rollup") continue;
                        if (name.StartsWith("KB", StringComparison.OrdinalIgnoreCase) && releaseType != null) continue;

                        var fullKey = root.Name + "\\" + sub + "\\" + keyName;
                        if (apps.ContainsKey(fullKey)) continue;

                        var sizeKb = app.GetValue("EstimatedSize") as int? ?? 0;
                        var item = new InstalledApp
                        {
                            DisplayName = name,
                            DisplayVersion = app.GetValue("DisplayVersion") as string ?? "",
                            Publisher = app.GetValue("Publisher") as string ?? "",
                            InstallLocation = app.GetValue("InstallLocation") as string ?? "",
                            UninstallString = app.GetValue("UninstallString") as string ?? "",
                            QuietUninstallString = app.GetValue("QuietUninstallString") as string ?? "",
                            EstimatedSize = sizeKb * 1024L,
                            RegistryKey = fullKey,
                        };
                        item.IsSystemComponent = (app.GetValue("WindowsInstaller") as int?) == 1 &&
                                                 string.Equals(item.Publisher, "Microsoft Corporation", StringComparison.OrdinalIgnoreCase) &&
                                                 name.Contains("Microsoft Visual C++");
                        var dateStr = app.GetValue("InstallDate") as string;
                        item.InstallDate = ParseInstallDate(dateStr);
                        var iconRaw = app.GetValue("DisplayIcon") as string;
                        item.IconPath = ExtractIconPath(iconRaw) ?? ExtractIconPath(item.UninstallString) ?? "";
                        apps[fullKey] = item;
                    }
                    catch { }
                }
            }
            catch { }
        }

        ReadRoot(Registry.CurrentUser, UninstallKeys[0]);
        ReadRoot(Registry.LocalMachine, UninstallKeys[0]);
        ReadRoot(Registry.LocalMachine, UninstallKeys[1]);
        return apps.Values.OrderBy(a => a.DisplayName).ToList();
    }

    public List<InstalledApp> EnumerateStoreApps()
    {
        if (_storeCache != null && (DateTime.Now - _storeCacheTime).TotalMinutes < 30) return _storeCache;
        try
        {
            var script = "Get-AppxPackage | Where-Object { -not $_.IsFramework -and $_.SignatureKind -ne 'System' } | Select-Object Name,PackageFullName,Version,Publisher | ConvertTo-Json -Compress";
            var psi = new ProcessStartInfo("powershell.exe", "-NoProfile -NonInteractive -Command " + script)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using var p = Process.Start(psi)!;
            var json = p.StandardOutput.ReadToEnd();
            p.WaitForExit(30000);
            if (p.ExitCode != 0) return _storeCache ?? new();
            json = json.Trim();
            var items = new List<InstalledApp>();
            if (json.StartsWith('['))
            {
                var list = JsonSerializer.Deserialize<List<StorePkg>>(json);
                foreach (var i in list ?? new()) items.Add(ToApp(i));
            }
            else if (json.StartsWith('{'))
            {
                var i = JsonSerializer.Deserialize<StorePkg>(json);
                if (i != null) items.Add(ToApp(i));
            }
            _storeCache = items;
            _storeCacheTime = DateTime.Now;
            return items;
        }
        catch
        {
            return _storeCache ?? new();
        }
    }

    private sealed class StorePkg
    {
        public string Name { get; set; } = "";
        public string PackageFullName { get; set; } = "";
        public string Version { get; set; } = "";
        public string Publisher { get; set; } = "";
    }

    private static InstalledApp ToApp(StorePkg p) => new()
    {
        DisplayName = p.Name.StartsWith("Microsoft.") ? p.Name[(p.Name.LastIndexOf('.') + 1)..] : p.Name,
        DisplayVersion = p.Version,
        Publisher = p.Publisher,
        IsStoreApp = true,
        StorePackageFullName = p.PackageFullName,
        IconPath = "",
    };

    /// <summary>启动卸载流程。返回 null 表示已启动；否则错误说明。</summary>
    public string? LaunchUninstall(InstalledApp app)
    {
        try
        {
            if (app.IsStoreApp)
            {
                var psi = new ProcessStartInfo("powershell.exe",
                    $"-NoProfile -NonInteractive -Command \"Remove-AppxPackage -Package '{app.StorePackageFullName}'\"")
                {
                    CreateNoWindow = false,
                    UseShellExecute = true,
                };
                Process.Start(psi);
                return null;
            }
            if (string.IsNullOrWhiteSpace(app.UninstallString)) return "该应用未提供卸载程序";
            // 用 cmd /c 保留原样参数；MSI 字符串也兼容
            Process.Start(new ProcessStartInfo("cmd.exe", "/c " + app.UninstallString)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
            });
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    public bool IsStillInstalled(InstalledApp app)
    {
        try
        {
            if (app.IsStoreApp)
                return EnumerateStoreApps().Any(a => a.StorePackageFullName == app.StorePackageFullName);
            var parts = SplitRegistryPath(app.RegistryKey);
            if (parts == null) return true;
            using var root = parts.Value.root == RegistryHive.LocalMachine
                ? RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                : RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
            using var k = root.OpenSubKey(parts.Value.sub);
            return k?.GetSubKeyNames().Contains(parts.Value.leaf, StringComparer.OrdinalIgnoreCase) == true;
        }
        catch { return true; }
    }

    private static (RegistryHive root, string sub, string leaf)? SplitRegistryPath(string full)
    {
        if (string.IsNullOrEmpty(full)) return null;
        RegistryHive hive;
        string rest;
        if (full.StartsWith("HKEY_LOCAL_MACHINE\\")) { hive = RegistryHive.LocalMachine; rest = full["HKEY_LOCAL_MACHINE\\".Length..]; }
        else if (full.StartsWith("HKEY_CURRENT_USER\\")) { hive = RegistryHive.CurrentUser; rest = full["HKEY_CURRENT_USER\\".Length..]; }
        else return null;
        var leafStart = rest.LastIndexOf('\\');
        if (leafStart < 0) return null;
        return (hive, rest[..leafStart], rest[(leafStart + 1)..]);
    }

    // ————— 卸载残留 —————

    /// <summary>基于应用名/安装目录的确定性残留检测（不做模糊全局搜索）。</summary>
    public List<LeftoverItem> FindLeftovers(InstalledApp app)
    {
        var result = new List<LeftoverItem>();
        var tokens = NameTokens(app);
        if (tokens.Count == 0) return result;

        string[][] dirRootsPerToken;
        var userLocal = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var userRoaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

        foreach (var token in tokens)
        {
            var candidates = new[]
            {
                Path.Combine(userLocal, token),
                Path.Combine(userRoaming, token),
                Path.Combine(programData, token),
                Path.Combine(pf, token),
                Path.Combine(pf86, token),
            };
            foreach (var c in candidates)
            {
                try
                {
                    if (!Directory.Exists(c)) continue;
                    // InstallLocation 本身只有在注册表卸载条目已消失时才算残留（调用方负责时机）
                    result.Add(new LeftoverItem
                    {
                        Path = c,
                        Size = FileUtil.DirSize(c),
                        Description = "数据目录",
                        Selected = true,
                    });
                }
                catch { }
            }
            // HKCU\Software\<Token>
            try
            {
                var regPath = Path.Combine("Software", token);
                using var k = Registry.CurrentUser.OpenSubKey(regPath);
                if (k != null)
                {
                    result.Add(new LeftoverItem
                    {
                        Path = "reg://HKCU\\" + regPath,
                        IsRegistry = true,
                        Description = "设置注册表键",
                        Selected = true,
                    });
                }
            }
            catch { }
        }

        // InstallLocation 残留
        try
        {
            if (!string.IsNullOrWhiteSpace(app.InstallLocation) && Directory.Exists(app.InstallLocation) &&
                SafetyCheck.InstallLocationRemovable(app.InstallLocation))
            {
                result.Add(new LeftoverItem
                {
                    Path = app.InstallLocation,
                    Size = FileUtil.DirSize(app.InstallLocation),
                    Description = "安装目录",
                    Selected = true,
                });
            }
        }
        catch { }

        return result.DistinctBy(r => r.Path.TrimEnd('\\')).Where(r => r.Size > 0 || r.IsRegistry).ToList();
    }

    private static List<string> NameTokens(InstalledApp app)
    {
        var tokens = new List<string>();
        void Add(string? t)
        {
            if (string.IsNullOrWhiteSpace(t)) return;
            t = t.Trim();
            if (t.Length is < 2 or > 60) return;
            if (tokens.Contains(t, StringComparer.OrdinalIgnoreCase)) return;
            tokens.Add(t);
        }
        Add(app.DisplayName.Split(" v")[0].Split(" for ")[0].Trim());
        // 从 InstallLocation 取目录名
        try
        {
            if (!string.IsNullOrWhiteSpace(app.InstallLocation))
            {
                var norm = app.InstallLocation.TrimEnd('\\');
                Add(Path.GetFileName(norm));
            }
        }
        catch { }
        // 从卸载命令路径推断
        try
        {
            if (!string.IsNullOrWhiteSpace(app.UninstallString))
            {
                var exe = PathUtil.Normalize(app.UninstallString.Trim('"').Split(" -")[0]);
                if (exe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(exe))
                    Add(Path.GetFileName(Path.GetDirectoryName(exe)));
            }
        }
        catch { }
        return tokens;
    }

    /// <summary>删除残留：目录进隔离区，注册表键直接删除（先导出备份 json）。</summary>
    public (int ok, int fail) RemoveLeftovers(IEnumerable<LeftoverItem> items)
    {
        int ok = 0, fail = 0;
        foreach (var item in items.Where(i => i.Selected))
        {
            if (item.IsRegistry)
            {
                try
                {
                    var sub = item.Path["reg://".Length..];
                    var hiveIdx = sub.IndexOf('\\');
                    var hive = sub[..hiveIdx];
                    var keyPath = sub[(hiveIdx + 1)..];
                    if (hive.Equals("HKCU", StringComparison.OrdinalIgnoreCase))
                    {
                        BackupRegistryKey("HKCU\\" + keyPath);
                        using var k = Registry.CurrentUser.OpenSubKey(keyPath, writable: true);
                        if (k != null)
                        {
                            // 删除键下所有值与子键后删除自身
                            foreach (var child in k.GetSubKeyNames())
                                try { k.DeleteSubKeyTree(child); } catch { }
                            foreach (var val in k.GetValueNames())
                                try { k.DeleteValue(val, false); } catch { }
                        }
                        DeleteKeySelf(Registry.CurrentUser, keyPath);
                        ok++;
                    }
                    else fail++;
                }
                catch { fail++; }
            }
            else
            {
                if (QuarantineService.Instance.MoveIntoQuarantine(item.Path, "leftover", out _)) ok++;
                else fail++;
            }
        }
        if (ok > 0)
            HistoryService.Add("Clean", $"清理了 {ok} 项卸载残留");
        return (ok, fail);
    }

    private static void DeleteKeySelf(RegistryKey root, string keyPath)
    {
        var parentIdx = keyPath.LastIndexOf('\\');
        if (parentIdx < 0) { try { root.DeleteSubKeyTree(keyPath); } catch { } return; }
        var parent = keyPath[..parentIdx];
        var leaf = keyPath[(parentIdx + 1)..];
        try
        {
            using var pk = root.OpenSubKey(parent, writable: true);
            pk?.DeleteSubKeyTree(leaf);
        }
        catch { }
    }

    private static void BackupRegistryKey(string fullPath)
    {
        try
        {
            var dir = Path.Combine(SettingsService.DataDir, "RegistryBackups");
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, $"reg_{DateTime.Now:yyyyMMdd_HHmmss}_{Math.Abs(fullPath.GetHashCode()):X8}.reg");
            var psi = new ProcessStartInfo("reg.exe", $"export \"{fullPath}\" \"{file}\" /y")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
            };
            using var p = Process.Start(psi)!;
            p.WaitForExit(15000);
        }
        catch { }
    }

    private static DateTime ParseInstallDate(string? s)
    {
        if (!string.IsNullOrEmpty(s) && s.Length == 8 &&
            DateTime.TryParseExact(s, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var d))
            return d;
        return default;
    }

    private static string? ExtractIconPath(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var s = raw.Trim();
        var comma = s.IndexOf(',');
        if (comma > 0) s = s[..comma];
        s = s.Trim('"');
        try { s = Environment.ExpandEnvironmentVariables(s); } catch { }
        return s.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(s) ? s : null;
    }
}

/// <summary>安装目录是否可被当作残留移除（防御性检查）。</summary>
internal static class SafetyCheck
{
    public static bool InstallLocationRemovable(string path)
    {
        try
        {
            var full = Path.GetFullPath(path.TrimEnd('\\'));
            // 不在 Program Files / Windows / 根目录下
            foreach (var root in new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            })
            {
                if (full.StartsWith(root + "\\", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(full, root, StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            if (full.Length <= 3) return false;
            return true;
        }
        catch { return false; }
    }
}
