using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace CleanMaster.App.Controls;

/// <summary>
/// 数字滚动动画：对 TextBlock 做 ease-out 数值过渡，每帧用 format 格式化。
/// 同一 TextBlock 重复调用会自动接续（从当前显示语义无法还原数值，故从上次目标继续）。
/// </summary>
public static class CountUp
{
    private sealed class State
    {
        public DispatcherTimer? Timer;
        public Stopwatch Watch = new();
        public double From, To;
        public Func<double, string> Format = _ => "";
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<TextBlock, State> Map = new();

    public static void Animate(TextBlock tb, double to, Func<double, string> format, TimeSpan? duration = null)
    {
        if (tb == null) return;
        var st = Map.GetValue(tb, _ => new State());
        st.Timer?.Stop();

        st.From = st.To != 0 || st.Watch.IsRunning ? st.To : 0;
        st.To = to;
        st.Format = format;
        st.Watch = Stopwatch.StartNew();
        st.Timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(1000.0 / 60) };
        var dur = (duration ?? TimeSpan.FromMilliseconds(600)).TotalMilliseconds;
        st.Timer.Tick += (s, e) =>
        {
            var t = Math.Min(1.0, st.Watch.Elapsed.TotalMilliseconds / dur);
            // ease-out cubic
            var k = 1 - Math.Pow(1 - t, 3);
            var v = st.From + (st.To - st.From) * k;
            tb.Text = format(v);
            if (t >= 1)
            {
                st.Timer?.Stop();
                st.Watch.Stop();
                tb.Text = format(st.To);
            }
        };
        st.Timer.Start();
    }
}
