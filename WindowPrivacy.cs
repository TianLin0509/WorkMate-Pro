using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace WorkMatePro
{
    /// <summary>
    /// 公司场景的窗口边界策略：桌宠不抢前台焦点；用户允许时，
    /// 请求 Windows 在支持的系统截图/共享链路中排除 WorkMate 窗口。
    /// 注意：SetWindowDisplayAffinity 不是 DRM，也不是绝对的内容保护。
    /// </summary>
    public static class WindowPrivacy
    {
        public const uint WdaNone = 0x00000000;
        public const uint WdaExcludeFromCapture = 0x00000011;

        private const int GwlExStyle = -20;
        private const long WsExNoActivate = 0x08000000L;

        public static void Bind(Window window, Func<bool> captureProtectionEnabled, bool noActivate)
        {
            if (window == null) return;
            window.SourceInitialized += delegate
            {
                if (noActivate) ApplyNoActivate(window);
                bool enabled = captureProtectionEnabled == null || captureProtectionEnabled();
                ApplyCapturePolicy(window, enabled);
            };
        }

        public static bool ApplyCapturePolicy(Window window, bool enabled)
        {
            if (window == null) return false;
            IntPtr handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero) return false;
            return PrivacyNative.SetWindowDisplayAffinity(handle, enabled ? WdaExcludeFromCapture : WdaNone);
        }

        public static void ApplyAll(bool enabled)
        {
            if (Application.Current == null) return;
            foreach (Window window in Application.Current.Windows)
            {
                try { ApplyCapturePolicy(window, enabled); } catch { }
            }
        }

        public static bool TryGetAffinity(Window window, out uint affinity)
        {
            affinity = WdaNone;
            if (window == null) return false;
            IntPtr handle = new WindowInteropHelper(window).Handle;
            return handle != IntPtr.Zero && PrivacyNative.GetWindowDisplayAffinity(handle, out affinity);
        }

        private static void ApplyNoActivate(Window window)
        {
            IntPtr handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero) return;
            IntPtr style = PrivacyNative.GetWindowLongPtr(handle, GwlExStyle);
            long updated = style.ToInt64() | WsExNoActivate;
            if (updated != style.ToInt64()) PrivacyNative.SetWindowLongPtr(handle, GwlExStyle, new IntPtr(updated));
        }
    }

    internal static class PrivacyNative
    {
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint affinity);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowDisplayAffinity(IntPtr hWnd, out uint affinity);

        [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
        private static extern int GetWindowLong32(IntPtr hWnd, int index);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int index);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
        private static extern int SetWindowLong32(IntPtr hWnd, int index, int value);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int index, IntPtr value);

        public static IntPtr GetWindowLongPtr(IntPtr hWnd, int index)
        {
            return IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, index) : new IntPtr(GetWindowLong32(hWnd, index));
        }

        public static IntPtr SetWindowLongPtr(IntPtr hWnd, int index, IntPtr value)
        {
            return IntPtr.Size == 8
                ? SetWindowLongPtr64(hWnd, index, value)
                : new IntPtr(SetWindowLong32(hWnd, index, value.ToInt32()));
        }
    }
}
