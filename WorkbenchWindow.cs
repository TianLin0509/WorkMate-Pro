using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace WorkMatePro
{
    public sealed class WorkbenchWindow : Window
    {
        private readonly WorkMateApp app;
        private readonly Grid pageHost;
        private readonly StackPanel sidebar;
        private Button memoNav;
        private Button todayNav;
        private Button capabilitiesNav;
        private Button settingsNav;
        private string currentPage = "memos";
        private bool showCompleted;
        private DateTime clearStatsArmedUntil = DateTime.MinValue;
        private readonly System.Windows.Threading.DispatcherTimer foregroundLease;
        private readonly List<string> customPetPhotoPaths = new List<string>();
        private string customPetProjectPath = "";

        public WorkbenchWindow(WorkMateApp app)
        {
            this.app = app;
            foregroundLease = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
            foregroundLease.Tick += delegate
            {
                foregroundLease.Stop();
                Topmost = false;
            };
            Title = "WorkMate 工作伴侣";
            Width = 980;
            Height = 700;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = true;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            WindowPrivacy.Bind(this, delegate { return app.Store.Data.HideFromCaptureEnabled; }, false);

            Border shell = new Border
            {
                Background = Theme.Surface,
                BorderBrush = Theme.Line,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(25),
                Effect = Theme.Shadow(36, 0.18, 8),
                Margin = new Thickness(16)
            };
            Grid shellGrid = new Grid();
            shellGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(52) });
            shellGrid.RowDefinitions.Add(new RowDefinition());
            shell.Child = shellGrid;

            Grid titleBar = BuildTitleBar();
            shellGrid.Children.Add(titleBar);

            Grid body = new Grid();
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(216) });
            body.ColumnDefinitions.Add(new ColumnDefinition());
            Grid.SetRow(body, 1);
            shellGrid.Children.Add(body);

            sidebar = new StackPanel { Margin = new Thickness(18, 20, 18, 18) };
            Border sidebarSurface = new Border
            {
                Background = Theme.Brush("#F7F1EC"),
                CornerRadius = new CornerRadius(0, 0, 0, 24),
                Child = sidebar
            };
            body.Children.Add(sidebarSurface);

            pageHost = new Grid { Background = Theme.Canvas, Margin = new Thickness(0) };
            Grid.SetColumn(pageHost, 1);
            body.Children.Add(pageHost);
            Content = shell;

            PreviewKeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Escape) Hide();
            };
            Closing += delegate(object sender, System.ComponentModel.CancelEventArgs e)
            {
                if (!app.IsExiting) { e.Cancel = true; Hide(); }
            };
            // 页面由 ShowPage 按目标页一次性构建。构造阶段不重复绘制，
            // 避免首次从桌宠打开工作台时完整创建两遍视觉树。
        }

        private Grid BuildTitleBar()
        {
            Grid bar = new Grid { Background = Theme.Surface };
            bar.ColumnDefinitions.Add(new ColumnDefinition());
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            bar.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                if (e.ChangedButton == MouseButton.Left) try { DragMove(); } catch { }
            };
            StackPanel title = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(22, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            title.Children.Add(Theme.Text("WORKMATE", 11, Theme.Accent, FontWeights.Bold));
            TextBlock caption = Theme.Text("工作伴侣", 12, Theme.Muted, FontWeights.Normal);
            caption.Margin = new Thickness(10, 0, 0, 0);
            title.Children.Add(caption);
            bar.Children.Add(title);

            StackPanel controls = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 7, 9, 7) };
            Button minimize = Theme.GhostButton("—");
            minimize.Width = 42;
            minimize.Padding = new Thickness(0);
            minimize.Click += delegate { WindowState = WindowState.Minimized; };
            controls.Children.Add(minimize);
            Button close = Theme.GhostButton("×");
            close.Width = 42;
            close.Padding = new Thickness(0);
            close.FontSize = 18;
            close.Click += delegate { Hide(); };
            controls.Children.Add(close);
            Grid.SetColumn(controls, 1);
            bar.Children.Add(controls);
            return bar;
        }

        private void BuildSidebar()
        {
            sidebar.Children.Clear();
            PetDefinition pet = PetCatalog.Find(app.Store.Data.PetId);
            Image portrait = PetAssets.View(pet.Id, "idle", 118);
            portrait.HorizontalAlignment = HorizontalAlignment.Center;
            sidebar.Children.Add(portrait);
            TextBlock name = Theme.Text(pet.Name, 18, Theme.Ink, FontWeights.Bold);
            name.TextAlignment = TextAlignment.Center;
            name.Margin = new Thickness(0, 2, 0, 2);
            sidebar.Children.Add(name);
            TextBlock species = Theme.Text(pet.Species + "  ·  Lv." + app.Store.Level, 11, Theme.Muted, FontWeights.Normal);
            species.TextAlignment = TextAlignment.Center;
            sidebar.Children.Add(species);
            Grid progress = Theme.Progress(Growth.LevelProgress(app.Store.Data.CompanionValue), Theme.Brush(pet.Accent), 7);
            progress.Margin = new Thickness(12, 13, 12, 22);
            sidebar.Children.Add(progress);

            memoNav = NavButton("备忘录", "memos");
            todayNav = NavButton("今日统计", "today");
            capabilitiesNav = NavButton("能力中心", "capabilities");
            settingsNav = NavButton("设置", "settings");
            sidebar.Children.Add(memoNav);
            sidebar.Children.Add(todayNav);
            sidebar.Children.Add(capabilitiesNav);
            sidebar.Children.Add(settingsNav);

            Border spacer = new Border { Height = 72 };
            sidebar.Children.Add(spacer);
            Border privacy = new Border
            {
                Background = Theme.Brush("#EAF2EF"),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(13, 11, 13, 11)
            };
            StackPanel privacyText = new StackPanel();
            privacyText.Children.Add(Theme.Text("本地隐私", 11, Theme.Success, FontWeights.Bold));
            TextBlock note = Theme.Text("只统计应用类别，不记录窗口标题", 10.5, Theme.Muted, FontWeights.Normal);
            note.Margin = new Thickness(0, 4, 0, 0);
            privacyText.Children.Add(note);
            privacy.Child = privacyText;
            sidebar.Children.Add(privacy);
            PaintNavigation();
        }

        private Button NavButton(string title, string page)
        {
            Button button = Theme.Button(title, Brushes.Transparent, Theme.Brush("#EFE6DF"), Theme.Muted, 13);
            button.HorizontalContentAlignment = HorizontalAlignment.Left;
            button.Padding = new Thickness(17, 12, 17, 12);
            button.Margin = new Thickness(0, 2, 0, 2);
            button.Tag = page;
            button.Click += delegate { ShowPage(page); };
            return button;
        }

        private void PaintNavigation()
        {
            if (memoNav == null) return;
            Button[] buttons = new[] { memoNav, todayNav, capabilitiesNav, settingsNav };
            foreach (Button button in buttons)
            {
                bool active = (string)button.Tag == currentPage;
                button.Background = active ? Theme.Surface : Brushes.Transparent;
                button.Foreground = active ? Theme.Ink : Theme.Muted;
                button.FontWeight = active ? FontWeights.Bold : FontWeights.SemiBold;
            }
        }

        public void ShowPage(string page)
        {
            if (page == "memos" || page == "today" || page == "capabilities" || page == "settings") currentPage = page;
            BuildSidebar();
            BuildCurrentPage();
            // 显式打开/二次启动时先租用 900ms 置顶，绕过 Windows 前台锁导致的“已打开但躲在后面”。
            Topmost = true;
            if (!IsVisible) Show();
            if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
            Activate();
            NativeMethods.ForceForeground(new WindowInteropHelper(this).Handle);
            foregroundLease.Stop();
            foregroundLease.Start();
        }

        public void RefreshCurrentPage()
        {
            if (!IsVisible) return;
            BuildSidebar();
            BuildCurrentPage();
        }

        private void BuildCurrentPage()
        {
            PaintNavigation();
            pageHost.Children.Clear();
            if (currentPage == "today") pageHost.Children.Add(BuildTodayPage());
            else if (currentPage == "capabilities") pageHost.Children.Add(BuildCapabilitiesPage());
            else if (currentPage == "settings") pageHost.Children.Add(BuildSettingsPage());
            else pageHost.Children.Add(BuildMemoPage());
        }

        private Grid PageRoot()
        {
            return new Grid { Margin = new Thickness(34, 26, 34, 28) };
        }

        private StackPanel PageHeading(string title, string subtitle)
        {
            StackPanel heading = new StackPanel();
            heading.Children.Add(Theme.Text(title, 27, Theme.Ink, FontWeights.Bold));
            TextBlock sub = Theme.Text(subtitle, 12, Theme.Muted, FontWeights.Normal);
            sub.Margin = new Thickness(0, 6, 0, 0);
            heading.Children.Add(sub);
            return heading;
        }

        private UIElement BuildMemoPage()
        {
            Grid page = PageRoot();
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(20) });
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(18) });
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
            page.RowDefinitions.Add(new RowDefinition());

            int openCount = app.Store.Data.Memos.Count(delegate(MemoItem memo) { return !memo.IsDone; });
            StackPanel heading = PageHeading("备忘录", "随手记下，完成后勾选。当前有 " + openCount + " 条进行中的记录。 ");
            page.Children.Add(heading);

            UIElement composer = BuildMemoComposer();
            Grid.SetRow(composer, 2);
            page.Children.Add(composer);

            Grid filters = new Grid();
            filters.ColumnDefinitions.Add(new ColumnDefinition());
            filters.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            StackPanel tabs = new StackPanel { Orientation = Orientation.Horizontal };
            Button active = SmallTab("进行中", !showCompleted);
            active.Click += delegate { showCompleted = false; BuildCurrentPage(); };
            tabs.Children.Add(active);
            Button done = SmallTab("已完成", showCompleted);
            done.Margin = new Thickness(8, 0, 0, 0);
            done.Click += delegate { showCompleted = true; BuildCurrentPage(); };
            tabs.Children.Add(done);
            filters.Children.Add(tabs);
            TextBlock mode = Theme.Text(app.Store.Data.FourQuadrantEnabled ? "四象限模式已开启" : "基础模式", 11, Theme.Faint, FontWeights.Normal);
            Grid.SetColumn(mode, 1);
            filters.Children.Add(mode);
            Grid.SetRow(filters, 4);
            page.Children.Add(filters);

            ScrollViewer scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Hidden, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            StackPanel list = new StackPanel { Margin = new Thickness(0, 0, 4, 0) };
            IEnumerable<MemoItem> memos = app.Store.Data.Memos.Where(delegate(MemoItem memo) { return memo.IsDone == showCompleted; });
            if (showCompleted) memos = memos.OrderByDescending(delegate(MemoItem memo) { return memo.DoneAt; }).Take(80);
            else memos = memos.OrderBy(delegate(MemoItem memo) { return memo.Term == "short" ? 0 : 1; }).ThenByDescending(delegate(MemoItem memo) { return memo.CreatedAt; });
            int count = 0;
            foreach (MemoItem memo in memos)
            {
                list.Children.Add(BuildMemoRow(memo));
                count++;
            }
            if (count == 0) list.Children.Add(BuildMemoEmptyState());
            scroll.Content = list;
            Grid.SetRow(scroll, 6);
            page.Children.Add(scroll);
            return page;
        }

        private UIElement BuildMemoComposer()
        {
            StackPanel stack = new StackPanel();
            TextBlock label = Theme.Text("新增记录", 11, Theme.Muted, FontWeights.SemiBold);
            label.Margin = new Thickness(1, 0, 0, 9);
            stack.Children.Add(label);

            TextBox input = new TextBox { MaxLength = 300 };
            System.Windows.Automation.AutomationProperties.SetName(input, "新增备忘录内容");
            Border inputShell = Theme.InputShell(input, 52);
            string selectedTerm = "short";
            bool important = false;
            bool urgent = false;

            Grid row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.Children.Add(inputShell);
            Button add = Theme.PrimaryButton("记下");
            add.Width = 82;
            add.Height = 52;
            Grid.SetColumn(add, 2);
            row.Children.Add(add);
            stack.Children.Add(row);

            StackPanel options = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
            options.Children.Add(Theme.Text("时效", 11, Theme.Faint, FontWeights.Normal));
            Button shortTerm = ChoiceButton("短期", true);
            shortTerm.Margin = new Thickness(10, 0, 6, 0);
            Button longTerm = ChoiceButton("长期", false);
            options.Children.Add(shortTerm);
            options.Children.Add(longTerm);
            Button importantButton = null;
            Button urgentButton = null;
            if (app.Store.Data.FourQuadrantEnabled)
            {
                TextBlock quadrantLabel = Theme.Text("四象限", 11, Theme.Faint, FontWeights.Normal);
                quadrantLabel.Margin = new Thickness(22, 0, 0, 0);
                options.Children.Add(quadrantLabel);
                importantButton = ChoiceButton("重要", false);
                importantButton.Margin = new Thickness(10, 0, 6, 0);
                urgentButton = ChoiceButton("紧急", false);
                options.Children.Add(importantButton);
                options.Children.Add(urgentButton);
            }
            stack.Children.Add(options);

            shortTerm.Click += delegate { selectedTerm = "short"; PaintChoice(shortTerm, true); PaintChoice(longTerm, false); };
            longTerm.Click += delegate { selectedTerm = "long"; PaintChoice(shortTerm, false); PaintChoice(longTerm, true); };
            if (importantButton != null)
            {
                importantButton.Click += delegate { important = !important; PaintChoice(importantButton, important); };
                urgentButton.Click += delegate { urgent = !urgent; PaintChoice(urgentButton, urgent); };
            }

            Action save = delegate
            {
                MemoItem memo = app.Store.AddMemo(input.Text, selectedTerm, important, urgent);
                if (memo == null) { input.Focus(); return; }
                app.Pet.Celebrate("已经记下");
                BuildCurrentPage();
            };
            add.Click += delegate { save(); };
            input.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Enter) { save(); e.Handled = true; }
            };

            Border card = Theme.Card(stack, 18, new Thickness(18, 15, 18, 15));
            card.Background = Theme.Surface;
            return card;
        }

        private Button ChoiceButton(string text, bool selected)
        {
            Button button = Theme.Button(text, selected ? Theme.AccentSoft : Theme.SoftSurface, selected ? Theme.AccentSoft : Theme.Brush("#EBE3DD"), selected ? Theme.Accent : Theme.Muted, 999);
            button.Padding = new Thickness(11, 5, 11, 5);
            button.FontSize = 11;
            return button;
        }

        private void PaintChoice(Button button, bool selected)
        {
            button.Background = selected ? Theme.AccentSoft : Theme.SoftSurface;
            button.Foreground = selected ? Theme.Accent : Theme.Muted;
        }

        private Button SmallTab(string text, bool active)
        {
            Button button = Theme.Button(text, active ? Theme.Ink : Theme.SoftSurface, active ? Theme.Ink : Theme.Brush("#EAE2DC"), active ? Brushes.White : Theme.Muted, 999);
            button.Padding = new Thickness(15, 7, 15, 7);
            button.FontSize = 11.5;
            return button;
        }

        private UIElement BuildMemoRow(MemoItem memo)
        {
            Grid row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Button check = Theme.Button(memo.IsDone ? "✓" : "", memo.IsDone ? Theme.Success : Theme.Surface, memo.IsDone ? Theme.Success : Theme.AccentSoft, memo.IsDone ? Brushes.White : Theme.Accent, 999);
            check.Width = 27;
            check.Height = 27;
            check.Padding = new Thickness(0);
            check.BorderBrush = memo.IsDone ? Theme.Success : Theme.Line;
            check.BorderThickness = new Thickness(1.5);
            check.VerticalAlignment = VerticalAlignment.Top;
            check.Margin = new Thickness(0, 1, 0, 0);
            System.Windows.Automation.AutomationProperties.SetName(check, (memo.IsDone ? "恢复记录：" : "完成记录：") + memo.Text);
            check.Click += delegate
            {
                bool done = !memo.IsDone;
                bool dailyPriority = done && app.Store.IsDailyPriority(memo);
                app.Store.SetMemoDone(memo, done);
                if (done) { app.NotifyPositiveFeedback(); app.Pet.Celebrate(dailyPriority ? "今日一件事完成，陪伴值 +6" : "完成一件事，陪伴值 +4"); }
                BuildCurrentPage();
            };
            row.Children.Add(check);

            StackPanel body = new StackPanel();
            TextBlock title = Theme.Text(memo.Text, 14, memo.IsDone ? Theme.Faint : Theme.Ink, memo.IsDone ? FontWeights.Normal : FontWeights.SemiBold);
            if (memo.IsDone) title.TextDecorations = TextDecorations.Strikethrough;
            body.Children.Add(title);
            StackPanel meta = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 9, 0, 0) };
            meta.Children.Add(Theme.Pill(memo.Term == "long" ? "长期" : "短期", memo.Term == "long" ? Theme.Brush("#EEE9F8") : Theme.WarningSoft, memo.Term == "long" ? Theme.Brush("#7762A6") : Theme.Warning));
            if (app.Store.Data.FourQuadrantEnabled)
            {
                string quadrant = memo.Important ? (memo.Urgent ? "重要 · 紧急" : "重要 · 不紧急") : (memo.Urgent ? "不重要 · 紧急" : "不重要 · 不紧急");
                Border quadrantPill = Theme.Pill(quadrant, Theme.SoftSurface, Theme.Muted);
                quadrantPill.Margin = new Thickness(7, 0, 0, 0);
                meta.Children.Add(quadrantPill);
            }
            TextBlock date = Theme.Text(Formatters.DateLabel(memo.IsDone ? memo.DoneAt : memo.CreatedAt), 10.5, Theme.Faint, FontWeights.Normal);
            date.Margin = new Thickness(10, 0, 0, 0);
            meta.Children.Add(date);
            bool isAnchor = app.Store.Data.AnchorMemoId == memo.Id;
            if (isAnchor)
            {
                Border anchorPill = Theme.Pill(app.Store.IsDailyPriority(memo) ? "今日一件事" : "在做", Theme.AccentSoft, Theme.Accent);
                anchorPill.Margin = new Thickness(7, 0, 0, 0);
                meta.Children.Add(anchorPill);
            }
            body.Children.Add(meta);
            Grid.SetColumn(body, 1);
            row.Children.Add(body);

            StackPanel actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top };
            if (!memo.IsDone)
            {
                Button pin = Theme.GhostButton(isAnchor ? "取消" : "今日一件事");
                pin.Padding = new Thickness(8, 6, 8, 6);
                pin.FontSize = 11;
                if (isAnchor) pin.Foreground = Theme.Accent;
                System.Windows.Automation.AutomationProperties.SetName(pin, (isAnchor ? "取消今日一件事：" : "设为今日一件事：") + memo.Text);
                pin.Click += delegate
                {
                    app.Store.SetAnchor(isAnchor ? "" : memo.Id);
                    app.Pet.RefreshPet();
                    BuildCurrentPage();
                };
                actions.Children.Add(pin);
            }
            Button delete = Theme.GhostButton("删除");
            delete.Padding = new Thickness(10, 6, 10, 6);
            delete.FontSize = 11;
            delete.Click += delegate { app.Store.DeleteMemo(memo); BuildCurrentPage(); };
            actions.Children.Add(delete);
            Grid.SetColumn(actions, 2);
            row.Children.Add(actions);

            Border card = Theme.Card(row, 16, new Thickness(15, 14, 15, 14));
            card.Margin = new Thickness(0, 0, 0, 9);
            card.Background = memo.IsDone ? Theme.Brush("#F8F5F2") : Theme.Surface;
            if (isAnchor)
            {
                card.BorderBrush = Theme.Accent;
                card.BorderThickness = new Thickness(1.5);
            }
            return card;
        }

        private UIElement BuildMemoEmptyState()
        {
            StackPanel empty = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 38, 0, 0) };
            Image image = PetAssets.View(app.Store.Data.PetId, showCompleted ? "happy" : "idle", 112);
            image.HorizontalAlignment = HorizontalAlignment.Center;
            empty.Children.Add(image);
            TextBlock title = Theme.Text(showCompleted ? "还没有已完成记录" : "现在没有待办的记录", 15, Theme.Ink, FontWeights.Bold);
            title.TextAlignment = TextAlignment.Center;
            empty.Children.Add(title);
            TextBlock note = Theme.Text(showCompleted ? "完成第一件事后，它会出现在这里。" : "有事情时随手记下，没事情时就安心工作。", 11.5, Theme.Muted, FontWeights.Normal);
            note.TextAlignment = TextAlignment.Center;
            note.Margin = new Thickness(0, 6, 0, 0);
            empty.Children.Add(note);
            return empty;
        }

        private UIElement BuildTodayPage()
        {
            Grid page = PageRoot();
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(22) });
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(18) });
            page.RowDefinitions.Add(new RowDefinition());

            StackPanel heading = PageHeading("今日统计", "只看时间花在哪一类应用上，不读取窗口标题和文档内容。");
            page.Children.Add(heading);

            Grid metrics = new Grid();
            metrics.ColumnDefinitions.Add(new ColumnDefinition());
            metrics.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            metrics.ColumnDefinitions.Add(new ColumnDefinition());
            metrics.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            metrics.ColumnDefinitions.Add(new ColumnDefinition());
            metrics.Children.Add(MetricCard("活跃工作", Formatters.Duration(app.Store.TodayActiveSeconds), "空闲超过 5 分钟不计入"));
            UIElement completed = MetricCard("完成记录", app.Store.TodayCompletedCount + " 条", "完成一条，陪伴值 +4");
            Grid.SetColumn(completed, 2);
            metrics.Children.Add(completed);
            UIElement value = MetricCard("陪伴值", app.Store.Data.CompanionValue.ToString(), "工作奖励每日有上限");
            Grid.SetColumn(value, 4);
            metrics.Children.Add(value);
            Grid.SetRow(metrics, 2);
            page.Children.Add(metrics);

            Grid content = new Grid();
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.25, GridUnitType.Star) });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
            content.ColumnDefinitions.Add(new ColumnDefinition());
            UIElement bars = BuildCategoryCard();
            content.Children.Add(bars);
            UIElement summary = BuildSummaryCard();
            Grid.SetColumn(summary, 2);
            content.Children.Add(summary);
            Grid.SetRow(content, 4);
            page.Children.Add(content);
            return page;
        }

        private UIElement MetricCard(string label, string value, string note)
        {
            StackPanel stack = new StackPanel();
            stack.Children.Add(Theme.Text(label, 11, Theme.Muted, FontWeights.SemiBold));
            TextBlock main = Theme.Text(value, 22, Theme.Ink, FontWeights.Bold);
            main.Margin = new Thickness(0, 9, 0, 5);
            stack.Children.Add(main);
            stack.Children.Add(Theme.Text(note, 10.5, Theme.Faint, FontWeights.Normal));
            Border card = Theme.Card(stack, 17, new Thickness(17, 15, 17, 15));
            card.MinHeight = 104;
            return card;
        }

        private UIElement BuildCategoryCard()
        {
            StackPanel stack = new StackPanel();
            Grid heading = new Grid { Margin = new Thickness(0, 0, 0, 15) };
            heading.ColumnDefinitions.Add(new ColumnDefinition());
            heading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            heading.Children.Add(Theme.Text("时间分布", 15, Theme.Ink, FontWeights.Bold));
            Button toggle = Theme.Button(app.Store.Data.TrackEnabled ? "统计中" : "已暂停", app.Store.Data.TrackEnabled ? Theme.SuccessSoft : Theme.SoftSurface, app.Store.Data.TrackEnabled ? Theme.SuccessSoft : Theme.Brush("#E9E1DB"), app.Store.Data.TrackEnabled ? Theme.Success : Theme.Muted, 999);
            toggle.Padding = new Thickness(11, 5, 11, 5);
            toggle.FontSize = 10.5;
            toggle.Click += delegate
            {
                app.Store.Data.TrackEnabled = !app.Store.Data.TrackEnabled;
                app.Store.Save();
                app.Pet.RefreshPet();
                BuildCurrentPage();
            };
            Grid.SetColumn(toggle, 1);
            heading.Children.Add(toggle);
            stack.Children.Add(heading);

            Dictionary<string, int> stats = app.Store.TodayStats();
            int total = Math.Max(1, stats.Values.Sum());
            List<KeyValuePair<string, int>> ordered = stats.OrderByDescending(delegate(KeyValuePair<string, int> item) { return item.Value; }).ToList();
            if (ordered.Count == 0)
            {
                TextBlock empty = Theme.Text("今天还没有统计数据。开始工作后，这里会按应用类别显示时间。", 12, Theme.Muted, FontWeights.Normal);
                empty.Margin = new Thickness(0, 18, 0, 0);
                stack.Children.Add(empty);
            }
            else
            {
                string[] colors = new[] { "#E95D4F", "#D38C45", "#6F8F7E", "#776DA8", "#4B8EA0", "#9A7B69", "#B3A89F" };
                for (int index = 0; index < ordered.Count; index++)
                {
                    KeyValuePair<string, int> item = ordered[index];
                    stack.Children.Add(CategoryRow(item.Key, item.Value, item.Value / (double)total, Theme.Brush(colors[index % colors.Length])));
                }
            }
            Border heatDivider = new Border { Height = 1, Background = Theme.Line, Margin = new Thickness(0, 5, 0, 14) };
            stack.Children.Add(heatDivider);
            stack.Children.Add(BuildRhythmHeatmap());
            Border card = Theme.Card(stack, 18, new Thickness(19));
            return card;
        }

        private UIElement BuildRhythmHeatmap()
        {
            StackPanel block = new StackPanel();
            Grid title = new Grid { Margin = new Thickness(0, 0, 0, 9) };
            title.ColumnDefinitions.Add(new ColumnDefinition());
            title.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            title.Children.Add(Theme.Text("近 7 天工作节奏", 12.5, Theme.Ink, FontWeights.Bold));
            TextBlock legend = Theme.Text("颜色越深，专注越久", 9.5, Theme.Faint, FontWeights.Normal);
            Grid.SetColumn(legend, 1);
            title.Children.Add(legend);
            block.Children.Add(title);

            const int firstHour = 8;
            const int hourCount = 14;
            Grid grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
            for (int h = 0; h < hourCount; h++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(16) });
            for (int d = 0; d < 7; d++) grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(18) });
            for (int h = 0; h < hourCount; h++)
            {
                if (h % 3 != 0) continue;
                TextBlock hour = Theme.Text((firstHour + h).ToString(), 8.5, Theme.Faint, FontWeights.Normal);
                hour.TextAlignment = TextAlignment.Center;
                Grid.SetColumn(hour, h + 1);
                grid.Children.Add(hour);
            }
            int max = 1;
            for (int d = 0; d < 7; d++)
            {
                string key = DateTime.Today.AddDays(d - 6).ToString("yyyy-MM-dd");
                Dictionary<string, int> day;
                if (!app.Store.Data.HourlyActiveSeconds.TryGetValue(key, out day)) continue;
                foreach (int value in day.Values) max = Math.Max(max, value);
            }
            string[] week = { "日", "一", "二", "三", "四", "五", "六" };
            for (int d = 0; d < 7; d++)
            {
                DateTime date = DateTime.Today.AddDays(d - 6);
                TextBlock dayLabel = Theme.Text(week[(int)date.DayOfWeek], 9.5, date == DateTime.Today ? Theme.Accent : Theme.Faint, date == DateTime.Today ? FontWeights.Bold : FontWeights.Normal);
                Grid.SetRow(dayLabel, d + 1);
                grid.Children.Add(dayLabel);
                Dictionary<string, int> day;
                app.Store.Data.HourlyActiveSeconds.TryGetValue(date.ToString("yyyy-MM-dd"), out day);
                for (int h = 0; h < hourCount; h++)
                {
                    int seconds = 0;
                    if (day != null) day.TryGetValue((firstHour + h).ToString("00"), out seconds);
                    double ratio = Math.Min(1, seconds / (double)max);
                    byte alpha = (byte)(28 + ratio * 205);
                    SolidColorBrush fill = new SolidColorBrush(Color.FromArgb(alpha, 233, 93, 79));
                    fill.Freeze();
                    Border cell = new Border { Background = fill, CornerRadius = new CornerRadius(3), Margin = new Thickness(1.5, 2, 1.5, 2), ToolTip = date.ToString("M/d") + " " + (firstHour + h) + ":00  " + Formatters.Duration(seconds) };
                    Grid.SetRow(cell, d + 1);
                    Grid.SetColumn(cell, h + 1);
                    grid.Children.Add(cell);
                }
            }
            block.Children.Add(grid);
            TextBlock note = Theme.Text("从 v1.7 起按小时累计；不记录窗口标题或文件名。", 9.5, Theme.Faint, FontWeights.Normal);
            note.Margin = new Thickness(0, 7, 0, 0);
            block.Children.Add(note);
            return block;
        }

        private UIElement CategoryRow(string name, int seconds, double ratio, Brush accent)
        {
            StackPanel row = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
            Grid labels = new Grid();
            labels.ColumnDefinitions.Add(new ColumnDefinition());
            labels.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            labels.Children.Add(Theme.Text(name, 11.5, Theme.Ink, FontWeights.SemiBold));
            TextBlock duration = Theme.Text(Formatters.Duration(seconds), 10.5, Theme.Muted, FontWeights.Normal);
            Grid.SetColumn(duration, 1);
            labels.Children.Add(duration);
            row.Children.Add(labels);
            Grid bar = Theme.Progress(ratio, accent, 8);
            bar.Margin = new Thickness(0, 7, 0, 0);
            row.Children.Add(bar);
            return row;
        }

        private UIElement BuildSummaryCard()
        {
            StackPanel stack = new StackPanel();
            stack.Children.Add(Theme.Text("今日小结", 15, Theme.Ink, FontWeights.Bold));
            TextBlock summary = Theme.Text(BuildSummaryText(), 11.5, Theme.Muted, FontWeights.Normal);
            summary.LineHeight = 22;
            summary.Margin = new Thickness(0, 14, 0, 17);
            stack.Children.Add(summary);
            InterruptionDay interruptions = app.Store.TodayInterruptions();
            Border interruptCard = new Border { Background = interruptions.Count > 4 ? Theme.WarningSoft : Theme.SoftSurface, CornerRadius = new CornerRadius(13), Padding = new Thickness(12, 9, 12, 9), Margin = new Thickness(0, 0, 0, 12) };
            StackPanel interruptText = new StackPanel();
            interruptText.Children.Add(Theme.Text("有效打断 " + interruptions.Count + " 次  ·  " + Formatters.Duration(interruptions.TotalSeconds), 11, interruptions.Count > 4 ? Theme.Warning : Theme.Ink, FontWeights.Bold));
            interruptText.Children.Add(Theme.Text("会议 " + interruptions.MeetingCount + " · 沟通 " + interruptions.CommunicationCount + " · 离开 " + interruptions.AwayCount + "；仅统计专注 90 秒后、持续 45 秒并返回工作的中断。", 9.5, Theme.Muted, FontWeights.Normal));
            interruptCard.Child = interruptText;
            stack.Children.Add(interruptCard);
            Button copy = Theme.PrimaryButton("复制今日小结");
            copy.HorizontalAlignment = HorizontalAlignment.Stretch;
            copy.Click += delegate
            {
                try { Clipboard.SetText(BuildSummaryText()); copy.Content = "已复制"; } catch { copy.Content = "复制失败"; }
            };
            stack.Children.Add(copy);
            Button copyWeek = Theme.SecondaryButton("复制本周素材");
            copyWeek.HorizontalAlignment = HorizontalAlignment.Stretch;
            copyWeek.Margin = new Thickness(0, 8, 0, 0);
            System.Windows.Automation.AutomationProperties.SetName(copyWeek, "复制本周素材");
            copyWeek.Click += delegate
            {
                try { Clipboard.SetText(WeeklyReport.BuildText(app.Store, DateTime.Now)); copyWeek.Content = "已复制"; } catch { copyWeek.Content = "复制失败"; }
            };
            stack.Children.Add(copyWeek);
            Border privacy = new Border { Background = Theme.SuccessSoft, CornerRadius = new CornerRadius(13), Padding = new Thickness(12), Margin = new Thickness(0, 13, 0, 0) };
            privacy.Child = Theme.Text("数据只写入本机 AppData，随时可以暂停或清空。", 10.5, Theme.Success, FontWeights.SemiBold);
            stack.Children.Add(privacy);
            Border card = Theme.Card(stack, 18, new Thickness(19));
            return card;
        }

        private string BuildSummaryText()
        {
            Dictionary<string, int> stats = app.Store.TodayStats();
            List<KeyValuePair<string, int>> top = stats.OrderByDescending(delegate(KeyValuePair<string, int> item) { return item.Value; }).Take(3).ToList();
            List<MemoItem> done = app.Store.Data.Memos.Where(delegate(MemoItem memo) { return memo.IsDone && !string.IsNullOrEmpty(memo.DoneAt) && memo.DoneAt.StartsWith(DateTime.Today.ToString("yyyy-MM-dd")); }).ToList();
            List<string> lines = new List<string>();
            lines.Add(DateTime.Today.ToString("M 月 d 日") + " · 今日小结");
            lines.Add("活跃工作：" + Formatters.Duration(app.Store.TodayActiveSeconds));
            if (top.Count > 0) lines.Add("主要投入：" + string.Join("、", top.Select(delegate(KeyValuePair<string, int> item) { return item.Key + " " + Formatters.Duration(item.Value); }).ToArray()));
            else lines.Add("主要投入：暂无统计");
            lines.Add("已完成：" + done.Count + " 条记录");
            foreach (MemoItem memo in done.Take(5)) lines.Add("  · " + memo.Text);
            InterruptionDay interruptions = app.Store.TodayInterruptions();
            lines.Add("有效打断：" + interruptions.Count + " 次（会议 " + interruptions.MeetingCount + " / 沟通 " + interruptions.CommunicationCount + " / 离开 " + interruptions.AwayCount + "）");
            lines.Add("陪伴值：" + app.Store.Data.CompanionValue + " · Lv." + app.Store.Level);
            return string.Join(Environment.NewLine, lines.ToArray());
        }

        private UIElement BuildCapabilitiesPage()
        {
            PetDefinition abilityPet = PetCatalog.Find(app.Store.Data.PetId);
            string abilityPetName = abilityPet == null ? "伙伴" : abilityPet.Name;
            Grid page = PageRoot();
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(18) });
            page.RowDefinitions.Add(new RowDefinition());
            page.Children.Add(PageHeading("能力中心", "让宠物主动连接天气、健康与工作上下文；可随时暂停，忙碌时自动延后。"));

            ScrollViewer scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Hidden, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            StackPanel content = new StackPanel { Margin = new Thickness(0, 0, 6, 0) };
            StackPanel utilityContent = new StackPanel();

            StackPanel captureActions = new StackPanel { Orientation = Orientation.Horizontal };
            Button capture = Theme.PrimaryButton("开始智能滚动截图");
            capture.Click += delegate { app.OpenScrollCapture(); };
            captureActions.Children.Add(capture);
            TextBlock captureHint = Theme.Text("默认视觉对齐；也支持 @0.30–@0.90 固定比例", 10.5, Theme.Muted, FontWeights.Normal);
            captureHint.Margin = new Thickness(14, 0, 0, 0);
            captureActions.Children.Add(captureHint);
            utilityContent.Children.Add(CapabilityCard("智能滚动截图", "自动试滚并测量真实位移，按视觉内容对齐相邻画面，输出多张独立图片；尽量减少重复信息，同时保留少量安全上下文避免漏行。", "本地 · 已集成", captureActions));

            TextBlock updateStatus = Theme.Text(
                "当前版本 v" + app.Updates.CurrentVersion + "。默认只检查本地更新收件箱、程序目录和下载目录；不会在后台访问 GitHub。",
                10.5, Theme.Muted, FontWeights.Normal);
            StackPanel updateBody = new StackPanel();
            WrapPanel updateActions = new WrapPanel();
            Button localUpdate = Theme.PrimaryButton("检查本地更新");
            System.Windows.Automation.AutomationProperties.SetName(localUpdate, "检查本地更新");
            localUpdate.Click += delegate
            {
                updateStatus.Text = "正在核验本地签名目录和更新包…";
                System.Threading.ThreadPool.QueueUserWorkItem(delegate
                {
                    UpdateCheckResult result = app.Updates.CheckLocal();
                    Dispatcher.BeginInvoke(new Action(delegate { HandleUpdateCheck(result, updateStatus, true); }));
                });
            };
            updateActions.Children.Add(localUpdate);
            Button importUpdate = Theme.SecondaryButton("导入更新包…");
            importUpdate.Margin = new Thickness(10, 0, 0, 0);
            System.Windows.Automation.AutomationProperties.SetName(importUpdate, "导入离线更新包");
            importUpdate.Click += delegate
            {
                OpenFileDialog dialog = new OpenFileDialog
                {
                    Title = "选择 WorkMate 离线更新包",
                    Filter = "WorkMate 更新包|*.workmate-update.zip|ZIP 文件|*.zip",
                    Multiselect = false
                };
                if (dialog.ShowDialog(this) == true) ImportUpdatePackage(dialog.FileName, updateStatus);
            };
            updateActions.Children.Add(importUpdate);
            Button onlineUpdate = Theme.SecondaryButton("手动检查 GitHub");
            onlineUpdate.Margin = new Thickness(10, 0, 0, 0);
            System.Windows.Automation.AutomationProperties.SetName(onlineUpdate, "手动检查 GitHub 更新");
            onlineUpdate.Click += delegate
            {
                updateStatus.Text = "正在下载很小的签名更新目录；不会自动下载程序包…";
                System.Threading.ThreadPool.QueueUserWorkItem(delegate
                {
                    UpdateCheckResult result = app.Updates.CheckOnlineCatalog();
                    Dispatcher.BeginInvoke(new Action(delegate { HandleUpdateCheck(result, updateStatus, false); }));
                });
            };
            updateActions.Children.Add(onlineUpdate);
            Button openInbox = Theme.SecondaryButton("打开更新收件箱");
            openInbox.Margin = new Thickness(10, 0, 0, 0);
            System.Windows.Automation.AutomationProperties.SetName(openInbox, "打开更新收件箱");
            openInbox.Click += delegate
            {
                try { Process.Start("explorer.exe", "\"" + app.Updates.InboxDirectory + "\""); }
                catch (Exception ex) { updateStatus.Text = "无法打开更新收件箱：" + ex.Message; }
            };
            updateActions.Children.Add(openInbox);
            updateBody.Children.Add(updateActions);
            updateStatus.Margin = new Thickness(0, 10, 0, 0);
            updateBody.Children.Add(updateStatus);
            utilityContent.Children.Add(CapabilityCard(
                "离线增量更新",
                "联网电脑只需下载与当前 EXE 哈希匹配的差分 ZIP，再通过 U 盘或合规通道拷入公司电脑。导入时会验证 RSA 签名、基线哈希和目标哈希；替换失败或新版本启动不健康会自动回滚。",
                "离线优先 · 可回滚",
                updateBody));

            TextBlock ocrStatus = Theme.Text("支持剪贴板图片或 PNG/JPG/BMP/TIFF 文件；识别结果会保存并复制到剪贴板。", 10.5, Theme.Muted, FontWeights.Normal);
            StackPanel ocrBody = new StackPanel();
            StackPanel ocrActions = new StackPanel { Orientation = Orientation.Horizontal };
            Button clipboardOcr = Theme.PrimaryButton("识别剪贴板图片");
            clipboardOcr.Click += delegate
            {
                ocrStatus.Text = "正在调用 Windows 本地 OCR…";
                app.RecognizeClipboardImage(delegate(OcrResponse response)
                {
                    ocrStatus.Text = response.Success
                        ? (string.IsNullOrWhiteSpace(response.Text) ? "识别完成，但没有检测到文字。" : "识别完成并已复制：" + Formatters.Truncate(response.Text.Replace("\r", " ").Replace("\n", " "), 92))
                        : "识别失败：" + response.Error;
                });
            };
            ocrActions.Children.Add(clipboardOcr);
            Button fileOcr = Theme.SecondaryButton("选择图片文件");
            fileOcr.Margin = new Thickness(10, 0, 0, 0);
            fileOcr.Click += delegate
            {
                OpenFileDialog dialog = new OpenFileDialog
                {
                    Title = "选择要识别的图片",
                    Filter = "图片文件|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff|所有文件|*.*",
                    Multiselect = false
                };
                if (dialog.ShowDialog(this) != true) return;
                ocrStatus.Text = "正在调用 Windows 本地 OCR…";
                app.RecognizeImage(dialog.FileName, delegate(OcrResponse response)
                {
                    ocrStatus.Text = response.Success
                        ? (string.IsNullOrWhiteSpace(response.Text) ? "识别完成，但没有检测到文字。" : "识别完成并已复制：" + Formatters.Truncate(response.Text.Replace("\r", " ").Replace("\n", " "), 92))
                        : "识别失败：" + response.Error;
                });
            };
            ocrActions.Children.Add(fileOcr);
            ocrBody.Children.Add(ocrActions);
            ocrStatus.Margin = new Thickness(0, 10, 0, 0);
            ocrBody.Children.Add(ocrStatus);
            utilityContent.Children.Add(CapabilityCard("本地 OCR", "默认使用 Windows.Media.Ocr，在电脑本地识别中英文，不上传图片，也不要求额外下载大型 AI 模型。", "离线 · 已集成", ocrBody));

            TextBlock weatherStatus = Theme.Text("后台每 30 分钟检查一次；若未来 0–2 小时降雨概率达到 60%，会等你退出会议、打字和心流后再提醒。", 10.5, Theme.Muted, FontWeights.Normal);
            StackPanel weatherBody = new StackPanel();
            StackPanel weatherActions = new StackPanel { Orientation = Orientation.Horizontal };
            TextBox city = new TextBox { Text = app.Store.Data.WeatherCity, MaxLength = 50 };
            System.Windows.Automation.AutomationProperties.SetName(city, "天气哨兵城市");
            Border cityShell = Theme.InputShell(city, 40);
            cityShell.Width = 210;
            weatherActions.Children.Add(cityShell);
            Button weather = Theme.PrimaryButton("立即巡天");
            System.Windows.Automation.AutomationProperties.SetName(weather, "立即巡天");
            weather.Margin = new Thickness(10, 0, 0, 0);
            weather.Click += delegate
            {
                weatherStatus.Text = "正在查询 Open-Meteo 小时级预报…";
                app.RefreshWeather(city.Text, delegate(WeatherResponse response)
                {
                    weatherStatus.Text = response.Success
                        ? response.Snapshot.Summary + (response.FromCache ? "\n本次来自 15 分钟本地缓存。" : "\n数据源：Open-Meteo")
                        : "查询失败：" + response.Error;
                });
            };
            weatherActions.Children.Add(weather);
            Button weatherToggle = Theme.SecondaryButton(app.Store.Data.WeatherSentinelEnabled ? "自动提醒：开" : "自动提醒：关");
            System.Windows.Automation.AutomationProperties.SetName(weatherToggle, "天气哨兵自动提醒");
            weatherToggle.Margin = new Thickness(10, 0, 0, 0);
            weatherToggle.Click += delegate
            {
                app.SetWeatherSentinelEnabled(!app.Store.Data.WeatherSentinelEnabled);
                app.Pet.CapabilityToast("☂", app.Store.Data.WeatherSentinelEnabled ? "天气哨兵开始替你望天。" : "天气哨兵已暂停。", false);
                BuildCurrentPage();
            };
            weatherActions.Children.Add(weatherToggle);
            weatherBody.Children.Add(weatherActions);
            weatherStatus.Margin = new Thickness(0, 10, 0, 0);
            weatherBody.Children.Add(weatherStatus);
            WrapPanel presenceActions = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
            Button presenceToggle = Theme.SecondaryButton(app.Store.Data.AmbientPresenceEnabled ? "环境共感：开" : "环境共感：关");
            System.Windows.Automation.AutomationProperties.SetName(presenceToggle, "桌宠环境共感");
            presenceToggle.Click += delegate
            {
                app.SetAmbientPresenceEnabled(!app.Store.Data.AmbientPresenceEnabled);
                app.Pet.EventToast(app.Store.Data.AmbientPresenceEnabled ? "我会用一个安静的小标记陪你感知环境" : "环境共感标记已隐藏");
                BuildCurrentPage();
            };
            presenceActions.Children.Add(presenceToggle);
            TextBlock presenceNote = Theme.Text("静态小标记，不额外弹窗；下雨、空气或紫外线异常时优先显示。", 10, Theme.Muted, FontWeights.Normal);
            presenceNote.Margin = new Thickness(10, 7, 0, 0);
            presenceActions.Children.Add(presenceNote);
            weatherBody.Children.Add(presenceActions);
            content.Children.Add(CapabilityCard("天气哨兵", "不只回答“现在什么天气”：它会在后台读取未来 6 小时预报，并在约一小时后可能下雨时，让宠物主动叼来带伞提醒。", app.Store.Data.WeatherSentinelEnabled ? "主动提醒 · 已开启" : "主动提醒 · 已暂停", weatherBody));

            TextBlock outdoorStatus = Theme.Text("沿用城市“" + app.Store.Data.WeatherCity + "”；全球提供空气指数、PM2.5 与紫外线，花粉按数据源覆盖范围显示。", 10.5, Theme.Muted, FontWeights.Normal);
            StackPanel outdoorBody = new StackPanel();
            StackPanel outdoorActions = new StackPanel { Orientation = Orientation.Horizontal };
            Button outdoor = Theme.PrimaryButton("查看户外指数");
            System.Windows.Automation.AutomationProperties.SetName(outdoor, "查看户外指数");
            outdoor.Click += delegate
            {
                outdoorStatus.Text = "正在查询空气质量、紫外线与花粉…";
                app.RefreshOutdoor(app.Store.Data.WeatherCity, delegate(AmbientResponse response)
                {
                    outdoorStatus.Text = response.Success && response.Snapshot != null && response.Snapshot.Outdoor != null
                        ? response.Snapshot.Outdoor.Summary + (response.FromCache ? "\n本次来自 15 分钟本地缓存。" : "\n数据源：Open-Meteo Air Quality")
                        : "查询失败：" + (response.Snapshot != null ? response.Snapshot.OutdoorError : response.Error);
                });
            };
            outdoorActions.Children.Add(outdoor);
            Button outdoorToggle = Theme.SecondaryButton(app.Store.Data.OutdoorAdvisorEnabled ? "主动守护：开" : "主动守护：关");
            System.Windows.Automation.AutomationProperties.SetName(outdoorToggle, "户外健康主动守护");
            outdoorToggle.Margin = new Thickness(10, 0, 0, 0);
            outdoorToggle.Click += delegate
            {
                app.SetOutdoorAdvisorEnabled(!app.Store.Data.OutdoorAdvisorEnabled);
                app.Pet.CapabilityToast("☀", app.Store.Data.OutdoorAdvisorEnabled ? "户外健康管家开始值班。" : "户外健康管家已暂停。", false);
                BuildCurrentPage();
            };
            outdoorActions.Children.Add(outdoorToggle);
            outdoorBody.Children.Add(outdoorActions);
            outdoorStatus.Margin = new Thickness(0, 10, 0, 0);
            outdoorBody.Children.Add(outdoorStatus);
            content.Children.Add(CapabilityCard("户外健康管家", "空气较差、紫外线很高或花粉浓度偏高时，宠物会做出对应反应并给出一句可执行建议；缺失数据会明确标注，不会用 0 代替。", app.Store.Data.OutdoorAdvisorEnabled ? "主动守护 · 已开启" : "主动守护 · 已暂停", outdoorBody));

            TextBlock briefingStatus = Theme.Text("每天在设定小时后首次处于低打扰状态时递送；整合天气、户外指数、下一场 Outlook 会议与本地待办。", 10.5, Theme.Muted, FontWeights.Normal);
            StackPanel briefingBody = new StackPanel();
            StackPanel briefingActions = new StackPanel { Orientation = Orientation.Horizontal };
            Button briefing = Theme.PrimaryButton("让" + abilityPetName + "给我一份简报");
            System.Windows.Automation.AutomationProperties.SetName(briefing, "立即生成宠物早报");
            briefing.Click += delegate
            {
                briefingStatus.Text = "正在整理联网信息与本地上下文…";
                app.ShowDailyBriefing(delegate(string text) { briefingStatus.Text = text; });
            };
            briefingActions.Children.Add(briefing);
            Button briefingToggle = Theme.SecondaryButton(app.Store.Data.DailyBriefingEnabled ? "每日递送：开" : "每日递送：关");
            System.Windows.Automation.AutomationProperties.SetName(briefingToggle, "宠物早报每日递送");
            briefingToggle.Margin = new Thickness(10, 0, 0, 0);
            briefingToggle.Click += delegate
            {
                app.SetDailyBriefingEnabled(!app.Store.Data.DailyBriefingEnabled);
                app.Pet.CapabilityToast("✦", app.Store.Data.DailyBriefingEnabled ? "每天我会把早报叼来。" : "宠物早报已暂停自动递送。", false);
                BuildCurrentPage();
            };
            briefingActions.Children.Add(briefingToggle);
            TextBox briefingHour = new TextBox { Text = app.Store.Data.DailyBriefingHour.ToString(), MaxLength = 2, TextAlignment = TextAlignment.Center };
            System.Windows.Automation.AutomationProperties.SetName(briefingHour, "宠物早报小时");
            Border hourShell = Theme.InputShell(briefingHour, 38);
            hourShell.Width = 54;
            hourShell.Margin = new Thickness(10, 0, 0, 0);
            briefingActions.Children.Add(hourShell);
            TextBlock hourUnit = Theme.Text("点后", 10.5, Theme.Muted, FontWeights.Normal);
            hourUnit.Margin = new Thickness(6, 0, 6, 0);
            briefingActions.Children.Add(hourUnit);
            Button saveBriefingHour = Theme.SecondaryButton("保存");
            System.Windows.Automation.AutomationProperties.SetName(saveBriefingHour, "保存宠物早报时间");
            saveBriefingHour.Padding = new Thickness(11, 8, 11, 8);
            saveBriefingHour.Click += delegate
            {
                int hour;
                if (!int.TryParse(briefingHour.Text, out hour) || hour < 6 || hour > 12)
                {
                    app.Pet.EventToast("早报时间请输入 6–12 点");
                    briefingHour.Focus();
                    briefingHour.SelectAll();
                    return;
                }
                app.Store.Data.DailyBriefingHour = hour;
                app.Store.Save();
                app.Pet.CapabilityToast("✦", "宠物早报改到 " + hour + " 点后递送。", false);
                BuildCurrentPage();
            };
            briefingActions.Children.Add(saveBriefingHour);
            briefingBody.Children.Add(briefingActions);
            briefingStatus.Margin = new Thickness(0, 10, 0, 0);
            briefingBody.Children.Add(briefingStatus);
            content.Children.Add(CapabilityCard("宠物早报", "不是另一个资讯列表，而是由宠物把外部环境与 WorkMate 已知的会议、锚点和待办合成一份当天可行动的简报。", app.Store.Data.DailyBriefingEnabled ? "每日互动 · 已开启" : "每日互动 · 已暂停", briefingBody));

            TextBlock utilityLabel = SectionLabel("本地工具");
            utilityLabel.Margin = new Thickness(2, 7, 0, 11);
            content.Children.Add(utilityLabel);
            content.Children.Add(utilityContent);

            scroll.Content = content;
            Grid.SetRow(scroll, 2);
            page.Children.Add(scroll);
            return page;
        }

        private void HandleUpdateCheck(UpdateCheckResult result, TextBlock status, bool offerLocalPackage)
        {
            if (result == null)
            {
                status.Text = "更新检查没有返回结果。";
                return;
            }
            status.Text = result.Message
                + (!string.IsNullOrWhiteSpace(result.RecommendedFileName)
                    ? "\n建议文件：" + result.RecommendedFileName : "");
            if (!result.Success || !result.Available || string.IsNullOrWhiteSpace(result.PackagePath) || !offerLocalPackage) return;
            MessageBoxResult choice = MessageBox.Show(
                this,
                "已找到本地更新包：\n" + result.PackagePath + "\n\n现在校验并准备安装吗？",
                "WorkMate 离线更新",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (choice == MessageBoxResult.Yes) ImportUpdatePackage(result.PackagePath, status);
        }

        private void ImportUpdatePackage(string path, TextBlock status)
        {
            status.Text = "正在验证更新包签名、版本基线和 SHA-256…";
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                StagedUpdate staged = app.Updates.StagePackage(path);
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    if (!staged.Success)
                    {
                        status.Text = "更新包已拒绝：" + staged.Error;
                        return;
                    }
                    string kind = staged.Kind == "delta" ? "差分更新" : "全量兜底更新";
                    string transition = staged.Kind == "delta"
                        ? "v" + staged.FromVersion + " → v" + staged.ToVersion
                        : "安装 v" + staged.ToVersion + "（全量包，不要求旧版哈希）";
                    MessageBoxResult choice = MessageBox.Show(
                        this,
                        kind + "已通过签名与哈希校验。\n\n"
                            + transition
                            + (string.IsNullOrWhiteSpace(staged.Notes) ? "" : "\n\n" + staged.Notes)
                            + "\n\n安装时 WorkMate 会正常退出、原子替换并启动新版本；启动检查失败会自动恢复备份。现在安装吗？",
                        "确认安装 WorkMate 更新",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);
                    if (choice != MessageBoxResult.Yes)
                    {
                        status.Text = "更新包已验证并暂存，本次未安装。";
                        return;
                    }
                    try
                    {
                        app.Updates.LaunchStagedUpdate(staged);
                        status.Text = "独立更新器已启动，WorkMate 即将退出。";
                        app.ExitApp();
                    }
                    catch (Exception ex)
                    {
                        status.Text = "无法启动更新器：" + ex.Message;
                    }
                }));
            });
        }

        private UIElement CapabilityCard(string title, string description, string badge, UIElement body)
        {
            StackPanel stack = new StackPanel();
            Grid header = new Grid();
            header.ColumnDefinitions.Add(new ColumnDefinition());
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.Children.Add(Theme.Text(title, 15, Theme.Ink, FontWeights.Bold));
            Border pill = Theme.Pill(badge, Theme.SuccessSoft, Theme.Success);
            Grid.SetColumn(pill, 1);
            header.Children.Add(pill);
            stack.Children.Add(header);
            TextBlock note = Theme.Text(description, 10.8, Theme.Muted, FontWeights.Normal);
            note.Margin = new Thickness(0, 8, 0, 12);
            stack.Children.Add(note);
            if (body != null) stack.Children.Add(body);
            Border card = Theme.Card(stack, 16, new Thickness(17, 15, 17, 15));
            card.Margin = new Thickness(0, 0, 0, 11);
            return card;
        }

        private UIElement BuildSettingsPage()
        {
            Grid page = PageRoot();
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(18) });
            page.RowDefinitions.Add(new RowDefinition());
            page.Children.Add(PageHeading("设置", "选择你的伙伴和工作方式。基础模式始终保持最少操作。"));

            ScrollViewer scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Hidden, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            StackPanel content = new StackPanel { Margin = new Thickness(0, 0, 6, 0) };
            content.Children.Add(SectionLabel("今天的节奏"));
            content.Children.Add(BuildEnergyCheckInCard());
            content.Children.Add(SectionLabel("伙伴"));
            content.Children.Add(BuildPetPicker());
            content.Children.Add(SectionLabel("自定义宠物工坊"));
            content.Children.Add(BuildCustomPetWorkshop());
            content.Children.Add(SectionLabel("工作方式"));
            content.Children.Add(BuildSettingRows());
            content.Children.Add(SectionLabel("行为与陪伴"));
            content.Children.Add(BuildBehaviorRows());
            content.Children.Add(SectionLabel("宠物尺寸"));
            content.Children.Add(BuildSizePicker());
            content.Children.Add(SectionLabel("隐私与数据"));
            content.Children.Add(BuildDataCard());
            scroll.Content = content;
            Grid.SetRow(scroll, 2);
            page.Children.Add(scroll);
            return page;
        }

        private TextBlock SectionLabel(string text)
        {
            TextBlock label = Theme.Text(text, 12, Theme.Muted, FontWeights.Bold);
            label.Margin = new Thickness(2, 10, 0, 11);
            return label;
        }

        private UIElement BuildPetPicker()
        {
            List<PetDefinition> pets = PetCatalog.All;
            UniformGrid grid = new UniformGrid { Columns = 3, Rows = Math.Max(1, (pets.Count + 2) / 3) };
            foreach (PetDefinition pet in pets)
            {
                bool selected = app.Store.Data.PetId == pet.Id;
                Grid cardBody = new Grid();
                cardBody.RowDefinitions.Add(new RowDefinition());
                cardBody.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                Image image = PetAssets.View(pet.Id, "idle", 98);
                image.HorizontalAlignment = HorizontalAlignment.Center;
                System.Windows.Automation.AutomationProperties.SetName(image, pet.Species + " " + pet.Name);
                cardBody.Children.Add(image);
                StackPanel labels = new StackPanel { Margin = new Thickness(0, 0, 0, 2) };
                TextBlock name = Theme.Text(pet.Name, 12, Theme.Ink, FontWeights.Bold);
                name.TextAlignment = TextAlignment.Center;
                labels.Children.Add(name);
                TextBlock species = Theme.Text(pet.Species + (pet.IsCustom ? " · 自定义" : ""), 10, Theme.Muted, FontWeights.Normal);
                species.TextAlignment = TextAlignment.Center;
                labels.Children.Add(species);
                Grid.SetRow(labels, 1);
                cardBody.Children.Add(labels);
                Button card = Theme.Button("", selected ? Theme.AccentSoft : Theme.Surface, selected ? Theme.AccentSoft : Theme.SoftSurface, Theme.Ink, 16);
                card.Content = cardBody;
                card.Height = 154;
                card.Margin = new Thickness(0, 0, 10, 10);
                card.Padding = new Thickness(6);
                card.BorderBrush = selected ? Theme.Accent : Theme.Line;
                card.BorderThickness = new Thickness(selected ? 2 : 1);
                System.Windows.Automation.AutomationProperties.SetName(card, "选择宠物 " + pet.Name + " " + pet.Species);
                string petId = pet.Id;
                card.Click += delegate { currentPage = "settings"; app.SetPet(petId); };
                grid.Children.Add(card);
            }
            return grid;
        }

        private UIElement BuildEnergyCheckInCard()
        {
            StackPanel body = new StackPanel();
            TextBlock explanation = Theme.Text("只影响今天：低能量会停掉自主巡游和稀有表演，只保留降雨/空气等关键提醒；精力足会稍微提高互动出现频率。", 10.5, Theme.Muted, FontWeights.Normal);
            explanation.TextWrapping = TextWrapping.Wrap;
            body.Children.Add(explanation);
            WrapPanel choices = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) };
            string current = app.Store.TodayEnergyMode;
            string[] modes = { "low", "steady", "high" };
            string[] labels = { "低能量", "稳稳来", "精力足" };
            for (int index = 0; index < modes.Length; index++)
            {
                string mode = modes[index];
                bool selected = current == mode;
                Button button = Theme.Button(labels[index], selected ? Theme.Accent : Theme.SoftSurface,
                    selected ? Theme.AccentHover : Theme.Brush("#EAE2DC"), selected ? Brushes.White : Theme.Muted, 999);
                button.Padding = new Thickness(15, 8, 15, 8);
                button.Margin = new Thickness(0, 0, 9, 0);
                System.Windows.Automation.AutomationProperties.SetName(button, "设置今日能量 " + labels[index]);
                button.Click += delegate
                {
                    app.SetTodayEnergyMode(mode);
                };
                choices.Children.Add(button);
            }
            body.Children.Add(choices);
            return CapabilityCard("今日能量", "让宠物适配你，而不是要求你每天都保持同一种效率。", "仅今天 · " + CompanionEnergyPolicy.Label(current), body);
        }

        private UIElement BuildCustomPetWorkshop()
        {
            StackPanel body = new StackPanel();
            TextBlock status = Theme.Text("选择 1–3 张照片后，WorkMate 会创建本地项目、生成严格提示词；你用任意支持参考图的生成工具产出四张透明 PNG，再回来校验启用。", 10.5, Theme.Muted, FontWeights.Normal);
            status.TextWrapping = TextWrapping.Wrap;
            body.Children.Add(status);

            WrapPanel identity = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) };
            TextBox name = new TextBox { Text = "我的伙伴", MaxLength = 24 };
            System.Windows.Automation.AutomationProperties.SetName(name, "自定义宠物名字");
            Border nameShell = Theme.InputShell(name, 38);
            nameShell.Width = 180;
            nameShell.Margin = new Thickness(0, 0, 10, 0);
            identity.Children.Add(nameShell);
            TextBox species = new TextBox { Text = "宠物猫", MaxLength = 24 };
            System.Windows.Automation.AutomationProperties.SetName(species, "自定义宠物类型");
            Border speciesShell = Theme.InputShell(species, 38);
            speciesShell.Width = 150;
            identity.Children.Add(speciesShell);
            body.Children.Add(identity);

            WrapPanel actions = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) };
            Button choose = Theme.SecondaryButton(customPetPhotoPaths.Count == 0 ? "选择宠物照片" : "已选 " + customPetPhotoPaths.Count + " 张照片");
            System.Windows.Automation.AutomationProperties.SetName(choose, "选择自定义宠物参考照片");
            choose.Margin = new Thickness(0, 0, 9, 9);
            choose.Click += delegate
            {
                OpenFileDialog dialog = new OpenFileDialog
                {
                    Title = "选择 1–3 张清晰的宠物照片",
                    Filter = "宠物照片|*.png;*.jpg;*.jpeg;*.webp;*.bmp|所有文件|*.*",
                    Multiselect = true
                };
                if (dialog.ShowDialog(this) != true) return;
                customPetPhotoPaths.Clear();
                customPetPhotoPaths.AddRange(dialog.FileNames.Take(3));
                choose.Content = "已选 " + customPetPhotoPaths.Count + " 张照片";
                status.Text = "照片只会复制到本机 WorkMate 数据目录；创建项目后可在 workflow.html 查看完整步骤。";
            };
            actions.Children.Add(choose);

            Button create = Theme.PrimaryButton("创建生成项目");
            System.Windows.Automation.AutomationProperties.SetName(create, "创建自定义宠物生成项目");
            create.Margin = new Thickness(0, 0, 9, 9);
            create.Click += delegate
            {
                CustomPetResult created = app.CustomPets.CreateProject(name.Text, species.Text, customPetPhotoPaths);
                if (!created.Success) { status.Text = created.Error; app.Pet.EventToast(created.Error); return; }
                customPetProjectPath = created.ProjectDirectory;
                status.Text = "项目已创建：" + created.ProjectDirectory + "\n下一步：打开 workflow.html，生成后把四张 PNG 放进 generated。";
                try { Process.Start(created.WorkflowPath); } catch { }
                app.Pet.EventToast("自定义宠物生成项目已创建");
            };
            actions.Children.Add(create);

            Button open = Theme.SecondaryButton("打开最近项目");
            System.Windows.Automation.AutomationProperties.SetName(open, "打开最近自定义宠物项目");
            open.Margin = new Thickness(0, 0, 9, 9);
            open.Click += delegate
            {
                string project = customPetProjectPath.Length > 0 ? customPetProjectPath : app.CustomPets.FindLatestProject();
                if (project.Length == 0) { status.Text = "还没有自定义宠物项目。"; return; }
                customPetProjectPath = project;
                try { Process.Start("explorer.exe", "\"" + project + "\""); }
                catch (Exception ex) { status.Text = "打开失败：" + ex.Message; }
            };
            actions.Children.Add(open);

            Button import = Theme.SecondaryButton("校验并启用最近项目");
            System.Windows.Automation.AutomationProperties.SetName(import, "校验并启用最近自定义宠物项目");
            import.Margin = new Thickness(0, 0, 9, 9);
            import.Click += delegate
            {
                string project = customPetProjectPath.Length > 0 ? customPetProjectPath : app.CustomPets.FindLatestProject();
                if (project.Length == 0) { status.Text = "还没有可导入的项目。"; return; }
                CustomPetResult imported = app.CustomPets.ImportGeneratedAssets(project);
                if (!imported.Success) { status.Text = imported.Error; app.Pet.EventToast(imported.Error); return; }
                customPetProjectPath = imported.ProjectDirectory;
                app.SetPet(imported.PetId, "你的专属伙伴加入 WorkMate 啦");
                currentPage = "settings";
            };
            actions.Children.Add(import);
            body.Children.Add(actions);
            return CapabilityCard("照片 → 二次元桌宠", "外部模型只负责创作；WorkMate 负责身份约束、四姿态文件契约、透明图质量门禁和运行时加载。", "本地项目 · 用户自主生成", body);
        }

        private UIElement BuildSettingRows()
        {
            StackPanel rows = new StackPanel();
            rows.Children.Add(SettingRow("时间统计", "只累计应用类别；空闲超过 5 分钟自动停止计时。", app.Store.Data.TrackEnabled, delegate
            {
                app.Store.Data.TrackEnabled = !app.Store.Data.TrackEnabled;
                app.Store.Save();
                app.Pet.RefreshPet();
                BuildCurrentPage();
            }));
            rows.Children.Add(SettingRow("四象限模式", "关闭时每条记录只有短期/长期与完成状态。", app.Store.Data.FourQuadrantEnabled, delegate
            {
                app.Store.Data.FourQuadrantEnabled = !app.Store.Data.FourQuadrantEnabled;
                app.Store.Save();
                BuildCurrentPage();
            }));
            rows.Children.Add(SettingRow("宠物置顶", "让伙伴始终停留在普通窗口上方。", app.Store.Data.AlwaysOnTop, delegate
            {
                app.Store.Data.AlwaysOnTop = !app.Store.Data.AlwaysOnTop;
                app.Store.Save();
                app.Pet.RefreshPet();
                BuildCurrentPage();
            }));
            rows.Children.Add(SettingRow("减少动效", "关闭淡入等微动效，宠物状态反馈仍保留。", app.Store.Data.ReducedMotion, delegate
            {
                app.Store.Data.ReducedMotion = !app.Store.Data.ReducedMotion;
                app.Store.Save();
                app.Pet.RefreshPet();
                BuildCurrentPage();
            }));
            rows.Children.Add(SettingRow("开机启动", "仅写入当前用户启动项，可随时关闭。", app.Store.Data.StartWithWindows, delegate
            {
                app.SetStartup(!app.Store.Data.StartWithWindows);
                BuildCurrentPage();
            }));
            return rows;
        }

        private UIElement BuildBehaviorRows()
        {
            StackPanel rows = new StackPanel();
            rows.Children.Add(SettingRow("行为表演", "识别打字/阅读/开会/看视频/思考/打盹并切换表演；只计数不记键码。", app.Store.Data.BehaviorEnabled, delegate
            {
                app.Store.Data.BehaviorEnabled = !app.Store.Data.BehaviorEnabled;
                app.Store.Save();
                BuildCurrentPage();
            }));
            rows.Children.Add(SettingRow("边缘缩入与探头", "拖到任意屏幕边缘会自动缩入；鼠标移到露出位置时再探头。", app.Store.Data.EdgeSnapEnabled, delegate
            {
                app.Store.Data.EdgeSnapEnabled = !app.Store.Data.EdgeSnapEnabled;
                app.Store.Save();
                BuildCurrentPage();
            }));
            rows.Children.Add(SettingRow("Outlook 会议雷达", "本地读取未来两天会议；会前 5 分钟显示名称、时间、地点和与会人员，不落盘。", app.Store.Data.MeetingRadarEnabled, delegate
            {
                app.Store.Data.MeetingRadarEnabled = !app.Store.Data.MeetingRadarEnabled;
                app.Store.Save();
                if (app.Store.Data.MeetingRadarEnabled) app.MeetingRadar.PollIfDue(true);
                BuildCurrentPage();
            }));
            rows.Children.Add(SettingRow("低打扰备忘提醒", "短期满 4 小时、长期满 48 小时才提醒；全局至少间隔 4 小时，每条每天最多一次。", app.Store.Data.MemoNudgesEnabled, delegate
            {
                app.Store.Data.MemoNudgesEnabled = !app.Store.Data.MemoNudgesEnabled;
                app.Store.Save();
                BuildCurrentPage();
            }));
            rows.Children.Add(SettingRow("连续工作提醒", "连续有意工作达到设定时长后提醒；会议、专注、安静陪伴、心流和演示时自动延后。", app.Store.Data.WorkBreakReminderEnabled, delegate
            {
                app.Store.Data.WorkBreakReminderEnabled = !app.Store.Data.WorkBreakReminderEnabled;
                app.Store.Save();
                BuildCurrentPage();
            }));
            rows.Children.Add(BuildWorkBreakIntervalRow());
            rows.Children.Add(SettingRow("休息示范动作", "久坐时，" + PetCatalog.Find(app.Store.Data.PetId).Name + "会播放完整拉伸动作；心流、开会和演示时不会主动打扰。", app.Store.Data.StretchEnabled, delegate
            {
                app.Store.Data.StretchEnabled = !app.Store.Data.StretchEnabled;
                app.Store.Save();
                BuildCurrentPage();
            }));
            rows.Children.Add(SettingRow("多屏跟随", "前台窗口连续在另一块屏时，桌宠自己走过去。", app.Store.Data.FollowMonitorEnabled, delegate
            {
                app.Store.Data.FollowMonitorEnabled = !app.Store.Data.FollowMonitorEnabled;
                app.Store.Save();
                BuildCurrentPage();
            }));
            rows.Children.Add(SettingRow("演示自动退避", "检测全屏/演示模式时桌宠自动隐身，结束后回来。", app.Store.Data.PresentationGuardEnabled, delegate
            {
                app.Store.Data.PresentationGuardEnabled = !app.Store.Data.PresentationGuardEnabled;
                app.Store.Save();
                BuildCurrentPage();
            }));
            rows.Children.Add(SettingRow("共享画面中隐藏（实验性）", "默认关闭；远程桌面或部分共享链路可能连自己也看不到，重新双击 EXE 可安全恢复。", app.Store.Data.HideFromCaptureEnabled, delegate
            {
                app.Store.Data.HideFromCaptureEnabled = !app.Store.Data.HideFromCaptureEnabled;
                app.Store.Save();
                WindowPrivacy.ApplyAll(app.Store.Data.HideFromCaptureEnabled);
                BuildCurrentPage();
            }));
            return rows;
        }

        private UIElement BuildWorkBreakIntervalRow()
        {
            Grid row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            StackPanel copy = new StackPanel();
            copy.Children.Add(Theme.Text("提醒间隔", 13, Theme.Ink, FontWeights.SemiBold));
            TextBlock note = Theme.Text("可设 15–240 分钟；短暂查看资料不会清零，离开 5 分钟后重新计时。", 10.5, Theme.Muted, FontWeights.Normal);
            note.Margin = new Thickness(0, 4, 0, 0);
            copy.Children.Add(note);
            row.Children.Add(copy);

            StackPanel actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            TextBox minutes = new TextBox { Text = app.Store.Data.WorkBreakMinutes.ToString(), MaxLength = 3, TextAlignment = TextAlignment.Center };
            Border shell = Theme.InputShell(minutes, 38);
            shell.Width = 82;
            actions.Children.Add(shell);
            TextBlock unit = Theme.Text("分钟", 11, Theme.Muted, FontWeights.Normal);
            unit.Margin = new Thickness(7, 0, 9, 0);
            actions.Children.Add(unit);
            Button save = Theme.SecondaryButton("保存");
            save.Padding = new Thickness(12, 8, 12, 8);
            save.Click += delegate
            {
                int value;
                if (!int.TryParse(minutes.Text, out value) || value < 15 || value > 240)
                {
                    app.Pet.EventToast("提醒间隔请输入 15–240 分钟");
                    minutes.Focus();
                    minutes.SelectAll();
                    return;
                }
                app.Store.Data.WorkBreakMinutes = value;
                app.Store.Save();
                app.Pet.EventToast("连续工作提醒已设为 " + value + " 分钟");
                BuildCurrentPage();
            };
            actions.Children.Add(save);
            Grid.SetColumn(actions, 1);
            row.Children.Add(actions);
            Border card = Theme.Card(row, 14, new Thickness(15, 12, 15, 12));
            card.Margin = new Thickness(0, 0, 0, 8);
            return card;
        }

        private UIElement SettingRow(string title, string description, bool enabled, Action action)
        {
            Grid row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            StackPanel copy = new StackPanel();
            copy.Children.Add(Theme.Text(title, 13, Theme.Ink, FontWeights.SemiBold));
            TextBlock note = Theme.Text(description, 10.5, Theme.Muted, FontWeights.Normal);
            note.Margin = new Thickness(0, 4, 0, 0);
            copy.Children.Add(note);
            row.Children.Add(copy);
            Button toggle = Theme.Button(enabled ? "已开启" : "已关闭", enabled ? Theme.SuccessSoft : Theme.SoftSurface, enabled ? Theme.SuccessSoft : Theme.Brush("#EAE2DC"), enabled ? Theme.Success : Theme.Muted, 999);
            toggle.Padding = new Thickness(13, 7, 13, 7);
            toggle.FontSize = 10.5;
            System.Windows.Automation.AutomationProperties.SetName(toggle, title + (enabled ? " 已开启" : " 已关闭"));
            toggle.Click += delegate { action(); };
            Grid.SetColumn(toggle, 1);
            row.Children.Add(toggle);
            Border card = Theme.Card(row, 14, new Thickness(15, 12, 15, 12));
            card.Margin = new Thickness(0, 0, 0, 8);
            return card;
        }

        private UIElement BuildSizePicker()
        {
            StackPanel picker = new StackPanel { Orientation = Orientation.Horizontal };
            double[] ratios = new[]
            {
                ResponsivePetSizing.CompactScaleRatio,
                ResponsivePetSizing.ComfortScaleRatio,
                ResponsivePetSizing.LargeScaleRatio
            };
            string[] labels = new[] { "小巧", "舒适", "醒目" };
            for (int index = 0; index < ratios.Length; index++)
            {
                double ratio = ratios[index];
                Rect area = WindowPlacement.WorkAreaFor(app.Pet);
                double resolved = ResponsivePetSizing.Resolve(area.Height, 190, ratio);
                Button button = ChoiceButton(labels[index] + "  ·  当前屏约 " + resolved.ToString("0") + "px",
                    Math.Abs(app.Store.Data.PetScaleRatio - ratio) < 0.001);
                button.Margin = new Thickness(0, 0, 8, 0);
                button.Click += delegate
                {
                    app.Store.Data.PetSize = 190;
                    app.Store.Data.PetScaleRatio = ratio;
                    app.Store.Save();
                    app.Pet.RefreshPet();
                    BuildCurrentPage();
                };
                picker.Children.Add(button);
            }
            Border card = Theme.Card(picker, 14, new Thickness(15, 13, 15, 13));
            return card;
        }

        private UIElement BuildDataCard()
        {
            StackPanel stack = new StackPanel();
            stack.Children.Add(Theme.Text("数据永不离开本机", 13, Theme.Ink, FontWeights.Bold));
            TextBlock path = Theme.Text(app.Store.DataPath, 10.5, Theme.Muted, FontWeights.Normal);
            path.Margin = new Thickness(0, 6, 0, 13);
            stack.Children.Add(path);
            StackPanel actions = new StackPanel { Orientation = Orientation.Horizontal };
            Button open = Theme.SecondaryButton("打开数据目录");
            open.Click += delegate
            {
                try { Process.Start("explorer.exe", "/select,\"" + app.Store.DataPath + "\""); } catch { }
            };
            actions.Children.Add(open);
            bool armed = DateTime.Now < clearStatsArmedUntil;
            Button clear = Theme.Button(armed ? "再次点击确认清空" : "清空今日统计", armed ? Theme.Accent : Theme.SoftSurface, armed ? Theme.AccentHover : Theme.Brush("#EAE2DC"), armed ? Brushes.White : Theme.Muted, 12);
            clear.Margin = new Thickness(9, 0, 0, 0);
            clear.Click += delegate
            {
                if (DateTime.Now < clearStatsArmedUntil)
                {
                    app.Store.ClearTodayStats();
                    clearStatsArmedUntil = DateTime.MinValue;
                    BuildCurrentPage();
                }
                else
                {
                    clearStatsArmedUntil = DateTime.Now.AddSeconds(6);
                    clear.Content = "再次点击确认清空";
                    clear.Background = Theme.Accent;
                    clear.Foreground = Brushes.White;
                }
            };
            actions.Children.Add(clear);
            stack.Children.Add(actions);
            TextBlock version = Theme.Text("WorkMate Pro 1.24 · 自适应陪伴、今日一件事与专属桌宠", 10, Theme.Faint, FontWeights.Normal);
            version.Margin = new Thickness(0, 14, 0, 0);
            stack.Children.Add(version);
            Border card = Theme.Card(stack, 16, new Thickness(17, 15, 17, 15));
            card.Margin = new Thickness(0, 0, 0, 18);
            return card;
        }
    }
}
