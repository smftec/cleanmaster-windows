using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using CleanMaster.Core.Models;
using CleanMaster.Core.Utils;

namespace CleanMaster.Core.Rules;

// ————————————————————————— 隐私痕迹 —————————————————————————
// 与垃圾清理分开：默认全部不勾选（除风险极低项），必须用户主动选择。

public sealed class RecentFilesRule : RuleBase
{
    private static string RecentDir => Exp(@"%APPDATA%\Microsoft\Windows\Recent");
    public override string Id => "privacy_recent";
    public override string Name => "最近使用文件记录";
    public override RuleGroup Group => RuleGroup.Privacy;
    public override RiskLevel Risk => RiskLevel.Confirm;
    public override bool DefaultSelected => false;
    public override bool CanRestore => false;
    public override string Reason => "资源管理器「最近使用」与跳转列表的来源记录";
    public override string Impact => "「最近使用」列表将被清空，不影响文件本身";
    public override IEnumerable<string> AllowedRoots() { yield return PathUtil.Normalize(RecentDir); }

    public override Task<RuleResult> ScanAsync(ScanContext ctx)
    {
        var res = new RuleResult
        {
            RuleId = Id, Name = Name, Group = Group, Risk = Risk,
            Reason = Reason, Impact = Impact, CanRestore = false,
        };
        AddDirFiles(res, ctx, RecentDir, "*.lnk", recursive: false);
        return Task.FromResult(res);
    }
}

public sealed class JumpListRule : RuleBase
{
    public override string Id => "privacy_jumplist";
    public override string Name => "跳转列表 (Jump List) 记录";
    public override RuleGroup Group => RuleGroup.Privacy;
    public override RiskLevel Risk => RiskLevel.Confirm;
    public override bool DefaultSelected => false;
    public override bool CanRestore => false;
    public override string Reason => "任务栏右键菜单展示的历史记录数据";
    public override string Impact => "各应用任务栏右键的历史列表将被清空，不影响应用功能";
    public override IEnumerable<string> AllowedRoots()
    {
        yield return PathUtil.Normalize(Exp(@"%APPDATA%\Microsoft\Windows\Recent\AutomaticDestinations"));
        yield return PathUtil.Normalize(Exp(@"%APPDATA%\Microsoft\Windows\Recent\CustomDestinations"));
    }

    public override Task<RuleResult> ScanAsync(ScanContext ctx)
    {
        var res = new RuleResult
        {
            RuleId = Id, Name = Name, Group = Group, Risk = Risk,
            Reason = Reason, Impact = Impact, CanRestore = false,
        };
        AddDirFiles(res, ctx, Exp(@"%APPDATA%\Microsoft\Windows\Recent\AutomaticDestinations"), "*.automaticDestinations-ms", recursive: false);
        AddDirFiles(res, ctx, Exp(@"%APPDATA%\Microsoft\Windows\Recent\CustomDestinations"), "*.customDestinations-ms", recursive: false);
        return Task.FromResult(res);
    }
}

/// <summary>注册表型隐私项：Scan 返回一个虚拟条目，Clean 走自定义逻辑。</summary>
public abstract class RegistryPrivacyRule : RuleBase
{
    protected abstract string KeyPath { get; }
    protected abstract string ItemNote { get; }
    protected virtual string Scheme => "reg://";

    public override bool CanRestore => false;
    public override IEnumerable<string> AllowedRoots() { yield break; }

    public override Task<RuleResult> ScanAsync(ScanContext ctx)
    {
        var res = new RuleResult
        {
            RuleId = Id, Name = Name, Group = Group, Risk = Risk,
            Reason = Reason, Impact = Impact, CanRestore = false,
        };
        int count = 0;
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(KeyPath);
            if (k != null) count = k.ValueCount;
        }
        catch { }
        res.Items.Add(new CleanItem { Path = Scheme + KeyPath, Size = 0, Note = ItemNote + (count > 0 ? $"（{count} 条记录）" : "（无记录）") });
        if (count > 0) { res.FileCount = count; }
        return Task.FromResult(res);
    }

    public override async Task CleanAsync(CleanContext ctx)
    {
        await Task.Run(() =>
        {
            try
            {
                using var k = Registry.CurrentUser.OpenSubKey(KeyPath, writable: true);
                if (k == null)
                {
                    foreach (var item in ctx.Selected) ctx.Report(item, CleanItemStatus.Success, "无记录");
                    return;
                }
                foreach (var name in k.GetValueNames().ToArray())
                {
                    try { k.DeleteValue(name, false); } catch { }
                }
                foreach (var item in ctx.Selected) ctx.Report(item, CleanItemStatus.Success, "");
            }
            catch (Exception ex)
            {
                foreach (var item in ctx.Selected) ctx.Report(item, CleanItemStatus.Failed, ex.Message);
            }
        }, ctx.Ct);
    }
}

public sealed class RunMruRule : RegistryPrivacyRule
{
    protected override string KeyPath => @"Software\Microsoft\Windows\CurrentVersion\Explorer\RunMRU";
    protected override string ItemNote => "Win+R 运行窗口的输入历史";
    public override string Id => "privacy_runmru";
    public override string Name => "运行窗口历史";
    public override RuleGroup Group => RuleGroup.Privacy;
    public override RiskLevel Risk => RiskLevel.Confirm;
    public override bool DefaultSelected => false;
    public override string Reason => "Win+R 对话框保留的命令输入记录";
    public override string Impact => "运行窗口的历史下拉被清空，不影响系统功能";
}

public sealed class SearchHistoryRule : RegistryPrivacyRule
{
    protected override string KeyPath => @"Software\Microsoft\Windows\CurrentVersion\Explorer\WordWheelQuery";
    protected override string ItemNote => "资源管理器搜索框的历史关键词";
    public override string Id => "privacy_search";
    public override string Name => "文件搜索历史";
    public override RuleGroup Group => RuleGroup.Privacy;
    public override RiskLevel Risk => RiskLevel.Confirm;
    public override bool DefaultSelected => false;
    public override string Reason => "资源管理器搜索框保留的搜索关键词";
    public override string Impact => "搜索历史下拉被清空，不影响搜索功能";
}

public sealed class DnsCacheRule : RuleBase
{
    public override string Id => "privacy_dns";
    public override string Name => "DNS 解析缓存";
    public override RuleGroup Group => RuleGroup.Privacy;
    public override RiskLevel Risk => RiskLevel.Confirm;
    public override bool DefaultSelected => false;
    public override bool CanRestore => false;
    public override string Reason => "系统保存的域名解析记录";
    public override string Impact => "清空后首次访问网站需重新解析，可能慢零点几秒；需管理员权限";
    public override IEnumerable<string> AllowedRoots() { yield break; }

    public override Task<RuleResult> ScanAsync(ScanContext ctx)
    {
        var res = new RuleResult
        {
            RuleId = Id, Name = Name, Group = Group, Risk = Risk,
            Reason = Reason, Impact = Impact, CanRestore = false,
        };
        res.Items.Add(new CleanItem { Path = "cmd://ipconfig /flushdns", Note = "系统 DNS 缓存" });
        res.FileCount = 1;
        return Task.FromResult(res);
    }

    public override async Task CleanAsync(CleanContext ctx)
    {
        await Task.Run(() =>
        {
            try
            {
                var psi = new ProcessStartInfo("ipconfig", "/flushdns")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                };
                using var p = Process.Start(psi)!;
                p.WaitForExit(15000);
                var ok = p.ExitCode == 0;
                foreach (var item in ctx.Selected)
                    ctx.Report(item, ok ? CleanItemStatus.Success : CleanItemStatus.Failed,
                        ok ? "" : "刷新失败，可能需要管理员权限");
            }
            catch (Exception ex)
            {
                foreach (var item in ctx.Selected) ctx.Report(item, CleanItemStatus.Failed, ex.Message);
            }
        }, ctx.Ct);
    }
}

public sealed class ClipboardRule : RuleBase
{
    public override string Id => "privacy_clipboard";
    public override string Name => "剪贴板内容";
    public override RuleGroup Group => RuleGroup.Privacy;
    public override RiskLevel Risk => RiskLevel.Confirm;
    public override bool DefaultSelected => false;
    public override bool CanRestore => false;
    public override string Reason => "剪贴板中可能还保留着你最近复制的内容";
    public override string Impact => "当前剪贴板内容将被清空";
    public override IEnumerable<string> AllowedRoots() { yield break; }

    public override Task<RuleResult> ScanAsync(ScanContext ctx)
    {
        var res = new RuleResult
        {
            RuleId = Id, Name = Name, Group = Group, Risk = Risk,
            Reason = Reason, Impact = Impact, CanRestore = false,
        };
        res.Items.Add(new CleanItem { Path = "clip://clipboard", Note = "当前剪贴板" });
        res.FileCount = 1;
        return Task.FromResult(res);
    }

    public override async Task CleanAsync(CleanContext ctx)
    {
        await Task.Run(() =>
        {
            var ok = ClipboardNative.TryClear();
            foreach (var item in ctx.Selected)
                ctx.Report(item, ok ? CleanItemStatus.Success : CleanItemStatus.Failed, ok ? "" : "剪贴板被其他程序占用");
        }, ctx.Ct);
    }
}

internal static class ClipboardNative
{
    [DllImport("user32.dll")] private static extern bool OpenClipboard(IntPtr hWndNewOwner);
    [DllImport("user32.dll")] private static extern bool CloseClipboard();
    [DllImport("user32.dll")] private static extern bool EmptyClipboard();

    public static bool TryClear()
    {
        for (int i = 0; i < 5; i++)
        {
            if (OpenClipboard(IntPtr.Zero))
            {
                try { EmptyClipboard(); }
                finally { CloseClipboard(); }
                return true;
            }
            Thread.Sleep(120);
        }
        return false;
    }
}

/// <summary>浏览器历史/Cookie：直接删除浏览器数据库文件，风险更高，独立列出。</summary>
public sealed class BrowserPrivacyRule : RuleBase
{
    private readonly string _browserName;
    private readonly string _userDataDir;
    private readonly string[] _exes;
    private readonly bool _cookies;

    public override string Id { get; }
    public override string Name => _cookies ? $"{_browserName} Cookie（会退出登录）" : $"{_browserName} 浏览历史";
    public override RuleGroup Group => RuleGroup.Privacy;
    public override RiskLevel Risk => RiskLevel.Confirm;
    public override bool DefaultSelected => false;
    public override bool CanRestore => false;

    public override string Reason => _cookies
        ? "浏览器保存的网站登录凭证"
        : "浏览器保存的网页访问历史数据库";
    public override string Impact => _cookies
        ? "清理后这些网站的登录状态将失效，需要重新登录"
        : "浏览历史将被清空；收藏夹与保存的密码不受影响";

    public BrowserPrivacyRule(string id, string browserName, string userDataDir, string[] exes, bool cookies)
    {
        Id = id; _browserName = browserName;
        _userDataDir = Environment.ExpandEnvironmentVariables(userDataDir);
        _exes = exes; _cookies = cookies;
    }

    public override IEnumerable<string> AllowedRoots() { yield return PathUtil.Normalize(_userDataDir); }

    private static readonly string[] Profiles = ["Default", "Profile 1", "Profile 2", "Profile 3", "Profile 4", "Profile 5"];

    public override Task<RuleResult> ScanAsync(ScanContext ctx)
    {
        var res = new RuleResult
        {
            RuleId = Id, Name = Name, Group = Group, Risk = Risk,
            Reason = Reason, Impact = Impact, CanRestore = false,
        };
        if (ProcRunning(_exes.Select(e => System.IO.Path.GetFileNameWithoutExtension(e)).ToArray()))
            res.RunningProcesses.AddRange(_exes);
        if (!Directory.Exists(_userDataDir)) return Task.FromResult(res);
        foreach (var profile in Profiles)
        {
            var pdir = System.IO.Path.Combine(_userDataDir, profile);
            if (!Directory.Exists(pdir)) continue;
            var files = _cookies
                ? new[] { "Cookies", "Cookies-journal" }
                : new[] { "History", "History-journal", "Visited Links", "Top Sites", "Top Sites-journal" };
            foreach (var f in files)
            {
                var fp = System.IO.Path.Combine(pdir, f);
                try
                {
                    if (!File.Exists(fp)) continue;
                    var fi = new FileInfo(fp);
                    res.Items.Add(new CleanItem { Path = fp, Size = fi.Length, ModifiedTime = fi.LastWriteTime, Note = profile });
                    res.FileCount++; res.TotalSize += fi.Length;
                }
                catch { }
            }
        }
        return Task.FromResult(res);
    }
}
