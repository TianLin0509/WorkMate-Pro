using System;
using System.Collections.Generic;

namespace WorkMatePro
{
    public enum RareIdleShowType
    {
        None,
        Dance,
        HulaHoop,
        ButterflyChase,
        BubbleBlow,
        TailChase
    }

    /// <summary>
    /// 动画节律只在这里定义：常态靠连续呼吸，离散帧只负责有意义的动作。
    /// 这样增加帧数会提升动作细腻度，而不会提高动作发生频率。
    /// </summary>
    public static class AnimationPolicy
    {
        // WindowMover 在四边实测有 70~120ms 调度余量；190ms 参数确保繁忙路径仍落在 0.3 秒内。
        public const int DockTuckDurationMs = 190;
        // 精灵表 10~19 是“坐下→蜷睡→缩成团”的连续段，20~24 才是醒来探头。
        // 磁吸只取 14→19，绝不能把第 20 帧的醒脸误当边驻睡姿。
        public const int DockTuckStartFrame = 14;
        public const int DockTuckEndFrame = 19;
        public const int DockTuckFrameIntervalMs = 38;
        public const int DockTuckFrameStep = 1;

        // BongoCat 式输入镜像：离散爪子直接响应，身体保持稳定。45ms 只用于抑制系统连发风暴，
        // 正常击键不会被 180ms 的旧限频吞掉。
        public const int TypingEventIntervalMs = 45;
        public const int TypingPressHoldMs = 72;
        public const int TypingReturnToRestMs = 400;
        public const int MeetingFrameIntervalMs = 145;
        public const int CarryFrameIntervalMs = 110;
        public const int CarryHoldMs = 4200;
        public const int StretchFrameIntervalMs = 115;

        // 高频表演的 A_Start / B_Hold / C_End 分段。帧多不等于连续播放；关键姿态需要被看清，
        // 收尾也必须回到主人此刻的行为，而不是硬切 idle。
        public const int MeetingEnterEndFrame = 14;
        public const int MeetingExitStartFrame = 15;
        public const int MeetingExitEndFrame = 24;
        public const int MeetingReminderHoldMs = 900;
        public const int StretchPeakHoldMs = 420;
        public const int IdleGestureHoldMs = 220;
        public const int CarryHoldFrame = 17;
        public const int CarryReleaseStartFrame = 18;
        public const int CarryReleaseEndFrame = 24;

        public const int IdleGestureFrameIntervalMs = 175;
        public const int IdleGestureMinSeconds = 28;
        public const int IdleGestureMaxSeconds = 45;
        public const int RareIdleShowMinSeconds = 240;
        public const int RareIdleShowMaxSeconds = 420;
        public const int DanceShowDurationMs = 2800;
        public const int HulaShowDurationMs = 3300;
        public const int ButterflyShowDurationMs = 4200;
        public const int BubbleShowDurationMs = 3800;
        public const double HulaMinStrokeDip = 3.4;
        public const int OrbitGestureCooldownSeconds = 20;

        public static int NextIdleGestureDelayMs(Random random)
        {
            if (random == null) return IdleGestureMinSeconds * 1000;
            return random.Next(IdleGestureMinSeconds * 1000, IdleGestureMaxSeconds * 1000 + 1);
        }

        public static bool AllowsIdleGesture(bool reducedMotion, bool quietActive)
        {
            return !reducedMotion && !quietActive;
        }

        public static bool ShouldPlayAutomaticStretch(bool stretchEnabled, bool reducedMotion, bool quietActive)
        {
            return stretchEnabled && !reducedMotion && !quietActive;
        }

        public static bool ShouldPlayStretchFallback(bool reducedMotion, bool quietActive)
        {
            return !reducedMotion && !quietActive;
        }

        public static int NextRareIdleShowDelayMs(Random random)
        {
            if (random == null) return RareIdleShowMinSeconds * 1000;
            return random.Next(RareIdleShowMinSeconds * 1000, RareIdleShowMaxSeconds * 1000 + 1);
        }

        public static int DurationFor(RareIdleShowType show)
        {
            if (show == RareIdleShowType.HulaHoop) return HulaShowDurationMs;
            if (show == RareIdleShowType.ButterflyChase) return ButterflyShowDurationMs;
            if (show == RareIdleShowType.BubbleBlow) return BubbleShowDurationMs;
            if (show == RareIdleShowType.TailChase) return TailChaseShowDurationMs;
            return DanceShowDurationMs;
        }

        // 追尾巴转圈：0-1.5s 加速转两圈，1.5-2.2s 原地晃稳，2.2-2.6s 收势
        public const int TailChaseShowDurationMs = 2600;
        public const double TailChaseSpinSec = 1.5;
        public const double TailChaseSpinDegrees = 720;

        /// <summary>
        /// AI 生成的 25 帧并不是所有角色都共享同一条时间线。阿企在第 8 帧达到峰值，
        /// 其后是另一组动作，因此用 7→3 反向收势；小满在第 16 帧达到峰值、第 17~18 帧收势。
        /// </summary>
        public static int StretchPeakFrame(string petId)
        {
            return string.Equals(petId, "01-cat", StringComparison.OrdinalIgnoreCase) ? 16 : 8;
        }

        public static int StretchExitStartFrame(string petId)
        {
            return string.Equals(petId, "01-cat", StringComparison.OrdinalIgnoreCase) ? 17 : 7;
        }

        public static int StretchExitEndFrame(string petId)
        {
            return string.Equals(petId, "01-cat", StringComparison.OrdinalIgnoreCase) ? 18 : 3;
        }

        public static int FrameStep(int startFrame, int endFrame)
        {
            return endFrame >= startFrame ? 1 : -1;
        }

        public static int FrameCount(int startFrame, int endFrame)
        {
            return Math.Abs(endFrame - startFrame) + 1;
        }

        /// <summary>可自动播放的待机片段起点；小满 15..24 存在跨格污染或裁切，全部退出播放白名单。</summary>
        public static int[] SafeIdleGestureStarts(string petId)
        {
            if (string.Equals(petId, "03-penguin", StringComparison.OrdinalIgnoreCase))
                return new[] { 0, 5, 10 };
            return new[] { 0, 5, 10, 15, 20 };
        }

        public static bool TryGetPatClip(string petId, out int startFrame, out int endFrame)
        {
            startFrame = 0;
            endFrame = 0;
            return false;
        }

        public static bool ShouldLoop(BehaviorState state)
        {
            // 所有行为都禁止无条件循环。打字由真实键事件推进，其他动作只播放一次。
            return false;
        }
    }

    /// <summary>记住最近两次稀有表演；候选池足够时不重复，动作库扩张后仍保持新鲜感。</summary>
    public sealed class RareIdleDirector
    {
        private static readonly RareIdleShowType[] All =
        {
            RareIdleShowType.Dance,
            RareIdleShowType.HulaHoop,
            RareIdleShowType.ButterflyChase,
            RareIdleShowType.BubbleBlow
        };
        private readonly List<RareIdleShowType> recent = new List<RareIdleShowType>();

        public RareIdleShowType Next(Random random)
        {
            List<RareIdleShowType> candidates = new List<RareIdleShowType>();
            for (int i = 0; i < All.Length; i++) if (!recent.Contains(All[i])) candidates.Add(All[i]);
            if (candidates.Count == 0)
            {
                RareIdleShowType last = recent.Count == 0 ? RareIdleShowType.None : recent[recent.Count - 1];
                for (int i = 0; i < All.Length; i++) if (All[i] != last) candidates.Add(All[i]);
            }
            int index = random == null ? 0 : random.Next(candidates.Count);
            RareIdleShowType selected = candidates[index];
            recent.Add(selected);
            while (recent.Count > 2) recent.RemoveAt(0);
            return selected;
        }

        public IList<RareIdleShowType> Recent { get { return recent.AsReadOnly(); } }
    }

    /// <summary>
    /// 鼠标绕宠物一圈才算“邀玩”。只保留最近 1.8 秒的相对坐标，不读取点击内容、不落盘。
    /// 半径稳定、旋转方向一致、闭合且覆盖至少 0.8 圈，避免普通路过或左右扫动误触。
    /// </summary>
    public static class CursorOrbitPolicy
    {
        public static bool IsDeliberateOrbit(IList<double> xs, IList<double> ys, IList<double> times)
        {
            if (xs == null || ys == null || times == null || xs.Count < 12 || ys.Count != xs.Count || times.Count != xs.Count)
                return false;
            double duration = times[times.Count - 1] - times[0];
            if (duration < 0.42 || duration > 1.8) return false;

            double radiusSum = 0;
            double radiusSqSum = 0;
            for (int i = 0; i < xs.Count; i++)
            {
                double radius = Math.Sqrt(xs[i] * xs[i] + ys[i] * ys[i]);
                radiusSum += radius;
                radiusSqSum += radius * radius;
                if (i > 0 && times[i] - times[i - 1] > 0.24) return false;
            }
            double meanRadius = radiusSum / xs.Count;
            if (meanRadius < 24 || meanRadius > 220) return false;
            double radialDeviation = Math.Sqrt(Math.Max(0, radiusSqSum / xs.Count - meanRadius * meanRadius));
            if (radialDeviation > meanRadius * 0.32) return false;

            double signedAngle = 0;
            double absoluteAngle = 0;
            double path = 0;
            double previousAngle = Math.Atan2(ys[0], xs[0]);
            for (int i = 1; i < xs.Count; i++)
            {
                double angle = Math.Atan2(ys[i], xs[i]);
                double delta = angle - previousAngle;
                while (delta > Math.PI) delta -= Math.PI * 2;
                while (delta < -Math.PI) delta += Math.PI * 2;
                if (Math.Abs(delta) > 1.15) return false;
                signedAngle += delta;
                absoluteAngle += Math.Abs(delta);
                double dx = xs[i] - xs[i - 1];
                double dy = ys[i] - ys[i - 1];
                path += Math.Sqrt(dx * dx + dy * dy);
                previousAngle = angle;
            }
            double closureX = xs[xs.Count - 1] - xs[0];
            double closureY = ys[ys.Count - 1] - ys[0];
            double closure = Math.Sqrt(closureX * closureX + closureY * closureY);
            return Math.Abs(signedAngle) >= Math.PI * 1.60
                && absoluteAngle <= Math.PI * 2.65
                && Math.Abs(signedAngle) / Math.Max(0.001, absoluteAngle) >= 0.76
                && path >= meanRadius * Math.PI * 1.45
                && closure <= meanRadius * 0.82;
        }
    }

    /// <summary>
    /// 屏幕比例尺寸：旧版基准是在 912 DIP 高工作区直接使用 PetSize；v1.9 先缩为 60%，
    /// 再按当前显示器工作区高度等比换算。PetSize 仍保留为“小巧/标准/大号”偏好。
    /// </summary>
    public static class ResponsivePetSizing
    {
        public const double ReferenceWorkAreaHeightDip = 912.0;
        public const double CompactScaleRatio = 0.36;
        public const double ComfortScaleRatio = 0.60;
        public const double LargeScaleRatio = 0.85;
        public const double DefaultScaleRatio = CompactScaleRatio;

        public static double Resolve(double workAreaHeightDip, int preferredBaseSize, double scaleRatio)
        {
            double height = workAreaHeightDip > 100 ? workAreaHeightDip : ReferenceWorkAreaHeightDip;
            double baseSize = preferredBaseSize > 0 ? preferredBaseSize : 190;
            double ratio = scaleRatio >= 0.35 && scaleRatio <= 1.20 ? scaleRatio : DefaultScaleRatio;
            double resolved = baseSize * ratio * height / ReferenceWorkAreaHeightDip;
            return Math.Max(48, Math.Min(260, Math.Round(resolved, 1)));
        }

        public static double NearestPreset(double ratio)
        {
            double[] presets = { CompactScaleRatio, ComfortScaleRatio, LargeScaleRatio };
            double nearest = presets[0];
            double distance = Math.Abs(ratio - nearest);
            for (int i = 1; i < presets.Length; i++)
            {
                double candidateDistance = Math.Abs(ratio - presets[i]);
                if (candidateDistance < distance) { nearest = presets[i]; distance = candidateDistance; }
            }
            return nearest;
        }

        public static double VerticalPadding(double petSize)
        {
            return Math.Max(6, Math.Round(petSize * 14.0 / 190.0, 1));
        }
    }

    /// <summary>
    /// 识别器负责“知道主人在做什么”，视觉导演负责“何时让宠物换戏”。
    /// 正常停顿不会让 Typing/Idle 来回翻脸；会议、离开等也各自有稳定入场和退场时间。
    /// </summary>
    public sealed class VisualBehaviorDirector
    {
        private BehaviorState current = BehaviorState.Idle;
        private BehaviorState candidate = BehaviorState.Idle;
        private double candidateSince;
        private double enteredAt;
        private bool initialized;

        public BehaviorState Current { get { return current; } }
        public int SwitchCount { get; private set; }

        public BehaviorState Observe(BehaviorState desired, double nowSeconds)
        {
            if (!initialized)
            {
                initialized = true;
                enteredAt = nowSeconds;
                candidateSince = nowSeconds;
            }
            if (desired == current)
            {
                candidate = current;
                candidateSince = nowSeconds;
                return current;
            }
            if (desired != candidate)
            {
                candidate = desired;
                candidateSince = nowSeconds;
            }

            double stableFor = nowSeconds - candidateSince;
            double currentFor = nowSeconds - enteredAt;
            if (stableFor < EntryDelay(current, desired) || currentFor < MinimumDwell(current)) return current;

            current = desired;
            candidate = desired;
            enteredAt = nowSeconds;
            candidateSince = nowSeconds;
            SwitchCount++;
            return current;
        }

        public void Force(BehaviorState state, double nowSeconds)
        {
            current = state;
            candidate = state;
            enteredAt = nowSeconds;
            candidateSince = nowSeconds;
            initialized = true;
        }

        private static double MinimumDwell(BehaviorState state)
        {
            if (state == BehaviorState.Typing || state == BehaviorState.Meeting) return 4.0;
            if (state == BehaviorState.Idle) return 0.0;
            return 1.2;
        }

        private static double EntryDelay(BehaviorState from, BehaviorState to)
        {
            if (from == BehaviorState.Typing) return 4.0;   // 正常思考/换行停顿不退戏
            if (from == BehaviorState.Meeting) return 3.0;
            if (to == BehaviorState.Meeting) return 0.35;
            if (to == BehaviorState.Typing) return 0.80;
            if (to == BehaviorState.Away || to == BehaviorState.Sleepy) return 1.8;
            return 1.0;
        }
    }

    public struct AttentionVector
    {
        public double X;
        public double Y;
    }

    /// <summary>全局光标只提供方向与距离；不记录坐标，不驱动离散换帧。</summary>
    public static class CursorAttentionPolicy
    {
        public const double TimeConstantSeconds = 0.18;

        public static AttentionVector Resolve(double deltaX, double deltaY, double radius, double deadZone)
        {
            AttentionVector result = new AttentionVector();
            double distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
            radius = Math.Max(deadZone + 1, radius);
            if (distance <= deadZone || distance >= radius) return result;
            double peakDistance = deadZone + (radius - deadZone) * 0.20;
            double t = distance < peakDistance
                ? (distance - deadZone) / Math.Max(1, peakDistance - deadZone)
                : (radius - distance) / Math.Max(1, radius - peakDistance);
            t = Math.Max(0, Math.Min(1, t));
            double strength = t * t * (3 - 2 * t);
            result.X = deltaX / distance * strength;
            result.Y = deltaY / distance * strength;
            return result;
        }

        public static double Damp(double current, double target, double deltaSeconds)
        {
            double dt = Math.Max(0, Math.Min(0.3, deltaSeconds));
            double alpha = 1 - Math.Exp(-dt / TimeConstantSeconds);
            return current + (target - current) * alpha;
        }
    }
}
