namespace CleanMaster.App.Windows;

/// <summary>Core 残留描述中文原文 → 本地化文本。</summary>
public static class LeftoverDesc
{
    public static string Of(string zh) => zh switch
    {
        "数据目录" => Services.Loc.T("leftover.datadir"),
        "安装目录" => Services.Loc.T("leftover.installdir"),
        _ => zh,
    };
}
