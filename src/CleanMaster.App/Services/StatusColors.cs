using System.Windows;
using System.Windows.Media;

namespace CleanMaster.App.Services;

/// <summary>
/// 状态语义化配色:图形与颜色必须跟随状态(良好/注意/告警)。
/// 全应用统一阈值:≥70 良好(绿) / 45-69 注意(橙) / <45 告警(红)。
/// </summary>
public static class StatusColors
{
    public const double GoodThreshold = 70;
    public const double WarnThreshold = 45;

    /// <summary>0-100 指标值 → 资源 key(SuccessBrush/WarningBrush/DangerBrush)。值越高越好。</summary>
    public static string BrushKeyFor(double v) => v switch
    {
        >= GoodThreshold => "SuccessBrush",
        >= WarnThreshold => "WarningBrush",
        _ => "DangerBrush",
    };

    public static Brush BrushFor(FrameworkElement el, double v) =>
        (Brush)el.FindResource(BrushKeyFor(v));

    /// <summary>软底色资源 key(徽章/圆底用)。</summary>
    public static string SoftKeyFor(double v) => v switch
    {
        >= GoodThreshold => "SuccessSoftBrush",
        >= WarnThreshold => "WarningSoftBrush",
        _ => "DangerSoftBrush",
    };
}
