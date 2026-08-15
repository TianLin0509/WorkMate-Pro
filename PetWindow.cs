using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace WorkMatePro
{
    public sealed class PetWindow : Window
    {
        private const int HotkeyId = 7713;
        private const int WmNcHitTest = 0x0084;
        private static readonly IntPtr HtTransparent = new IntPtr(-1);
        private readonly WorkMateApp app;
        private readonly Grid root;
        private readonly Grid motionLayer;
        private readonly ScaleTransform scaleTransform;
        private readonly RotateTransform tiltTransform;
        private readonly TranslateTransform liftTransform;
        private readonly Image backImage;
        private readonly Image frontImage;
        private readonly ScaleTransform backImageMirror;
        private readonly ScaleTransform frontImageMirror;
        private readonly TranslateTransform backImageShift;
        private readonly TranslateTransform frontImageShift;
        private readonly Canvas overlay;
        private readonly System.Windows.Shapes.Ellipse contactShadow;
        private readonly Border identityPill;
        private readonly TextBlock identityText;
        private readonly Border ambientPill;
        private readonly TextBlock ambientText;
        private readonly PetMotion motion;
        private readonly WindowMover mover;
        private readonly DispatcherTimer singleClickTimer;

        private BehaviorState behavior = BehaviorState.Idle;
        private BehaviorState visualBehavior = BehaviorState.Idle;
        private readonly VisualBehaviorDirector visualDirector = new VisualBehaviorDirector();
        private DateTime overrideUntil = DateTime.MinValue;
        private string previewState;
        private string lastChipText = "";
        private string lastAction = "";
        private AmbientPresenceStyle ambientPresence;
        private string lastAmbientPresenceKey = "";

        // 拖拽物理
        private Point grabOffsetDip;
        private bool pointerCaptured;
        private bool dragging;
        private readonly List<double> dragX = new List<double>();
        private readonly List<double> dragT = new List<double>();

        // 摸头检测（悬停抚摸）
        private readonly List<double> hoverX = new List<double>();
        private readonly List<double> hoverT = new List<double>();
        private double lastPat = double.MinValue;
        private bool suppressClickOnce;

        // 捏脸检测（按住 350ms 触发）
        private DispatcherTimer pressHoldTimer;
        private bool squishHolding;
        private Point pressLocalPoint;

        // 拎起摇晃晕眩
        private bool dizzyTriggered;
        private double lastStrokeBounce = double.MinValue;

        // 困倦点头分镜（Top1：点头→惊醒→蜷睡→伸懒腰）
        private readonly NodOffDirector nodOffDir = new NodOffDirector();
        private readonly Random nodOffRandom = new Random();
        private DispatcherTimer nodOffTimer;
        private DateTime nodOffCooldownUntil = DateTime.MinValue;
        private bool nodOffSleeping;
        private bool nodOffStretched;
        private DateTime quietUntil = DateTime.MinValue;
        private DispatcherTimer quietTimer;

        // 可见性状态机：演示退避 / 正常（边驻不再隐藏窗口，只缩进边缘）
        private bool retreatActive;
        private bool migrating;

        // 四边缩入 + 探头（v1.5）
        private enum DockState { None, Snapped, Tucking, Docked, Peeking }
        private DockState dockState = DockState.None;
        private DockEdge dockEdge = DockEdge.None;
        private DateTime dozeUntil = DateTime.MinValue;
        private Border dockHitArea;
        private DispatcherTimer peekDelayTimer;
        private DispatcherTimer peekExitTimer;
        private DispatcherTimer dockHoverTimer;
        private readonly List<double> dragY = new List<double>();

        private bool hotkeyRegistered;
        private BubbleWindow bubble;
        private ToastWindow toast;
        private DispatcherTimer retreatReturnTimer;
        private readonly DispatcherTimer retreatDockDelayTimer;
        private int dockRequestToken;
        private int scheduledDockToken;
        private bool buildWait;
        private DispatcherTimer buildWaitTimer;
        private bool flowActive;
        private DateTime focusRitualStarted = DateTime.MinValue;
        private DateTime focusRitualUntil = DateTime.MinValue;
        private DispatcherTimer focusRitualTimer;
        private bool pendingFocusCompletion;
        private readonly DispatcherTimer frameTimer;
        private string animationSequence = "";
        private int animationStart;
        private int animationFrame;
        private int animationEnd;
        private int animationStep = 1;
        private bool animationLoop;
        private Action animationCompleted;
        private int animationPlaybackToken;
        private string behaviorAnimation = "";
        private readonly TypingPawDirector typingPawDirector = new TypingPawDirector();
        private readonly DispatcherTimer typingPoseTimer;
        private TypingPawPose displayedTypingPose = TypingPawPose.Rest;
        private double renderedPetSize;
        private readonly DispatcherTimer idleGestureTimer;
        private readonly DispatcherTimer rareIdleShowTimer;
        private readonly DispatcherTimer patrolTimer;
        private readonly DispatcherTimer attentionTimer;
        private readonly DispatcherTimer identityRevealTimer;
        private readonly Random idleGestureRandom = new Random();
        private RareIdleShowType lastRareIdleShow = RareIdleShowType.None;
        private readonly RareIdleDirector rareIdleDirector = new RareIdleDirector();
        private readonly List<double> orbitX = new List<double>();
        private readonly List<double> orbitY = new List<double>();
        private readonly List<double> orbitT = new List<double>();
        private double lastOrbitPlayAt = -1000;
        private double shyHoverStartedAt = -1;
        private double lastShyAt = -1000;
        private double cursorGreetingDwellAt = -1;
        private double lastCursorGreetingAt = -1000;
        private double visualAwayStartedAt = -1;
        private Point cursorGreetingPointer;
        private bool cursorGreetingPointerKnown;
        private bool patrolActive;
        private Point patrolOrigin;
        private double ambientInputGraceUntil = -1;
        private bool exclusiveAnimation;
        private bool patReactionActive;
        private int exclusiveAnimationToken;
        private string exclusiveStatus = "";
        private bool dropPreviewActive;
        private readonly string animationTraceFile;
        private DateTime dockTransitionStarted = DateTime.MinValue;
        private EngineOutput lastEngineOutput;
        private MeetingRadarWindow meetingWindow;
        private string lastAttentionZone = "";
        public string HotkeyLabel { get; private set; }
        public int PatCount;
        public int BalloonBopCount;

        private bool AttentionRitualActive
        {
            get
            {
                return AttentionRitualPolicy.BlocksAutonomousAction(
                    motion.CursorGreetingActive, motion.WakeStretchActive, motion.BubbleAttentionActive);
            }
        }

        private void YieldAttentionRitual(string source)
        {
            if (!AttentionRitualPolicy.ShouldYieldToUser(AttentionRitualActive, true)) return;
            TraceAnimation("ATTENTION_RITUAL yield source=" + source);
            ResetCursorGreetingObservation(true);
            motion.StopWakeStretch();
            motion.StopBubbleAttention();
        }

        public PetWindow(WorkMateApp app)
        {
            this.app = app;
            HotkeyLabel = "Alt+Q";
            Title = "WorkMate 桌宠";
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            Topmost = app.Store.Data.AlwaysOnTop;
            WindowStartupLocation = WindowStartupLocation.Manual;
            SizeToContent = SizeToContent.Manual;
            ShowActivated = false;
            WindowPrivacy.Bind(this, delegate { return app.Store.Data.HideFromCaptureEnabled; }, true);

            root = new Grid { Background = Brushes.Transparent, Cursor = Cursors.Hand };
            root.AllowDrop = PetInteractionPolicy.FileCarryEnabled || UpdateDropPolicy.Enabled;

            scaleTransform = new ScaleTransform(1, 1);
            tiltTransform = new RotateTransform(0);
            liftTransform = new TranslateTransform(0, 0);
            TransformGroup group = new TransformGroup();
            group.Children.Add(scaleTransform);
            group.Children.Add(tiltTransform);
            group.Children.Add(liftTransform);
            RadialGradientBrush shadowBrush = new RadialGradientBrush
            {
                Center = new Point(0.5, 0.5),
                GradientOrigin = new Point(0.5, 0.5),
                RadiusX = 0.55,
                RadiusY = 0.55
            };
            shadowBrush.GradientStops.Add(new GradientStop(Color.FromArgb(92, 45, 34, 43), 0));
            shadowBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 45, 34, 43), 1));
            contactShadow = new System.Windows.Shapes.Ellipse
            {
                Fill = shadowBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                IsHitTestVisible = false,
                Opacity = 0
            };
            root.Children.Add(contactShadow);

            motionLayer = new Grid
            {
                Background = Brushes.Transparent,
                RenderTransformOrigin = new Point(0.5, 1),
                RenderTransform = group
            };
            backImage = new Image { Stretch = Stretch.Uniform };
            frontImage = new Image { Stretch = Stretch.Uniform };
            backImageMirror = new ScaleTransform(1, 1);
            frontImageMirror = new ScaleTransform(1, 1);
            backImageShift = new TranslateTransform(0, 0);
            frontImageShift = new TranslateTransform(0, 0);
            TransformGroup backImageTransform = new TransformGroup();
            backImageTransform.Children.Add(backImageMirror);
            backImageTransform.Children.Add(backImageShift);
            TransformGroup frontImageTransform = new TransformGroup();
            frontImageTransform.Children.Add(frontImageMirror);
            frontImageTransform.Children.Add(frontImageShift);
            backImage.RenderTransformOrigin = new Point(.5, .5);
            frontImage.RenderTransformOrigin = new Point(.5, .5);
            backImage.RenderTransform = backImageTransform;
            frontImage.RenderTransform = frontImageTransform;
            RenderOptions.SetBitmapScalingMode(backImage, BitmapScalingMode.HighQuality);
            RenderOptions.SetBitmapScalingMode(frontImage, BitmapScalingMode.HighQuality);
            motionLayer.Children.Add(backImage);
            motionLayer.Children.Add(frontImage);
            root.Children.Add(motionLayer);

            overlay = new Canvas { Background = Brushes.Transparent, IsHitTestVisible = false };
            root.Children.Add(overlay);

            ambientText = Theme.Text("", 10, Theme.Muted, FontWeights.Bold);
            ambientText.TextWrapping = TextWrapping.NoWrap;
            ambientPill = new Border
            {
                Background = Theme.Brush("#F2ECE8"),
                CornerRadius = new CornerRadius(999),
                Padding = new Thickness(7, 3, 7, 3),
                Child = ambientText,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 3, 2, 0),
                IsHitTestVisible = false,
                Visibility = Visibility.Collapsed,
                Opacity = 0
            };
            System.Windows.Automation.AutomationProperties.SetName(ambientText, "宠物环境共感状态");
            root.Children.Add(ambientPill);

            identityText = Theme.Text("", 11, Brushes.White, FontWeights.SemiBold);
            identityText.TextWrapping = TextWrapping.NoWrap;
            identityText.TextTrimming = TextTrimming.CharacterEllipsis;
            identityPill = new Border
            {
                Background = Theme.Brush("#D92F2926"),
                CornerRadius = new CornerRadius(999),
                Padding = new Thickness(11, 5, 11, 5),
                Child = identityText,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 4),
                Opacity = 0
            };
            root.Children.Add(identityPill);
            Content = root;

            motion = new PetMotion(motionLayer, scaleTransform, tiltTransform, liftTransform, backImage, frontImage, overlay, contactShadow,
                app.Store.Data.PetSize * app.Store.Data.PetScaleRatio / 240.0);
            motion.SetReducedMotion(app.Store.Data.ReducedMotion);
            visualDirector.Force(BehaviorState.Idle, MonotonicSeconds());
            mover = new WindowMover(this);

            // AllowsTransparency 分层窗的全透明像素会被系统直接穿透，WM_NCHITTEST 无法把它们“救回来”。
            // 用 1/255 alpha 铺出真实命中面：肉眼不可见，但四边露出区可以稳定收到 MouseEnter。
            dockHitArea = new Border
            {
                Background = Theme.Brush("#01FFFFFF"),
                Opacity = 0,
                IsHitTestVisible = false
            };
            root.Children.Add(dockHitArea);

            peekDelayTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
            peekDelayTimer.Tick += delegate { peekDelayTimer.Stop(); EnterPeek(); };
            peekExitTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800) };
            peekExitTimer.Tick += delegate { peekExitTimer.Stop(); EnterDock(false); };
            // AllowsTransparency 分层窗在高 DPI / 远程桌面链路下偶尔吞 MouseEnter。
            // 80ms 只读取光标坐标的兜底轮询确保“移到露出带就探头”，不采集点击或输入内容。
            dockHoverTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) };
            dockHoverTimer.Tick += DockHoverTick;

            frameTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
            frameTimer.Tick += AnimationTick;
            typingPoseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(24) };
            typingPoseTimer.Tick += TypingPoseTick;
            idleGestureTimer = new DispatcherTimer();
            idleGestureTimer.Tick += IdleGestureTick;
            rareIdleShowTimer = new DispatcherTimer();
            rareIdleShowTimer.Tick += RareIdleShowTick;
            patrolTimer = new DispatcherTimer();
            patrolTimer.Tick += PatrolTick;
            attentionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            attentionTimer.Tick += AttentionTick;
            attentionTimer.Start();
            identityRevealTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(IdentityHoverPolicy.RevealDelayMs) };
            identityRevealTimer.Tick += delegate
            {
                identityRevealTimer.Stop();
                bool tucked = dockState == DockState.Docked || dockState == DockState.Tucking;
                if (!IdentityHoverPolicy.AllowsReveal(root.IsMouseOver, tucked, retreatActive, dragging)) return;
                FadeIdentity(1);
                TraceAnimation("IDENTITY reveal hover-intent");
            };
            animationTraceFile = Environment.GetEnvironmentVariable("WORKMATE_ANIMATION_TRACE_FILE");
            nodOffCooldownUntil = DateTime.Now.AddSeconds(NodOffSchedule.NextDelaySeconds(nodOffRandom));
            quietTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            quietTimer.Tick += delegate
            {
                if (QuietModeActive) UpdateChip();
                else ExitQuietMode(false);
            };
            focusRitualTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            focusRitualTimer.Tick += delegate
            {
                if (FocusRitualActive)
                {
                    motion.SetFocusRitualProgress(FocusRitualPolicy.ElapsedProgress(DateTime.Now,
                        focusRitualStarted, focusRitualUntil));
                    lastChipText = "";
                    UpdateChip();
                }
                else ExitFocusRitual(true);
            };

            singleClickTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(210) };
            singleClickTimer.Tick += delegate
            {
                singleClickTimer.Stop();
                if (!dragging) OpenBubble();
            };

            retreatReturnTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(30) };
            retreatReturnTimer.Tick += delegate
            {
                retreatReturnTimer.Stop();
                if (dockState != DockState.None) ExitDock(true);
            };
            retreatDockDelayTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(420) };
            retreatDockDelayTimer.Tick += RetreatDockDelayTick;

            root.MouseEnter += delegate { HandleDockMouseEnter(); };
            root.MouseLeave += delegate { HandleDockMouseLeave(); };
            root.MouseLeftButtonDown += PointerDown;
            root.MouseMove += PointerMove;
            root.MouseLeftButtonUp += PointerUp;
            root.LostMouseCapture += delegate { CancelSquishPress(); };
            root.MouseRightButtonUp += delegate { OpenBubble(); };
            if (PetInteractionPolicy.FileCarryEnabled || UpdateDropPolicy.Enabled)
            {
                root.DragEnter += PetDragEnter;
                root.DragOver += PetDragEnter;
                root.DragLeave += PetDragLeave;
                root.Drop += PetDrop;
            }
            SourceInitialized += SourceReady;
            Closed += OnClosed;

            RefreshPet();
        }

        public PetMotion Motion { get { return motion; } }
        public bool DockActive { get { return dockState != DockState.None; } }
        public bool QuietModeActive { get { return QuietCompanionPolicy.IsActive(DateTime.Now, quietUntil); } }
        public string QuietRemainingLabel { get { return QuietCompanionPolicy.RemainingLabel(DateTime.Now, quietUntil); } }
        public bool FocusRitualActive { get { return FocusRitualPolicy.IsActive(DateTime.Now, focusRitualUntil); } }
        public string FocusRitualRemainingLabel { get { return FocusRitualPolicy.RemainingLabel(DateTime.Now, focusRitualUntil); } }
        private bool FocusVisualActive { get { return flowActive || FocusRitualActive; } }
        public bool RetreatActive { get { return retreatActive; } }
        public string DockDebug { get { return dockState + "/" + dockEdge; } }

        private static double MonotonicSeconds()
        {
            return System.Diagnostics.Stopwatch.GetTimestamp() / (double)System.Diagnostics.Stopwatch.Frequency;
        }

        private void SourceReady(object sender, EventArgs e)
        {
            IntPtr handle = new WindowInteropHelper(this).Handle;
            HwndSource source = HwndSource.FromHwnd(handle);
            if (source != null) source.AddHook(WndProc);
            hotkeyRegistered = NativeMethods.RegisterHotKey(handle, HotkeyId, NativeMethods.MOD_ALT, 0x51);
            if (!hotkeyRegistered)
            {
                hotkeyRegistered = NativeMethods.RegisterHotKey(handle, HotkeyId, NativeMethods.MOD_ALT | NativeMethods.MOD_CONTROL, 0x51);
                HotkeyLabel = hotkeyRegistered ? "Ctrl+Alt+Q" : "快捷键不可用";
            }
            app.AttachRawInput(handle);
            RestorePosition();
        }

        private IntPtr WndProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (message == NativeMethods.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
            {
                Dispatcher.BeginInvoke(new Action(app.OpenQuickCapture), DispatcherPriority.Normal);
                handled = true;
            }
            else if (message == NativeSignals.WM_INPUT)
            {
                app.RouteRawInput(lParam);
            }
            else if (message == WmNcHitTest && !pointerCaptured && !dragging && !IsInteractivePixel(lParam))
            {
                // 仅 PNG 实体像素和可见身份胶囊接收鼠标；透明画布让给下方工作窗口。
                handled = true;
                return HtTransparent;
            }
            return IntPtr.Zero;
        }

        private bool IsInteractivePixel(IntPtr lParam)
        {
            long packed = lParam.ToInt64();
            double screenX = unchecked((short)(packed & 0xFFFF));
            double screenY = unchecked((short)((packed >> 16) & 0xFFFF));
            Point screenPoint = new Point(screenX, screenY);
            if (dockState != DockState.None && HitDockStrip(screenPoint)) return true;
            if (identityPill.Opacity > 0.05 && HitVisual(identityPill, screenPoint)) return true;
            if (frontImage.Opacity > 0.02 && HitSprite(frontImage, screenPoint)) return true;
            if (backImage.Opacity > 0.02 && HitSprite(backImage, screenPoint)) return true;
            return false;
        }

        /// <summary>边驻/探头时，整条露出带都可交互（悬停探头、点击恢复），不只 3px 握把。</summary>
        private bool HitDockStrip(Point screenPoint)
        {
            if (root.ActualWidth <= 0) return false;
            Point local = root.PointFromScreen(screenPoint);
            bool vertical = dockEdge == DockEdge.Left || dockEdge == DockEdge.Right;
            double v = dockState == DockState.Peeking
                ? (vertical ? Width : Height) * DockGeometry.PeekFraction
                : DockGeometry.SliverVisibleFor(Width, Height);
            switch (dockEdge)
            {
                case DockEdge.Right:
                    return local.X >= 0 && local.X <= v && local.Y >= 0 && local.Y < Height;
                case DockEdge.Left:
                    return local.X >= Width - v && local.X <= Width && local.Y >= 0 && local.Y < Height;
                case DockEdge.Bottom:
                    return local.Y >= 0 && local.Y <= v && local.X >= 0 && local.X < Width;
                case DockEdge.Top:
                    return local.Y >= Height - v && local.Y <= Height && local.X >= 0 && local.X < Width;
                default:
                    return false;
            }
        }

        private static bool HitVisual(FrameworkElement element, Point screenPoint)
        {
            if (element == null || element.ActualWidth <= 0 || element.ActualHeight <= 0) return false;
            Point local = element.PointFromScreen(screenPoint);
            return local.X >= 0 && local.Y >= 0 && local.X < element.ActualWidth && local.Y < element.ActualHeight;
        }

        private static bool HitSprite(Image image, Point screenPoint)
        {
            BitmapSource source = image == null ? null : image.Source as BitmapSource;
            if (source == null || image.ActualWidth <= 0 || image.ActualHeight <= 0) return false;
            Point local = image.PointFromScreen(screenPoint);

            double sourceAspect = source.PixelWidth / (double)Math.Max(1, source.PixelHeight);
            double controlAspect = image.ActualWidth / Math.Max(1.0, image.ActualHeight);
            double drawWidth = image.ActualWidth;
            double drawHeight = image.ActualHeight;
            double offsetX = 0;
            double offsetY = 0;
            if (controlAspect > sourceAspect)
            {
                drawWidth = drawHeight * sourceAspect;
                offsetX = (image.ActualWidth - drawWidth) / 2.0;
            }
            else if (controlAspect < sourceAspect)
            {
                drawHeight = drawWidth / sourceAspect;
                offsetY = (image.ActualHeight - drawHeight) / 2.0;
            }
            double normalizedX = (local.X - offsetX) / drawWidth;
            double normalizedY = (local.Y - offsetY) / drawHeight;
            return PetAssets.IsOpaque(source, normalizedX, normalizedY, 12);
        }

        /// <summary>
        /// 把当前 PNG 的 alpha 主体边界换算到屏幕 DIP。窗口透明留白不属于宠物，不能触发磁吸。
        /// 拖拽状态下精灵位移为 0；呼吸/轻倾造成的亚像素差由 2 DIP 接触容差吸收。
        /// </summary>
        private Rect SubjectBoundsOnScreen()
        {
            BitmapSource source = frontImage.Source as BitmapSource;
            if (source == null || frontImage.ActualWidth <= 0 || frontImage.ActualHeight <= 0)
                return new Rect(Left, Top, Math.Max(1, Width), Math.Max(1, Height));

            Point device = frontImage.PointToScreen(new Point(0, 0));
            PresentationSource presentation = PresentationSource.FromVisual(this);
            Point origin = presentation != null && presentation.CompositionTarget != null
                ? presentation.CompositionTarget.TransformFromDevice.Transform(device) : device;

            double sourceAspect = source.PixelWidth / (double)Math.Max(1, source.PixelHeight);
            double controlAspect = frontImage.ActualWidth / Math.Max(1.0, frontImage.ActualHeight);
            double drawWidth = frontImage.ActualWidth;
            double drawHeight = frontImage.ActualHeight;
            double offsetX = 0;
            double offsetY = 0;
            if (controlAspect > sourceAspect)
            {
                drawWidth = drawHeight * sourceAspect;
                offsetX = (frontImage.ActualWidth - drawWidth) / 2.0;
            }
            else if (controlAspect < sourceAspect)
            {
                drawHeight = drawWidth / sourceAspect;
                offsetY = (frontImage.ActualHeight - drawHeight) / 2.0;
            }
            Rect opaque = PetAssets.OpaqueBounds(source, 12);
            return new Rect(origin.X + offsetX + opaque.X * drawWidth,
                origin.Y + offsetY + opaque.Y * drawHeight,
                Math.Max(1, opaque.Width * drawWidth), Math.Max(1, opaque.Height * drawHeight));
        }

        private void OnClosed(object sender, EventArgs e)
        {
            dockRequestToken++;
            retreatDockDelayTimer.Stop();
            singleClickTimer.Stop();
            if (pressHoldTimer != null) pressHoldTimer.Stop();
            peekDelayTimer.Stop();
            peekExitTimer.Stop();
            dockHoverTimer.Stop();
            retreatReturnTimer.Stop();
            if (buildWaitTimer != null) buildWaitTimer.Stop();
            frameTimer.Stop();
            typingPoseTimer.Stop();
            idleGestureTimer.Stop();
            rareIdleShowTimer.Stop();
            patrolTimer.Stop();
            attentionTimer.Stop();
            identityRevealTimer.Stop();
            if (quietTimer != null) quietTimer.Stop();
            if (focusRitualTimer != null) focusRitualTimer.Stop();
            if (nodOffTimer != null) nodOffTimer.Stop();
            mover.Cancel();
            motion.Dispose();
            TraceAnimation("WINDOW_CLOSED motionDisposed=" + motion.IsDisposed);
            if (hotkeyRegistered)
            {
                IntPtr handle = new WindowInteropHelper(this).Handle;
                NativeMethods.UnregisterHotKey(handle, HotkeyId);
            }
        }

        // ---------------- 拖拽物理：速度采样 + 甩动检测，替代 DragMove ----------------

        private Point ScreenDip(MouseEventArgs e)
        {
            Point device = PointToScreen(e.GetPosition(root));
            PresentationSource source = PresentationSource.FromVisual(this);
            if (source != null && source.CompositionTarget != null)
                return source.CompositionTarget.TransformFromDevice.Transform(device);
            return device;
        }

        private void PointerDown(object sender, MouseButtonEventArgs e)
        {
            dockRequestToken++;
            retreatDockDelayTimer.Stop();
            Point localPoint = e.GetPosition(root);
            if (HighFivePolicy.CapturesPointer(motion.HighFiveActive, motion.HighFiveCelebrating))
            {
                RespondToHighFive();
                e.Handled = true;
                return;
            }
            if (motion.HitBalloon(localPoint))
            {
                BalloonBopCount++;
                motion.BopBalloon(localPoint);
                TraceAnimation("BALLOON bop count=" + BalloonBopCount);
                e.Handled = true;
                return;
            }
            if (e.ClickCount >= 2)
            {
                singleClickTimer.Stop();
                app.OpenWorkbench("memos");
                e.Handled = true;
                return;
            }
            // 必须在窗口从屏外恢复前取真实屏幕坐标；否则旧事件的局部坐标会被套到新窗口位置，拖拽首帧跳动。
            Point pointer = ScreenDip(e);
            if (dockState != DockState.None)
            {
                // 点击边驻/探头中的宠物：先恢复，再进入正常拖拽；这次点击不开气泡
                ExitDock(false);
                suppressClickOnce = true;
            }
            grabOffsetDip = new Point(Left - pointer.X, Top - pointer.Y);
            pointerCaptured = true;
            dragging = false;
            dragX.Clear();
            dragT.Clear();
            dragY.Clear();
            pressLocalPoint = e.GetPosition(root);
            if (pressHoldTimer == null)
            {
                pressHoldTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
                pressHoldTimer.Tick += delegate { pressHoldTimer.Stop(); EnterSquish(); };
            }
            pressHoldTimer.Stop();
            pressHoldTimer.Start();
            root.CaptureMouse();
        }

        private void EnterSquish()
        {
            if (!pointerCaptured || dragging || squishHolding || dockState != DockState.None) return;
            if (Mouse.LeftButton != MouseButtonState.Pressed) return;
            squishHolding = true;
            double bias = root.ActualWidth > 0 ? (pressLocalPoint.X / root.ActualWidth - 0.5) * 2 : 0;
            motion.SetSquishHold(true, bias);
        }

        private void CancelSquishPress()
        {
            if (pressHoldTimer != null) pressHoldTimer.Stop();
            if (squishHolding)
            {
                squishHolding = false;
                motion.SetSquishHold(false, 0);
            }
        }

        private void PointerMove(object sender, MouseEventArgs e)
        {
            // 无按键悬停：朝向偏置（伪视线）+ 摸头检测
            if (!pointerCaptured || e.LeftButton != MouseButtonState.Pressed)
            {
                if (root.ActualWidth > 0)
                {
                    Point local = e.GetPosition(root);
                    double nowHover = Environment.TickCount / 1000.0;
                    hoverX.Add(local.X);
                    hoverT.Add(nowHover);
                    while (hoverT.Count > 0 && nowHover - hoverT[0] > 2.5)
                    {
                        hoverX.RemoveAt(0);
                        hoverT.RemoveAt(0);
                    }
                    // 抚摸迎手：划动换向时给小回弹（手感，不抢戏，150ms 限频）
                    if (hoverX.Count >= 4 && nowHover - lastStrokeBounce > 0.15)
                    {
                        int n = hoverX.Count;
                        double d1 = hoverX[n - 2] - hoverX[n - 3];
                        double d2 = hoverX[n - 1] - hoverX[n - 2];
                        if (Math.Abs(d1) > 4 && Math.Abs(d2) > 4 && ((d1 > 0) != (d2 > 0)))
                        {
                            lastStrokeBounce = nowHover;
                            motion.Squash(0.04);
                        }
                    }
                    if (nowHover - lastPat > 8 && PatLogic.IsPat(hoverX, hoverT))
                    {
                        lastPat = nowHover;
                        PatCount++;
                        hoverX.Clear();
                        hoverT.Clear();
                        double patBias = root.ActualWidth > 0 ? (local.X / root.ActualWidth - 0.5) * 2 : 0;
                        PlayPatReaction(patBias);
                        motion.Heart();
                        motion.Squash(0.08);
                    }
                }
                return;
            }
            Point pointer = ScreenDip(e);
            if (!dragging)
            {
                if (Math.Abs(pointer.X + grabOffsetDip.X - Left) < 5 && Math.Abs(pointer.Y + grabOffsetDip.Y - Top) < 5) return;
                dragging = true;
                if (motion.PlayfulActive || motion.ShyActive || motion.BalloonActive || motion.HighFiveActive
                    || patrolActive || patReactionActive)
                {
                    motion.StopPlayfulShow();
                    motion.StopShy();
                    motion.StopBalloonGame();
                    CancelExclusiveAnimation(false);
                }
                CancelSquishPress(); // 拖起来了就不是捏脸
                singleClickTimer.Stop();
                mover.Cancel();
                motion.SetDragging(true);
            }
            double nextLeft = pointer.X + grabOffsetDip.X;
            double nextTop = pointer.Y + grabOffsetDip.Y;
            double dragNow = MonotonicSeconds();
            if (dragT.Count > 0)
            {
                double dt = dragNow - dragT[dragT.Count - 1];
                if (dt > 0.004 && dt < 0.20)
                    motion.SetDragVelocity((nextLeft - dragX[dragX.Count - 1]) / dt, (nextTop - dragY[dragY.Count - 1]) / dt);
            }
            Left = nextLeft;
            Top = nextTop;
            dragX.Add(Left);
            dragY.Add(Top);
            dragT.Add(dragNow);
            if (!dizzyTriggered && EdgeLogic.ShouldDizzy(dragX, dragT, dragNow))
            {
                // 拎着晃三下 → 晕眩转圈冒星星（即时反馈，不打断拖拽）
                dizzyTriggered = true;
                motion.SetDizzy(true);
            }
            if (dragX.Count > 240)
            {
                dragX.RemoveRange(0, 120);
                dragY.RemoveRange(0, 120);
                dragT.RemoveRange(0, 120);
            }
        }

        private void AttentionTick(object sender, EventArgs e)
        {
            bool eligible = !app.Store.Data.ReducedMotion && !dragging && !pointerCaptured && !retreatActive
                && dockState == DockState.None && !exclusiveAnimation && !dropPreviewActive
                && (visualBehavior == BehaviorState.Idle || visualBehavior == BehaviorState.Reading || visualBehavior == BehaviorState.Thinking)
                && PresentationSource.FromVisual(this) != null;
            AttentionVector vector = new AttentionVector();
            if (eligible)
            {
                Rect subject = SubjectBoundsOnScreen();
                System.Drawing.Point deviceCursor = System.Windows.Forms.Cursor.Position;
                PresentationSource source = PresentationSource.FromVisual(this);
                Point cursor = source != null && source.CompositionTarget != null
                    ? source.CompositionTarget.TransformFromDevice.Transform(new Point(deviceCursor.X, deviceCursor.Y))
                    : new Point(deviceCursor.X, deviceCursor.Y);
                double centerX = subject.Left + subject.Width / 2.0;
                double centerY = subject.Top + subject.Height * 0.44;
                double radius = Math.Min(360, Math.Max(180, renderedPetSize * 3.2));
                double deadZone = Math.Max(18, renderedPetSize * 0.24);
                double relativeX = cursor.X - centerX;
                double relativeY = cursor.Y - centerY;
                vector = CursorAttentionPolicy.Resolve(relativeX, relativeY, radius, deadZone);
                if (visualBehavior == BehaviorState.Idle && !PetInteractionPolicy.HasActiveCarry(app.Store.Data.CarryItems.Count))
                {
                    ObserveCursorOrbit(relativeX, relativeY, MonotonicSeconds());
                    ObserveShyGaze(Math.Sqrt(relativeX * relativeX + relativeY * relativeY), MonotonicSeconds());
                    ObserveCursorGreeting(cursor, relativeX, relativeY, MonotonicSeconds());
                }
                else ResetCursorGreetingObservation(true);
            }
            else { ClearCursorOrbit(); shyHoverStartedAt = -1; ResetCursorGreetingObservation(true); }
            motion.SetAttentionTarget(vector.X, vector.Y);
            string zone = Math.Abs(vector.X) < 0.12 && Math.Abs(vector.Y) < 0.12 ? "center"
                : Math.Abs(vector.X) >= Math.Abs(vector.Y) ? (vector.X < 0 ? "left" : "right")
                : (vector.Y < 0 ? "up" : "down");
            if (zone != lastAttentionZone)
            {
                lastAttentionZone = zone;
                TraceAnimation("ATTENTION zone=" + zone + " x=" + vector.X.ToString("0.00") + " y=" + vector.Y.ToString("0.00"));
            }
        }

        private void ObserveCursorOrbit(double x, double y, double now)
        {
            double distance = Math.Sqrt(x * x + y * y);
            double minRadius = Math.Max(24, renderedPetSize * 0.46);
            double maxRadius = Math.Min(220, Math.Max(100, renderedPetSize * 2.7));
            if (distance < minRadius || distance > maxRadius)
            {
                ClearCursorOrbit();
                return;
            }
            if (orbitT.Count > 0 && now - orbitT[orbitT.Count - 1] > 0.24) ClearCursorOrbit();
            if (orbitX.Count > 0)
            {
                double dx = x - orbitX[orbitX.Count - 1];
                double dy = y - orbitY[orbitY.Count - 1];
                if (dx * dx + dy * dy < 9) return;
            }
            orbitX.Add(x);
            orbitY.Add(y);
            orbitT.Add(now);
            while (orbitT.Count > 0 && now - orbitT[0] > 1.8)
            {
                orbitX.RemoveAt(0);
                orbitY.RemoveAt(0);
                orbitT.RemoveAt(0);
            }
            if (now - lastOrbitPlayAt < AnimationPolicy.OrbitGestureCooldownSeconds) return;
            if (!CursorOrbitPolicy.IsDeliberateOrbit(orbitX, orbitY, orbitT)) return;
            lastOrbitPlayAt = now;
            ClearCursorOrbit();
            if (PlayIdleShow(RareIdleShowType.HulaHoop, true))
                TraceAnimation("ORBIT accepted cooldown=" + AnimationPolicy.OrbitGestureCooldownSeconds);
        }

        private void ClearCursorOrbit()
        {
            orbitX.Clear();
            orbitY.Clear();
            orbitT.Clear();
        }

        private void ObserveShyGaze(double distance, double now)
        {
            double faceRadius = Math.Max(14, renderedPetSize * 0.28);
            if (distance > faceRadius)
            {
                shyHoverStartedAt = -1;
                return;
            }
            if (shyHoverStartedAt < 0) shyHoverStartedAt = now;
            double dwell = now - shyHoverStartedAt;
            if (!ShyGazePolicy.ShouldTrigger(distance, faceRadius, dwell, now - lastShyAt)) return;
            shyHoverStartedAt = -1;
            lastShyAt = now;
            PlayShyReaction();
        }

        private void ObserveCursorGreeting(Point cursor, double relativeX, double relativeY, double now)
        {
            if (motion.CursorGreetingActive) return;
            if (!cursorGreetingPointerKnown)
            {
                cursorGreetingPointer = cursor;
                cursorGreetingPointerKnown = true;
                cursorGreetingDwellAt = now;
                return;
            }
            double dx = cursor.X - cursorGreetingPointer.X;
            double dy = cursor.Y - cursorGreetingPointer.Y;
            if (dx * dx + dy * dy > 36)
            {
                cursorGreetingPointer = cursor;
                cursorGreetingDwellAt = now;
                return;
            }
            double distance = Math.Sqrt(relativeX * relativeX + relativeY * relativeY);
            double dwell = cursorGreetingDwellAt < 0 ? 0 : now - cursorGreetingDwellAt;
            bool busy = CursorGreetingPolicy.IsBusy(QuietModeActive, buildWait, FocusVisualActive,
                exclusiveAnimation, nodOffDir.Active, motion.PlayfulActive, motion.ShyActive,
                motion.BalloonActive, patrolActive);
            if (!CursorGreetingPolicy.ShouldTrigger(distance, renderedPetSize, root.IsMouseOver,
                dwell, now - lastCursorGreetingAt, app.Store.Data.ReducedMotion, busy)) return;
            lastCursorGreetingAt = now;
            cursorGreetingDwellAt = -1;
            double bias = Math.Max(-1, Math.Min(1, relativeX / Math.Max(1, renderedPetSize)));
            motion.StartCursorGreeting(bias);
            TraceAnimation("CURSOR_GREETING bias=" + bias.ToString("0.00")
                + " active=" + motion.CursorGreetingActive);
        }

        private void ResetCursorGreetingObservation(bool stopActive)
        {
            cursorGreetingDwellAt = -1;
            cursorGreetingPointerKnown = false;
            if (stopActive) motion.StopCursorGreeting();
        }

        private void PointerUp(object sender, MouseButtonEventArgs e)
        {
            if (pointerCaptured && root.IsMouseCaptured) root.ReleaseMouseCapture();
            pointerCaptured = false;
            if (pressHoldTimer != null) pressHoldTimer.Stop();
            if (squishHolding)
            {
                // 捏脸松手：回弹 + 一颗爱心收尾，不打断当前行为
                squishHolding = false;
                motion.SetSquishHold(false, 0);
                motion.Squash(0.16);
                motion.Heart();
                return;
            }
            if (!dragging)
            {
                if (suppressClickOnce) { suppressClickOnce = false; return; }
                singleClickTimer.Stop();
                singleClickTimer.Start();
                return;
            }
            suppressClickOnce = false;
            dragging = false;
            dizzyTriggered = false;
            motion.SetDizzy(false);
            bool gliding = HandleRelease();
            motion.SetDragging(false, !gliding);
        }

        private bool HandleRelease()
        {
            Rect area = WindowPlacement.WorkAreaFor(this);
            ApplyResponsiveDimensions(area, true);
            Rect subject = SubjectBoundsOnScreen();
            double distRight = area.Right - subject.Right;
            double distLeft = subject.Left - area.Left;
            double distBottom = area.Bottom - subject.Bottom;
            double distTop = subject.Top - area.Top;
            DockEdge edge = DockEdge.Right;
            double dist = distRight;
            if (distLeft < dist) { dist = distLeft; edge = DockEdge.Left; }
            if (distBottom < dist) { dist = distBottom; edge = DockEdge.Bottom; }
            if (distTop < dist) { dist = distTop; edge = DockEdge.Top; }
            dist = Math.Max(0, dist);
            TraceAnimation("RELEASE subject=" + subject.ToString() + " edge=" + edge + " distanceDip=" + dist.ToString("0.0"));
            bool horizontal = edge == DockEdge.Left || edge == DockEdge.Right;

            double nowT = MonotonicSeconds();
            List<double> recentX = new List<double>();
            List<double> recentT = new List<double>();
            for (int i = 0; i < dragX.Count; i++)
            {
                if (nowT - dragT[i] <= 0.9)
                {
                    recentX.Add(horizontal ? dragX[i] : dragY[i]);
                    recentT.Add(dragT[i]);
                }
            }
            int reversals = horizontal ? EdgeLogic.CountShakeReversals(recentX, EdgeLogic.ShakeMinDeltaDip) : 0;
            double velocity = EdgeLogic.EstimateVelocity(recentX, recentT, nowT, edge == DockEdge.Right || edge == DockEdge.Bottom);

            bool corridor = false;
            if (app.Store.Data.EdgeSnapEnabled)
            {
                List<MonitorInfoEx> all = MonitorHelper.All();
                IntPtr handle = new WindowInteropHelper(this).Handle;
                MonitorInfoEx current = MonitorHelper.FindByHandle(all, NativeMethods.MonitorFromWindow(handle, 2));
                if (current != null) corridor = MonitorHelper.IsCorridorEdge(current, edge, all);
            }

            ReleaseAction action = app.Store.Data.EdgeSnapEnabled
                ? EdgeLogic.ClassifyRelease(dist, velocity, reversals, corridor)
                : ReleaseAction.None;

            if (action == ReleaseAction.Retreat)
            {
                dockEdge = edge;
                motion.Squash(0.22);
                ScheduleRetreatDock();
                return false;
            }
            if (action == ReleaseAction.Fin)
            {
                dockEdge = edge;
                EnterDock(false);
                return false;
            }
            if (action == ReleaseAction.Snap)
            {
                dockEdge = edge;
                dockState = DockState.Snapped;
                motion.Squash(0.12);
                // 松手即磁吸并缩入：从当前位置到蜷睡边驻的总时长不超过 0.3 秒。
                EnterDock(false);
                return false;
            }
            DragInertiaPlan inertia = DragInertiaPolicy.Resolve(dragX, dragY, dragT, nowT, area,
                Left, Top, Width, Height, app.Store.Data.ReducedMotion);
            if (inertia.Valid)
            {
                TraceAnimation("RELEASE inertia speed=" + inertia.Speed.ToString("0")
                    + " target=" + inertia.Target + " durationMs=" + inertia.DurationMs);
                mover.AnimateTo(inertia.Target.X, inertia.Target.Y, inertia.DurationMs, delegate
                {
                    motion.Land();
                    motion.Squash(0.045);
                    SavePosition();
                    TraceAnimation("RELEASE inertia-land");
                });
                return true;
            }
            // 无吸附动作：钳回工作区内，防止宠物被拖丢到屏幕外
            Left = Math.Max(area.Left + 6, Math.Min(area.Right - Width - 6, Left));
            Top = Math.Max(area.Top + 6, Math.Min(area.Bottom - Height - 6, Top));
            SavePosition();
            return false;
        }

        private void SavePosition()
        {
            double saveLeft = Left;
            double saveTop = Top;
            if (dockState == DockState.Tucking || dockState == DockState.Docked || dockState == DockState.Peeking)
            {
                // 边驻/探头时落盘"贴边未缩入"的位置：下次启动不会以无状态之身还原始缩略坐标
                Rect area = WindowPlacement.WorkAreaFor(this);
                System.Windows.Point flush = DockGeometry.FlushTarget(dockEdge, area, Width, Height,
                    Math.Max(area.Top + 6, Math.Min(area.Bottom - Height - 6, Top)));
                if (dockEdge == DockEdge.Top || dockEdge == DockEdge.Bottom)
                    flush.X = Math.Max(area.Left + 6, Math.Min(area.Right - Width - 6, Left));
                saveLeft = flush.X;
                saveTop = flush.Y;
            }
            app.Store.Data.PetLeft = saveLeft;
            app.Store.Data.PetTop = saveTop;
            app.Store.Data.DockSide = dockState == DockState.None ? "" : DockGeometry.PersistedEdge(dockEdge);
            app.Store.Save();
        }

        // ---------------- 四边缩入 + 探头（v1.5）----------------

        /// <summary>测试钩子：直接进入指定边驻（tell dock-top 等）。</summary>
        public void DebugDock(DockEdge edge)
        {
            dockEdge = edge;
            dockState = DockState.Snapped;
            EnterDock(false);
        }

        private void EnterDock(bool dozed)
        {
            dockRequestToken++;
            retreatDockDelayTimer.Stop();
            CancelExclusiveAnimation(false);
            idleGestureTimer.Stop();
            peekDelayTimer.Stop();
            peekExitTimer.Stop();
            identityRevealTimer.Stop();
            Rect area = WindowPlacement.WorkAreaFor(this);
            bool returningFromPeek = dockState == DockState.Peeking;
            dockState = DockState.Tucking;
            RefreshAmbientPresenceVisual(false);
            motion.SetDocked(true);
            ApplyDockSpriteClip(true);
            dockTransitionStarted = DateTime.Now;
            TraceAnimation("DOCK begin edge=" + dockEdge + " returning=" + returningFromPeek);
            dozeUntil = dozed ? DateTime.Now.AddMinutes(30) : DateTime.MinValue;
            if (dozed) retreatReturnTimer.Start();
            System.Windows.Point target = DockGeometry.SliverTarget(dockEdge, area, Width, Height,
                Math.Max(area.Top + 6, Math.Min(area.Bottom - Height - 6, Top)));
            if (dockEdge == DockEdge.Top || dockEdge == DockEdge.Bottom)
                target.X = Math.Max(area.Left + 6, Math.Min(area.Right - Width - 6, Left));
            FadeIdentity(0);
            LayoutDockHitArea(true);
            if (!dockHoverTimer.IsEnabled) dockHoverTimer.Start();
            motion.Squash(0.1);
            bool side = dockEdge == DockEdge.Left || dockEdge == DockEdge.Right;
            if (side && PetAssets.SupportsAnimation(app.Store.Data.PetId))
            {
                MirrorAnimation(dockEdge == DockEdge.Left);
                if (returningFromPeek) PlayAnimation("dock-right", 24, 19, -1, 45, false, null);
                else PlayAnimation("dock-right", AnimationPolicy.DockTuckStartFrame,
                    AnimationPolicy.DockTuckEndFrame, AnimationPolicy.DockTuckFrameStep,
                    AnimationPolicy.DockTuckFrameIntervalMs, false, null);
            }
            else
            {
                StopAnimation(false);
                MirrorAnimation(false);
                motion.SetPose(PetAssets.Get(app.Store.Data.PetId, "sleep"));
            }
            int tuckDuration = returningFromPeek ? 260 : AnimationPolicy.DockTuckDurationMs;
            AnimateDockSpriteShift(true, tuckDuration);
            mover.AnimateTo(target.X, target.Y, tuckDuration, delegate
            {
                dockState = DockState.Docked;
                if (side && PetAssets.SupportsAnimation(app.Store.Data.PetId)) ShowAnimationFrame("dock-right", 19);
                SavePosition();
                TraceAnimation("DOCK docked elapsedMs=" + (DateTime.Now - dockTransitionStarted).TotalMilliseconds.ToString("0"));
            });
        }

        private void EnterPeek()
        {
            if (dockState != DockState.Docked || DateTime.Now <= dozeUntil) return;
            peekDelayTimer.Stop();
            peekExitTimer.Stop();
            dockState = DockState.Peeking;
            ApplyDockSpriteClip(false);
            Rect area = WindowPlacement.WorkAreaFor(this);
            System.Windows.Point target = DockGeometry.PeekTarget(dockEdge, area, Width, Height,
                Math.Max(area.Top + 6, Math.Min(area.Bottom - Height - 6, Top)));
            if (dockEdge == DockEdge.Top || dockEdge == DockEdge.Bottom)
                target.X = Math.Max(area.Left + 6, Math.Min(area.Right - Width - 6, Left));
            FadeIdentity(1);
            bool side = dockEdge == DockEdge.Left || dockEdge == DockEdge.Right;
            if (side && PetAssets.SupportsAnimation(app.Store.Data.PetId))
            {
                MirrorAnimation(dockEdge == DockEdge.Left);
                PlayAnimation("dock-right", 20, 24, 1, 62, false, null);
            }
            AnimateDockSpriteShift(false, 310);
            mover.AnimateTo(target.X, target.Y, 310, null);
        }

        private void ExitDock(bool animate)
        {
            dockRequestToken++;
            retreatDockDelayTimer.Stop();
            if (dockState == DockState.None) return;
            dockState = DockState.None;
            RefreshAmbientPresenceVisual(false);
            dozeUntil = DateTime.MinValue;
            retreatReturnTimer.Stop();
            peekDelayTimer.Stop();
            peekExitTimer.Stop();
            dockHoverTimer.Stop();
            LayoutDockHitArea(false);
            StopAnimation(false);
            AnimateSpriteShift(0, 0, animate ? 240 : 0);
            Rect area = WindowPlacement.WorkAreaFor(this);
            double targetLeft = Math.Max(area.Left + 6, Math.Min(area.Right - Width - 6, Left));
            double targetTop = Math.Max(area.Top + 6, Math.Min(area.Bottom - Height - 6, Top));
            if (animate)
            {
                motion.Squash(0.14);
                mover.AnimateTo(targetLeft, targetTop, 240, delegate
                {
                    MirrorAnimation(false);
                    SetSpriteShift(0, 0);
                    motion.SetDocked(false);
                    ApplyDockSpriteClip(false);
                    SavePosition();
                    ResumeBehaviorVisual();
                });
            }
            else
            {
                Left = targetLeft;
                Top = targetTop;
                MirrorAnimation(false);
                SetSpriteShift(0, 0);
                motion.SetDocked(false);
                ApplyDockSpriteClip(false);
                SavePosition();
                ResumeBehaviorVisual();
            }
        }

        private void ScheduleRetreatDock()
        {
            scheduledDockToken = ++dockRequestToken;
            retreatDockDelayTimer.Stop();
            retreatDockDelayTimer.Start();
            TraceAnimation("DOCK_DELAY scheduled token=" + scheduledDockToken);
        }

        private void RetreatDockDelayTick(object sender, EventArgs e)
        {
            retreatDockDelayTimer.Stop();
            bool valid = scheduledDockToken == dockRequestToken && !pointerCaptured && !dragging
                && dockState == DockState.None;
            TraceAnimation("DOCK_DELAY fire token=" + scheduledDockToken + " current=" + dockRequestToken
                + " valid=" + valid);
            if (valid) EnterDock(true);
        }

        public void PlayDockDelayCancelDemo()
        {
            dockEdge = DockEdge.Right;
            ScheduleRetreatDock();
            dockRequestToken++;
            TraceAnimation("DOCK_DELAY demo-cancel current=" + dockRequestToken);
        }

        /// <summary>进入露出带：取消任何旧缩回任务；边驻状态经 120ms 防误触后探头。</summary>
        private void HandleDockMouseEnter()
        {
            peekExitTimer.Stop();
            if (dockState == DockState.Docked && DateTime.Now > dozeUntil)
            {
                peekDelayTimer.Stop();
                peekDelayTimer.Start();
            }
            else
            {
                peekDelayTimer.Stop();
                identityRevealTimer.Stop();
                identityRevealTimer.Start();
            }
        }

        /// <summary>离开露出带：无条件取消尚未触发的探头；只有已经探头才延迟缩回。</summary>
        private void HandleDockMouseLeave()
        {
            peekDelayTimer.Stop();
            identityRevealTimer.Stop();
            FadeIdentity(0);
            motion.SetAttentionTarget(0, 0);
            hoverX.Clear();
            hoverT.Clear();
            peekExitTimer.Stop();
            if (dockState == DockState.Peeking) peekExitTimer.Start();
        }

        private void DockHoverTick(object sender, EventArgs e)
        {
            if (dockState == DockState.None)
            {
                dockHoverTimer.Stop();
                return;
            }
            System.Drawing.Point cursor = System.Windows.Forms.Cursor.Position;
            bool inside = HitDockStrip(new Point(cursor.X, cursor.Y));
            if (dockState == DockState.Docked && inside && DateTime.Now > dozeUntil)
            {
                peekExitTimer.Stop();
                if (!peekDelayTimer.IsEnabled) peekDelayTimer.Start();
            }
            else if (dockState == DockState.Docked && !inside)
            {
                peekDelayTimer.Stop();
            }
            else if (dockState == DockState.Peeking && inside)
            {
                peekExitTimer.Stop();
            }
            else if (dockState == DockState.Peeking && !inside && !peekExitTimer.IsEnabled)
            {
                peekExitTimer.Start();
            }
        }

        /// <summary>边驻命中面保持 1/255 alpha，不再绘制任何握把线；露出的身体本身就是把手。</summary>
        private void LayoutDockHitArea(bool show)
        {
            if (dockHitArea != null)
            {
                dockHitArea.Opacity = show ? 1 : 0;
                dockHitArea.IsHitTestVisible = show;
            }
            if (!show) return;
            bool vertical = dockEdge == DockEdge.Left || dockEdge == DockEdge.Right;
            if (dockHitArea != null)
            {
                dockHitArea.Width = vertical ? Width * DockGeometry.PeekFraction : double.NaN;
                dockHitArea.Height = vertical ? double.NaN : Height * DockGeometry.PeekFraction;
                dockHitArea.VerticalAlignment = vertical ? VerticalAlignment.Stretch :
                    (dockEdge == DockEdge.Bottom ? VerticalAlignment.Top : VerticalAlignment.Bottom);
                dockHitArea.HorizontalAlignment = vertical ?
                    (dockEdge == DockEdge.Right ? HorizontalAlignment.Left : HorizontalAlignment.Right) : HorizontalAlignment.Stretch;
            }
        }

        /// <summary>二次启动的可见性兜底：退出边驻、清除可能悬挂的淡出动画，并夹回当前工作区。</summary>
        public void RestoreForActivation()
        {
            if (dockState != DockState.None) ExitDock(false);
            BeginAnimation(OpacityProperty, null);
            Opacity = 1;
            Topmost = app.Store.Data.AlwaysOnTop;
            if (!retreatActive)
            {
                if (!IsVisible) Show();
                motion.SetHidden(false);
            }

            Rect area = WindowPlacement.WorkAreaFor(this);
            double safeLeft = Math.Max(area.Left + 8, Math.Min(area.Right - Width - 8, Left));
            double safeTop = Math.Max(area.Top + 8, Math.Min(area.Bottom - Height - 8, Top));
            if (double.IsNaN(safeLeft) || double.IsInfinity(safeLeft)) safeLeft = area.Right - Width - 24;
            if (double.IsNaN(safeTop) || double.IsInfinity(safeTop)) safeTop = area.Bottom - Height - 12;
            if (Math.Abs(Left - safeLeft) > 0.1 || Math.Abs(Top - safeTop) > 0.1)
            {
                Left = safeLeft;
                Top = safeTop;
                SavePosition();
            }
        }

        // ---------------- 演示退避（全屏/投屏/手动演示模式）----------------

        public void SetRetreat(bool retreat)
        {
            if (retreat == retreatActive) return;
            if (retreat)
            {
                YieldAttentionRitual("retreat");
                CancelExclusiveAnimation(false);
                if (nodOffDir.Active) AbortNodOff("retreat");
                typingPoseTimer.Stop();
            }
            retreatActive = retreat;
            RefreshAmbientPresenceVisual(false);
            UpdateVisibility();
            if (!retreat)
            {
                motion.EnterReaction(0.6);
                motion.Squash(0.1);
                ResumeBehaviorVisual();
            }
        }

        private void UpdateVisibility()
        {
            bool visible = !retreatActive;
            if (visible && !IsVisible)
            {
                Show();
                motion.SetHidden(false);
            }
            else if (!visible && IsVisible)
            {
                Hide();
                motion.SetHidden(true);
            }
        }

        // ---------------- 多屏跟随 ----------------

        public bool CanMigrate()
        {
            return app.Store.Data.FollowMonitorEnabled && !dragging && dockState == DockState.None && !retreatActive && !migrating && mover.IsAnimating == false;
        }

        public void MigrateToMonitor(IntPtr monitor)
        {
            if (migrating) return;
            List<MonitorInfoEx> all = MonitorHelper.All();
            MonitorInfoEx target = MonitorHelper.FindByHandle(all, monitor);
            if (target == null) return;
            migrating = true;

            Rect currentArea = WindowPlacement.WorkAreaFor(this);
            double fx = currentArea.Width > Width ? (Left - currentArea.Left) / (currentArea.Width - Width) : 0.5;
            double fy = currentArea.Height > Height ? (Top - currentArea.Top) / (currentArea.Height - Height) : 0.5;
            fx = Math.Max(0, Math.Min(1, fx));
            fy = Math.Max(0, Math.Min(1, fy));

            Matrix fromDevice = Matrix.Identity;
            PresentationSource source = PresentationSource.FromVisual(this);
            if (source != null && source.CompositionTarget != null) fromDevice = source.CompositionTarget.TransformFromDevice;
            Point tl = fromDevice.Transform(new Point(target.Work.Left, target.Work.Top));
            Point br = fromDevice.Transform(new Point(target.Work.Right, target.Work.Bottom));
            Rect targetArea = new Rect(tl, br);
            ApplyResponsiveDimensions(targetArea, true);
            double newLeft = targetArea.Left + fx * Math.Max(0, targetArea.Width - Width);
            double newTop = targetArea.Top + fy * Math.Max(0, targetArea.Height - Height);

            if (app.Store.Data.ReducedMotion)
            {
                Left = newLeft;
                Top = newTop;
                migrating = false;
                SavePosition();
                return;
            }
            DoubleAnimation fadeOut = new DoubleAnimation(0, TimeSpan.FromMilliseconds(140));
            fadeOut.EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn };
            fadeOut.Completed += delegate
            {
                Left = newLeft;
                Top = newTop;
                SavePosition();
                DoubleAnimation fadeIn = new DoubleAnimation(1, TimeSpan.FromMilliseconds(180));
                fadeIn.EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut };
                fadeIn.Completed += delegate { migrating = false; };
                BeginAnimation(OpacityProperty, fadeIn);
                motion.Squash(0.14);
            };
            BeginAnimation(OpacityProperty, fadeOut);
        }

        // ---------------- 150 帧动画播放器（6 张 5×5 精灵表，按需解码）----------------

        private void PlayAnimation(string sequence, int start, int end, int step, int milliseconds, bool loop, Action completed)
        {
            StartAnimation(sequence, start, end, step, milliseconds, loop, completed, false);
        }

        private void PlayAnimationSmooth(string sequence, int start, int end, int step, int milliseconds, bool loop, Action completed)
        {
            StartAnimation(sequence, start, end, step, milliseconds, loop, completed, true);
        }

        private void StartAnimation(string sequence, int start, int end, int step, int milliseconds, bool loop, Action completed, bool smoothStart)
        {
            BitmapSource first = PetAssets.GetAnimationFrame(app.Store.Data.PetId, sequence, start);
            if (first == null)
            {
                if (completed != null) completed();
                return;
            }
            animationSequence = sequence;
            animationStart = start;
            animationFrame = start;
            animationEnd = end;
            animationStep = step == 0 ? 1 : step;
            animationLoop = loop;
            animationCompleted = completed;
            frameTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(34, milliseconds));
            int token = ++animationPlaybackToken;
            frameTimer.Stop();
            TraceAnimation("START sequence=" + sequence + " frame=" + start + " end=" + end + " step=" + animationStep + " intervalMs=" + frameTimer.Interval.TotalMilliseconds.ToString("0") + " loop=" + loop + " smooth=" + smoothStart);
            if (!smoothStart || app.Store.Data.ReducedMotion)
            {
                motion.SetFrame(first);
                frameTimer.Start();
                return;
            }
            motion.SetPose(first);
            DispatcherTimer directorDelay = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(145) };
            directorDelay.Tick += delegate
            {
                directorDelay.Stop();
                if (token == animationPlaybackToken && animationSequence == sequence) frameTimer.Start();
            };
            directorDelay.Start();
        }

        private void AnimationTick(object sender, EventArgs e)
        {
            if (animationSequence.Length == 0) { frameTimer.Stop(); return; }
            bool reached = animationStep > 0 ? animationFrame >= animationEnd : animationFrame <= animationEnd;
            if (reached)
            {
                if (animationLoop)
                {
                    animationFrame = animationStart;
                    ShowAnimationFrameInternal(animationSequence, animationFrame, false);
                    return;
                }
                frameTimer.Stop();
                Action done = animationCompleted;
                animationCompleted = null;
                TraceAnimation("END sequence=" + animationSequence + " frame=" + animationFrame);
                if (done != null) done();
                return;
            }
            animationFrame += animationStep;
            ShowAnimationFrameInternal(animationSequence, animationFrame, false);
        }

        private void ShowAnimationFrame(string sequence, int frame)
        {
            animationPlaybackToken++;
            frameTimer.Stop();
            animationSequence = sequence;
            animationFrame = frame;
            animationEnd = frame;
            animationLoop = false;
            animationCompleted = null;
            ShowAnimationFrameInternal(sequence, frame, true);
        }

        private void ShowAnimationFrameSmooth(string sequence, int frame)
        {
            animationPlaybackToken++;
            frameTimer.Stop();
            animationSequence = sequence;
            animationFrame = frame;
            animationEnd = frame;
            animationLoop = false;
            animationCompleted = null;
            BitmapSource source = PetAssets.GetAnimationFrame(app.Store.Data.PetId, sequence, frame);
            if (source == null) return;
            motion.SetPose(source);
            TraceAnimation("DIRECT sequence=" + sequence + " frame=" + frame);
            System.Windows.Automation.AutomationProperties.SetName(frontImage, "桌宠动画 " + sequence + " 第 " + (frame + 1) + " 帧");
        }

        private void ShowAnimationFrameInternal(string sequence, int frame, bool reaction)
        {
            BitmapSource source = PetAssets.GetAnimationFrame(app.Store.Data.PetId, sequence, frame);
            if (source == null) return;
            motion.SetFrame(source);
            TraceAnimation("FRAME sequence=" + sequence + " frame=" + frame);
            if (reaction) motion.EnterReaction(.22);
            System.Windows.Automation.AutomationProperties.SetName(frontImage, "桌宠动画 " + sequence + " 第 " + (frame + 1) + " 帧");
        }

        private void StopAnimation(bool clearPose)
        {
            animationPlaybackToken++;
            frameTimer.Stop();
            animationSequence = "";
            animationCompleted = null;
            animationLoop = false;
            if (clearPose) frontImage.Source = null;
        }

        private void MirrorAnimation(bool mirror)
        {
            double scaleX = mirror ? -1 : 1;
            frontImageMirror.ScaleX = scaleX;
            backImageMirror.ScaleX = scaleX;
        }

        /// <summary>
        /// 缩入窗口时不能只移动窗口：精灵表的主体位于透明画布中央，必须同步把“睡脸/耳尖”
        /// 推到屏幕仍可见的 36 DIP 露出带。位移跟随宠物尺寸缩放，四边共用同一条连续动画。
        /// </summary>
        private void AnimateDockSpriteShift(bool tucked, int milliseconds)
        {
            double x = 0;
            double y = 0;
            if (tucked)
            {
                double size = Math.Max(1, renderedPetSize > 0 ? renderedPetSize : Width);
                if (dockEdge == DockEdge.Right) x = -0.40 * size;
                else if (dockEdge == DockEdge.Left) x = 0.40 * size;
                else if (dockEdge == DockEdge.Bottom) y = -0.09 * size;
                else if (dockEdge == DockEdge.Top) y = 0.13 * size;
            }
            AnimateSpriteShift(x, y, milliseconds);
        }

        private void AnimateSpriteShift(double x, double y, int milliseconds)
        {
            if (milliseconds <= 0 || app.Store.Data.ReducedMotion)
            {
                SetSpriteShift(x, y);
                return;
            }
            CubicEase ease = new CubicEase { EasingMode = EasingMode.EaseInOut };
            DoubleAnimation frontX = new DoubleAnimation(x, TimeSpan.FromMilliseconds(milliseconds)) { EasingFunction = ease };
            DoubleAnimation backX = new DoubleAnimation(x, TimeSpan.FromMilliseconds(milliseconds)) { EasingFunction = ease };
            DoubleAnimation frontY = new DoubleAnimation(y, TimeSpan.FromMilliseconds(milliseconds)) { EasingFunction = ease };
            DoubleAnimation backY = new DoubleAnimation(y, TimeSpan.FromMilliseconds(milliseconds)) { EasingFunction = ease };
            frontImageShift.BeginAnimation(TranslateTransform.XProperty, frontX, HandoffBehavior.SnapshotAndReplace);
            backImageShift.BeginAnimation(TranslateTransform.XProperty, backX, HandoffBehavior.SnapshotAndReplace);
            frontImageShift.BeginAnimation(TranslateTransform.YProperty, frontY, HandoffBehavior.SnapshotAndReplace);
            backImageShift.BeginAnimation(TranslateTransform.YProperty, backY, HandoffBehavior.SnapshotAndReplace);
        }

        private void SetSpriteShift(double x, double y)
        {
            frontImageShift.BeginAnimation(TranslateTransform.XProperty, null);
            backImageShift.BeginAnimation(TranslateTransform.XProperty, null);
            frontImageShift.BeginAnimation(TranslateTransform.YProperty, null);
            backImageShift.BeginAnimation(TranslateTransform.YProperty, null);
            frontImageShift.X = x;
            backImageShift.X = x;
            frontImageShift.Y = y;
            backImageShift.Y = y;
        }

        private void ResumeBehaviorVisual()
        {
            if (dockState != DockState.None || exclusiveAnimation || DateTime.Now < overrideUntil) return;
            if (QuietModeActive) { ApplyQuietVisual(); return; }
            behaviorAnimation = "";
            SetBehaviorVisual(visualBehavior);
        }

        private void SetBehaviorVisual(BehaviorState state)
        {
            if (exclusiveAnimation || dropPreviewActive) return;
            if (QuietModeActive) { ApplyQuietVisual(); return; }
            if (state != BehaviorState.Idle) { rareIdleShowTimer.Stop(); patrolTimer.Stop(); }
            if (!PetAssets.SupportsAnimation(app.Store.Data.PetId))
            {
                SetAction(ActionFor(state));
                if (state == BehaviorState.Idle && !PetInteractionPolicy.HasActiveCarry(app.Store.Data.CarryItems.Count))
                {
                    ScheduleRareIdleShow();
                    SchedulePatrol();
                }
                else { rareIdleShowTimer.Stop(); patrolTimer.Stop(); }
                return;
            }
            if (state == BehaviorState.Sleepy || state == BehaviorState.Away)
            {
                idleGestureTimer.Stop();
                behaviorAnimation = "";
                StopAnimation(false);
                SetAction("sleep");
                return;
            }

            if (state == BehaviorState.Typing)
            {
                idleGestureTimer.Stop();
                if (behaviorAnimation == "typing-reactive") return;
                behaviorAnimation = "typing-reactive";
                lastAction = "";
                typingPawDirector.Reset();
                displayedTypingPose = TypingPawPose.Rest;
                PetAssets.WarmTypingSoftFrames(app.Store.Data.PetId);
                ShowTypingSoftPose(displayedTypingPose, true);
                return;
            }

            if (state == BehaviorState.Meeting)
            {
                idleGestureTimer.Stop();
                if (behaviorAnimation == "meeting-hold") return;
                behaviorAnimation = "meeting-hold";
                lastAction = "";
                // 会议只演“打开并记笔记”的起手，随后稳定驻留；离会时再单独播放合本收尾。
                PlayAnimationSmooth("meeting", 0, AnimationPolicy.MeetingEnterEndFrame, 1, AnimationPolicy.MeetingFrameIntervalMs, false, delegate
                {
                    if (!exclusiveAnimation && dockState == DockState.None && visualBehavior == BehaviorState.Meeting)
                        ShowAnimationFrame("meeting", AnimationPolicy.MeetingEnterEndFrame);
                });
                return;
            }

            // 阅读/思考/看视频用稳定姿态 + 参数补间表达，避免把“会议翻日历”误演成浏览网页。
            bool carrying = state == BehaviorState.Idle && PetInteractionPolicy.HasActiveCarry(app.Store.Data.CarryItems.Count);
            string steady = carrying ? "carry-steady" : state == BehaviorState.Reading ? "reading-steady"
                : state == BehaviorState.Thinking ? "thinking-steady"
                : state == BehaviorState.Watching ? "watching-steady" : "idle-steady";
            if (state == BehaviorState.Idle && behaviorAnimation == "idle-gesture" && frameTimer.IsEnabled) return;
            if (behaviorAnimation == steady && !frameTimer.IsEnabled)
            {
                if (state == BehaviorState.Idle && !carrying) ScheduleRareIdleShow();
                if (state == BehaviorState.Idle && !carrying) SchedulePatrol();
                return;
            }
            idleGestureTimer.Stop();
            behaviorAnimation = steady;
            lastAction = "";
            if (carrying) ShowAnimationFrameSmooth("carry-file", 17);
            else
            {
                int frame = state == BehaviorState.Reading ? 10 : state == BehaviorState.Thinking ? 12 : 0;
                ShowAnimationFrameSmooth("idle-life", frame);
            }
            if (state == BehaviorState.Idle && !carrying)
            {
                ScheduleIdleGesture();
                ScheduleRareIdleShow();
                SchedulePatrol();
            }
        }

        private void ApplyDockSpriteClip(bool enabled)
        {
            bool sideAnimation = DockGeometry.NeedsSpriteArtifactClip(dockEdge);
            if (!enabled || !sideAnimation)
            {
                motionLayer.Clip = null;
                return;
            }
            double width = Math.Max(1, motionLayer.ActualWidth > 0 ? motionLayer.ActualWidth : Width);
            double height = Math.Max(1, motionLayer.ActualHeight > 0 ? motionLayer.ActualHeight : Height);
            double inset = Math.Max(1, height * DockGeometry.SpriteArtifactInsetRatio);
            motionLayer.Clip = new RectangleGeometry(new Rect(0, inset, width, Math.Max(1, height - inset * 2)));
        }

        private void ScheduleRareIdleShow()
        {
            if (app.Store.Data.ReducedMotion || !CompanionEnergyPolicy.AllowsAutonomousMotion(app.Store.TodayEnergyMode)
                || !ContextScenePolicy.AllowsAutonomousAction(buildWait))
            {
                rareIdleShowTimer.Stop();
                return;
            }
            if (rareIdleShowTimer.IsEnabled) return;
            rareIdleShowTimer.Interval = TimeSpan.FromMilliseconds(AnimationPolicy.NextRareIdleShowDelayMs(idleGestureRandom)
                * CompanionEnergyPolicy.MotionDelayMultiplier(app.Store.TodayEnergyMode));
            rareIdleShowTimer.Start();
        }

        private void RareIdleShowTick(object sender, EventArgs e)
        {
            rareIdleShowTimer.Stop();
            bool eligible = visualBehavior == BehaviorState.Idle && !PetInteractionPolicy.HasActiveCarry(app.Store.Data.CarryItems.Count)
                && dockState == DockState.None && !exclusiveAnimation && !dropPreviewActive && !retreatActive
                && !AttentionRitualActive && !FocusVisualActive
                && !motion.KneadActive
                && ContextScenePolicy.AllowsAutonomousAction(buildWait) && !app.Store.Data.ReducedMotion
                && CompanionEnergyPolicy.AllowsAutonomousMotion(app.Store.TodayEnergyMode);
            if (!eligible)
            {
                if (app.Store.Data.ReducedMotion || !CompanionEnergyPolicy.AllowsAutonomousMotion(app.Store.TodayEnergyMode)
                    || QuietModeActive) return;
                rareIdleShowTimer.Interval = TimeSpan.FromSeconds(45);
                rareIdleShowTimer.Start();
                return;
            }
            lastRareIdleShow = rareIdleDirector.Next(idleGestureRandom);
            PlayIdleShow(lastRareIdleShow, false);
        }

        private void SchedulePatrol()
        {
            if (app.Store.Data.ReducedMotion || patrolTimer.IsEnabled
                || !CompanionEnergyPolicy.AllowsAutonomousMotion(app.Store.TodayEnergyMode)
                || !ContextScenePolicy.AllowsAutonomousAction(buildWait))
            {
                if (!CompanionEnergyPolicy.AllowsAutonomousMotion(app.Store.TodayEnergyMode) || app.Store.Data.ReducedMotion)
                    patrolTimer.Stop();
                return;
            }
            patrolTimer.Interval = TimeSpan.FromMilliseconds(PatrolPolicy.NextDelayMs(idleGestureRandom)
                * CompanionEnergyPolicy.MotionDelayMultiplier(app.Store.TodayEnergyMode));
            patrolTimer.Start();
        }

        private void PatrolTick(object sender, EventArgs e)
        {
            patrolTimer.Stop();
            bool eligible = visualBehavior == BehaviorState.Idle && !PetInteractionPolicy.HasActiveCarry(app.Store.Data.CarryItems.Count)
                && dockState == DockState.None && !exclusiveAnimation && !dropPreviewActive && !retreatActive
                && !AttentionRitualActive && !FocusVisualActive && !mover.IsAnimating
                && !motion.KneadActive
                && ContextScenePolicy.AllowsAutonomousAction(buildWait)
                && !app.Store.Data.ReducedMotion
                && CompanionEnergyPolicy.AllowsAutonomousMotion(app.Store.TodayEnergyMode);
            if (!eligible)
            {
                if (app.Store.Data.ReducedMotion || !CompanionEnergyPolicy.AllowsAutonomousMotion(app.Store.TodayEnergyMode)
                    || QuietModeActive) return;
                patrolTimer.Interval = TimeSpan.FromSeconds(60);
                patrolTimer.Start();
                return;
            }
            if (!StartPatrol(false)) SchedulePatrol();
        }

        private Rect ForegroundPerchCandidate()
        {
            IntPtr foreground = NativeMethods.GetForegroundWindow();
            if (foreground == IntPtr.Zero || NativeMethods.IsZoomed(foreground)) return Rect.Empty;
            uint pid;
            NativeMethods.GetWindowThreadProcessId(foreground, out pid);
            if (pid == System.Diagnostics.Process.GetCurrentProcess().Id) return Rect.Empty;
            NativeMethods.RECT device;
            if (!NativeMethods.GetWindowRect(foreground, out device) || device.Right <= device.Left || device.Bottom <= device.Top)
                return Rect.Empty;
            PresentationSource source = PresentationSource.FromVisual(this);
            if (source == null || source.CompositionTarget == null) return Rect.Empty;
            Point topLeft = source.CompositionTarget.TransformFromDevice.Transform(new Point(device.Left, device.Top));
            Point bottomRight = source.CompositionTarget.TransformFromDevice.Transform(new Point(device.Right, device.Bottom));
            return new Rect(topLeft, bottomRight);
        }

        public bool StartPatrol(bool userInitiated)
        {
            if (userInitiated) YieldAttentionRitual("patrol");
            if (visualBehavior != BehaviorState.Idle || dockState != DockState.None || exclusiveAnimation
                || dropPreviewActive || retreatActive || FocusVisualActive || AttentionRitualActive
                || !ContextScenePolicy.AllowsAutonomousAction(buildWait) || app.Store.Data.ReducedMotion
                || (!userInitiated && !CompanionEnergyPolicy.AllowsAutonomousMotion(app.Store.TodayEnergyMode))) return false;
            Rect area = WindowPlacement.WorkAreaFor(this);
            Rect petRect = new Rect(Left, Top, Width, Height);
            PatrolPlan plan = PatrolPolicy.Resolve(area, petRect, ForegroundPerchCandidate());
            if (!plan.Valid) return false;
            idleGestureTimer.Stop();
            rareIdleShowTimer.Stop();
            patrolTimer.Stop();
            exclusiveAnimation = true;
            exclusiveStatus = "";
            patrolActive = true;
            patrolOrigin = new Point(Left, Top);
            int token = ++exclusiveAnimationToken;
            ambientInputGraceUntil = MonotonicSeconds() + (userInitiated
                ? AmbientInterruptionPolicy.UserInvitationGraceSeconds
                : AmbientInterruptionPolicy.AutomaticActionGraceSeconds);
            overrideUntil = DateTime.Now.AddSeconds(6.8);
            SetAction("idle");
            MirrorAnimation(!plan.FacingRight);
            motion.StartPatrol(plan.FacingRight);
            TraceAnimation("PATROL start mode=" + (plan.WindowPerch ? "window" : "short") + " source="
                + (userInitiated ? "demo" : "idle") + " target=" + plan.Target + " token=" + token);
            mover.AnimateTo(plan.Target.X, plan.Target.Y, 1650, delegate
            {
                if (token != exclusiveAnimationToken || !patrolActive) return;
                motion.StopPatrol();
                motion.Squash(0.06);
                DispatcherTimer perch = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(plan.WindowPerch ? 2500 : 1400)
                };
                perch.Tick += delegate
                {
                    perch.Stop();
                    if (token != exclusiveAnimationToken || !patrolActive) return;
                    MirrorAnimation(plan.FacingRight);
                    motion.StartPatrol(!plan.FacingRight);
                    mover.AnimateTo(patrolOrigin.X, patrolOrigin.Y, 1450, delegate
                    {
                        if (token != exclusiveAnimationToken) return;
                        patrolActive = false;
                        motion.StopPatrol();
                        MirrorAnimation(false);
                        SavePosition();
                        TraceAnimation("PATROL end token=" + token);
                        CompleteExclusiveAnimation(token, "desktop-patrol");
                    });
                };
                perch.Start();
            });
            return true;
        }

        private bool KeepDemoRunning(bool started)
        {
            if (started) ambientInputGraceUntil = MonotonicSeconds() + AmbientInterruptionPolicy.DemoGraceSeconds;
            return started;
        }

        public bool PlayPatrolDemo() { return KeepDemoRunning(StartPatrol(true)); }

        public bool PlayIdleShow(RareIdleShowType show, bool userInitiated)
        {
            if (userInitiated) YieldAttentionRitual("idle-show");
            if (show == RareIdleShowType.None || visualBehavior != BehaviorState.Idle || dockState != DockState.None
                || exclusiveAnimation || dropPreviewActive || retreatActive || AttentionRitualActive
                || PetInteractionPolicy.HasActiveCarry(app.Store.Data.CarryItems.Count)
                || app.Store.Data.ReducedMotion) return false;
            idleGestureTimer.Stop();
            rareIdleShowTimer.Stop();
            exclusiveAnimation = true;
            exclusiveStatus = "";
            int token = ++exclusiveAnimationToken;
            ambientInputGraceUntil = MonotonicSeconds() + (userInitiated
                ? AmbientInterruptionPolicy.UserInvitationGraceSeconds
                : AmbientInterruptionPolicy.AutomaticActionGraceSeconds);
            int duration = AnimationPolicy.DurationFor(show);
            overrideUntil = DateTime.Now.AddMilliseconds(duration + 350);
            motion.SetPose(PetAssets.Get(app.Store.Data.PetId,
                show == RareIdleShowType.BubbleBlow ? "idle" : "happy"));
            if (!motion.StartPlayfulShow(show))
            {
                exclusiveAnimation = false;
                ambientInputGraceUntil = -1;
                overrideUntil = DateTime.MinValue;
                return false;
            }
            TraceAnimation("PLAYFUL start show=" + show + " source=" + (userInitiated ? "gesture" : "idle") + " token=" + token);
            DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(duration) };
            timer.Tick += delegate
            {
                timer.Stop();
                if (token != exclusiveAnimationToken) return;
                motion.StopPlayfulShow();
                TraceAnimation("PLAYFUL end show=" + show + " token=" + token);
                CompleteExclusiveAnimation(token, "playful-" + show);
            };
            timer.Start();
            return true;
        }

        public bool PlayDanceDemo() { return KeepDemoRunning(PlayIdleShow(RareIdleShowType.Dance, true)); }
        public bool PlayHulaDemo() { return KeepDemoRunning(PlayIdleShow(RareIdleShowType.HulaHoop, true)); }
        public bool PlayButterflyDemo() { return KeepDemoRunning(PlayIdleShow(RareIdleShowType.ButterflyChase, true)); }
        public bool PlayBubblesDemo() { return KeepDemoRunning(PlayIdleShow(RareIdleShowType.BubbleBlow, true)); }

        public bool PlayShyReaction()
        {
            if (visualBehavior != BehaviorState.Idle || dockState != DockState.None || exclusiveAnimation
                || dropPreviewActive || retreatActive || AttentionRitualActive || app.Store.Data.ReducedMotion) return false;
            idleGestureTimer.Stop();
            rareIdleShowTimer.Stop();
            exclusiveAnimation = true;
            exclusiveStatus = "";
            int token = ++exclusiveAnimationToken;
            ambientInputGraceUntil = MonotonicSeconds() + AmbientInterruptionPolicy.ShyGraceSeconds;
            overrideUntil = DateTime.Now.AddMilliseconds(2600);
            SetAction("idle");
            if (!motion.StartShy())
            {
                exclusiveAnimation = false;
                ambientInputGraceUntil = -1;
                overrideUntil = DateTime.MinValue;
                return false;
            }
            TraceAnimation("SHY start token=" + token);
            DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2300) };
            timer.Tick += delegate
            {
                timer.Stop();
                if (token != exclusiveAnimationToken) return;
                motion.StopShy();
                TraceAnimation("SHY end token=" + token);
                CompleteExclusiveAnimation(token, "shy-peek");
            };
            timer.Start();
            return true;
        }

        public bool PlayShyDemo() { return KeepDemoRunning(PlayShyReaction()); }

        public bool PlayTailChaseDemo() { return KeepDemoRunning(PlayIdleShow(RareIdleShowType.TailChase, true)); }

        public bool PlayBalloonGame()
        {
            YieldAttentionRitual("balloon");
            if (visualBehavior != BehaviorState.Idle || dockState != DockState.None || exclusiveAnimation
                || dropPreviewActive || retreatActive || AttentionRitualActive || app.Store.Data.ReducedMotion) return false;
            idleGestureTimer.Stop();
            rareIdleShowTimer.Stop();
            exclusiveAnimation = true;
            exclusiveStatus = "顶气球";
            BalloonBopCount = 0;
            int token = ++exclusiveAnimationToken;
            ambientInputGraceUntil = MonotonicSeconds() + AmbientInterruptionPolicy.BalloonGraceSeconds;
            overrideUntil = DateTime.Now.AddSeconds(10.4);
            SetAction("happy");
            if (!motion.StartBalloonGame())
            {
                exclusiveAnimation = false;
                exclusiveStatus = "";
                ambientInputGraceUntil = -1;
                overrideUntil = DateTime.MinValue;
                return false;
            }
            UpdateChip();
            ShowToast("点一下气球，和我一起别让它落地");
            TraceAnimation("BALLOON start token=" + token);
            DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            timer.Tick += delegate
            {
                timer.Stop();
                if (token != exclusiveAnimationToken) return;
                motion.StopBalloonGame();
                TraceAnimation("BALLOON end bops=" + BalloonBopCount + " token=" + token);
                CompleteExclusiveAnimation(token, "balloon-game");
            };
            timer.Start();
            return true;
        }

        public bool PlayBalloonDemo() { return KeepDemoRunning(PlayBalloonGame()); }

        /// <summary>用户主动投喂：饼干入场、轻咬咀嚼、爱心收尾；六宠共用连续参数，不依赖新位图帧。</summary>
        public bool PlayTreatInteraction()
        {
            YieldAttentionRitual("treat");
            if (retreatActive || dragging) return false;
            if (dockState != DockState.None) ExitDock(false);
            if (QuietModeActive) ExitQuietMode(false);
            CancelExclusiveAnimation(false);
            idleGestureTimer.Stop();
            rareIdleShowTimer.Stop();
            exclusiveAnimation = true;
            exclusiveStatus = "吃点心";
            int token = ++exclusiveAnimationToken;
            ambientInputGraceUntil = MonotonicSeconds() + TreatMotionPolicy.DurationSeconds + 0.4;
            overrideUntil = DateTime.Now.AddSeconds(TreatMotionPolicy.DurationSeconds + 0.35);
            SetAction("idle");
            if (!motion.StartTreat())
            {
                exclusiveAnimation = false;
                exclusiveStatus = "";
                return false;
            }
            UpdateChip();
            TraceAnimation("TREAT start token=" + token);
            DispatcherTimer bite = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1050) };
            bite.Tick += delegate
            {
                bite.Stop();
                if (token != exclusiveAnimationToken) return;
                SetAction("happy");
                motion.Squash(0.045);
            };
            bite.Start();
            DispatcherTimer finish = new DispatcherTimer { Interval = TimeSpan.FromSeconds(TreatMotionPolicy.DurationSeconds) };
            finish.Tick += delegate
            {
                finish.Stop();
                if (token != exclusiveAnimationToken) return;
                motion.StopTreat();
                TraceAnimation("TREAT end token=" + token);
                CompleteExclusiveAnimation(token, "treat");
            };
            finish.Start();
            return true;
        }

        public bool PlayTreatDemo() { return KeepDemoRunning(PlayTreatInteraction()); }

        /// <summary>用户主动邀请击掌；只在 2.6 秒邀请期把一次宠物点击解释为回应。</summary>
        public bool InviteHighFive()
        {
            YieldAttentionRitual("high-five");
            if (retreatActive || dragging) return false;
            if (dockState != DockState.None) ExitDock(false);
            if (QuietModeActive) ExitQuietMode(false);
            CancelExclusiveAnimation(false);
            idleGestureTimer.Stop();
            rareIdleShowTimer.Stop();
            exclusiveAnimation = true;
            exclusiveStatus = "等你击掌";
            int token = ++exclusiveAnimationToken;
            ambientInputGraceUntil = MonotonicSeconds() + HighFivePolicy.InviteSeconds + HighFivePolicy.CelebrateSeconds;
            overrideUntil = DateTime.Now.AddSeconds(HighFivePolicy.InviteSeconds + HighFivePolicy.CelebrateSeconds + 0.3);
            SetAction("happy");
            if (!motion.StartHighFive())
            {
                exclusiveAnimation = false;
                exclusiveStatus = "";
                return false;
            }
            UpdateChip();
            TraceAnimation("HIGHFIVE invite token=" + token);
            DispatcherTimer timeout = new DispatcherTimer { Interval = TimeSpan.FromSeconds(HighFivePolicy.InviteSeconds) };
            timeout.Tick += delegate
            {
                timeout.Stop();
                if (!HighFivePolicy.ShouldTimeout(token == exclusiveAnimationToken,
                    exclusiveAnimation, motion.HighFiveCelebrating)) return;
                TraceAnimation("HIGHFIVE timeout token=" + token);
                motion.StopHighFive();
                CompleteExclusiveAnimation(token, "high-five-timeout");
            };
            timeout.Start();
            return true;
        }

        private void RespondToHighFive()
        {
            if (!exclusiveAnimation || !motion.CompleteHighFive()) return;
            int token = exclusiveAnimationToken;
            exclusiveStatus = "好耶";
            motion.Squash(0.055);
            motion.Heart();
            UpdateChip();
            TraceAnimation("HIGHFIVE hit token=" + token);
            DispatcherTimer settle = new DispatcherTimer { Interval = TimeSpan.FromSeconds(HighFivePolicy.CelebrateSeconds) };
            settle.Tick += delegate
            {
                settle.Stop();
                if (token != exclusiveAnimationToken) return;
                motion.StopHighFive();
                CompleteExclusiveAnimation(token, "high-five-hit");
            };
            settle.Start();
        }

        public bool PlayHighFiveDemo() { return KeepDemoRunning(InviteHighFive()); }

        /// <summary>用户主动召唤：先看向光标，再以低幅小步跑到旁边，落地后短暂停留。</summary>
        public bool SummonToCursor()
        {
            YieldAttentionRitual("summon");
            if (retreatActive || dragging) return false;
            if (dockState != DockState.None) ExitDock(false);
            if (QuietModeActive) ExitQuietMode(false);
            PresentationSource source = PresentationSource.FromVisual(this);
            if (source == null || source.CompositionTarget == null) return false;
            System.Drawing.Point cursorDevice = System.Windows.Forms.Cursor.Position;
            Point cursor = source.CompositionTarget.TransformFromDevice.Transform(new Point(cursorDevice.X, cursorDevice.Y));
            Rect area = WindowPlacement.WorkAreaFor(this);
            SummonPlan plan = SummonGeometry.Resolve(area, cursor, Width, Height);
            if (!plan.Valid) return false;

            CancelExclusiveAnimation(false);
            idleGestureTimer.Stop();
            rareIdleShowTimer.Stop();
            patrolTimer.Stop();
            exclusiveAnimation = true;
            exclusiveStatus = "跑来找你";
            // 召唤也是一次窗口位移：复用 patrolActive 的互斥/取消路径，避免中途点投喂后旧轨迹继续滑行。
            patrolActive = true;
            int token = ++exclusiveAnimationToken;
            ambientInputGraceUntil = MonotonicSeconds() + AmbientInterruptionPolicy.UserInvitationGraceSeconds + 1.8;
            overrideUntil = DateTime.Now.AddSeconds(2.4);
            SetAction("happy");
            MirrorAnimation(!plan.FacingRight);
            motion.SetAttentionTarget(plan.FacingRight ? 0.8 : -0.8, -0.12);
            UpdateChip();
            TraceAnimation("SUMMON look target=" + plan.Target + " token=" + token);

            DispatcherTimer look = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(app.Store.Data.ReducedMotion ? 80 : 220) };
            look.Tick += delegate
            {
                look.Stop();
                if (token != exclusiveAnimationToken) return;
                if (app.Store.Data.ReducedMotion)
                {
                    Left = plan.Target.X;
                    Top = plan.Target.Y;
                    FinishSummon(token);
                    return;
                }
                motion.StartPatrol(plan.FacingRight);
                TraceAnimation("SUMMON move token=" + token);
                mover.AnimateTo(plan.Target.X, plan.Target.Y, 820, delegate
                {
                    if (token != exclusiveAnimationToken) return;
                    FinishSummon(token);
                });
            };
            look.Start();
            return true;
        }

        private void FinishSummon(int token)
        {
            if (token != exclusiveAnimationToken) return;
            motion.StopPatrol();
            motion.SetAttentionTarget(0, 0);
            motion.Squash(0.055);
            motion.Heart();
            SavePosition();
            TraceAnimation("SUMMON arrive token=" + token);
            DispatcherTimer settle = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(720) };
            settle.Tick += delegate
            {
                settle.Stop();
                if (token != exclusiveAnimationToken) return;
                MirrorAnimation(false);
                CompleteExclusiveAnimation(token, "summon");
            };
            settle.Start();
        }

        public bool PlaySummonDemo() { return KeepDemoRunning(SummonToCursor()); }

        private void ScheduleIdleGesture()
        {
            idleGestureTimer.Stop();
            if (!AnimationPolicy.AllowsIdleGesture(app.Store.Data.ReducedMotion, QuietModeActive)
                || !CompanionEnergyPolicy.AllowsAutonomousMotion(app.Store.TodayEnergyMode)) return;
            idleGestureTimer.Interval = TimeSpan.FromMilliseconds(AnimationPolicy.NextIdleGestureDelayMs(idleGestureRandom)
                * CompanionEnergyPolicy.MotionDelayMultiplier(app.Store.TodayEnergyMode));
            idleGestureTimer.Start();
        }

        private void IdleGestureTick(object sender, EventArgs e)
        {
            idleGestureTimer.Stop();
            if (!AnimationPolicy.AllowsIdleGesture(app.Store.Data.ReducedMotion, QuietModeActive)
                || !CompanionEnergyPolicy.AllowsAutonomousMotion(app.Store.TodayEnergyMode)) return;
            if (visualBehavior != BehaviorState.Idle || motion.KneadActive || PetInteractionPolicy.HasActiveCarry(app.Store.Data.CarryItems.Count)
                || dockState != DockState.None || exclusiveAnimation || dropPreviewActive || retreatActive
                || AttentionRitualActive)
            {
                ScheduleIdleGesture();
                return;
            }
            // 一次只演一个角色允许的微动作；小满仅使用通过逐帧 QC 的 0/5/10 三段。
            int[] safeStarts = AnimationPolicy.SafeIdleGestureStarts(app.Store.Data.PetId);
            int start = safeStarts[idleGestureRandom.Next(safeStarts.Length)];
            int end = start + 4;
            behaviorAnimation = "idle-gesture";
            PlayAnimation("idle-life", start, end, 1, AnimationPolicy.IdleGestureFrameIntervalMs, false, delegate
            {
                if (visualBehavior != BehaviorState.Idle || dockState != DockState.None || exclusiveAnimation)
                { ScheduleIdleGesture(); return; }
                DispatcherTimer hold = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(AnimationPolicy.IdleGestureHoldMs) };
                hold.Tick += delegate
                {
                    hold.Stop();
                    // 最后一段素材自身已回到静止；其余小动作倒放收势，避免从歪头/舔爪硬切站姿。
                    if (start == 20 || end <= start)
                    {
                        FinishIdleGesture();
                        return;
                    }
                    PlayAnimation("idle-life", end - 1, start, -1, AnimationPolicy.IdleGestureFrameIntervalMs, false, FinishIdleGesture);
                };
                hold.Start();
            });
        }

        private void FinishIdleGesture()
        {
            if (visualBehavior == BehaviorState.Idle && dockState == DockState.None && !exclusiveAnimation)
            {
                behaviorAnimation = "idle-steady";
                ShowAnimationFrameSmooth("idle-life", 0);
            }
            ScheduleIdleGesture();
        }

        private void PlayOneShot(string sequence, int milliseconds, int holdMilliseconds, string status)
        {
            PlayOneShotTo(sequence, 24, milliseconds, holdMilliseconds, status);
        }

        private void PlayOneShotTo(string sequence, int endFrame, int milliseconds, int holdMilliseconds, string status)
        {
            if (!PetAssets.SupportsAnimation(app.Store.Data.PetId)) return;
            if (dockState != DockState.None) ExitDock(true);
            CancelExclusiveAnimation(false);
            idleGestureTimer.Stop();
            exclusiveAnimation = true;
            exclusiveStatus = status ?? "";
            int token = ++exclusiveAnimationToken;
            behaviorAnimation = "";
            endFrame = Math.Max(0, Math.Min(24, endFrame));
            double seconds = ((endFrame + 1) * Math.Max(34, milliseconds) + Math.Max(0, holdMilliseconds)) / 1000.0 + .4;
            overrideUntil = DateTime.Now.AddSeconds(seconds);
            TraceAnimation("EXCLUSIVE begin sequence=" + sequence + " token=" + token);
            UpdateChip();
            PlayAnimationSmooth(sequence, 0, endFrame, 1, milliseconds, false, delegate
            {
                if (token != exclusiveAnimationToken) return;
                if (holdMilliseconds <= 0) { CompleteExclusiveAnimation(token, sequence); return; }
                DispatcherTimer hold = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(holdMilliseconds) };
                hold.Tick += delegate
                {
                    hold.Stop();
                    CompleteExclusiveAnimation(token, sequence);
                };
                hold.Start();
            });
        }

        private void PlayChoreographedOneShot(string sequence, int enterEndFrame, int exitStartFrame, int endFrame,
            int milliseconds, int holdMilliseconds, string status)
        {
            if (!PetAssets.SupportsAnimation(app.Store.Data.PetId)) return;
            if (dockState != DockState.None) ExitDock(true);
            CancelExclusiveAnimation(false);
            idleGestureTimer.Stop();
            exclusiveAnimation = true;
            exclusiveStatus = status ?? "";
            int token = ++exclusiveAnimationToken;
            behaviorAnimation = "";
            int exitStep = AnimationPolicy.FrameStep(exitStartFrame, endFrame);
            double seconds = ((enterEndFrame + 1 + AnimationPolicy.FrameCount(exitStartFrame, endFrame)) * Math.Max(34, milliseconds)
                + Math.Max(0, holdMilliseconds)) / 1000.0 + .5;
            overrideUntil = DateTime.Now.AddSeconds(seconds);
            TraceAnimation("CHOREO start sequence=" + sequence + " enter=0.." + enterEndFrame + " holdMs=" + holdMilliseconds
                + " exit=" + exitStartFrame + ".." + endFrame + " step=" + exitStep + " token=" + token);
            UpdateChip();
            PlayAnimationSmooth(sequence, 0, enterEndFrame, 1, milliseconds, false, delegate
            {
                if (token != exclusiveAnimationToken) return;
                DispatcherTimer hold = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(Math.Max(1, holdMilliseconds)) };
                hold.Tick += delegate
                {
                    hold.Stop();
                    if (token != exclusiveAnimationToken) return;
                    PlayAnimation(sequence, exitStartFrame, endFrame, exitStep, milliseconds, false,
                        delegate { CompleteExclusiveAnimation(token, sequence); });
                };
                hold.Start();
            });
        }

        private void PlayExclusiveRange(string sequence, int startFrame, int endFrame, int milliseconds, string status)
        {
            if (!PetAssets.SupportsAnimation(app.Store.Data.PetId)) { RefreshPet(); return; }
            if (dockState != DockState.None) ExitDock(true);
            CancelExclusiveAnimation(false);
            idleGestureTimer.Stop();
            exclusiveAnimation = true;
            exclusiveStatus = status ?? "";
            int token = ++exclusiveAnimationToken;
            overrideUntil = DateTime.Now.AddSeconds(((endFrame - startFrame + 1) * milliseconds) / 1000.0 + .5);
            TraceAnimation("CHOREO range sequence=" + sequence + " start=" + startFrame + " end=" + endFrame + " token=" + token);
            UpdateChip();
            PlayAnimationSmooth(sequence, startFrame, endFrame, 1, milliseconds, false,
                delegate { CompleteExclusiveAnimation(token, sequence); });
        }

        private void CompleteExclusiveAnimation(int token, string sequence)
        {
            if (token != exclusiveAnimationToken) return;
            patrolActive = false;
            motion.StopPatrol();
            ambientInputGraceUntil = -1;
            motion.StopPlayfulShow();
            motion.StopShy();
            motion.StopBalloonGame();
            motion.StopTreat();
            motion.StopHighFive();
            motion.StopPatAffection();
            patReactionActive = false;
            exclusiveAnimation = false;
            exclusiveStatus = "";
            overrideUntil = DateTime.MinValue;
            TraceAnimation("EXCLUSIVE end sequence=" + sequence + " token=" + token);
            lastChipText = "";
            ResumeBehaviorVisual();
            if (visualBehavior == BehaviorState.Idle && !PetInteractionPolicy.HasActiveCarry(app.Store.Data.CarryItems.Count)) ScheduleRareIdleShow();
            UpdateChip();
            TryPresentFocusCompletion();
        }

        private void CancelExclusiveAnimation(bool resume)
        {
            if (!exclusiveAnimation && string.IsNullOrEmpty(exclusiveStatus)) return;
            if (patrolActive)
            {
                patrolActive = false;
                mover.Cancel();
                motion.StopPatrol();
                MirrorAnimation(false);
                SavePosition();
            }
            motion.StopPlayfulShow();
            motion.StopShy();
            motion.StopBalloonGame();
            motion.StopTreat();
            motion.StopHighFive();
            motion.StopPatAffection();
            patReactionActive = false;
            exclusiveAnimationToken++;
            ambientInputGraceUntil = -1;
            exclusiveAnimation = false;
            exclusiveStatus = "";
            overrideUntil = DateTime.MinValue;
            StopAnimation(false);
            lastChipText = "";
            if (resume) ResumeBehaviorVisual();
        }

        public void PlayCarryAnimation() { PlayOneShotTo("carry-file", AnimationPolicy.CarryHoldFrame, AnimationPolicy.CarryFrameIntervalMs, AnimationPolicy.CarryHoldMs, "叼东西中"); }

        public void PlayCarryReleaseAnimation()
        {
            if (PetInteractionPolicy.HasActiveCarry(app.Store.Data.CarryItems.Count)) { RefreshPet(); return; }
            PlayExclusiveRange("carry-file", AnimationPolicy.CarryReleaseStartFrame, AnimationPolicy.CarryReleaseEndFrame,
                AnimationPolicy.CarryFrameIntervalMs, "放下东西");
        }

        public void PlayStretchAnimation()
        {
            string petId = app.Store.Data.PetId;
            PlayChoreographedOneShot("stretch", AnimationPolicy.StretchPeakFrame(petId),
                AnimationPolicy.StretchExitStartFrame(petId), AnimationPolicy.StretchExitEndFrame(petId),
                AnimationPolicy.StretchFrameIntervalMs, AnimationPolicy.StretchPeakHoldMs, "伸懒腰");
        }

        // ---------------- 主动专注仪式：明确倒计时、可提前结束、只收尾一次 ----------------

        public bool EnterFocusRitual(TimeSpan duration)
        {
            if (!FocusRitualPolicy.CanStart(visualBehavior, buildWait, dockState != DockState.None, retreatActive))
            {
                EventToast(visualBehavior == BehaviorState.Meeting ? "会议中先不开始专注计时"
                    : buildWait ? "先等这次编译结束"
                    : "先把 " + PetCatalog.Find(app.Store.Data.PetId).Name + " 从屏幕边缘叫回来");
                return false;
            }
            if (QuietModeActive) ExitQuietMode(false);
            YieldAttentionRitual("focus");
            CancelExclusiveAnimation(false);
            ResetCursorGreetingObservation(true);
            if (nodOffDir.Active) AbortNodOff("focus-ritual");
            idleGestureTimer.Stop();
            rareIdleShowTimer.Stop();
            patrolTimer.Stop();
            focusRitualStarted = DateTime.Now;
            focusRitualUntil = focusRitualStarted.Add(duration <= TimeSpan.Zero
                ? TimeSpan.FromMinutes(FocusRitualPolicy.DefaultMinutes) : duration);
            focusRitualTimer.Start();
            motion.SetContextFlags(buildWait, true);
            motion.SetFocusRitualProgress(0);
            motion.SetBehavior(visualBehavior);
            motion.Squash(0.035);
            lastChipText = "";
            UpdateChip();
            EventToast("我陪你专注 25 分钟，结束时叫你伸个懒腰");
            TraceAnimation("FOCUS_RITUAL start seconds=" + Math.Round((focusRitualUntil - DateTime.Now).TotalSeconds));
            return true;
        }

        public void ExitFocusRitual(bool completed)
        {
            ExitFocusRitual(completed, true);
        }

        private void ExitFocusRitual(bool completed, bool notify)
        {
            bool wasActive = focusRitualUntil != DateTime.MinValue;
            focusRitualStarted = DateTime.MinValue;
            focusRitualUntil = DateTime.MinValue;
            focusRitualTimer.Stop();
            motion.SetContextFlags(buildWait, FocusVisualActive);
            motion.SetFocusRitualProgress(-1);
            lastChipText = "";
            UpdateChip();
            if (!wasActive) return;
            TraceAnimation("FOCUS_RITUAL end completed=" + completed);
            if (completed)
            {
                pendingFocusCompletion = notify;
                TryPresentFocusCompletion();
            }
            else
            {
                pendingFocusCompletion = false;
                if (notify) EventToast("专注已提前结束");
            }
            if (visualBehavior == BehaviorState.Idle)
            {
                ScheduleRareIdleShow();
                SchedulePatrol();
            }
        }

        private void TryPresentFocusCompletion()
        {
            if (!pendingFocusCompletion || !FocusRitualPolicy.CanPresentCompletion(visualBehavior, buildWait,
                dockState != DockState.None, retreatActive, QuietModeActive, exclusiveAnimation)) return;
            pendingFocusCompletion = false;
            TraceAnimation("FOCUS_RITUAL completion-presented");
            if (!app.Store.Data.ReducedMotion) PlayStretchAnimation();
            EventToast("25 分钟到了，看看远处，活动一下肩颈吧");
        }

        public void ToggleFocusRitual()
        {
            if (FocusRitualActive) ExitFocusRitual(false, true);
            else EnterFocusRitual(TimeSpan.FromMinutes(FocusRitualPolicy.DefaultMinutes));
        }

        public bool PlayFocusRitualDemo() { return EnterFocusRitual(TimeSpan.FromSeconds(6)); }

        // ---------------- 安静陪伴：用户主动邀请，小满蜷睡且不弹低优先级提醒 ----------------

        private void ApplyQuietVisual()
        {
            idleGestureTimer.Stop();
            rareIdleShowTimer.Stop();
            patrolTimer.Stop();
            motion.SetQuietCompanion(true);
            motion.SetBehavior(BehaviorState.Sleepy);
            SetAction("sleep");
        }

        public void EnterQuietMode(TimeSpan duration)
        {
            if (FocusRitualActive) ExitFocusRitual(false, false);
            YieldAttentionRitual("quiet");
            CancelExclusiveAnimation(false);
            if (nodOffDir.Active) AbortNodOff("quiet-mode");
            quietUntil = DateTime.Now.Add(duration <= TimeSpan.Zero ? TimeSpan.FromMinutes(QuietCompanionPolicy.DefaultMinutes) : duration);
            quietTimer.Start();
            ApplyQuietVisual();
            lastChipText = "";
            UpdateChip();
            TraceAnimation("QUIET start seconds=" + Math.Round(duration.TotalSeconds));
        }

        public void ExitQuietMode(bool notify)
        {
            bool wasActive = quietUntil != DateTime.MinValue;
            quietUntil = DateTime.MinValue;
            quietTimer.Stop();
            motion.SetQuietCompanion(false);
            motion.SetBehavior(visualBehavior);
            behaviorAnimation = "";
            lastChipText = "";
            ResumeBehaviorVisual();
            UpdateChip();
            if (notify && wasActive) EventToast("我醒啦，继续陪你");
            if (wasActive) TraceAnimation("QUIET end");
        }

        public void ToggleQuietMode()
        {
            if (QuietModeActive) ExitQuietMode(true);
            else EnterQuietMode(TimeSpan.FromMinutes(QuietCompanionPolicy.DefaultMinutes));
        }

        public void PlayQuietDemo() { EnterQuietMode(TimeSpan.FromSeconds(6)); }

        // ---------------- 困倦点头分镜（Top1 完整小剧场）----------------

        /// <summary>触发条件：Idle 且夜深或低兴奋、冷却已过、无打扰场合（绝不打断工作）。</summary>
        public bool TryStartNodOff(bool force)
        {
            if (nodOffDir.Active) return false;
            if (!force && DateTime.Now < nodOffCooldownUntil) return false;
            string refuse = behavior != BehaviorState.Idle ? "behavior " + behavior
                : QuietModeActive ? "quiet-mode"
                : !NodOffSchedule.AllowsAutonomousScene(app.Store.Data.ReducedMotion, buildWait)
                    ? (app.Store.Data.ReducedMotion ? "reduced-motion" : "build-wait")
                : dockState != DockState.None ? "docked"
                : exclusiveAnimation ? "exclusive"
                : dropPreviewActive ? "drop-preview"
                : retreatActive ? "retreat"
                : dragging ? "dragging"
                : patrolActive ? "patrol"
                : motion.PlayfulActive ? "playful"
                : motion.ShyActive ? "shy"
                : motion.BalloonActive ? "balloon"
                : motion.KneadActive ? "knead"
                : AttentionRitualActive ? "attention-ritual"
                : !force && !NodOffDirector.DrowsyWindow(DateTime.Now) && lastEngineOutput != null && lastEngineOutput.Arousal >= 0.3 ? "not-drowsy"
                : null;
            if (refuse != null)
            {
                TraceAnimation("NODOFF refuse " + refuse);
                return false;
            }
            nodOffSleeping = false;
            nodOffStretched = false;
            nodOffDir.Start(15 + nodOffRandom.NextDouble() * 20);
            if (nodOffTimer == null)
            {
                nodOffTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
                nodOffTimer.Tick += NodOffTick;
            }
            nodOffTimer.Start();
            TraceAnimation("NODOFF start");
            return true;
        }

        private void NodOffTick(object sender, EventArgs e)
        {
            if (behavior != BehaviorState.Idle || dockState != DockState.None || exclusiveAnimation || dragging)
            {
                AbortNodOff("interrupt");
                return;
            }
            NodOffPose pose = nodOffDir.Tick(0.05);
            if (!nodOffDir.Active)
            {
                FinishNodOff();
                return;
            }
            if (pose.SleepPose && !nodOffSleeping)
            {
                nodOffSleeping = true;
                motion.ClearNodOffPose();
                SetAction("sleep");
                return;
            }
            if (pose.StretchPose)
            {
                if (nodOffSleeping)
                {
                    nodOffSleeping = false;
                    SetAction(ActionFor(behavior));
                }
                if (!nodOffStretched)
                {
                    nodOffStretched = true;
                    PlayStretchAnimation(); // 收尾：醒来伸个懒腰
                }
                motion.SetNodOffPose(pose.Tilt, pose.ScaleY, pose.StartleFlash);
                return;
            }
            motion.SetNodOffPose(pose.Tilt, pose.ScaleY, pose.StartleFlash);
        }

        private void FinishNodOff()
        {
            if (nodOffTimer != null) nodOffTimer.Stop();
            nodOffCooldownUntil = DateTime.Now.AddSeconds(NodOffSchedule.NextDelaySeconds(nodOffRandom));
            motion.ClearNodOffPose();
            if (nodOffSleeping)
            {
                nodOffSleeping = false;
                SetAction(ActionFor(behavior));
            }
            TraceAnimation("NODOFF done");
        }

        public void AbortNodOff(string reason)
        {
            if (!nodOffDir.Active) return;
            nodOffDir.Abort();
            FinishNodOff();
            TraceAnimation("NODOFF abort " + reason);
        }

        public bool PlayNodOffDemo() { return KeepDemoRunning(TryStartNodOff(true)); }

        private void PlayPatReaction(double pointerBias)
        {
            YieldAttentionRitual("pat");
            // 摸头是用户主动互动，不得抢 Typing / Meeting / Dock 等工作姿态。
            if (visualBehavior != BehaviorState.Idle || dockState != DockState.None || exclusiveAnimation || dropPreviewActive) return;
            int start;
            int end;
            if (PetAssets.SupportsAnimation(app.Store.Data.PetId)
                && AnimationPolicy.TryGetPatClip(app.Store.Data.PetId, out start, out end))
            {
                PlayExclusiveRange("idle-life", start, end, 145, "");
                patReactionActive = exclusiveAnimation;
                ScheduleKneadAfterPat(1300);
                return;
            }

            idleGestureTimer.Stop();
            patReactionActive = true;
            exclusiveAnimation = true;
            exclusiveStatus = "";
            int token = ++exclusiveAnimationToken;
            overrideUntil = DateTime.Now.AddMilliseconds(900);
            motion.SetPose(PetAssets.Get(app.Store.Data.PetId, "happy"));
            motion.StartPatAffection(pointerBias);
            TraceAnimation("PAT happy token=" + token);
            DispatcherTimer hold = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(720) };
            hold.Tick += delegate
            {
                hold.Stop();
                CompleteExclusiveAnimation(token, "pat-happy");
                // 摸完头满足地踩奶（无缝接续，任何忙碌状态自动让位）
                ScheduleKneadAfterPat(200);
            };
            hold.Start();
        }

        /// <summary>摸头后的保留节目：满足踩奶。只在 Idle 安静时接续，不抢任何工作姿态。</summary>
        private void ScheduleKneadAfterPat(int delayMs)
        {
            DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(Math.Max(50, delayMs)) };
            timer.Tick += delegate
            {
                timer.Stop();
                if (behavior == BehaviorState.Idle && !exclusiveAnimation && dockState == DockState.None && !dragging)
                    motion.StartKnead(5);
            };
            timer.Start();
        }

        public bool PlayKneadDemo()
        {
            if (app.Store.Data.ReducedMotion) return false;
            motion.StartKnead(5);
            TraceAnimation("KNEAD start active=" + motion.KneadActive);
            return true;
        }

        public void DebugPatReaction() { PlayPatReaction(0.78); }

        public void PlayMeetingAnimation()
        {
            PlayChoreographedOneShot("meeting", AnimationPolicy.MeetingEnterEndFrame, AnimationPolicy.MeetingExitStartFrame,
                AnimationPolicy.MeetingExitEndFrame, AnimationPolicy.MeetingFrameIntervalMs,
                AnimationPolicy.MeetingReminderHoldMs, "会议提醒");
        }

        private void PetDragEnter(object sender, DragEventArgs e)
        {
            string updatePackage;
            bool updateDrop = TryGetDroppedUpdatePackage(e.Data, out updatePackage);
            bool carryDrop = PetInteractionPolicy.FileCarryEnabled
                && (e.Data.GetDataPresent(DataFormats.FileDrop) || e.Data.GetDataPresent(DataFormats.UnicodeText));
            if (updateDrop || carryDrop)
            {
                e.Effects = DragDropEffects.Copy;
                if (!dropPreviewActive)
                {
                    dropPreviewActive = true;
                    if (!exclusiveAnimation && dockState == DockState.None && PetAssets.SupportsAnimation(app.Store.Data.PetId))
                    {
                        idleGestureTimer.Stop();
                        behaviorAnimation = "drop-preview";
                        ShowAnimationFrame("carry-file", 1);
                    }
                }
                exclusiveStatus = updateDrop ? "松开即可更新" : "准备叼住";
                lastChipText = "";
                UpdateChip();
            }
            else
            {
                e.Effects = DragDropEffects.None;
                EndDropPreview();
            }
            e.Handled = true;
        }

        private void PetDragLeave(object sender, DragEventArgs e)
        {
            EndDropPreview();
        }

        private void EndDropPreview()
        {
            if (!dropPreviewActive) return;
            dropPreviewActive = false;
            exclusiveStatus = "";
            lastChipText = "";
            ResumeBehaviorVisual();
            UpdateChip();
        }

        private void PetDrop(object sender, DragEventArgs e)
        {
            EndDropPreview();
            string updatePackage;
            if (TryGetDroppedUpdatePackage(e.Data, out updatePackage))
            {
                app.UpdateInstaller.BeginImport(updatePackage, this, UpdateToast);
                e.Handled = true;
                return;
            }

            int count = 0;
            if (PetInteractionPolicy.FileCarryEnabled && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
                count = app.Carry.AddFiles(files);
            }
            else if (PetInteractionPolicy.FileCarryEnabled && e.Data.GetDataPresent(DataFormats.UnicodeText))
            {
                string value = e.Data.GetData(DataFormats.UnicodeText) as string;
                if (!string.IsNullOrWhiteSpace(value))
                {
                    string kind = Uri.IsWellFormedUriString(value.Trim(), UriKind.Absolute) ? "link" : "text";
                    app.Store.AddCarryItem(kind, value.Trim(), Formatters.Truncate(value.Trim(), 32));
                    count = 1;
                }
            }
            if (count > 0)
            {
                PlayCarryAnimation();
                EventToast("叼住了 " + count + " 件；单击小满可查看或放下");
                app.RefreshOpenWindows();
            }
            e.Handled = true;
        }

        private static bool TryGetDroppedUpdatePackage(System.Windows.IDataObject data, out string packagePath)
        {
            packagePath = null;
            if (data == null || !data.GetDataPresent(DataFormats.FileDrop)) return false;
            return UpdateDropPolicy.TrySelectSinglePackage(data.GetData(DataFormats.FileDrop) as string[], out packagePath);
        }

        public void ShowMeeting(MeetingInfo meeting)
        {
            if (meeting == null || QuietModeActive) return;
            if (meetingWindow != null) try { meetingWindow.Close(); } catch { }
            meetingWindow = new MeetingRadarWindow(app, this, meeting);
            meetingWindow.ShowNearPet();
        }

        // ---------------- 行为表演 ----------------

        private static string ActionFor(BehaviorState state)
        {
            if (state == BehaviorState.Typing) return "typing";
            if (state == BehaviorState.Sleepy || state == BehaviorState.Away) return "sleep";
            return "idle";
        }

        private static string ChipFor(BehaviorState state)
        {
            switch (state)
            {
                case BehaviorState.Meeting: return "开会中";
                case BehaviorState.Typing: return "敲键盘中";
                case BehaviorState.Reading: return "阅读中";
                case BehaviorState.Watching: return "看视频";
                case BehaviorState.Thinking: return "思考中";
                case BehaviorState.Sleepy: return "打盹";
                case BehaviorState.Away: return "离开";
                default: return "";
            }
        }

        /// <summary>引擎 500ms 喂一次：行为状态 → 姿态帧 + 氛围层 + 身份胶囊，全部缓动过渡。</summary>
        public void ApplyBehavior(EngineOutput output)
        {
            if (!string.IsNullOrEmpty(previewState)) return;
            if (!app.Store.Data.BehaviorEnabled) output.State = BehaviorState.Idle;
            lastEngineOutput = output;
            behavior = output.State;
            flowActive = output.FlowActive;
            motion.SetParams(output.Arousal, output.Valence);
            motion.SetContextFlags(buildWait, FocusVisualActive);
            BehaviorState directed = visualDirector.Observe(behavior, MonotonicSeconds());
            ObserveWakeStretchTransition(visualBehavior, directed, MonotonicSeconds());
            if (QuietModeActive)
            {
                visualBehavior = directed;
                if (nodOffDir.Active) AbortNodOff("quiet-mode");
                ApplyQuietVisual();
                UpdateChip();
                return;
            }
            if ((motion.PlayfulActive || motion.ShyActive || motion.BalloonActive || patrolActive) && directed != BehaviorState.Idle)
            {
                motion.StopPlayfulShow();
                motion.StopShy();
                motion.StopBalloonGame();
                CancelExclusiveAnimation(false);
            }
            if (directed != visualBehavior)
            {
                BehaviorState previous = visualBehavior;
                if (previous == BehaviorState.Typing) typingPoseTimer.Stop();
                visualBehavior = directed;
                TraceAnimation("DIRECTOR " + previous + " -> " + visualBehavior + " engine=" + behavior);
                if (previous == BehaviorState.Meeting && directed != BehaviorState.Meeting
                    && PetAssets.SupportsAnimation(app.Store.Data.PetId) && dockState == DockState.None && !exclusiveAnimation)
                {
                    motion.SetBehavior(visualBehavior);
                    PlayExclusiveRange("meeting", AnimationPolicy.MeetingExitStartFrame, AnimationPolicy.MeetingExitEndFrame,
                        AnimationPolicy.MeetingFrameIntervalMs, "");
                    UpdateChip();
                    return;
                }
            }
            motion.SetBehavior(visualBehavior);
            // 独占事件期间仍更新“主人当前在做什么”，但不允许识别器把叼取/拉伸动画抢回去。
            if (dockState == DockState.None && !exclusiveAnimation && DateTime.Now >= overrideUntil && !nodOffDir.Active) SetBehaviorVisual(visualBehavior);
            // 困倦点头分镜：Idle 且夜深或低兴奋时偶发小剧场；非 Idle 立即退场
            if (visualBehavior == BehaviorState.Idle) TryStartNodOff(false);
            else if (nodOffDir.Active) AbortNodOff("behavior " + visualBehavior);
            UpdateChip();
            TryPresentFocusCompletion();
        }

        private void SetAction(string action)
        {
            if (action == lastAction) return;
            StopAnimation(false);
            behaviorAnimation = "";
            lastAction = action;
            motion.SetPose(PetAssets.Get(app.Store.Data.PetId, action));
            System.Windows.Automation.AutomationProperties.SetName(frontImage, "桌宠当前状态 " + action);
        }

        private void UpdateChip()
        {
            PetDefinition pet = PetCatalog.Find(app.Store.Data.PetId);
            string stateName;
            if (!string.IsNullOrEmpty(exclusiveStatus)) stateName = exclusiveStatus;
            else if (QuietModeActive) stateName = QuietCompanionPolicy.RemainingLabel(DateTime.Now, quietUntil);
            else if (DateTime.Now < overrideUntil) stateName = "开心";
            else if (visualBehavior == BehaviorState.Meeting) stateName = ChipFor(visualBehavior);
            else if (buildWait) stateName = "等编译中";
            else if (FocusRitualActive) stateName = FocusRitualRemainingLabel;
            else if (flowActive) stateName = "心流中";
            else stateName = ChipFor(visualBehavior);
            MemoItem anchor = app.Store.AnchorMemo();
            string anchorText = anchor == null ? "" : "  ·  在做：" + Formatters.Truncate(anchor.Text, 6);
            int carryCount = PetInteractionPolicy.HasActiveCarry(app.Store.Data.CarryItems.Count) ? app.Store.Data.CarryItems.Count : 0;
            string text = renderedPetSize < 100
                ? PetIdentityChipPolicy.CompactLabel(pet.Name, app.Store.Level, stateName,
                    anchor == null ? "" : anchor.Text, carryCount, app.Store.Data.TrackEnabled)
                : string.Format("{0}  ·  Lv.{1}{2}{3}{4}",
                    pet.Name,
                    app.Store.Level,
                    string.IsNullOrEmpty(stateName) ? "" : "  ·  " + stateName,
                    anchorText + (carryCount > 0 ? "  ·  叼着 " + carryCount + " 件" : ""),
                    app.Store.Data.TrackEnabled ? "" : "  ·  统计暂停");
            if (text == lastChipText) return;
            lastChipText = text;
            identityText.Text = text;
        }

        /// <summary>等编译表演：build-start 进，build-ok/fail 出，10 分钟超时自动退。</summary>
        public void SetBuildWait(bool waiting)
        {
            if (buildWait == waiting) return;
            if (waiting)
            {
                YieldAttentionRitual("build");
                if (nodOffDir.Active) AbortNodOff("build-wait");
            }
            buildWait = waiting;
            motion.SetContextFlags(buildWait, FocusVisualActive);
            if (waiting)
            {
                rareIdleShowTimer.Stop();
                patrolTimer.Stop();
                bool interruptedAutonomousAction = patrolActive || motion.PlayfulActive;
                if (interruptedAutonomousAction) CancelExclusiveAnimation(false);
                if (interruptedAutonomousAction && visualBehavior == BehaviorState.Idle && dockState == DockState.None)
                {
                    behaviorAnimation = "";
                    lastAction = "";
                    SetBehaviorVisual(visualBehavior);
                }
            }
            if (buildWaitTimer == null)
            {
                buildWaitTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(10) };
                buildWaitTimer.Tick += delegate { buildWaitTimer.Stop(); SetBuildWait(false); };
            }
            buildWaitTimer.Stop();
            if (waiting) buildWaitTimer.Start();
            else if (visualBehavior == BehaviorState.Idle)
            {
                ScheduleRareIdleShow();
                SchedulePatrol();
            }
            lastChipText = "";
            UpdateChip();
            TryPresentFocusCompletion();
        }

        private void ObserveWakeStretchTransition(BehaviorState previous, BehaviorState directed, double now)
        {
            bool previousAway = previous == BehaviorState.Away || previous == BehaviorState.Sleepy;
            bool directedAway = directed == BehaviorState.Away || directed == BehaviorState.Sleepy;
            if (directedAway)
            {
                if (!previousAway || visualAwayStartedAt < 0) visualAwayStartedAt = now;
                return;
            }
            if (!previousAway)
            {
                visualAwayStartedAt = -1;
                return;
            }

            double awaySeconds = visualAwayStartedAt < 0 ? 0 : Math.Max(0, now - visualAwayStartedAt);
            visualAwayStartedAt = -1;
            bool busy = QuietModeActive || buildWait || FocusVisualActive || exclusiveAnimation
                || nodOffDir.Active || motion.PlayfulActive || motion.ShyActive || motion.BalloonActive
                || patrolActive || dockState != DockState.None || retreatActive || dragging || dropPreviewActive;
            if (!WakeStretchPolicy.ShouldStart(previous, directed, awaySeconds,
                app.Store.Data.ReducedMotion, busy)) return;
            motion.StartWakeStretch();
            TraceAnimation("WAKE_STRETCH start awaySeconds=" + awaySeconds.ToString("0.0")
                + " active=" + motion.WakeStretchActive);
        }

        public bool PlayWakeStretchDemo()
        {
            visualAwayStartedAt = MonotonicSeconds() - 65;
            ObserveWakeStretchTransition(BehaviorState.Away, BehaviorState.Idle, MonotonicSeconds());
            return motion.WakeStretchActive;
        }

        /// <summary>真实击键 → 单爪即时下压；头和身体来自同一基准帧，不再整身换图。</summary>
        public void OnKeyRhythm()
        {
            if (motion.BubbleAttentionActive) TraceAnimation("BUBBLE_ATTENTION cancel source=keyboard");
            motion.StopBubbleAttention();
            if (motion.WakeStretchActive) TraceAnimation("WAKE_STRETCH cancel source=keyboard");
            motion.StopWakeStretch();
            if (motion.CursorGreetingActive) TraceAnimation("CURSOR_GREETING cancel source=keyboard");
            ResetCursorGreetingObservation(true);
            if (patReactionActive)
            {
                TraceAnimation("PAT cancel source=keyboard");
                CancelExclusiveAnimation(true);
            }
            if (nodOffDir.Active
                && AmbientInterruptionPolicy.ShouldCancel(true, MonotonicSeconds(), ambientInputGraceUntil))
            {
                TraceAnimation("NODOFF cancel source=keyboard");
                AbortNodOff("keyboard");
            }
            bool active = patrolActive || motion.PlayfulActive || motion.ShyActive || motion.BalloonActive;
            if (AmbientInterruptionPolicy.ShouldCancel(active, MonotonicSeconds(), ambientInputGraceUntil))
            {
                TraceAnimation("AMBIENT cancel source=keyboard");
                CancelExclusiveAnimation(true);
            }
            if (!motion.KeyPulse()) return;
            if (visualBehavior != BehaviorState.Typing || dockState != DockState.None || exclusiveAnimation || dropPreviewActive) return;
            TypingPawPose pose;
            if (!typingPawDirector.TryPress(MonotonicSeconds(), out pose)) return;
            ShowTypingSoftPose(pose, false);
            typingPoseTimer.Stop();
            typingPoseTimer.Start();
        }

        public bool PlayPatKeyboardCancelDemo()
        {
            DebugPatReaction();
            bool before = patReactionActive;
            OnKeyRhythm();
            bool after = patReactionActive;
            TraceAnimation("PAT_KEY_CANCEL before=" + before + " after=" + after);
            return before && !after;
        }

        public void OnAmbientUserActivity()
        {
            if (motion.BubbleAttentionActive) TraceAnimation("BUBBLE_ATTENTION cancel source=input");
            motion.StopBubbleAttention();
            if (motion.WakeStretchActive) TraceAnimation("WAKE_STRETCH cancel source=input");
            motion.StopWakeStretch();
            if (motion.CursorGreetingActive) TraceAnimation("CURSOR_GREETING cancel source=input");
            ResetCursorGreetingObservation(true);
            if (nodOffDir.Active
                && AmbientInterruptionPolicy.ShouldCancel(true, MonotonicSeconds(), ambientInputGraceUntil))
            {
                TraceAnimation("NODOFF cancel source=input");
                AbortNodOff("input");
                return;
            }
            // 顶气球本身依赖鼠标点击，不被普通鼠标脉冲取消；键盘仍会在 OnKeyRhythm 中退出游戏。
            bool active = patrolActive || motion.PlayfulActive || motion.ShyActive;
            if (!AmbientInterruptionPolicy.ShouldCancel(active, MonotonicSeconds(), ambientInputGraceUntil)) return;
            TraceAnimation("AMBIENT cancel source=input");
            CancelExclusiveAnimation(true);
        }

        private void TypingPoseTick(object sender, EventArgs e)
        {
            if (visualBehavior != BehaviorState.Typing || dockState != DockState.None || exclusiveAnimation || dropPreviewActive)
            {
                typingPoseTimer.Stop();
                return;
            }
            TypingPawPose pose = typingPawDirector.PoseAt(MonotonicSeconds());
            // 按下必须即时；松爪回静止则复用 65ms dip + 75ms fade 的 140ms 柔和过渡。
            // 这避免 400ms 停顿点出现一个虽小但可见的局部硬切。
            ShowTypingSoftPose(pose, pose == TypingPawPose.Rest);
            if (pose == TypingPawPose.Rest) typingPoseTimer.Stop();
        }

        private void ShowTypingSoftPose(TypingPawPose pose, bool smooth)
        {
            if (pose == displayedTypingPose && frontImage.Source == PetAssets.GetTypingSoftFrame(app.Store.Data.PetId, pose)) return;
            displayedTypingPose = pose;
            BitmapSource source = PetAssets.GetTypingSoftFrame(app.Store.Data.PetId, pose);
            if (source == null) return;
            if (smooth) motion.SetPose(source); else motion.SetFrame(source);
            TraceAnimation("TYPING_SOFT pose=" + pose);
            System.Windows.Automation.AutomationProperties.SetName(frontImage, "桌宠打字姿态 " + pose);
        }

        public void SetPreviewState(string state)
        {
            CancelExclusiveAnimation(false);
            previewState = state;
            BehaviorState parsed = BehaviorState.Idle;
            if (string.Equals(state, "typing", StringComparison.OrdinalIgnoreCase)) parsed = BehaviorState.Typing;
            else if (string.Equals(state, "meeting", StringComparison.OrdinalIgnoreCase)) parsed = BehaviorState.Meeting;
            else if (string.Equals(state, "reading", StringComparison.OrdinalIgnoreCase)) parsed = BehaviorState.Reading;
            else if (string.Equals(state, "watching", StringComparison.OrdinalIgnoreCase)) parsed = BehaviorState.Watching;
            else if (string.Equals(state, "thinking", StringComparison.OrdinalIgnoreCase)) parsed = BehaviorState.Thinking;
            else if (string.Equals(state, "sleep", StringComparison.OrdinalIgnoreCase)) parsed = BehaviorState.Sleepy;
            else if (string.Equals(state, "happy", StringComparison.OrdinalIgnoreCase))
            {
                overrideUntil = DateTime.Now.AddMinutes(10);
                behavior = BehaviorState.Idle;
                SetAction("happy");
                motion.SetBehavior(BehaviorState.Idle);
                UpdateChip();
                return;
            }
            behavior = parsed;
            visualBehavior = parsed;
            visualDirector.Force(parsed, MonotonicSeconds());
            motion.SetBehavior(parsed);
            behaviorAnimation = "";
            SetBehaviorVisual(parsed);
            UpdateChip();
        }

        public void Celebrate(string message)
        {
            if (QuietModeActive) return;
            TraceAnimation("CELEBRATE");
            CancelExclusiveAnimation(false);
            previewState = null;
            overrideUntil = DateTime.Now.AddSeconds(3.2);
            SetAction("happy");
            motion.Squash(0.16);
            UpdateChip();
            ShowToast(message);
            DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3.25) };
            timer.Tick += delegate
            {
                timer.Stop();
                overrideUntil = DateTime.MinValue;
                lastAction = "";
                UpdateChip();
            };
            timer.Start();
        }

        public void EventToast(string message)
        {
            if (QuietModeActive) return;
            ShowToast(Formatters.Truncate(message, 64));
        }

        /// <summary>用户主动拖入更新包后的高优先级反馈；安静模式也必须显示验签结果。</summary>
        public void UpdateToast(string message)
        {
            ShowToast("↻  " + Formatters.Truncate(message, 62));
        }

        /// <summary>能力递送：宠物先做一次轻量反馈，再把可点击提示叼到用户面前。</summary>
        public void CapabilityToast(string cue, string message, bool celebratory)
        {
            if (QuietModeActive) return;
            TraceAnimation("CAPABILITY cue=" + cue);
            if (celebratory && !app.Store.Data.ReducedMotion) motion.Hop();
            else motion.Squash(celebratory ? 0.12 : 0.07);
            if (toast != null) toast.Close();
            string prefix = string.IsNullOrWhiteSpace(cue) ? "" : cue + "  ";
            toast = new ToastWindow(app, this, prefix + message, delegate { app.OpenWorkbench("capabilities"); });
            toast.ShowNearPet();
        }

        public void Farewell(string message)
        {
            TraceAnimation("FAREWELL");
            CancelExclusiveAnimation(false);
            rareIdleShowTimer.Stop();
            motion.SetBehavior(BehaviorState.Sleepy);
            SetAction("sleep");
            motion.Squash(0.08);
            ShowToast(Formatters.Truncate(message, 64));
        }

        /// <summary>升级庆祝：彩带 + 开心姿态 + 提示。</summary>
        public void LevelUp(int level)
        {
            motion.Confetti();
            Celebrate("升到 Lv." + level + " 了，继续一起加油");
        }

        public void ShowWelcome()
        {
            ShowToast("单击快速操作，双击打开工作台；" + HotkeyLabel + " 随时记一笔");
        }

        private void ShowToast(string message)
        {
            if (toast != null) toast.Close();
            toast = new ToastWindow(app, this, message);
            toast.ShowNearPet();
        }

        /// <summary>可点击提示：中断恢复书签专用，点一下回到备忘录。</summary>
        public void ResumeToast(string message)
        {
            if (QuietModeActive) return;
            if (toast != null) toast.Close();
            toast = new ToastWindow(app, this, message, delegate { app.OpenWorkbench("memos"); });
            toast.ShowNearPet();
        }

        private void OpenBubble()
        {
            identityRevealTimer.Stop();
            FadeIdentity(0);
            if (bubble == null) bubble = new BubbleWindow(app, this);
            bubble.ShowForPet();
        }

        public void AcknowledgeBubble(Rect bubbleBounds)
        {
            ResetCursorGreetingObservation(true);
            motion.StopPatAffection();
            motion.StopWakeStretch();
            if (nodOffDir.Active) AbortNodOff("bubble");
            Rect petBounds = new Rect(Left, Top, Width, Height);
            double bias = BubbleAttentionPolicy.HorizontalBias(petBounds, bubbleBounds);
            motion.StartBubbleAttention(bias);
            TraceAnimation("BUBBLE_ATTENTION side=" + (bias < 0 ? "left" : bias > 0 ? "right" : "center")
                + " active=" + motion.BubbleAttentionActive);
        }

        public bool PlayBubbleDemo()
        {
            OpenBubble();
            return bubble != null && bubble.IsVisible;
        }

        private void FadeIdentity(double target)
        {
            if (app.Store.Data.ReducedMotion)
            {
                identityPill.Opacity = target;
                return;
            }
            DoubleAnimation animation = new DoubleAnimation(target, TimeSpan.FromMilliseconds(150));
            animation.EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            identityPill.BeginAnimation(OpacityProperty, animation);
        }

        private void RestorePosition()
        {
            double left = app.Store.Data.PetLeft;
            double top = app.Store.Data.PetTop;
            double virtualLeft = SystemParameters.VirtualScreenLeft;
            double virtualTop = SystemParameters.VirtualScreenTop;
            double virtualRight = virtualLeft + SystemParameters.VirtualScreenWidth;
            double virtualBottom = virtualTop + SystemParameters.VirtualScreenHeight;
            bool valid = left + Width > virtualLeft + 40 && left < virtualRight - 40 && top + Height > virtualTop + 40 && top < virtualBottom - 40;
            if (valid)
            {
                Left = left;
                Top = top;
            }
            else
            {
                Rect area = WindowPlacement.WorkAreaFor(this);
                Left = area.Right - Width - 24;
                Top = area.Bottom - Height - 8;
                app.Store.Data.DockSide = "";
            }

            DockEdge restoredEdge = DockGeometry.ParsePersistedEdge(app.Store.Data.DockSide);
            if (valid && app.Store.Data.EdgeSnapEnabled && restoredEdge == DockEdge.None && app.Store.LoadedSchemaVersion < 6)
            {
                Rect restoredArea = WindowPlacement.WorkAreaFor(this);
                DockEdge inferred = DockGeometry.InferFlushEdge(restoredArea, Left, Top, Width, Height, 8);
                if (inferred != DockEdge.None)
                {
                    List<MonitorInfoEx> all = MonitorHelper.All();
                    IntPtr handle = new WindowInteropHelper(this).Handle;
                    MonitorInfoEx current = MonitorHelper.FindByHandle(all, NativeMethods.MonitorFromWindow(handle, 2));
                    if (current == null || !MonitorHelper.IsCorridorEdge(current, inferred, all)) restoredEdge = inferred;
                }
            }
            if (valid && app.Store.Data.EdgeSnapEnabled && restoredEdge != DockEdge.None)
            {
                dockEdge = restoredEdge;
                dockState = DockState.Snapped;
                app.Store.Data.DockSide = DockGeometry.PersistedEdge(restoredEdge);
                // 等窗口完成首次布局再缩入，确保多屏 + 高 DPI 下工作区来自已恢复的位置。
                Dispatcher.BeginInvoke(new Action(delegate { EnterDock(false); }), DispatcherPriority.Loaded);
            }
        }

        private void ApplyResponsiveDimensions(Rect area, bool preserveCenter)
        {
            double size = ResponsivePetSizing.Resolve(area.Height, app.Store.Data.PetSize, app.Store.Data.PetScaleRatio);
            if (Math.Abs(size - renderedPetSize) < 0.05 && Math.Abs(Width - size) < 0.05) return;
            double oldWidth = double.IsNaN(Width) ? 0 : Width;
            double oldHeight = double.IsNaN(Height) ? 0 : Height;
            double oldLeft = Left;
            double oldTop = Top;
            renderedPetSize = size;
            Width = size;
            Height = size + ResponsivePetSizing.VerticalPadding(size);
            backImage.Width = size;
            backImage.Height = size;
            frontImage.Width = size;
            frontImage.Height = size;
            motion.SetUiScale(size / 240.0);
            identityText.FontSize = Math.Max(8.5, 11 * size / 190.0);
            ambientText.FontSize = Math.Max(8, 10 * size / 190.0);
            ambientPill.Padding = new Thickness(Math.Max(4, 7 * size / 190.0), Math.Max(2, 3 * size / 190.0),
                Math.Max(4, 7 * size / 190.0), Math.Max(2, 3 * size / 190.0));
            identityPill.MaxWidth = Math.Max(48, size - 4);
            identityPill.Padding = new Thickness(Math.Max(7, 11 * size / 190.0), Math.Max(3, 5 * size / 190.0),
                Math.Max(7, 11 * size / 190.0), Math.Max(3, 5 * size / 190.0));
            if (preserveCenter && oldWidth > 0 && oldHeight > 0 && !double.IsNaN(oldLeft) && !double.IsNaN(oldTop))
            {
                Left = oldLeft + (oldWidth - Width) / 2.0;
                Top = oldTop + (oldHeight - Height) / 2.0;
            }
            TraceAnimation("SIZE base=" + app.Store.Data.PetSize + " ratio=" + app.Store.Data.PetScaleRatio.ToString("0.00")
                + " areaH=" + area.Height.ToString("0.0") + " rendered=" + size.ToString("0.0"));
        }

        public void RefreshPet()
        {
            dockRequestToken++;
            retreatDockDelayTimer.Stop();
            // 设置页切换宠物、尺寸或动效偏好时，旧角色的短时注视不能在新角色身上续演。
            // 这类微动作不是 exclusive animation，必须在刷新入口单独让位。
            YieldAttentionRitual("refresh");
            CancelExclusiveAnimation(false);
            Rect sizingArea = PresentationSource.FromVisual(this) != null ? WindowPlacement.WorkAreaFor(this) : SystemParameters.WorkArea;
            ApplyResponsiveDimensions(sizingArea, true);
            Topmost = app.Store.Data.AlwaysOnTop;
            motion.SetReducedMotion(app.Store.Data.ReducedMotion);
            motion.SetPetKind(app.Store.Data.PetId);
            // 切换能量/角色时旧计时器的间隔已经失效：统一停止，再由当前状态按新策略重排。
            idleGestureTimer.Stop();
            rareIdleShowTimer.Stop();
            patrolTimer.Stop();
            if (app.Store.Data.ReducedMotion && nodOffDir.Active) AbortNodOff("reduced-motion");
            PetDefinition pet = PetCatalog.Find(app.Store.Data.PetId);
            System.Windows.Automation.AutomationProperties.SetName(root, "桌面宠物 " + pet.Name);
            lastChipText = "";
            lastAction = "";
            behaviorAnimation = "";
            StopAnimation(false);
            if (dockState == DockState.None)
            {
                MirrorAnimation(false);
                SetSpriteShift(0, 0);
            }
            UpdateChip();
            RefreshAmbientPresenceVisual(false);
            if (dockState == DockState.None)
            {
                if (QuietModeActive) ApplyQuietVisual();
                else SetBehaviorVisual(visualBehavior);
            }
            if (PresentationSource.FromVisual(this) != null)
            {
                Rect area = WindowPlacement.WorkAreaFor(this);
                if (dockState == DockState.None)
                {
                    Left = Math.Max(area.Left + 6, Math.Min(area.Right - Width - 6, Left));
                    Top = Math.Max(area.Top + 6, Math.Min(area.Bottom - Height - 6, Top));
                }
                else RestoreDockAfterRefresh(area);
            }
        }

        public void UpdateAmbientPresence(AmbientSnapshot snapshot)
        {
            ambientPresence = AmbientPresencePolicy.Resolve(snapshot, app.Store.Data.AmbientPresenceEnabled);
            bool changed = ambientPresence != null && !string.Equals(lastAmbientPresenceKey, ambientPresence.Key, StringComparison.Ordinal);
            lastAmbientPresenceKey = ambientPresence == null ? "" : ambientPresence.Key;
            RefreshAmbientPresenceVisual(changed);
        }

        private void RefreshAmbientPresenceVisual(bool animate)
        {
            if (ambientPresence == null || !app.Store.Data.AmbientPresenceEnabled
                || dockState != DockState.None || retreatActive)
            {
                ambientPill.BeginAnimation(OpacityProperty, null);
                ambientPill.Opacity = 0;
                ambientPill.Visibility = Visibility.Collapsed;
                return;
            }
            string label = ambientPresence.Label ?? "";
            if (renderedPetSize < 94 && ambientPresence.Priority >= 80)
            {
                if (ambientPresence.Priority >= 100) label = "雨";
                else if (ambientPresence.Priority >= 90) label = "气";
                else label = "UV";
            }
            ambientText.Text = label;
            ambientText.Foreground = Theme.Brush(ambientPresence.Foreground);
            ambientPill.Background = Theme.Brush(ambientPresence.Background);
            ambientPill.ToolTip = ambientPresence.Tooltip;
            System.Windows.Automation.AutomationProperties.SetHelpText(ambientText, ambientPresence.Tooltip ?? "");
            ambientPill.Visibility = Visibility.Visible;
            ambientPill.BeginAnimation(OpacityProperty, null);
            if (!animate || app.Store.Data.ReducedMotion)
            {
                ambientPill.Opacity = 0.92;
                return;
            }
            ambientPill.Opacity = 0;
            DoubleAnimation fade = new DoubleAnimation(0.92, TimeSpan.FromMilliseconds(190));
            fade.EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            ambientPill.BeginAnimation(OpacityProperty, fade, HandoffBehavior.SnapshotAndReplace);
        }

        private void RestoreDockAfterRefresh(Rect area)
        {
            bool peeking = dockState == DockState.Peeking;
            mover.Cancel();
            dockState = peeking ? DockState.Peeking : DockState.Docked;
            motion.SetDocked(true);
            ApplyDockSpriteClip(!peeking);
            LayoutDockHitArea(true);
            AnimateDockSpriteShift(!peeking, 0);

            bool side = dockEdge == DockEdge.Left || dockEdge == DockEdge.Right;
            if (side && PetAssets.SupportsAnimation(app.Store.Data.PetId))
            {
                MirrorAnimation(dockEdge == DockEdge.Left);
                ShowAnimationFrame("dock-right", peeking ? 24 : 19);
            }
            else
            {
                MirrorAnimation(false);
                motion.SetPose(PetAssets.Get(app.Store.Data.PetId, "sleep"));
            }

            Point target = peeking
                ? DockGeometry.PeekTarget(dockEdge, area, Width, Height, Math.Max(area.Top + 6, Math.Min(area.Bottom - Height - 6, Top)))
                : DockGeometry.SliverTarget(dockEdge, area, Width, Height, Math.Max(area.Top + 6, Math.Min(area.Bottom - Height - 6, Top)));
            if (dockEdge == DockEdge.Top || dockEdge == DockEdge.Bottom)
                target.X = Math.Max(area.Left + 6, Math.Min(area.Right - Width - 6, Left));
            Left = target.X;
            Top = target.Y;
            TraceAnimation("REFRESH_DOCK state=" + dockState + " edge=" + dockEdge
                + " pet=" + app.Store.Data.PetId + " target=" + target);
        }

        public bool PlayRefreshAttentionDemo()
        {
            PlayWakeStretchDemo();
            bool before = AttentionRitualActive;
            RefreshPet();
            bool after = AttentionRitualActive;
            TraceAnimation("REFRESH_ATTENTION before=" + before + " after=" + after);
            return before && !after;
        }

        public bool PlayDockRefreshDemo()
        {
            DebugDock(DockEdge.Right);
            RefreshPet();
            Rect area = WindowPlacement.WorkAreaFor(this);
            Point expected = DockGeometry.SliverTarget(DockEdge.Right, area, Width, Height,
                Math.Max(area.Top + 6, Math.Min(area.Bottom - Height - 6, Top)));
            bool passed = dockState == DockState.Docked && Math.Abs(Left - expected.X) < 0.6
                && Math.Abs(Top - expected.Y) < 0.6;
            TraceAnimation("DOCK_REFRESH_CHECK state=" + dockState + " delta="
                + Math.Abs(Left - expected.X).ToString("0.00") + ","
                + Math.Abs(Top - expected.Y).ToString("0.00") + " passed=" + passed);
            ExitDock(false);
            return passed;
        }

        public bool PlayContextPreemptionDemo()
        {
            // 压力测试可能恰逢自动打盹；先清理测试前置，不能把“策略正确拒绝启动”误报成抢占失败。
            if (QuietModeActive) ExitQuietMode(false);
            if (FocusRitualActive) ExitFocusRitual(false, false);
            if (retreatActive) SetRetreat(false);
            if (dockState != DockState.None) ExitDock(false);
            SetBuildWait(false);
            CancelExclusiveAnimation(false);
            if (nodOffDir.Active) AbortNodOff("context-demo-reset");

            PlayWakeStretchDemo();
            bool buildBefore = AttentionRitualActive;
            SetBuildWait(true);
            bool buildAfter = AttentionRitualActive;
            SetBuildWait(false);

            PlayWakeStretchDemo();
            bool retreatBefore = AttentionRitualActive;
            SetRetreat(true);
            bool retreatAfter = AttentionRitualActive;
            SetRetreat(false);

            PlayWakeStretchDemo();
            bool focusBefore = AttentionRitualActive;
            bool focusStarted = EnterFocusRitual(TimeSpan.FromSeconds(2));
            bool focusAfter = AttentionRitualActive;
            ExitFocusRitual(false, false);

            PlayWakeStretchDemo();
            bool quietBefore = AttentionRitualActive;
            EnterQuietMode(TimeSpan.FromSeconds(2));
            bool quietAfter = AttentionRitualActive;
            ExitQuietMode(false);

            bool passed = buildBefore && !buildAfter && retreatBefore && !retreatAfter
                && focusBefore && focusStarted && !focusAfter && quietBefore && !quietAfter;
            TraceAnimation("CONTEXT_PREEMPT build=" + buildBefore + "/" + buildAfter
                + " retreat=" + retreatBefore + "/" + retreatAfter
                + " focus=" + focusBefore + "/" + focusAfter
                + " quiet=" + quietBefore + "/" + quietAfter + " passed=" + passed);
            return passed;
        }

        public bool PlayBuildNodOffPreemptionDemo()
        {
            SetBuildWait(false);
            CancelExclusiveAnimation(false);
            if (nodOffDir.Active) AbortNodOff("build-demo-reset");
            bool started = TryStartNodOff(true);
            bool before = nodOffDir.Active;
            SetBuildWait(true);
            bool after = nodOffDir.Active;
            bool context = buildWait;
            SetBuildWait(false);
            bool passed = started && before && !after && context;
            TraceAnimation("BUILD_NODOFF before=" + before + " after=" + after
                + " context=" + context + " passed=" + passed);
            return passed;
        }

        public bool PlayRetreatPreemptionDemo()
        {
            bool started = PlayTreatInteraction();
            bool before = exclusiveAnimation && motion.TreatActive;
            SetRetreat(true);
            bool after = exclusiveAnimation || motion.TreatActive;
            SetRetreat(false);
            bool passed = started && before && !after;
            TraceAnimation("RETREAT_PREEMPT before=" + before + " after=" + after + " passed=" + passed);
            return passed;
        }

        private void TraceAnimation(string message)
        {
            if (string.IsNullOrWhiteSpace(animationTraceFile)) return;
            try
            {
                string directory = System.IO.Path.GetDirectoryName(animationTraceFile);
                if (!string.IsNullOrEmpty(directory)) System.IO.Directory.CreateDirectory(directory);
                System.IO.File.AppendAllText(animationTraceFile,
                    DateTime.Now.ToString("HH:mm:ss.fff") + " " + message + Environment.NewLine,
                    new System.Text.UTF8Encoding(false));
            }
            catch { }
        }
    }

    public sealed class BubbleWindow : Window
    {
        private readonly WorkMateApp app;
        private readonly PetWindow pet;
        private readonly Border shell;

        public BubbleWindow(WorkMateApp app, PetWindow pet)
        {
            this.app = app;
            this.pet = pet;
            Title = "WorkMate 快捷气泡";
            Width = 280;
            Height = 548;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            Topmost = true;
            WindowStartupLocation = WindowStartupLocation.Manual;
            WindowPrivacy.Bind(this, delegate { return app.Store.Data.HideFromCaptureEnabled; }, false);
            shell = new Border
            {
                Background = Theme.Surface,
                BorderBrush = Theme.Line,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(24),
                Padding = new Thickness(18),
                Effect = Theme.Shadow(28, 0.18, 7)
            };
            Content = shell;
            Deactivated += delegate { Hide(); };
        }

        public void ShowForPet()
        {
            BuildContent();
            Topmost = app.Store.Data.AlwaysOnTop;
            Rect area = WindowPlacement.WorkAreaFor(pet);
            double desiredLeft = pet.Left - Width - 12;
            if (desiredLeft < area.Left + 8) desiredLeft = pet.Left + pet.Width + 12;
            Left = Math.Max(area.Left + 8, Math.Min(area.Right - Width - 8, desiredLeft));
            Top = Math.Max(area.Top + 8, Math.Min(area.Bottom - Height - 8, pet.Top + pet.Height - Height));
            pet.AcknowledgeBubble(new Rect(Left, Top, Width, Height));
            if (!IsVisible) Show();
            Activate();
            NativeMethods.ForceForeground(new WindowInteropHelper(this).Handle);
        }

        private void BuildContent()
        {
            PetDefinition definition = PetCatalog.Find(app.Store.Data.PetId);
            StackPanel content = new StackPanel();
            Grid header = new Grid { Margin = new Thickness(2, 0, 2, 13) };
            header.ColumnDefinitions.Add(new ColumnDefinition());
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            StackPanel identity = new StackPanel();
            identity.Children.Add(Theme.Text(definition.Name, 18, Theme.Ink, FontWeights.Bold));
            identity.Children.Add(Theme.Text(definition.Species + "  ·  Lv." + app.Store.Level, 11, Theme.Muted, FontWeights.Normal));
            header.Children.Add(identity);
            Border value = Theme.Pill("陪伴值 " + app.Store.Data.CompanionValue, Theme.AccentSoft, Theme.Accent);
            Grid.SetColumn(value, 1);
            header.Children.Add(value);
            content.Children.Add(header);

            Grid progress = Theme.Progress(Growth.LevelProgress(app.Store.Data.CompanionValue), Theme.Brush(definition.Accent), 7);
            progress.Margin = new Thickness(2, 0, 2, 16);
            content.Children.Add(progress);

            Button capture = RowButton("记一笔", pet.HotkeyLabel);
            capture.Click += delegate { Hide(); app.OpenQuickCapture(); };
            content.Children.Add(capture);
            MemoItem anchorMemo = app.Store.AnchorMemo();
            if (anchorMemo != null)
            {
                Button completeAnchor = RowButton("完成当前任务", Formatters.Truncate(anchorMemo.Text, 8));
                completeAnchor.Click += delegate
                {
                    bool dailyPriority = app.Store.IsDailyPriority(anchorMemo);
                    MemoItem done = app.Store.CompleteAnchor();
                    Hide();
                    if (done != null) { app.NotifyPositiveFeedback(); app.Pet.Celebrate((dailyPriority ? "今日一件事完成：" : "完成：") + Formatters.Truncate(done.Text, 12)); }
                    app.RefreshOpenWindows();
                };
                content.Children.Add(completeAnchor);
            }
            Button memos = RowButton("打开备忘录", OutstandingCount() + " 条进行中");
            memos.Click += delegate { Hide(); app.OpenWorkbench("memos"); };
            content.Children.Add(memos);
            Button capabilities = RowButton("能力中心", "OCR · 天气 · 离线更新");
            capabilities.Click += delegate { Hide(); app.OpenWorkbench("capabilities"); };
            content.Children.Add(capabilities);
            int carried = app.Store.Data.CarryItems.Count;
            if (PetInteractionPolicy.FileCarryEnabled && carried > 0)
            {
                Button carry = RowButton(definition.Name + "叼着", carried + " 件待放下");
                carry.Click += delegate { Hide(); app.OpenCarryShelf(); };
                content.Children.Add(carry);
            }
            else
            {
                Grid play = new Grid { Margin = new Thickness(0, 1, 0, 1) };
                play.ColumnDefinitions.Add(new ColumnDefinition());
                play.ColumnDefinitions.Add(new ColumnDefinition());
                play.ColumnDefinitions.Add(new ColumnDefinition());
                Button treat = PlayButton("喂饼干", "投喂互动", "#FFF3EA", "#FCE6D7");
                treat.Margin = new Thickness(0, 0, 2, 0);
                treat.Click += delegate { Hide(); pet.PlayTreatInteraction(); };
                play.Children.Add(treat);
                Button summon = PlayButton("叫过来", "跑到鼠标旁", "#EEF2FF", "#E2E8FB");
                summon.Margin = new Thickness(2, 0, 2, 0);
                summon.Click += delegate { Hide(); pet.SummonToCursor(); };
                Grid.SetColumn(summon, 1);
                play.Children.Add(summon);
                Button balloon = PlayButton("顶气球", "10 秒小游戏", "#F4EEFF", "#EAE0FA");
                balloon.Margin = new Thickness(2, 0, 0, 0);
                balloon.Click += delegate { Hide(); pet.PlayBalloonGame(); };
                Grid.SetColumn(balloon, 2);
                play.Children.Add(balloon);
                content.Children.Add(play);

                Grid rituals = new Grid { Margin = new Thickness(0, 3, 0, 1) };
                rituals.ColumnDefinitions.Add(new ColumnDefinition());
                rituals.ColumnDefinitions.Add(new ColumnDefinition());
                Button highFive = PlayButton("击个掌", "举爪等你", "#FFF0F2", "#FCE2E8");
                highFive.Margin = new Thickness(0, 0, 2, 0);
                highFive.Click += delegate { Hide(); pet.InviteHighFive(); };
                rituals.Children.Add(highFive);
                Button focus = PlayButton(pet.FocusRitualActive ? "结束专注" : "专注 25 分",
                    pet.FocusRitualActive ? pet.FocusRitualRemainingLabel : "倒计时陪伴", "#EDF8F2", "#DFF1E7");
                focus.Margin = new Thickness(2, 0, 0, 0);
                focus.Click += delegate { Hide(); pet.ToggleFocusRitual(); };
                Grid.SetColumn(focus, 1);
                rituals.Children.Add(focus);
                content.Children.Add(rituals);
            }
            Button quiet = RowButton(pet.QuietModeActive ? "结束安静陪伴" : "安静陪伴 · 30分",
                pet.QuietModeActive ? pet.QuietRemainingLabel : definition.Name + " · 蜷睡中，不主动提醒");
            quiet.Click += delegate { Hide(); pet.ToggleQuietMode(); };
            content.Children.Add(quiet);
            MeetingInfo nextMeeting = app.MeetingRadar == null ? null : app.MeetingRadar.NextMeeting;
            if (nextMeeting != null)
            {
                Button meeting = RowButton("下一场会议", nextMeeting.Start.ToString("HH:mm") + "  " + Formatters.Truncate(nextMeeting.Subject, 9));
                meeting.Click += delegate { Hide(); pet.ShowMeeting(nextMeeting); };
                content.Children.Add(meeting);
            }
            Button today = RowButton("今日小结", Formatters.Duration(app.Store.TodayActiveSeconds));
            today.Click += delegate { Hide(); app.OpenWorkbench("today"); };
            content.Children.Add(today);
            Button tracking = RowButton(app.Store.Data.TrackEnabled ? "暂停时间统计" : "恢复时间统计", "不记录窗口标题");
            tracking.Click += delegate
            {
                app.Store.Data.TrackEnabled = !app.Store.Data.TrackEnabled;
                app.Store.Save();
                pet.RefreshPet();
                BuildContent();
            };
            content.Children.Add(tracking);

            Button hud = RowButton(app.HudVisible ? "关闭状态调试" : "状态调试", "看 " + definition.Name + " 怎么理解你");
            hud.Click += delegate
            {
                app.ToggleHud();
                BuildContent();
            };
            content.Children.Add(hud);

            Button demo = RowButton(app.DemoModeActive ? "退出演示模式" : "演示模式", "投屏时藏起桌宠");
            demo.Click += delegate
            {
                app.ToggleDemoMode();
                BuildContent();
            };
            content.Children.Add(demo);

            Border line = new Border { Height = 1, Background = Theme.Line, Margin = new Thickness(2, 8, 2, 8) };
            content.Children.Add(line);
            Grid footer = new Grid();
            footer.ColumnDefinitions.Add(new ColumnDefinition());
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Button settings = Theme.GhostButton("设置");
            settings.HorizontalAlignment = HorizontalAlignment.Left;
            settings.Click += delegate { Hide(); app.OpenWorkbench("settings"); };
            footer.Children.Add(settings);
            Button exit = Theme.GhostButton("退出");
            exit.Foreground = Theme.Faint;
            exit.Click += delegate { app.ExitApp(); };
            Grid.SetColumn(exit, 1);
            footer.Children.Add(exit);
            content.Children.Add(footer);
            shell.Child = content;
            content.Measure(new Size(Math.Max(1, Width - 36), double.PositiveInfinity));
            Height = BubbleSizing.Resolve(content.DesiredSize.Height);
        }

        private int OutstandingCount()
        {
            return app.Store.Data.Memos.Count(delegate(MemoItem memo) { return !memo.IsDone; });
        }

        private Button RowButton(string title, string hint)
        {
            Grid grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            TextBlock name = Theme.Text(title, 13, Theme.Ink, FontWeights.SemiBold);
            name.TextWrapping = TextWrapping.NoWrap;
            name.TextTrimming = TextTrimming.CharacterEllipsis;
            grid.Children.Add(name);
            TextBlock note = Theme.Text(hint, 11, Theme.Faint, FontWeights.Normal);
            note.TextWrapping = TextWrapping.NoWrap;
            note.TextTrimming = TextTrimming.CharacterEllipsis;
            note.MaxWidth = 102;
            note.Margin = new Thickness(10, 0, 0, 0);
            Grid.SetColumn(note, 1);
            grid.Children.Add(note);
            Button button = Theme.Button("", Theme.Surface, Theme.SoftSurface, Theme.Ink, 12);
            button.Content = grid;
            button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            button.Padding = new Thickness(12, 10, 12, 10);
            button.Margin = new Thickness(0, 1, 0, 1);
            System.Windows.Automation.AutomationProperties.SetName(button, title);
            return button;
        }

        private Button PlayButton(string title, string hint, string normalHex, string hoverHex)
        {
            StackPanel stack = new StackPanel();
            stack.Children.Add(Theme.Text(title, 12, Theme.Ink, FontWeights.SemiBold));
            stack.Children.Add(Theme.Text(hint, 9.5, Theme.Faint, FontWeights.Normal));
            Button button = Theme.Button("", Theme.Brush(normalHex), Theme.Brush(hoverHex), Theme.Ink, 12);
            button.Content = stack;
            button.HorizontalContentAlignment = HorizontalAlignment.Left;
            button.Padding = new Thickness(11, 8, 8, 8);
            System.Windows.Automation.AutomationProperties.SetName(button, title);
            return button;
        }
    }

    public sealed class ToastWindow : Window
    {
        private readonly PetWindow pet;
        private readonly WorkMateApp app;
        private readonly DispatcherTimer closeTimer;

        public ToastWindow(WorkMateApp app, PetWindow pet, string message) : this(app, pet, message, null) { }

        public ToastWindow(WorkMateApp app, PetWindow pet, string message, Action clickAction)
        {
            this.app = app;
            this.pet = pet;
            Title = "WorkMate 提示";
            Width = 330;
            Height = 82;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            ShowActivated = false;
            Topmost = true;
            WindowStartupLocation = WindowStartupLocation.Manual;
            WindowPrivacy.Bind(this, delegate { return app.Store.Data.HideFromCaptureEnabled; }, true);

            StackPanel text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            PetDefinition definition = PetCatalog.Find(app.Store.Data.PetId);
            text.Children.Add(Theme.Text(definition.Name + (clickAction != null ? "  ·  点我继续" : ""), 11, Theme.Brush("#F8CFC7"), FontWeights.SemiBold));
            TextBlock body = Theme.Text(message, 13, Brushes.White, FontWeights.SemiBold);
            body.Margin = new Thickness(0, 3, 0, 0);
            text.Children.Add(body);
            UIElement shell;
            if (clickAction != null)
            {
                Button action = Theme.Button("", Theme.Brush("#F22F2926"), Theme.Brush("#FF433A36"), Brushes.White, 19);
                action.Content = text;
                action.Padding = new Thickness(17, 12, 17, 12);
                action.Effect = Theme.Shadow(24, 0.2, 6);
                System.Windows.Automation.AutomationProperties.SetName(action, "打开相关页面：" + message);
                System.Windows.Automation.AutomationProperties.SetHelpText(action, "单击打开 WorkMate 对应页面");
                action.Click += delegate { Close(); clickAction(); };
                shell = action;
            }
            else shell = new Border
            {
                Background = Theme.Brush("#F22F2926"),
                CornerRadius = new CornerRadius(19),
                Padding = new Thickness(17, 12, 17, 12),
                Child = text,
                Effect = Theme.Shadow(24, 0.2, 6)
            };
            Content = shell;
            Opacity = app.Store.Data.ReducedMotion ? 1 : 0;
            closeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(clickAction != null ? 6.5 : 2.8) };
            closeTimer.Tick += delegate { closeTimer.Stop(); Close(); };
        }

        public void ShowNearPet()
        {
            Rect area = WindowPlacement.WorkAreaFor(pet);
            double desiredLeft = pet.Left - Width + pet.Width;
            Left = Math.Max(area.Left + 8, Math.Min(area.Right - Width - 8, desiredLeft));
            Top = Math.Max(area.Top + 8, pet.Top - Height - 8);
            Show();
            if (!app.Store.Data.ReducedMotion)
            {
                DoubleAnimation fade = new DoubleAnimation(1, TimeSpan.FromMilliseconds(170));
                fade.EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut };
                BeginAnimation(OpacityProperty, fade);
            }
            closeTimer.Start();
        }
    }
}
