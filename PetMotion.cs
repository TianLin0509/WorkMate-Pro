using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace WorkMatePro
{
    /// <summary>
    /// 丝滑层：褪色过渡（先淡出再淡入，任何时刻最多一只猫——消灭双猫重叠）
    /// + 呼吸/侧倾正弦补间（dt 驱动，速率随 arousal）+ 压扁拉伸弹簧
    /// + 漂浮氛围层（Zzz/♪/?/摸头爱心/升级彩带）+ 朝向偏置 + 自适应帧率。
    /// 氛围层只是环境效果，主美术永远来自素材帧——程序绘不当主角。
    /// </summary>
    public sealed class PetMotion
    {
        private readonly Grid motionLayer;
        private readonly ScaleTransform scale;
        private readonly RotateTransform tilt;
        private readonly TranslateTransform lift;
        private readonly Image backImage;
        private readonly Image frontImage;
        private readonly Canvas overlay;
        private readonly System.Windows.Shapes.Ellipse contactShadow;
        private double uiScale;

        private readonly TextBlock[] zzz = new TextBlock[3];
        private readonly TextBlock[] notes = new TextBlock[2];
        private readonly Border thought;
        private readonly TextBlock thoughtText;
        private readonly TextBlock[] hearts = new TextBlock[2];
        private readonly TextBlock[] shyMarks = new TextBlock[2];
        private readonly System.Windows.Shapes.Line balloonString;
        private readonly System.Windows.Shapes.Ellipse balloon;
        private readonly TextBlock balloonSpark;
        private readonly System.Windows.Shapes.Ellipse hulaHoop;
        private readonly System.Windows.Shapes.Ellipse hulaBead;
        private readonly RotateTransform hulaRotate = new RotateTransform();
        private readonly Border bookLeft;
        private readonly Border bookRight;
        private readonly System.Windows.Shapes.Line bookSpine;
        private readonly System.Windows.Shapes.Line[] bookLines = new System.Windows.Shapes.Line[4];
        private readonly System.Windows.Shapes.Path headsetBand;
        private readonly Border headsetLeft;
        private readonly Border headsetRight;
        private readonly System.Windows.Shapes.Line headsetMic;
        private readonly System.Windows.Shapes.Ellipse focusHalo;
        private readonly System.Windows.Shapes.Path focusProgressArc;
        private readonly System.Windows.Shapes.Ellipse focusProgressDot;
        private readonly System.Windows.Shapes.Path hourglass;
        private readonly System.Windows.Shapes.Path quietMoon;
        private readonly Canvas butterfly;
        private readonly System.Windows.Shapes.Ellipse butterflyLeftWing;
        private readonly System.Windows.Shapes.Ellipse butterflyRightWing;
        private readonly System.Windows.Shapes.Ellipse butterflyBody;
        private readonly ScaleTransform butterflyScale = new ScaleTransform(1, 1);
        private readonly System.Windows.Shapes.Ellipse[] soapBubbles = new System.Windows.Shapes.Ellipse[3];
        private readonly Canvas treatSnack;
        private readonly System.Windows.Shapes.Path treatSnackShape;
        private readonly System.Windows.Shapes.Ellipse treatSnackDetail;
        private readonly ScaleTransform treatScale = new ScaleTransform(1, 1);
        private readonly RotateTransform treatRotate = new RotateTransform();
        private readonly System.Windows.Shapes.Ellipse[] treatCrumbs = new System.Windows.Shapes.Ellipse[3];
        private readonly TextBlock treatJoy;
        private readonly Canvas highFivePaw;
        private readonly ScaleTransform highFiveScale = new ScaleTransform(1, 1);
        private readonly RotateTransform highFiveRotate = new RotateTransform();
        private readonly Canvas highFiveWing;
        private readonly ScaleTransform highFiveWingScale = new ScaleTransform(1, 1);
        private readonly RotateTransform highFiveWingRotate = new RotateTransform();
        private readonly System.Windows.Shapes.Line[] highFiveSparks = new System.Windows.Shapes.Line[4];
        private readonly System.Windows.Shapes.Ellipse[] confetti = new System.Windows.Shapes.Ellipse[12];
        private readonly double[] confVX = new double[12];
        private readonly double[] confVY = new double[12];
        private static readonly string[] ConfettiColors = { "#E95D4F", "#F2B84B", "#3D8C6A", "#5B8DEF", "#B085D8" };

        private BehaviorState behavior = BehaviorState.Idle;
        private double arousal = 0.4;
        private double valence = 0.55;
        private bool reducedMotion;
        private bool docked;
        private bool hidden;
        private bool dragging;
        private double dragTilt;
        private double dragTiltTarget;
        private double dragLagX;
        private double dragLagXTarget;
        private double dragLift;
        private double dragLiftTarget;
        private DateTime dragVelocityAt = DateTime.MinValue;

        private double phase;
        private DateTime lastTick = DateTime.MinValue;
        private DateTime reactionUntil = DateTime.MinValue;
        private DateTime squashStart = DateTime.MinValue;
        private double squashPower;
        private DateTime lastNod = DateTime.MinValue;
        private DateTime nodStart = DateTime.MinValue;
        private readonly TypingRhythm rhythm = new TypingRhythm();
        private DateTime hopStart = DateTime.MinValue;
        private DateTime landStart = DateTime.MinValue;
        private DateTime yawnStart = DateTime.MinValue;
        private DateTime lastYawn = DateTime.MinValue;
        private DateTime heartStart = DateTime.MinValue;
        private DateTime confettiStart = DateTime.MinValue;
        private double attentionTargetX;
        private double attentionTargetY;
        private double attentionX;
        private double attentionY;
        private RareIdleShowType playfulShow = RareIdleShowType.None;
        private DateTime playfulStart = DateTime.MinValue;
        private DateTime playfulUntil = DateTime.MinValue;
        private DateTime shyStart = DateTime.MinValue;
        private DateTime shyUntil = DateTime.MinValue;
        private DateTime balloonUntil = DateTime.MinValue;
        private DateTime balloonLastTick = DateTime.MinValue;
        private DateTime balloonSparkStart = DateTime.MinValue;
        private BalloonToyState balloonState;
        private Rect balloonBounds;
        private DateTime treatStart = DateTime.MinValue;
        private DateTime treatUntil = DateTime.MinValue;
        private TreatSnackKind treatSnackKind = TreatSnackKind.Fish;
        private DateTime highFiveStart = DateTime.MinValue;
        private DateTime highFiveUntil = DateTime.MinValue;
        private DateTime highFiveHitAt = DateTime.MinValue;
        private bool highFiveUsesWing;
        private DateTime patAffectionStart = DateTime.MinValue;
        private DateTime patAffectionUntil = DateTime.MinValue;
        private double patAffectionBias;
        private DateTime cursorGreetingStart = DateTime.MinValue;
        private DateTime cursorGreetingUntil = DateTime.MinValue;
        private double cursorGreetingBias;
        private DateTime wakeStretchStart = DateTime.MinValue;
        private DateTime wakeStretchUntil = DateTime.MinValue;
        private DateTime bubbleAttentionStart = DateTime.MinValue;
        private DateTime bubbleAttentionUntil = DateTime.MinValue;
        private double bubbleAttentionBias;
        private bool patrolling;
        private double patrolDirection = 1;
        private DateTime patrolStart = DateTime.MinValue;
        private bool buildWaiting;
        private bool flowActive;
        private double focusRitualProgress = -1;
        private bool quietCompanion;

        private bool transitionRunning;
        private System.Windows.Media.Imaging.BitmapSource pendingPose;

        private DispatcherTimer sustainedTimer;
        private DispatcherTimer drowsyTimer;
        private bool renderingHooked;
        private bool disposed;
        private string fpsMode = "0 停止";

        public PetMotion(Grid motionLayer, ScaleTransform scale, RotateTransform tilt, TranslateTransform lift, Image backImage, Image frontImage,
            Canvas overlay, System.Windows.Shapes.Ellipse contactShadow, double uiScale)
        {
            this.motionLayer = motionLayer;
            this.scale = scale;
            this.tilt = tilt;
            this.lift = lift;
            this.backImage = backImage;
            this.frontImage = frontImage;
            this.overlay = overlay;
            this.contactShadow = contactShadow;
            this.uiScale = uiScale;
            rhythm.MinIntervalSec = AnimationPolicy.TypingEventIntervalMs / 1000.0;

            for (int i = 0; i < 3; i++)
            {
                zzz[i] = new TextBlock
                {
                    Text = "Z",
                    FontFamily = Theme.Font,
                    FontWeight = FontWeights.Bold,
                    Foreground = Theme.Brush("#8A94A6"),
                    FontSize = Math.Max(9, (12 + i * 4) * uiScale),
                    Opacity = 0
                };
                overlay.Children.Add(zzz[i]);
            }
            for (int i = 0; i < 2; i++)
            {
                notes[i] = new TextBlock
                {
                    Text = "♪",
                    FontFamily = Theme.Font,
                    Foreground = Theme.Brush("#B085D8"),
                    FontSize = Math.Max(10, (15 + i * 3) * uiScale),
                    Opacity = 0
                };
                overlay.Children.Add(notes[i]);
            }
            thoughtText = Theme.Text("?", 15 * uiScale, Theme.Muted, FontWeights.Bold);
            thoughtText.TextWrapping = TextWrapping.NoWrap;
            thought = new Border
            {
                Background = Theme.Brush("#F2FFFFFF"),
                CornerRadius = new CornerRadius(12 * uiScale),
                Padding = new Thickness(8 * uiScale, 3 * uiScale, 8 * uiScale, 3 * uiScale),
                Effect = Theme.Shadow(8, 0.12, 2),
                Child = thoughtText,
                Opacity = 0
            };
            overlay.Children.Add(thought);

            for (int i = 0; i < 2; i++)
            {
                hearts[i] = new TextBlock
                {
                    Text = "♥",
                    FontFamily = Theme.Font,
                    FontWeight = FontWeights.Bold,
                    Foreground = Theme.Brush("#E95D4F"),
                    FontSize = Math.Max(11, (16 + i * 4) * uiScale),
                    Opacity = 0
                };
                overlay.Children.Add(hearts[i]);
            }
            for (int i = 0; i < shyMarks.Length; i++)
            {
                shyMarks[i] = new TextBlock
                {
                    Text = "///",
                    FontFamily = Theme.Font,
                    FontWeight = FontWeights.Bold,
                    Foreground = Theme.Brush("#EF7890"),
                    FontSize = Math.Max(8, 11 * uiScale),
                    Opacity = 0,
                    IsHitTestVisible = false
                };
                overlay.Children.Add(shyMarks[i]);
            }
            balloonString = new System.Windows.Shapes.Line
            {
                Stroke = Theme.Brush("#9A8174"),
                StrokeThickness = Math.Max(0.8, 1.2 * uiScale),
                Opacity = 0,
                IsHitTestVisible = false
            };
            overlay.Children.Add(balloonString);
            RadialGradientBrush balloonBrush = new RadialGradientBrush
            {
                Center = new Point(0.34, 0.28),
                GradientOrigin = new Point(0.28, 0.20),
                RadiusX = 0.75,
                RadiusY = 0.75
            };
            balloonBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#FFF1F4"), 0));
            balloonBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#F26F8D"), 0.42));
            balloonBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#C94367"), 1));
            balloon = new System.Windows.Shapes.Ellipse
            {
                Fill = balloonBrush,
                Stroke = Theme.Brush("#F8D4DE"),
                StrokeThickness = Math.Max(1, 1.4 * uiScale),
                Opacity = 0,
                IsHitTestVisible = false
            };
            overlay.Children.Add(balloon);
            balloonSpark = new TextBlock
            {
                Text = "✦",
                FontFamily = Theme.Font,
                FontWeight = FontWeights.Bold,
                Foreground = Theme.Brush("#F2C14E"),
                FontSize = Math.Max(10, 14 * uiScale),
                Opacity = 0,
                IsHitTestVisible = false
            };
            overlay.Children.Add(balloonSpark);
            LinearGradientBrush hoopBrush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0.5),
                EndPoint = new Point(1, 0.5)
            };
            hoopBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#EF6F91"), 0));
            hoopBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#F2C14E"), 0.34));
            hoopBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#63B7AF"), 0.67));
            hoopBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#8E79C6"), 1));
            hulaHoop = new System.Windows.Shapes.Ellipse
            {
                Stroke = hoopBrush,
                StrokeThickness = Math.Max(AnimationPolicy.HulaMinStrokeDip, 4.2 * uiScale),
                Opacity = 0,
                IsHitTestVisible = false,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = hulaRotate,
                Effect = Theme.Shadow(7, 0.34, 0)
            };
            overlay.Children.Add(hulaHoop);
            hulaBead = new System.Windows.Shapes.Ellipse
            {
                Fill = Theme.Brush("#FFF5C7"),
                Stroke = Theme.Brush("#EAAE37"),
                StrokeThickness = 1,
                Opacity = 0,
                IsHitTestVisible = false,
                Effect = Theme.Shadow(5, 0.30, 0)
            };
            overlay.Children.Add(hulaBead);

            // 情境道具层：只用几何图形解释已有状态，不依赖字体图标，也不抢鼠标命中。
            bookLeft = new Border
            {
                Background = Theme.Brush("#FFF8E8"),
                BorderBrush = Theme.Brush("#C88B64"),
                BorderThickness = new Thickness(Math.Max(1, 1.5 * uiScale)),
                CornerRadius = new CornerRadius(5, 2, 2, 5),
                Opacity = 0,
                IsHitTestVisible = false,
                Effect = Theme.Shadow(6, 0.16, 2)
            };
            bookRight = new Border
            {
                Background = Theme.Brush("#FFFDF4"),
                BorderBrush = Theme.Brush("#C88B64"),
                BorderThickness = new Thickness(Math.Max(1, 1.5 * uiScale)),
                CornerRadius = new CornerRadius(2, 5, 5, 2),
                Opacity = 0,
                IsHitTestVisible = false,
                Effect = Theme.Shadow(6, 0.16, 2)
            };
            bookSpine = new System.Windows.Shapes.Line
            {
                Stroke = Theme.Brush("#A9674D"),
                StrokeThickness = Math.Max(1, 1.4 * uiScale),
                Opacity = 0,
                IsHitTestVisible = false
            };
            overlay.Children.Add(bookLeft);
            overlay.Children.Add(bookRight);
            overlay.Children.Add(bookSpine);
            for (int i = 0; i < bookLines.Length; i++)
            {
                bookLines[i] = new System.Windows.Shapes.Line
                {
                    Stroke = Theme.Brush("#CFA987"),
                    StrokeThickness = Math.Max(0.7, 0.9 * uiScale),
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round,
                    Opacity = 0,
                    IsHitTestVisible = false
                };
                overlay.Children.Add(bookLines[i]);
            }

            headsetBand = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse("M 3,22 C 3,4 37,4 37,22"),
                Stretch = Stretch.Fill,
                Stroke = Theme.Brush("#6877A8"),
                StrokeThickness = Math.Max(2.2, 3.4 * uiScale),
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Opacity = 0,
                IsHitTestVisible = false,
                Effect = Theme.Shadow(6, 0.18, 1)
            };
            headsetLeft = new Border { Background = Theme.Brush("#5E6D9C"), CornerRadius = new CornerRadius(5), Opacity = 0, IsHitTestVisible = false };
            headsetRight = new Border { Background = Theme.Brush("#5E6D9C"), CornerRadius = new CornerRadius(5), Opacity = 0, IsHitTestVisible = false };
            headsetMic = new System.Windows.Shapes.Line
            {
                Stroke = Theme.Brush("#5E6D9C"),
                StrokeThickness = Math.Max(1.6, 2.2 * uiScale),
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Opacity = 0,
                IsHitTestVisible = false
            };
            overlay.Children.Add(headsetBand);
            overlay.Children.Add(headsetLeft);
            overlay.Children.Add(headsetRight);
            overlay.Children.Add(headsetMic);

            LinearGradientBrush focusBrush = new LinearGradientBrush(
                (Color)ColorConverter.ConvertFromString("#A9C8FF"),
                (Color)ColorConverter.ConvertFromString("#91D7B7"), 0);
            focusHalo = new System.Windows.Shapes.Ellipse
            {
                Fill = Brushes.Transparent,
                Stroke = focusBrush,
                StrokeThickness = Math.Max(2.0, 2.8 * uiScale),
                Opacity = 0,
                IsHitTestVisible = false,
                Effect = Theme.Shadow(9, 0.26, 0)
            };
            overlay.Children.Add(focusHalo);
            focusProgressArc = new System.Windows.Shapes.Path
            {
                Stroke = Theme.Brush("#4FA887"),
                StrokeThickness = Math.Max(2.2, 3.2 * uiScale),
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Fill = Brushes.Transparent,
                Opacity = 0,
                IsHitTestVisible = false,
                Effect = Theme.Shadow(6, 0.18, 0)
            };
            overlay.Children.Add(focusProgressArc);
            focusProgressDot = new System.Windows.Shapes.Ellipse
            {
                Fill = Theme.Brush("#4FA887"),
                Stroke = Theme.Brush("#F7FFFB"),
                StrokeThickness = Math.Max(0.8, 1.0 * uiScale),
                Opacity = 0,
                IsHitTestVisible = false,
                Effect = Theme.Shadow(5, 0.16, 0)
            };
            overlay.Children.Add(focusProgressDot);
            hourglass = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse("M 3,2 H27 M3,28 H27 M5,3 C5,10 25,20 25,27 M25,3 C25,10 5,20 5,27 M8,22 L15,15 L22,22 Z"),
                Stretch = Stretch.Fill,
                Stroke = Theme.Brush("#B67943"),
                Fill = Theme.Brush("#F3C76A"),
                StrokeThickness = Math.Max(1.6, 2.2 * uiScale),
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Opacity = 0,
                IsHitTestVisible = false,
                Effect = Theme.Shadow(7, 0.18, 1)
            };
            overlay.Children.Add(hourglass);
            quietMoon = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse("M 19,2 A 14,14 0 1 0 28,25 A 11,11 0 0 1 19,2 Z"),
                Stretch = Stretch.Fill,
                Fill = Theme.Brush("#F5C85B"),
                Stroke = Theme.Brush("#D79C38"),
                StrokeThickness = Math.Max(1, 1.4 * uiScale),
                Opacity = 0,
                IsHitTestVisible = false,
                Effect = Theme.Shadow(9, 0.24, 0)
            };
            overlay.Children.Add(quietMoon);

            butterfly = new Canvas
            {
                Opacity = 0,
                IsHitTestVisible = false,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = butterflyScale
            };
            butterflyLeftWing = new System.Windows.Shapes.Ellipse
            {
                Fill = Theme.Brush("#F5A4BC"),
                Stroke = Theme.Brush("#B95B7D"),
                StrokeThickness = Math.Max(0.8, 1.1 * uiScale)
            };
            butterflyRightWing = new System.Windows.Shapes.Ellipse
            {
                Fill = Theme.Brush("#A99AE8"),
                Stroke = Theme.Brush("#6E5EAE"),
                StrokeThickness = Math.Max(0.8, 1.1 * uiScale)
            };
            butterflyBody = new System.Windows.Shapes.Ellipse { Fill = Theme.Brush("#6D4A52") };
            butterfly.Children.Add(butterflyLeftWing);
            butterfly.Children.Add(butterflyRightWing);
            butterfly.Children.Add(butterflyBody);
            overlay.Children.Add(butterfly);

            for (int i = 0; i < soapBubbles.Length; i++)
            {
                RadialGradientBrush soap = new RadialGradientBrush
                {
                    Center = new Point(0.35, 0.32),
                    GradientOrigin = new Point(0.28, 0.24),
                    RadiusX = 0.75,
                    RadiusY = 0.75
                };
                soap.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#F7FFFFFF"), 0));
                soap.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#72F5B8CE"), 0.50));
                soap.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#557E74D8"), 1));
                soapBubbles[i] = new System.Windows.Shapes.Ellipse
                {
                    Fill = soap,
                    Stroke = Theme.Brush("#F4D7F0"),
                    StrokeThickness = Math.Max(1.4, 1.8 * uiScale),
                    Opacity = 0,
                    IsHitTestVisible = false,
                    Effect = Theme.Shadow(5, 0.12, 0)
                };
                overlay.Children.Add(soapBubbles[i]);
            }

            // 用户主动投喂：食物按物种换成鱼/胡萝卜/葵花籽/骨形饼干，沿同一条连续弧线送到嘴边。
            TransformGroup treatTransforms = new TransformGroup();
            treatTransforms.Children.Add(treatScale);
            treatTransforms.Children.Add(treatRotate);
            treatSnack = new Canvas
            {
                Opacity = 0,
                IsHitTestVisible = false,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = treatTransforms
            };
            treatSnackShape = new System.Windows.Shapes.Path
            {
                Stretch = Stretch.Uniform,
                StrokeThickness = Math.Max(1, 1.4 * uiScale),
                StrokeLineJoin = PenLineJoin.Round,
                Effect = Theme.Shadow(7, 0.22, 2)
            };
            treatSnackDetail = new System.Windows.Shapes.Ellipse
            {
                Fill = Theme.Brush("#3E4750"),
                IsHitTestVisible = false
            };
            treatSnack.Children.Add(treatSnackShape);
            treatSnack.Children.Add(treatSnackDetail);
            ApplyTreatSnackKind();
            overlay.Children.Add(treatSnack);
            for (int i = 0; i < treatCrumbs.Length; i++)
            {
                treatCrumbs[i] = new System.Windows.Shapes.Ellipse
                {
                    Fill = Theme.Brush(i == 1 ? "#F5D28C" : "#C9894B"),
                    Opacity = 0,
                    IsHitTestVisible = false
                };
                overlay.Children.Add(treatCrumbs[i]);
            }
            treatJoy = new TextBlock
            {
                Text = "♥",
                FontFamily = Theme.Font,
                FontWeight = FontWeights.Bold,
                Foreground = Theme.Brush("#ED6F88"),
                FontSize = Math.Max(11, 17 * uiScale),
                Opacity = 0,
                IsHitTestVisible = false,
                Effect = Theme.Shadow(5, 0.16, 0)
            };
            overlay.Children.Add(treatJoy);

            // 用户主动击掌：柔和的小爪只在邀请期出现；主体仍使用当前角色的 happy 姿态。
            TransformGroup highFiveTransforms = new TransformGroup();
            highFiveTransforms.Children.Add(highFiveScale);
            highFiveTransforms.Children.Add(highFiveRotate);
            highFivePaw = new Canvas
            {
                Width = 42,
                Height = 48,
                Opacity = 0,
                IsHitTestVisible = false,
                RenderTransformOrigin = new Point(0.5, 0.8),
                RenderTransform = highFiveTransforms
            };
            RadialGradientBrush pawBrush = new RadialGradientBrush
            {
                Center = new Point(0.38, 0.30),
                GradientOrigin = new Point(0.30, 0.22),
                RadiusX = 0.78,
                RadiusY = 0.78
            };
            pawBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#FFF7F3"), 0));
            pawBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#F7CFC7"), 0.72));
            pawBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#EFAAA4"), 1));
            System.Windows.Shapes.Ellipse pawPalm = new System.Windows.Shapes.Ellipse
            {
                Width = 29,
                Height = 32,
                Fill = pawBrush,
                Stroke = Theme.Brush("#E99A98"),
                StrokeThickness = 1.4,
                Effect = Theme.Shadow(7, 0.18, 2)
            };
            Canvas.SetLeft(pawPalm, 7);
            Canvas.SetTop(pawPalm, 15);
            highFivePaw.Children.Add(pawPalm);
            for (int i = 0; i < 4; i++)
            {
                System.Windows.Shapes.Ellipse toe = new System.Windows.Shapes.Ellipse
                {
                    Width = 9.5,
                    Height = 13.5,
                    Fill = pawBrush,
                    Stroke = Theme.Brush("#E99A98"),
                    StrokeThickness = 1.1
                };
                Canvas.SetLeft(toe, 2 + i * 9.1);
                Canvas.SetTop(toe, i == 0 || i == 3 ? 6 : 1);
                highFivePaw.Children.Add(toe);
            }
            overlay.Children.Add(highFivePaw);

            TransformGroup wingTransforms = new TransformGroup();
            wingTransforms.Children.Add(highFiveWingScale);
            wingTransforms.Children.Add(highFiveWingRotate);
            highFiveWing = new Canvas
            {
                Width = 42,
                Height = 48,
                Opacity = 0,
                IsHitTestVisible = false,
                RenderTransformOrigin = new Point(0.45, 0.82),
                RenderTransform = wingTransforms
            };
            LinearGradientBrush wingBrush = new LinearGradientBrush(
                (Color)ColorConverter.ConvertFromString("#FFFDF9"),
                (Color)ColorConverter.ConvertFromString("#F5C9CB"), 55);
            System.Windows.Shapes.Path wing = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse("M 8,35 C 4,20 7,7 17,3 C 22,12 23,18 21,25 C 27,18 32,18 36,22 C 33,33 25,41 13,44 C 10,42 9,39 8,35 Z"),
                Fill = wingBrush,
                Stroke = Theme.Brush("#E99A98"),
                StrokeThickness = 1.5,
                Effect = Theme.Shadow(7, 0.18, 2)
            };
            highFiveWing.Children.Add(wing);
            for (int i = 0; i < 3; i++)
            {
                System.Windows.Shapes.Path feather = new System.Windows.Shapes.Path
                {
                    Data = Geometry.Parse("M 13," + (25 + i * 5) + " C 19," + (22 + i * 4) + " 24," + (23 + i * 4) + " 29," + (21 + i * 3)),
                    Stroke = Theme.Brush("#E8B1B5"),
                    StrokeThickness = 1.1,
                    Opacity = 0.78
                };
                highFiveWing.Children.Add(feather);
            }
            overlay.Children.Add(highFiveWing);
            for (int i = 0; i < highFiveSparks.Length; i++)
            {
                highFiveSparks[i] = new System.Windows.Shapes.Line
                {
                    Stroke = Theme.Brush(i % 2 == 0 ? "#F2B84B" : "#EF7890"),
                    StrokeThickness = Math.Max(1.4, 1.8 * uiScale),
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round,
                    Opacity = 0,
                    IsHitTestVisible = false
                };
                overlay.Children.Add(highFiveSparks[i]);
            }

            for (int i = 0; i < 3; i++)
            {
                dizzyStars[i] = new TextBlock
                {
                    Text = "✦",
                    FontFamily = Theme.Font,
                    FontWeight = FontWeights.Bold,
                    Foreground = Theme.Brush("#F2C14E"),
                    FontSize = Math.Max(12, 13 * uiScale),
                    Opacity = 0
                };
                overlay.Children.Add(dizzyStars[i]);
            }
            Random confRnd = new Random();
            for (int i = 0; i < confetti.Length; i++)
            {
                confetti[i] = new System.Windows.Shapes.Ellipse
                {
                    Width = 6 * uiScale,
                    Height = 6 * uiScale,
                    Fill = Theme.Brush(ConfettiColors[confRnd.Next(ConfettiColors.Length)]),
                    Opacity = 0
                };
                overlay.Children.Add(confetti[i]);
            }

            sustainedTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(42) }; // 约 24fps 常态
            sustainedTimer.Tick += delegate { Tick(); };
            drowsyTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) }; // 4fps 睡眠
            drowsyTimer.Tick += delegate { Tick(); };
        }

        public void SetUiScale(double value)
        {
            uiScale = Math.Max(0.30, Math.Min(1.20, value));
            for (int i = 0; i < zzz.Length; i++) zzz[i].FontSize = Math.Max(9, (12 + i * 4) * uiScale);
            for (int i = 0; i < notes.Length; i++) notes[i].FontSize = Math.Max(10, (15 + i * 3) * uiScale);
            thoughtText.FontSize = Math.Max(10, 15 * uiScale);
            thought.CornerRadius = new CornerRadius(12 * uiScale);
            thought.Padding = new Thickness(8 * uiScale, 3 * uiScale, 8 * uiScale, 3 * uiScale);
            for (int i = 0; i < hearts.Length; i++) hearts[i].FontSize = Math.Max(11, (16 + i * 4) * uiScale);
            for (int i = 0; i < shyMarks.Length; i++) shyMarks[i].FontSize = Math.Max(8, 11 * uiScale);
            balloonString.StrokeThickness = Math.Max(0.8, 1.2 * uiScale);
            balloon.StrokeThickness = Math.Max(1, 1.4 * uiScale);
            balloonSpark.FontSize = Math.Max(10, 14 * uiScale);
            hulaHoop.StrokeThickness = Math.Max(AnimationPolicy.HulaMinStrokeDip, 4.2 * uiScale);
            hulaBead.Width = hulaBead.Height = Math.Max(5, 7 * uiScale);
            bookLeft.BorderThickness = bookRight.BorderThickness = new Thickness(Math.Max(1, 1.5 * uiScale));
            bookSpine.StrokeThickness = Math.Max(1, 1.4 * uiScale);
            for (int i = 0; i < bookLines.Length; i++) bookLines[i].StrokeThickness = Math.Max(0.7, 0.9 * uiScale);
            headsetBand.StrokeThickness = Math.Max(2.2, 3.4 * uiScale);
            headsetMic.StrokeThickness = Math.Max(1.6, 2.2 * uiScale);
            focusHalo.StrokeThickness = Math.Max(2.0, 2.8 * uiScale);
            focusProgressArc.StrokeThickness = Math.Max(2.2, 3.2 * uiScale);
            focusProgressDot.StrokeThickness = Math.Max(0.8, 1.0 * uiScale);
            hourglass.StrokeThickness = Math.Max(1.6, 2.2 * uiScale);
            quietMoon.StrokeThickness = Math.Max(1, 1.4 * uiScale);
            butterflyLeftWing.StrokeThickness = butterflyRightWing.StrokeThickness = Math.Max(0.8, 1.1 * uiScale);
            for (int i = 0; i < soapBubbles.Length; i++) soapBubbles[i].StrokeThickness = Math.Max(1.4, 1.8 * uiScale);
            treatJoy.FontSize = Math.Max(11, 17 * uiScale);
            for (int i = 0; i < confetti.Length; i++)
            {
                confetti[i].Width = 6 * uiScale;
                confetti[i].Height = 6 * uiScale;
            }
        }

        public void SetPetKind(string petId)
        {
            highFiveUsesWing = HighFivePolicy.UsesWing(petId);
            treatSnackKind = TreatSnackPolicy.ForPet(petId);
            ApplyTreatSnackKind();
            highFivePaw.Opacity = 0;
            highFiveWing.Opacity = 0;
        }

        private void ApplyTreatSnackKind()
        {
            if (treatSnackShape == null) return;
            string data;
            string fill;
            string stroke;
            if (treatSnackKind == TreatSnackKind.Carrot)
            {
                data = "M11,8 C15,5 24,7 28,11 C27,20 21,31 14,36 C11,29 8,18 11,8 Z M14,8 C10,5 9,1 12,1 C16,3 17,6 17,9 Z M19,9 C19,4 23,1 25,3 C25,7 23,9 20,11 Z";
                fill = "#F28B45"; stroke = "#C96832";
            }
            else if (treatSnackKind == TreatSnackKind.Seeds)
            {
                data = "M3,13 C6,7 14,6 18,10 C17,16 10,20 5,18 C3,17 2,15 3,13 Z M15,21 C18,15 27,15 31,20 C29,26 22,29 17,27 C15,26 14,23 15,21 Z M18,5 C22,1 29,3 31,8 C27,12 22,13 18,10 C17,9 17,7 18,5 Z";
                fill = "#E4B96A"; stroke = "#96653A";
            }
            else if (treatSnackKind == TreatSnackKind.Biscuit)
            {
                data = "M9,11 C6,7 2,8 2,12 C2,15 5,17 8,16 L26,25 C28,28 32,28 34,25 C36,22 33,19 30,19 L12,10 C11,10 10,10 9,11 Z M8,20 C5,18 2,20 2,23 C2,27 6,28 9,25 L27,16 C30,17 34,15 34,12 C34,8 30,7 27,10 Z";
                fill = "#E7B66D"; stroke = "#A66D35";
            }
            else
            {
                data = "M5,18 C10,8 25,7 32,18 C25,29 10,28 5,18 Z M6,18 L0,10 L0,26 Z";
                fill = "#7FC6D1"; stroke = "#3D8898";
            }
            treatSnackShape.Data = Geometry.Parse(data);
            treatSnackShape.Fill = Theme.Brush(fill);
            treatSnackShape.Stroke = Theme.Brush(stroke);
            treatSnackDetail.Fill = Theme.Brush(treatSnackKind == TreatSnackKind.Carrot ? "#5FA66B"
                : treatSnackKind == TreatSnackKind.Seeds ? "#6D472E"
                : treatSnackKind == TreatSnackKind.Biscuit ? "#FFF0C2" : "#344E5B");
        }

        public string FpsMode { get { return fpsMode; } }
        public bool IsDisposed { get { return disposed; } }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            hidden = true;
            UnhookRendering();
            sustainedTimer.Stop();
            drowsyTimer.Stop();
            StopPlayfulShow();
            StopShy();
            StopBalloonGame();
            StopTreat();
            StopHighFive();
            StopPatAffection();
            StopCursorGreeting();
            StopWakeStretch();
            StopBubbleAttention();
            StopPatrol();
            fpsMode = "0 已释放";
        }

        public void SetReducedMotion(bool value)
        {
            reducedMotion = value;
            if (value)
            {
                scale.ScaleX = 1; scale.ScaleY = 1; tilt.Angle = 0; lift.X = 0; lift.Y = 0;
                attentionTargetX = attentionTargetY = attentionX = attentionY = 0;
                StopPlayfulShow();
                StopShy();
                StopBalloonGame();
                StopTreat();
                StopHighFive();
                StopPatAffection();
                StopCursorGreeting();
                StopWakeStretch();
                StopBubbleAttention();
                StopPatrol();
            }
            Evaluate();
        }

        public void SetHidden(bool value)
        {
            hidden = value;
            if (value) contactShadow.Opacity = 0;
            Evaluate();
        }

        public void SetDocked(bool value)
        {
            docked = value;
            if (value)
            {
                contactShadow.Opacity = 0;
                StopPatAffection();
                StopCursorGreeting();
                StopWakeStretch();
                StopBubbleAttention();
            }
        }

        public void SetDragging(bool value, bool landOnRelease = true)
        {
            if (dragging && !value && landOnRelease) Land();
            dragging = value;
            if (value)
            {
                StopPatAffection();
                StopCursorGreeting();
                StopWakeStretch();
                StopBubbleAttention();
                dragTilt = dragTiltTarget = dragLagX = dragLagXTarget = 0;
                dragLift = dragLiftTarget = -4.0;
                dragVelocityAt = DateTime.MinValue;
                EnterReaction(0.5);
            }
            else
            {
                dragTilt = dragTiltTarget = dragLagX = dragLagXTarget = dragLift = dragLiftTarget = 0;
                dragVelocityAt = DateTime.MinValue;
            }
            Evaluate();
        }

        public void SetDragVelocity(double velocityX, double velocityY)
        {
            if (!dragging || reducedMotion) return;
            DragLiftPose pose = DragLiftPosePolicy.Resolve(velocityX, velocityY);
            dragTiltTarget = pose.TiltDegrees;
            dragLagXTarget = pose.LagXDip;
            dragLiftTarget = pose.LiftDip;
            dragVelocityAt = DateTime.Now;
        }

        public void Land()
        {
            if (reducedMotion) return;
            landStart = DateTime.Now;
            EnterReaction(0.55);
        }

        public void SetBehavior(BehaviorState state)
        {
            if (behavior == state) return;
            behavior = state;
            EnterReaction(0.9); // 状态切换后短暂满帧，让过渡丝滑
        }

        public void SetParams(double newArousal, double newValence)
        {
            arousal = newArousal;
            valence = newValence;
        }

        public void SetContextFlags(bool isBuildWaiting, bool isFlowActive)
        {
            buildWaiting = isBuildWaiting;
            flowActive = isFlowActive;
        }

        public void SetFocusRitualProgress(double elapsedProgress)
        {
            focusRitualProgress = elapsedProgress < 0 ? -1 : Math.Max(0, Math.Min(1, elapsedProgress));
            if (focusRitualProgress < 0)
            {
                focusProgressArc.Opacity = 0;
                focusProgressDot.Opacity = 0;
            }
        }

        public void SetQuietCompanion(bool active)
        {
            quietCompanion = active;
            if (!active) quietMoon.Opacity = 0;
        }

        /// <summary>真实击键限频门。爪子由 TypingPawDirector 驱动，身体不再随按键缩放或倾斜。</summary>
        public bool KeyPulse()
        {
            if (reducedMotion || behavior != BehaviorState.Typing) return false;
            double now = Environment.TickCount / 1000.0;
            if (!rhythm.Accept(now)) return false;
            return true;
        }

        /// <summary>跳跃（周五雀跃/升级之外的轻快事件）。</summary>
        public void Hop()
        {
            if (reducedMotion) return;
            hopStart = DateTime.Now;
            EnterReaction(0.8);
        }

        public bool StartPlayfulShow(RareIdleShowType show)
        {
            if (reducedMotion || show == RareIdleShowType.None) return false;
            playfulShow = show;
            playfulStart = DateTime.Now;
            int duration = AnimationPolicy.DurationFor(show);
            playfulUntil = playfulStart.AddMilliseconds(duration);
            EnterReaction(duration / 1000.0 + 0.25);
            return true;
        }

        public void StopPlayfulShow()
        {
            playfulShow = RareIdleShowType.None;
            playfulUntil = DateTime.MinValue;
            hulaHoop.Opacity = 0;
            hulaBead.Opacity = 0;
            butterfly.Opacity = 0;
            for (int i = 0; i < soapBubbles.Length; i++) soapBubbles[i].Opacity = 0;
        }

        public bool PlayfulActive { get { return playfulShow != RareIdleShowType.None && DateTime.Now < playfulUntil; } }

        public bool StartShy()
        {
            if (reducedMotion) return false;
            shyStart = DateTime.Now;
            shyUntil = shyStart.AddMilliseconds(2300);
            EnterReaction(2.55);
            return true;
        }

        public void StopShy()
        {
            shyUntil = DateTime.MinValue;
            for (int i = 0; i < shyMarks.Length; i++) shyMarks[i].Opacity = 0;
        }

        public bool ShyActive { get { return DateTime.Now < shyUntil; } }

        public bool StartBalloonGame()
        {
            if (reducedMotion) return false;
            balloonState = BalloonToyPhysics.Initial();
            balloonLastTick = DateTime.Now;
            balloonUntil = balloonLastTick.AddSeconds(10);
            EnterReaction(10.3);
            return true;
        }

        public void StopBalloonGame()
        {
            balloonUntil = DateTime.MinValue;
            balloon.Opacity = 0;
            balloonString.Opacity = 0;
            balloonSpark.Opacity = 0;
            balloonBounds = Rect.Empty;
        }

        public bool BalloonActive { get { return DateTime.Now < balloonUntil; } }

        public bool StartTreat()
        {
            treatStart = DateTime.Now;
            treatUntil = treatStart.AddSeconds(TreatMotionPolicy.DurationSeconds);
            EnterReaction(TreatMotionPolicy.DurationSeconds + 0.2);
            return true;
        }

        public void StopTreat()
        {
            treatUntil = DateTime.MinValue;
            treatSnack.Opacity = 0;
            treatJoy.Opacity = 0;
            for (int i = 0; i < treatCrumbs.Length; i++) treatCrumbs[i].Opacity = 0;
        }

        public bool TreatActive { get { return DateTime.Now < treatUntil; } }

        public bool StartHighFive()
        {
            highFiveStart = DateTime.Now;
            highFiveHitAt = DateTime.MinValue;
            highFiveUntil = highFiveStart.AddSeconds(HighFivePolicy.InviteSeconds);
            EnterReaction(HighFivePolicy.InviteSeconds + HighFivePolicy.CelebrateSeconds + 0.25);
            return true;
        }

        public bool CompleteHighFive()
        {
            if (!HighFiveActive || highFiveHitAt != DateTime.MinValue) return false;
            highFiveHitAt = DateTime.Now;
            highFiveUntil = highFiveHitAt.AddSeconds(HighFivePolicy.CelebrateSeconds);
            EnterReaction(HighFivePolicy.CelebrateSeconds + 0.2);
            return true;
        }

        public void StopHighFive()
        {
            highFiveUntil = DateTime.MinValue;
            highFiveHitAt = DateTime.MinValue;
            highFivePaw.Opacity = 0;
            highFiveWing.Opacity = 0;
            for (int i = 0; i < highFiveSparks.Length; i++) highFiveSparks[i].Opacity = 0;
        }

        public bool HighFiveActive { get { return DateTime.Now < highFiveUntil; } }
        public bool HighFiveCelebrating { get { return HighFiveActive && highFiveHitAt != DateTime.MinValue; } }

        public void StartPatAffection(double pointerBias)
        {
            if (reducedMotion) return;
            patAffectionBias = Math.Max(-1, Math.Min(1, pointerBias));
            patAffectionStart = DateTime.Now;
            patAffectionUntil = patAffectionStart.AddSeconds(PatAffectionPolicy.DurationSeconds);
            EnterReaction(PatAffectionPolicy.DurationSeconds + 0.15);
        }

        public void StopPatAffection()
        {
            patAffectionUntil = DateTime.MinValue;
            patAffectionStart = DateTime.MinValue;
            patAffectionBias = 0;
        }

        public bool PatAffectionActive { get { return DateTime.Now < patAffectionUntil; } }

        public void StartCursorGreeting(double horizontalBias)
        {
            if (reducedMotion || docked || dragging) return;
            cursorGreetingBias = Math.Max(-1, Math.Min(1, horizontalBias));
            cursorGreetingStart = DateTime.Now;
            cursorGreetingUntil = cursorGreetingStart.AddSeconds(CursorGreetingPolicy.DurationSeconds);
            EnterReaction(CursorGreetingPolicy.DurationSeconds + 0.12);
        }

        public void StopCursorGreeting()
        {
            cursorGreetingStart = DateTime.MinValue;
            cursorGreetingUntil = DateTime.MinValue;
            cursorGreetingBias = 0;
        }

        public bool CursorGreetingActive { get { return DateTime.Now < cursorGreetingUntil; } }

        public void StartWakeStretch()
        {
            if (reducedMotion || docked || dragging) return;
            StopCursorGreeting();
            wakeStretchStart = DateTime.Now;
            wakeStretchUntil = wakeStretchStart.AddSeconds(WakeStretchPolicy.DurationSeconds);
            EnterReaction(WakeStretchPolicy.DurationSeconds + 0.12);
        }

        public void StopWakeStretch()
        {
            wakeStretchStart = DateTime.MinValue;
            wakeStretchUntil = DateTime.MinValue;
        }

        public bool WakeStretchActive { get { return DateTime.Now < wakeStretchUntil; } }

        public void StartBubbleAttention(double horizontalBias)
        {
            if (reducedMotion || docked || dragging) return;
            StopCursorGreeting();
            StopPatAffection();
            StopWakeStretch();
            bubbleAttentionBias = Math.Max(-1, Math.Min(1, horizontalBias));
            bubbleAttentionStart = DateTime.Now;
            bubbleAttentionUntil = bubbleAttentionStart.AddSeconds(BubbleAttentionPolicy.DurationSeconds);
            EnterReaction(BubbleAttentionPolicy.DurationSeconds + 0.12);
        }

        public void StopBubbleAttention()
        {
            bubbleAttentionStart = DateTime.MinValue;
            bubbleAttentionUntil = DateTime.MinValue;
            bubbleAttentionBias = 0;
        }

        public bool BubbleAttentionActive { get { return DateTime.Now < bubbleAttentionUntil; } }

        public bool HitBalloon(Point localPoint)
        {
            return BalloonActive && !balloonBounds.IsEmpty && balloonBounds.Contains(localPoint);
        }

        public void BopBalloon(Point localPoint)
        {
            if (!BalloonActive) return;
            double center = balloonBounds.IsEmpty ? 0 : balloonBounds.Left + balloonBounds.Width / 2;
            double bias = balloonBounds.IsEmpty ? 0 : (localPoint.X - center) / Math.Max(1, balloonBounds.Width / 2);
            balloonState = BalloonToyPhysics.Bop(balloonState, bias);
            balloonSparkStart = DateTime.Now;
            Hop();
        }

        public void StartPatrol(bool facingRight)
        {
            if (reducedMotion) return;
            patrolling = true;
            patrolDirection = facingRight ? 1 : -1;
            patrolStart = DateTime.Now;
            EnterReaction(8);
        }

        public void StopPatrol()
        {
            patrolling = false;
        }

        public bool PatrolActive { get { return patrolling; } }

        /// <summary>深夜哈欠：22 点后低 arousal 时偶发大慢拉伸。</summary>
        private void MaybeYawn(DateTime now)
        {
            if (!YawnWindow(now, arousal)) return;
            if ((now - lastYawn).TotalSeconds < 75) return;
            lastYawn = now;
            yawnStart = now;
        }

        /// <summary>打哈欠窗口（纯函数，可单测）：深夜 ≥22 点低兴奋，或清晨 8-10 点中低兴奋。</summary>
        public static bool YawnWindow(DateTime now, double arousalValue)
        {
            if (now.Hour >= 22 && arousalValue < 0.35) return true;
            if (now.Hour >= 8 && now.Hour < 10 && arousalValue < 0.5) return true;
            return false;
        }

        /// <summary>踩奶：被摸舒服了之后双爪交替揉按（慢节奏、轻重渐变）。</summary>
        public void StartKnead(double seconds)
        {
            if (reducedMotion) return;
            kneadStart = DateTime.Now;
            kneadUntil = kneadStart.AddSeconds(Math.Max(1, seconds));
            EnterReaction(seconds + 0.3);
        }

        public bool KneadActive { get { return DateTime.Now < kneadUntil; } }

        private DateTime kneadStart = DateTime.MinValue;
        private DateTime kneadUntil = DateTime.MinValue;
        public void SetAttentionTarget(double x, double y)
        {
            attentionTargetX = Math.Max(-1, Math.Min(1, x));
            attentionTargetY = Math.Max(-1, Math.Min(1, y));
        }

        /// <summary>困倦点头分镜的姿态通道：倾斜/纵向压缩/"!" 闪现，由 NodOffDirector 每帧驱动。</summary>
        public void SetNodOffPose(double tilt, double scaleY, bool startleFlash)
        {
            nodOffHold = true;
            nodOffTilt = tilt;
            nodOffScaleY = scaleY;
            nodOffStartle = startleFlash;
        }

        public void ClearNodOffPose()
        {
            nodOffHold = false;
            nodOffStartle = false;
            thoughtText.Text = "?";
        }

        private bool nodOffHold;
        private double nodOffTilt;
        private double nodOffScaleY = 1;
        private bool nodOffStartle;

        public void SetDizzy(bool active)
        {
            if (active && !dizzyActive)
            {
                dizzyActive = true;
                dizzyStart = DateTime.Now;
                EnterReaction(1.8);
            }
            else if (!active) dizzyActive = false;
        }

        private bool dizzyActive;
        private DateTime dizzyStart = DateTime.MinValue;
        private readonly TextBlock[] dizzyStars = new TextBlock[3];

        public bool DizzyActive { get { return dizzyActive; } }
        public void SetSquishHold(bool active, double biasX)
        {
            squishHold = active;
            squishBias = Math.Max(-1, Math.Min(1, biasX));
            if (active) EnterReaction(30); // 按住期间保持流畅
            else Evaluate();
        }

        private bool squishHold;
        private double squishBias;

        public void Squash(double power)
        {
            if (reducedMotion) return;
            squashStart = DateTime.Now;
            squashPower = power;
            EnterReaction(1.2);
        }

        /// <summary>摸头反馈：两颗爱心飘起。</summary>
        public void Heart()
        {
            if (reducedMotion) return;
            heartStart = DateTime.Now;
            EnterReaction(1.3);
        }

        /// <summary>升级庆祝：彩带迸发。</summary>
        public void Confetti()
        {
            if (reducedMotion) return;
            Random rnd = new Random();
            for (int i = 0; i < confetti.Length; i++)
            {
                confVX[i] = (rnd.NextDouble() - 0.5) * 160;
                confVY[i] = -110 - rnd.NextDouble() * 130;
            }
            confettiStart = DateTime.Now;
            EnterReaction(1.8);
        }

        public void EnterReaction(double seconds)
        {
            reactionUntil = DateTime.Now.AddSeconds(seconds);
            Evaluate();
        }

        /// <summary>褪色过渡换姿态：先淡出当前姿态，再淡入新姿态——任何时刻屏幕里最多一只猫。</summary>
        public void SetPose(System.Windows.Media.Imaging.BitmapSource pose)
        {
            if (pose == null) return;
            if (transitionRunning)
            {
                pendingPose = pose; // 过渡中来了新姿态：排队，以最新为准
                return;
            }
            if (frontImage.Source == pose && frontImage.Opacity > 0.95) return;
            if (reducedMotion)
            {
                backImage.Source = null;
                frontImage.Source = pose;
                frontImage.Opacity = 1;
                return;
            }
            transitionRunning = true;
            pendingPose = pose;
            // 竞品常用的是“轻微 dip”而非完全消失：65ms 降到 55%，换姿态后 75ms 回到 100%。
            // 全程只有一张图，既不叠影，也不会像 PPT 硬切或突然闪没。
            DoubleAnimation fadeOut = new DoubleAnimation(0.55, TimeSpan.FromMilliseconds(65));
            fadeOut.EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn };
            fadeOut.Completed += delegate { SwapToPending(); };
            frontImage.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            EnterReaction(0.45);
        }

        /// <summary>连续精灵帧专用：取消姿态淡变并原位换帧，避免 80ms 帧被 270ms 淡入淡出吞掉。</summary>
        public void SetFrame(System.Windows.Media.Imaging.BitmapSource pose)
        {
            if (pose == null) return;
            pendingPose = null;
            transitionRunning = false;
            frontImage.BeginAnimation(UIElement.OpacityProperty, null);
            backImage.BeginAnimation(UIElement.OpacityProperty, null);
            backImage.Source = null;
            backImage.Opacity = 0;
            frontImage.Source = pose;
            frontImage.Opacity = 1;
        }

        private void SwapToPending()
        {
            System.Windows.Media.Imaging.BitmapSource next = pendingPose;
            pendingPose = null;
            if (next != null && next != frontImage.Source)
            {
                backImage.Source = null;
                frontImage.Source = next;
            }
            DoubleAnimation fadeIn = new DoubleAnimation(1, TimeSpan.FromMilliseconds(75));
            fadeIn.EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            fadeIn.Completed += delegate
            {
                transitionRunning = false;
                if (pendingPose != null)
                {
                    System.Windows.Media.Imaging.BitmapSource queued = pendingPose;
                    pendingPose = null;
                    SetPose(queued);
                }
            };
            frontImage.BeginAnimation(UIElement.OpacityProperty, fadeIn);
        }

        private void HookRendering()
        {
            if (renderingHooked) return;
            CompositionTarget.Rendering += OnRendering;
            renderingHooked = true;
        }

        private void UnhookRendering()
        {
            if (!renderingHooked) return;
            CompositionTarget.Rendering -= OnRendering;
            renderingHooked = false;
        }

        private void OnRendering(object sender, EventArgs e)
        {
            Tick();
            if (DateTime.Now >= reactionUntil) Evaluate();
        }

        private void Evaluate()
        {
            if (disposed) return;
            if (hidden)
            {
                UnhookRendering();
                sustainedTimer.Stop();
                drowsyTimer.Stop();
                fpsMode = "0 隐藏";
                return;
            }
            if (DateTime.Now < reactionUntil || dragging)
            {
                sustainedTimer.Stop();
                drowsyTimer.Stop();
                HookRendering();
                fpsMode = "60 反应";
                return;
            }
            UnhookRendering();
            if (behavior == BehaviorState.Sleepy || behavior == BehaviorState.Away)
            {
                sustainedTimer.Stop();
                if (!drowsyTimer.IsEnabled) drowsyTimer.Start();
                fpsMode = "4 睡眠";
            }
            else
            {
                drowsyTimer.Stop();
                if (!sustainedTimer.IsEnabled) sustainedTimer.Start();
                fpsMode = "24 常态";
            }
        }

        private static double Approach(double current, double target, double amount)
        {
            if (current < target) return Math.Min(target, current + amount);
            return Math.Max(target, current - amount);
        }

        private static Geometry FocusArcGeometry(double centerX, double centerY, double radiusX, double radiusY,
            double fraction, out Point endPoint)
        {
            double clamped = Math.Max(0.002, Math.Min(0.998, fraction));
            double startAngle = -Math.PI / 2;
            double sweep = Math.PI * 2 * clamped;
            Point startPoint = new Point(centerX + Math.Cos(startAngle) * radiusX,
                centerY + Math.Sin(startAngle) * radiusY);
            double endAngle = startAngle + sweep;
            endPoint = new Point(centerX + Math.Cos(endAngle) * radiusX,
                centerY + Math.Sin(endAngle) * radiusY);
            StreamGeometry geometry = new StreamGeometry();
            using (StreamGeometryContext context = geometry.Open())
            {
                context.BeginFigure(startPoint, false, false);
                context.ArcTo(endPoint, new Size(radiusX, radiusY), 0, sweep > Math.PI,
                    SweepDirection.Clockwise, true, false);
            }
            geometry.Freeze();
            return geometry;
        }

        private void RenderContextScene(DateTime now, double dt, double width, double height, bool isSleepy)
        {
            bool suppress = docked || dragging || isSleepy || nodOffHold || patrolling || playfulShow != RareIdleShowType.None
                || ShyActive || BalloonActive || TreatActive || HighFiveActive || PatAffectionActive
                || CursorGreetingActive || WakeStretchActive || BubbleAttentionActive || dizzyActive;
            ContextSceneKind desired = suppress ? ContextSceneKind.None
                : ContextScenePolicy.Resolve(behavior, buildWaiting, flowActive);
            double step = Math.Max(0.08, dt * 5.5); // 约 180ms 淡入/淡出，不硬切道具
            double bookOpacity = Approach(bookLeft.Opacity, desired == ContextSceneKind.Book ? 0.94 : 0, step);
            double headsetOpacity = Approach(headsetBand.Opacity, desired == ContextSceneKind.Headset ? 0.90 : 0, step);
            double focusOpacity = Approach(focusHalo.Opacity, desired == ContextSceneKind.FocusHalo ? 0.76 : 0, step);
            double hourglassOpacity = Approach(hourglass.Opacity, desired == ContextSceneKind.Hourglass ? 0.94 : 0, step);

            double t = now.TimeOfDay.TotalSeconds;
            double motion = reducedMotion ? 0 : Math.Sin(t * 1.7);

            double bookWidth = width * 0.36;
            double bookHeight = Math.Max(14, height * 0.095);
            double bookLeftX = width * 0.32;
            double bookTop = height * 0.77 + motion * 0.7 * uiScale;
            double halfBook = bookWidth * 0.5;
            bookLeft.Width = halfBook;
            bookLeft.Height = bookHeight;
            bookRight.Width = halfBook * (1 - (reducedMotion ? 0 : 0.025 * Math.Max(0, motion)));
            bookRight.Height = bookHeight;
            Canvas.SetLeft(bookLeft, bookLeftX);
            Canvas.SetTop(bookLeft, bookTop);
            Canvas.SetLeft(bookRight, bookLeftX + halfBook - 1);
            Canvas.SetTop(bookRight, bookTop);
            bookSpine.X1 = bookSpine.X2 = bookLeftX + halfBook;
            bookSpine.Y1 = bookTop + 2;
            bookSpine.Y2 = bookTop + bookHeight - 2;
            bookLeft.Opacity = bookRight.Opacity = bookSpine.Opacity = bookOpacity;
            for (int i = 0; i < bookLines.Length; i++)
            {
                bool rightPage = i >= 2;
                double lineY = bookTop + bookHeight * (i % 2 == 0 ? 0.38 : 0.64);
                bookLines[i].X1 = rightPage ? bookLeftX + halfBook + 4 : bookLeftX + 4;
                bookLines[i].X2 = rightPage ? bookLeftX + bookWidth - 4 : bookLeftX + halfBook - 4;
                bookLines[i].Y1 = bookLines[i].Y2 = lineY;
                bookLines[i].Opacity = bookOpacity * 0.54;
            }

            double bandLeft = width * 0.28;
            double bandTop = height * 0.18;
            double bandWidth = width * 0.44;
            double bandHeight = height * 0.25;
            headsetBand.Width = bandWidth;
            headsetBand.Height = bandHeight;
            Canvas.SetLeft(headsetBand, bandLeft);
            Canvas.SetTop(headsetBand, bandTop + motion * 0.45 * uiScale);
            double cupWidth = Math.Max(7, width * 0.065);
            double cupHeight = Math.Max(12, height * 0.095);
            double cupTop = height * 0.355;
            headsetLeft.Width = headsetRight.Width = cupWidth;
            headsetLeft.Height = headsetRight.Height = cupHeight;
            Canvas.SetLeft(headsetLeft, width * 0.285);
            Canvas.SetLeft(headsetRight, width * 0.65);
            Canvas.SetTop(headsetLeft, cupTop);
            Canvas.SetTop(headsetRight, cupTop);
            headsetMic.X1 = width * 0.68;
            headsetMic.Y1 = cupTop + cupHeight * 0.72;
            headsetMic.X2 = width * 0.75;
            headsetMic.Y2 = cupTop + cupHeight * 0.92;
            headsetBand.Opacity = headsetLeft.Opacity = headsetRight.Opacity = headsetMic.Opacity = headsetOpacity;

            // 贴着脚下做成“专注光池”，不横穿身体，避免与呼啦圈动作混淆。
            double focusWidth = width * 0.70;
            double focusHeight = Math.Max(7.5, height * 0.075);
            double focusLeft = width * 0.15;
            double focusTop = height * 0.82 + motion * 0.35 * uiScale;
            focusHalo.Width = focusWidth;
            focusHalo.Height = focusHeight;
            Canvas.SetLeft(focusHalo, focusLeft);
            Canvas.SetTop(focusHalo, focusTop);
            focusHalo.Opacity = focusOpacity * (reducedMotion ? 1 : 0.86 + 0.14 * Math.Sin(t * 2.0));
            if (desired == ContextSceneKind.FocusHalo && focusRitualProgress >= 0)
            {
                double remaining = Math.Max(0.002, 1 - focusRitualProgress);
                Point focusEnd;
                focusProgressArc.Data = FocusArcGeometry(focusLeft + focusWidth / 2, focusTop + focusHeight / 2,
                    focusWidth / 2, focusHeight / 2, remaining, out focusEnd);
                focusProgressArc.Opacity = focusOpacity * 0.98;
                double dotSize = Math.Max(3.6, width * 0.034);
                focusProgressDot.Width = focusProgressDot.Height = dotSize;
                Canvas.SetLeft(focusProgressDot, focusEnd.X - dotSize / 2);
                Canvas.SetTop(focusProgressDot, focusEnd.Y - dotSize / 2);
                focusProgressDot.Opacity = focusOpacity;
            }
            else
            {
                focusProgressArc.Opacity = 0;
                focusProgressDot.Opacity = 0;
            }

            double glassWidth = Math.Max(13, width * 0.10);
            double glassHeight = Math.Max(18, height * 0.13);
            hourglass.Width = glassWidth;
            hourglass.Height = glassHeight;
            Canvas.SetLeft(hourglass, width * 0.77);
            Canvas.SetTop(hourglass, height * 0.17 + motion * 1.2 * uiScale);
            hourglass.Opacity = hourglassOpacity;

            double moonOpacity = Approach(quietMoon.Opacity, quietCompanion ? 0.92 : 0, step);
            double moonSize = Math.Max(15, width * 0.115);
            quietMoon.Width = moonSize;
            quietMoon.Height = moonSize * 1.08;
            Canvas.SetLeft(quietMoon, width * 0.72);
            Canvas.SetTop(quietMoon, height * 0.12 + (reducedMotion ? 0 : motion * 1.1 * uiScale));
            quietMoon.Opacity = moonOpacity;
        }

        private void Tick()
        {
            if (hidden || disposed) return;
            DateTime now = DateTime.Now;
            double dt = lastTick == DateTime.MinValue ? 0.042 : Math.Min(0.3, (now - lastTick).TotalSeconds);
            lastTick = now;
            double width = overlay.ActualWidth > 0 ? overlay.ActualWidth : 240;
            double height = overlay.ActualHeight > 0 ? overlay.ActualHeight : 254;
            if (playfulShow != RareIdleShowType.None && now >= playfulUntil) StopPlayfulShow();
            if (shyUntil != DateTime.MinValue && now >= shyUntil) StopShy();
            if (balloonUntil != DateTime.MinValue && now >= balloonUntil) StopBalloonGame();
            if (patAffectionUntil != DateTime.MinValue && now >= patAffectionUntil) StopPatAffection();
            if (cursorGreetingUntil != DateTime.MinValue && now >= cursorGreetingUntil) StopCursorGreeting();
            if (wakeStretchUntil != DateTime.MinValue && now >= wakeStretchUntil) StopWakeStretch();
            if (bubbleAttentionUntil != DateTime.MinValue && now >= bubbleAttentionUntil) StopBubbleAttention();
            double playfulT = playfulShow == RareIdleShowType.None ? 0 : (now - playfulStart).TotalSeconds;
            double shyT = !ShyActive ? 0 : (now - shyStart).TotalSeconds;
            double playfulX = 0;
            attentionX = CursorAttentionPolicy.Damp(attentionX, attentionTargetX, dt);
            attentionY = CursorAttentionPolicy.Damp(attentionY, attentionTargetY, dt);

            // ---- 身体：呼吸 + 侧倾 + 压扁拉伸 + Y 位移（事件叠加在参数之上，呼吸永不停）----
            if (!reducedMotion)
            {
                double breathHz = 0.18 + 0.6 * arousal;
                phase += dt * breathHz * 2 * Math.PI;
                double amp = 0.011 + 0.010 * arousal;
                double sy = 1 + amp * Math.Sin(phase);
                double sx = 1 - 0.55 * (sy - 1);

                double nodFactor = 0;
                if (behavior == BehaviorState.Meeting)
                {
                    if ((now - lastNod).TotalSeconds > 4) { lastNod = now; nodStart = now; }
                    double nodT = (now - nodStart).TotalSeconds;
                    if (nodT < 0.55) nodFactor = -0.035 * Math.Sin(Math.PI * nodT / 0.55);
                }

                // 深夜哈欠：大慢拉伸一次
                MaybeYawn(now);
                double yawnFactor = 0;
                double yt = (now - yawnStart).TotalSeconds;
                if (yt < 1.4) yawnFactor = 0.045 * Math.Sin(Math.PI * yt / 1.4);

                double squashFactor = 0;
                double squashT = (now - squashStart).TotalSeconds;
                if (squashT < 1.2 && squashPower > 0)
                {
                    squashFactor = -squashPower * Math.Exp(-3.5 * squashT) * Math.Sin(2 * Math.PI * 2.2 * squashT);
                }

                double deform = nodFactor + squashFactor + yawnFactor;
                scale.ScaleY = sy + deform;
                scale.ScaleX = sx - 0.6 * deform;

                double sway = behavior == BehaviorState.Watching
                    ? Math.Sin(phase * 0.5) * 1.1 // 看视频：轻微跟拍，不做钟摆
                    : Math.Sin(phase * 0.31) * (0.28 + 0.45 * valence);

                // Y 位移通道：拎起 / 落地 / 跳跃
                double liftY = 0;
                if (squishHold)
                {
                    // 捏脸保持：身体被捏扁、向按压侧倾（覆盖常规呼吸/摇摆）
                    scale.ScaleY = 0.90 + deform * 0.4;
                    scale.ScaleX = 1.05 - 0.4 * deform;
                    tilt.Angle = squishBias * 3.0;
                }
                else if (dragging)
                {
                    // 被拎起：真实速度驱动的小幅滞后。停手 100ms 后自然收稳，不做固定频率整身乱晃。
                    if (dragVelocityAt == DateTime.MinValue || (now - dragVelocityAt).TotalSeconds > 0.10)
                    {
                        dragTiltTarget = 0;
                        dragLagXTarget = 0;
                        dragLiftTarget = -4.0;
                    }
                    dragTilt = Approach(dragTilt, dragTiltTarget, Math.Max(0.10, dt * 28));
                    dragLagX = Approach(dragLagX, dragLagXTarget, Math.Max(0.08, dt * 20));
                    dragLift = Approach(dragLift, dragLiftTarget, Math.Max(0.10, dt * 24));
                    tilt.Angle = dragTilt;
                    liftY = dragLift;
                    playfulX = dragLagX * Math.Max(0.58, uiScale);
                }
                else
                {
                    tilt.Angle = sway + attentionX * 1.05;
                }
                if (!dragging && !dizzyActive && playfulShow == RareIdleShowType.Dance)
                {
                    double beat = Math.Sin(playfulT * Math.PI * 3.4);
                    double side = Math.Sin(playfulT * Math.PI * 1.7);
                    tilt.Angle = side * 3.2;
                    scale.ScaleX = sx + 0.012 * Math.Abs(beat);
                    scale.ScaleY = sy - 0.009 * Math.Abs(beat);
                    liftY += -Math.Max(0, beat) * 3.4;
                    playfulX = side * 1.8;
                }
                else if (!dragging && !dizzyActive && playfulShow == RareIdleShowType.HulaHoop)
                {
                    double hip = Math.Sin(playfulT * Math.PI * 3.2);
                    tilt.Angle = hip * 2.4;
                    scale.ScaleX = sx + 0.008 * Math.Abs(hip);
                    scale.ScaleY = sy - 0.004 * Math.Abs(hip);
                    playfulX = hip * 1.3;
                }
                else if (!dragging && !dizzyActive && playfulShow == RareIdleShowType.ButterflyChase)
                {
                    double curiosity = Math.Sin(playfulT * 2.4);
                    double flutter = Math.Sin(playfulT * 5.1);
                    tilt.Angle = curiosity * 2.2;
                    playfulX = curiosity * 2.1;
                    liftY += -Math.Max(0, flutter) * 2.3 * Math.Max(0.58, uiScale);
                }
                else if (!dragging && !dizzyActive && playfulShow == RareIdleShowType.BubbleBlow)
                {
                    double puff = Math.Max(0, Math.Sin(playfulT * Math.PI * 1.65));
                    tilt.Angle = -1.2 - puff * 0.8;
                    scale.ScaleX = sx + puff * 0.008;
                    scale.ScaleY = sy - puff * 0.005;
                    playfulX = -0.8 * puff;
                }
                else if (!dragging && !dizzyActive && playfulShow == RareIdleShowType.TailChase)
                {
                    // 追尾巴：先蹲一下 → 加速转两圈 → 原地晃稳收势
                    double spinT = playfulT;
                    if (spinT < 0.28)
                    {
                        // 起跳前小蹲
                        double crouch = Math.Sin(Math.PI * spinT / 0.28);
                        liftY += 3.5 * crouch * Math.Max(0.58, uiScale);
                        tilt.Angle = 0;
                    }
                    else if (spinT < 0.28 + AnimationPolicy.TailChaseSpinSec)
                    {
                        double k = (spinT - 0.28) / AnimationPolicy.TailChaseSpinSec;
                        double eased = k < 0.5 ? 4 * k * k * k : 1 - Math.Pow(-2 * k + 2, 3) / 2;
                        tilt.Angle = AnimationPolicy.TailChaseSpinDegrees * eased;
                    }
                    else
                    {
                        double wt = spinT - 0.28 - AnimationPolicy.TailChaseSpinSec;
                        double decay = Math.Max(0, 1 - wt / 0.8);
                        tilt.Angle = AnimationPolicy.TailChaseSpinDegrees + Math.Sin(wt * Math.PI * 4) * 5 * decay;
                        scale.ScaleY = sy - 0.012 * decay;
                    }
                }
                if (!dragging && !dizzyActive && ShyActive)
                {
                    double hide = Math.Min(1, shyT / 0.42);
                    double peek = shyT < 1.25 ? 0 : Math.Min(1, (shyT - 1.25) / 0.55);
                    double shyAmount = hide * (1 - 0.58 * peek);
                    tilt.Angle = 5.2 * shyAmount;
                    scale.ScaleX = sx * (1 - 0.018 * shyAmount);
                    scale.ScaleY = sy * (1 - 0.025 * shyAmount);
                    playfulX = 3.2 * Math.Max(0.55, uiScale) * shyAmount;
                }
                if (!dragging && !dizzyActive && patrolling)
                {
                    double walkT = (now - patrolStart).TotalSeconds;
                    double step = Math.Sin(walkT * Math.PI * 5.2);
                    tilt.Angle = patrolDirection * 1.35 + step * 1.15;
                    scale.ScaleX = sx + 0.007 * Math.Abs(step);
                    scale.ScaleY = sy - 0.006 * Math.Abs(step);
                    liftY += -Math.Abs(step) * 2.7 * Math.Max(0.58, uiScale);
                    playfulX = step * 0.65 * patrolDirection;
                }
                if (!dragging && !dizzyActive && TreatActive)
                {
                    double treatT = (now - treatStart).TotalSeconds;
                    TreatMotionFrame treatFrame = TreatMotionPolicy.Sample(treatT);
                    if (treatT < 0.92)
                    {
                        double lean = Math.Sin(Math.PI * treatFrame.Offer);
                        tilt.Angle = 1.8 * lean;
                        playfulX = 1.5 * lean * Math.Max(0.58, uiScale);
                    }
                    else
                    {
                        double chew = Math.Sin((treatT - 0.92) * Math.PI * 6.2) * treatFrame.Chew;
                        scale.ScaleY = sy - Math.Abs(chew) * 0.009;
                        scale.ScaleX = sx + Math.Abs(chew) * 0.006;
                        tilt.Angle = chew * 0.65;
                    }
                }
                if (!dragging && !dizzyActive && HighFiveActive)
                {
                    double raised = HighFivePolicy.InviteProgress((now - highFiveStart).TotalSeconds);
                    double hit = highFiveHitAt == DateTime.MinValue ? 0
                        : HighFivePolicy.CelebrateProgress((now - highFiveHitAt).TotalSeconds);
                    // 身体只做低幅迎手与回弹，动作主语由局部爪印承担，避免再次出现整身抖动。
                    double pulse = highFiveHitAt == DateTime.MinValue ? 0 : Math.Sin(Math.PI * Math.Min(1, hit * 1.8));
                    tilt.Angle = -0.85 * raised + 0.55 * pulse;
                    liftY -= (1.2 * raised + 1.5 * pulse) * Math.Max(0.58, uiScale);
                }
                if (!dragging && !dizzyActive && PatAffectionActive)
                {
                    PatAffectionFrame pat = PatAffectionPolicy.Sample((now - patAffectionStart).TotalSeconds, patAffectionBias);
                    tilt.Angle = pat.TiltDegrees;
                    playfulX = pat.LeanXDip * Math.Max(0.58, uiScale);
                    scale.ScaleX = sx + 0.008 * pat.Intensity;
                    scale.ScaleY = sy - 0.005 * pat.Intensity;
                }
                if (!dragging && !dizzyActive && CursorGreetingActive)
                {
                    CursorGreetingFrame greeting = CursorGreetingPolicy.Sample(
                        (now - cursorGreetingStart).TotalSeconds, cursorGreetingBias);
                    tilt.Angle = greeting.TiltDegrees;
                    liftY += greeting.LiftDip * Math.Max(0.58, uiScale);
                }
                if (!dragging && !dizzyActive && WakeStretchActive)
                {
                    WakeStretchFrame wake = WakeStretchPolicy.Sample((now - wakeStretchStart).TotalSeconds);
                    scale.ScaleX = sx + wake.ScaleXDelta;
                    scale.ScaleY = sy + wake.ScaleYDelta;
                    tilt.Angle = wake.TiltDegrees;
                    liftY += wake.LiftDip * Math.Max(0.58, uiScale);
                }
                if (!dragging && !dizzyActive && BubbleAttentionActive)
                {
                    CursorGreetingFrame bubbleLook = BubbleAttentionPolicy.Sample(
                        (now - bubbleAttentionStart).TotalSeconds, bubbleAttentionBias);
                    tilt.Angle = bubbleLook.TiltDegrees;
                    liftY += bubbleLook.LiftDip * Math.Max(0.58, uiScale);
                }
                // 拎起摇晃晕眩：快速摇摆 + 星星绕头（覆盖常规倾斜）
                if (dizzyActive)
                {
                    double dz = (now - dizzyStart).TotalSeconds;
                    if (dz > 1.6)
                    {
                        dizzyActive = false;
                        for (int i = 0; i < 3; i++) dizzyStars[i].Opacity = 0;
                    }
                    else
                    {
                        tilt.Angle = Math.Sin(now.TimeOfDay.TotalSeconds * 10) * 7;
                        double fade = Math.Min(1, (1.6 - dz) / 0.5);
                        for (int i = 0; i < 3; i++)
                        {
                            double ang = now.TimeOfDay.TotalSeconds * 4 + i * 2.094;
                            dizzyStars[i].Opacity = (0.5 + 0.5 * Math.Sin(now.TimeOfDay.TotalSeconds * 8 + i)) * fade;
                            // 弧线走头顶上方透明区，别压在橙猫脸上（低对比）
                            Canvas.SetLeft(dizzyStars[i], width * 0.5 + Math.Cos(ang) * width * 0.34 - 6 * uiScale);
                            Canvas.SetTop(dizzyStars[i], height * 0.12 + Math.Sin(ang) * width * 0.06 - 6 * uiScale);
                        }
                    }
                }
                else
                {
                    for (int i = 0; i < 3; i++) dizzyStars[i].Opacity = 0;
                }

                // 困倦点头分镜：由 NodOffDirector 驱动的姿态通道（晕眩优先）
                if (nodOffHold && !dizzyActive)
                {
                    scale.ScaleY *= nodOffScaleY;
                    tilt.Angle = nodOffTilt;
                    if (nodOffStartle)
                    {
                        thoughtText.Text = "!";
                        thought.Opacity = 0.95;
                        Canvas.SetLeft(thought, width * 0.62);
                        Canvas.SetTop(thought, height * 0.06);
                    }
                }

                // 踩奶：摸完后的满足揉按（双爪交替 ±1.5° + 轻重呼吸按压，渐入渐出）
                if (KneadActive && behavior == BehaviorState.Idle && !docked && !dragging && !dizzyActive && !nodOffHold)
                {
                    double kneadT = (now - kneadStart).TotalSeconds;
                    double total = (kneadUntil - kneadStart).TotalSeconds;
                    double fade = Math.Min(1, Math.Min(kneadT / 0.4, (total - kneadT) / 0.4));
                    double paw = Math.Sin(kneadT * Math.PI * 2.4);
                    double press = 0.5 + 0.5 * Math.Sin(kneadT * Math.PI * 2.4 + Math.PI / 2);
                    tilt.Angle = paw * 3.0 * fade;
                    scale.ScaleY = (sy - 0.011 * press * fade);
                    scale.ScaleX = (sx + 0.007 * press * fade);
                }
                double hopT = (now - hopStart).TotalSeconds;
                if (hopT < 0.55) liftY += -34 * Math.Sin(Math.PI * hopT / 0.55);
                double landT = (now - landStart).TotalSeconds;
                if (landT < 0.3) liftY += 3 * Math.Sin(Math.PI * landT / 0.3);
                double attentionScale = Math.Max(0.55, uiScale);
                lift.X = attentionX * 1.7 * attentionScale + playfulX;
                lift.Y = liftY + attentionY * 0.8 * attentionScale;

                bool airborne = hopT >= 0 && hopT < 0.55;
                GroundingShadowStyle shadowStyle = GroundingShadowPolicy.Resolve(behavior, airborne, dragging, docked);
                contactShadow.Width = width * shadowStyle.WidthRatio;
                contactShadow.Height = Math.Max(3.5, height * shadowStyle.HeightRatio);
                contactShadow.Margin = new Thickness(0, 0, 0, height * 0.065);
                contactShadow.Opacity = reducedMotion ? shadowStyle.Opacity
                    : Approach(contactShadow.Opacity, shadowStyle.Opacity, Math.Max(0.02, dt * 0.45));
            }
            else
            {
                lift.X = 0;
                lift.Y = 0;
                GroundingShadowStyle shadowStyle = GroundingShadowPolicy.Resolve(behavior, false, false, docked);
                contactShadow.Width = width * shadowStyle.WidthRatio;
                contactShadow.Height = Math.Max(3.5, height * shadowStyle.HeightRatio);
                contactShadow.Margin = new Thickness(0, 0, 0, height * 0.065);
                contactShadow.Opacity = shadowStyle.Opacity;
            }

            // ---- 氛围层：按行为开关，全部漂浮元素，不锚定脸部 ----
            bool isSleepy = behavior == BehaviorState.Sleepy || behavior == BehaviorState.Away;
            RenderContextScene(now, dt, width, height, isSleepy);
            double t = now.TimeOfDay.TotalSeconds;
            for (int i = 0; i < 3; i++)
            {
                if (isSleepy && !reducedMotion)
                {
                    double p = (t * 0.4 + i * 0.33) % 1.0;
                    zzz[i].Opacity = Math.Sin(Math.PI * p) * 0.85;
                    Canvas.SetLeft(zzz[i], width * 0.68 + i * 6 * uiScale);
                    Canvas.SetTop(zzz[i], height * 0.16 - p * 18 * uiScale - i * 10 * uiScale);
                }
                else zzz[i].Opacity = 0;
            }
            bool isWatching = behavior == BehaviorState.Watching || playfulShow == RareIdleShowType.Dance;
            for (int i = 0; i < 2; i++)
            {
                if (isWatching && !reducedMotion)
                {
                    double p = (t * 0.3 + i * 0.5) % 1.0;
                    notes[i].Opacity = Math.Sin(Math.PI * p) * 0.8;
                    Canvas.SetLeft(notes[i], width * (0.72 + 0.08 * i));
                    Canvas.SetTop(notes[i], height * 0.30 - p * 22 * uiScale);
                }
                else notes[i].Opacity = 0;
            }
            bool isThinking = behavior == BehaviorState.Thinking;
            bool isStartled = nodOffHold && nodOffStartle;
            if (isThinking || isStartled)
            {
                thoughtText.Text = isStartled ? "!" : "?";
                thought.Opacity = isStartled ? 0.95
                    : reducedMotion ? 0.95 : 0.55 + 0.4 * Math.Sin(t * 1.4);
                Canvas.SetLeft(thought, width * 0.62);
                Canvas.SetTop(thought, height * 0.06);
            }
            else thought.Opacity = 0;

            for (int i = 0; i < shyMarks.Length; i++)
            {
                if (ShyActive && !reducedMotion)
                {
                    double fadeIn = Math.Min(1, shyT / 0.35);
                    double fadeOut = Math.Min(1, Math.Max(0, 2.3 - shyT) / 0.4);
                    shyMarks[i].Opacity = Math.Min(fadeIn, fadeOut) * 0.86;
                    Canvas.SetLeft(shyMarks[i], width * (i == 0 ? 0.28 : 0.64));
                    Canvas.SetTop(shyMarks[i], height * 0.38 + Math.Sin(shyT * 4 + i) * uiScale);
                }
                else shyMarks[i].Opacity = 0;
            }

            if (playfulShow == RareIdleShowType.HulaHoop && !reducedMotion)
            {
                double duration = AnimationPolicy.HulaShowDurationMs / 1000.0;
                double fadeIn = Math.Min(1, playfulT / 0.22);
                double fadeOut = Math.Min(1, Math.Max(0, duration - playfulT) / 0.35);
                hulaHoop.Opacity = Math.Min(fadeIn, fadeOut) * 0.94;
                hulaHoop.Width = width * 0.72;
                hulaHoop.Height = Math.Max(12, height * 0.14);
                double hoopLeft = width * 0.14 + Math.Sin(playfulT * Math.PI * 3.2) * width * 0.025;
                double hoopTop = height * 0.60 + Math.Cos(playfulT * Math.PI * 3.2) * height * 0.012;
                Canvas.SetLeft(hulaHoop, hoopLeft);
                Canvas.SetTop(hulaHoop, hoopTop);
                // 绝不转到竖直：保持“绕腰”语义，只用小角度摆动和沿轨高光表达速度。
                double orbit = playfulT * Math.PI * 3.2;
                hulaRotate.Angle = Math.Sin(orbit) * 5.5;
                double beadSize = Math.Max(5, 7 * uiScale);
                hulaBead.Width = hulaBead.Height = beadSize;
                hulaBead.Opacity = Math.Min(fadeIn, fadeOut);
                Canvas.SetLeft(hulaBead, hoopLeft + hulaHoop.Width / 2 + Math.Cos(orbit) * hulaHoop.Width * 0.45 - beadSize / 2);
                Canvas.SetTop(hulaBead, hoopTop + hulaHoop.Height / 2 + Math.Sin(orbit) * hulaHoop.Height * 0.38 - beadSize / 2);
            }
            else { hulaHoop.Opacity = 0; hulaBead.Opacity = 0; }

            if (playfulShow == RareIdleShowType.ButterflyChase && !reducedMotion)
            {
                double duration = AnimationPolicy.ButterflyShowDurationMs / 1000.0;
                double fade = Math.Min(Math.Min(1, playfulT / 0.24), Math.Min(1, Math.Max(0, duration - playfulT) / 0.36));
                double size = Math.Max(13, width * 0.105);
                butterfly.Width = size;
                butterfly.Height = size * 0.78;
                double wingWidth = size * 0.48;
                double wingHeight = size * 0.62;
                butterflyLeftWing.Width = butterflyRightWing.Width = wingWidth;
                butterflyLeftWing.Height = butterflyRightWing.Height = wingHeight;
                Canvas.SetLeft(butterflyLeftWing, 0);
                Canvas.SetLeft(butterflyRightWing, size * 0.45);
                Canvas.SetTop(butterflyLeftWing, 0);
                Canvas.SetTop(butterflyRightWing, size * 0.08);
                butterflyBody.Width = size * 0.13;
                butterflyBody.Height = size * 0.55;
                Canvas.SetLeft(butterflyBody, size * 0.435);
                Canvas.SetTop(butterflyBody, size * 0.18);
                double wingBeat = Math.Abs(Math.Sin(playfulT * Math.PI * 6.2));
                butterflyScale.ScaleX = 0.62 + wingBeat * 0.38;
                butterflyScale.ScaleY = 0.94 + wingBeat * 0.06;
                double pathX = width * (0.52 + 0.29 * Math.Sin(playfulT * 1.85));
                double pathY = height * (0.18 + 0.105 * Math.Sin(playfulT * 2.45 + 0.8));
                Canvas.SetLeft(butterfly, pathX - size / 2);
                Canvas.SetTop(butterfly, pathY - size / 2);
                butterfly.Opacity = fade * 0.96;
            }
            else butterfly.Opacity = 0;

            if (playfulShow == RareIdleShowType.BubbleBlow && !reducedMotion)
            {
                double duration = AnimationPolicy.BubbleShowDurationMs / 1000.0;
                double fadeOut = Math.Min(1, Math.Max(0, duration - playfulT) / 0.38);
                for (int i = 0; i < soapBubbles.Length; i++)
                {
                    double p = (playfulT - i * 0.62) / 2.35;
                    if (p > 0 && p < 1)
                    {
                        double bubbleSize = Math.Max(7, width * (0.05 + 0.035 * p));
                        soapBubbles[i].Width = soapBubbles[i].Height = bubbleSize;
                        soapBubbles[i].Opacity = Math.Sin(Math.PI * p) * 0.88 * fadeOut;
                        Canvas.SetLeft(soapBubbles[i], width * 0.66 + Math.Sin(p * Math.PI * 2 + i) * width * 0.045 - bubbleSize / 2);
                        Canvas.SetTop(soapBubbles[i], height * (0.48 - p * 0.38) - bubbleSize / 2);
                    }
                    else soapBubbles[i].Opacity = 0;
                }
            }
            else for (int i = 0; i < soapBubbles.Length; i++) soapBubbles[i].Opacity = 0;

            if (TreatActive)
            {
                double treatT = (now - treatStart).TotalSeconds;
                TreatMotionFrame frame = TreatMotionPolicy.Sample(treatT);
                double size = Math.Max(14, width * 0.115);
                treatSnack.Width = treatSnack.Height = size;
                treatSnackShape.Width = treatSnackShape.Height = size;
                double detailSize = Math.Max(1.8, size * 0.12);
                treatSnackDetail.Width = treatSnackKind == TreatSnackKind.Carrot ? Math.Max(4.2, size * 0.34) : detailSize;
                treatSnackDetail.Height = treatSnackKind == TreatSnackKind.Carrot ? Math.Max(2.4, size * 0.16) : detailSize;
                Canvas.SetLeft(treatSnackDetail, size * (treatSnackKind == TreatSnackKind.Fish ? 0.67
                    : treatSnackKind == TreatSnackKind.Carrot ? 0.18 : 0.48));
                Canvas.SetTop(treatSnackDetail, size * (treatSnackKind == TreatSnackKind.Fish ? 0.36
                    : treatSnackKind == TreatSnackKind.Carrot ? 0.06 : 0.43));
                treatSnackDetail.Opacity = treatSnackKind == TreatSnackKind.Carrot ? 0.94 : 0.88;
                double offer = reducedMotion ? 1 : frame.Offer;
                double fromX = width * 0.86;
                double fromY = height * 0.26;
                double toX = width * 0.60;
                double toY = height * 0.45;
                double left = fromX + (toX - fromX) * offer - size / 2;
                double top = fromY + (toY - fromY) * offer - (reducedMotion ? 0 : Math.Sin(Math.PI * offer) * height * 0.075) - size / 2;
                Canvas.SetLeft(treatSnack, left);
                Canvas.SetTop(treatSnack, top);
                double biteScale = 1 - frame.Bite * 0.72;
                treatScale.ScaleX = treatScale.ScaleY = biteScale;
                treatRotate.Angle = reducedMotion ? 0 : -16 + offer * 27;
                treatSnack.Opacity = frame.CookieOpacity;

                for (int i = 0; i < treatCrumbs.Length; i++)
                {
                    double p = (treatT - 1.04 - i * 0.07) / 0.68;
                    if (p > 0 && p < 1 && !reducedMotion)
                    {
                        double crumbSize = Math.Max(2.4, width * (0.018 + i * 0.003));
                        treatCrumbs[i].Width = treatCrumbs[i].Height = crumbSize;
                        treatCrumbs[i].Opacity = Math.Sin(Math.PI * p) * 0.84;
                        Canvas.SetLeft(treatCrumbs[i], width * 0.61 + (i - 1) * width * 0.035 + Math.Sin(p * Math.PI) * width * 0.025);
                        Canvas.SetTop(treatCrumbs[i], height * 0.45 - p * height * (0.08 + i * 0.015));
                    }
                    else treatCrumbs[i].Opacity = 0;
                }
                treatJoy.Opacity = frame.Joy * 0.94;
                Canvas.SetLeft(treatJoy, width * 0.70);
                Canvas.SetTop(treatJoy, height * (0.26 - (reducedMotion ? 0 : frame.Joy * 0.08)));
            }
            else
            {
                treatSnack.Opacity = 0;
                treatJoy.Opacity = 0;
                for (int i = 0; i < treatCrumbs.Length; i++) treatCrumbs[i].Opacity = 0;
            }

            if (HighFiveActive)
            {
                double inviteT = (now - highFiveStart).TotalSeconds;
                double raised = reducedMotion ? 1 : HighFivePolicy.InviteProgress(inviteT);
                double hitT = highFiveHitAt == DateTime.MinValue ? -1 : (now - highFiveHitAt).TotalSeconds;
                double hitProgress = hitT < 0 ? 0 : HighFivePolicy.CelebrateProgress(hitT);
                double pulse = hitT < 0 || reducedMotion ? 0 : Math.Sin(Math.PI * Math.Min(1, hitProgress * 1.75));
                double pawScale = Math.Max(0.56, Math.Min(1.05, width / 190.0)) * (0.78 + raised * 0.22 + pulse * 0.13);
                highFiveScale.ScaleX = highFiveScale.ScaleY = pawScale;
                highFiveWingScale.ScaleX = highFiveWingScale.ScaleY = pawScale;
                highFiveRotate.Angle = reducedMotion ? -4 : -13 + raised * 9 + Math.Sin(inviteT * 2.8) * 1.2;
                highFiveWingRotate.Angle = reducedMotion ? -7 : -18 + raised * 12 + Math.Sin(inviteT * 2.8) * 1.1;
                highFivePaw.Opacity = highFiveUsesWing ? 0 : Math.Min(0.97, raised * 1.25);
                highFiveWing.Opacity = highFiveUsesWing ? Math.Min(0.97, raised * 1.25) : 0;
                Canvas.SetLeft(highFivePaw, width * 0.66);
                Canvas.SetTop(highFivePaw, height * (0.21 - raised * 0.045 - pulse * 0.03));
                Canvas.SetLeft(highFiveWing, width * 0.66);
                Canvas.SetTop(highFiveWing, height * (0.21 - raised * 0.045 - pulse * 0.03));

                double centerX = width * 0.66 + 21 * pawScale;
                double centerY = height * 0.25;
                for (int i = 0; i < highFiveSparks.Length; i++)
                {
                    double angle = -Math.PI * 0.82 + i * Math.PI * 0.55;
                    double inner = width * (0.10 + pulse * 0.015);
                    double outer = inner + width * 0.055 * pulse;
                    highFiveSparks[i].X1 = centerX + Math.Cos(angle) * inner;
                    highFiveSparks[i].Y1 = centerY + Math.Sin(angle) * inner;
                    highFiveSparks[i].X2 = centerX + Math.Cos(angle) * outer;
                    highFiveSparks[i].Y2 = centerY + Math.Sin(angle) * outer;
                    highFiveSparks[i].Opacity = hitT < 0 || reducedMotion ? 0 : Math.Sin(Math.PI * Math.Min(1, hitProgress * 1.6)) * 0.92;
                }
            }
            else
            {
                highFivePaw.Opacity = 0;
                highFiveWing.Opacity = 0;
                for (int i = 0; i < highFiveSparks.Length; i++) highFiveSparks[i].Opacity = 0;
            }

            if (BalloonActive && !reducedMotion)
            {
                double balloonDt = balloonLastTick == DateTime.MinValue ? 0.042 : (now - balloonLastTick).TotalSeconds;
                balloonLastTick = now;
                balloonState = BalloonToyPhysics.Step(balloonState, balloonDt);
                double balloonWidth = Math.Max(15, width * 0.22);
                double balloonHeight = balloonWidth * 1.18;
                double left = balloonState.X * width - balloonWidth / 2;
                double top = balloonState.Y * height - balloonHeight / 2;
                balloonBounds = new Rect(left, top, balloonWidth, balloonHeight);
                balloon.Width = balloonWidth;
                balloon.Height = balloonHeight;
                balloon.Opacity = 0.96;
                Canvas.SetLeft(balloon, left);
                Canvas.SetTop(balloon, top);
                balloonString.X1 = left + balloonWidth / 2;
                balloonString.Y1 = top + balloonHeight * 0.88;
                balloonString.X2 = left + balloonWidth / 2 + Math.Sin(now.TimeOfDay.TotalSeconds * 3) * 3 * uiScale;
                balloonString.Y2 = top + balloonHeight * 1.55;
                balloonString.Opacity = 0.72;
                double sparkT = (now - balloonSparkStart).TotalSeconds;
                if (sparkT >= 0 && sparkT < 0.55)
                {
                    balloonSpark.Opacity = Math.Sin(Math.PI * sparkT / 0.55);
                    Canvas.SetLeft(balloonSpark, left + balloonWidth * 0.72);
                    Canvas.SetTop(balloonSpark, top - 7 * uiScale);
                }
                else balloonSpark.Opacity = 0;
            }
            else
            {
                balloon.Opacity = 0;
                balloonString.Opacity = 0;
                balloonSpark.Opacity = 0;
            }

            // 摸头爱心
            double ht = (now - heartStart).TotalSeconds;
            for (int i = 0; i < 2; i++)
            {
                double p = (ht - i * 0.18) / 1.0;
                if (p > 0 && p < 1 && !reducedMotion)
                {
                    hearts[i].Opacity = Math.Sin(Math.PI * p) * 0.95;
                    Canvas.SetLeft(hearts[i], width * (0.52 + 0.14 * i));
                    Canvas.SetTop(hearts[i], height * 0.30 - p * 42 * uiScale);
                }
                else hearts[i].Opacity = 0;
            }

            // 升级彩带：重力抛物线
            double ct = (now - confettiStart).TotalSeconds;
            for (int i = 0; i < confetti.Length; i++)
            {
                if (ct < 1.5 && ct > 0 && !reducedMotion)
                {
                    double p = ct / 1.5;
                    confetti[i].Opacity = 1 - p;
                    Canvas.SetLeft(confetti[i], width / 2 + confVX[i] * ct);
                    Canvas.SetTop(confetti[i], height * 0.30 + confVY[i] * ct + 130 * ct * ct);
                }
                else confetti[i].Opacity = 0;
            }
        }
    }
}
