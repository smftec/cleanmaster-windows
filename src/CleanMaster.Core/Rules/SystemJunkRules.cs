using System.IO;
using CleanMaster.Core.Models;
using CleanMaster.Core.Utils;

namespace CleanMaster.Core.Rules;

// ————————————————————————— 系统垃圾 —————————————————————————

public sealed class UserTempRule : RuleBase
{
    public override string Id => "user_temp";
    public override string Name => "用户临时文件";
    public override RuleGroup Group => RuleGroup.SystemJunk;
    public override RiskLevel Risk => RiskLevel.Safe;
    public override string Reason => "应用运行时产生的临时文件，正常关闭后即无用";
    public override string Impact => "无影响，极少数正在运行的安装程序可能需要重新开始";
    public override IEnumerable<string> AllowedRoots()
    {
        yield return PathUtil.Normalize(Exp(Path.GetTempPath()));
        yield return PathUtil.Normalize(Exp(@"%LOCALAPPDATA%\Temp"));
    }

    public override Task<RuleResult> ScanAsync(ScanContext ctx)
    {
        var res = NewResult();
        AddDirFiles(res, ctx, Exp(Path.GetTempPath()), "*", recursive: true);
        AddDirFiles(res, ctx, Exp(@"%LOCALAPPDATA%\Temp"), "*", recursive: true);
        // 顺带清理空目录项不加入，避免误删正在使用的目录结构
        return Task.FromResult(res);
    }

    private RuleResult NewResult() => new()
    {
        RuleId = Id, Name = Name, Group = Group, Risk = Risk,
        Reason = Reason, Impact = Impact, CanRestore = false,
    };
}

public sealed class WindowsTempRule : RuleBase
{
    public override string Id => "windows_temp";
    public override string Name => "系统临时文件 (Windows\\Temp)";
    public override RuleGroup Group => RuleGroup.SystemJunk;
    public override RiskLevel Risk => RiskLevel.Safe;
    public override string Reason => "系统组件与安装程序留下的临时文件";
    public override string Impact => "无影响；非管理员权限下部分文件会被跳过";
    public override IEnumerable<string> AllowedRoots()
    {
        yield return PathUtil.Normalize(Exp(@"%SystemRoot%\Temp"));
    }

    public override Task<RuleResult> ScanAsync(ScanContext ctx)
    {
        var res = new RuleResult
        {
            RuleId = Id, Name = Name, Group = Group, Risk = Risk,
            Reason = Reason, Impact = Impact, CanRestore = false,
        };
        AddDirFiles(res, ctx, Exp(@"%SystemRoot%\Temp"), "*", recursive: true);
        return Task.FromResult(res);
    }
}

public sealed class ErrorReportsRule : RuleBase
{
    public override string Id => "error_reports";
    public override string Name => "错误报告与崩溃转储";
    public override RuleGroup Group => RuleGroup.SystemJunk;
    public override RiskLevel Risk => RiskLevel.Safe;
    public override string Reason => "应用崩溃时生成的诊断报告，已失去分析价值";
    public override string Impact => "无影响，仅影响向微软反馈历史错误的能力";
    public override IEnumerable<string> AllowedRoots()
    {
        yield return PathUtil.Normalize(Exp(@"%LOCALAPPDATA%\CrashDumps"));
        yield return PathUtil.Normalize(Exp(@"%LOCALAPPDATA%\Microsoft\Windows\WER"));
        yield return PathUtil.Normalize(Exp(@"%ProgramData%\Microsoft\Windows\WER"));
        yield return PathUtil.Normalize(Exp(@"%SystemRoot%\Minidump"));
        yield return PathUtil.Normalize(Exp(@"%SystemRoot%\LiveKernelReports"));
    }

    public override Task<RuleResult> ScanAsync(ScanContext ctx)
    {
        var res = new RuleResult
        {
            RuleId = Id, Name = Name, Group = Group, Risk = Risk,
            Reason = Reason, Impact = Impact, CanRestore = false,
        };
        AddDirFiles(res, ctx, Exp(@"%LOCALAPPDATA%\CrashDumps"), "*", recursive: true);
        AddDirFiles(res, ctx, Exp(@"%LOCALAPPDATA%\Microsoft\Windows\WER\ReportArchive"), "*", recursive: true);
        AddDirFiles(res, ctx, Exp(@"%LOCALAPPDATA%\Microsoft\Windows\WER\ReportQueue"), "*", recursive: true);
        AddDirFiles(res, ctx, Exp(@"%ProgramData%\Microsoft\Windows\WER\ReportArchive"), "*", recursive: true);
        AddDirFiles(res, ctx, Exp(@"%ProgramData%\Microsoft\Windows\WER\ReportQueue"), "*", recursive: true);
        AddDirFiles(res, ctx, Exp(@"%SystemRoot%\Minidump"), "*.dmp", recursive: false);
        AddDirFiles(res, ctx, Exp(@"%SystemRoot%\LiveKernelReports"), "*", recursive: true);
        return Task.FromResult(res);
    }
}

public sealed class ThumbnailCacheRule : RuleBase
{
    public override string Id => "thumb_cache";
    public override string Name => "缩略图缓存";
    public override RuleGroup Group => RuleGroup.SystemJunk;
    public override RiskLevel Risk => RiskLevel.Low;
    public override string Reason => "资源管理器为图片/视频生成的预览缓存";
    public override string Impact => "清理后 Windows 会在需要时重新生成缩略图";
    public override IEnumerable<string> AllowedRoots()
    {
        yield return PathUtil.Normalize(Exp(@"%LOCALAPPDATA%\Microsoft\Windows\Explorer"));
    }

    public override Task<RuleResult> ScanAsync(ScanContext ctx)
    {
        var res = new RuleResult
        {
            RuleId = Id, Name = Name, Group = Group, Risk = Risk,
            Reason = Reason, Impact = Impact, CanRestore = false,
        };
        // explorer.exe 正在运行时 thumbcache 处于占用状态，扫描照常、清理时自动跳过
        if (ProcRunning("explorer"))
            res.RunningProcesses.Add("explorer.exe");
        AddDirFiles(res, ctx, Exp(@"%LOCALAPPDATA%\Microsoft\Windows\Explorer"), "thumbcache_*.db", recursive: false);
        AddDirFiles(res, ctx, Exp(@"%LOCALAPPDATA%\Microsoft\Windows\Explorer"), "iconcache_*.db", recursive: false);
        return Task.FromResult(res);
    }
}

public sealed class ShaderCacheRule : RuleBase
{
    public override string Id => "shader_cache";
    public override string Name => "DirectX / 显卡着色器缓存";
    public override RuleGroup Group => RuleGroup.SystemJunk;
    public override RiskLevel Risk => RiskLevel.Low;
    public override string Reason => "游戏与图形应用编译后的着色器缓存";
    public override string Impact => "游戏或图形应用下次首次启动时需要重新生成，可能略微变慢";
    public override IEnumerable<string> AllowedRoots()
    {
        yield return PathUtil.Normalize(Exp(@"%LOCALAPPDATA%\D3DSCache"));
        yield return PathUtil.Normalize(Exp(@"%LOCALAPPDATA%\NVIDIA\DXCache"));
        yield return PathUtil.Normalize(Exp(@"%LOCALAPPDATA%\NVIDIA\GLCache"));
        yield return PathUtil.Normalize(Exp(@"%LOCALAPPDATA%\NVIDIA\ComputeCache"));
        yield return PathUtil.Normalize(Exp(@"%LOCALAPPDATA%\AMD\DxCache"));
        yield return PathUtil.Normalize(Exp(@"%LOCALAPPDATA%\AMD\DxcCache"));
        yield return PathUtil.Normalize(Exp(@"%LOCALAPPDATA%\Intel\ShaderCache"));
    }

    public override Task<RuleResult> ScanAsync(ScanContext ctx)
    {
        var res = new RuleResult
        {
            RuleId = Id, Name = Name, Group = Group, Risk = Risk,
            Reason = Reason, Impact = Impact, CanRestore = false,
        };
        AddWholeDirItem(res, Exp(@"%LOCALAPPDATA%\D3DSCache"));
        AddWholeDirItem(res, Exp(@"%LOCALAPPDATA%\NVIDIA\DXCache"));
        AddWholeDirItem(res, Exp(@"%LOCALAPPDATA%\NVIDIA\GLCache"));
        AddWholeDirItem(res, Exp(@"%LOCALAPPDATA%\NVIDIA\ComputeCache"));
        AddWholeDirItem(res, Exp(@"%LOCALAPPDATA%\AMD\DxCache"));
        AddWholeDirItem(res, Exp(@"%LOCALAPPDATA%\AMD\DxcCache"));
        AddWholeDirItem(res, Exp(@"%LOCALAPPDATA%\Intel\ShaderCache"));
        return Task.FromResult(res);
    }
}

public sealed class WindowsLogsRule : RuleBase
{
    public override string Id => "windows_logs";
    public override string Name => "系统日志缓存";
    public override RuleGroup Group => RuleGroup.SystemJunk;
    public override RiskLevel Risk => RiskLevel.Low;
    public override string Reason => "Windows 组件产生的旧日志文件（7 天以上）";
    public override string Impact => "不影响系统运行，仅影响排障时查看历史日志";
    public override IEnumerable<string> AllowedRoots()
    {
        yield return PathUtil.Normalize(Exp(@"%SystemRoot%\Logs"));
        yield return PathUtil.Normalize(Exp(@"%ProgramData%\Microsoft\Windows\WER\Temp"));
    }

    public override Task<RuleResult> ScanAsync(ScanContext ctx)
    {
        var res = new RuleResult
        {
            RuleId = Id, Name = Name, Group = Group, Risk = Risk,
            Reason = Reason, Impact = Impact, CanRestore = false,
        };
        AddOldLogs(res, ctx, Exp(@"%SystemRoot%\Logs"), 0);
        return Task.FromResult(res);
    }

    private void AddOldLogs(RuleResult res, ScanContext ctx, string dir, int depth)
    {
        if (!Directory.Exists(dir) || depth > 4) return;
        foreach (var f in SafeFiles(dir, "*.log", ctx))
        {
            try
            {
                var fi = new FileInfo(f);
                if ((DateTime.Now - fi.LastWriteTime).TotalDays < 7) continue;
                res.Items.Add(new CleanItem { Path = f, Size = fi.Length, ModifiedTime = fi.LastWriteTime });
                res.FileCount++; res.TotalSize += fi.Length;
            }
            catch { }
        }
        foreach (var d in SafeDirs(dir))
        {
            if (PathUtil.IsReparsePoint(d)) continue;
            AddOldLogs(res, ctx, d, depth + 1);
        }
    }

    private static IEnumerable<string> SafeFiles(string dir, string pattern, ScanContext ctx)
    {
        try { return Directory.GetFiles(dir, pattern); } catch { return Enumerable.Empty<string>(); }
    }

    private static IEnumerable<string> SafeDirs(string dir)
    {
        try { return Directory.GetDirectories(dir); } catch { return Enumerable.Empty<string>(); }
    }
}

public sealed class UpdateCacheRule : RuleBase
{
    public override string Id => "update_cache";
    public override string Name => "Windows 更新下载缓存";
    public override RuleGroup Group => RuleGroup.SystemJunk;
    public override RiskLevel Risk => RiskLevel.Confirm;
    public override bool DefaultSelected => false;
    public override bool CanRestore => false;
    public override string Reason => "Windows 更新已安装后遗留的安装包下载缓存";
    public override string Impact => "更新已安装后无影响；若正在下载更新会被自动跳过，需管理员权限";
    public override IEnumerable<string> AllowedRoots()
    {
        yield return PathUtil.Normalize(Exp(@"%SystemRoot%\SoftwareDistribution\Download"));
    }

    public override Task<RuleResult> ScanAsync(ScanContext ctx)
    {
        var res = new RuleResult
        {
            RuleId = Id, Name = Name, Group = Group, Risk = Risk,
            Reason = Reason, Impact = Impact, CanRestore = false,
        };
        AddDirFiles(res, ctx, Exp(@"%SystemRoot%\SoftwareDistribution\Download"), "*", recursive: true);
        return Task.FromResult(res);
    }
}

public sealed class DeliveryOptimizationRule : RuleBase
{
    public override string Id => "delivery_opt";
    public override string Name => "传递优化缓存";
    public override RuleGroup Group => RuleGroup.SystemJunk;
    public override RiskLevel Risk => RiskLevel.Confirm;
    public override bool DefaultSelected => false;
    public override bool CanRestore => false;
    public override string Reason => "Windows 用于 P2P 分发更新的缓存（通过官方机制清理）";
    public override string Impact => "通过系统官方命令清理，不影响 Windows 更新功能；需管理员权限";
    public override IEnumerable<string> AllowedRoots() { yield break; } // 走官方 cmdlet，不走文件删除

    public override Task<RuleResult> ScanAsync(ScanContext ctx)
    {
        var res = new RuleResult
        {
            RuleId = Id, Name = Name, Group = Group, Risk = Risk,
            Reason = Reason, Impact = Impact, CanRestore = false,
        };
        res.Items.Add(new CleanItem
        {
            Path = "opt://delivery-optimization",
            Size = 0,
            Note = "通过系统官方机制清理",
        });
        res.FileCount = 1;
        return Task.FromResult(res);
    }

    public override async Task CleanAsync(CleanContext ctx)
    {
        var ok = await ElevatedService.RunTaskAsync("clear-do-cache", "{}");
        foreach (var item in ctx.Selected)
            ctx.Report(item, ok ? CleanItemStatus.Success : CleanItemStatus.Failed, ok ? "" : "需要管理员权限，请在 UAC 弹窗中允许");
    }
}

// ————————————————————————— 回收站 —————————————————————————

public sealed class RecycleBinRule : RuleBase
{
    public override string Id => "recycle_bin";
    public override string Name => "回收站";
    public override RuleGroup Group => RuleGroup.RecycleBin;
    public override RiskLevel Risk => RiskLevel.Confirm;
    public override bool DefaultSelected => false;
    public override bool CanRestore => false;
    public override string Reason => "你删除过的文件仍在回收站占着磁盘空间";
    public override string Impact => "清空后回收站中的文件将被永久删除，无法恢复";
    public override IEnumerable<string> AllowedRoots() { yield break; }

    public override Task<RuleResult> ScanAsync(ScanContext ctx)
    {
        var res = new RuleResult
        {
            RuleId = Id, Name = Name, Group = Group, Risk = Risk,
            Reason = Reason, Impact = Impact, CanRestore = false,
        };
        var (size, count) = ShellUtil.QueryRecycleBin();
        res.TotalSize = size;
        res.FileCount = (int)Math.Min(count, int.MaxValue);
        if (count > 0)
            res.Items.Add(new CleanItem { Path = "shell://RecycleBinFolder", Size = size, Note = $"{count} 个项目" });
        return Task.FromResult(res);
    }

    public override async Task CleanAsync(CleanContext ctx)
    {
        await Task.Run(() =>
        {
            var ok = ShellUtil.EmptyRecycleBin();
            foreach (var item in ctx.Selected)
                ctx.Report(item, ok ? CleanItemStatus.Success : CleanItemStatus.Failed, ok ? "" : "清空回收站失败");
        }, ctx.Ct);
    }
}
