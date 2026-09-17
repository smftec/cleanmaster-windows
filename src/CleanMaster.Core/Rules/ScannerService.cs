using CleanMaster.Core.Models;
using CleanMaster.Core.Store;
using CleanMaster.Core.Utils;

namespace CleanMaster.Core.Rules;

public sealed class RuleCatalog
{
    public List<IScanRule> Rules { get; }

    public RuleCatalog() => Rules = BuildBuiltinRules();

    public static List<IScanRule> BuildBuiltinRules()
    {
        var list = new List<IScanRule>
        {
            new UserTempRule(),
            new WindowsTempRule(),
            new ErrorReportsRule(),
            new ThumbnailCacheRule(),
            new ShaderCacheRule(),
            new WindowsLogsRule(),
            new UpdateCacheRule(),
            new DeliveryOptimizationRule(),
            new RecycleBinRule(),
            new BrowserCacheRule("edge_cache", "Microsoft Edge",
                @"%LOCALAPPDATA%\Microsoft\Edge\User Data", "msedge.exe"),
            new BrowserCacheRule("chrome_cache", "Google Chrome",
                @"%LOCALAPPDATA%\Google\Chrome\User Data", "chrome.exe"),
            new BrowserCacheRule("brave_cache", "Brave",
                @"%LOCALAPPDATA%\BraveSoftware\Brave-Browser\User Data", "brave.exe"),
            new BrowserCacheRule("opera_cache", "Opera",
                @"%LOCALAPPDATA%\Opera Software\Opera Stable", "opera.exe"),
            new FirefoxCacheRule(),
            new RecentFilesRule(),
            new JumpListRule(),
            new RunMruRule(),
            new SearchHistoryRule(),
            new DnsCacheRule(),
            new ClipboardRule(),
            new BrowserPrivacyRule("edge_history", "Microsoft Edge",
                @"%LOCALAPPDATA%\Microsoft\Edge\User Data", ["msedge.exe"], cookies: false),
            new BrowserPrivacyRule("chrome_history", "Google Chrome",
                @"%LOCALAPPDATA%\Google\Chrome\User Data", ["chrome.exe"], cookies: false),
            new BrowserPrivacyRule("edge_cookies", "Microsoft Edge",
                @"%LOCALAPPDATA%\Microsoft\Edge\User Data", ["msedge.exe"], cookies: true),
            new BrowserPrivacyRule("chrome_cookies", "Google Chrome",
                @"%LOCALAPPDATA%\Google\Chrome\User Data", ["chrome.exe"], cookies: true),
        };
        list.AddRange(AppCacheCatalog.Build());
        return list;
    }
}

/// <summary>扫描编排：按组顺序执行所有规则，汇报进度。</summary>
public sealed class ScannerService
{
    private readonly RuleCatalog _catalog;

    public ScannerService(RuleCatalog? catalog = null)
        => _catalog = catalog ?? new RuleCatalog();

    public IReadOnlyList<IScanRule> Rules => _catalog.Rules;

    public ScanContext BuildContext(CancellationToken ct, Action<string>? detail = null)
    {
        var s = SettingsService.Current;
        return new ScanContext
        {
            Ct = ct,
            Detail = detail,
            SkipRecentMinutes = s.SkipRecentMinutes,
            Whitelist = new HashSet<string>(s.Whitelist, StringComparer.OrdinalIgnoreCase),
            ExcludedPaths = s.ExcludedPaths,
        };
    }

    /// <summary>扫描指定规则集合（null = 全部）。</summary>
    public async Task<List<RuleResult>> ScanAsync(
        IEnumerable<IScanRule>? rules,
        ScanContext ctx,
        IProgress<ScanProgress>? progress)
    {
        var targets = (rules ?? _catalog.Rules).ToList();
        var results = new List<RuleResult>(targets.Count);
        int index = 0;
        long foundBytes = 0;
        int foundFiles = 0;
        foreach (var rule in targets)
        {
            ctx.Ct.ThrowIfCancellationRequested();
            progress?.Report(new ScanProgress
            {
                Stage = rule.Name,
                Detail = "",
                RuleIndex = ++index,
                RuleTotal = targets.Count,
                FoundBytes = foundBytes,
                FoundFiles = foundFiles,
            });
            RuleResult res;
            try
            {
                res = await rule.ScanAsync(ctx);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                res = new RuleResult
                {
                    RuleId = rule.Id, Name = rule.Name, Group = rule.Group, Risk = rule.Risk,
                    Reason = rule.Reason, Impact = rule.Impact, Error = ex.Message,
                };
            }
            foundBytes += res.TotalSize;
            foundFiles += res.FileCount;
            results.Add(res);
            progress?.Report(new ScanProgress
            {
                Stage = rule.Name,
                Detail = "完成",
                RuleIndex = index,
                RuleTotal = targets.Count,
                FoundBytes = foundBytes,
                FoundFiles = foundFiles,
            });
        }
        return results;
    }

    /// <summary>智能扫描：垃圾/浏览器/应用缓存 + 回收站 + 隐私提示（不含隐私默认项外的扫描细节）。</summary>
    public Task<List<RuleResult>> SmartScanAsync(ScanContext ctx, IProgress<ScanProgress>? progress)
    {
        var rules = _catalog.Rules.Where(r =>
            r.Group is RuleGroup.SystemJunk or RuleGroup.BrowserCache or RuleGroup.AppCache
            or RuleGroup.RecycleBin).ToList();
        return ScanAsync(rules, ctx, progress);
    }
}

public static class RuleResultExtensions
{
    public static bool IsMeaningful(this RuleResult r) => r.Error == null && r.TotalSize > 0;
}
