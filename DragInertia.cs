using System;
using System.Collections.Generic;
using System.Windows;

namespace WorkMatePro
{
    public struct DragInertiaPlan
    {
        public bool Valid;
        public Point Target;
        public int DurationMs;
        public double Speed;
    }

    /// <summary>
    /// 松手后的低幅惯性：只取最后 180ms 的真实拖动速度，超过阈值才滑行；
    /// 位移封顶并钳在当前工作区，保留 Shimeji 的速度感但不把办公桌宠甩丢。
    /// </summary>
    public static class DragInertiaPolicy
    {
        public const double SampleWindowSeconds = 0.18;
        public const double MinimumSpeedDipPerSecond = 260;
        public const double MaximumTravelDip = 52;

        public static DragInertiaPlan Resolve(IList<double> x, IList<double> y, IList<double> t,
            double nowSeconds, Rect workArea, double left, double top, double width, double height, bool reducedMotion)
        {
            DragInertiaPlan plan = new DragInertiaPlan();
            if (reducedMotion || x == null || y == null || t == null || x.Count < 2
                || x.Count != y.Count || x.Count != t.Count || workArea.IsEmpty) return plan;
            int last = t.Count - 1;
            int first = last - 1;
            while (first > 0 && nowSeconds - t[first - 1] <= SampleWindowSeconds) first--;
            double dt = t[last] - t[first];
            if (dt < 0.035 || nowSeconds - t[last] > 0.12) return plan;
            double vx = (x[last] - x[first]) / dt;
            double vy = (y[last] - y[first]) / dt;
            double speed = Math.Sqrt(vx * vx + vy * vy);
            if (speed < MinimumSpeedDipPerSecond) return plan;
            double travel = Math.Min(MaximumTravelDip, 12 + (speed - MinimumSpeedDipPerSecond) * 0.035);
            double nx = vx / Math.Max(1, speed);
            double ny = vy / Math.Max(1, speed);
            double targetLeft = Math.Max(workArea.Left + 6, Math.Min(workArea.Right - width - 6, left + nx * travel));
            double targetTop = Math.Max(workArea.Top + 6, Math.Min(workArea.Bottom - height - 6, top + ny * travel));
            double actual = Math.Sqrt((targetLeft - left) * (targetLeft - left) + (targetTop - top) * (targetTop - top));
            if (actual < 7) return plan;
            plan.Valid = true;
            plan.Target = new Point(targetLeft, targetTop);
            plan.DurationMs = Math.Max(120, Math.Min(210, (int)Math.Round(120 + actual * 1.6)));
            plan.Speed = speed;
            return plan;
        }
    }
}
