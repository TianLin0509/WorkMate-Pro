using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WorkMatePro
{
    /// <summary>
    /// 状态调试 HUD：把“识别准不准”从感觉变成可测。
    /// 2Hz 刷新：当前状态/置信度/输入速率/系统信号/帧率/内存。
    /// 隐私口径也直接写在上面：只计数、不记键码、不记标题。
    /// </summary>
    public sealed class DebugHud : Window
    {
        private readonly TextBlock body;
        private readonly PetWindow pet;

        public DebugHud(WorkMateApp app, PetWindow petWindow)
        {
            pet = petWindow;
            Title = "WorkMate 状态调试";
            Width = 318;
            Height = 252;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            Topmost = true;
            WindowStartupLocation = WindowStartupLocation.Manual;
            ShowActivated = false;
            WindowPrivacy.Bind(this, delegate { return app.Store.Data.HideFromCaptureEnabled; }, true);

            body = new TextBlock
            {
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                Foreground = Theme.Brush("#D8DEE9"),
                Padding = new Thickness(14, 10, 14, 10),
                TextWrapping = TextWrapping.Wrap
            };
            Border shell = new Border
            {
                Background = Theme.Brush("#E61F2329"),
                CornerRadius = new CornerRadius(12),
                BorderBrush = Theme.Brush("#3B4252"),
                BorderThickness = new Thickness(1),
                Child = body
            };
            Content = shell;
            MouseLeftButtonDown += delegate { try { DragMove(); } catch { } };
            MouseRightButtonUp += delegate { Hide(); };
        }

        public void ShowNearPet()
        {
            Rect area = WindowPlacement.WorkAreaFor(pet);
            Left = area.Left + 16;
            Top = area.Top + 16;
            Show();
        }

        public void UpdateFrom(SignalSample sample, EngineOutput output, string fpsMode, bool presentationRetreat, string rawDetail, bool audioOk)
        {
            if (!IsVisible) return;
            long workingSet = 0;
            try { workingSet = Process.GetCurrentProcess().WorkingSet64 / (1024 * 1024); } catch { }
            body.Text =
                "状态 " + output.State + "  置信 " + output.Confidence.ToString("0.00") + "\n" +
                "情绪 arousal " + output.Arousal.ToString("0.00") + "  valence " + output.Valence.ToString("0.00") + "\n" +
                "键盘 " + sample.KeyPerMin.ToString("0") + "/min  滚轮 " + sample.WheelPerMin.ToString("0") + "/min\n" +
                "点击 " + sample.ClickPerMin.ToString("0") + "/min  移动 " + sample.MovePerMin.ToString("0") + "/min\n" +
                "空闲 " + sample.IdleSeconds + "s / 有意 " + sample.IdleIntentionalSeconds + "s  前台 " + sample.ForegroundCategory + "\n" +
                "麦克风 " + (sample.MicInUse ? "占用" : "-") + "  摄像头 " + (sample.CameraInUse ? "占用" : "-") +
                "  音频 " + (sample.AudioActive ? "播放" : "-") + " " + sample.AudioPeak.ToString("0.000") + "\n" +
                "RawInput " + rawDetail + "  音频探针 " + (audioOk ? "正常" : "不可用") + "\n" +
                "帧率 " + fpsMode + "  演示退避 " + (presentationRetreat ? "是" : "否") + "  内存 " + workingSet + "MB\n" +
                "口径：只计数不记键码 · 不记标题 · 不出本机\n" +
                "左键拖动 · 右键关闭";
        }
    }
}
