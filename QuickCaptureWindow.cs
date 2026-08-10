using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace WorkMatePro
{
    /// <summary>Alt+Q 同时承担“随手记”和“快速看”：输入永远在最上方，未完成记录在下方一眼可扫。</summary>
    public sealed class QuickCaptureWindow : Window
    {
        private readonly WorkMateApp app;
        private readonly Border shell;
        private TextBox input;
        private TextBlock placeholder;
        private Button shortButton;
        private Button longButton;
        private Button importantButton;
        private Button urgentButton;
        private string term = "short";
        private bool important;
        private bool urgent;

        public QuickCaptureWindow(WorkMateApp app)
        {
            this.app = app;
            Title = "WorkMate 快速记录与查看";
            Width = 720;
            Height = 525;
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
                CornerRadius = new CornerRadius(25),
                Padding = new Thickness(22, 19, 22, 19),
                Effect = Theme.Shadow(36, .24, 10)
            };
            Content = shell;
            Deactivated += delegate { if (IsVisible && input != null && input.Text.Trim().Length == 0) Hide(); };
            PreviewKeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Escape) { Hide(); e.Handled = true; }
            };
        }

        public void ShowCapture()
        {
            term = "short";
            important = false;
            urgent = false;
            if (app.Store.Data.MeetingRadarEnabled) app.MeetingRadar.PollIfDue(false);
            MeetingInfo meeting = app.MeetingRadar == null ? null : app.MeetingRadar.NextMeeting;
            Height = 500 + (app.Store.Data.FourQuadrantEnabled ? 32 : 0) + (meeting != null ? 72 : 0);
            BuildContent();
            input.Text = "";
            Rect area = WindowPlacement.WorkAreaFor(app.Pet);
            Left = area.Left + (area.Width - Width) / 2.0;
            Top = area.Top + Math.Max(24, area.Height * .07);
            Topmost = true;
            if (!IsVisible) Show();
            Activate();
            NativeMethods.ForceForeground(new WindowInteropHelper(this).Handle);
            input.Focus();
            Keyboard.Focus(input);
        }

        private void BuildContent()
        {
            StackPanel root = new StackPanel();
            root.Children.Add(BuildHeader());
            MeetingInfo meeting = app.MeetingRadar == null ? null : app.MeetingRadar.NextMeeting;
            if (meeting != null) root.Children.Add(BuildMeetingStrip(meeting));
            root.Children.Add(BuildCaptureRow());
            root.Children.Add(BuildOptions());
            root.Children.Add(BuildMemoPanel());
            shell.Child = root;
        }

        private UIElement BuildHeader()
        {
            Grid header = new Grid { Margin = new Thickness(0, 0, 0, 14) };
            header.ColumnDefinitions.Add(new ColumnDefinition());
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            StackPanel copy = new StackPanel { Orientation = Orientation.Horizontal };
            copy.Children.Add(Theme.Text("记一笔", 18, Theme.Ink, FontWeights.Bold));
            TextBlock key = Theme.Text(app.Pet.HotkeyLabel + "  ·  也可以只看不记", 11, Theme.Faint, FontWeights.Normal);
            key.Margin = new Thickness(10, 2, 0, 0);
            copy.Children.Add(key);
            header.Children.Add(copy);
            Button close = Theme.GhostButton("关闭");
            close.Padding = new Thickness(10, 5, 10, 5);
            close.Click += delegate { Hide(); };
            Grid.SetColumn(close, 1);
            header.Children.Add(close);
            return header;
        }

        private UIElement BuildMeetingStrip(MeetingInfo meeting)
        {
            Grid body = new Grid();
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(64) });
            body.ColumnDefinitions.Add(new ColumnDefinition());
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            StackPanel time = new StackPanel();
            time.Children.Add(Theme.Text(meeting.Start.ToString("HH:mm"), 16, Theme.Accent, FontWeights.Bold));
            time.Children.Add(Theme.Text(meeting.Start.Date == DateTime.Today ? "今天" : meeting.Start.ToString("M/d"), 10, Theme.Faint, FontWeights.Normal));
            body.Children.Add(time);
            StackPanel detail = new StackPanel();
            detail.Children.Add(Theme.Text(Formatters.Truncate(meeting.Subject, 44), 12.5, Theme.Ink, FontWeights.SemiBold));
            string second = (string.IsNullOrWhiteSpace(meeting.Location) ? "地点未提供" : meeting.Location) + "  ·  " + meeting.Attendees;
            detail.Children.Add(Theme.Text(Formatters.Truncate(second, 62), 10.5, Theme.Muted, FontWeights.Normal));
            Grid.SetColumn(detail, 1);
            body.Children.Add(detail);
            Border label = Theme.Pill("会议雷达", Theme.AccentSoft, Theme.Accent);
            Grid.SetColumn(label, 2);
            body.Children.Add(label);
            Border strip = new Border { Background = Theme.Brush("#FFF5F1"), BorderBrush = Theme.Brush("#F5D7CF"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(15), Padding = new Thickness(14, 10, 14, 10), Margin = new Thickness(0, 0, 0, 13), Child = body };
            return strip;
        }

        private UIElement BuildCaptureRow()
        {
            placeholder = Theme.Text("输入要记下的事情，按 Enter 保存", 15, Theme.Faint, FontWeights.Normal);
            placeholder.IsHitTestVisible = false;
            input = new TextBox { MaxLength = 300 };
            System.Windows.Automation.AutomationProperties.SetName(input, "快速记录内容");
            input.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { SaveMemo(); e.Handled = true; } };
            input.TextChanged += delegate { placeholder.Visibility = input.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed; };
            Grid capture = new Grid();
            capture.ColumnDefinitions.Add(new ColumnDefinition());
            capture.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            capture.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid layer = new Grid();
            layer.Children.Add(Theme.InputShell(input, 52));
            placeholder.Margin = new Thickness(17, 0, 0, 0);
            layer.Children.Add(placeholder);
            capture.Children.Add(layer);
            Button save = Theme.PrimaryButton("记下");
            save.Width = 84; save.Height = 52;
            save.Click += delegate { SaveMemo(); };
            Grid.SetColumn(save, 2);
            capture.Children.Add(save);
            return capture;
        }

        private UIElement BuildOptions()
        {
            Grid options = new Grid { Margin = new Thickness(0, 11, 0, 15) };
            options.ColumnDefinitions.Add(new ColumnDefinition());
            options.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            StackPanel choices = new StackPanel { Orientation = Orientation.Horizontal };
            choices.Children.Add(Theme.Text("时效", 11, Theme.Faint, FontWeights.Normal));
            shortButton = OptionButton("短期", true);
            shortButton.Margin = new Thickness(10, 0, 6, 0);
            shortButton.Click += delegate { SetTerm("short"); };
            choices.Children.Add(shortButton);
            longButton = OptionButton("长期", false);
            longButton.Click += delegate { SetTerm("long"); };
            choices.Children.Add(longButton);
            if (app.Store.Data.FourQuadrantEnabled)
            {
                importantButton = OptionButton("重要", false);
                importantButton.Margin = new Thickness(16, 0, 6, 0);
                importantButton.Click += delegate { important = !important; PaintOption(importantButton, important); };
                choices.Children.Add(importantButton);
                urgentButton = OptionButton("紧急", false);
                urgentButton.Click += delegate { urgent = !urgent; PaintOption(urgentButton, urgent); };
                choices.Children.Add(urgentButton);
            }
            options.Children.Add(choices);
            TextBlock privacy = Theme.Text("仅保存在本机", 11, Theme.Faint, FontWeights.Normal);
            Grid.SetColumn(privacy, 1);
            options.Children.Add(privacy);
            return options;
        }

        private UIElement BuildMemoPanel()
        {
            System.Collections.Generic.List<MemoItem> open = app.Store.Data.Memos.Where(delegate(MemoItem memo) { return !memo.IsDone; })
                .OrderBy(delegate(MemoItem memo) { return memo.Term == "short" ? 0 : 1; })
                .ThenByDescending(delegate(MemoItem memo) { return memo.CreatedAt; }).ToList();
            StackPanel panel = new StackPanel();
            Grid heading = new Grid { Margin = new Thickness(1, 0, 1, 9) };
            heading.ColumnDefinitions.Add(new ColumnDefinition());
            heading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            heading.Children.Add(Theme.Text("当前记录  " + open.Count, 12, Theme.Ink, FontWeights.Bold));
            Button all = Theme.GhostButton("打开全部");
            all.Padding = new Thickness(9, 5, 9, 5);
            all.Click += delegate { Hide(); app.OpenWorkbench("memos"); };
            Grid.SetColumn(all, 1);
            heading.Children.Add(all);
            panel.Children.Add(heading);

            StackPanel rows = new StackPanel();
            if (open.Count == 0)
            {
                Border empty = new Border { Background = Theme.SoftSurface, CornerRadius = new CornerRadius(14), Padding = new Thickness(16, 18, 16, 18) };
                TextBlock text = Theme.Text("目前没有未完成记录。", 12, Theme.Muted, FontWeights.Normal);
                text.TextAlignment = TextAlignment.Center;
                empty.Child = text;
                rows.Children.Add(empty);
            }
            foreach (MemoItem memo in open.Take(8)) rows.Children.Add(MemoRow(memo));
            ScrollViewer scroll = new ScrollViewer { Content = rows, Height = 195, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            panel.Children.Add(scroll);
            Border card = new Border { Background = Theme.Brush("#FCF9F6"), BorderBrush = Theme.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(17), Padding = new Thickness(13), Child = panel };
            return card;
        }

        private UIElement MemoRow(MemoItem memo)
        {
            Grid row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Button done = Theme.Button("○", Brushes.Transparent, Theme.SuccessSoft, Theme.Muted, 999);
            done.Width = 28; done.Height = 28; done.Padding = new Thickness(0); done.FontSize = 16;
            done.Click += delegate
            {
                app.Store.SetMemoDone(memo, true);
                app.NotifyPositiveFeedback();
                BuildContent();
                input.Focus();
            };
            row.Children.Add(done);
            TextBlock title = Theme.Text(memo.Text, 12, Theme.Ink, FontWeights.SemiBold);
            title.TextTrimming = TextTrimming.CharacterEllipsis;
            title.TextWrapping = TextWrapping.NoWrap;
            Grid.SetColumn(title, 1);
            row.Children.Add(title);
            Border termPill = Theme.Pill(memo.Term == "long" ? "长期" : "短期", memo.Term == "long" ? Theme.SoftSurface : Theme.AccentSoft, memo.Term == "long" ? Theme.Muted : Theme.Accent);
            Grid.SetColumn(termPill, 2);
            row.Children.Add(termPill);
            Border holder = new Border { Padding = new Thickness(2, 7, 2, 7), BorderBrush = Theme.Line, BorderThickness = new Thickness(0, 0, 0, 1), Child = row };
            return holder;
        }

        private Button OptionButton(string text, bool selected)
        {
            Button button = Theme.Button(text, selected ? Theme.AccentSoft : Theme.SoftSurface, selected ? Theme.AccentSoft : Theme.Brush("#EEE6E0"), selected ? Theme.Accent : Theme.Muted, 999);
            button.Padding = new Thickness(11, 5, 11, 5);
            button.FontSize = 11;
            return button;
        }

        private void PaintOption(Button button, bool selected)
        {
            button.Background = selected ? Theme.AccentSoft : Theme.SoftSurface;
            button.Foreground = selected ? Theme.Accent : Theme.Muted;
        }

        private void SetTerm(string value)
        {
            term = value;
            PaintOption(shortButton, term == "short");
            PaintOption(longButton, term == "long");
        }

        private void SaveMemo()
        {
            MemoItem memo = app.Store.AddMemo(input.Text, term, important, urgent);
            if (memo == null) { input.Focus(); return; }
            input.Text = "";
            Hide();
            app.RefreshOpenWindows();
            app.Pet.Celebrate("已经记下，稍后再处理也没关系");
        }
    }
}
