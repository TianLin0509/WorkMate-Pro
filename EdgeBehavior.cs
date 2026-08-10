using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace WorkMatePro
{
    /// <summary>窗口位移动画：cubic-out 缓动，用于磁吸/迁移/鳍条弹回。</summary>
    public sealed class WindowMover
    {
        private readonly Window window;
        private readonly DispatcherTimer timer;
        private double fromLeft, fromTop, toLeft, toTop;
        private DateTime start;
        private int durationMs;
        private Action onDone;

        public WindowMover(Window window)
        {
            this.window = window;
            timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(15) };
            timer.Tick += Step;
        }

        public bool IsAnimating { get { return timer.IsEnabled; } }

        public void AnimateTo(double left, double top, int ms, Action done)
        {
            fromLeft = window.Left;
            fromTop = window.Top;
            toLeft = left;
            toTop = top;
            durationMs = Math.Max(60, ms);
            start = DateTime.Now;
            onDone = done;
            timer.Start();
        }

        public void Cancel()
        {
            timer.Stop();
            onDone = null;
        }

        private void Step(object sender, EventArgs e)
        {
            double k = Math.Min(1.0, (DateTime.Now - start).TotalMilliseconds / durationMs);
            double eased = 1 - Math.Pow(1 - k, 3); // cubic-out
            window.Left = fromLeft + (toLeft - fromLeft) * eased;
            window.Top = fromTop + (toTop - fromTop) * eased;
            if (k >= 1.0)
            {
                timer.Stop();
                Action done = onDone;
                onDone = null;
                if (done != null) done();
            }
        }
    }

    /// <summary>
    /// 鳍条：高速甩向边缘（或甩退）后，桌宠收成屏幕边的一条呼吸小条，
    /// 点一下弹回来。退散模式显示 Zzz，30 分钟后也会自己回来。
    /// </summary>
    public sealed class FinWindow : Window
    {
        private readonly Border shell;
        private readonly TextBlock label;
        private readonly Action onRestore;

        public FinWindow(string petName, string accentHex, bool sleepy, bool hideFromCapture, Action restore)
        {
            onRestore = restore;
            Title = "WorkMate 鳍条";
            Width = 15;
            Height = 78;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            Topmost = true;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Cursor = Cursors.Hand;
            ShowActivated = false;
            WindowPrivacy.Bind(this, delegate { return hideFromCapture; }, true);

            label = new TextBlock
            {
                Text = sleepy ? "Zzz" : petName,
                FontFamily = Theme.Font,
                FontSize = 9,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                LayoutTransform = new RotateTransform(90),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            shell = new Border
            {
                Background = Theme.Brush(accentHex),
                Child = label,
                Effect = Theme.Shadow(8, 0.25, 2)
            };
            Content = shell;
            shell.MouseLeftButtonUp += delegate { Restore(); };
            ToolTip = new ToolTip { Content = "点我，把" + petName + "叫回来" };
        }

        public void ShowAt(bool rightEdge, Rect workAreaDip, double centerY)
        {
            shell.CornerRadius = rightEdge ? new CornerRadius(8, 0, 0, 8) : new CornerRadius(0, 8, 8, 0);
            Left = rightEdge ? workAreaDip.Right - Width : workAreaDip.Left;
            Top = Math.Max(workAreaDip.Top + 10, Math.Min(workAreaDip.Bottom - Height - 10, centerY - Height / 2));
            Show();
            DoubleAnimation breathe = new DoubleAnimation(0.62, 1.0, TimeSpan.FromSeconds(1.2));
            breathe.AutoReverse = true;
            breathe.RepeatBehavior = RepeatBehavior.Forever;
            breathe.EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut };
            shell.BeginAnimation(UIElement.OpacityProperty, breathe);
        }

        private void Restore()
        {
            Close();
            Action callback = onRestore;
            if (callback != null) callback();
        }
    }

    /// <summary>
    /// 多屏跟随：技术员工是多屏人群。前台窗口连续约 6 秒在另一块屏、
    /// 且用户处于活跃状态时，桌宠 poof 一下迁移到你正在看的那块屏。
    /// 拖拽中、鳍条中、演示退避中不迁移。
    /// </summary>
    public sealed class MonitorFollower
    {
        private readonly Func<IntPtr> petHandle;
        private readonly Func<bool> canMigrate;
        private readonly Action<IntPtr> migrate;
        private IntPtr foreignMonitor;
        private int foreignTicks;

        public MonitorFollower(Func<IntPtr> petWindowHandle, Func<bool> canMigrateNow, Action<IntPtr> migrateTo)
        {
            petHandle = petWindowHandle;
            canMigrate = canMigrateNow;
            migrate = migrateTo;
        }

        /// <summary>每 2 秒调用一次。</summary>
        public void Check()
        {
            if (!canMigrate())
            {
                foreignTicks = 0;
                foreignMonitor = IntPtr.Zero;
                return;
            }
            IntPtr foreground = NativeMethods.GetForegroundWindow();
            if (foreground == IntPtr.Zero || foreground == petHandle())
            {
                foreignTicks = 0;
                return;
            }
            // 自家窗口（工作台/速记/气泡/鳍条）不算"你在看另一块屏"，避免宠物追着自己的 App 跑
            uint foregroundPid;
            NativeMethods.GetWindowThreadProcessId(foreground, out foregroundPid);
            if (foregroundPid == System.Diagnostics.Process.GetCurrentProcess().Id)
            {
                foreignTicks = 0;
                foreignMonitor = IntPtr.Zero;
                return;
            }
            IntPtr monitor = NativeMethods.MonitorFromWindow(foreground, 2);
            IntPtr petMonitor = NativeMethods.MonitorFromWindow(petHandle(), 2);
            if (monitor == IntPtr.Zero || monitor == petMonitor)
            {
                foreignTicks = 0;
                foreignMonitor = IntPtr.Zero;
                return;
            }
            if (monitor == foreignMonitor) foreignTicks++;
            else
            {
                foreignMonitor = monitor;
                foreignTicks = 1;
            }
            if (foreignTicks >= 3)
            {
                foreignTicks = 0;
                foreignMonitor = IntPtr.Zero;
                migrate(monitor);
            }
        }
    }
}
