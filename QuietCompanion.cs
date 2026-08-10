using System;

namespace WorkMatePro
{
    public static class QuietCompanionPolicy
    {
        public const int DefaultMinutes = 30;

        public static bool IsActive(DateTime now, DateTime until)
        {
            return until > now;
        }

        public static string RemainingLabel(DateTime now, DateTime until)
        {
            int minutes = Math.Max(1, (int)Math.Ceiling((until - now).TotalMinutes));
            return "安静陪伴 · " + minutes + "分";
        }
    }
}
