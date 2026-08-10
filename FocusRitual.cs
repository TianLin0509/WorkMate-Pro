using System;

namespace WorkMatePro
{
    /// <summary>
    /// 用户主动发起的专注仪式。它与自动识别的心流不同：有明确时长、可随时结束、
    /// 结束只收尾一次；会议、编译等待、边驻和退避时不允许新开，避免导演抢戏。
    /// </summary>
    public static class FocusRitualPolicy
    {
        public const int DefaultMinutes = 25;

        public static bool IsActive(DateTime now, DateTime until)
        {
            return until > now;
        }

        public static bool CanStart(BehaviorState behavior, bool buildWaiting, bool docked, bool retreating)
        {
            return behavior != BehaviorState.Meeting && !buildWaiting && !docked && !retreating;
        }

        public static string RemainingLabel(DateTime now, DateTime until)
        {
            TimeSpan remaining = until - now;
            if (remaining <= TimeSpan.Zero) return "专注结束";
            int minutes = Math.Max(0, (int)Math.Floor(remaining.TotalMinutes));
            int seconds = Math.Max(0, remaining.Seconds);
            return string.Format("专注 · {0:00}:{1:00}", minutes, seconds);
        }

        public static string RemainingClock(DateTime now, DateTime until)
        {
            TimeSpan remaining = until - now;
            if (remaining <= TimeSpan.Zero) return "00:00";
            int totalSeconds = Math.Max(0, (int)Math.Ceiling(remaining.TotalSeconds));
            return string.Format("{0:00}:{1:00}", totalSeconds / 60, totalSeconds % 60);
        }

        public static double ElapsedProgress(DateTime now, DateTime started, DateTime until)
        {
            double total = (until - started).TotalSeconds;
            if (total <= 0) return 1;
            return Math.Max(0, Math.Min(1, (now - started).TotalSeconds / total));
        }

        public static bool CanPresentCompletion(BehaviorState behavior, bool buildWaiting, bool docked,
            bool retreating, bool quiet, bool exclusiveAnimation)
        {
            return behavior != BehaviorState.Meeting && !buildWaiting && !docked && !retreating
                && !quiet && !exclusiveAnimation;
        }
    }
}
