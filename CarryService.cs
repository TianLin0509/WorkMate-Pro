using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WorkMatePro
{
    /// <summary>“叼东西”数据层：文件只保存路径，文本只保存在本机 JSON；默认动作是复制，绝不移动或删除源文件。</summary>
    public sealed class CarryService
    {
        private readonly DataStore store;

        public CarryService(DataStore store) { this.store = store; }

        public int AddFiles(IEnumerable<string> paths)
        {
            int added = 0;
            foreach (string path in paths ?? new string[0])
            {
                if (!File.Exists(path) && !Directory.Exists(path)) continue;
                string full = Path.GetFullPath(path);
                string name = Path.GetFileName(full.TrimEnd(Path.DirectorySeparatorChar));
                store.AddCarryItem(Directory.Exists(full) ? "folder" : "file", full, name);
                added++;
            }
            return added;
        }

        public bool AddClipboardText()
        {
            try
            {
                if (!Clipboard.ContainsText()) return false;
                string text = Clipboard.GetText().Trim();
                if (text.Length == 0) return false;
                if (text.Length > 4000) text = text.Substring(0, 4000);
                string kind = Uri.IsWellFormedUriString(text, UriKind.Absolute) ? "link" : "text";
                store.AddCarryItem(kind, text, Formatters.Truncate(text.Replace("\r", " ").Replace("\n", " "), 32));
                return true;
            }
            catch { return false; }
        }

        public int CopyFilesTo(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return 0;
            int copied = 0;
            foreach (CarryItem item in store.Data.CarryItems.ToList())
            {
                if (item.Kind == "file" && File.Exists(item.Value))
                {
                    string target = UniquePath(Path.Combine(folder, Path.GetFileName(item.Value)), false);
                    File.Copy(item.Value, target, false);
                    copied++;
                }
                else if (item.Kind == "folder" && Directory.Exists(item.Value))
                {
                    string target = UniquePath(Path.Combine(folder, Path.GetFileName(item.Value.TrimEnd(Path.DirectorySeparatorChar))), true);
                    CopyDirectory(item.Value, target);
                    copied++;
                }
            }
            return copied;
        }

        private static void CopyDirectory(string source, string target)
        {
            Directory.CreateDirectory(target);
            foreach (string file in Directory.GetFiles(source)) File.Copy(file, Path.Combine(target, Path.GetFileName(file)), false);
            foreach (string directory in Directory.GetDirectories(source)) CopyDirectory(directory, Path.Combine(target, Path.GetFileName(directory)));
        }

        private static string UniquePath(string candidate, bool directory)
        {
            if (!PathExists(candidate)) return candidate;
            string parent = Path.GetDirectoryName(candidate);
            string fileName = Path.GetFileName(candidate);
            string stem = directory ? fileName : Path.GetFileNameWithoutExtension(fileName);
            string ext = directory ? "" : Path.GetExtension(fileName);
            for (int i = 2; i < 1000; i++)
            {
                string value = Path.Combine(parent, stem + " (" + i + ")" + ext);
                if (!PathExists(value)) return value;
            }
            throw new IOException("目标目录中同名文件过多");
        }

        private static bool PathExists(string value)
        {
            return File.Exists(value) || Directory.Exists(value);
        }
    }

    public sealed class CarryShelfWindow : Window
    {
        private readonly WorkMateApp app;
        private readonly StackPanel host;

        public CarryShelfWindow(WorkMateApp app)
        {
            this.app = app;
            Title = "WorkMate 小满叼着";
            Width = 570;
            Height = 480;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = true;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            WindowPrivacy.Bind(this, delegate { return app.Store.Data.HideFromCaptureEnabled; }, false);

            Grid layout = new Grid();
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition());
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid header = new Grid { Margin = new Thickness(2, 0, 2, 16) };
            header.ColumnDefinitions.Add(new ColumnDefinition());
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            StackPanel copy = new StackPanel();
            copy.Children.Add(Theme.Text("小满叼着", 22, Theme.Ink, FontWeights.Bold));
            TextBlock help = Theme.Text("把文件拖给小满，跨目录时再统一放下；默认只复制，不移动源文件。", 11, Theme.Muted, FontWeights.Normal);
            help.Margin = new Thickness(0, 4, 0, 0);
            copy.Children.Add(help);
            header.Children.Add(copy);
            Button close = Theme.GhostButton("关闭");
            close.Click += delegate { Hide(); };
            Grid.SetColumn(close, 1);
            header.Children.Add(close);
            layout.Children.Add(header);

            host = new StackPanel();
            ScrollViewer scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = host };
            Grid.SetRow(scroll, 1);
            layout.Children.Add(scroll);

            Grid actions = new Grid { Margin = new Thickness(0, 16, 0, 0) };
            actions.ColumnDefinitions.Add(new ColumnDefinition());
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            actions.ColumnDefinitions.Add(new ColumnDefinition());
            Button carryClipboard = Theme.SecondaryButton("叼住剪贴板文字 / 链接");
            carryClipboard.Click += delegate
            {
                if (app.Carry.AddClipboardText()) { app.Pet.PlayCarryAnimation(); BuildItems(); }
                else carryClipboard.Content = "剪贴板里没有文字";
            };
            actions.Children.Add(carryClipboard);
            Button putDown = Theme.PrimaryButton("把文件放到…");
            putDown.Click += delegate
            {
                using (System.Windows.Forms.FolderBrowserDialog dialog = new System.Windows.Forms.FolderBrowserDialog())
                {
                    dialog.Description = "选择小满放下文件的位置（源文件不会被移动）";
                    if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                    try
                    {
                        int count = app.Carry.CopyFilesTo(dialog.SelectedPath);
                        app.Pet.Celebrate("已经放下 " + count + " 件，源文件还在原处");
                    }
                    catch (Exception ex) { app.Pet.EventToast("没有放成功：" + Formatters.Truncate(ex.Message, 28)); }
                }
            };
            Grid.SetColumn(putDown, 2);
            actions.Children.Add(putDown);
            Grid.SetRow(actions, 2);
            layout.Children.Add(actions);

            Content = new Border
            {
                Background = Theme.Surface,
                BorderBrush = Theme.Line,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(25),
                Padding = new Thickness(24),
                Effect = Theme.Shadow(36, .2, 9),
                Child = layout,
                Margin = new Thickness(16)
            };
            Closing += delegate(object sender, System.ComponentModel.CancelEventArgs e) { if (!app.IsExiting) { e.Cancel = true; Hide(); } };
        }

        public void ShowShelf()
        {
            BuildItems();
            if (!IsVisible) Show();
            Activate();
            NativeMethods.ForceForeground(new System.Windows.Interop.WindowInteropHelper(this).Handle);
        }

        private void BuildItems()
        {
            host.Children.Clear();
            List<CarryItem> items = app.Store.Data.CarryItems.ToList();
            if (items.Count == 0)
            {
                Border empty = new Border { Background = Theme.SoftSurface, CornerRadius = new CornerRadius(16), Padding = new Thickness(20), Margin = new Thickness(0, 0, 0, 8) };
                empty.Child = Theme.Text("小满嘴里是空的。把文件、文件夹拖到她身上，或叼住一段剪贴板文字。", 12, Theme.Muted, FontWeights.Normal);
                host.Children.Add(empty);
                return;
            }
            foreach (CarryItem item in items)
            {
                Grid row = new Grid();
                row.ColumnDefinitions.Add(new ColumnDefinition());
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                StackPanel labels = new StackPanel();
                labels.Children.Add(Theme.Text(item.DisplayName, 12.5, Theme.Ink, FontWeights.SemiBold));
                string kind = item.Kind == "file" ? "文件" : item.Kind == "folder" ? "文件夹" : item.Kind == "link" ? "链接" : "文字";
                TextBlock detail = Theme.Text(kind + "  ·  " + Formatters.Truncate(item.Value, 54), 10.5, Theme.Faint, FontWeights.Normal);
                detail.Margin = new Thickness(0, 4, 0, 0);
                labels.Children.Add(detail);
                row.Children.Add(labels);
                StackPanel buttons = new StackPanel { Orientation = Orientation.Horizontal };
                if (item.Kind == "text" || item.Kind == "link")
                {
                    Button copy = Theme.GhostButton("复制");
                    copy.Click += delegate { try { Clipboard.SetText(item.Value); copy.Content = "已复制"; } catch { } };
                    buttons.Children.Add(copy);
                }
                else
                {
                    Button reveal = Theme.GhostButton("查看");
                    reveal.Click += delegate
                    {
                        try
                        {
                            if (File.Exists(item.Value)) Process.Start("explorer.exe", "/select,\"" + item.Value + "\"");
                            else if (Directory.Exists(item.Value)) Process.Start("explorer.exe", "\"" + item.Value + "\"");
                        }
                        catch { }
                    };
                    buttons.Children.Add(reveal);
                }
                Button remove = Theme.GhostButton("放下");
                remove.Foreground = Theme.Faint;
                remove.Click += delegate
                {
                    app.Store.RemoveCarryItem(item);
                    if (app.Store.Data.CarryItems.Count == 0) app.Pet.PlayCarryReleaseAnimation();
                    else app.Pet.RefreshPet();
                    BuildItems();
                };
                buttons.Children.Add(remove);
                Grid.SetColumn(buttons, 1);
                row.Children.Add(buttons);
                Border card = Theme.Card(row, 15, new Thickness(14, 11, 11, 11));
                card.Margin = new Thickness(0, 0, 0, 8);
                host.Children.Add(card);
            }
        }
    }
}
