using System;
using System.Collections.Generic;

namespace WorkMatePro
{
    /// <summary>桌宠可表演的行为状态。Happy 由 Celebrate 覆盖通道负责，不在引擎内。</summary>
    public enum BehaviorState
    {
        Idle,
        Typing,
        Reading,
        Watching,
        Meeting,
        Thinking,
        Sleepy,
        Away
    }

    public sealed class EngineOutput
    {
        public BehaviorState State;
        public double Confidence;
        public double Arousal;
        public double Valence;
        public bool Changed;
        public bool FlowActive;
        public double FlowSeconds;
        public System.Collections.Generic.List<string> Events;
    }

    /// <summary>
    /// 状态引擎：双通道架构。
    /// 通道一：arousal/valence 连续情绪参数，永远在线，估错也只是“略快略慢”；
    /// 通道二：高置信事件才触发表演（优先级 + 滞回 + 最小驻留），低置信回退通道一。
    /// 纯逻辑、无 UI 依赖，SelfTest 直接喂合成样本做表驱动测试。
    /// </summary>
    public sealed class StateEngine
    {
        // ---- 阈值集中一处，便于校准（HUD 实时可见，右键“刚才演错了”辅助调参）----
        public double TypingEnterKeysPerMin = 90;   // 8 秒约 12 次击键，真打字
        public double TypingExitKeysPerMin = 40;    // 退出阈值更低：滞回
        public double ReadingEnterWheelPerMin = 30; // 10 秒 5 次滚轮
        public double WatchingMaxKeyPerMin = 10;
        public double WatchingMaxClickPerMin = 6;
        public int WatchingEnterIdleSec = 10;
        public int ThinkingEnterIdleSec = 20;
        public int SleepyEnterIdleSec = 180;
        public int AwayEnterIdleSec = 600;
        public int MinDwellTicks = 6;               // 最小驻留 3 秒（500ms/tick）
        public bool LongSittingEnabled = true;
        public int LongSittingThresholdSec = 60 * 60; // 默认一小时；运行时由用户设置覆盖
        public int BreakRewardIdleSec = 180;        // 提醒后真去休息的认定
        public int TickIntervalMs = 500;
        public int FlowThresholdSec = 30 * 60;      // 心流进入阈值（连续打字）
        public int FlowGraceSec = 90;               // 心流退出宽限（停顿不立刻掉出）

        private readonly Dictionary<BehaviorState, int> enterDwell = new Dictionary<BehaviorState, int>
        {
            { BehaviorState.Meeting, 4 },   // 2 秒确认，宁可慢一拍
            { BehaviorState.Typing, 3 },
            { BehaviorState.Reading, 6 },
            { BehaviorState.Watching, 8 },
            { BehaviorState.Thinking, 4 },
            { BehaviorState.Sleepy, 4 },
            { BehaviorState.Away, 8 },
            { BehaviorState.Idle, 4 }
        };

        private BehaviorState current = BehaviorState.Idle;
        private BehaviorState candidate = BehaviorState.Idle;
        private int candidateTicks;
        private int dwellTicks;
        private double arousal = 0.4;
        private double valence = 0.55;
        private DateTime positiveUntil = DateTime.MinValue;

        // 久坐追踪
        private double continuousActiveSec;
        private bool longSittingFired;
        private DateTime breakOfferUntil = DateTime.MinValue;

        // 心流追踪：连续打字累计，停顿宽限内不清零
        private double continuousTypingSec;
        private double flowGraceSec;
        private bool flowActive;

        public BehaviorState Current { get { return current; } }
        public double ContinuousActiveSec { get { return continuousActiveSec; } }
        public bool FlowActive { get { return flowActive; } }

        /// <summary>完成任务/编译成功等正反馈，30 分钟内 valence 基线抬升。</summary>
        public void NotifyPositive()
        {
            positiveUntil = DateTime.Now.AddMinutes(30);
        }

        public EngineOutput Tick(SignalSample s)
        {
            BehaviorState desired = Decide(s);

            if (desired != candidate)
            {
                candidate = desired;
                candidateTicks = 0;
            }
            candidateTicks++;
            dwellTicks++; // 在当前状态里又度过了一跳；切换时归零

            bool changed = false;
            if (desired != current)
            {
                int need = enterDwell[desired];
                // 会议可抢占别人；别人不能抢占会议（开会中途喘口气不该立刻被换掉）
                bool canLeave = dwellTicks >= MinDwellTicks || desired == BehaviorState.Meeting || current == BehaviorState.Meeting;
                if (candidateTicks >= need && canLeave)
                {
                    current = desired;
                    dwellTicks = 0;
                    changed = true;
                }
            }

            // 通道一：连续情绪参数（永不声明状态，所以永远不会“演错”）
            double instant = Math.Min(1.0, (s.KeyPerMin + s.WheelPerMin * 2 + s.ClickPerMin * 3) / 160.0);
            double arousalTarget = s.IdleSeconds >= 300 ? 0.05 : 0.25 + 0.75 * instant;
            arousal += (arousalTarget - arousal) * 0.15;

            double valenceTarget = 0.55 + RhythmBaseline.ValenceBoost(s.Now);
            if (DateTime.Now < positiveUntil) valenceTarget += 0.15;
            valenceTarget = Math.Max(0.05, Math.Min(1.0, valenceTarget));
            valence += (valenceTarget - valence) * 0.05;

            // 久坐追踪：连续有意输入累计；真去休息则发休息奖励
            List<string> events = null;
            if (!LongSittingEnabled)
            {
                continuousActiveSec = 0;
                longSittingFired = false;
                breakOfferUntil = DateTime.MinValue;
            }
            else
            {
                if (s.IdleIntentionalSeconds >= 300)
                {
                    continuousActiveSec = 0;
                    longSittingFired = false;
                }
                else
                {
                    continuousActiveSec += TickIntervalMs / 1000.0;
                }
                if (!longSittingFired && continuousActiveSec >= LongSittingThresholdSec)
                {
                    longSittingFired = true;
                    breakOfferUntil = DateTime.Now.AddMinutes(10);
                    events = new List<string> { "long-sitting" };
                }
                else if (longSittingFired && DateTime.Now < breakOfferUntil && s.IdleSeconds >= BreakRewardIdleSec)
                {
                    longSittingFired = false;
                    breakOfferUntil = DateTime.MinValue;
                    continuousActiveSec = 0;
                    events = new List<string> { "break-reward" };
                }
            }

            // 心流追踪：连续打字累计；非打字状态有宽限，宽限耗尽才退出
            if (desired == BehaviorState.Typing)
            {
                continuousTypingSec += TickIntervalMs / 1000.0;
                flowGraceSec = 0;
            }
            else
            {
                flowGraceSec += TickIntervalMs / 1000.0;
                if (flowGraceSec >= FlowGraceSec)
                {
                    continuousTypingSec = 0;
                    flowGraceSec = 0;
                }
            }
            flowActive = continuousTypingSec >= FlowThresholdSec;

            EngineOutput output = new EngineOutput();
            output.State = current;
            output.Changed = changed;
            output.Arousal = arousal;
            output.Valence = valence;
            output.Confidence = ConfidenceFor(current);
            output.FlowActive = flowActive;
            output.FlowSeconds = continuousTypingSec;
            output.Events = events;
            return output;
        }

        private BehaviorState Decide(SignalSample s)
        {
            // 优先级：高置信、免内容信号在前
            if (s.MicInUse || s.CameraInUse) return BehaviorState.Meeting;
            if (s.KeyPerMin >= TypingEnterKeysPerMin) return BehaviorState.Typing;
            if (current == BehaviorState.Typing && s.KeyPerMin >= TypingExitKeysPerMin) return BehaviorState.Typing;
            if (s.AudioActive && s.IdleIntentionalSeconds >= WatchingEnterIdleSec
                && s.KeyPerMin < WatchingMaxKeyPerMin && s.ClickPerMin < WatchingMaxClickPerMin)
                return BehaviorState.Watching;
            if (s.WheelPerMin >= ReadingEnterWheelPerMin && s.KeyPerMin < 20) return BehaviorState.Reading;
            if (s.IdleSeconds >= AwayEnterIdleSec) return BehaviorState.Away;
            if (s.IdleSeconds >= SleepyEnterIdleSec) return BehaviorState.Sleepy;
            if (s.ForegroundCategory == "开发工具" && s.IdleIntentionalSeconds >= ThinkingEnterIdleSec) return BehaviorState.Thinking;
            return BehaviorState.Idle;
        }

        private static double ConfidenceFor(BehaviorState state)
        {
            switch (state)
            {
                case BehaviorState.Meeting: return 0.95; // 系统级布尔信号
                case BehaviorState.Typing: return 0.9;
                case BehaviorState.Sleepy: return 0.9;
                case BehaviorState.Away: return 0.9;
                case BehaviorState.Watching: return 0.6; // 诚实标注：无标题无内容，只能估
                case BehaviorState.Reading: return 0.55;
                case BehaviorState.Thinking: return 0.5;
                default: return 0.8;
            }
        }
    }

    /// <summary>星期/时辰节奏：参数通道建成后的“免费红利”，纯时间函数、零新美术。</summary>
    public static class RhythmBaseline
    {
        public static double ValenceBoost(DateTime now)
        {
            double boost = 0;
            if (now.DayOfWeek == DayOfWeek.Friday && now.Hour >= 17) boost += 0.15; // 周五傍晚的愉悦
            if (now.DayOfWeek == DayOfWeek.Monday && now.Hour < 11) boost -= 0.10;  // 周一早上的困顿
            return boost;
        }

        /// <summary>每天最多提示一次的时机关怀：12 点干饭、18 点下班问候、22 点该回家了。</summary>
        public static string OccasionToast(DateTime now, string lastLunchDate, string lastHomeDate, out string kind, string lastOffworkDate = null)
        {
            kind = null;
            string today = now.ToString("yyyy-MM-dd");
            if (now.Hour == 12 && now.Minute < 10 && lastLunchDate != today)
            {
                kind = "lunch";
                return "到点了，先去干饭";
            }
            if (now.Hour == 18 && now.Minute < 20 && lastOffworkDate != today)
            {
                kind = "offwork";
                return "下班啦，今天辛苦了";
            }
            if (now.Hour >= 22 && lastHomeDate != today)
            {
                kind = "home";
                return "很晚了，该回家了";
            }
            return null;
        }
    }

    public enum ReleaseAction
    {
        None,
        Snap,
        Fin,
        Retreat
    }

    /// <summary>摸头检测：在宠物身上来回慢速抚摸（纯函数，可单测）。</summary>
    public static class PatLogic
    {
        public const int MinReversals = 2;
        public const double MinPathDip = 48;
        public const double MaxSpeedDipPerMs = 1.4;

        public static bool IsPat(List<double> xs, List<double> ts)
        {
            if (xs == null || ts == null || xs.Count < 5 || xs.Count != ts.Count) return false;
            int reversals = 0;
            int lastSign = 0;
            double path = 0;
            for (int i = 1; i < xs.Count; i++)
            {
                double dx = xs[i] - xs[i - 1];
                path += Math.Abs(dx);
                if (Math.Abs(dx) < 6) continue;
                int sign = dx > 0 ? 1 : -1;
                if (lastSign != 0 && sign != lastSign) reversals++;
                lastSign = sign;
            }
            double span = ts[ts.Count - 1] - ts[0];
            if (span < 0.35 || span > 2.5) return false;
            double speed = path / (span * 1000.0);
            return reversals >= MinReversals && path >= MinPathDip && speed < MaxSpeedDipPerMs;
        }
    }

    /// <summary>拖拽释放判定：纯几何/运动学，无 UI 依赖，可表驱动测试。</summary>
    public static class EdgeLogic
    {
        // 距离由 PetWindow 按 PNG alpha 主体轮廓计算；只有主体真正触边（允许 2 DIP 抗锯齿误差）才吸附。
        public const double SnapDistanceDip = 2;
        public const double TossDistanceDip = 2;
        public const double TossVelocityDipPerMs = 0.45; // 约 450 DIP/秒的高速甩
        public const int ShakeReversalThreshold = 3;
        public const double ShakeMinDeltaDip = 18;

        public static ReleaseAction ClassifyRelease(double distToEdgeDip, double velocityTowardEdgeDipPerMs, int shakeReversals, bool corridor)
        {
            if (corridor) return ReleaseAction.None; // 多屏走廊边缘不吸附
            if (shakeReversals >= ShakeReversalThreshold) return ReleaseAction.Retreat;
            if (distToEdgeDip <= TossDistanceDip && velocityTowardEdgeDipPerMs >= TossVelocityDipPerMs) return ReleaseAction.Fin;
            if (distToEdgeDip <= SnapDistanceDip) return ReleaseAction.Snap;
            return ReleaseAction.None;
        }

        /// <summary>统计拖动轨迹中的快速左右甩动次数（方向反转计数）。</summary>
        public static int CountShakeReversals(List<double> xSeries, double minDeltaDip)
        {
            int reversals = 0;
            int lastSign = 0;
            for (int i = 1; i < xSeries.Count; i++)
            {
                double dx = xSeries[i] - xSeries[i - 1];
                if (Math.Abs(dx) < minDeltaDip) continue;
                int sign = dx > 0 ? 1 : -1;
                if (lastSign != 0 && sign != lastSign) reversals++;
                lastSign = sign;
            }
            return reversals;
        }

        /// <summary>拎起摇晃晕眩判定：最近 1 秒内甩动反转 ≥3 次（拎着晃三下就晕）。</summary>
        public static bool ShouldDizzy(List<double> xs, List<double> ts, double nowT)
        {
            List<double> recent = new List<double>();
            for (int i = 0; i < xs.Count; i++) { if (nowT - ts[i] <= 1.0) recent.Add(xs[i]); }
            return CountShakeReversals(recent, ShakeMinDeltaDip) >= 3;
        }

        /// <summary>用最近约 150ms 的轨迹估计释放速度（DIP/ms，向边缘为正）。</summary>
        public static double EstimateVelocity(List<double> xSeries, List<double> tSeries, double nowT, bool towardRight)
        {
            if (xSeries.Count < 2) return 0;
            int start = xSeries.Count - 1;
            for (int i = xSeries.Count - 1; i >= 0; i--)
            {
                if (nowT - tSeries[i] > 0.15) { start = i; break; }
                if (i == 0) start = 0;
            }
            double dt = nowT - tSeries[start];
            if (dt <= 0.005) return 0;
            double dx = xSeries[xSeries.Count - 1] - xSeries[start];
            double v = dx / (dt * 1000.0);
            return towardRight ? v : -v;
        }
    }

    /// <summary>边缘驻留方向（v1.5 四边缩入）。</summary>
    public enum DockEdge
    {
        None,
        Left,
        Right,
        Top,
        Bottom
    }

    /// <summary>四边缩入/探头的目标几何：纯函数、无 UI 依赖，表驱动可测。</summary>
    public static class DockGeometry
    {
        public const double SliverVisibleDip = 36;
        public const double PeekFraction = 0.65;
        public const double SpriteArtifactInsetRatio = 0.09;

        public static bool NeedsSpriteArtifactClip(DockEdge edge)
        {
            return edge == DockEdge.Left || edge == DockEdge.Right;
        }

        public static double SliverVisibleFor(double w, double h)
        {
            return Math.Max(12, Math.Min(44, Math.Round(Math.Min(w, h) * 0.19, 1)));
        }

        /// <summary>只接受四个稳定值，损坏或未来版本的值安全退化为不驻边。</summary>
        public static DockEdge ParsePersistedEdge(string value)
        {
            switch ((value ?? "").Trim().ToLowerInvariant())
            {
                case "left": return DockEdge.Left;
                case "right": return DockEdge.Right;
                case "top": return DockEdge.Top;
                case "bottom": return DockEdge.Bottom;
                default: return DockEdge.None;
            }
        }

        public static string PersistedEdge(DockEdge edge)
        {
            return edge == DockEdge.Left || edge == DockEdge.Right || edge == DockEdge.Top || edge == DockEdge.Bottom
                ? edge.ToString()
                : "";
        }

        /// <summary>v1.5 只保存贴边坐标、未保存方向；升级时用严格 8 DIP 容差恢复一次。</summary>
        public static DockEdge InferFlushEdge(System.Windows.Rect area, double left, double top, double w, double h, double toleranceDip)
        {
            DockEdge edge = DockEdge.None;
            double best = Math.Max(0, toleranceDip) + 0.0001;
            double distance = Math.Abs(left - (area.Left + 4));
            if (distance < best) { best = distance; edge = DockEdge.Left; }
            distance = Math.Abs(left - (area.Right - w - 4));
            if (distance < best) { best = distance; edge = DockEdge.Right; }
            distance = Math.Abs(top - (area.Top + 4));
            if (distance < best) { best = distance; edge = DockEdge.Top; }
            distance = Math.Abs(top - (area.Bottom - h - 4));
            if (distance < best) edge = DockEdge.Bottom;
            return edge;
        }

        /// <summary>贴边（磁吸）目标：边缘内侧留 4px。</summary>
        public static System.Windows.Point FlushTarget(DockEdge edge, System.Windows.Rect area, double w, double h, double top)
        {
            switch (edge)
            {
                case DockEdge.Right: return new System.Windows.Point(area.Right - w - 4, top);
                case DockEdge.Left: return new System.Windows.Point(area.Left + 4, top);
                case DockEdge.Top: return new System.Windows.Point(area.Left, area.Top + 4);
                default: return new System.Windows.Point(area.Left, area.Bottom - h - 4);
            }
        }

        /// <summary>缩入（边驻）目标：只露 visibleDip。</summary>
        public static System.Windows.Point SliverTarget(DockEdge edge, System.Windows.Rect area, double w, double h, double top)
        {
            double v = SliverVisibleFor(w, h);
            switch (edge)
            {
                case DockEdge.Right: return new System.Windows.Point(area.Right - v, top);
                case DockEdge.Left: return new System.Windows.Point(area.Left + v - w, top);
                case DockEdge.Top: return new System.Windows.Point(area.Left, area.Top + v - h);
                default: return new System.Windows.Point(area.Left, area.Bottom - v);
            }
        }

        /// <summary>探头目标：滑出 fraction 比例的身体。</summary>
        public static System.Windows.Point PeekTarget(DockEdge edge, System.Windows.Rect area, double w, double h, double top)
        {
            double f = PeekFraction;
            switch (edge)
            {
                case DockEdge.Right: return new System.Windows.Point(area.Right - w * f, top);
                case DockEdge.Left: return new System.Windows.Point(area.Left + w * f - w, top);
                case DockEdge.Top: return new System.Windows.Point(area.Left, area.Top + h * f - h);
                default: return new System.Windows.Point(area.Left, area.Bottom - h * f);
            }
        }
    }

    /// <summary>
    /// 打字节奏同步（v1.5）：真实键盘时间戳驱动，限频 + 左右交替。
    /// 奇偶键交替换向，剪影层面即"双爪交替落键"。
    /// </summary>
    public sealed class TypingRhythm
    {
        private double lastAccept = double.MinValue;
        private int sign = 1;
        public double MinIntervalSec = 0.06;

        /// <summary>接受一个键事件则返回 true（限频内丢弃连发）。</summary>
        public bool Accept(double nowSec)
        {
            if (nowSec - lastAccept < MinIntervalSec) return false;
            lastAccept = nowSec;
            sign = -sign;
            return true;
        }

        public int Sign { get { return sign; } }
    }

    public enum TypingPawPose
    {
        Rest = 0,
        LeftSoft = 1,
        LeftDown = 2,
        RightSoft = 3,
        RightDown = 4
    }

    /// <summary>
    /// 打字视觉导演：一次真实击键只驱动一只爪子；按下后短驻留，随后回弹，
    /// 400ms 没有新键才回到双爪静止。它不读取键码，只消费已有的匿名击键事件。
    /// </summary>
    public sealed class TypingPawDirector
    {
        private double lastAccepted = double.MinValue;
        private bool nextLeft = true;
        private TypingPawPose downPose = TypingPawPose.Rest;

        public double MinIntervalSec = AnimationPolicy.TypingEventIntervalMs / 1000.0;

        public bool TryPress(double nowSeconds, out TypingPawPose pose)
        {
            if (nowSeconds - lastAccepted < MinIntervalSec)
            {
                pose = PoseAt(nowSeconds);
                return false;
            }
            lastAccepted = nowSeconds;
            downPose = nextLeft ? TypingPawPose.LeftDown : TypingPawPose.RightDown;
            nextLeft = !nextLeft;
            pose = downPose;
            return true;
        }

        public TypingPawPose PoseAt(double nowSeconds)
        {
            double elapsedMs = (nowSeconds - lastAccepted) * 1000.0;
            if (elapsedMs < 0 || elapsedMs >= AnimationPolicy.TypingReturnToRestMs) return TypingPawPose.Rest;
            if (elapsedMs < AnimationPolicy.TypingPressHoldMs) return downPose;
            return downPose == TypingPawPose.LeftDown ? TypingPawPose.LeftSoft : TypingPawPose.RightSoft;
        }

        public void Reset()
        {
            lastAccepted = double.MinValue;
            downPose = TypingPawPose.Rest;
        }
    }
}
