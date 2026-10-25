using System.IO;
using CleanMaster.Core.Models;
using CleanMaster.Core.Store;
using CleanMaster.Core.Utils;

namespace CleanMaster.Core.Rules;

/// <summary>
/// 自定义清理目录(竞品对照:CCleaner Custom Files and Folders)。
/// 用户在设置中添加的目录,按统一的扫描约束(跳过近期文件/白名单/排除)纳入清理;
/// 桌面/文档/图片/下载等用户数据目录仍被 SafetyGuard 拦截,只清缓存类目录是安全用法。
/// </summary>
public sealed class CustomFoldersRule : RuleBase
{
    public override string Id => "custom_folders";
    public override string Name => "自定义清理目录";
    public override RuleGroup Group => RuleGroup.SystemJunk;
    public override RiskLevel Risk => RiskLevel.Low;
    public override string Reason => "你手动添加、希望定期清理的目录";
    public override string Impact => "目录内文件将按扫描约束清理；可恢复类进入隔离区";

    public override IEnumerable<string> AllowedRoots()
    {
        foreach (var d in SettingsService.Current.CustomCleanDirs)
        {
            var n = PathUtil.Normalize(d);
            if (Directory.Exists(n)) yield return n;
        }
    }

    public override Task<RuleResult> ScanAsync(ScanContext ctx)
    {
        var res = new RuleResult
        {
            RuleId = Id, Name = Name, Group = Group, Risk = Risk,
            Reason = Reason, Impact = Impact, CanRestore = CanRestore,
        };
        foreach (var d in SettingsService.Current.CustomCleanDirs)
        {
            ctx.Ct.ThrowIfCancellationRequested();
            AddDirFiles(res, ctx, PathUtil.Normalize(d), "*", recursive: true);
        }
        return Task.FromResult(res);
    }
}
