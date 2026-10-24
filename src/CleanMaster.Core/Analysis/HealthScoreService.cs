


namespace CleanMaster.Core.Analysis;

/// <summary>
/// 首页健康评分:磁盘余量 40% + 垃圾体量 35% + 高影响启动项 25%。
/// 回收站体量作为轻量扣分项(最多 10 分)。
/// </summary>
public static class HealthScoreService
{
    public sealed class Report
    {
        public int Score { get; init; }
        /// <summary>score.level.* 词条 key。</summary>
        public string LevelKey { get; init; } = "score.fair";
        /// <summary>级别色资源 key:SuccessBrush / TealBrush / WarningBrush / DangerBrush。</summary>
        public string BrushKey { get; init; } = "WarningBrush";
        public long JunkBytes { get; init; }
        public int HighImpactStartup { get; init; }
        public double DiskFreePercent { get; init; }
    }

    public static Report Compute(long junkBytes, int highImpactStartup, double diskFreePercent, long? recycleBinBytes = null)
    {
        // 磁盘:余量 ≥40% 满分,线性下降,≤5% 记 0
        var diskScore = Math.Clamp((diskFreePercent - 5) * 100.0 / 35, 0, 100);
        // 垃圾:每 GB 扣 4 分
        var junkGb = junkBytes / 1024.0 / 1024 / 1024;
        var junkScore = Math.Clamp(100 - junkGb * 4, 0, 100);
        // 启动项:每个高影响扣 12 分
        var startupScore = Math.Clamp(100 - (double)highImpactStartup * 12, 0, 100);

        double score = diskScore * 0.40 + junkScore * 0.35 + startupScore * 0.25;

        // 回收站轻量扣分:每 GB 扣 2 分,最多 10
        if (recycleBinBytes is > 0)
        {
            var binGb = recycleBinBytes.Value / 1024.0 / 1024 / 1024;
            score -= Math.Clamp(binGb * 2, 0, 10);
        }

        var final = (int)Math.Clamp(Math.Round(score), 0, 100);
        return new Report
        {
            Score = final,
            LevelKey = final switch
            {
                >= 85 => "score.excellent",
                >= 70 => "score.good",
                >= 50 => "score.fair",
                _ => "score.poor",
            },
            BrushKey = final switch
            {
                >= 85 => "SuccessBrush",
                >= 70 => "TealBrush",
                >= 50 => "WarningBrush",
                _ => "DangerBrush",
            },
            JunkBytes = junkBytes,
            HighImpactStartup = highImpactStartup,
            DiskFreePercent = diskFreePercent,
        };
    }
}
