using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace WorkMatePro
{
    public sealed class MeetingInfo
    {
        public string Id { get; set; }
        public string Subject { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public string Location { get; set; }
        public string Attendees { get; set; }
    }

    /// <summary>
    /// Outlook 本地会议雷达。所有 COM 访问都在独立 STA 线程完成；结果只驻留内存，不写会议正文到 data.json。
    /// </summary>
    public sealed class OutlookMeetingRadar : IDisposable
    {
        private readonly object sync = new object();
        private List<MeetingInfo> upcoming = new List<MeetingInfo>();
        private DateTime lastPoll = DateTime.MinValue;
        private volatile bool polling;
        private bool disposed;
        private volatile bool enabled;
        private volatile int generation;

        public string Status { get; private set; }
        public bool OutlookAvailable { get; private set; }
        public event EventHandler Updated;

        public OutlookMeetingRadar()
        {
            Status = "尚未读取";
        }

        public MeetingInfo NextMeeting
        {
            get
            {
                lock (sync)
                {
                    DateTime floor = DateTime.Now.AddMinutes(-10);
                    return upcoming.Where(delegate(MeetingInfo meeting) { return meeting.End >= floor; }).OrderBy(delegate(MeetingInfo meeting) { return meeting.Start; }).FirstOrDefault();
                }
            }
        }

        public List<MeetingInfo> Snapshot()
        {
            lock (sync) return new List<MeetingInfo>(upcoming);
        }

        public void PollIfDue(bool force)
        {
            if (!enabled || disposed || polling) return;
            if (!force && (DateTime.Now - lastPoll).TotalMinutes < 3) return;
            polling = true;
            lastPoll = DateTime.Now;
            int requestGeneration = generation;
            Thread worker = new Thread(delegate() { ReadCalendar(requestGeneration); });
            worker.IsBackground = true;
            worker.Name = "WorkMate-OutlookCalendar";
            worker.SetApartmentState(ApartmentState.STA);
            worker.Start();
        }

        public void SetEnabled(bool value)
        {
            lock (sync)
            {
                enabled = value;
                generation++;
                upcoming.Clear();
                OutlookAvailable = false;
                Status = value ? "尚未读取" : "未授权读取 Outlook";
                lastPoll = DateTime.MinValue;
            }
        }

        public static List<MeetingInfo> SelectUpcoming(IEnumerable<MeetingInfo> source, DateTime from, DateTime until)
        {
            return source.Where(m => m != null && m.End >= from && m.Start <= until && m.End >= m.Start)
                .GroupBy(m => (m.Id ?? "") + "|" + m.Start.ToString("o"))
                .Select(g => g.First()).OrderBy(m => m.Start).Take(20).ToList();
        }

        private void ReadCalendar(int requestGeneration)
        {
            object outlook = null, session = null, calendar = null, items = null, restricted = null;
            List<MeetingInfo> result = new List<MeetingInfo>();
            string status = "Outlook 暂不可用";
            bool available = false;
            try
            {
                if (!enabled || requestGeneration != generation) return;
                Type type = Type.GetTypeFromProgID("Outlook.Application");
                if (type == null) throw new InvalidOperationException("未安装 Outlook 桌面版");
                outlook = Activator.CreateInstance(type);
                dynamic app = outlook;
                session = app.GetNamespace("MAPI");
                dynamic ns = session;
                calendar = ns.GetDefaultFolder(9); // olFolderCalendar
                dynamic folder = calendar;
                items = folder.Items;
                dynamic collection = items;
                collection.Sort("[Start]", false);
                collection.IncludeRecurrences = true;
                DateTime from = DateTime.Now.AddMinutes(-10);
                DateTime until = DateTime.Now.AddDays(2);
                // Outlook Jet date syntax uses the current Windows regional short date/time.
                string filter = "[End] >= '" + from.ToString("g") + "' AND [Start] <= '" + until.ToString("g") + "'";
                restricted = collection.Restrict(filter);
                dynamic selected = restricted;
                int errors = 0;
                bool capped = false;
                result = ReadCursor(delegate { return (object)selected.GetFirst(); }, delegate { return (object)selected.GetNext(); },
                    delegate(object raw)
                    {
                        dynamic item = raw;
                        DateTime start = (DateTime)item.Start;
                        DateTime end = (DateTime)item.End;
                        if (start > until || end < from) return null;
                        if (end < start) throw new InvalidOperationException("会议结束时间早于开始时间。");
                        string subject = SafeString(delegate { return (string)item.Subject; }, "未命名会议");
                        string location = SafeString(delegate { return (string)item.Location; }, "");
                        string attendees = SafeString(delegate { return (string)item.RequiredAttendees; }, "");
                        string optional = SafeString(delegate { return (string)item.OptionalAttendees; }, "");
                        if (attendees.Length == 0) attendees = optional;
                        else if (optional.Length > 0) attendees += "; " + optional;
                        string id = SafeString(delegate { return (string)item.EntryID; }, subject) + "|" + start.ToString("o");
                        return new MeetingInfo
                        {
                            Id = id,
                            Subject = subject,
                            Start = start,
                            End = end,
                            Location = location,
                            Attendees = CompactAttendees(attendees)
                        };
                    }, Release, delegate { return !enabled || requestGeneration != generation; }, out errors, out capped);
                result = SelectUpcoming(result, from, until);
                available = true;
                status = errors > 0 || capped ? "日历读取不完整（失败 " + errors + (capped ? "，达到 500 条上限" : "") + "），请在 Outlook 核对"
                    : result.Count == 0 ? "未来两天没有会议" : "已同步 " + result.Count + " 场会议";
            }
            catch (Exception ex)
            {
                status = "Outlook 暂不可用：" + Formatters.Truncate(ex.Message, 42);
            }
            finally
            {
                Release(restricted); Release(items); Release(calendar); Release(session); Release(outlook);
                lock (sync)
                {
                    if (enabled && requestGeneration == generation)
                    {
                        if (available) upcoming = result;
                        OutlookAvailable = available;
                        Status = status;
                    }
                    polling = false;
                }
                EventHandler handler = Updated;
                if (handler != null) handler(this, EventArgs.Empty);
            }
        }

        private static string SafeString(Func<string> getter, string fallback)
        {
            try { return (getter() ?? "").Trim(); } catch { return fallback; }
        }

        internal static List<MeetingInfo> ReadCursor(Func<object> first, Func<object> next,
            Func<object, MeetingInfo> read, Action<object> release, Func<bool> cancelled, out int errors, out bool capped)
        {
            errors = 0;
            capped = false;
            List<MeetingInfo> result = new List<MeetingInfo>();
            object raw = first();
            for (int index = 0; raw != null; index++)
            {
                try
                {
                    if (cancelled()) throw new OperationCanceledException();
                    MeetingInfo meeting = read(raw);
                    if (meeting != null) result.Add(meeting);
                }
                catch (OperationCanceledException) { throw; }
                catch { errors++; }
                finally { release(raw); }
                raw = next();
                if (index >= 499 && raw != null) { capped = true; release(raw); break; }
            }
            return result;
        }

        private static string CompactAttendees(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "未提供";
            string[] parts = value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            List<string> clean = parts.Select(delegate(string part) { return part.Trim(); }).Where(delegate(string part) { return part.Length > 0; }).ToList();
            if (clean.Count <= 4) return string.Join("、", clean.ToArray());
            return string.Join("、", clean.Take(4).ToArray()) + " 等 " + clean.Count + " 人";
        }

        private static void Release(object value)
        {
            if (value == null) return;
            try { if (Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value); } catch { }
        }

        public void Dispose() { disposed = true; SetEnabled(false); }
    }

    public sealed class MeetingRadarWindow : Window
    {
        private readonly PetWindow pet;
        private readonly DispatcherTimer closeTimer;

        public MeetingRadarWindow(WorkMateApp app, PetWindow pet, MeetingInfo meeting)
        {
            this.pet = pet;
            Title = "WorkMate 会议雷达";
            Width = 390;
            Height = 205;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            ShowActivated = false;
            Topmost = true;
            WindowPrivacy.Bind(this, delegate { return app.Store.Data.HideFromCaptureEnabled; }, true);

            StackPanel content = new StackPanel();
            Grid head = new Grid();
            head.ColumnDefinitions.Add(new ColumnDefinition());
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            head.Children.Add(Theme.Text("会议雷达", 11, Theme.Accent, FontWeights.Bold));
            string time = meeting.Start.ToString("HH:mm") + " – " + meeting.End.ToString("HH:mm");
            TextBlock timeText = Theme.Text(time, 11, Theme.Muted, FontWeights.SemiBold);
            Grid.SetColumn(timeText, 1);
            head.Children.Add(timeText);
            content.Children.Add(head);
            TextBlock subject = Theme.Text(meeting.Subject, 17, Theme.Ink, FontWeights.Bold);
            subject.Margin = new Thickness(0, 8, 0, 10);
            subject.MaxHeight = 48;
            content.Children.Add(subject);
            content.Children.Add(InfoRow("地点", string.IsNullOrWhiteSpace(meeting.Location) ? "未提供" : meeting.Location));
            content.Children.Add(InfoRow("与会", meeting.Attendees));
            Border privacy = new Border { Background = Theme.SuccessSoft, CornerRadius = new CornerRadius(10), Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(0, 10, 0, 0) };
            privacy.Child = Theme.Text("来自本机 Outlook，仅在内存中显示", 10, Theme.Success, FontWeights.SemiBold);
            content.Children.Add(privacy);
            Content = new Border
            {
                Background = Theme.Surface,
                BorderBrush = Theme.Line,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(22),
                Padding = new Thickness(19, 16, 19, 16),
                Effect = Theme.Shadow(30, .2, 8),
                Child = content
            };
            Opacity = app.Store.Data.ReducedMotion ? 1 : 0;
            closeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(12) };
            closeTimer.Tick += delegate { closeTimer.Stop(); Close(); };
            MouseEnter += delegate { closeTimer.Stop(); };
            MouseLeave += delegate { closeTimer.Start(); };
        }

        private static UIElement InfoRow(string label, string value)
        {
            Grid row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.Children.Add(Theme.Text(label, 10.5, Theme.Faint, FontWeights.SemiBold));
            TextBlock body = Theme.Text(Formatters.Truncate(value, 60), 11, Theme.Muted, FontWeights.Normal);
            Grid.SetColumn(body, 1);
            row.Children.Add(body);
            return row;
        }

        public void ShowNearPet()
        {
            Rect area = WindowPlacement.WorkAreaFor(pet);
            Left = Math.Max(area.Left + 8, Math.Min(area.Right - Width - 8, pet.Left - Width + pet.Width));
            Top = Math.Max(area.Top + 8, pet.Top - Height - 10);
            Show();
            DoubleAnimation fade = new DoubleAnimation(1, TimeSpan.FromMilliseconds(180));
            fade.EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            BeginAnimation(OpacityProperty, fade);
            closeTimer.Start();
        }
    }
}
