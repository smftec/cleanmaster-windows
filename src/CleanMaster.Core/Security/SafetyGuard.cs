using CleanMaster.Core.Utils;

namespace CleanMaster.Core.Security;

/// <summary>
/// 统一删除保护：所有清理/粉碎操作必须经过本类校验，禁止业务代码直接 File.Delete。
/// 校验策略：
///  1. 目标必须位于规则声明的允许根目录之下；
///  2. 目标绝不能位于用户文档目录与系统关键目录（硬编码拒绝表）；
///  3. 白名单优先级最高。
/// </summary>
public static class SafetyGuard
{
    /// <summary>硬编码拒绝清理的目录（不允许被任何通配规则直接删除）。</summary>
    private static readonly string[] UserProtectedDirs =
    {
        Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
        Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
        Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
        System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
    };

    /// <summary>程序自身安装目录（防止自删）。</summary>
    public static string AppInstallDir { get; set; } =
        AppContext.BaseDirectory;

    private static readonly string[] SystemDirsRelative =
    {
        "System32", "SysWOW64", "WinSxS", "Installer", "DriverStore", "Fonts",
        "registration", "servicing", "Microsoft.NET", "assembly",
    };

    private static readonly char[] Sep = ['\\', '/'];

    /// <summary>拒绝表命中判断（供测试与 UI 提示）。</summary>
    public static string? FindDenyReason(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "空路径";
        string full;
        try { full = System.IO.Path.GetFullPath(Environment.ExpandEnvironmentVariables(path)).TrimEnd('\\'); }
        catch { return "非法路径"; }

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        foreach (var dir in UserProtectedDirs)
        {
            if (string.IsNullOrEmpty(dir)) continue;
            var d = dir.TrimEnd('\\');
            // 用户主目录本身，或用户目录的直接子目录（Documents/Downloads 等）受保护；
            // 但 AppData 下的内容不受此表保护（由规则允许根目录约束）。
            if (string.Equals(full, d, StringComparison.OrdinalIgnoreCase))
                return string.Equals(full, userProfile, StringComparison.OrdinalIgnoreCase) ? "用户主目录受保护" : "用户文档目录受保护";
            if (full.Length > d.Length && full.StartsWith(d + "\\", StringComparison.OrdinalIgnoreCase))
            {
                // AppData 白名单例外：AppData 由各规则的允许根约束
                var rest = full[(d.Length + 1)..].Split(Sep, 2)[0];
                if (!rest.Equals("AppData", StringComparison.OrdinalIgnoreCase))
                    return "用户文档目录受保护";
            }
        }

        var windir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (full.StartsWith(windir + "\\", StringComparison.OrdinalIgnoreCase))
        {
            var firstSeg = full[(windir.Length + 1)..].Split(Sep)[0];
            if (SystemDirsRelative.Contains(firstSeg, StringComparer.OrdinalIgnoreCase))
                return "Windows 关键目录受保护";
        }

        foreach (var pf in new[] {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) })
        {
            if (!string.IsNullOrEmpty(pf) &&
                (string.Equals(full, pf, StringComparison.OrdinalIgnoreCase) ||
                 full.StartsWith(pf + "\\", StringComparison.OrdinalIgnoreCase)))
                return "程序安装目录受保护（请使用应用管理卸载）";
        }

        if (full.EndsWith(":\\")) return "磁盘根目录受保护";
        if (full.Length <= 3 && full.EndsWith(':')) return "磁盘根目录受保护";

        if (AppInstallDir.TrimEnd('\\') != "" &&
            (string.Equals(full, AppInstallDir.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase) ||
             full.StartsWith(AppInstallDir.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)))
            return "应用自身目录受保护";

        return null;
    }

    /// <summary>
    /// 清理校验：目标必须在规则允许根内、不在拒绝表、不在白名单。
    /// 返回 null 表示通过，否则返回拒绝原因。
    /// </summary>
    public static string? ValidateForClean(string path, IEnumerable<string> allowedRoots, ISet<string>? whitelist = null)
    {
        var deny = FindDenyReason(path);
        if (deny != null) return deny;

        string full;
        try { full = System.IO.Path.GetFullPath(Environment.ExpandEnvironmentVariables(path)).TrimEnd('\\'); }
        catch { return "非法路径"; }

        bool inAllowed = false;
        foreach (var root in allowedRoots)
        {
            if (PathUtil.IsUnder(full, root)) { inAllowed = true; break; }
        }
        if (!inAllowed) return "目标不在该规则的允许目录内";

        if (whitelist != null)
        {
            foreach (var w in whitelist)
            {
                if (string.IsNullOrWhiteSpace(w)) continue;
                if (PathUtil.IsUnder(full, w)) return "白名单项目";
            }
        }
        return null;
    }
}
