using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using CleanMaster.Core.Models;
using CleanMaster.Core.Security;
using CleanMaster.Core.Store;
using CleanMaster.Core.Utils;

namespace CleanMaster.Core.Rules;

public sealed class ScanContext
{
    public CancellationToken Ct { get; init; }
    /// <summary>扫描明细回调（当前扫描路径）。</summary>
    public Action<string>? Detail { get; init; }
    public int SkipRecentMinutes { get; init; } = 10;
    public HashSet<string> Whitelist { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> ExcludedPaths { get; init; } = new();
}

public sealed class CleanContext
{
    public required RuleResult Result { get; init; }
    public required IReadOnlyList<CleanItem> Selected { get; init; }
    public required bool UseQuarantine { get; init; }
    public required CancellationToken Ct { get; init; }
    public required IProgress<CleanProgress>? Progress { get; init; }
    public required Action<CleanItem, CleanItemStatus, string> Report { get; init; }
    public required ISet<string> Whitelist { get; init; }
    public List<string> AllowedRoots { get; } = new();
}

public interface IScanRule
{
    string Id { get; }
    string Name { get; }
    RuleGroup Group { get; }
    RiskLevel Risk { get; }
    bool DefaultSelected { get; }
    bool CanRestore { get; }
    /// <summary>为什么可以清理（展示给用户）。</summary>
    string Reason { get; }
    /// <summary>清理后的影响（展示给用户）。</summary>
    string Impact { get; }
    /// <summary>允许清理的根目录（SafetyGuard 白名单约束）。</summary>
    IEnumerable<string> AllowedRoots();
    Task<RuleResult> ScanAsync(ScanContext ctx);
    Task CleanAsync(CleanContext ctx);
}

public abstract class RuleBase : IScanRule
{
    public abstract string Id { get; }
    public abstract string Name { get; }
    public abstract RuleGroup Group { get; }
    public abstract RiskLevel Risk { get; }
    public virtual bool DefaultSelected => Risk <= RiskLevel.Low;
    public virtual bool CanRestore => Risk <= RiskLevel.Confirm;
    public virtual string Reason => "";
    public virtual string Impact => "";
    public virtual IEnumerable<string> AllowedRoots() { yield break; }

    public abstract Task<RuleResult> ScanAsync(ScanContext ctx);

    public virtual async Task CleanAsync(CleanContext ctx)
    {
        await Task.Run(() =>
        {
            int total = ctx.Selected.Count;
            int done = 0;
            foreach (var item in ctx.Selected)
            {
                ctx.Ct.ThrowIfCancellationRequested();
                var deny = SafetyGuard.ValidateForClean(item.Path, ctx.AllowedRoots, ctx.Whitelist);
                if (deny != null)
                {
                    ctx.Report(item, CleanItemStatus.SkippedWhitelist, deny);
                }
                else if (item.IsDirectory)
                {
                    // 目录：优先整体移入隔离区（同卷移动）
                    string? qErr = null;
                    if (ctx.UseQuarantine && QuarantineService.Instance.MoveIntoQuarantine(item.Path, Id, out qErr))
                        ctx.Report(item, CleanItemStatus.Quarantined, "");
                    else if (ctx.UseQuarantine && qErr != null)
                        ctx.Report(item, CleanItemStatus.Failed, qErr);
                    else
                    {
                        var (ok, fail, _) = FileUtil.DeleteDirectoryTree(item.Path);
                        ctx.Report(item, fail == 0 ? CleanItemStatus.Success : ok > 0 ? CleanItemStatus.Success : CleanItemStatus.SkippedLocked,
                            fail > 0 ? $"{fail} 个子项被跳过" : "");
                    }
                }
                else
                {
                    if (ctx.UseQuarantine && File.Exists(item.Path))
                    {
                        if (QuarantineService.Instance.MoveIntoQuarantine(item.Path, Id, out var err))
                            ctx.Report(item, CleanItemStatus.Quarantined, "");
                        else
                            ctx.Report(item, CleanItemStatus.Failed, err ?? "无法移入隔离区");
                    }
                    else
                    {
                        var (ok, reason) = FileUtil.DeleteFile(item.Path);
                        ctx.Report(item, ok ? CleanItemStatus.Success : reason.Contains("占用") ? CleanItemStatus.SkippedLocked : CleanItemStatus.Failed, reason);
                    }
                }
                done++;
                if (done % 20 == 0 || done == total)
                    ctx.Progress?.Report(new CleanProgress { CurrentPath = item.Path, RuleName = Name, Processed = done, Total = total });
            }
        }, ctx.Ct);
    }

    // ————— 扫描辅助 —————

    protected static void AddDirFiles(RuleResult res, ScanContext ctx, string dir, string pattern, bool recursive, long minSize = 0)
    {
        if (!Directory.Exists(dir)) return;
        ctx.Detail?.Invoke(dir);
        var files = recursive ? SafeWalk(dir, ctx) : SafeFiles(dir, pattern, ctx);
        foreach (var f in files)
        {
            ctx.Ct.ThrowIfCancellationRequested();
            try
            {
                if (pattern != "*" && !recursive)
                {
                    var name = System.IO.Path.GetFileName(f);
                    if (!LikeMatch(name, pattern)) continue;
                }
                var fi = new FileInfo(f);
                if (minSize > 0 && fi.Length < minSize) continue;
                if (ctx.SkipRecentMinutes > 0 &&
                    (DateTime.Now - fi.LastWriteTime).TotalMinutes < ctx.SkipRecentMinutes) continue;
                if (ctx.Whitelist.Count > 0 && IsWhitelisted(f, ctx)) { continue; }
                res.Items.Add(new CleanItem { Path = f, Size = fi.Length, ModifiedTime = fi.LastWriteTime });
                res.FileCount++;
                res.TotalSize += fi.Length;
            }
            catch { }
        }
    }

    private static bool IsWhitelisted(string path, ScanContext ctx)
    {
        foreach (var w in ctx.Whitelist)
            if (!string.IsNullOrWhiteSpace(w) && PathUtil.IsUnder(path, w)) return true;
        return false;
    }

    protected static void AddWholeDirItem(RuleResult res, string dir)
    {
        try
        {
            if (!Directory.Exists(dir)) return;
            var size = FileUtil.DirSize(dir);
            if (size == 0) return;
            res.Items.Add(new CleanItem { Path = dir, Size = size, IsDirectory = true, ModifiedTime = SafeDirTime(dir) });
            res.FileCount++;
            res.TotalSize += size;
        }
        catch { }
    }

    private static DateTime SafeDirTime(string d)
    {
        try { return Directory.GetLastWriteTime(d); } catch { return DateTime.Now; }
    }

    private static IEnumerable<string> SafeFiles(string dir, string pattern, ScanContext ctx)
    {
        try { return Directory.GetFiles(dir, pattern, SearchOption.TopDirectoryOnly); }
        catch { return Enumerable.Empty<string>(); }
    }

    /// <summary>安全递归遍历：不进入 Reparse Point、排除目录、深度限制。</summary>
    protected static IEnumerable<string> SafeWalk(string root, ScanContext ctx, int maxDepth = 12)
    {
        var stack = new Stack<(string dir, int depth)>();
        stack.Push((root, 0));
        while (stack.Count > 0)
        {
            ctx.Ct.ThrowIfCancellationRequested();
            var (dir, depth) = stack.Pop();
            ctx.Detail?.Invoke(dir);
            string[] files = Array.Empty<string>(), dirs = Array.Empty<string>();
            try
            {
                files = Directory.GetFiles(dir);
                dirs = Directory.GetDirectories(dir);
            }
            catch { continue; }
            foreach (var f in files) yield return f;
            if (depth >= maxDepth) continue;
            foreach (var d in dirs)
            {
                try
                {
                    if (PathUtil.IsReparsePoint(d)) continue;
                    if (ctx.ExcludedPaths.Any(e => PathUtil.IsUnder(d, e))) continue;
                    stack.Push((d, depth + 1));
                }
                catch { }
            }
        }
    }

    protected static bool LikeMatch(string text, string pattern)
    {
        // 简单通配符 * 匹配（不区分大小写）
        return LikeImpl(text.ToLowerInvariant(), pattern.ToLowerInvariant());
        static bool LikeImpl(string t, string p)
        {
            int ti = 0, pi = 0, star = -1, mark = 0;
            while (ti < t.Length)
            {
                if (pi < p.Length && (p[pi] == '?' || p[pi] == t[ti])) { ti++; pi++; }
                else if (pi < p.Length && p[pi] == '*') { star = pi++; mark = ti; }
                else if (star >= 0) { pi = star + 1; ti = ++mark; }
                else return false;
            }
            while (pi < p.Length && p[pi] == '*') pi++;
            return pi == p.Length;
        }
    }

    protected static string Exp(params string[] parts)
    {
        var p = System.IO.Path.Combine(parts);
        return Environment.ExpandEnvironmentVariables(p);
    }

    protected static bool ProcRunning(params string[] names) => ProcessUtil.AnyRunning(names);
}

public static class ProcessUtil
{
    public static bool AnyRunning(params string[] names)
    {
        try
        {
            var set = names.Select(n => n.ToLowerInvariant()).ToHashSet();
            return Process.GetProcesses().Any(p =>
            {
                try { return set.Contains(p.ProcessName.ToLowerInvariant()); }
                catch { return false; }
            });
        }
        catch { return false; }
    }

    public static bool AnyRunning(out List<string> found, params string[] names)
    {
        found = new List<string>();
        try
        {
            var set = names.Select(n => n.ToLowerInvariant()).ToHashSet();
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    if (set.Contains(p.ProcessName.ToLowerInvariant()) && !found.Contains(p.ProcessName + ".exe"))
                        found.Add(p.ProcessName + ".exe");
                }
                catch { }
            }
        }
        catch { }
        return found.Count > 0;
    }
}
