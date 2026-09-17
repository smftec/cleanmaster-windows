using CleanMaster.Core.Models;
using CleanMaster.Core.Security;
using CleanMaster.Core.Store;
using CleanMaster.Core.Utils;

namespace CleanMaster.Core.Rules;

public sealed class CleanRequestRule
{
    public required IScanRule Rule { get; init; }
    public required RuleResult Result { get; init; }
    public required List<CleanItem> Selected { get; init; }
}

/// <summary>
/// 清理执行器：所有删除必须经由 SafetyGuard 校验；
/// 可恢复类项目按设置进入隔离区；全程汇报逐项结果并落盘清理历史。
/// </summary>
public sealed class CleanService
{
    /// <summary>
    /// 执行清理。返回汇总。异常仅在取消时抛出。
    /// </summary>
    public async Task<CleanSummary> CleanAsync(
        IReadOnlyList<CleanRequestRule> requests,
        CancellationToken ct,
        IProgress<CleanProgress>? progress)
    {
        var summary = new CleanSummary();
        var settings = SettingsService.Current;
        var whitelist = new HashSet<string>(settings.Whitelist, StringComparer.OrdinalIgnoreCase);
        bool useQuarantine = settings.QuarantineEnabled;

        foreach (var req in requests)
        {
            ct.ThrowIfCancellationRequested();
            summary.RequestedBytes += req.Selected.Sum(i => i.Size);

            var ctx = new CleanContext
            {
                Result = req.Result,
                Selected = req.Selected,
                UseQuarantine = useQuarantine && req.Rule.CanRestore,
                Ct = ct,
                Progress = progress,
                Whitelist = whitelist,
                Report = (item, status, note) =>
                {
                    long size = status is CleanItemStatus.Success or CleanItemStatus.Quarantined ? item.Size : 0;
                    switch (status)
                    {
                        case CleanItemStatus.Success:
                            summary.SuccessCount++;
                            summary.FreedBytes += size;
                            break;
                        case CleanItemStatus.Quarantined:
                            summary.QuarantineCount++;
                            summary.FreedBytes += size;
                            break;
                        case CleanItemStatus.SkippedLocked:
                        case CleanItemStatus.SkippedWhitelist:
                        case CleanItemStatus.NotFound:
                            summary.SkipCount++;
                            break;
                        case CleanItemStatus.Failed:
                            summary.FailCount++;
                            break;
                    }
                    lock (summary.Details)
                    {
                        summary.Details.Add(new CleanItemOutcome
                        {
                            Path = item.Path,
                            Status = status,
                            Size = item.Size,
                            Note = note,
                        });
                    }
                    progress?.Report(new CleanProgress
                    {
                        CurrentPath = item.Path,
                        RuleName = req.Rule.Name,
                        FreedBytes = summary.FreedBytes,
                    });
                },
            };
            // 允许根来自规则自身声明
            ctx.AllowedRoots.AddRange(req.Rule.AllowedRoots());

            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                await req.Rule.CleanAsync(ctx);
            }
            catch (OperationCanceledException)
            {
                summary.Cancelled = true;
                Finalize(summary);
                return summary;
            }
            catch (Exception ex)
            {
                foreach (var item in req.Selected.Where(i => summary.Details.All(d => d.Path != i.Path)))
                {
                    summary.FailCount++;
                    summary.Details.Add(new CleanItemOutcome
                    {
                        Path = item.Path, Status = CleanItemStatus.Failed, Note = ex.Message,
                    });
                }
            }
            sw.Stop();
        }
        Finalize(summary);
        return summary;
    }

    private static void Finalize(CleanSummary s)
    {
        s.Duration = DateTime.Now - s.StartedAt;
        var title = $"清理了 {SizeText.OfBytes(s.FreedBytes)}";
        var detail = $"成功 {s.SuccessCount}，进隔离区 {s.QuarantineCount}，跳过 {s.SkipCount}，失败 {s.FailCount}";
        HistoryService.Add(s.Cancelled ? "CleanCancelled" : "Clean", title, detail, s.FreedBytes);
    }
}
