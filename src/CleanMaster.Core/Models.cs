namespace CleanMaster.Core.Models;

public enum RiskLevel
{
    Safe = 0,     // L0 安全
    Low = 1,      // L1 低风险
    Confirm = 2,  // L2 需确认
    High = 3,     // L3 高风险
}

public enum RuleGroup
{
    SystemJunk,
    BrowserCache,
    AppCache,
    Privacy,
    RecycleBin,
}

public static class RiskLevelText
{
    public static string Of(RiskLevel r) => r switch
    {
        RiskLevel.Safe => "安全",
        RiskLevel.Low => "低风险",
        RiskLevel.Confirm => "需确认",
        _ => "高风险",
    };
}

/// <summary>扫描出的单个可清理对象（文件 / 目录 / 注册表虚拟路径）。</summary>
public sealed class CleanItem
{
    /// <summary>文件/目录绝对路径；注册表项使用 reg:// 前缀。</summary>
    public string Path { get; set; } = "";
    public long Size { get; set; }
    public DateTime ModifiedTime { get; set; }
    public bool IsDirectory { get; set; }
    public string Note { get; set; } = "";
    public RiskLevel Risk { get; set; } = RiskLevel.Safe;
}

/// <summary>一条扫描规则的扫描结果。</summary>
public sealed class RuleResult
{
    public string RuleId { get; set; } = "";
    public string Name { get; set; } = "";
    public RuleGroup Group { get; set; }
    public RiskLevel Risk { get; set; } = RiskLevel.Safe;
    public bool DefaultSelected { get; set; } = true;
    public bool CanRestore { get; set; }
    /// <summary>为什么可以清理。</summary>
    public string Reason { get; set; } = "";
    /// <summary>清理后的影响。</summary>
    public string Impact { get; set; } = "";
    public int FileCount { get; set; }
    public long TotalSize { get; set; }
    public int LockedCount { get; set; }
    public string? Error { get; set; }
    public List<CleanItem> Items { get; } = new();
    /// <summary>检测到的占用进程（浏览器正在运行等）。</summary>
    public List<string> RunningProcesses { get; } = new();
    /// <summary>浏览器需要保留用户数据的说明（隐私项用）。</summary>
    public bool SkippedByDefault { get; set; }
}

public sealed class ScanProgress
{
    public string Stage { get; set; } = "";
    public string Detail { get; set; } = "";
    public int RuleIndex { get; set; }
    public int RuleTotal { get; set; }
    public long FoundBytes { get; set; }
    public int FoundFiles { get; set; }
}

public enum CleanItemStatus
{
    Success,
    Quarantined,
    SkippedLocked,
    SkippedWhitelist,
    Failed,
    NotFound,
}

public sealed class CleanItemOutcome
{
    public string Path { get; set; } = "";
    public CleanItemStatus Status { get; set; }
    public long Size { get; set; }
    public string Note { get; set; } = "";
}

public sealed class CleanSummary
{
    public Guid TransactionId { get; } = Guid.NewGuid();
    public DateTime StartedAt { get; set; } = DateTime.Now;
    public TimeSpan Duration { get; set; }
    public long RequestedBytes { get; set; }
    public long FreedBytes { get; set; }
    public int SuccessCount { get; set; }
    public int QuarantineCount { get; set; }
    public int SkipCount { get; set; }
    public int FailCount { get; set; }
    public bool Cancelled { get; set; }
    public bool ElevatedUsed { get; set; }
    public List<CleanItemOutcome> Details { get; } = new();

    public string ResultText => Cancelled ? "已取消" : FailCount == 0 && SkipCount == 0 ? "成功" : SkipCount + FailCount > 0 && SuccessCount > 0 ? "部分成功" : FailCount > 0 && SuccessCount == 0 ? "失败" : "成功";
}

public sealed class CleanProgress
{
    public string CurrentPath { get; set; } = "";
    public string RuleName { get; set; } = "";
    public int Processed { get; set; }
    public int Total { get; set; }
    public long FreedBytes { get; set; }
}

/// <summary>首页/历史中的活动记录。</summary>
public sealed class ActivityRecord
{
    public DateTime Time { get; set; } = DateTime.Now;
    /// <summary>Clean / Restore / Uninstall / StartupToggle / AutoClean / QuarantinePurge / Shred</summary>
    public string Type { get; set; } = "";
    public string Title { get; set; } = "";
    public string Detail { get; set; } = "";
    public long Bytes { get; set; }
}
