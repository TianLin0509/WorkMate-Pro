using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WorkMatePro
{
    public sealed partial class WorkbenchWindow
    {
        private readonly Dictionary<string, string> customPetPosePaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private int customPetStep = 1;
        private string customPetNameDraft = "我的伙伴";
        private string customPetSpeciesDraft = "宠物猫";
        private string customPetStatus = "从身份和照片开始；每一步都会自动保存在本地项目中。";
        private bool customPetStatusError;
        private bool customPetBusy;
        private bool customPetShowAllProjects;
        private CustomPetValidationResult customPetValidation;

        private UIElement BuildCustomPetPage()
        {
            Grid page = PageRoot();
            System.Windows.Automation.AutomationProperties.SetAutomationId(page, "custom-pet-page");
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(14) });
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(14) });
            page.RowDefinitions.Add(new RowDefinition());
            page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(14) });
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Grid heading = new Grid();
            heading.ColumnDefinitions.Add(new ColumnDefinition());
            heading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            StackPanel title = PageHeading("自定义宠物向导", "照片只留在本机；你可以随时返回或关闭，草稿不会丢失。");
            heading.Children.Add(title);
            Button settings = Theme.GhostButton("返回设置");
            settings.Padding = new Thickness(13, 7, 13, 7);
            settings.VerticalAlignment = VerticalAlignment.Top;
            System.Windows.Automation.AutomationProperties.SetName(settings, "返回 WorkMate 设置");
            settings.Click += delegate { ShowPage("settings"); };
            Grid.SetColumn(settings, 1);
            heading.Children.Add(settings);
            page.Children.Add(heading);

            UIElement progress = BuildCustomPetProgress();
            Grid.SetRow(progress, 2);
            page.Children.Add(progress);

            ScrollViewer scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                CanContentScroll = false,
                Padding = new Thickness(0, 0, 6, 0)
            };
            System.Windows.Automation.AutomationProperties.SetAutomationId(scroll, "custom-pet-step-" + customPetStep);
            if (customPetStep == 1) scroll.Content = BuildCustomPetIdentityStep();
            else if (customPetStep == 2) scroll.Content = BuildCustomPetGenerationStep();
            else if (customPetStep == 3) scroll.Content = BuildCustomPetImagesStep();
            else scroll.Content = BuildCustomPetEnableStep();
            Grid.SetRow(scroll, 4);
            page.Children.Add(scroll);

            UIElement footer = BuildCustomPetFooter();
            Grid.SetRow(footer, 6);
            page.Children.Add(footer);
            return page;
        }

        private UIElement BuildCustomPetProgress()
        {
            string[] titles = { "身份与照片", "生成指引", "四张图片", "校验并启用" };
            UniformGrid steps = new UniformGrid { Columns = 4, Rows = 1 };
            int maxStep = string.IsNullOrWhiteSpace(customPetProjectPath) ? 1 : (customPetValidation != null && customPetValidation.Success ? 4 : 3);
            for (int i = 0; i < titles.Length; i++)
            {
                int step = i + 1;
                bool current = step == customPetStep;
                bool completed = step < customPetStep;
                Button button = Theme.Button(step + "  " + titles[i],
                    current ? Theme.AccentSoft : completed ? Theme.SuccessSoft : Theme.SoftSurface,
                    current ? Theme.AccentSoft : completed ? Theme.SuccessSoft : Theme.Brush("#EEE5DE"),
                    current ? Theme.Accent : completed ? Theme.Success : Theme.Muted, 11);
                button.FontSize = 11.5;
                button.Padding = new Thickness(8, 8, 8, 8);
                button.Margin = new Thickness(i == 0 ? 0 : 4, 0, i == titles.Length - 1 ? 0 : 4, 0);
                button.IsEnabled = !customPetBusy && step <= maxStep;
                System.Windows.Automation.AutomationProperties.SetName(button, "自定义宠物第 " + step + " 步：" + titles[i]
                    + (current ? "，当前步骤" : completed ? "，已完成" : ""));
                System.Windows.Automation.AutomationProperties.SetAutomationId(button, "custom-pet-progress-" + step);
                button.Click += delegate { customPetStep = step; BuildCurrentPage(); };
                steps.Children.Add(button);
            }
            return steps;
        }

        private UIElement BuildCustomPetIdentityStep()
        {
            StackPanel body = new StackPanel();
            CustomPetProjectInfo active = string.IsNullOrWhiteSpace(customPetProjectPath) ? null : app.CustomPets.GetProjectInfo(customPetProjectPath);
            if (active != null && active.Success)
            {
                body.Children.Add(CustomPetNotice("正在继续 · " + active.Name,
                    "身份与参考照片已写入项目。返回本步不会覆盖草稿；若要换照片，请新建另一个项目。", false));
                customPetNameDraft = active.Name;
                customPetSpeciesDraft = active.Species;
                customPetPhotoPaths.Clear();
                customPetPhotoPaths.AddRange(active.ReferencePaths);
                Button restart = Theme.SecondaryButton("新建另一个伙伴");
                restart.Margin = new Thickness(0, 10, 0, 14);
                restart.HorizontalAlignment = HorizontalAlignment.Left;
                restart.IsEnabled = !customPetBusy;
                restart.Click += delegate { ResetCustomPetFlow(); BuildCurrentPage(); };
                body.Children.Add(restart);
            }
            else
            {
                List<CustomPetProjectInfo> projects = app.CustomPets.ListProjects();
                if (projects.Count > 0)
                {
                    body.Children.Add(Theme.Text("继续已有项目 · " + projects.Count, 13, Theme.Ink, FontWeights.Bold));
                    foreach (CustomPetProjectInfo info in (customPetShowAllProjects ? projects : projects.Take(3)))
                        body.Children.Add(BuildCustomPetProjectRow(info));
                    WrapPanel projectActions = new WrapPanel { Margin = new Thickness(0, 9, 0, 0) };
                    if (projects.Count > 3)
                    {
                        Button showAll = Theme.GhostButton(customPetShowAllProjects ? "收起项目" : "显示全部 " + projects.Count + " 个");
                        showAll.Margin = new Thickness(0, 0, 8, 8);
                        showAll.Click += delegate { customPetShowAllProjects = !customPetShowAllProjects; BuildCurrentPage(); };
                        projectActions.Children.Add(showAll);
                    }
                    projectActions.Children.Add(CustomPetFolderButton("打开项目根目录", app.CustomPets.CustomRoot));
                    body.Children.Add(projectActions);
                    TextBlock divider = Theme.Text("或者创建新伙伴", 12, Theme.Muted, FontWeights.Bold);
                    divider.Margin = new Thickness(0, 14, 0, 8);
                    body.Children.Add(divider);
                }
            }

            Grid identity = new Grid { Margin = new Thickness(0, 6, 0, 0) };
            identity.ColumnDefinitions.Add(new ColumnDefinition());
            identity.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
            identity.ColumnDefinitions.Add(new ColumnDefinition());
            identity.Children.Add(CustomPetInput("伙伴名字", "例如：团子", customPetNameDraft, 24, active != null && active.Success,
                delegate(string value) { customPetNameDraft = value; }));
            UIElement species = CustomPetInput("宠物类型", "例如：三花猫", customPetSpeciesDraft, 24, active != null && active.Success,
                delegate(string value) { customPetSpeciesDraft = value; });
            Grid.SetColumn(species, 2);
            identity.Children.Add(species);
            body.Children.Add(identity);

            Grid photoHeader = new Grid { Margin = new Thickness(0, 18, 0, 9) };
            photoHeader.ColumnDefinitions.Add(new ColumnDefinition());
            photoHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            photoHeader.Children.Add(Theme.Text("参考照片 · 1–3 张", 13, Theme.Ink, FontWeights.Bold));
            Button choose = Theme.SecondaryButton(customPetPhotoPaths.Count == 0 ? "选择照片" : "重新选择");
            choose.Padding = new Thickness(12, 7, 12, 7);
            choose.IsEnabled = !customPetBusy && (active == null || !active.Success);
            System.Windows.Automation.AutomationProperties.SetName(choose, "选择一到三张自定义宠物参考照片");
            choose.Click += delegate { ChooseCustomPetPhotos(); };
            Grid.SetColumn(choose, 1);
            photoHeader.Children.Add(choose);
            body.Children.Add(photoHeader);

            if (customPetPhotoPaths.Count == 0)
                body.Children.Add(CustomPetNotice("还没有照片", "优先选择正脸、三分之四侧脸和能看清花纹的全身照；避免多人、多宠物或严重遮挡。", false));
            else
            {
                for (int i = 0; i < customPetPhotoPaths.Count; i++)
                {
                    int index = i;
                    body.Children.Add(BuildCustomPetPhotoRow(customPetPhotoPaths[i], active == null || !active.Success, delegate
                    {
                        customPetPhotoPaths.RemoveAt(index);
                        SetCustomPetStatus("已移除一张参考照片。", false);
                        BuildCurrentPage();
                    }));
                }
            }
            body.Children.Add(CustomPetNotice("本地隐私边界",
                "WorkMate 只把照片复制到本机 CustomPets 项目。App 不上传照片、不保存第三方模型密钥，也不会替你选择生成服务。", false));
            return body;
        }

        private UIElement BuildCustomPetProjectRow(CustomPetProjectInfo info)
        {
            Grid row = new Grid { Margin = new Thickness(0, 7, 0, 0) };
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            StackPanel text = new StackPanel();
            text.Children.Add(Theme.Text(info.Name + " · " + info.Species, 12.5, Theme.Ink, FontWeights.SemiBold));
            text.Children.Add(Theme.Text(info.Ready ? "已就绪" : "草稿 · 已收集 " + info.GeneratedCount + "/4 张姿态", 10.5,
                info.Ready ? Theme.Success : Theme.Muted, FontWeights.Normal));
            row.Children.Add(text);
            Button resume = Theme.SecondaryButton("继续");
            resume.Padding = new Thickness(12, 6, 12, 6);
            resume.IsEnabled = !customPetBusy;
            resume.Click += delegate { ContinueCustomPetProject(info); };
            Grid.SetColumn(resume, 1);
            row.Children.Add(resume);
            Border card = Theme.Card(row, 13, new Thickness(13, 10, 13, 10));
            card.Background = Theme.Brush("#FAF6F2");
            return card;
        }

        private UIElement CustomPetInput(string label, string hint, string value, int maxLength, bool readOnly, Action<string> changed)
        {
            StackPanel stack = new StackPanel();
            stack.Children.Add(Theme.Text(label, 11, Theme.Muted, FontWeights.SemiBold));
            TextBox box = new TextBox { Text = value ?? "", MaxLength = maxLength, IsReadOnly = readOnly };
            box.ToolTip = hint;
            System.Windows.Automation.AutomationProperties.SetName(box, label + "，" + hint);
            box.TextChanged += delegate { changed(box.Text); };
            Border shell = Theme.InputShell(box, 40);
            shell.Margin = new Thickness(0, 6, 0, 0);
            stack.Children.Add(shell);
            return stack;
        }

        private UIElement BuildCustomPetPhotoRow(string path, bool removable, Action remove)
        {
            Grid row = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(64) });
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.Children.Add(CustomPetPreview(path, 54));
            StackPanel detail = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 8, 0) };
            detail.Children.Add(Theme.Text(Path.GetFileName(path), 11.5, Theme.Ink, FontWeights.SemiBold));
            try
            {
                FileInfo file = new FileInfo(path);
                detail.Children.Add(Theme.Text(CustomPetFileSize(file.Length) + " · 仅复制到本地项目", 10, Theme.Muted, FontWeights.Normal));
            }
            catch { detail.Children.Add(Theme.Text("文件暂时不可读", 10, Theme.Warning, FontWeights.Normal)); }
            Grid.SetColumn(detail, 1);
            row.Children.Add(detail);
            if (removable)
            {
                Button button = Theme.GhostButton("移除");
                button.Padding = new Thickness(10, 5, 10, 5);
                button.Click += delegate { remove(); };
                Grid.SetColumn(button, 2);
                row.Children.Add(button);
            }
            Border card = Theme.Card(row, 14, new Thickness(10));
            card.Background = Theme.Surface;
            return card;
        }

        private UIElement BuildCustomPetGenerationStep()
        {
            StackPanel body = new StackPanel();
            CustomPetProjectInfo info = app.CustomPets.GetProjectInfo(customPetProjectPath);
            if (!info.Success) return CustomPetNotice("项目无法读取", info.Error, true);
            body.Children.Add(CustomPetNotice("项目已安全保存",
                info.Name + " 的参考图、提示词和中间文件都在本机。关闭 WorkMate 后仍可从设置页继续。", false));
            body.Children.Add(BuildCustomPetGuideCard("1", "复制完整提示词",
                "提示词已经写入身份一致性、画布、透明背景、主体中心、脚底基线和四个动作约束。"));
            body.Children.Add(BuildCustomPetGuideCard("2", "把参考图交给你选择的生成工具",
                "先锁定同一角色，再只改变动作；若工具支持 seed / character reference，四张固定同一设置。"));
            body.Children.Add(BuildCustomPetGuideCard("3", "分别导出四张透明 PNG",
                "不要直接使用四宫格截图。下一步可以为 idle、typing、happy、sleep 分别选择任意文件名的 PNG。"));

            string prompt;
            try { prompt = app.CustomPets.ReadPrompt(customPetProjectPath); }
            catch (Exception ex) { prompt = "读取提示词失败：" + ex.Message; }
            TextBox preview = new TextBox
            {
                Text = prompt,
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Height = 190,
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                FontFamily = Theme.Font,
                FontSize = 11.5,
                Foreground = Theme.Ink,
                Padding = new Thickness(12)
            };
            System.Windows.Automation.AutomationProperties.SetName(preview, "自定义宠物完整生成提示词预览");
            Border promptCard = Theme.Card(preview, 14, new Thickness(0));
            promptCard.Margin = new Thickness(0, 12, 0, 0);
            promptCard.Background = Theme.Brush("#FAF6F2");

            WrapPanel actions = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) };
            Button copy = Theme.PrimaryButton("复制完整提示词");
            copy.Margin = new Thickness(0, 0, 8, 8);
            copy.IsEnabled = !customPetBusy;
            copy.Click += delegate
            {
                try { Clipboard.SetText(prompt); SetCustomPetStatus("完整提示词已复制，可以粘贴到你选择的生成工具。", false); }
                catch (Exception ex) { SetCustomPetStatus("复制失败：" + ex.Message, true); }
                BuildCurrentPage();
            };
            actions.Children.Add(copy);
            actions.Children.Add(CustomPetFolderButton("打开参考照片", Path.Combine(info.ProjectDirectory, "references")));
            actions.Children.Add(CustomPetFolderButton("打开项目文件夹", info.ProjectDirectory));
            Button offline = Theme.SecondaryButton("打开离线指南");
            offline.Margin = new Thickness(0, 0, 8, 8);
            offline.Click += delegate { OpenCustomPetPath(info.WorkflowPath); };
            actions.Children.Add(offline);
            body.Children.Add(actions);
            body.Children.Add(promptCard);
            if (info.GeneratedCount == 4)
                body.Children.Add(CustomPetNotice("已发现 generated 中的四张图片", "下一步会自动带入，也可以逐张替换。", false));
            return body;
        }

        private UIElement BuildCustomPetGuideCard(string number, string title, string description)
        {
            Grid grid = new Grid { Margin = new Thickness(0, 8, 0, 0) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            Border badge = new Border
            {
                Width = 28,
                Height = 28,
                CornerRadius = new CornerRadius(14),
                Background = Theme.AccentSoft,
                Child = Theme.Text(number, 12, Theme.Accent, FontWeights.Bold),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };
            ((TextBlock)badge.Child).TextAlignment = TextAlignment.Center;
            grid.Children.Add(badge);
            StackPanel text = new StackPanel();
            text.Children.Add(Theme.Text(title, 12.5, Theme.Ink, FontWeights.Bold));
            TextBlock note = Theme.Text(description, 10.7, Theme.Muted, FontWeights.Normal);
            note.Margin = new Thickness(0, 3, 0, 0);
            text.Children.Add(note);
            Grid.SetColumn(text, 1);
            grid.Children.Add(text);
            return grid;
        }

        private UIElement BuildCustomPetImagesStep()
        {
            RefreshCustomPetPosePaths(false);
            StackPanel body = new StackPanel();
            if (customPetValidation != null && !customPetValidation.Success)
            {
                Border recovery = (Border)CustomPetNotice("这组图片还需要调整",
                    customPetValidation.Error + "\n" + customPetValidation.RecoveryHint, true);
                StackPanel recoveryContent = recovery.Child as StackPanel;
                if (recoveryContent != null && recoveryContent.Children.Count > 0)
                    System.Windows.Automation.AutomationProperties.SetAutomationId(recoveryContent.Children[0], "custom-pet-validation-error");
                body.Children.Add(recovery);
            }
            body.Children.Add(CustomPetNotice("逐张映射，不要求原文件名",
                "点击每一行选择对应姿态，或把单张 PNG 直接拖到该行。WorkMate 会先校验，再原子复制进 generated。", false));
            foreach (string action in CustomPetService.RequiredActions) body.Children.Add(BuildCustomPetPoseRow(action));
            WrapPanel actions = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
            Button refresh = Theme.SecondaryButton("从 generated 自动识别");
            refresh.Margin = new Thickness(0, 0, 8, 8);
            refresh.IsEnabled = !customPetBusy;
            refresh.Click += delegate { RefreshCustomPetPosePaths(true); BuildCurrentPage(); };
            actions.Children.Add(refresh);
            if (!string.IsNullOrWhiteSpace(customPetProjectPath))
                actions.Children.Add(CustomPetFolderButton("打开 generated 文件夹", Path.Combine(customPetProjectPath, "generated")));
            body.Children.Add(actions);
            return body;
        }

        private UIElement BuildCustomPetPoseRow(string action)
        {
            string path;
            customPetPosePaths.TryGetValue(action, out path);
            Grid row = new Grid { Margin = new Thickness(0, 8, 0, 0), AllowDrop = true };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(76) });
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.Children.Add(CustomPetPreview(path, 64));
            StackPanel text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 10, 0) };
            text.Children.Add(Theme.Text(CustomPetPoseTitle(action) + " · " + action + ".png", 12.5, Theme.Ink, FontWeights.Bold));
            text.Children.Add(Theme.Text(string.IsNullOrWhiteSpace(path) ? CustomPetPoseHint(action) : Path.GetFileName(path),
                10.5, string.IsNullOrWhiteSpace(path) ? Theme.Muted : Theme.Success, FontWeights.Normal));
            Grid.SetColumn(text, 1);
            row.Children.Add(text);
            Button choose = Theme.SecondaryButton(string.IsNullOrWhiteSpace(path) ? "选择 PNG" : "替换");
            choose.Padding = new Thickness(12, 7, 12, 7);
            choose.IsEnabled = !customPetBusy;
            choose.Click += delegate { ChooseCustomPetPose(action); };
            Grid.SetColumn(choose, 2);
            row.Children.Add(choose);
            row.DragOver += delegate(object sender, DragEventArgs e)
            {
                string dropped;
                e.Effects = TryGetSinglePng(e.Data, out dropped) ? DragDropEffects.Copy : DragDropEffects.None;
                e.Handled = true;
            };
            row.Drop += delegate(object sender, DragEventArgs e)
            {
                string dropped;
                if (TryGetSinglePng(e.Data, out dropped))
                {
                    customPetPosePaths[action] = dropped;
                    customPetValidation = null;
                    SetCustomPetStatus("已把 " + Path.GetFileName(dropped) + " 映射为 " + action + ".png。", false);
                    BuildCurrentPage();
                }
                e.Handled = true;
            };
            Border card = Theme.Card(row, 15, new Thickness(10));
            card.Background = string.IsNullOrWhiteSpace(path) ? Theme.Surface : Theme.SuccessSoft;
            return card;
        }

        private UIElement BuildCustomPetEnableStep()
        {
            StackPanel body = new StackPanel();
            CustomPetProjectInfo info = app.CustomPets.GetProjectInfo(customPetProjectPath);
            if (!info.Success) return CustomPetNotice("项目无法读取", info.Error, true);
            if (customPetValidation == null)
            {
                body.Children.Add(CustomPetNotice("还需要一次质量校验", "点击下方“重新校验”检查四张图；失败时会给出可以直接照做的修复建议。", false));
            }
            else if (!customPetValidation.Success)
            {
                body.Children.Add(CustomPetNotice("校验未通过", customPetValidation.Error + "\n" + customPetValidation.RecoveryHint, true));
            }
            else
            {
                body.Children.Add(CustomPetNotice(info.Ready ? "伙伴资源已就绪" : "四张图片全部通过",
                    customPetValidation.Width + "×" + customPetValidation.Height + " · RGBA 透明背景 · 中心/尺度/脚底基线一致", false));
                UniformGrid gallery = new UniformGrid { Columns = 4, Rows = 1, Margin = new Thickness(0, 12, 0, 0) };
                foreach (string action in CustomPetService.RequiredActions)
                {
                    StackPanel pose = new StackPanel { Margin = new Thickness(4) };
                    string path = Path.Combine(info.ProjectDirectory, info.Ready ? "assets" : "generated", action + ".png");
                    pose.Children.Add(CustomPetPreview(path, 94));
                    TextBlock label = Theme.Text(CustomPetPoseTitle(action), 10.5, Theme.Muted, FontWeights.SemiBold);
                    label.TextAlignment = TextAlignment.Center;
                    pose.Children.Add(label);
                    gallery.Children.Add(pose);
                }
                body.Children.Add(gallery);
                foreach (CustomPetAssetSummary asset in customPetValidation.Assets)
                {
                    TextBlock metric = Theme.Text(CustomPetPoseTitle(asset.Action) + "：透明 "
                        + Math.Round(asset.TransparentRatio * 100) + "% · 主体 " + Math.Round(asset.OpaqueRatio * 100) + "% · 底部 "
                        + Math.Round(asset.Bottom * 100) + "%", 10.3, Theme.Muted, FontWeights.Normal);
                    metric.Margin = new Thickness(4, 4, 0, 0);
                    body.Children.Add(metric);
                }
            }
            WrapPanel actions = new WrapPanel { Margin = new Thickness(0, 13, 0, 0) };
            Button validate = Theme.SecondaryButton("重新校验");
            validate.Margin = new Thickness(0, 0, 8, 8);
            validate.IsEnabled = !customPetBusy;
            validate.Click += delegate { ValidateCurrentCustomPetProject(); };
            actions.Children.Add(validate);
            actions.Children.Add(CustomPetFolderButton("打开项目文件夹", info.ProjectDirectory));
            body.Children.Add(actions);
            if (info.Ready && string.Equals(app.Store.Data.PetId, info.Id, StringComparison.OrdinalIgnoreCase))
                body.Children.Add(CustomPetNotice("当前正在使用这只伙伴", "工作、开心、休息和睡眠状态会使用你的四张图片；复杂逐帧动作会安全回退为轻量程序动效。", false));
            return body;
        }

        private UIElement BuildCustomPetFooter()
        {
            Grid footer = new Grid();
            footer.ColumnDefinitions.Add(new ColumnDefinition());
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            TextBlock status = Theme.Text((customPetBusy ? "正在处理 · " : "") + customPetStatus, 10.5,
                customPetStatusError ? Theme.Warning : Theme.Muted, customPetStatusError ? FontWeights.SemiBold : FontWeights.Normal);
            status.MaxWidth = 390;
            System.Windows.Automation.AutomationProperties.SetName(status, "自定义宠物向导状态：" + customPetStatus);
            footer.Children.Add(status);
            StackPanel actions = new StackPanel { Orientation = Orientation.Horizontal };
            Button back = Theme.SecondaryButton(customPetStep == 1 ? "返回设置" : "上一步");
            back.Margin = new Thickness(0, 0, 8, 0);
            back.IsEnabled = !customPetBusy;
            back.Click += delegate
            {
                if (customPetStep == 1) ShowPage("settings");
                else { customPetStep--; BuildCurrentPage(); }
            };
            actions.Children.Add(back);
            Button next = Theme.PrimaryButton(CustomPetNextLabel());
            next.IsEnabled = !customPetBusy && CanAdvanceCustomPet();
            System.Windows.Automation.AutomationProperties.SetName(next, "自定义宠物向导：" + CustomPetNextLabel());
            System.Windows.Automation.AutomationProperties.SetAutomationId(next, "custom-pet-next");
            next.Click += delegate { AdvanceCustomPet(); };
            actions.Children.Add(next);
            Grid.SetColumn(actions, 1);
            footer.Children.Add(actions);
            return footer;
        }

        private bool CanAdvanceCustomPet()
        {
            if (customPetStep == 1)
                return !string.IsNullOrWhiteSpace(customPetProjectPath)
                    || (!string.IsNullOrWhiteSpace(customPetNameDraft) && customPetPhotoPaths.Count >= 1 && customPetPhotoPaths.Count <= 3);
            if (customPetStep == 2) return !string.IsNullOrWhiteSpace(customPetProjectPath);
            if (customPetStep == 3)
                return CustomPetService.RequiredActions.All(delegate(string action)
                {
                    string path;
                    return customPetPosePaths.TryGetValue(action, out path) && File.Exists(path);
                });
            return customPetValidation != null && customPetValidation.Success;
        }

        private string CustomPetNextLabel()
        {
            if (customPetStep == 1) return string.IsNullOrWhiteSpace(customPetProjectPath) ? "创建本地项目" : "继续生成指引";
            if (customPetStep == 2) return "我已生成，选择图片";
            if (customPetStep == 3) return "校验四张图片";
            CustomPetProjectInfo info = app.CustomPets.GetProjectInfo(customPetProjectPath);
            if (info.Success && info.Ready) return string.Equals(app.Store.Data.PetId, info.Id, StringComparison.OrdinalIgnoreCase) ? "完成" : "设为当前伙伴";
            return "启用这只伙伴";
        }

        private void AdvanceCustomPet()
        {
            if (customPetStep == 1)
            {
                if (!string.IsNullOrWhiteSpace(customPetProjectPath)) { customPetStep = 2; BuildCurrentPage(); return; }
                List<string> photos = customPetPhotoPaths.ToList();
                string name = customPetNameDraft;
                string species = customPetSpeciesDraft;
                RunCustomPetTask("正在创建可恢复的本地项目…", delegate { return app.CustomPets.CreateProject(name, species, photos); }, delegate(CustomPetResult result)
                {
                    if (!result.Success) { SetCustomPetStatus(result.Error, true); return; }
                    customPetProjectPath = result.ProjectDirectory;
                    customPetStep = 2;
                    SetCustomPetStatus("项目已创建。下一步复制提示词并生成四个姿态。", false);
                    app.Pet.EventToast("自定义宠物项目已创建");
                });
                return;
            }
            if (customPetStep == 2)
            {
                RefreshCustomPetPosePaths(false);
                customPetStep = 3;
                SetCustomPetStatus("为四个姿态逐张选择 PNG；原文件名可以不同。", false);
                BuildCurrentPage();
                return;
            }
            if (customPetStep == 3)
            {
                Dictionary<string, string> selected = new Dictionary<string, string>(customPetPosePaths, StringComparer.OrdinalIgnoreCase);
                string project = customPetProjectPath;
                RunCustomPetTask("正在检查透明背景、中心、尺度和脚底基线…", delegate
                {
                    return app.CustomPets.PrepareGeneratedAssets(project, selected);
                }, delegate(CustomPetResult result)
                {
                    customPetValidation = result.Validation;
                    if (!result.Success)
                    {
                        string recovery = result.Validation == null ? "" : result.Validation.RecoveryHint;
                        SetCustomPetStatus(result.Error + (string.IsNullOrWhiteSpace(recovery) ? "" : " " + recovery), true);
                        return;
                    }
                    RefreshCustomPetPosePaths(true);
                    customPetStep = 4;
                    SetCustomPetStatus("四张图片全部通过，确认后即可启用。", false);
                });
                return;
            }

            CustomPetProjectInfo info = app.CustomPets.GetProjectInfo(customPetProjectPath);
            if (info.Success && info.Ready)
            {
                if (!string.Equals(app.Store.Data.PetId, info.Id, StringComparison.OrdinalIgnoreCase))
                    app.SetPet(info.Id, "你的专属伙伴回来啦");
                ShowPage("settings");
                return;
            }
            string currentProject = customPetProjectPath;
            RunCustomPetTask("正在原子导入并刷新桌宠资源…", delegate { return app.CustomPets.ImportGeneratedAssets(currentProject); }, delegate(CustomPetResult result)
            {
                customPetValidation = result.Validation;
                if (!result.Success) { SetCustomPetStatus(result.Error, true); return; }
                PetAssets.InvalidatePet(result.PetId);
                SetCustomPetStatus("启用成功。你的专属伙伴已经加入 WorkMate。", false);
                app.SetPet(result.PetId, "你的专属伙伴加入 WorkMate 啦");
            });
        }

        private void ContinueCustomPetProject(CustomPetProjectInfo info)
        {
            if (info == null || !info.Success) return;
            customPetProjectPath = info.ProjectDirectory;
            customPetNameDraft = info.Name;
            customPetSpeciesDraft = info.Species;
            customPetPhotoPaths.Clear();
            customPetPhotoPaths.AddRange(info.ReferencePaths);
            customPetPosePaths.Clear();
            foreach (KeyValuePair<string, string> pair in info.GeneratedPaths) customPetPosePaths[pair.Key] = pair.Value;
            customPetValidation = null;
            customPetStep = info.Ready ? 4 : info.GeneratedCount == 4 ? 3 : 2;
            SetCustomPetStatus(info.Ready ? "已载入就绪项目，正在重新核验资源。" : "已继续本地草稿。", false);
            currentPage = "custom-pet";
            BuildCurrentPage();
            if (info.Ready) ValidateCurrentCustomPetProject();
        }

        internal void ShowCustomPetProjectForE2E(string projectDirectory, int step)
        {
            if (Environment.GetEnvironmentVariable("WORKMATE_CUSTOM_PET_E2E") != "1"
                || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WORKMATE_TEST_DIR")))
                throw new InvalidOperationException("自定义宠物 UI E2E 只能在隔离测试目录中运行。");
            CustomPetProjectInfo info = app.CustomPets.GetProjectInfo(projectDirectory);
            if (!info.Success) throw new InvalidOperationException(info.Error);
            customPetProjectPath = info.ProjectDirectory;
            customPetNameDraft = info.Name;
            customPetSpeciesDraft = info.Species;
            customPetPhotoPaths.Clear();
            customPetPhotoPaths.AddRange(info.ReferencePaths);
            customPetPosePaths.Clear();
            foreach (KeyValuePair<string, string> pair in info.GeneratedPaths) customPetPosePaths[pair.Key] = pair.Value;
            customPetStep = Math.Max(1, Math.Min(4, step));
            customPetValidation = customPetStep == 4 ? app.CustomPets.ValidateProjectAssets(info.ProjectDirectory) : null;
            SetCustomPetStatus("隔离 UI 验证 · 第 " + customPetStep + " 步", false);
            currentPage = "custom-pet";
            BuildCurrentPage();
        }

        private void ResetCustomPetFlow()
        {
            customPetStep = 1;
            customPetProjectPath = "";
            customPetPhotoPaths.Clear();
            customPetPosePaths.Clear();
            customPetValidation = null;
            customPetShowAllProjects = false;
            customPetNameDraft = "我的伙伴";
            customPetSpeciesDraft = "宠物猫";
            SetCustomPetStatus("已开始新伙伴；之前的项目仍保留在本机。", false);
        }

        private void ChooseCustomPetPhotos()
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Title = "选择 1–3 张清晰的宠物照片",
                Filter = "宠物照片|*.png;*.jpg;*.jpeg;*.webp;*.bmp|所有文件|*.*",
                Multiselect = true
            };
            if (dialog.ShowDialog(this) != true) return;
            CustomPetReferenceValidationResult validation = CustomPetService.ValidateReferencePhotos(dialog.FileNames);
            if (!validation.Success)
            {
                SetCustomPetStatus(validation.Error + " " + validation.RecoveryHint, true);
                BuildCurrentPage();
                return;
            }
            customPetPhotoPaths.Clear();
            customPetPhotoPaths.AddRange(validation.Photos.Select(delegate(CustomPetPhotoInfo photo) { return photo.Path; }));
            SetCustomPetStatus("已检查 " + validation.Photos.Count + " 张照片；创建项目后只会复制到本机。", false);
            BuildCurrentPage();
        }

        private void ChooseCustomPetPose(string action)
        {
            OpenFileDialog dialog = new OpenFileDialog { Title = "选择 " + CustomPetPoseTitle(action) + " PNG", Filter = "透明 PNG|*.png", Multiselect = false };
            if (dialog.ShowDialog(this) != true) return;
            customPetPosePaths[action] = dialog.FileName;
            customPetValidation = null;
            SetCustomPetStatus("已选择 " + CustomPetPoseTitle(action) + "；凑齐四张后统一校验。", false);
            BuildCurrentPage();
        }

        private void RefreshCustomPetPosePaths(bool announce)
        {
            if (string.IsNullOrWhiteSpace(customPetProjectPath)) return;
            CustomPetProjectInfo info = app.CustomPets.GetProjectInfo(customPetProjectPath);
            if (!info.Success) { if (announce) SetCustomPetStatus(info.Error, true); return; }
            foreach (KeyValuePair<string, string> pair in info.GeneratedPaths)
                if (!customPetPosePaths.ContainsKey(pair.Key) || !File.Exists(customPetPosePaths[pair.Key])) customPetPosePaths[pair.Key] = pair.Value;
            if (announce) SetCustomPetStatus("已从 generated 识别 " + info.GeneratedCount + "/4 张图片。", info.GeneratedCount != 4);
        }

        private void ValidateCurrentCustomPetProject()
        {
            string project = customPetProjectPath;
            RunCustomPetTask("正在重新校验四张图片…", delegate
            {
                CustomPetValidationResult validation = app.CustomPets.ValidateProjectAssets(project);
                return new CustomPetResult { Success = validation.Success, Error = validation.Error, ProjectDirectory = project, Validation = validation };
            }, delegate(CustomPetResult result)
            {
                customPetValidation = result.Validation;
                SetCustomPetStatus(result.Success ? "质量门禁通过，可以启用。" : result.Error + " " + result.Validation.RecoveryHint, !result.Success);
            });
        }

        private void RunCustomPetTask(string busyMessage, Func<CustomPetResult> worker, Action<CustomPetResult> completed)
        {
            if (customPetBusy) return;
            customPetBusy = true;
            SetCustomPetStatus(busyMessage, false);
            BuildCurrentPage();
            ThreadPool.QueueUserWorkItem(delegate
            {
                CustomPetResult result;
                try { result = worker() ?? new CustomPetResult { Error = "操作没有返回结果。" }; }
                catch (Exception ex) { result = new CustomPetResult { Error = "操作失败：" + ex.Message }; }
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    customPetBusy = false;
                    try { completed(result); }
                    catch (Exception ex) { SetCustomPetStatus("界面更新失败：" + ex.Message, true); }
                    if (currentPage == "custom-pet") BuildCurrentPage();
                }));
            });
        }

        private void SetCustomPetStatus(string message, bool error)
        {
            customPetStatus = string.IsNullOrWhiteSpace(message) ? "等待下一步。" : message.Replace("\r", " ").Replace("\n", " ").Trim();
            customPetStatusError = error;
        }

        private Button CustomPetFolderButton(string text, string path)
        {
            Button button = Theme.SecondaryButton(text);
            button.Margin = new Thickness(0, 0, 8, 8);
            button.IsEnabled = !customPetBusy;
            button.Click += delegate { OpenCustomPetPath(path); };
            return button;
        }

        private void OpenCustomPetPath(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path) || (!Directory.Exists(path) && !File.Exists(path))) throw new FileNotFoundException("路径不存在。", path);
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
                SetCustomPetStatus("已打开：" + path, false);
            }
            catch (Exception ex) { SetCustomPetStatus("打开失败：" + ex.Message, true); }
            BuildCurrentPage();
        }

        private UIElement CustomPetNotice(string title, string description, bool error)
        {
            StackPanel stack = new StackPanel();
            stack.Children.Add(Theme.Text(title, 12.5, error ? Theme.Warning : Theme.Ink, FontWeights.Bold));
            TextBlock note = Theme.Text(description, 10.6, error ? Theme.Warning : Theme.Muted, FontWeights.Normal);
            note.Margin = new Thickness(0, 4, 0, 0);
            stack.Children.Add(note);
            Border card = Theme.Card(stack, 14, new Thickness(14, 11, 14, 11));
            card.Background = error ? Theme.WarningSoft : Theme.Brush("#FAF6F2");
            card.Margin = new Thickness(0, 0, 0, 8);
            return card;
        }

        private UIElement CustomPetPreview(string path, double size)
        {
            Border shell = new Border
            {
                Width = size,
                Height = size,
                Background = Theme.Brush("#F2ECE7"),
                CornerRadius = new CornerRadius(12),
                ClipToBounds = true
            };
            try
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) throw new FileNotFoundException();
                BitmapImage bitmap = new BitmapImage();
                using (FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.DecodePixelWidth = Math.Max(64, (int)Math.Ceiling(size * 2));
                    bitmap.StreamSource = stream;
                    bitmap.EndInit();
                    bitmap.Freeze();
                }
                Image image = new Image { Source = bitmap, Stretch = Stretch.Uniform, Margin = new Thickness(3) };
                RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
                shell.Child = image;
            }
            catch
            {
                TextBlock empty = Theme.Text("PNG", 10, Theme.Faint, FontWeights.Bold);
                empty.TextAlignment = TextAlignment.Center;
                shell.Child = empty;
            }
            return shell;
        }

        private static bool TryGetSinglePng(System.Windows.IDataObject data, out string path)
        {
            path = null;
            if (data == null || !data.GetDataPresent(DataFormats.FileDrop)) return false;
            string[] files = data.GetData(DataFormats.FileDrop) as string[];
            if (files == null || files.Length != 1 || !File.Exists(files[0])
                || !string.Equals(Path.GetExtension(files[0]), ".png", StringComparison.OrdinalIgnoreCase)) return false;
            path = Path.GetFullPath(files[0]);
            return true;
        }

        private static string CustomPetPoseTitle(string action)
        {
            if (action == "typing") return "工作";
            if (action == "happy") return "开心";
            if (action == "sleep") return "睡眠";
            return "日常";
        }

        private static string CustomPetPoseHint(string action)
        {
            if (action == "typing") return "同一角色轻敲键盘，不遮挡脸";
            if (action == "happy") return "开心但不改变主体尺度";
            if (action == "sleep") return "蜷睡且底部基线接近日常姿态";
            return "身份、中心和尺度的基准姿态";
        }

        private static string CustomPetFileSize(long bytes)
        {
            if (bytes >= 1024L * 1024) return Math.Round(bytes / 1024d / 1024d, 1) + " MB";
            if (bytes >= 1024) return Math.Round(bytes / 1024d) + " KB";
            return bytes + " B";
        }
    }
}
