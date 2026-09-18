using CleanMaster.Core.Startup;

namespace CleanMaster.App.Pages;

/// <summary>启动项界面文本转换（Core 存储中文原文，显示走翻译）。</summary>
public static class StartupTexts
{
    public static string Of(StartupSource source) => source switch
    {
        StartupSource.RegHkcuRun => Services.Loc.T("startup.src.reghkcu"),
        StartupSource.RegHklmRun => Services.Loc.T("startup.src.reghklm"),
        StartupSource.StartupFolderUser => Services.Loc.T("startup.src.folderuser"),
        StartupSource.StartupFolderCommon => Services.Loc.T("startup.src.foldercommon"),
        _ => Services.Loc.T("startup.src.task"),
    };
}

public static class StartupImpactText
{
    public static string Of(string zh) => zh switch
    {
        "高" => Services.Loc.T("startup.impact.high"),
        "中" => Services.Loc.T("startup.impact.med"),
        "低" => Services.Loc.T("startup.impact.low"),
        "未测量" => Services.Loc.T("startup.impact.unknown"),
        _ => zh,
    };
}

public static class StartupSuggestText
{
    public static string Of(string zh) => zh switch
    {
        "可考虑关闭" => Services.Loc.T("startup.suggest.off"),
        "系统组件" => Services.Loc.T("startup.suggest.system"),
        "保留" => Services.Loc.T("startup.suggest.keep"),
        _ => zh,
    };
}

/// <summary>历史活动类型徽章文本。</summary>
public static class HistoryTypeText
{
    public static string Of(string type) => type switch
    {
        "Clean" => Services.Loc.T("history.type.clean"),
        "AutoClean" => Services.Loc.T("history.type.autoclean"),
        "Restore" => Services.Loc.T("history.type.restore"),
        "Uninstall" => Services.Loc.T("history.type.uninstall"),
        "StartupToggle" => Services.Loc.T("history.type.startup"),
        "QuarantinePurge" => Services.Loc.T("history.type.quarantine"),
        "Shred" => Services.Loc.T("history.type.shred"),
        _ => Services.Loc.T("history.type.other"),
    };
}
