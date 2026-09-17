using System.IO;
using CleanMaster.Core.Models;
using CleanMaster.Core.Utils;

namespace CleanMaster.Core.Rules;

// ————————————————————————— 浏览器缓存 —————————————————————————

/// <summary>Chromium 系浏览器的用户数据目录布局一致，统一处理。</summary>
public sealed class BrowserCacheRule : RuleBase
{
    private readonly string _userDataDir;
    private readonly string[] _exes;
    private readonly string _browserName;

    public override string Id { get; }
    public override string Name => $"{_browserName} 缓存";
    public override RuleGroup Group => RuleGroup.BrowserCache;
    public override RiskLevel Risk => RiskLevel.Low;
    public override string Reason => "浏览器为加速网页加载生成的缓存文件";
    public override string Impact => "网页首次打开可能略慢；不影响登录状态、密码与历史记录";
    public override bool CanRestore => false;

    public BrowserCacheRule(string id, string browserName, string userDataDir, params string[] exes)
    {
        Id = id;
        _browserName = browserName;
        _userDataDir = Environment.ExpandEnvironmentVariables(userDataDir);
        _exes = exes;
    }

    public override IEnumerable<string> AllowedRoots()
    {
        yield return PathUtil.Normalize(_userDataDir);
    }

    private static readonly string[] ProfileCandidates =
        ["Default", "Profile 1", "Profile 2", "Profile 3", "Profile 4", "Profile 5", "Guest Profile"];

    private static readonly string[] CacheSubDirs =
    [
        @"Cache\Cache_Data",
        @"Code Cache",
        @"GPUCache",
        @"Media Cache",
        @"Service Worker\CacheStorage",
        @"Service Worker\ScriptCache",
        @"DawnCache",
        @"GrShaderCache",
        @"ShaderCache",
        @"Crashpad\completed",
        @"Crashpad\reports",
        @"Crashpad\pending",
    ];

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

        foreach (var profile in ProfileCandidates)
        {
            var pdir = System.IO.Path.Combine(_userDataDir, profile);
            if (!Directory.Exists(pdir)) continue;
            foreach (var sub in CacheSubDirs)
            {
                var cdir = System.IO.Path.Combine(pdir, sub);
                AddWholeDirItem(res, cdir);
            }
        }
        // 根级缓存（部分老版本）
        foreach (var sub in new[] { "GraphiteDawnCache", "GrShaderCache", "ShaderCache", "Crashpad" })
            AddWholeDirItem(res, System.IO.Path.Combine(_userDataDir, sub));
        return Task.FromResult(res);
    }
}

public sealed class FirefoxCacheRule : RuleBase
{
    public override string Id => "firefox_cache";
    public override string Name => "Firefox 缓存";
    public override RuleGroup Group => RuleGroup.BrowserCache;
    public override RiskLevel Risk => RiskLevel.Low;
    public override bool CanRestore => false;
    public override string Reason => "浏览器为加速网页加载生成的缓存文件";
    public override string Impact => "网页首次打开可能略慢；不影响登录状态、密码与历史记录";

    public override IEnumerable<string> AllowedRoots()
    {
        yield return PathUtil.Normalize(Exp(@"%LOCALAPPDATA%\Mozilla\Firefox"));
    }

    public override Task<RuleResult> ScanAsync(ScanContext ctx)
    {
        var res = new RuleResult
        {
            RuleId = Id, Name = Name, Group = Group, Risk = Risk,
            Reason = Reason, Impact = Impact, CanRestore = false,
        };
        if (ProcRunning("firefox")) res.RunningProcesses.Add("firefox.exe");

        var profilesRoot = Exp(@"%LOCALAPPDATA%\Mozilla\Firefox\Profiles");
        if (Directory.Exists(profilesRoot))
        {
            foreach (var profile in SafeDirs(profilesRoot))
            {
                foreach (var sub in new[] { "cache2", "startupCache", "shader-cache", "thumbnails", "crashes", "minidumps" })
                    AddWholeDirItem(res, System.IO.Path.Combine(profile, sub));
            }
        }
        AddWholeDirItem(res, Exp(@"%LOCALAPPDATA%\Mozilla\Firefox\Crash Reports"));
        return Task.FromResult(res);
    }

    private static IEnumerable<string> SafeDirs(string dir)
    {
        try { return Directory.GetDirectories(dir); } catch { return Enumerable.Empty<string>(); }
    }
}

// ————————————————————————— 应用缓存 —————————————————————————

/// <summary>
/// 通用应用缓存规则：一组"整目录移除"或"递归文件"的路径定义。
/// 目录存在才计入；清理时整个目录按安全等级处理。
/// </summary>
public sealed class AppCacheRule : RuleBase
{
    private readonly (string path, bool wholeDir)[] _targets;
    private readonly string[]? _processes;
    private readonly string _id, _name, _reason, _impact;

    public override string Id => _id;
    public override string Name => _name;
    public override RuleGroup Group => RuleGroup.AppCache;
    public override RiskLevel Risk { get; }
    public override bool DefaultSelected { get; }
    public override bool CanRestore { get; }
    public override string Reason => _reason;
    public override string Impact => _impact;

    public AppCacheRule(string id, string name, RiskLevel risk, bool defaultSelected,
        string reason, string impact, string[]? processes, params (string path, bool wholeDir)[] targets)
    {
        _id = id; _name = name; Risk = risk; DefaultSelected = defaultSelected;
        CanRestore = risk <= RiskLevel.Confirm;
        _reason = reason; _impact = impact; _processes = processes; _targets = targets;
    }

    public override IEnumerable<string> AllowedRoots()
    {
        foreach (var (raw, _) in _targets)
        {
            var path = Environment.ExpandEnvironmentVariables(raw);
            if (path.Contains('*'))
            {
                // 通配段：允许根取通配段的父目录
                var stem = path.TrimEnd('*').TrimEnd('\\');
                var parent = System.IO.Path.GetDirectoryName(stem);
                if (!string.IsNullOrEmpty(parent)) yield return PathUtil.Normalize(parent);
            }
            else
            {
                yield return PathUtil.Normalize(path);
            }
        }
    }

    public override Task<RuleResult> ScanAsync(ScanContext ctx)
    {
        var res = new RuleResult
        {
            RuleId = Id, Name = Name, Group = Group, Risk = Risk,
            Reason = Reason, Impact = Impact, CanRestore = CanRestore,
        };
        if (_processes != null && ProcRunning(_processes.Select(p => System.IO.Path.GetFileNameWithoutExtension(p)).ToArray()))
            res.RunningProcesses.AddRange(_processes);

        foreach (var (raw, wholeDir) in _targets)
        {
            var path = Environment.ExpandEnvironmentVariables(raw);
            if (path.EndsWith('*'))
            {
                // 通配目录：Cache/Code Cache 这类多版本目录
                var parent = System.IO.Path.GetDirectoryName(path[..^1]);
                var pattern = System.IO.Path.GetFileName(path[..^1]) + "*";
                if (parent == null || !Directory.Exists(parent)) continue;
                foreach (var d in SafeDirs(parent))
                {
                    var name = System.IO.Path.GetFileName(d);
                    if (!LikeMatch(name, pattern)) continue;
                    if (wholeDir) AddWholeDirItem(res, d);
                    else AddDirFiles(res, ctx, d, "*", recursive: true);
                }
            }
            else if (wholeDir)
            {
                AddWholeDirItem(res, path);
            }
            else if (Directory.Exists(path))
            {
                AddDirFiles(res, ctx, path, "*", recursive: true);
            }
        }
        return Task.FromResult(res);
    }

    private static IEnumerable<string> SafeDirs(string dir)
    {
        try { return Directory.GetDirectories(dir); } catch { return Enumerable.Empty<string>(); }
    }
}

public static class AppCacheCatalog
{
    /// <summary>构建内置应用缓存规则。数据布局按主流版本维护，路径只含缓存/日志，绝不含用户聊天内容与存档。</summary>
    public static List<IScanRule> Build() => new()
    {
        new AppCacheRule("wechat_cache", "微信 (缓存/日志)", RiskLevel.Low, true,
            "微信客户端的运行日志与网页缓存",
            "不影响聊天记录；聊天文件保存在用户目录中，本规则不会触碰",
            ["WeChat", "Weixin"],
            (@"%APPDATA%\Tencent\Logs", true),
            (@"%APPDATA%\Tencent\WeChat\log", true),
            (@"%APPDATA%\Tencent\xwechat\radium", true)),
        new AppCacheRule("qq_cache", "QQ (缓存/日志)", RiskLevel.Low, true,
            "QQ 客户端的运行日志与组件缓存",
            "不影响聊天记录；聊天文件保存在用户目录中，本规则不会触碰",
            ["QQ"],
            (@"%APPDATA%\Tencent\QQ\Logs", true),
            (@"%APPDATA%\Tencent\QQ\Temp", true),
            (@"%APPDATA%\Tencent\QQ\nr\versions\*\cache", true)),
        new AppCacheRule("discord_cache", "Discord 缓存", RiskLevel.Low, true,
            "Discord 桌面客户端的网页缓存",
            "应用内图片/视频需要重新加载；不影响账号与消息",
            ["Discord"],
            (@"%APPDATA%\discord\Cache", true),
            (@"%APPDATA%\discord\Code Cache", true),
            (@"%APPDATA%\discord\GPUCache", true),
            (@"%APPDATA%\discord\Crashpad", true)),
        new AppCacheRule("telegram_cache", "Telegram 媒体缓存", RiskLevel.Confirm, false,
            "Telegram 已下载媒体的本地缓存",
            "聊天记录在云端不受影响，但离线可看的图片/视频需要重新下载",
            ["Telegram"],
            (@"%APPDATA%\Telegram Desktop\tdata\user_data", true)),
        new AppCacheRule("teams_cache", "Microsoft Teams 缓存", RiskLevel.Low, true,
            "Teams 桌面客户端的网页与组件缓存",
            "下次启动 Teams 需要重新加载；不影响账号与聊天",
            ["ms-teams", "Teams"],
            (@"%APPDATA%\Microsoft\Teams\Cache", true),
            (@"%APPDATA%\Microsoft\Teams\GPUCache", true),
            (@"%APPDATA%\Microsoft\Teams\Service Worker\CacheStorage", true),
            (@"%APPDATA%\Microsoft\Teams\logs", true),
            (@"%LOCALAPPDATA%\Packages\MSTeams_8wekyb3d8bbwe\LocalCache\Microsoft\MSTeams\EBWebView\Default\Code Cache", true),
            (@"%LOCALAPPDATA%\Packages\MSTeams_8wekyb3d8bbwe\LocalCache\Microsoft\MSTeams\EBWebView\Default\Cache", true)),
        new AppCacheRule("office_cache", "Office 文件缓存", RiskLevel.Low, true,
            "Office 文档上传/协作时产生的本地缓存",
            "已保存到云端的文档不受影响；本地未保存的编辑不受影响",
            null,
            (@"%LOCALAPPDATA%\Microsoft\Office\16.0\OfficeFileCache", true),
            (@"%LOCALAPPDATA%\Microsoft\Office\FileCache", true),
            (@"%LOCALAPPDATA%\Microsoft\Office\OTele", true)),
        new AppCacheRule("onedrive_logs", "OneDrive 日志", RiskLevel.Safe, true,
            "OneDrive 同步产生的诊断日志",
            "不影响同步与文件",
            null,
            (@"%LOCALAPPDATA%\Microsoft\OneDrive\logs", true),
            (@"%LOCALAPPDATA%\Microsoft\OneDrive\setup\logs", true)),
        new AppCacheRule("nvidia_cache", "NVIDIA / AMD 图形缓存", RiskLevel.Low, true,
            "显卡驱动生成的着色器与材质编译缓存",
            "游戏首次启动需重新编译，可能略微变慢",
            null,
            (@"%LOCALAPPDATA%\NVIDIA Corporation\NV_Cache", true)),
        new AppCacheRule("steam_htmlcache", "Steam 网页缓存", RiskLevel.Low, true,
            "Steam 内置浏览器（商店/社区页面）的缓存",
            "商店页面首次打开略慢；不影响游戏本体、存档与库存",
            ["steam"],
            (@"%LOCALAPPDATA%\Steam\htmlcache", true)),
        new AppCacheRule("epic_cache", "Epic Games 启动器缓存", RiskLevel.Low, true,
            "Epic 启动器网页视图的缓存与日志",
            "不影响已安装游戏与账号",
            ["EpicGamesLauncher"],
            (@"%LOCALAPPDATA%\EpicGamesLauncher\Saved\webcache*", true),
            (@"%LOCALAPPDATA%\EpicGamesLauncher\Saved\Logs", true)),
        new AppCacheRule("vscode_cache", "VS Code 缓存", RiskLevel.Low, true,
            "VS Code 的界面缓存与崩溃日志",
            "下次启动需重建索引，窗口布局与扩展不受影响",
            ["Code"],
            (@"%APPDATA%\Code\Cache*", true),
            (@"%APPDATA%\Code\CachedData", true),
            (@"%APPDATA%\Code\Code Cache", true),
            (@"%APPDATA%\Code\GPUCache", true),
            (@"%APPDATA%\Code\logs", true),
            (@"%APPDATA%\Code\Service Worker\CacheStorage", true)),
        new AppCacheRule("dev_caches", "开发工具缓存 (npm/pip/NuGet/Gradle)", RiskLevel.Confirm, false,
            "包管理器下载的依赖缓存与构建产物",
            "下次构建/安装需重新下载依赖或重建索引，消耗额外网络流量",
            null,
            (@"%LOCALAPPDATA%\npm-cache", true),
            (@"%LOCALAPPDATA%\pip\cache", true),
            (@"%USERPROFILE%\.nuget\packages", true),
            (@"%USERPROFILE%\.gradle\caches", true),
            (@"%USERPROFILE%\.m2\repository", true)),
        new AppCacheRule("jetbrains_cache", "JetBrains IDE 缓存", RiskLevel.Confirm, false,
            "JetBrains 系列 IDE 的索引与本地历史缓存",
            "下次打开项目需重新建立索引，本地历史记录会被清空",
            null,
            (@"%LOCALAPPDATA%\JetBrains\*\caches", true),
            (@"%LOCALAPPDATA%\JetBrains\*\log", true)),
    };
}
