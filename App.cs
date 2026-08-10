using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace WorkMatePro
{
    public static class NativeMethods
    {
        public const int WM_HOTKEY = 0x0312;
        public const uint MOD_ALT = 0x0001;
        public const uint MOD_CONTROL = 0x0002;

        [StructLayout(LayoutKind.Sequential)]
        public struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        [DllImport("user32.dll")]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint virtualKey);

        [DllImport("user32.dll")]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        public static extern bool GetLastInputInfo(ref LASTINPUTINFO info);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

        [DllImport("user32.dll")]
        public static extern bool IsZoomed(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("kernel32.dll")]
        public static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        public static extern bool AttachThreadInput(uint attach, uint attachTo, bool attachState);

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int command);

        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromWindow(IntPtr hWnd, uint flags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);

        public static void ForceForeground(IntPtr handle)
        {
            IntPtr foreground = GetForegroundWindow();
            uint ignored;
            uint foregroundThread = foreground == IntPtr.Zero ? 0 : GetWindowThreadProcessId(foreground, out ignored);
            uint currentThread = GetCurrentThreadId();
            bool attached = foregroundThread != 0 && foregroundThread != currentThread && AttachThreadInput(currentThread, foregroundThread, true);
            try
            {
                ShowWindow(handle, 5);
                BringWindowToTop(handle);
                SetForegroundWindow(handle);
            }
            finally
            {
                if (attached) AttachThreadInput(currentThread, foregroundThread, false);
            }
        }
    }

    public static class WindowPlacement
    {
        public static Rect WorkAreaFor(Window window)
        {
            try
            {
                IntPtr handle = new WindowInteropHelper(window).Handle;
                IntPtr monitor = NativeMethods.MonitorFromWindow(handle, 2);
                NativeMethods.MONITORINFO info = new NativeMethods.MONITORINFO();
                info.cbSize = Marshal.SizeOf(info);
                if (monitor != IntPtr.Zero && NativeMethods.GetMonitorInfo(monitor, ref info))
                {
                    PresentationSource source = PresentationSource.FromVisual(window);
                    if (source != null && source.CompositionTarget != null)
                    {
                        Matrix fromDevice = source.CompositionTarget.TransformFromDevice;
                        Point topLeft = fromDevice.Transform(new Point(info.rcWork.Left, info.rcWork.Top));
                        Point bottomRight = fromDevice.Transform(new Point(info.rcWork.Right, info.rcWork.Bottom));
                        return new Rect(topLeft, bottomRight);
                    }
                }
            }
            catch { }
            return SystemParameters.WorkArea;
        }
    }

    public static class Theme
    {
        public static readonly FontFamily Font = new FontFamily("Microsoft YaHei UI");
        public static readonly SolidColorBrush Ink = Brush("#2F2926");
        public static readonly SolidColorBrush Muted = Brush("#716965");
        public static readonly SolidColorBrush Faint = Brush("#9B928D");
        public static readonly SolidColorBrush Canvas = Brush("#FBF8F4");
        public static readonly SolidColorBrush Surface = Brush("#FFFEFC");
        public static readonly SolidColorBrush SoftSurface = Brush("#F5EFEA");
        public static readonly SolidColorBrush Line = Brush("#E8E0DA");
        public static readonly SolidColorBrush Accent = Brush("#E95D4F");
        public static readonly SolidColorBrush AccentHover = Brush("#D94F43");
        public static readonly SolidColorBrush AccentSoft = Brush("#FDEAE5");
        public static readonly SolidColorBrush Success = Brush("#3D8C6A");
        public static readonly SolidColorBrush SuccessSoft = Brush("#E8F4EE");
        public static readonly SolidColorBrush Warning = Brush("#B8792D");
        public static readonly SolidColorBrush WarningSoft = Brush("#FFF3DF");

        public static SolidColorBrush Brush(string hex)
        {
            SolidColorBrush brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            return brush;
        }

        public static DropShadowEffect Shadow(double blur, double opacity, double depth)
        {
            return new DropShadowEffect
            {
                BlurRadius = blur,
                ShadowDepth = depth,
                Opacity = opacity,
                Color = Color.FromRgb(70, 52, 42),
                Direction = 270
            };
        }

        public static TextBlock Text(string value, double size, Brush color, FontWeight weight)
        {
            return new TextBlock
            {
                Text = value,
                FontFamily = Font,
                FontSize = size,
                Foreground = color,
                FontWeight = weight,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        public static Border Card(UIElement child, double radius, Thickness padding)
        {
            return new Border
            {
                Background = Surface,
                BorderBrush = Line,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(radius),
                Padding = padding,
                Child = child
            };
        }

        public static Style RoundedButtonStyle(Brush normal, Brush hover, Brush foreground, double radius)
        {
            Style style = new Style(typeof(Button));
            style.Setters.Add(new Setter(Control.BackgroundProperty, normal));
            style.Setters.Add(new Setter(Control.ForegroundProperty, foreground));
            style.Setters.Add(new Setter(Control.BorderBrushProperty, Brushes.Transparent));
            style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
            style.Setters.Add(new Setter(Control.FontFamilyProperty, Font));
            style.Setters.Add(new Setter(Control.FontSizeProperty, 13.0));
            style.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));
            style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(14, 9, 14, 9)));
            style.Setters.Add(new Setter(Control.CursorProperty, Cursors.Hand));
            style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Center));
            style.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
            style.Setters.Add(new Setter(UIElement.FocusableProperty, true));
            style.Setters.Add(new Setter(FrameworkElement.FocusVisualStyleProperty, null));

            ControlTemplate template = new ControlTemplate(typeof(Button));
            FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
            border.Name = "border";
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
            border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(radius));
            border.SetValue(Border.SnapsToDevicePixelsProperty, true);

            FrameworkElementFactory presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, new TemplateBindingExtension(Control.HorizontalContentAlignmentProperty));
            presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, new TemplateBindingExtension(Control.VerticalContentAlignmentProperty));
            presenter.SetValue(ContentPresenter.MarginProperty, new TemplateBindingExtension(Control.PaddingProperty));
            presenter.SetValue(ContentPresenter.RecognizesAccessKeyProperty, true);
            border.AppendChild(presenter);
            template.VisualTree = border;

            Trigger hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, hover, "border"));
            template.Triggers.Add(hoverTrigger);
            Trigger pressed = new Trigger { Property = ButtonBase.IsPressedProperty, Value = true };
            pressed.Setters.Add(new Setter(UIElement.OpacityProperty, 0.82, "border"));
            template.Triggers.Add(pressed);
            Trigger disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
            disabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.42, "border"));
            template.Triggers.Add(disabled);
            Trigger focused = new Trigger { Property = UIElement.IsKeyboardFocusedProperty, Value = true };
            focused.Setters.Add(new Setter(Border.BorderBrushProperty, Brush("#7A3029"), "border"));
            focused.Setters.Add(new Setter(Border.BorderThicknessProperty, new Thickness(2), "border"));
            template.Triggers.Add(focused);
            style.Setters.Add(new Setter(Control.TemplateProperty, template));
            return style;
        }

        public static Button Button(string text, Brush normal, Brush hover, Brush foreground, double radius)
        {
            Button button = new Button { Content = text, Style = RoundedButtonStyle(normal, hover, foreground, radius) };
            RenderOptions.SetBitmapScalingMode(button, BitmapScalingMode.HighQuality);
            return button;
        }

        public static Button PrimaryButton(string text)
        {
            return Button(text, Accent, AccentHover, Brushes.White, 12);
        }

        public static Button SecondaryButton(string text)
        {
            return Button(text, SoftSurface, Brush("#EEE5DE"), Ink, 12);
        }

        public static Button GhostButton(string text)
        {
            return Button(text, Brushes.Transparent, SoftSurface, Muted, 10);
        }

        public static Border Pill(string text, Brush background, Brush foreground)
        {
            TextBlock label = Text(text, 11, foreground, FontWeights.SemiBold);
            label.TextWrapping = TextWrapping.NoWrap;
            return new Border
            {
                Background = background,
                CornerRadius = new CornerRadius(999),
                Padding = new Thickness(9, 4, 9, 4),
                Child = label,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        public static Border InputShell(TextBox box, double height)
        {
            box.BorderThickness = new Thickness(0);
            box.Background = Brushes.Transparent;
            box.FontFamily = Font;
            box.FontSize = 15;
            box.Foreground = Ink;
            box.CaretBrush = Accent;
            box.VerticalContentAlignment = VerticalAlignment.Center;
            box.Padding = new Thickness(2, 0, 2, 0);
            Border shell = new Border
            {
                Height = height,
                CornerRadius = new CornerRadius(14),
                Background = Brush("#F7F2EE"),
                BorderBrush = Line,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(15, 0, 15, 0),
                Child = box
            };
            box.GotKeyboardFocus += delegate { shell.BorderBrush = Accent; shell.BorderThickness = new Thickness(2); };
            box.LostKeyboardFocus += delegate { shell.BorderBrush = Line; shell.BorderThickness = new Thickness(1); };
            return shell;
        }

        public static Grid Progress(double value, Brush accent, double height)
        {
            value = Math.Max(0, Math.Min(1, value));
            Grid root = new Grid { Height = height, ClipToBounds = true };
            Border track = new Border { Background = Brush("#EAE2DC"), CornerRadius = new CornerRadius(height / 2) };
            Border fill = new Border
            {
                Background = accent,
                CornerRadius = new CornerRadius(height / 2),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            root.Children.Add(track);
            root.Children.Add(fill);
            root.SizeChanged += delegate { fill.Width = root.ActualWidth * value; };
            return root;
        }
    }

    public static class PetAssets
    {
        private static readonly Dictionary<string, BitmapImage> Cache = new Dictionary<string, BitmapImage>();
        private static readonly Dictionary<string, BitmapSource> AnimationFrames = new Dictionary<string, BitmapSource>();
        private static string activeAnimationSequence = "";
        private static readonly Dictionary<BitmapSource, AlphaMask> AlphaMasks = new Dictionary<BitmapSource, AlphaMask>();

        private sealed class AlphaMask
        {
            public int Width;
            public int Height;
            public byte[] Alpha;
        }

        public static BitmapImage Get(string petId, string action)
        {
            string key = petId + ":" + action;
            BitmapImage cached;
            if (Cache.TryGetValue(key, out cached)) return cached;
            PetDefinition definition = PetCatalog.Find(petId);
            Stream stream;
            string sourceLabel;
            if (definition != null && definition.IsCustom)
            {
                string path = Path.Combine(definition.AssetRoot, action + ".png");
                if (!File.Exists(path)) path = Path.Combine(definition.AssetRoot, "idle.png");
                if (!File.Exists(path)) throw new InvalidOperationException("Missing custom pet asset: " + path);
                stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                sourceLabel = path;
            }
            else
            {
                string resourceName = "WorkMate.Assets." + petId + "." + action + ".png";
                stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
                if (stream == null) throw new InvalidOperationException("Missing embedded pet resource: " + resourceName);
                sourceLabel = resourceName;
            }
            BitmapImage image = new BitmapImage();
            try
            {
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.DecodePixelWidth = 512; // 最大桌宠约 230 DIP；4K@225% 下 512px 足够，同时避免按原图解码
                image.StreamSource = stream;
                image.EndInit();
                image.Freeze();
            }
            catch (Exception ex) { throw new InvalidDataException("Invalid pet asset " + sourceLabel + ": " + ex.Message, ex); }
            finally { stream.Dispose(); }
            Cache[key] = image;
            return image;
        }

        public static void InvalidatePet(string petId)
        {
            string prefix = (petId ?? "") + ":";
            foreach (string key in Cache.Keys.Where(delegate(string value) { return value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase); }).ToList())
                Cache.Remove(key);
            AnimationFrames.Clear();
            AlphaMasks.Clear();
            activeAnimationSequence = "";
        }

        public static Image View(string petId, string action, double size)
        {
            Image view = new Image
            {
                Source = Get(petId, action),
                Width = size,
                Height = size,
                Stretch = Stretch.Uniform
            };
            RenderOptions.SetBitmapScalingMode(view, BitmapScalingMode.HighQuality);
            return view;
        }

        public static bool SupportsAnimation(string petId)
        {
            return string.Equals(petId, "01-cat", StringComparison.OrdinalIgnoreCase)
                || string.Equals(petId, "03-penguin", StringComparison.OrdinalIgnoreCase);
        }

        public static BitmapSource GetAnimationFrame(string petId, string sequence, int frame)
        {
            if (!SupportsAnimation(petId)) return null;
            frame = Math.Max(0, Math.Min(24, frame));
            string sequenceKey = petId + ":" + sequence;
            if (!string.Equals(activeAnimationSequence, sequenceKey, StringComparison.Ordinal))
            {
                AnimationFrames.Clear();
                AlphaMasks.Clear();
                activeAnimationSequence = sequenceKey;
            }
            string frameKey = petId + ":" + sequence + ":" + frame;
            BitmapSource cached;
            if (AnimationFrames.TryGetValue(frameKey, out cached)) return cached;
            string resourceName = "WorkMate.Frames." + petId + "." + sequence + "." + frame.ToString("00") + ".png";
            Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
            if (stream == null) return null;
            BitmapImage image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 251;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            stream.Dispose();
            BitmapSource decoded = image;
            if (ShouldGroundAlign(sequence)) decoded = AlignToGroundAnchor(image);
            AnimationFrames[frameKey] = decoded;
            return decoded;
        }

        private static bool ShouldGroundAlign(string sequence)
        {
            return string.Equals(sequence, "idle-life", StringComparison.OrdinalIgnoreCase)
                || string.Equals(sequence, "stretch", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// AI 分镜在固定画布中的落点会漂移。以 alpha 主体最底部 8% 的加权中心为“脚底”，
        /// 统一到画布底部中央；位移严格限制在不裁掉任何有效像素的范围内。
        /// </summary>
        private static BitmapSource AlignToGroundAnchor(BitmapSource source)
        {
            Point anchor = FindGroundAnchor(source);
            if (double.IsNaN(anchor.X) || double.IsNaN(anchor.Y)) return source;
            BitmapSource bgra = source;
            if (source.Format != PixelFormats.Bgra32)
            {
                FormatConvertedBitmap converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
                converted.Freeze();
                bgra = converted;
            }
            int width = bgra.PixelWidth;
            int height = bgra.PixelHeight;
            int stride = width * 4;
            byte[] original = new byte[stride * height];
            bgra.CopyPixels(original, stride, 0);
            int minX = width, minY = height, maxX = -1, maxY = -1;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (original[y * stride + x * 4 + 3] < 12) continue;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
            if (maxX < minX || maxY < minY) return source;
            int desiredX = (int)Math.Round((width - 1) / 2.0);
            int desiredY = height - 2;
            int dx = (int)Math.Round(desiredX - anchor.X);
            int dy = (int)Math.Round(desiredY - anchor.Y);
            dx = Math.Max(-minX, Math.Min(width - 1 - maxX, dx));
            dy = Math.Max(-minY, Math.Min(height - 1 - maxY, dy));
            if (dx == 0 && dy == 0) return bgra;

            byte[] shifted = new byte[original.Length];
            int sourceX = dx < 0 ? -dx : 0;
            int targetX = dx > 0 ? dx : 0;
            int copyPixels = width - Math.Abs(dx);
            for (int y = 0; y < height; y++)
            {
                int targetY = y + dy;
                if (targetY < 0 || targetY >= height) continue;
                Buffer.BlockCopy(original, y * stride + sourceX * 4,
                    shifted, targetY * stride + targetX * 4, copyPixels * 4);
            }
            BitmapSource output = BitmapSource.Create(width, height, bgra.DpiX, bgra.DpiY,
                PixelFormats.Bgra32, null, shifted, stride);
            output.Freeze();
            return output;
        }

        private static Point FindGroundAnchor(BitmapSource source)
        {
            if (source == null) return new Point(double.NaN, double.NaN);
            BitmapSource bgra = source;
            if (source.Format != PixelFormats.Bgra32 && source.Format != PixelFormats.Pbgra32)
            {
                FormatConvertedBitmap converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
                converted.Freeze();
                bgra = converted;
            }
            int width = bgra.PixelWidth;
            int height = bgra.PixelHeight;
            int stride = width * 4;
            byte[] pixels = new byte[stride * height];
            bgra.CopyPixels(pixels, stride, 0);
            int maxY = -1;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    if (pixels[y * stride + x * 4 + 3] >= 12) maxY = y;
            if (maxY < 0) return new Point(double.NaN, double.NaN);
            int bandTop = Math.Max(0, maxY - Math.Max(4, (int)Math.Round(height * 0.08)));
            double weightedX = 0;
            double weight = 0;
            for (int y = bandTop; y <= maxY; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    byte alpha = pixels[y * stride + x * 4 + 3];
                    if (alpha < 12) continue;
                    weightedX += x * alpha;
                    weight += alpha;
                }
            }
            return new Point(weight <= 0 ? (width - 1) / 2.0 : weightedX / weight, maxY);
        }

        public static Point AnimationGroundAnchor(BitmapSource source)
        {
            return FindGroundAnchor(source);
        }

        /// <summary>
        /// 生成“身体锁定、单爪下压”的打字差分帧。全部姿态都从同一张基准帧做局部平滑形变，
        /// 因此头、躯干、围巾、键盘和画布位置逐像素一致，不会再随击键整身跳动。
        /// </summary>
        public static BitmapSource GetTypingSoftFrame(string petId, TypingPawPose pose)
        {
            BitmapSource baseline = GetAnimationFrame(petId, "typing", 0);
            if (baseline == null || pose == TypingPawPose.Rest) return baseline;
            string key = petId + ":typing-soft:" + (int)pose;
            BitmapSource cached;
            if (AnimationFrames.TryGetValue(key, out cached)) return cached;

            bool left = pose == TypingPawPose.LeftSoft || pose == TypingPawPose.LeftDown;
            bool down = pose == TypingPawPose.LeftDown || pose == TypingPawPose.RightDown;
            PawRegion region = PawRegionFor(petId, left, baseline.PixelWidth, baseline.PixelHeight);
            BitmapSource warped = WarpLocalPaw(baseline, region, down ? 5.4 : 2.4);
            AnimationFrames[key] = warped;
            return warped;
        }

        public static void WarmTypingSoftFrames(string petId)
        {
            for (int pose = 0; pose <= 4; pose++) GetTypingSoftFrame(petId, (TypingPawPose)pose);
        }

        /// <summary>自测指标：活动爪区域之外的像素变化比例，商用品质红线为 &lt;2%。</summary>
        public static double TypingSoftOutsidePawDifferenceRatio(string petId, TypingPawPose pose)
        {
            BitmapSource baseline = GetTypingSoftFrame(petId, TypingPawPose.Rest);
            BitmapSource changed = GetTypingSoftFrame(petId, pose);
            if (baseline == null || changed == null) return 1;
            byte[] a = Pixels32(baseline);
            byte[] b = Pixels32(changed);
            bool left = pose == TypingPawPose.LeftSoft || pose == TypingPawPose.LeftDown;
            PawRegion region = PawRegionFor(petId, left, baseline.PixelWidth, baseline.PixelHeight);
            int different = 0;
            int considered = 0;
            for (int y = 0; y < baseline.PixelHeight; y++)
            {
                for (int x = 0; x < baseline.PixelWidth; x++)
                {
                    if (region.Contains(x, y)) continue;
                    considered++;
                    int i = (y * baseline.PixelWidth + x) * 4;
                    if (a[i] != b[i] || a[i + 1] != b[i + 1] || a[i + 2] != b[i + 2] || a[i + 3] != b[i + 3]) different++;
                }
            }
            return considered == 0 ? 0 : different / (double)considered;
        }

        public static double TypingSoftOverallDifferenceRatio(string petId, TypingPawPose pose)
        {
            BitmapSource baseline = GetTypingSoftFrame(petId, TypingPawPose.Rest);
            BitmapSource changed = GetTypingSoftFrame(petId, pose);
            if (baseline == null || changed == null) return 0;
            byte[] a = Pixels32(baseline);
            byte[] b = Pixels32(changed);
            int different = 0;
            int count = baseline.PixelWidth * baseline.PixelHeight;
            for (int pixel = 0; pixel < count; pixel++)
            {
                int i = pixel * 4;
                if (a[i] != b[i] || a[i + 1] != b[i + 1] || a[i + 2] != b[i + 2] || a[i + 3] != b[i + 3]) different++;
            }
            return different / (double)Math.Max(1, count);
        }

        private sealed class PawRegion
        {
            public double X;
            public double Y;
            public double RadiusX;
            public double RadiusY;

            public bool Contains(double x, double y)
            {
                double nx = (x - X) / RadiusX;
                double ny = (y - Y) / RadiusY;
                return nx * nx + ny * ny < 1;
            }
        }

        private static PawRegion PawRegionFor(string petId, bool left, int width, int height)
        {
            bool penguin = string.Equals(petId, "03-penguin", StringComparison.OrdinalIgnoreCase);
            double x = penguin
                ? (left ? 0.355 : 0.620)
                : (left ? 0.365 : 0.585);
            // 椭圆从腕部以下开始，避免局部形变碰到脸和围巾；中心下移后只压爪尖与键帽。
            double y = penguin ? (left ? 0.675 : 0.690) : 0.680;
            return new PawRegion
            {
                X = width * x,
                Y = height * y,
                RadiusX = width * (penguin ? 0.145 : 0.135),
                RadiusY = height * 0.100
            };
        }

        private static BitmapSource WarpLocalPaw(BitmapSource source, PawRegion region, double verticalPixelsAt251)
        {
            BitmapSource bgra = source;
            if (source.Format != PixelFormats.Bgra32)
            {
                FormatConvertedBitmap converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
                converted.Freeze();
                bgra = converted;
            }
            int width = bgra.PixelWidth;
            int height = bgra.PixelHeight;
            int stride = width * 4;
            byte[] original = new byte[stride * height];
            bgra.CopyPixels(original, stride, 0);
            byte[] result = (byte[])original.Clone();
            double shift = verticalPixelsAt251 * height / 251.0;
            int minX = Math.Max(1, (int)Math.Floor(region.X - region.RadiusX));
            int maxX = Math.Min(width - 2, (int)Math.Ceiling(region.X + region.RadiusX));
            int minY = Math.Max(1, (int)Math.Floor(region.Y - region.RadiusY));
            int maxY = Math.Min(height - 2, (int)Math.Ceiling(region.Y + region.RadiusY));
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    double nx = (x - region.X) / region.RadiusX;
                    double ny = (y - region.Y) / region.RadiusY;
                    double radius2 = nx * nx + ny * ny;
                    if (radius2 >= 1) continue;
                    double q = 1 - radius2;
                    double weight = q * q * (3 - 2 * q);
                    double sourceY = Math.Max(0, Math.Min(height - 1.001, y - shift * weight));
                    int y0 = (int)Math.Floor(sourceY);
                    int y1 = Math.Min(height - 1, y0 + 1);
                    double fy = sourceY - y0;
                    int target = y * stride + x * 4;
                    int sample0 = y0 * stride + x * 4;
                    int sample1 = y1 * stride + x * 4;
                    for (int channel = 0; channel < 4; channel++)
                        result[target + channel] = (byte)Math.Round(original[sample0 + channel] * (1 - fy) + original[sample1 + channel] * fy);
                }
            }
            BitmapSource output = BitmapSource.Create(width, height, bgra.DpiX, bgra.DpiY, PixelFormats.Bgra32, null, result, stride);
            output.Freeze();
            return output;
        }

        private static byte[] Pixels32(BitmapSource source)
        {
            BitmapSource bgra = source;
            if (source.Format != PixelFormats.Bgra32)
            {
                FormatConvertedBitmap converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
                converted.Freeze();
                bgra = converted;
            }
            int stride = bgra.PixelWidth * 4;
            byte[] pixels = new byte[stride * bgra.PixelHeight];
            bgra.CopyPixels(pixels, stride, 0);
            return pixels;
        }

        public static int AnimationFrameCount { get { return 25; } }
        public static int AnimationSheetCount { get { return 6; } }
        public static int AnimatedPetCount { get { return 2; } }

        /// <summary>按原始 PNG 的 alpha 通道判断局部是否属于宠物，而不是透明画布。</summary>
        public static bool IsOpaque(BitmapSource source, double normalizedX, double normalizedY, byte threshold)
        {
            if (source == null || normalizedX < 0 || normalizedY < 0 || normalizedX > 1 || normalizedY > 1) return false;
            AlphaMask mask = GetAlphaMask(source);
            if (mask == null) return false;
            int x = Math.Max(0, Math.Min(mask.Width - 1, (int)(normalizedX * (mask.Width - 1))));
            int y = Math.Max(0, Math.Min(mask.Height - 1, (int)(normalizedY * (mask.Height - 1))));
            return mask.Alpha[y * mask.Width + x] >= threshold;
        }

        /// <summary>
        /// 返回 PNG 非透明主体的归一化边界。边缘吸附使用这条真实轮廓，透明画布不再提前触发。
        /// </summary>
        public static Rect OpaqueBounds(BitmapSource source, byte threshold)
        {
            AlphaMask mask = GetAlphaMask(source);
            if (mask == null) return new Rect(0, 0, 1, 1);
            int minX = mask.Width, minY = mask.Height, maxX = -1, maxY = -1;
            for (int y = 0; y < mask.Height; y++)
            {
                int row = y * mask.Width;
                for (int x = 0; x < mask.Width; x++)
                {
                    if (mask.Alpha[row + x] < threshold) continue;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
            if (maxX < minX || maxY < minY) return new Rect(0, 0, 1, 1);
            double x0 = minX / (double)mask.Width;
            double y0 = minY / (double)mask.Height;
            double x1 = (maxX + 1) / (double)mask.Width;
            double y1 = (maxY + 1) / (double)mask.Height;
            return new Rect(x0, y0, x1 - x0, y1 - y0);
        }

        private static AlphaMask GetAlphaMask(BitmapSource source)
        {
            if (source == null) return null;
            AlphaMask mask;
            if (AlphaMasks.TryGetValue(source, out mask)) return mask;

            // 命中检测只需要轮廓，不应为每个姿态永久复制一份 512² BGRA 图。
            // 先降采样到最长边 192px，再只保留单通道 alpha：每帧约 36KB。
            BitmapSource sample = source;
            const double maxMaskSide = 192.0;
            double longest = Math.Max(source.PixelWidth, source.PixelHeight);
            if (longest > maxMaskSide)
            {
                double ratio = maxMaskSide / longest;
                TransformedBitmap scaled = new TransformedBitmap(source, new ScaleTransform(ratio, ratio));
                scaled.Freeze();
                sample = scaled;
            }
            BitmapSource bgra = sample;
            if (sample.Format != PixelFormats.Bgra32 && sample.Format != PixelFormats.Pbgra32)
            {
                FormatConvertedBitmap converted = new FormatConvertedBitmap(sample, PixelFormats.Bgra32, null, 0);
                converted.Freeze();
                bgra = converted;
            }
            int stride = bgra.PixelWidth * 4;
            byte[] pixels = new byte[stride * bgra.PixelHeight];
            bgra.CopyPixels(pixels, stride, 0);
            byte[] alpha = new byte[bgra.PixelWidth * bgra.PixelHeight];
            for (int maskY = 0; maskY < bgra.PixelHeight; maskY++)
                for (int maskX = 0; maskX < bgra.PixelWidth; maskX++)
                    alpha[maskY * bgra.PixelWidth + maskX] = pixels[maskY * stride + maskX * 4 + 3];
            mask = new AlphaMask { Width = bgra.PixelWidth, Height = bgra.PixelHeight, Alpha = alpha };
            AlphaMasks[source] = mask;
            return mask;
        }
    }

    public sealed class WorkMateApp : Application
    {
        private readonly string[] args;
        public DataStore Store { get; private set; }
        public ActivityTracker Tracker { get; private set; }
        public PetWindow Pet { get; private set; }
        public WorkbenchWindow Workbench { get; private set; }
        public QuickCaptureWindow QuickCapture { get; private set; }
        public OutlookMeetingRadar MeetingRadar { get; private set; }
        public CarryService Carry { get; private set; }
        public CarryShelfWindow CarryShelf { get; private set; }
        public EmbeddedToolManager Tools { get; private set; }
        public WindowsOcrService Ocr { get; private set; }
        public OpenMeteoWeatherService Weather { get; private set; }
        public CustomPetService CustomPets { get; private set; }
        public UpdateManager Updates { get; private set; }
        public bool IsExiting { get; private set; }

        // 感知与行为层（Kimi 轮新增）
        public RawInputMonitor RawInput { get; private set; }
        public MediaStateMonitor Media { get; private set; }
        public PresentationGuard Guard { get; private set; }
        public StateEngine Engine { get; private set; }
        public EventBridge Bridge { get; private set; }
        public DebugHud Hud { get; private set; }
        public bool DemoModeActive { get; private set; }
        private MonitorFollower follower;
        private DispatcherTimer engineTimer;
        private int engineTicks;
        private string lastLunchDate = "";
        private string lastHomeDate = "";
        private string lastOffworkDate = "";
        private string lastWeeklyDate = "";
        private DateTime sessionLockedAt = DateTime.MinValue;
        private bool systemEventsAttached;

        // 中断恢复书签（v1.5）
        private readonly InterruptionSession interruption = new InterruptionSession();
        private readonly InterruptionAnalytics interruptionAnalytics = new InterruptionAnalytics();
        private readonly MemoNudgeService memoNudges = new MemoNudgeService();
        private BehaviorState prevBehavior = BehaviorState.Idle;
        private string lastMeetingAlertId = "";
        private bool pendingWorkBreakReminder;
        private bool scrollCaptureActive;
        private readonly ProactiveNoticeQueue pendingProactiveNotices = new ProactiveNoticeQueue();
        private AmbientSnapshot latestAmbient;
        private bool ambientPollInFlight;
        private bool ambientInteractiveInFlight;
        private bool ambientAttemptFinished;
        private int ambientRequestGeneration;
        private int ambientCompletedGeneration;
        private bool pendingDailyBriefing;
        private DateTime nextAmbientPollAt = DateTime.MaxValue;
        private DateTime lastProactiveAt = DateTime.MinValue;
        private string lastDailyStateMaintenanceDate = "";

        public bool HudVisible { get { return Hud != null && Hud.IsVisible; } }

        public WorkMateApp(string[] args)
        {
            this.args = args ?? new string[0];
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            DispatcherUnhandledException += delegate(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
            {
                try
                {
                    string root = Store == null ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WorkMatePro") : Store.RootDirectory;
                    Directory.CreateDirectory(root);
                    File.AppendAllText(Path.Combine(root, "error.log"), DateTime.Now.ToString("o") + Environment.NewLine + e.Exception + Environment.NewLine + Environment.NewLine, new UTF8Encoding(false));
                }
                catch { }
                e.Handled = true;
            };
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement), new FrameworkPropertyMetadata(XmlLanguage.GetLanguage("zh-CN")));
            Store = new DataStore();
            CustomPets = new CustomPetService(Store.RootDirectory);
            PetCatalog.ReloadCustom();
            Carry = new CarryService(Store);
            MeetingRadar = new OutlookMeetingRadar();
            Tools = new EmbeddedToolManager();
            Ocr = new WindowsOcrService(Tools, Store.RootDirectory);
            Weather = new OpenMeteoWeatherService();
            Updates = new UpdateManager();
            nextAmbientPollAt = DateTime.Now.AddSeconds(15);
            lastProactiveAt = DateTime.Now;

            RawInput = new RawInputMonitor();
            RawInput.KeyMake += delegate { if (Pet != null) Pet.OnKeyRhythm(); };
            RawInput.UserActivity += delegate { if (Pet != null) Pet.OnAmbientUserActivity(); };
            Media = new MediaStateMonitor();
            Guard = new PresentationGuard();
            Engine = new StateEngine();
            Bridge = new EventBridge(OnBridgeEvent);
            Bridge.Start();

            Pet = new PetWindow(this);
            Pet.Show();
            AttachSystemMoments();

            Hud = new DebugHud(this, Pet);
            follower = new MonitorFollower(PetHandle, Pet.CanMigrate, Pet.MigrateToMonitor);
            engineTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            engineTimer.Tick += EngineTick;
            engineTimer.Start();

            Tracker = new ActivityTracker(Store);
            Tracker.Reward += delegate(string text) { Engine.NotifyPositive(); Pet.Celebrate(text); };
            Tracker.Start();
            if (Store.Data.MeetingRadarEnabled) MeetingRadar.PollIfDue(true);

            if (args.Contains("--capabilities")) OpenWorkbench("capabilities");
            else if (args.Contains("--settings")) OpenWorkbench("settings");
            else if (args.Contains("--workbench")) OpenWorkbench("memos");
            if (args.Contains("--quick")) OpenQuickCapture();
            if (args.Contains("--hud")) ToggleHud();
            string stateArg = args.FirstOrDefault(delegate(string value) { return value.StartsWith("--state="); });
            if (stateArg != null) Pet.SetPreviewState(stateArg.Substring("--state=".Length));

            if (Store.Data.FirstRun)
            {
                DispatcherTimer welcome = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
                welcome.Tick += delegate
                {
                    welcome.Stop();
                    Pet.ShowWelcome();
                    Store.Data.FirstRun = false;
                    Store.Save();
                };
                welcome.Start();
            }

            // 开工问候：每天第一次见面，跳一下打个招呼（当天多次启动只问一次）
            string todayGreet = DateTime.Today.ToString("yyyy-MM-dd");
            if (Store.Data.LastGreetDate != todayGreet)
            {
                Store.Data.LastGreetDate = todayGreet;
                Store.Save();
                DispatcherTimer greet = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1300) };
                greet.Tick += delegate
                {
                    greet.Stop();
                    Pet.Motion.Hop();
                    Pet.EventToast("早上好，今天也一起加油");
                };
                greet.Start();
            }

            int anniversaryDay;
            string anniversaryKey = CompanionMilestones.KeyFor(Store.Data.FirstCompanionDate, DateTime.Now,
                Store.Data.LastAnniversaryKey, out anniversaryDay);
            if (anniversaryKey != null)
            {
                Store.Data.LastAnniversaryKey = anniversaryKey;
                Store.Save();
                DispatcherTimer anniversary = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2600) };
                anniversary.Tick += delegate
                {
                    anniversary.Stop();
                    Pet.Motion.Hop();
                    Pet.Celebrate("一起陪伴第 " + anniversaryDay + " 天啦");
                };
                anniversary.Start();
            }

            // 更新器只有在完整初始化、桌宠窗口和后台服务均建立后才收到健康标记。
            // 若启动在此前失败，独立更新器会自动恢复上一个 EXE。
            UpdateApplier.MarkHealthy(args);
            string updatedVersion;
            if (UpdateApplier.TryArgument(args, "--post-update-version", out updatedVersion))
            {
                DispatcherTimer updated = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
                updated.Tick += delegate
                {
                    updated.Stop();
                    if (Pet != null) Pet.CapabilityToast("↻", "已安全更新到 v" + updatedVersion + "。", true);
                };
                updated.Start();
            }

            // 启动时只看本地目录，不产生任何联网请求。用户把目录或 ZIP 拷进收件箱后，
            // 下次启动即可得到一次提醒；同一目标版本不会反复打扰。
            DispatcherTimer localUpdateProbe = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
            localUpdateProbe.Tick += delegate
            {
                localUpdateProbe.Stop();
                ThreadPool.QueueUserWorkItem(delegate
                {
                    UpdateCheckResult check = Updates.CheckLocal();
                    if (!Updates.MarkNotificationIfNew(check)) return;
                    Dispatcher.BeginInvoke(new Action(delegate
                    {
                        if (Pet != null) Pet.CapabilityToast("↻", "检测到本地更新 v" + check.LatestVersion + "，可在能力中心导入。", false);
                    }));
                });
            };
            localUpdateProbe.Start();

            if (args.Contains("--update-health-exit"))
            {
                DispatcherTimer healthExit = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
                healthExit.Tick += delegate { healthExit.Stop(); Shutdown(); };
                healthExit.Start();
            }
        }

        private void AttachSystemMoments()
        {
            if (systemEventsAttached) return;
            try
            {
                SystemEvents.SessionEnding += SystemSessionEnding;
                SystemEvents.SessionSwitch += SystemSessionSwitch;
                SystemEvents.PowerModeChanged += SystemPowerModeChanged;
                systemEventsAttached = true;
            }
            catch { systemEventsAttached = false; }
        }

        private void DetachSystemMoments()
        {
            if (!systemEventsAttached) return;
            try
            {
                SystemEvents.SessionEnding -= SystemSessionEnding;
                SystemEvents.SessionSwitch -= SystemSessionSwitch;
                SystemEvents.PowerModeChanged -= SystemPowerModeChanged;
            }
            catch { }
            systemEventsAttached = false;
        }

        private void SystemSessionEnding(object sender, SessionEndingEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(delegate { PresentFarewell(DateTime.Now, true); }), DispatcherPriority.Send);
        }

        private void SystemPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode == PowerModes.Suspend)
                Dispatcher.BeginInvoke(new Action(delegate { PresentFarewell(DateTime.Now, false); }));
        }

        private void SystemSessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            if (e.Reason == SessionSwitchReason.SessionLock)
            {
                sessionLockedAt = DateTime.Now;
                if (sessionLockedAt.Hour >= 18)
                    Dispatcher.BeginInvoke(new Action(delegate { PresentFarewell(sessionLockedAt, false); }));
            }
            else if (e.Reason == SessionSwitchReason.SessionUnlock && sessionLockedAt != DateTime.MinValue)
            {
                TimeSpan away = DateTime.Now - sessionLockedAt;
                sessionLockedAt = DateTime.MinValue;
                if (away.TotalMinutes >= 10)
                    Dispatcher.BeginInvoke(new Action(delegate { Pet.EventToast("你回来啦，我刚刚也休息了一会儿"); }));
            }
        }

        private void PresentFarewell(DateTime now, bool force)
        {
            if (Pet == null || Pet.RetreatActive) return;
            string today = now.ToString("yyyy-MM-dd");
            if (!force && Store.Data.LastFarewellDate == today) return;
            Store.Data.LastFarewellDate = today;
            Store.Save();
            Pet.Farewell(CompanionMilestones.FarewellMessage(now));
        }

        private IntPtr PetHandle()
        {
            return new WindowInteropHelper(Pet).Handle;
        }

        public void AttachRawInput(IntPtr handle)
        {
            if (RawInput != null) RawInput.Register(handle);
        }

        public void RouteRawInput(IntPtr lParam)
        {
            if (RawInput != null)
            {
                // 消息投递时间（与 TickCount 同基）：主机拥塞延迟投递时仍按真实输入时刻统计
                double eventTime = NativeSignals.GetMessageTime() / 1000.0;
                RawInput.ProcessInput(lParam, eventTime);
            }
        }

        /// <summary>状态引擎 500ms 一跳：采样 → 决策 → 表演 → HUD。前台进程采样降为 2s（句柄抖动）。</summary>
        private string foregroundCategory = "其他";
        private int foregroundIdleFallback;
        private int lastLevel = -1;

        private void EngineTick(object sender, EventArgs e)
        {
            engineTicks++;
            if (engineTicks <= 2 || engineTicks % 4 == 0)
            {
                Media.Poll();
                Guard.Poll();
                follower.Check();
                ActivitySnapshot snapshot = ActivityTracker.ReadSnapshot();
                foregroundCategory = snapshot.Category;
                foregroundIdleFallback = snapshot.IdleSeconds;
            }

            double keyPerMin, wheelPerMin, clickPerMin, movePerMin;
            int idleSeconds;
            RawInput.GetRates(out keyPerMin, out wheelPerMin, out clickPerMin, out movePerMin, out idleSeconds);
            if (!RawInput.Registered) idleSeconds = foregroundIdleFallback; // RawInput 挂载失败的兜底

            SignalSample sample = new SignalSample
            {
                KeyPerMin = keyPerMin,
                WheelPerMin = wheelPerMin,
                ClickPerMin = clickPerMin,
                MovePerMin = movePerMin,
                IdleSeconds = idleSeconds,
                IdleIntentionalSeconds = RawInput.Registered ? RawInput.IntentionalIdleSeconds() : idleSeconds,
                MicInUse = Media.MicInUse,
                CameraInUse = Media.CameraInUse,
                AudioActive = Media.AudioActive,
                AudioPeak = Media.LastPeak,
                ForegroundCategory = foregroundCategory,
                Now = DateTime.Now
            };
            Engine.LongSittingEnabled = Store.Data.WorkBreakReminderEnabled;
            Engine.LongSittingThresholdSec = Math.Max(15, Math.Min(240, Store.Data.WorkBreakMinutes)) * 60;
            if (!Engine.LongSittingEnabled) pendingWorkBreakReminder = false;
            EngineOutput output = Engine.Tick(sample);
            Pet.ApplyBehavior(output);
            Pet.SetRetreat(scrollCaptureActive || (Store.Data.PresentationGuardEnabled && Guard.ShouldRetreat) || DemoModeActive);

            // 心流记账（不触发工作奖励，单独累计）
            if (output.FlowActive && engineTicks % 4 == 0) Store.AddFlowSeconds(2);

            // 中断恢复书签：连续的开会/离开合并为一次中断，真正回到工作态后再提醒。
            BehaviorState nowBehavior = output.State;
            bool wasInterrupted = prevBehavior == BehaviorState.Meeting || prevBehavior == BehaviorState.Away;
            bool isInterrupted = nowBehavior == BehaviorState.Meeting || nowBehavior == BehaviorState.Away;
            if (!wasInterrupted && isInterrupted)
            {
                interruption.Begin(Store, foregroundCategory, nowBehavior == BehaviorState.Meeting, DateTime.Now);
            }
            else if (wasInterrupted && isInterrupted)
            {
                interruption.Continue(nowBehavior == BehaviorState.Meeting);
            }
            else if (wasInterrupted && !isInterrupted)
            {
                string resume = interruption.End(DateTime.Now);
                if (resume != null) Pet.ResumeToast(resume);
            }
            prevBehavior = nowBehavior;
            interruptionAnalytics.Observe(Store, nowBehavior, foregroundCategory, DateTime.Now);

            // 引擎事件：久坐提醒 / 休息奖励
            if (output.Events != null)
            {
                foreach (string ev in output.Events)
                {
                    if (ev == "long-sitting")
                    {
                        if (CanPresentWorkBreak(output)) PresentWorkBreakReminder();
                        else pendingWorkBreakReminder = true;
                    }
                    else if (ev == "break-reward")
                    {
                        pendingWorkBreakReminder = false;
                        Store.AddCompanionValue(2);
                        Store.Save();
                        Engine.NotifyPositive();
                        Pet.Celebrate("好好休息了一下，陪伴值 +2");
                    }
                }
            }
            if (pendingWorkBreakReminder && CanPresentWorkBreak(output)) PresentWorkBreakReminder();

            // 升级检测（会话内）：等级抬升 → 彩带庆祝
            if (lastLevel < 0) lastLevel = Store.Level;
            if (Store.Level > lastLevel)
            {
                lastLevel = Store.Level;
                Engine.NotifyPositive();
                Pet.LevelUp(Store.Level);
            }

            if (Hud != null && Hud.IsVisible)
            {
                string rawDetail = (RawInput.Registered ? "已挂" : "失败") +
                    " msg=" + RawInput.MessagesSeen + " 键=" + RawInput.KeyEvents + " make=" + RawInput.KeyMakes + " fl=" + RawInput.LastFlags +
                    " 鼠=" + RawInput.MouseEvents + " 错=" + RawInput.ParseFailures +
                    " 队=" + RawInput.KeyQueueSize + "/" + RawInput.WheelQueueSize +
                    " now=" + RawInput.LastNowRates.ToString("0.0") + "/" + RawInput.LastNowProcess.ToString("0.0") + "/" + RawInput.LastEnqueuedT.ToString("0.0") +
                    " 久坐=" + (int)(Engine.ContinuousActiveSec / 60) + "m 摸头=" + Pet.PatCount + " dock=" + Pet.DockDebug + " 晕=" + (Pet.Motion.DizzyActive ? 1 : 0) +
                    (Media.ConsentStoreAvailable ? "" : " 信号源不可用");
                Hud.UpdateFrom(sample, output, Pet.Motion.FpsMode, Guard.ShouldRetreat || DemoModeActive, rawDetail, Media.AudioProbeOk);
            }

            // 时机关怀（参数通道的免费红利）：每天最多一次的干饭/回家提醒
            string kind;
            string toast = RhythmBaseline.OccasionToast(DateTime.Now, lastLunchDate, lastHomeDate, out kind, lastOffworkDate);
            if (toast != null && !Pet.DockActive && !Pet.RetreatActive && !Pet.QuietModeActive
                && !Pet.FocusRitualActive && !output.FlowActive)
            {
                if (kind == "lunch") lastLunchDate = DateTime.Now.ToString("yyyy-MM-dd");
                if (kind == "home") lastHomeDate = DateTime.Now.ToString("yyyy-MM-dd");
                if (kind == "offwork")
                {
                    lastOffworkDate = DateTime.Now.ToString("yyyy-MM-dd");
                    Pet.Motion.Hop();
                }
                Pet.EventToast(toast);
            }
            // 周五 17 点：周报素材就绪提醒（心流中也不打断，这条放过——周报是刚需）
            if (!Pet.QuietModeActive && !Pet.FocusRitualActive && DateTime.Now.DayOfWeek == DayOfWeek.Friday
                && DateTime.Now.Hour == 17 && lastWeeklyDate != DateTime.Now.ToString("yyyy-MM-dd"))
            {
                lastWeeklyDate = DateTime.Now.ToString("yyyy-MM-dd");
                Pet.EventToast("周五了，本周素材已就绪：今日小结 → 复制本周素材");
            }

            // Outlook 会议雷达：3 分钟后台轮询；会前 5 分钟只提醒一次，会议信息不落盘。
            if (Store.Data.MeetingRadarEnabled && engineTicks % 120 == 0) MeetingRadar.PollIfDue(false);
            if (Store.Data.MeetingRadarEnabled && engineTicks % 10 == 0)
            {
                MeetingInfo next = MeetingRadar.NextMeeting;
                if (next != null)
                {
                    double minutes = (next.Start - DateTime.Now).TotalMinutes;
                    if (minutes >= 0 && minutes <= 5 && next.Id != lastMeetingAlertId && !Pet.RetreatActive && !Pet.QuietModeActive)
                    {
                        lastMeetingAlertId = next.Id;
                        Pet.ShowMeeting(next);
                    }
                }
            }

            // 备忘催办：一分钟评估一次，但全局 4 小时 + 单条 24 小时限流，心流/会议/打字时永不弹。
            if (engineTicks % 120 == 0 && !Pet.DockActive && !Pet.RetreatActive && !Pet.QuietModeActive)
            {
                MemoItem due = memoNudges.TryPick(Store, DateTime.Now, output.State,
                    output.FlowActive || Pet.FocusRitualActive);
                if (due != null)
                {
                    Store.MarkMemoNudged(due, DateTime.Now);
                    Pet.ResumeToast("还记得这件事吗：" + Formatters.Truncate(due.Text, 24));
                }
            }

            // 主动能力：联网轮询与提醒展示分离。后台可先查，会议/打字/心流时只排队，安全时再由宠物递送。
            if (engineTicks % 2 == 0)
            {
                DateTime proactiveNow = DateTime.Now;
                AmbientBackgroundTick(output, proactiveNow);
                DailyPriorityBackgroundTick(output, proactiveNow);
            }
        }

        public void ToggleHud()
        {
            if (Hud == null) return;
            if (Hud.IsVisible) Hud.Hide();
            else Hud.ShowNearPet();
        }

        public void ToggleDemoMode()
        {
            DemoModeActive = !DemoModeActive;
            if (!DemoModeActive) Pet.EventToast("演示结束，我回来了");
        }

        public void NotifyPositiveFeedback()
        {
            if (Engine != null) Engine.NotifyPositive();
        }

        private void OnBridgeEvent(string line)
        {
            Dispatcher.BeginInvoke(new Action(delegate
            {
                string message = (line ?? "").Trim();
                if (message.Length == 0 || message == "__bye__") return;
                if (message == EventBridge.ActivateWorkbench) { ActivateExistingInstance(false); return; }
                if (message == EventBridge.ActivateQuickCapture) { ActivateExistingInstance(true); return; }
                if (message == "open-settings") { OpenWorkbench("settings"); return; }
                if (message == "open-capabilities") { OpenWorkbench("capabilities"); return; }
                if (message == "build-start") { Pet.SetBuildWait(true); Pet.EventToast("收到，我帮你盯着编译"); }
                else if (message == "build-ok") { Pet.SetBuildWait(false); Engine.NotifyPositive(); Pet.Celebrate("编译成功，干得漂亮"); }
                else if (message == "build-fail") { Pet.SetBuildWait(false); Pet.EventToast("编译没过，没关系，一起看看日志"); }
                else if (message == "test-failed") { Pet.SetBuildWait(false); Pet.EventToast("有测试红了，我陪你修"); }
                else if (message == "deploy-ok") { Pet.SetBuildWait(false); Engine.NotifyPositive(); Pet.Celebrate("部署完成"); }
                else if (message == "dock-top") Pet.DebugDock(DockEdge.Top);
                else if (message == "dock-bottom") Pet.DebugDock(DockEdge.Bottom);
                else if (message == "dock-left") Pet.DebugDock(DockEdge.Left);
                else if (message == "dock-right") Pet.DebugDock(DockEdge.Right);
                else if (message == "carry-demo") Pet.PlayCarryAnimation();
                else if (message == "stretch-demo") Pet.PlayStretchAnimation();
                else if (message == "wake-demo") Pet.PlayWakeStretchDemo();
                else if (message == "bubble-demo") Pet.PlayBubbleDemo();
                else if (message == "refresh-attention-demo") Pet.PlayRefreshAttentionDemo();
                else if (message == "dock-refresh-demo") Pet.PlayDockRefreshDemo();
                else if (message == "dock-delay-cancel-demo") Pet.PlayDockDelayCancelDemo();
                else if (message == "context-preemption-demo") Pet.PlayContextPreemptionDemo();
                else if (message == "build-nodoff-preemption-demo") Pet.PlayBuildNodOffPreemptionDemo();
                else if (message == "retreat-preemption-demo") Pet.PlayRetreatPreemptionDemo();
                else if (message == "shutdown-demo") Shutdown();
                else if (message == "nodoff-demo") Pet.PlayNodOffDemo();
                else if (message == "quiet-demo") Pet.PlayQuietDemo();
                else if (message == "focus-ritual-demo") Pet.PlayFocusRitualDemo();
                else if (message == "dance-demo") Pet.PlayDanceDemo();
                else if (message == "hula-demo") Pet.PlayHulaDemo();
                else if (message == "butterfly-demo") Pet.PlayButterflyDemo();
                else if (message == "bubbles-demo") Pet.PlayBubblesDemo();
                else if (message == "farewell-demo") Pet.Farewell(CompanionMilestones.FarewellMessage(DateTime.Now));
                else if (message == "anniversary-demo") Pet.Celebrate("一起陪伴第 100 天啦");
                else if (message == "shy-demo") Pet.PlayShyDemo();
                else if (message == "tailchase-demo") Pet.PlayTailChaseDemo();
                else if (message == "knead-demo") Pet.PlayKneadDemo();
                else if (message == "balloon-demo") Pet.PlayBalloonDemo();
                else if (message == "treat-demo") Pet.PlayTreatDemo();
                else if (message == "highfive-demo") Pet.PlayHighFiveDemo();
                else if (message == "summon-demo") Pet.PlaySummonDemo();
                else if (message == "patrol-demo") Pet.PlayPatrolDemo();
                else if (message == "pat-demo") Pet.DebugPatReaction();
                else if (message == "pat-key-cancel-demo") Pet.PlayPatKeyboardCancelDemo();
                else if (message == "meeting-demo")
                {
                    DateTime start = DateTime.Now.AddMinutes(5);
                    Pet.PlayMeetingAnimation();
                    Pet.ShowMeeting(new MeetingInfo
                    {
                        Id = "demo",
                        Subject = "技术方案评审（示例）",
                        Start = start,
                        End = start.AddMinutes(45),
                        Location = "线上会议 / A3-201",
                        Attendees = "张三、李四、王五 等 8 人"
                    });
                }
                else if (message == "weather-sentinel-demo") Pet.CapabilityToast("☂", "约 1 小时后可能下雨——我把伞提醒叼来啦。", true);
                else if (message == "outdoor-advisor-demo") Pet.CapabilityToast("☀", "今天紫外线很高，我替你守着防晒提醒。", false);
                else if (message == "daily-briefing-demo") ShowDailyBriefing(null);
                else if (message.StartsWith("say ")) Pet.EventToast(message.Substring(4));
                else Pet.EventToast(message);
            }));
        }

        private void ActivateExistingInstance(bool quickCapture)
        {
            // 用户再次双击代表“我需要看见它”。捕获保护若与远程桌面/共享链路不兼容，
            // 必须先恢复可见性，否则即使工作台打开，用户仍会以为程序死了。
            if (Store.Data.HideFromCaptureEnabled)
            {
                Store.Data.HideFromCaptureEnabled = false;
                Store.Save();
                WindowPrivacy.ApplyAll(false);
            }
            Pet.RestoreForActivation();
            if (quickCapture) OpenQuickCapture();
            else OpenWorkbench("memos");
        }

        public void OpenQuickCapture()
        {
            if (QuickCapture == null) QuickCapture = new QuickCaptureWindow(this);
            QuickCapture.ShowCapture();
        }

        public void OpenWorkbench(string page)
        {
            if (Workbench == null) Workbench = new WorkbenchWindow(this);
            Workbench.ShowPage(page);
        }

        public void OpenScrollCapture()
        {
            try
            {
                scrollCaptureActive = true;
                if (Workbench != null) Workbench.Hide();
                if (QuickCapture != null) QuickCapture.Hide();
                if (Pet != null) { Pet.SetRetreat(true); Pet.Hide(); }
                Process process = Tools.LaunchScrollCapture();
                if (process == null) throw new InvalidOperationException("滚动截图工具未能启动。");
                process.Exited += delegate
                {
                    try
                    {
                        Dispatcher.BeginInvoke(new Action(delegate
                        {
                            if (IsExiting || Pet == null) return;
                            scrollCaptureActive = false;
                            Pet.SetRetreat((Store.Data.PresentationGuardEnabled && Guard.ShouldRetreat) || DemoModeActive);
                            Pet.RestoreForActivation();
                            Pet.EventToast("滚动截图已结束，我回来啦");
                        }));
                    }
                    catch { }
                    try { process.Dispose(); } catch { }
                };
                process.EnableRaisingEvents = true;
            }
            catch (Exception ex)
            {
                scrollCaptureActive = false;
                if (Pet != null)
                {
                    Pet.SetRetreat((Store.Data.PresentationGuardEnabled && Guard.ShouldRetreat) || DemoModeActive);
                    Pet.RestoreForActivation();
                    Pet.EventToast("滚动截图启动失败：" + Formatters.Truncate(ex.Message, 42));
                }
            }
        }

        public void RecognizeClipboardImage(Action<OcrResponse> callback)
        {
            try
            {
                if (!Clipboard.ContainsImage())
                {
                    CompleteOcr(new OcrResponse { Success = false, Error = "剪贴板里没有图片。" }, callback);
                    return;
                }
                BitmapSource image = Clipboard.GetImage();
                string inbox = Path.Combine(Store.RootDirectory, "OCR", "Inbox");
                Directory.CreateDirectory(inbox);
                string path = Path.Combine(inbox, "Clipboard-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".png");
                PngBitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(image));
                using (FileStream stream = File.Create(path)) encoder.Save(stream);
                RecognizeImage(path, callback);
            }
            catch (Exception ex)
            {
                CompleteOcr(new OcrResponse { Success = false, Error = ex.Message }, callback);
            }
        }

        public void RecognizeImage(string imagePath, Action<OcrResponse> callback)
        {
            Ocr.RecognizeAsync(imagePath, delegate(OcrResponse response)
            {
                Dispatcher.BeginInvoke(new Action(delegate { CompleteOcr(response, callback); }));
            });
        }

        private void CompleteOcr(OcrResponse response, Action<OcrResponse> callback)
        {
            if (response.Success)
            {
                if (!string.IsNullOrWhiteSpace(response.Text))
                {
                    try { Clipboard.SetText(response.Text); } catch { }
                    if (Pet != null) Pet.EventToast("OCR 完成，文字已复制到剪贴板");
                }
                else if (Pet != null) Pet.EventToast("OCR 完成，但没有识别到文字");
            }
            else if (Pet != null) Pet.EventToast("OCR 失败：" + Formatters.Truncate(response.Error, 42));
            if (callback != null) callback(response);
        }

        public void RefreshWeather(string city, Action<WeatherResponse> callback)
        {
            FetchAmbient(city, true, delegate(AmbientResponse ambient)
            {
                WeatherResponse response = new WeatherResponse
                {
                    Success = ambient.Success && ambient.Snapshot != null && ambient.Snapshot.Weather != null,
                    Snapshot = ambient.Snapshot == null ? null : ambient.Snapshot.Weather,
                    Error = ambient.Error,
                    FromCache = ambient.FromCache
                };
                if (response.Success && Pet != null)
                    Pet.CapabilityToast("☁", response.Snapshot.Location + "：" + response.Snapshot.Condition + " " + response.Snapshot.Temperature.ToString("0.#") + "°C", false);
                else if (Pet != null)
                    Pet.EventToast("天气获取失败：" + Formatters.Truncate(response.Error, 36));
                if (callback != null) callback(response);
            });
        }

        public void RefreshOutdoor(string city, Action<AmbientResponse> callback)
        {
            FetchAmbient(city, true, delegate(AmbientResponse response)
            {
                if (response.Success && response.Snapshot != null && response.Snapshot.Outdoor != null)
                    Pet.CapabilityToast("☀", Formatters.Truncate(response.Snapshot.Outdoor.Summary, 72), false);
                else if (Pet != null)
                {
                    string error = response.Snapshot != null ? response.Snapshot.OutdoorError : response.Error;
                    Pet.EventToast("户外健康数据暂不可用：" + Formatters.Truncate(error, 30));
                }
                if (callback != null) callback(response);
            });
        }

        public void ShowDailyBriefing(Action<string> callback)
        {
            if (latestAmbient != null && DateTime.Now - latestAmbient.RetrievedAt < TimeSpan.FromMinutes(60))
            {
                PresentDailyBriefing(callback);
                return;
            }
            FetchAmbient(Store.Data.WeatherCity, true, delegate(AmbientResponse response)
            {
                // 即使网络失败，会议和本地待办仍会进入简报；失败只会明确缺少天气部分。
                PresentDailyBriefing(callback);
            });
        }

        public void ScheduleAmbientRefresh()
        {
            nextAmbientPollAt = DateTime.Now;
        }

        public void SetWeatherSentinelEnabled(bool enabled)
        {
            if (Store.Data.WeatherSentinelEnabled == enabled) return;
            Store.Data.WeatherSentinelEnabled = enabled;
            Store.Data.LastWeatherAlertKey = "";
            pendingProactiveNotices.Purge("weather");
            Store.Save();
            ScheduleAmbientRefresh();
        }

        public void SetOutdoorAdvisorEnabled(bool enabled)
        {
            if (Store.Data.OutdoorAdvisorEnabled == enabled) return;
            Store.Data.OutdoorAdvisorEnabled = enabled;
            Store.Data.LastOutdoorAlertKey = "";
            pendingProactiveNotices.Purge("outdoor");
            Store.Save();
            ScheduleAmbientRefresh();
        }

        public void SetDailyBriefingEnabled(bool enabled)
        {
            if (Store.Data.DailyBriefingEnabled == enabled) return;
            Store.Data.DailyBriefingEnabled = enabled;
            if (!enabled) pendingDailyBriefing = false;
            Store.Save();
            ScheduleAmbientRefresh();
        }

        public void SetAmbientPresenceEnabled(bool enabled)
        {
            if (Store.Data.AmbientPresenceEnabled == enabled) return;
            Store.Data.AmbientPresenceEnabled = enabled;
            Store.Save();
            if (Pet != null) Pet.UpdateAmbientPresence(enabled ? AmbientFreshnessPolicy.ForPresence(latestAmbient, DateTime.Now) : null);
            if (enabled) ScheduleAmbientRefresh();
        }

        public void SetTodayEnergyMode(string mode)
        {
            Store.SetTodayEnergyMode(mode);
            RefreshOpenWindows();
            if (Pet != null)
            {
                Pet.EventToast("今天按“" + CompanionEnergyPolicy.Label(Store.TodayEnergyMode) + "”的节奏陪你");
            }
        }

        private void FetchAmbient(string city, bool forceRefresh, Action<AmbientResponse> callback)
        {
            string normalized = (city ?? "").Trim();
            if (normalized.Length > 50) normalized = normalized.Substring(0, 50);
            if (normalized.Length > 0 && !string.Equals(Store.Data.WeatherCity, normalized, StringComparison.Ordinal))
            {
                Store.Data.WeatherCity = normalized;
                Store.Data.LastWeatherAlertKey = "";
                Store.Data.LastOutdoorAlertKey = "";
                pendingProactiveNotices.Purge("weather");
                pendingProactiveNotices.Purge("outdoor");
                latestAmbient = null;
                ambientAttemptFinished = false;
                Store.Save();
            }
            ambientInteractiveInFlight = true;
            int requestGeneration = ++ambientRequestGeneration;
            Weather.GetAmbientAsync(normalized, forceRefresh, delegate(AmbientResponse response)
            {
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    if (requestGeneration != ambientRequestGeneration) return;
                    ambientInteractiveInFlight = false;
                    if (!string.Equals(Store.Data.WeatherCity, normalized, StringComparison.Ordinal)) return;
                    ambientAttemptFinished = true;
                    ambientCompletedGeneration = requestGeneration;
                    if (response.Success && response.Snapshot != null)
                    {
                        latestAmbient = response.Snapshot;
                        if (Pet != null) Pet.UpdateAmbientPresence(latestAmbient);
                        nextAmbientPollAt = DateTime.Now.AddMinutes(30);
                    }
                    if (callback != null) callback(response);
                }));
            });
        }

        private void AmbientBackgroundTick(EngineOutput output, DateTime now)
        {
            if (latestAmbient != null && AmbientFreshnessPolicy.ForPresence(latestAmbient, now) == null)
            {
                latestAmbient = null;
                if (Pet != null) Pet.UpdateAmbientPresence(null);
            }
            bool anyEnabled = Store.Data.WeatherSentinelEnabled || Store.Data.OutdoorAdvisorEnabled
                || Store.Data.DailyBriefingEnabled || Store.Data.AmbientPresenceEnabled;
            if (anyEnabled && !ambientPollInFlight && !ambientInteractiveInFlight && now >= nextAmbientPollAt)
                PollAmbientInBackground(now);

            string today = now.ToString("yyyy-MM-dd");
            bool briefingWindow = now.Hour >= Store.Data.DailyBriefingHour && now.Hour <= 14;
            if (Store.Data.DailyBriefingEnabled && briefingWindow && Store.Data.LastDailyBriefingDate != today)
            {
                pendingDailyBriefing = true;
                if (latestAmbient == null && !ambientPollInFlight && !ambientInteractiveInFlight && now >= nextAmbientPollAt) PollAmbientInBackground(now);
            }
            if (!Store.Data.DailyBriefingEnabled) pendingDailyBriefing = false;

            if (!CanPresentProactive(output) || now - lastProactiveAt < TimeSpan.FromSeconds(12)) return;
            while (pendingProactiveNotices.Count > 0)
            {
                CompanionNotice notice = pendingProactiveNotices.DequeueReady(now);
                if (notice == null) break;
                if (notice.Category == "weather" && !Store.Data.WeatherSentinelEnabled) continue;
                if (notice.Category == "outdoor" && !Store.Data.OutdoorAdvisorEnabled) continue;
                if (notice.Priority < CompanionEnergyPolicy.MinimumProactivePriority(Store.TodayEnergyMode)) continue;
                Pet.CapabilityToast(notice.Cue, notice.Message, notice.Category == "weather");
                if (notice.Category == "weather") Store.Data.LastWeatherAlertKey = notice.Key;
                else if (notice.Category == "outdoor") Store.Data.LastOutdoorAlertKey = notice.Key;
                Store.Save();
                lastProactiveAt = now;
                return;
            }
            if (pendingDailyBriefing && ambientAttemptFinished && ambientCompletedGeneration == ambientRequestGeneration)
            {
                pendingDailyBriefing = false;
                PresentDailyBriefing(null);
                lastProactiveAt = now;
            }
        }

        private void PollAmbientInBackground(DateTime now)
        {
            string city = (Store.Data.WeatherCity ?? "").Trim();
            if (city.Length == 0) return;
            ambientPollInFlight = true;
            nextAmbientPollAt = now.AddMinutes(30);
            int requestGeneration = ++ambientRequestGeneration;
            Weather.GetAmbientAsync(city, delegate(AmbientResponse response)
            {
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    ambientPollInFlight = false;
                    if (requestGeneration != ambientRequestGeneration
                        || !string.Equals(Store.Data.WeatherCity, city, StringComparison.Ordinal))
                    {
                        nextAmbientPollAt = DateTime.Now;
                        return;
                    }
                    ambientAttemptFinished = true;
                    ambientCompletedGeneration = requestGeneration;
                    if (!response.Success || response.Snapshot == null)
                    {
                        nextAmbientPollAt = DateTime.Now.AddMinutes(10);
                        return;
                    }
                    latestAmbient = response.Snapshot;
                    if (Pet != null) Pet.UpdateAmbientPresence(latestAmbient);
                    QueueAmbientInsights(response.Snapshot, DateTime.Now);
                }));
            });
        }

        private void QueueAmbientInsights(AmbientSnapshot snapshot, DateTime now)
        {
            List<CompanionNotice> notices = AmbientInsightPolicy.Evaluate(snapshot,
                Store.Data.WeatherSentinelEnabled, Store.Data.OutdoorAdvisorEnabled, now);
            foreach (CompanionNotice notice in notices)
            {
                if (notice.Priority < CompanionEnergyPolicy.MinimumProactivePriority(Store.TodayEnergyMode)) continue;
                if (notice.Category == "weather")
                {
                    if (notice.Key == Store.Data.LastWeatherAlertKey) continue;
                }
                else
                {
                    if (notice.Key == Store.Data.LastOutdoorAlertKey) continue;
                }
                pendingProactiveNotices.EnqueueLatest(notice);
            }
        }

        private void DailyPriorityBackgroundTick(EngineOutput output, DateTime now)
        {
            string today = now.ToString("yyyy-MM-dd");
            if (lastDailyStateMaintenanceDate != today)
            {
                lastDailyStateMaintenanceDate = today;
                bool dailyStateReset = Store.ClearExpiredDailyPriority(now);
                dailyStateReset = Store.ClearExpiredEnergyMode(now) || dailyStateReset;
                if (dailyStateReset) RefreshOpenWindows();
            }
            if (now - lastProactiveAt < TimeSpan.FromSeconds(12)) return;
            MemoItem anchor = Store.AnchorMemo();
            int openMemoCount = Store.Data.Memos.Count(delegate(MemoItem memo) { return !memo.IsDone; });
            if (!DailyPriorityPromptPolicy.CanPrompt(now, Store.Data.LastPriorityPromptDate, anchor != null, openMemoCount,
                output.State, output.FlowActive, Pet.QuietModeActive, Pet.FocusRitualActive,
                Guard.ShouldRetreat || scrollCaptureActive || Pet.RetreatActive, DemoModeActive, Pet.DockActive)) return;
            Store.Data.LastPriorityPromptDate = now.ToString("yyyy-MM-dd");
            Store.Save();
            Pet.ResumeToast("今天先圈出一件最重要的事吧");
            lastProactiveAt = now;
        }

        private bool CanPresentProactive(EngineOutput output)
        {
            return ProactivePresentationPolicy.CanPresent(output.State, output.FlowActive, Pet.QuietModeActive,
                Pet.FocusRitualActive, Guard.ShouldRetreat || scrollCaptureActive || Pet.RetreatActive,
                DemoModeActive, Pet.DockActive);
        }

        private void PresentDailyBriefing(Action<string> callback)
        {
            if (MeetingRadar != null && Store.Data.MeetingRadarEnabled) MeetingRadar.PollIfDue(false);
            MeetingInfo meeting = MeetingRadar == null ? null : MeetingRadar.NextMeeting;
            int openMemoCount = Store.Data.Memos.Count(delegate(MemoItem memo) { return !memo.IsDone; });
            MemoItem anchor = Store.AnchorMemo();
            string briefing = DailyBriefingBuilder.Build(AmbientFreshnessPolicy.ForBriefing(latestAmbient, DateTime.Now), meeting, openMemoCount,
                anchor == null ? "" : anchor.Text, DateTime.Now);
            Store.Data.LastDailyBriefingDate = DateTime.Today.ToString("yyyy-MM-dd");
            Store.Save();
            if (Pet != null) Pet.CapabilityToast("✦", DailyBriefingBuilder.ToastText(briefing), true);
            if (callback != null) callback(briefing);
        }

        private bool CanPresentWorkBreak(EngineOutput output)
        {
            return WorkBreakPresentationPolicy.CanPresent(output.State, output.FlowActive, Pet.QuietModeActive,
                Pet.FocusRitualActive, Guard.ShouldRetreat || scrollCaptureActive, DemoModeActive);
        }

        private void PresentWorkBreakReminder()
        {
            pendingWorkBreakReminder = false;
            Pet.EventToast("已经连续工作 " + Store.Data.WorkBreakMinutes + " 分钟啦，伸个懒腰，再看看远处 20 秒");
            if (AnimationPolicy.ShouldPlayAutomaticStretch(Store.Data.StretchEnabled, Store.Data.ReducedMotion, Pet.QuietModeActive))
                Pet.PlayStretchAnimation();
            else if (AnimationPolicy.ShouldPlayStretchFallback(Store.Data.ReducedMotion, Pet.QuietModeActive))
                Pet.Motion.Squash(0.1);
        }

        public void OpenCarryShelf()
        {
            if (CarryShelf == null) CarryShelf = new CarryShelfWindow(this);
            CarryShelf.ShowShelf();
        }

        public void RefreshOpenWindows()
        {
            if (Workbench != null) Workbench.RefreshCurrentPage();
            if (Pet != null) Pet.RefreshPet();
        }

        public void SetPet(string petId)
        {
            SetPet(petId, "");
        }

        public void SetPet(string petId, string celebrationMessage)
        {
            if (PetCatalog.Find(petId) == null) return;
            Store.Data.PetId = petId;
            Store.Save();
            RefreshOpenWindows();
            Pet.Celebrate(string.IsNullOrWhiteSpace(celebrationMessage)
                ? "你好，我是" + PetCatalog.Find(petId).Name : celebrationMessage);
        }

        public void SetStartup(bool enabled)
        {
            const string path = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(path, true))
            {
                if (key != null)
                {
                    if (enabled) key.SetValue("WorkMatePro", "\"" + Assembly.GetExecutingAssembly().Location + "\"");
                    else key.DeleteValue("WorkMatePro", false);
                }
            }
            Store.Data.StartWithWindows = enabled;
            Store.Save();
        }

        public void ExitApp()
        {
            if (IsExiting) return;
            IsExiting = true;
            if (Pet == null) { Shutdown(); return; }
            PresentFarewell(DateTime.Now, true);
            DispatcherTimer goodbye = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(850) };
            goodbye.Tick += delegate { goodbye.Stop(); Shutdown(); };
            goodbye.Start();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            DetachSystemMoments();
            try { if (Tracker != null) Tracker.Dispose(); } catch { }
            try { if (Bridge != null) Bridge.Dispose(); } catch { }
            try { if (MeetingRadar != null) MeetingRadar.Dispose(); } catch { }
            try { if (Store != null) Store.Save(); } catch { }
            base.OnExit(e);
        }
    }

    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            if (args != null && args.Length > 0 && args[0] == "--apply-update")
            {
                Environment.ExitCode = UpdateApplier.Run(args);
                return;
            }
            if (args != null && args.Length > 0 && args[0] == "--create-delta")
            {
                try
                {
                    string source;
                    string target;
                    string output;
                    if (!UpdateApplier.TryArgument(args, "--source", out source)
                        || !UpdateApplier.TryArgument(args, "--target", out target)
                        || !UpdateApplier.TryArgument(args, "--output", out output))
                        throw new ArgumentException("--create-delta 需要 --source、--target 和 --output。");
                    MsDeltaCodec.CreateFile(Path.GetFullPath(source), Path.GetFullPath(target), Path.GetFullPath(output));
                    Environment.ExitCode = 0;
                }
                catch (Exception ex)
                {
                    try
                    {
                        string output;
                        if (UpdateApplier.TryArgument(args, "--output", out output))
                            File.WriteAllText(Path.GetFullPath(output) + ".error.log", ex.ToString(), new UTF8Encoding(false));
                    }
                    catch { }
                    Environment.ExitCode = 7;
                }
                return;
            }
            if (args != null && args.Length == 2 && args[0] == "--update-e2e-install"
                && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WORKMATE_TEST_DIR"))
                && Environment.GetEnvironmentVariable("WORKMATE_UPDATE_E2E") == "1")
            {
                UpdateManager manager = new UpdateManager();
                StagedUpdate staged = manager.StagePackage(args[1]);
                if (!staged.Success) { Environment.ExitCode = 8; return; }
                try { manager.LaunchStagedUpdate(staged); Environment.ExitCode = 0; }
                catch { Environment.ExitCode = 9; }
                return;
            }
            // 事件桥 CLI：WorkMate.exe tell build-ok —— 只把消息写进管道就退出
            if (args != null && args.Length > 0 && args[0] == "tell")
            {
                if (args.Length < 2) { Environment.ExitCode = 3; return; }
                string message = string.Join(" ", args, 1, args.Length - 1);
                Environment.ExitCode = EventBridge.TrySend(message) ? 0 : 2;
                return;
            }
            // 隔离测试目录下的自测不能被正在运行的生产桌宠互斥锁挡住，也不会接触生产数据。
            if (args != null && args.Contains("--stress-test")
                && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WORKMATE_TEST_DIR")))
            {
                Environment.ExitCode = StressTest.Run();
                return;
            }
            if (args != null && args.Contains("--self-test")
                && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WORKMATE_TEST_DIR")))
            {
                Environment.ExitCode = SelfTest.Run();
                return;
            }
            string testInstanceRoot = Environment.GetEnvironmentVariable("WORKMATE_TEST_DIR");
            string mutexName = string.IsNullOrWhiteSpace(testInstanceRoot)
                ? "Local\\WorkMatePro-SingleInstance-9C21A7"
                : "Local\\WorkMatePro-SingleInstance-" + EventBridge.PipeName.Replace('.', '-');
            bool created;
            using (Mutex mutex = new Mutex(true, mutexName, out created))
            {
                if (!created)
                {
                    if (args != null && args.Contains("--self-test")) { Environment.ExitCode = 4; return; }
                    string activation = EventBridge.ActivationMessageFor(args);
                    Environment.ExitCode = EventBridge.TrySendWithRetry(activation, 4, 120) ? 0 : 2;
                    return;
                }
                if (args != null && args.Contains("--self-test"))
                {
                    Environment.ExitCode = SelfTest.Run();
                    return;
                }
                WorkMateApp app = new WorkMateApp(args);
                app.Run();
                GC.KeepAlive(mutex);
            }
        }
    }
}
