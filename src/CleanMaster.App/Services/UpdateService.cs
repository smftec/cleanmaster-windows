using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using CleanMaster.Core.Store;

namespace CleanMaster.App.Services;

/// <summary>应用内更新检查：查询 GitHub Releases，静默每日一次，支持手动触发。</summary>
public static class UpdateService
{
    private const string Repo = "smftec/cleanmaster-windows";
    private static readonly HttpClient Http = new HttpClient();

    private static string StateFile => Path.Combine(SettingsService.DataDir, "update.json");

    public sealed class UpdateState
    {
        public DateTime LastCheck { get; set; }
        public string? LatestVersion { get; set; }
        public string? DownloadUrl { get; set; }
        public string? LastError { get; set; }
    }

    public sealed class UpdateInfo
    {
        public bool HasUpdate { get; init; }
        public string Current { get; init; } = "";
        public string Latest { get; init; } = "";
        public string DownloadUrl { get; init; } = "";
        public string Error { get; init; } = "";
    }

    public static string CurrentVersion =>
        Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion.Split('+')[0] ?? "1.0.0";

    private static UpdateState LoadState()
    {
        try
        {
            if (File.Exists(StateFile))
                return JsonSerializer.Deserialize<UpdateState>(File.ReadAllText(StateFile)) ?? new();
        }
        catch { }
        return new UpdateState();
    }

    private static void SaveState(UpdateState s)
    {
        try
        {
            Directory.CreateDirectory(SettingsService.DataDir);
            File.WriteAllText(StateFile, JsonSerializer.Serialize(s,
                new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    /// <summary>距上次检查超过 24 小时才发起网络请求。</summary>
    public static bool ShouldAutoCheck()
    {
        var st = LoadState();
        return DateTime.Now - st.LastCheck >= TimeSpan.FromHours(24);
    }

    /// <summary>查询 GitHub 最新版本。失败时返回带 Error 的结果（不抛异常）。</summary>
    public static async Task<UpdateInfo> CheckAsync()
    {
        var cur = CurrentVersion;
        try
        {
            var req = new HttpRequestMessage(HttpMethod.Get,
                $"https://api.github.com/repos/{Repo}/releases/latest");
            req.Headers.UserAgent.ParseAdd("CleanMaster/" + cur);
            using var resp = await Http.SendAsync(req);
            resp.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var root = doc.RootElement;
            var tag = root.TryGetProperty("tag_name", out var t) && t.ValueKind == JsonValueKind.String
                ? t.GetString()!.TrimStart('v', 'V') : "";
            var url = root.TryGetProperty("html_url", out var u) && u.ValueKind == JsonValueKind.String
                ? u.GetString()! : $"https://github.com/{Repo}/releases/latest";

            var state = new UpdateState { LastCheck = DateTime.Now, LatestVersion = tag, DownloadUrl = url };
            SaveState(state);

            var has = TryCompare(cur, tag) < 0;
            return new UpdateInfo { HasUpdate = has, Current = cur, Latest = tag, DownloadUrl = url };
        }
        catch (Exception ex)
        {
            SaveState(new UpdateState { LastCheck = DateTime.Now, LastError = ex.Message });
            return new UpdateInfo { Current = cur, Error = ex.Message };
        }
    }

    /// <summary>版本比较："1.2.0" vs "1.10.0" 按数字段比较；解析失败按字符串比较。</summary>
    private static int TryCompare(string a, string b)
    {
        static int[]? Parse(string s) =>
            s.Split('.', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
             .Select(p => int.TryParse(p, out var n) ? n : -1)
             .ToArray() is { Length: > 0 } arr && arr.All(n => n >= 0) ? arr : null;
        var pa = Parse(a); var pb = Parse(b);
        if (pa == null || pb == null) return string.CompareOrdinal(a, b);
        int n = Math.Max(pa.Length, pb.Length);
        for (int i = 0; i < n; i++)
        {
            int va = i < pa.Length ? pa[i] : 0;
            int vb = i < pb.Length ? pb[i] : 0;
            if (va != vb) return va.CompareTo(vb);
        }
        return 0;
    }
}
