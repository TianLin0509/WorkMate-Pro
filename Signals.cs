using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace WorkMatePro
{
    /// <summary>
    /// 一次状态采样。隐私红线：所有输入只保留“计数与布尔”，
    /// 键码、窗口标题、音频内容一律当场丢弃，永不落盘。
    /// </summary>
    public struct SignalSample
    {
        public double KeyPerMin;
        public double WheelPerMin;
        public double ClickPerMin;
        public double MovePerMin;
        public int IdleSeconds;
        public int IdleIntentionalSeconds;
        public bool MicInUse;
        public bool CameraInUse;
        public bool AudioActive;
        public float AudioPeak;
        public string ForegroundCategory;
        public DateTime Now;
    }

    public static class NativeSignals
    {
        public const int WM_INPUT = 0x00FF;
        public const uint RID_INPUT = 0x10000003;
        public const uint RIM_TYPEMOUSE = 0;
        public const uint RIM_TYPEKEYBOARD = 1;
        public const uint RIDEV_INPUTSINK = 0x00000100;

        [StructLayout(LayoutKind.Sequential)]
        public struct RAWINPUTDEVICE
        {
            public ushort UsagePage;
            public ushort Usage;
            public uint Flags;
            public IntPtr Target;
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] devices, uint count, uint size);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint GetRawInputData(IntPtr rawInput, uint command, IntPtr data, ref uint size, uint headerSize);

        [DllImport("shell32.dll")]
        public static extern int SHQueryUserNotificationState(out int state);

        [DllImport("user32.dll")]
        public static extern int GetMessageTime();

        public delegate bool MonitorEnumProc(IntPtr monitor, IntPtr hdc, IntPtr rect, IntPtr data);

        [DllImport("user32.dll")]
        public static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc callback, IntPtr data);

        public static uint RawInputHeaderSize
        {
            get { return (uint)(Marshal.SizeOf(typeof(IntPtr)) * 2 + 8); }
        }
    }

    /// <summary>
    /// Raw Input 分设备计数器。通过 RIDEV_INPUTSINK 挂在桌宠窗口上，
    /// 即使桌宠不在前台也能收到全局输入。回调里只累加次数，键码立即丢弃。
    /// </summary>
    public sealed class RawInputMonitor
    {
        private readonly object sync = new object();
        private readonly Queue<double> keys = new Queue<double>();
        private readonly Queue<KeyValuePair<double, double>> wheels = new Queue<KeyValuePair<double, double>>();
        private readonly Queue<double> clicks = new Queue<double>();
        private readonly Queue<double> moves = new Queue<double>();
        private double lastInput = double.MinValue;
        private double lastIntentionalInput = double.MinValue;
        private bool registered;

        /// <summary>高精度滚轮换算成"格数"（120 单位=1 格）：触控板惯性滚动不再虚报速率。</summary>
        public static double WheelNotches(int buttonData)
        {
            return Math.Abs(buttonData) / 120.0;
        }

        public bool Registered { get { return registered; } }
        public int MessagesSeen;
        public int ParseFailures;
        public int KeyEvents;
        public int MouseEvents;
        public int KeyMakes;
        public int LastFlags;

        /// <summary>真实击键事件（仅次数语义，无键码），用于打字节奏同步。</summary>
        public event Action KeyMake;
        /// <summary>任意输入活动（只发一个无内容脉冲），用于让自动表演立即给工作让位。</summary>
        public event Action UserActivity;
        private double lastUserActivityRaised = double.MinValue;

        private void RaiseKeyMake()
        {
            Action handler = KeyMake;
            if (handler != null) handler();
        }

        private void RaiseUserActivity(double now)
        {
            if (now - lastUserActivityRaised < 0.12) return;
            lastUserActivityRaised = now;
            Action handler = UserActivity;
            if (handler != null) handler();
        }
        public int KeyQueueSize { get { lock (sync) { return keys.Count; } } }
        public int WheelQueueSize { get { lock (sync) { return wheels.Count; } } }
        public double LastNowProcess;
        public double LastNowRates;
        public double LastEnqueuedT;

        public bool Register(IntPtr hwnd)
        {
            NativeSignals.RAWINPUTDEVICE[] devices = new NativeSignals.RAWINPUTDEVICE[2];
            devices[0].UsagePage = 1; devices[0].Usage = 6; // keyboard
            devices[0].Flags = NativeSignals.RIDEV_INPUTSINK; devices[0].Target = hwnd;
            devices[1].UsagePage = 1; devices[1].Usage = 2; // mouse
            devices[1].Flags = NativeSignals.RIDEV_INPUTSINK; devices[1].Target = hwnd;
            registered = NativeSignals.RegisterRawInputDevices(devices, 2, (uint)Marshal.SizeOf(typeof(NativeSignals.RAWINPUTDEVICE)));
            return registered;
        }

        private static double Now() { return Environment.TickCount / 1000.0; }

        /// <summary>
        /// 处理一条 WM_INPUT。eventTimeSeconds 用消息投递时间（GetMessageTime，与 TickCount 同基），
        /// 主机拥塞导致 Raw Input 延迟投递时，速率窗口仍按真实输入时刻统计。
        /// </summary>
        public void ProcessInput(IntPtr lParam, double eventTimeSeconds)
        {
            MessagesSeen++;
            uint headerSize = NativeSignals.RawInputHeaderSize;
            uint size = 0;
            NativeSignals.GetRawInputData(lParam, NativeSignals.RID_INPUT, IntPtr.Zero, ref size, headerSize);
            if (size == 0 || size > 4096) { ParseFailures++; return; }
            IntPtr buffer = Marshal.AllocHGlobal((int)size);
            try
            {
                if (NativeSignals.GetRawInputData(lParam, NativeSignals.RID_INPUT, buffer, ref size, headerSize) != size) { ParseFailures++; return; }
                uint type = (uint)Marshal.ReadInt32(buffer, 0);
                long bodyAddress = buffer.ToInt64() + headerSize;
                double now = eventTimeSeconds > 0 ? eventTimeSeconds : Now();
                LastNowProcess = Now();
                bool activity = false;
                lock (sync)
                {
                    if (type == NativeSignals.RIM_TYPEKEYBOARD)
                    {
                        KeyEvents++;
                        ushort flags = (ushort)Marshal.ReadInt16(new IntPtr(bodyAddress), 2);
                        LastFlags = flags;
                        if ((flags & 1) == 0) // RI_KEY_MAKE：只数按下，不读键码
                        {
                            KeyMakes++;
                            keys.Enqueue(now);
                            LastEnqueuedT = now;
                            lastInput = now;
                            lastIntentionalInput = now;
                            RaiseKeyMake();
                            activity = true;
                        }
                    }
                    else if (type == NativeSignals.RIM_TYPEMOUSE)
                    {
                        MouseEvents++;
                        IntPtr body = new IntPtr(bodyAddress);
                        ushort buttonFlags = (ushort)Marshal.ReadInt16(body, 4);
                        short wheelDelta = (short)Marshal.ReadInt16(body, 6);
                        int dx = Marshal.ReadInt32(body, 12);
                        int dy = Marshal.ReadInt32(body, 16);
                        if ((buttonFlags & 0x0400) != 0 || (buttonFlags & 0x0800) != 0)
                        {
                            wheels.Enqueue(new KeyValuePair<double, double>(now, WheelNotches(wheelDelta)));
                            lastIntentionalInput = now;
                            activity = true;
                        }
                        if ((buttonFlags & 0x0015) != 0) // 左/右/中键按下
                        {
                            clicks.Enqueue(now);
                            lastIntentionalInput = now;
                            activity = true;
                        }
                        if (dx != 0 || dy != 0) { moves.Enqueue(now); activity = true; }
                        lastInput = now;
                    }
                    Prune(keys, now, 60);
                    PrunePairs(wheels, now, 60);
                    Prune(clicks, now, 60);
                    Prune(moves, now, 12);
                }
                if (activity) RaiseUserActivity(now);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        private static void Prune(Queue<double> queue, double now, double windowSeconds)
        {
            while (queue.Count > 0 && now - queue.Peek() > windowSeconds) queue.Dequeue();
        }

        private static void PrunePairs(Queue<KeyValuePair<double, double>> queue, double now, double windowSeconds)
        {
            while (queue.Count > 0 && now - queue.Peek().Key > windowSeconds) queue.Dequeue();
        }

        private static double RatePerMin(Queue<double> queue, double now, double windowSeconds)
        {
            int count = 0;
            foreach (double t in queue) { if (now - t <= windowSeconds) count++; }
            return count * (60.0 / windowSeconds);
        }

        private static double NotchesPerMin(Queue<KeyValuePair<double, double>> queue, double now, double windowSeconds)
        {
            double sum = 0;
            foreach (KeyValuePair<double, double> item in queue) { if (now - item.Key <= windowSeconds) sum += item.Value; }
            return sum * (60.0 / windowSeconds);
        }

        public void GetRates(out double keyPerMin, out double wheelPerMin, out double clickPerMin, out double movePerMin, out int idleSeconds)
        {
            double now = Now();
            LastNowRates = now;
            lock (sync)
            {
                keyPerMin = RatePerMin(keys, now, 8);
                wheelPerMin = NotchesPerMin(wheels, now, 10);
                clickPerMin = RatePerMin(clicks, now, 8);
                movePerMin = RatePerMin(moves, now, 8);
                idleSeconds = ComputeIdle(lastInput, now);
            }
        }

        /// <summary>空闲秒数（纯函数，可单测）。TickCount 负值（开机超 24.8 天）下差值依然正确；
        /// MinValue 哨兵表示"尚无输入"。</summary>
        public static int ComputeIdle(double lastInput, double now)
        {
            if (lastInput == double.MinValue) return 0;
            return (int)Math.Max(0, now - lastInput);
        }

        /// <summary>只统计"有意输入"（键盘/滚轮/点击）的空闲——纯鼠标移动不算，看视频时手抖不误伤。</summary>
        public int IntentionalIdleSeconds()
        {
            double now = Now();
            lock (sync)
            {
                return ComputeIdle(lastIntentionalInput, now);
            }
        }
    }

    /// <summary>
    /// 免内容的系统信号：麦克风/摄像头占用（ConsentStore 注册表，与系统托盘图标同源）
    /// 与音频播放峰值（WASAPI Peak Meter）。全是布尔/电平，不碰任何内容。
    /// </summary>
    public sealed class MediaStateMonitor
    {
        private readonly Queue<bool> peakHistory = new Queue<bool>();
        private IMMDeviceEnumerator enumerator;
        private bool audioFailed;

        public bool MicInUse { get; private set; }
        public bool CameraInUse { get; private set; }
        public bool AudioActive { get; private set; }
        public float LastPeak { get; private set; }
        public bool AudioProbeOk { get { return !audioFailed; } }

        public void Poll()
        {
            MicInUse = CapabilityInUse("microphone");
            CameraInUse = CapabilityInUse("webcam");
            LastPeak = ReadPeak();
            bool loud = LastPeak > 0.0005f;
            peakHistory.Enqueue(loud);
            while (peakHistory.Count > 15) peakHistory.Dequeue();
            int loudCount = 0;
            foreach (bool value in peakHistory) { if (value) loudCount++; }
            AudioActive = peakHistory.Count >= 8 && loudCount >= (int)Math.Ceiling(peakHistory.Count * 0.7);
        }

        /// <summary>
        /// Win10/11 的正确路径：CapabilityAccessManager\ConsentStore。
        /// 终审 P0：旧代码读 Win8 时代的 DeviceAccess\Global，Win11 上不存在、静默恒 false。
        /// 注意必须递归 NonPackaged 子键——会议软件（微信/腾讯会议/Zoom 等）多为 Win32 应用。
        /// </summary>
        public bool ConsentStoreAvailable { get; private set; }

        private bool CapabilityInUse(string capabilityName)
        {
            try
            {
                using (RegistryKey root = Registry.CurrentUser.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\" + capabilityName))
                {
                    ConsentStoreAvailable = root != null;
                    if (root == null) return false;
                    if (ScanConsentValues(root.GetValue("LastUsedTimeStart"), root.GetValue("LastUsedTimeStop"))) return true;
                    foreach (string subName in root.GetSubKeyNames())
                    {
                        using (RegistryKey sub = root.OpenSubKey(subName))
                        {
                            if (sub == null) continue;
                            if (ScanConsentValues(sub.GetValue("LastUsedTimeStart"), sub.GetValue("LastUsedTimeStop"))) return true;
                            foreach (string leafName in sub.GetSubKeyNames())
                            {
                                using (RegistryKey leaf = sub.OpenSubKey(leafName))
                                {
                                    if (leaf != null && ScanConsentValues(leaf.GetValue("LastUsedTimeStart"), leaf.GetValue("LastUsedTimeStop")))
                                        return true;
                                }
                            }
                        }
                    }
                }
            }
            catch { }
            return false;
        }

        /// <summary>ConsentStore 判定规则（纯函数，可单测）：开始过且未写入结束时间 → 占用中。</summary>
        public static bool ScanConsentValues(object start, object stop)
        {
            long startValue = start is long ? (long)start : 0;
            long stopValue = stop is long ? (long)stop : 0;
            return startValue > 0 && stopValue == 0;
        }

        private float ReadPeak()
        {
            if (audioFailed) return 0;
            try
            {
                if (enumerator == null) enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorCom();
                IMMDevice device;
                int hr = enumerator.GetDefaultAudioEndpoint(0, 0, out device); // eRender, eConsole
                if (hr != 0 || device == null) { enumerator = null; return 0; }
                Guid iid = typeof(IAudioMeterInformation).GUID;
                object instance;
                hr = device.Activate(ref iid, 23, IntPtr.Zero, out instance); // CLSCTX_ALL
                Marshal.ReleaseComObject(device);
                if (hr != 0 || instance == null) return 0;
                IAudioMeterInformation meter = (IAudioMeterInformation)instance;
                float peak;
                hr = meter.GetPeakValue(out peak);
                Marshal.ReleaseComObject(instance);
                return hr == 0 ? peak : 0;
            }
            catch
            {
                enumerator = null;
                audioFailed = true; // 无音频设备等环境：永久关闭探测，不刷屏
                return 0;
            }
        }

        [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        private class MMDeviceEnumeratorCom { }

        [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceEnumerator
        {
            int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr endpoints);
            int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
            int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IntPtr device);
            int RegisterEndpointNotificationCallback(IntPtr client);
            int UnregisterEndpointNotificationCallback(IntPtr client);
        }

        [Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDevice
        {
            int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object instance);
            int OpenPropertyStore(int stgmAccess, out IntPtr properties);
            int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
            int GetState(out int state);
        }

        [Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioMeterInformation
        {
            int GetPeakValue(out float peak);
            int GetMeteringChannelCount(out uint count);
            int GetChannelsPeakValues(uint count, float[] values);
            int QueryHardwareSupport(out uint flags);
        }
    }

    /// <summary>
    /// 演示/全屏退避：SHQueryUserNotificationState 检测 D3D 全屏与演示模式，
    /// 投屏、放 PPT、共享屏幕时桌宠自动隐身，结束后自己回来。
    /// </summary>
    public sealed class PresentationGuard
    {
        public int LastState { get; private set; }

        public bool ShouldRetreat
        {
            get { return LastState == 3 || LastState == 4; } // D3D 全屏 / 演示模式
        }

        public void Poll()
        {
            int state;
            LastState = NativeSignals.SHQueryUserNotificationState(out state) == 0 ? state : 1;
        }
    }

    public sealed class MonitorInfoEx
    {
        public IntPtr Handle;
        public NativeMethods.RECT Monitor;
        public NativeMethods.RECT Work;
        public bool Primary;
    }

    public static class MonitorHelper
    {
        public static List<MonitorInfoEx> All()
        {
            List<MonitorInfoEx> list = new List<MonitorInfoEx>();
            NativeSignals.MonitorEnumProc callback = delegate(IntPtr monitor, IntPtr hdc, IntPtr rect, IntPtr data)
            {
                NativeMethods.MONITORINFO info = new NativeMethods.MONITORINFO();
                info.cbSize = Marshal.SizeOf(info);
                if (NativeMethods.GetMonitorInfo(monitor, ref info))
                {
                    list.Add(new MonitorInfoEx
                    {
                        Handle = monitor,
                        Monitor = info.rcMonitor,
                        Work = info.rcWork,
                        Primary = (info.dwFlags & 1) != 0
                    });
                }
                return true;
            };
            NativeSignals.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero);
            return list;
        }

        /// <summary>多屏之间的“走廊边缘”不算磁吸区：另一侧紧邻着另一块屏。</summary>
        public static bool IsCorridorEdge(MonitorInfoEx current, bool rightEdge, List<MonitorInfoEx> all)
        {
            return IsCorridorEdge(current, rightEdge ? DockEdge.Right : DockEdge.Left, all);
        }

        /// <summary>四向走廊判定（v1.5）：上下叠屏同样豁免。</summary>
        public static bool IsCorridorEdge(MonitorInfoEx current, DockEdge edge, List<MonitorInfoEx> all)
        {
            for (int i = 0; i < all.Count; i++)
            {
                MonitorInfoEx other = all[i];
                if (other.Handle == current.Handle) continue;
                bool adjacent = false;
                bool overlaps = false;
                switch (edge)
                {
                    case DockEdge.Right:
                        adjacent = Math.Abs(other.Monitor.Left - current.Monitor.Right) <= 4;
                        overlaps = RangesOverlap(other.Monitor.Top, other.Monitor.Bottom, current.Monitor.Top, current.Monitor.Bottom);
                        break;
                    case DockEdge.Left:
                        adjacent = Math.Abs(current.Monitor.Left - other.Monitor.Right) <= 4;
                        overlaps = RangesOverlap(other.Monitor.Top, other.Monitor.Bottom, current.Monitor.Top, current.Monitor.Bottom);
                        break;
                    case DockEdge.Bottom:
                        adjacent = Math.Abs(other.Monitor.Top - current.Monitor.Bottom) <= 4;
                        overlaps = RangesOverlap(other.Monitor.Left, other.Monitor.Right, current.Monitor.Left, current.Monitor.Right);
                        break;
                    case DockEdge.Top:
                        adjacent = Math.Abs(current.Monitor.Top - other.Monitor.Bottom) <= 4;
                        overlaps = RangesOverlap(other.Monitor.Left, other.Monitor.Right, current.Monitor.Left, current.Monitor.Right);
                        break;
                }
                if (adjacent && overlaps) return true;
            }
            return false;
        }

        private static bool RangesOverlap(int aTop, int aBottom, int bTop, int bBottom)
        {
            return aTop < bBottom && bTop < aBottom;
        }

        public static MonitorInfoEx FindByHandle(List<MonitorInfoEx> all, IntPtr handle)
        {
            for (int i = 0; i < all.Count; i++) { if (all[i].Handle == handle) return all[i]; }
            return null;
        }

        public static MonitorInfoEx Primary(List<MonitorInfoEx> all)
        {
            for (int i = 0; i < all.Count; i++) { if (all[i].Primary) return all[i]; }
            return all.Count > 0 ? all[0] : null;
        }
    }
}
