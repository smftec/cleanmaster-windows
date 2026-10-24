using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace CleanMaster.App.Controls;

/// <summary>
/// 环形仪表:轨道圆 + 进度弧 + 中心内容。
/// 进度用 StreamGeometry(顶部起点、顺时针)绘制,Percent 依赖属性支持动画。
/// </summary>
public sealed class RingGauge : Grid
{
    public static readonly DependencyProperty PercentProperty = DependencyProperty.Register(
        nameof(Percent), typeof(double), typeof(RingGauge),
        new FrameworkPropertyMetadata(0.0, (d, e) => ((RingGauge)d).ApplyPercent((double)e.NewValue)));

    private readonly Ellipse _track = new();
    private readonly System.Windows.Shapes.Path _arc = new();
    // 中心宿主用 Border(布局行为确定):默认 ContentControl 模板的 ContentPresenter
    // 不会把 ContentContentAlignment 应用到内容,中心内容会落到左上角
    private readonly Border _center = new()
    {
        IsHitTestVisible = false,
        Focusable = false,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Stretch,
        Background = null,
    };

    public double Percent
    {
        get => (double)GetValue(PercentProperty);
        set => SetValue(PercentProperty, value);
    }

    public Brush TrackBrush { get => _track.Stroke; set => _track.Stroke = value; }
    public Brush ArcBrush { get => _arc.Stroke; set => _arc.Stroke = value; }
    public double StrokeW { get; private set; } = 9;

    public double StrokeThickness
    {
        get => StrokeW;
        set { StrokeW = value; _track.StrokeThickness = value; _arc.StrokeThickness = value; Recompute(); }
    }

    /// <summary>中心内容(文本等),赋值时强制居中。</summary>
    public object Center
    {
        get => _center.Child;
        set
        {
            switch (value)
            {
                case FrameworkElement fe:
                    fe.HorizontalAlignment = HorizontalAlignment.Center;
                    fe.VerticalAlignment = VerticalAlignment.Center;
                    _center.Child = fe;
                    break;
                case null:
                    _center.Child = null;
                    break;
                default:
                    _center.Child = new TextBlock
                    {
                        Text = value.ToString(),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                    };
                    break;
            }
        }
    }

    private double _size = 96;

    public RingGauge(double size = 96, double stroke = 9)
    {
        _size = size;
        Width = Height = size;
        VerticalAlignment = VerticalAlignment.Center;
        HorizontalAlignment = HorizontalAlignment.Center;

        _track.Stretch = Stretch.Fill;
        _track.StrokeThickness = stroke;
        _arc.StrokeThickness = stroke;
        _arc.StrokeStartLineCap = PenLineCap.Round;
        _arc.StrokeEndLineCap = PenLineCap.Round;

        Children.Add(_track);
        Children.Add(_arc);
        Children.Add(_center);

        TrackBrush = System.Windows.Application.Current?.TryFindResource("TrackBrush") as Brush ?? Brushes.LightGray;
        ArcBrush = System.Windows.Application.Current?.TryFindResource("PrimaryBrush") as Brush ?? Brushes.DodgerBlue;
        StrokeThickness = stroke;
        Loaded += (s, e) => Recompute();
        SizeChanged += (s, e) => Recompute();
    }

    private void Recompute()
    {
        var s = ActualWidth > 0 ? ActualWidth : _size;
        if (s <= 0) s = _size;
        _size = s;
        ApplyPercent(Percent);
    }

    private void ApplyPercent(double pct)
    {
        var s = _size;
        if (s <= 0) return;
        var p = Math.Clamp(pct, 0, 100) / 100.0;
        if (p <= 0.0001)
        {
            _arc.Data = null;
            return;
        }
        var r = s / 2 - StrokeW / 2;
        if (r <= 0) r = 1;
        var cx = s / 2;
        var cy = s / 2;
        // 顶部起点,顺时针
        var start = new Point(cx, cy - r);
        var angle = 360.0 * p;
        // 顺时针角度 → WPF 坐标(y 向下),终点 = (cx + r·sin θ, cy - r·cos θ)
        var rad = angle * Math.PI / 180.0;
        var end = new Point(cx + r * Math.Sin(rad), cy - r * Math.Cos(rad));

        var geom = new StreamGeometry();
        using (var ctx = geom.Open())
        {
            ctx.BeginFigure(start, isFilled: false, isClosed: false);
            ctx.ArcTo(end, new System.Windows.Size(r, r), rotationAngle: 0,
                isLargeArc: angle > 180,
                SweepDirection.Clockwise, isStroked: true, isSmoothJoin: false);
        }
        geom.Freeze();
        _arc.Data = geom;
    }

    /// <summary>带缓动的进度动画。</summary>
    public void AnimateTo(double pct, TimeSpan? duration = null)
    {
        pct = Math.Clamp(pct, 0, 100);
        var anim = new DoubleAnimation(Percent, pct, duration ?? TimeSpan.FromMilliseconds(700))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        BeginAnimation(PercentProperty, anim);
    }

    /// <summary>直接设置(无动画)。</summary>
    public void SetPercent(double pct)
    {
        BeginAnimation(PercentProperty, null);
        Percent = Math.Clamp(pct, 0, 100);
    }
}
