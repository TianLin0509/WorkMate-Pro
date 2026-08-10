using System;

namespace WorkMatePro
{
    public static class NodOffSchedule
    {
        public const int MinDelaySeconds = 90;
        public const int MaxDelaySeconds = 150;

        public static double NextDelaySeconds(Random random)
        {
            if (random == null) return MinDelaySeconds;
            return MinDelaySeconds + random.NextDouble() * (MaxDelaySeconds - MinDelaySeconds);
        }

        public static bool AllowsAutonomousScene(bool reducedMotion, bool buildWaiting)
        {
            return !reducedMotion && !buildWaiting;
        }
    }

    public enum NodOffStage
    {
        None,
        Drooping,      // 困倦点头：慢慢垂下去
        Startled,      // 惊醒：猛地一抬头
        DroopingDeep,  // 没扛住，再次更深地垂下
        Curled,        // 蜷睡（切 sleep 姿态）
        WakeStretch,   // 醒来伸个懒腰
        Done
    }

    public struct NodOffPose
    {
        public double Tilt;        // 倾斜角（度，前倾为正）
        public double ScaleY;      // 纵向压缩系数
        public bool StartleFlash;  // 惊醒瞬间的 "!" 闪现
        public bool SleepPose;     // 进入蜷睡姿态
        public bool StretchPose;   // 进入伸懒腰
    }

    /// <summary>
    /// 困倦点头完整分镜（纯逻辑、无 UI 依赖，可表驱动测试）：
    /// 困倦点头 2.4s → 惊醒 0.5s → 深垂 1.8s → 蜷睡 N 秒 → 醒来伸懒腰 1.4s。
    /// 参考打工人真实犯困曲线：先点一下、惊醒、扛不住才睡。
    /// </summary>
    public sealed class NodOffDirector
    {
        public const double DroopingSec = 2.4;
        public const double StartledSec = 0.5;
        public const double DroopingDeepSec = 1.8;
        public const double WakeStretchSec = 1.4;

        private double sleepSeconds = 20;
        private double stageElapsed;

        public NodOffStage Stage { get; private set; }
        public bool Active { get { return Stage != NodOffStage.None && Stage != NodOffStage.Done; } }
        public double StageElapsed { get { return stageElapsed; } }

        public void Start(double sleepSec)
        {
            sleepSeconds = Math.Max(5, Math.Min(45, sleepSec));
            Stage = NodOffStage.Drooping;
            stageElapsed = 0;
        }

        public void Abort()
        {
            Stage = NodOffStage.None;
            stageElapsed = 0;
        }

        /// <summary>推进一小步，返回当前姿态参数。</summary>
        public NodOffPose Tick(double dt)
        {
            if (!Active) return new NodOffPose();
            stageElapsed += dt;
            switch (Stage)
            {
                case NodOffStage.Drooping:
                    if (stageElapsed >= DroopingSec) { Stage = NodOffStage.Startled; stageElapsed = 0; }
                    break;
                case NodOffStage.Startled:
                    if (stageElapsed >= StartledSec) { Stage = NodOffStage.DroopingDeep; stageElapsed = 0; }
                    break;
                case NodOffStage.DroopingDeep:
                    if (stageElapsed >= DroopingDeepSec) { Stage = NodOffStage.Curled; stageElapsed = 0; }
                    break;
                case NodOffStage.Curled:
                    if (stageElapsed >= sleepSeconds) { Stage = NodOffStage.WakeStretch; stageElapsed = 0; }
                    break;
                case NodOffStage.WakeStretch:
                    if (stageElapsed >= WakeStretchSec) { Stage = NodOffStage.Done; stageElapsed = 0; }
                    break;
            }
            return PoseAt(Stage, stageElapsed);
        }

        /// <summary>各阶段姿态曲线（纯函数）。</summary>
        public static NodOffPose PoseAt(NodOffStage stage, double elapsed)
        {
            NodOffPose pose = new NodOffPose();
            switch (stage)
            {
                case NodOffStage.Drooping:
                    // 缓慢垂下：倾斜 0→5°，纵向压 1→0.94，ease-in
                    double k1 = EaseIn(Math.Min(1, elapsed / DroopingSec));
                    pose.Tilt = 5.0 * k1;
                    pose.ScaleY = 1 - 0.06 * k1;
                    break;
                case NodOffStage.Startled:
                    // 惊醒：快速回弹 -2° + 轻微过度拉伸，"!" 闪现
                    double k2 = 1 - Math.Min(1, elapsed / StartledSec);
                    pose.Tilt = -2.0 * k2;
                    pose.ScaleY = 1 + 0.03 * k2;
                    pose.StartleFlash = elapsed < 0.35;
                    break;
                case NodOffStage.DroopingDeep:
                    // 更深的第二次下垂：5→8°，0.94→0.90
                    double k3 = EaseIn(Math.Min(1, elapsed / DroopingDeepSec));
                    pose.Tilt = 5.0 + 3.0 * k3;
                    pose.ScaleY = 0.94 - 0.04 * k3;
                    break;
                case NodOffStage.Curled:
                    pose.SleepPose = true;
                    pose.Tilt = 0;
                    pose.ScaleY = 1;
                    break;
                case NodOffStage.WakeStretch:
                    // 伸懒腰：先拉长再回落
                    double k4 = Math.Sin(Math.PI * Math.Min(1, elapsed / WakeStretchSec));
                    pose.StretchPose = true;
                    pose.Tilt = -1.5 * k4;
                    pose.ScaleY = 1 + 0.06 * k4;
                    break;
            }
            return pose;
        }

        private static double EaseIn(double k)
        {
            return k * k;
        }

        /// <summary>犯困窗口（纯函数，可单测）：深夜 ≥22 点，或午后 14:00-15:00 犯困带。</summary>
        public static bool DrowsyWindow(DateTime now)
        {
            return now.Hour >= 22 || (now.Hour >= 14 && now.Hour < 15);
        }
    }
}
