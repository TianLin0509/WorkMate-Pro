using System;
using System.Windows;

namespace WorkMatePro
{
    public static class PetInteractionPolicy
    {
        // 用户已明确回退“叼取中转站”；保留底层兼容代码与旧数据，但不再暴露拖放入口。
        public static readonly bool FileCarryEnabled = false;

        public static bool HasActiveCarry(int itemCount)
        {
            return FileCarryEnabled && itemCount > 0;
        }
    }

    /// <summary>小尺寸桌宠的悬停胶囊只回答“它现在怎样”，避免长名称、等级和任务串成一行后被裁断。</summary>
    public static class PetIdentityChipPolicy
    {
        public static string CompactLabel(string petName, int level, string stateName, string anchorText,
            int carryCount, bool trackingEnabled)
        {
            if (!trackingEnabled) return "统计暂停";
            if (!string.IsNullOrWhiteSpace(stateName)) return stateName.Trim();
            if (carryCount > 0) return "叼着 " + carryCount + " 件";
            if (!string.IsNullOrWhiteSpace(anchorText)) return "在做 · " + Formatters.Truncate(anchorText.Trim(), 4);
            string name = string.IsNullOrWhiteSpace(petName) ? "伙伴" : petName.Trim().Split(' ')[0];
            return name + " · Lv." + Math.Max(1, level);
        }
    }

    public static class IdentityHoverPolicy
    {
        public const int RevealDelayMs = 220;

        public static bool AllowsReveal(bool pointerInside, bool tucked, bool retreating, bool dragging)
        {
            return pointerInside && !tucked && !retreating && !dragging;
        }
    }

    /// <summary>低打扰陪伴仪式：里程碑只在少数真正值得记住的日期出现一次。</summary>
    public static class CompanionMilestones
    {
        private static readonly int[] Days = { 7, 30, 100, 365 };

        public static string KeyFor(string firstDate, DateTime now, string lastShownKey, out int day)
        {
            day = 0;
            DateTime first;
            if (!DateTime.TryParse(firstDate, out first)) return null;
            day = Math.Max(1, (now.Date - first.Date).Days + 1);
            bool milestone = Array.IndexOf(Days, day) >= 0 || (day > 365 && day % 365 == 0);
            if (!milestone) return null;
            string key = first.ToString("yyyy-MM-dd") + ":" + day;
            return string.Equals(key, lastShownKey, StringComparison.Ordinal) ? null : key;
        }

        public static string FarewellMessage(DateTime now)
        {
            return now.Hour >= 18 || now.Hour < 6 ? "晚安，明天见，我会乖乖等你" : "先去忙吧，我在这里等你回来";
        }
    }

    public static class ShyGazePolicy
    {
        public const double DwellSeconds = 3.0;
        public const double CooldownSeconds = 60.0;

        public static bool ShouldTrigger(double distance, double faceRadius, double dwellSeconds, double sinceLastSeconds)
        {
            return distance <= Math.Max(12, faceRadius)
                && dwellSeconds >= DwellSeconds
                && sinceLastSeconds >= CooldownSeconds;
        }
    }

    public struct CursorGreetingFrame
    {
        public double Intensity;
        public double TiltDegrees;
        public double LiftDip;
    }

    /// <summary>
    /// 光标在宠物身边安静停留时的一次“我看到你了”。它不是鼠标追逐器：
    /// 只有近距离、非悬停、非繁忙场景才允许触发，并用长冷却避免成为注意力噪声。
    /// </summary>
    public static class CursorGreetingPolicy
    {
        public const double DwellSeconds = 1.15;
        public const double CooldownSeconds = 18.0;
        public const double DurationSeconds = 0.92;

        public static bool ShouldTrigger(double distanceDip, double petSizeDip, bool pointerInside,
            double dwellSeconds, double sinceLastSeconds, bool reducedMotion, bool busy)
        {
            double size = Math.Max(40, petSizeDip);
            return !pointerInside && !reducedMotion && !busy
                && distanceDip >= size * 0.42 && distanceDip <= size * 1.72
                && dwellSeconds >= DwellSeconds && sinceLastSeconds >= CooldownSeconds;
        }

        public static bool IsBusy(bool quiet, bool buildWaiting, bool focusVisual, bool exclusive,
            bool noddingOff, bool playful, bool shy, bool balloon, bool patrol)
        {
            return quiet || buildWaiting || focusVisual || exclusive || noddingOff
                || playful || shy || balloon || patrol;
        }

        public static CursorGreetingFrame Sample(double seconds, double horizontalBias)
        {
            double intensity;
            if (seconds <= 0) intensity = 0;
            else if (seconds < 0.16) intensity = Smooth(seconds / 0.16);
            else if (seconds < 0.58) intensity = 1;
            else if (seconds < DurationSeconds) intensity = 1 - Smooth((seconds - 0.58) / (DurationSeconds - 0.58));
            else intensity = 0;
            double bias = Math.Max(-1, Math.Min(1, horizontalBias));
            return new CursorGreetingFrame
            {
                Intensity = intensity,
                TiltDegrees = bias * 1.8 * intensity,
                LiftDip = -1.6 * intensity
            };
        }

        private static double Smooth(double value)
        {
            value = Math.Max(0, Math.Min(1, value));
            return value * value * (3 - 2 * value);
        }
    }

    public struct WakeStretchFrame
    {
        public double Intensity;
        public double ScaleXDelta;
        public double ScaleYDelta;
        public double LiftDip;
        public double TiltDegrees;
    }

    /// <summary>长时间离开后的单次醒来舒展；只修饰 Sleep/Away → Idle，不拦截用户继续输入。</summary>
    public static class WakeStretchPolicy
    {
        public const double MinimumAwaySeconds = 60.0;
        public const double DurationSeconds = 1.15;

        public static bool ShouldStart(BehaviorState from, BehaviorState to, double awaySeconds,
            bool reducedMotion, bool busy)
        {
            bool woke = from == BehaviorState.Away || from == BehaviorState.Sleepy;
            return woke && to == BehaviorState.Idle && awaySeconds >= MinimumAwaySeconds
                && !reducedMotion && !busy;
        }

        public static WakeStretchFrame Sample(double seconds)
        {
            double intensity;
            if (seconds <= 0) intensity = 0;
            else if (seconds < 0.20) intensity = Smooth(seconds / 0.20);
            else if (seconds < 0.55) intensity = 1;
            else if (seconds < DurationSeconds) intensity = 1 - Smooth((seconds - 0.55) / (DurationSeconds - 0.55));
            else intensity = 0;
            double progress = Math.Max(0, Math.Min(1, seconds / DurationSeconds));
            return new WakeStretchFrame
            {
                Intensity = intensity,
                ScaleXDelta = -0.008 * intensity,
                ScaleYDelta = 0.018 * intensity,
                LiftDip = -2.1 * intensity,
                TiltDegrees = Math.Sin(progress * Math.PI) * 0.65 * intensity
            };
        }

        private static double Smooth(double value)
        {
            value = Math.Max(0, Math.Min(1, value));
            return value * value * (3 - 2 * value);
        }
    }

    /// <summary>快捷气泡落位完成后，桌宠只看向真实气泡中心；多屏/边缘夹取后的最终坐标才是事实。</summary>
    public static class BubbleAttentionPolicy
    {
        public const double DurationSeconds = 0.92;

        public static double HorizontalBias(Rect petBounds, Rect bubbleBounds)
        {
            double delta = (bubbleBounds.Left + bubbleBounds.Width / 2)
                - (petBounds.Left + petBounds.Width / 2);
            if (Math.Abs(delta) < 1) return 0;
            return delta < 0 ? -1 : 1;
        }

        public static CursorGreetingFrame Sample(double seconds, double horizontalBias)
        {
            double bias = Math.Max(-1, Math.Min(1, horizontalBias));
            double intensity;
            if (seconds <= 0) intensity = 0;
            else if (seconds < 0.14) intensity = Smooth(seconds / 0.14);
            else if (seconds < 0.50) intensity = 1;
            else if (seconds < DurationSeconds) intensity = 1 - Smooth((seconds - 0.50) / (DurationSeconds - 0.50));
            else intensity = 0;
            return new CursorGreetingFrame
            {
                Intensity = intensity,
                TiltDegrees = bias * 2.1 * intensity,
                LiftDip = -0.7 * intensity
            };
        }

        private static double Smooth(double value)
        {
            value = Math.Max(0, Math.Min(1, value));
            return value * value * (3 - 2 * value);
        }
    }

    /// <summary>不到一秒的关系型微动作拥有短暂舞台；随机待机表演只延后，不取消、不叠加。</summary>
    public static class AttentionRitualPolicy
    {
        public static bool BlocksAutonomousAction(bool cursorGreeting, bool wakeStretch, bool bubbleAttention)
        {
            return cursorGreeting || wakeStretch || bubbleAttention;
        }

        public static bool ShouldYieldToUser(bool ritualActive, bool userInitiated)
        {
            return ritualActive && userInitiated;
        }
    }

    public struct PatAffectionFrame
    {
        public double Intensity;
        public double LeanXDip;
        public double TiltDegrees;
    }

    /// <summary>被摸头后的迎手动作：短起手、可读驻留、慢收势；方向只来自鼠标在脸部左右的位置。</summary>
    public static class PatAffectionPolicy
    {
        public const double DurationSeconds = 0.78;

        public static PatAffectionFrame Sample(double seconds, double pointerBias)
        {
            double bias = Math.Max(-1, Math.Min(1, pointerBias));
            double intensity;
            if (seconds <= 0) intensity = 0;
            else if (seconds < 0.18) intensity = Smooth(seconds / 0.18);
            else if (seconds < 0.50) intensity = 1;
            else if (seconds < DurationSeconds) intensity = 1 - Smooth((seconds - 0.50) / (DurationSeconds - 0.50));
            else intensity = 0;
            return new PatAffectionFrame
            {
                Intensity = intensity,
                LeanXDip = bias * 2.2 * intensity,
                TiltDegrees = bias * 2.6 * intensity
            };
        }

        private static double Smooth(double value)
        {
            value = Math.Max(0, Math.Min(1, value));
            return value * value * (3 - 2 * value);
        }
    }

    /// <summary>
    /// 用户主动投喂的小剧场时间线。三段式来自桌宠业界常见的“送到嘴边 → 咀嚼 → 满足反馈”，
    /// 所有值都是连续参数，避免用若干离散姿态硬切。
    /// </summary>
    public struct TreatMotionFrame
    {
        public double Offer;
        public double Bite;
        public double Chew;
        public double Joy;
        public double CookieOpacity;
    }

    public enum TreatSnackKind
    {
        Fish,
        Carrot,
        Seeds,
        Biscuit
    }

    public static class TreatSnackPolicy
    {
        public static TreatSnackKind ForPet(string petId)
        {
            if (string.Equals(petId, "03-penguin", StringComparison.OrdinalIgnoreCase)
                || string.Equals(petId, "01-cat", StringComparison.OrdinalIgnoreCase)) return TreatSnackKind.Fish;
            if (string.Equals(petId, "05-rabbit", StringComparison.OrdinalIgnoreCase)) return TreatSnackKind.Carrot;
            if (string.Equals(petId, "09-hamster", StringComparison.OrdinalIgnoreCase)
                || string.Equals(petId, "11-cockatiel", StringComparison.OrdinalIgnoreCase)) return TreatSnackKind.Seeds;
            return TreatSnackKind.Biscuit;
        }
    }

    public static class TreatMotionPolicy
    {
        public const double DurationSeconds = 3.15;

        private static double Clamp01(double value)
        {
            return Math.Max(0, Math.Min(1, value));
        }

        private static double Smooth(double value)
        {
            value = Clamp01(value);
            return value * value * (3 - 2 * value);
        }

        public static TreatMotionFrame Sample(double seconds)
        {
            double offer = Smooth(seconds / 0.82);
            double bite = Smooth((seconds - 0.78) / 0.42);
            double chewWindow = Clamp01((seconds - 0.92) / 0.26) * Clamp01((2.18 - seconds) / 0.34);
            double joy = Smooth((seconds - 1.32) / 0.40) * Clamp01((DurationSeconds - seconds) / 0.48);
            return new TreatMotionFrame
            {
                Offer = offer,
                Bite = bite,
                Chew = chewWindow,
                Joy = joy,
                CookieOpacity = Clamp01(1 - bite)
            };
        }
    }

    /// <summary>
    /// 用户主动发起的击掌窗口。宠物只在短暂邀请期拦截一次点击；超时就自然收爪，
    /// 不把普通键鼠输入误当成回应，也不会在后台自动弹出。
    /// </summary>
    public static class HighFivePolicy
    {
        public const double InviteSeconds = 2.60;
        public const double CelebrateSeconds = 0.95;

        public static bool CanRespond(double nowSeconds, double invitedAtSeconds, bool awaiting)
        {
            return awaiting && nowSeconds >= invitedAtSeconds
                && nowSeconds - invitedAtSeconds <= InviteSeconds;
        }

        public static double InviteProgress(double elapsedSeconds)
        {
            return Math.Max(0, Math.Min(1, elapsedSeconds / 0.34));
        }

        public static double CelebrateProgress(double elapsedSeconds)
        {
            return Math.Max(0, Math.Min(1, elapsedSeconds / CelebrateSeconds));
        }

        public static bool UsesWing(string petId)
        {
            return string.Equals(petId, "03-penguin", StringComparison.OrdinalIgnoreCase)
                || string.Equals(petId, "11-cockatiel", StringComparison.OrdinalIgnoreCase);
        }

        public static bool CapturesPointer(bool active, bool celebrating)
        {
            return active && !celebrating;
        }

        public static bool ShouldTimeout(bool tokenMatches, bool exclusiveActive, bool celebrating)
        {
            return tokenMatches && exclusiveActive && !celebrating;
        }
    }

    public struct SummonPlan
    {
        public bool Valid;
        public bool FacingRight;
        public Point Target;
    }

    /// <summary>“到我这来”只由用户主动触发；宠物停在光标侧边而不是盖住光标，并始终留在当前工作区。</summary>
    public static class SummonGeometry
    {
        public static SummonPlan Resolve(Rect workArea, Point pointer, double width, double height)
        {
            SummonPlan plan = new SummonPlan();
            if (workArea.IsEmpty || width < 20 || height < 20 || !workArea.Contains(pointer)) return plan;
            double gap = Math.Max(14, Math.Min(30, width * 0.18));
            bool placeRight = pointer.X + gap + width <= workArea.Right - 6;
            double left = placeRight ? pointer.X + gap : pointer.X - gap - width;
            double top = pointer.Y - height * 0.70;
            left = Math.Max(workArea.Left + 6, Math.Min(workArea.Right - width - 6, left));
            top = Math.Max(workArea.Top + 6, Math.Min(workArea.Bottom - height - 6, top));
            plan.Valid = true;
            plan.FacingRight = !placeRight;
            plan.Target = new Point(left, top);
            return plan;
        }
    }

    public struct BalloonToyState
    {
        public double X, Y, VX, VY;
    }

    public static class BalloonToyPhysics
    {
        public static BalloonToyState Initial()
        {
            return new BalloonToyState { X = 0.50, Y = 0.20, VX = 0.11, VY = -0.32 };
        }

        public static BalloonToyState Step(BalloonToyState state, double dt)
        {
            dt = Math.Max(0.005, Math.Min(0.08, dt));
            state.VY += 0.72 * dt;
            state.X += state.VX * dt;
            state.Y += state.VY * dt;
            if (state.X < 0.18) { state.X = 0.18; state.VX = Math.Abs(state.VX) * 0.92; }
            if (state.X > 0.82) { state.X = 0.82; state.VX = -Math.Abs(state.VX) * 0.92; }
            if (state.Y < 0.10) { state.Y = 0.10; state.VY = Math.Abs(state.VY) * 0.72; }
            // 落到头顶时小满自动托一下，小游戏不会因用户不点而制造失败焦虑。
            if (state.Y > 0.43) { state.Y = 0.43; state.VY = -0.56; state.VX = -state.VX; }
            return state;
        }

        public static BalloonToyState Bop(BalloonToyState state, double horizontalBias)
        {
            state.VY = -0.72;
            state.VX = Math.Max(-0.28, Math.Min(0.28, state.VX + horizontalBias * 0.12));
            return state;
        }
    }

    public struct PatrolPlan
    {
        public bool Valid;
        public bool WindowPerch;
        public bool FacingRight;
        public Point Target;
    }

    public static class PatrolPolicy
    {
        public const int MinDelaySeconds = 480;
        public const int MaxDelaySeconds = 840;

        public static int NextDelayMs(Random random)
        {
            return (random == null ? MinDelaySeconds : random.Next(MinDelaySeconds, MaxDelaySeconds + 1)) * 1000;
        }

        public static PatrolPlan Resolve(Rect workArea, Rect pet, Rect foreground)
        {
            PatrolPlan plan = new PatrolPlan();
            if (workArea.IsEmpty || pet.Width < 20 || pet.Height < 20) return plan;
            if (!foreground.IsEmpty && foreground.Width >= 280 && foreground.Height >= 160)
            {
                double perchLeft = Math.Max(workArea.Left + 6, Math.Min(workArea.Right - pet.Width - 6,
                    foreground.Right - pet.Width - 22));
                double perchTop = Math.Max(workArea.Top + 6, Math.Min(workArea.Bottom - pet.Height - 6,
                    foreground.Top - pet.Height * 0.72));
                double dx = perchLeft - pet.Left;
                double dy = perchTop - pet.Top;
                if (Math.Abs(dx) >= pet.Width * 0.35 && Math.Abs(dx) <= pet.Width * 2.25
                    && Math.Abs(dy) <= pet.Height * 1.35)
                {
                    plan.Valid = true;
                    plan.WindowPerch = true;
                    plan.FacingRight = dx >= 0;
                    plan.Target = new Point(perchLeft, perchTop);
                    return plan;
                }
            }

            double roomRight = workArea.Right - pet.Right - 8;
            double roomLeft = pet.Left - workArea.Left - 8;
            bool goRight = roomRight >= roomLeft;
            double room = goRight ? roomRight : roomLeft;
            if (room < pet.Width * 0.65)
            {
                goRight = !goRight;
                room = goRight ? roomRight : roomLeft;
            }
            if (room < pet.Width * 0.45) return plan;
            double distance = Math.Min(room, pet.Width * 1.55);
            plan.Valid = true;
            plan.WindowPerch = false;
            plan.FacingRight = goRight;
            plan.Target = new Point(pet.Left + (goRight ? distance : -distance), pet.Top);
            return plan;
        }
    }

    public static class AmbientInterruptionPolicy
    {
        public const double UserInvitationGraceSeconds = 0.90;
        public const double AutomaticActionGraceSeconds = 0.18;
        public const double ShyGraceSeconds = 0.25;
        public const double BalloonGraceSeconds = 0.45;
        public const double DemoGraceSeconds = 11.0;

        public static bool ShouldCancel(bool active, double now, double graceUntil)
        {
            return active && now >= graceUntil;
        }
    }
}
