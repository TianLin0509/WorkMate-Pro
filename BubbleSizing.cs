using System;

namespace WorkMatePro
{
    /// <summary>快捷气泡随实际内容收紧；有会议/当前任务时自然增高，无内容时不留下大块空白。</summary>
    public static class BubbleSizing
    {
        public const double MinimumHeight = 520;
        public const double MaximumHeight = 700;
        public const double ShellVerticalPadding = 38;

        public static double Resolve(double contentDesiredHeight)
        {
            double desired = Math.Max(0, contentDesiredHeight) + ShellVerticalPadding;
            return Math.Max(MinimumHeight, Math.Min(MaximumHeight, Math.Ceiling(desired)));
        }
    }
}
