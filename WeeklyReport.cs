using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WorkMatePro
{
    /// <summary>周报素材：周一到周日聚合完成事项、分类投入与心流时长，一键复制进周报。</summary>
    public static class WeeklyReport
    {
        private static readonly string[] WeekdayNames = { "周日", "周一", "周二", "周三", "周四", "周五", "周六" };

        public static DateTime WeekStart(DateTime day)
        {
            int diff = ((int)day.DayOfWeek + 6) % 7; // 周一为一周起点
            return day.Date.AddDays(-diff);
        }

        public static string BuildText(DataStore store, DateTime today)
        {
            DateTime weekStart = WeekStart(today);
            string weekEnd = weekStart.AddDays(6).ToString("M 月 d 日");
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(weekStart.ToString("M 月 d 日") + " - " + weekEnd + " · 本周小结");

            // 分类投入（周一到周日求和）
            Dictionary<string, int> categoryTotals = new Dictionary<string, int>();
            for (int i = 0; i < 7; i++)
            {
                Dictionary<string, int> day;
                if (store.Data.Stats.TryGetValue(weekStart.AddDays(i).ToString("yyyy-MM-dd"), out day))
                {
                    foreach (KeyValuePair<string, int> item in day)
                    {
                        int old;
                        categoryTotals.TryGetValue(item.Key, out old);
                        categoryTotals[item.Key] = old + item.Value;
                    }
                }
            }
            int totalActive = categoryTotals.Values.Sum();
            sb.AppendLine("活跃工作：" + Formatters.Duration(totalActive));
            if (categoryTotals.Count > 0)
            {
                List<string> parts = new List<string>();
                foreach (KeyValuePair<string, int> item in categoryTotals.OrderByDescending(delegate(KeyValuePair<string, int> kv) { return kv.Value; }).Take(4))
                    parts.Add(item.Key + " " + Formatters.Duration(item.Value));
                sb.AppendLine("主要投入：" + string.Join("、", parts.ToArray()));
            }
            int flow = store.WeekFlowSeconds(weekStart);
            if (flow > 0) sb.AppendLine("心流时间：" + Formatters.Duration(flow));
            int interruptionCount = 0;
            int interruptionSeconds = 0;
            for (int i = 0; i < 7; i++)
            {
                InterruptionDay day;
                if (store.Data.Interruptions.TryGetValue(weekStart.AddDays(i).ToString("yyyy-MM-dd"), out day))
                {
                    interruptionCount += day.Count;
                    interruptionSeconds += day.TotalSeconds;
                }
            }
            if (interruptionCount > 0) sb.AppendLine("有效打断：" + interruptionCount + " 次 · " + Formatters.Duration(interruptionSeconds));

            // 完成事项按天分组
            List<MemoItem> done = store.Data.Memos.Where(delegate(MemoItem memo)
            {
                if (!memo.IsDone || string.IsNullOrEmpty(memo.DoneAt)) return false;
                DateTime doneDay;
                return DateTime.TryParse(memo.DoneAt, out doneDay) && doneDay.Date >= weekStart && doneDay.Date < weekStart.AddDays(7);
            }).ToList();
            sb.AppendLine("已完成：" + done.Count + " 条记录");
            foreach (var group in done.GroupBy(delegate(MemoItem memo) { return DateTime.Parse(memo.DoneAt).Date; }).OrderBy(delegate(IGrouping<DateTime, MemoItem> g) { return g.Key; }))
            {
                string label = WeekdayNames[(int)group.Key.DayOfWeek];
                sb.AppendLine("  " + label + "（" + group.Count() + "）：" + string.Join("；", group.Select(delegate(MemoItem memo) { return memo.Text; }).Take(3).ToArray()));
            }
            sb.AppendLine("陪伴值：" + store.Data.CompanionValue + " · Lv." + store.Level);
            return sb.ToString().TrimEnd();
        }
    }
}
