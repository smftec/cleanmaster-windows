using System.IO;
using CleanMaster.Core.Utils;

namespace CleanMaster.Core.Analysis;

public sealed class EmptyFolderInfo
{
    public string Path { get; set; } = "";
    public DateTime LastWriteTime { get; set; }
    /// <summary>目录树内的子目录数（含自身层级下的空子目录）。</summary>
    public int SubFolderCount { get; set; }
}

/// <summary>
/// 空文件夹查找：递归判定"整棵子树不含任何文件"的目录。
/// 不跨越 Reparse Point，忽略常见系统/受保护目录；
/// 无法枚举（权限不足等）的目录一律视为非空，宁漏勿删。
/// </summary>
public sealed class EmptyFolderService
{
    private static readonly string[] SkipDirNames =
    [
        "$Recycle.Bin", "System Volume Information", "$WinREAgent", "Config.Msi",
        "Windows.old", "Recovery", "PerfLogs", "AppData",
    ];

    /// <summary>扫描 roots 下的空目录。maxDepth 为相对根的层级上限。</summary>
    public async Task<List<EmptyFolderInfo>> ScanAsync(
        IReadOnlyList<string> roots, int maxDepth, CancellationToken ct,
        IProgress<string>? progress = null)
    {
        var found = new List<EmptyFolderInfo>();
        await Task.Run(() =>
        {
            foreach (var root in roots)
            {
                ct.ThrowIfCancellationRequested();
                if (!Directory.Exists(root) || PathUtil.IsReparsePoint(root)) continue;
                progress?.Report(root);
                Walk(root, 0, ct, found);
            }
        }, ct);
        // 只保留最顶层的空目录（父空目录已覆盖子树），避免重复计数
        var distinct = new List<EmptyFolderInfo>();
        foreach (var f in found)
        {
            if (!found.Any(o => o != f && PathUtil.IsUnder(f.Path, o.Path)))
                distinct.Add(f);
        }
        return distinct;
    }

    /// <summary>判定目录子树；整棵子树无文件时记入 found 并返回 true。</summary>
    private static bool Walk(string dir, int depth, CancellationToken ct, List<EmptyFolderInfo> found)
    {
        ct.ThrowIfCancellationRequested();

        string[] entries;
        try
        {
            entries = Directory.GetFileSystemEntries(dir);
        }
        catch
        {
            // 枚举失败（权限等）：视为非空，保守处理
            return false;
        }

        int childDirs = 0, emptySubs = 0;
        foreach (var entry in entries)
        {
            if (File.Exists(entry)) return false; // 有文件 → 非空，直接短路
        }
        foreach (var entry in entries)
        {
            if (PathUtil.IsReparsePoint(entry)) continue;
            if (depth >= 32) continue;
            if (SkipDirNames.Contains(Path.GetFileName(entry), StringComparer.OrdinalIgnoreCase)) continue;
            childDirs++;
            if (Walk(entry, depth + 1, ct, found)) emptySubs++;
        }

        // 空目录 = 无文件且所有子目录（可判定的）都是空目录
        if (childDirs == emptySubs)
        {
            found.Add(new EmptyFolderInfo
            {
                Path = dir,
                LastWriteTime = TryGetLastWrite(dir),
                SubFolderCount = childDirs,
            });
            return true;
        }
        return false;
    }

    private static DateTime TryGetLastWrite(string dir)
    {
        try { return Directory.GetLastWriteTime(dir); }
        catch { return default; }
    }
}
