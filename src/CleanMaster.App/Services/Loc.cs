using System.Windows;
using CleanMaster.Core.Store;

namespace CleanMaster.App.Services;

/// <summary>
/// 轻量多语言服务：语言字典以合并 ResourceDictionary 叠加，
/// XAML 用 {DynamicResource key}、代码用 Loc.T(key) 取词。
/// 回退链：当前语言 → en-US → zh-CN（基础字典，App.xaml 常驻）。
/// </summary>
public static class Loc
{
    /// <summary>支持的语言（value = 字典文件名）。</summary>
    public static readonly (string Code, string NativeName)[] Supported =
    [
        ("zh-CN", "简体中文"),
        ("zh-TW", "繁體中文"),
        ("en-US", "English"),
        ("ja-JP", "日本語"),
        ("ko-KR", "한국어"),
        ("fr-FR", "Français"),
        ("es-ES", "Español"),
        ("de-DE", "Deutsch"),
    ];

    public static event Action? LanguageChanged;

    private static string _current = "zh-CN";

    public static string Current => _current;

    /// <summary>设置语言并应用（"auto" 按系统语言解析）。</summary>
    public static void SetLanguage(string language)
    {
        var code = language == "auto" ? DetectSystemLanguage() : language;
        if (!Supported.Any(s => s.Code == code)) code = "zh-CN";
        _current = code;

        var merged = Application.Current.Resources.MergedDictionaries;
        // 结构：[0]=主题色, [1]=控件, [2]=zh-CN 基础字典, [3]=en-US 回退, [4]=当前语言覆盖
        var zhUri = new Uri($"Assets/Langs/zh-CN.xaml", UriKind.Relative);
        var enUri = new Uri($"Assets/Langs/en-US.xaml", UriKind.Relative);

        // 移除旧的语言层（索引 2..4）
        for (int i = merged.Count - 1; i >= 2; i--)
            merged.RemoveAt(i);

        var idx = merged.Count;
        merged.Add(new ResourceDictionary { Source = zhUri });   // 底层：全量基础
        if (code != "zh-CN")
        {
            merged.Add(new ResourceDictionary { Source = enUri });   // 中层：英文回退
            var uri = new Uri($"Assets/Langs/{code}.xaml", UriKind.Relative);
            merged.Add(new ResourceDictionary { Source = uri });     // 顶层：当前语言
        }
        LanguageChanged?.Invoke();
    }

    /// <summary>应用启动时的语言（设置里保存的值，未保存过则 auto）。</summary>
    public static void ApplyFromSettings()
    {
        SetLanguage(string.IsNullOrWhiteSpace(SettingsService.Current.Language)
            ? "auto" : SettingsService.Current.Language);
    }

    public static string DetectSystemLanguage()
    {
        try
        {
            var name = System.Globalization.CultureInfo.CurrentUICulture.Name;
            if (name.StartsWith("zh")) return name.Contains("TW") || name.Contains("HK") || name.Contains("MO") ? "zh-TW" : "zh-CN";
            if (name.StartsWith("en")) return "en-US";
            if (name.StartsWith("ja")) return "ja-JP";
            if (name.StartsWith("ko")) return "ko-KR";
            if (name.StartsWith("fr")) return "fr-FR";
            if (name.StartsWith("es")) return "es-ES";
            if (name.StartsWith("de")) return "de-DE";
        }
        catch { }
        return "zh-CN";
    }

    /// <summary>代码取词；缺失时回退链由 WPF 资源查找顺序保证，最终返回 key 本身。</summary>
    public static string T(string key)
    {
        var v = Application.Current?.TryFindResource(key);
        return v as string ?? key;
    }

    /// <summary>规则元数据翻译：rule.&lt;id&gt;.field 缺失时回退 Core 中的中文原文。</summary>
    public static string RuleField(string ruleId, string field, string fallback)
    {
        var v = Application.Current?.TryFindResource($"rule.{ruleId}.{field}");
        return v as string ?? fallback;
    }

    public static string RuleName(string ruleId, string fallback) => RuleField(ruleId, "name", fallback);
    public static string RuleReason(string ruleId, string fallback) => RuleField(ruleId, "reason", fallback);
    public static string RuleImpact(string ruleId, string fallback) => RuleField(ruleId, "impact", fallback);

    /// <summary>Core 存储的中文类别名 → 本地化类别名（cat.* key）。</summary>
    private static readonly Dictionary<string, string> CategoryKeyMap = new()
    {
        ["系统"] = "cat.system", ["应用"] = "cat.apps", ["图片"] = "cat.images",
        ["视频"] = "cat.videos", ["音频"] = "cat.audio", ["文档"] = "cat.documents",
        ["压缩包"] = "cat.archives", ["其他"] = "cat.other",
    };

    public static string CategoryDisplay(string zhCategoryName)
    {
        return CategoryKeyMap.TryGetValue(zhCategoryName, out var key) ? T(key) : zhCategoryName;
    }
}
