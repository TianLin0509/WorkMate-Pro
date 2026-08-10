using System;
using System.Linq;

namespace WorkMatePro
{
    /// <summary>
    /// 中断恢复书签：被拉会/被叫走时自动记下"刚才在做"，回来时一句提醒 + 一键回到备忘录。
    /// 隐私口径：只记录用户自己的备忘正文与应用类别，不读窗口标题、不读任何内容。
    /// </summary>
    public sealed class BookmarkService
    {
        private string bookmarkText;
        private DateTime capturedAt = DateTime.MinValue;

        public bool HasBookmark { get { return bookmarkText != null; } }

        /// <summary>捕获当前"在做"：优先用户钉住的当前任务，再回退到第一条未完成备忘。</summary>
        public void Capture(DataStore store, string foregroundCategory)
        {
            MemoItem top = store.AnchorMemo() ?? store.Data.Memos.FirstOrDefault(delegate(MemoItem memo) { return !memo.IsDone; });
            if (top != null) bookmarkText = Formatters.Truncate(top.Text, 20);
            else bookmarkText = "在「" + (string.IsNullOrEmpty(foregroundCategory) ? "其他" : foregroundCategory) + "」里忙碌";
            capturedAt = DateTime.Now;
        }

        public string BuildMeetingResume(int meetingMinutes)
        {
            if (!HasBookmark) return null;
            string text = "会议结束（" + meetingMinutes + " 分钟）。刚才" + bookmarkText + "，继续吗？";
            Clear();
            return text;
        }

        public string BuildAwayResume(int awayMinutes)
        {
            if (!HasBookmark) return null;
            string text = "欢迎回来（离开 " + awayMinutes + " 分钟）。刚才" + bookmarkText + "，继续吗？";
            Clear();
            return text;
        }

        public void Clear()
        {
            bookmarkText = null;
            capturedAt = DateTime.MinValue;
        }
    }

    /// <summary>
    /// 把连续的“开会 / 离开”视为一次中断，避免两种状态互相切换时在无人或仍在会议中弹恢复提示。
    /// </summary>
    public sealed class InterruptionSession
    {
        private readonly BookmarkService bookmark = new BookmarkService();
        private DateTime startedAt = DateTime.MinValue;
        private bool includedMeeting;

        public bool Active { get { return startedAt != DateTime.MinValue; } }

        public void Begin(DataStore store, string foregroundCategory, bool meeting, DateTime now)
        {
            if (!Active)
            {
                bookmark.Capture(store, foregroundCategory);
                startedAt = now;
            }
            includedMeeting = includedMeeting || meeting;
        }

        public void Continue(bool meeting)
        {
            includedMeeting = includedMeeting || meeting;
        }

        public string End(DateTime now)
        {
            if (!Active) return null;
            int minutes = Math.Max(0, (int)Math.Round((now - startedAt).TotalMinutes));
            string text = null;
            if (includedMeeting && minutes >= 2) text = bookmark.BuildMeetingResume(minutes);
            else if (!includedMeeting && minutes >= 10) text = bookmark.BuildAwayResume(minutes);
            else bookmark.Clear();
            startedAt = DateTime.MinValue;
            includedMeeting = false;
            return text;
        }
    }
}
