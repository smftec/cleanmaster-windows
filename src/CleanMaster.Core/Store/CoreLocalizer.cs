namespace CleanMaster.Core.Store;

/// <summary>
/// Core 层不依赖 UI,需要产出用户可见文案(历史记录/文件名后缀)时,
/// 由 App 启动时注入本地化函数;未注入时回退默认文案。
/// </summary>
public static class CoreLocalizer
{
    /// <summary>(key, fallback) → 本地化文案。</summary>
    public static Func<string, string, string>? Translate { get; set; }

    public static string T(string key, string fallback)
        => Translate?.Invoke(key, fallback) ?? fallback;
}
