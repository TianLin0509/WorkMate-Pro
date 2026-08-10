using System;
using System.Linq;

namespace WorkMatePro
{
    /// <summary>
    /// 低打扰备忘催办：全局最短间隔 4 小时、每条最短间隔 24 小时；
    /// 短期记录满 4 小时、长期记录满 48 小时才有资格。这里只做选择，不直接弹窗。
    /// </summary>
    public sealed class MemoNudgeService
    {
        public MemoItem TryPick(DataStore store, DateTime now, BehaviorState state, bool flowActive)
        {
            if (store == null || !store.Data.MemoNudgesEnabled || flowActive) return null;
            if (state == BehaviorState.Meeting || state == BehaviorState.Typing || state == BehaviorState.Watching) return null;
            DateTime globalLast;
            if (DateTime.TryParse(store.Data.LastMemoNudgeAt, out globalLast) && (now - globalLast).TotalHours < 4) return null;

            return store.Data.Memos
                .Where(delegate(MemoItem memo)
                {
                    if (memo.IsDone) return false;
                    DateTime created;
                    if (!DateTime.TryParse(memo.CreatedAt, out created)) return false;
                    double minAgeHours = memo.Term == "long" ? 48 : 4;
                    if ((now - created).TotalHours < minAgeHours) return false;
                    DateTime last;
                    if (DateTime.TryParse(memo.LastNudgedAt, out last) && (now - last).TotalHours < 24) return false;
                    return true;
                })
                .OrderBy(delegate(MemoItem memo) { DateTime dt; return DateTime.TryParse(memo.CreatedAt, out dt) ? dt : now; })
                .FirstOrDefault();
        }
    }

    /// <summary>
    /// 中断频次判定：先有至少 90 秒连续生产态，随后进入会议/离开/沟通，
    /// 且中断持续至少 45 秒并在 30 分钟内返回生产态，才记为一次。
    /// 90 秒内连续切换合并，避免应用抖动把一次中断拆成多次。
    /// </summary>
    public sealed class InterruptionAnalytics
    {
        private DateTime focusSince = DateTime.MinValue;
        private DateTime interruptedAt = DateTime.MinValue;
        private DateTime lastRecordedAt = DateTime.MinValue;
        private string interruptionKind = "";

        public bool CandidateActive { get { return interruptedAt != DateTime.MinValue; } }

        public void Observe(DataStore store, BehaviorState state, string category, DateTime now)
        {
            bool productive = state == BehaviorState.Typing || state == BehaviorState.Reading || state == BehaviorState.Thinking;
            string kind = KindFor(state, category);

            if (!CandidateActive)
            {
                if (productive)
                {
                    if (focusSince == DateTime.MinValue) focusSince = now;
                }
                else if (kind.Length > 0)
                {
                    if (focusSince != DateTime.MinValue && (now - focusSince).TotalSeconds >= 90)
                    {
                        interruptedAt = now;
                        interruptionKind = kind;
                    }
                    focusSince = DateTime.MinValue;
                }
                else focusSince = DateTime.MinValue;
                return;
            }

            if (kind.Length > 0)
            {
                // 会议优先级最高；一次中断里从聊天切到会议仍算一次会议中断。
                if (kind == "meeting") interruptionKind = kind;
                return;
            }

            if (!productive) return;
            int seconds = (int)Math.Round((now - interruptedAt).TotalSeconds);
            if (seconds >= 45 && seconds <= 30 * 60 && (now - lastRecordedAt).TotalSeconds >= 90)
            {
                store.RecordInterruption(interruptionKind, seconds, now);
                lastRecordedAt = now;
            }
            interruptedAt = DateTime.MinValue;
            interruptionKind = "";
            focusSince = now;
        }

        private static string KindFor(BehaviorState state, string category)
        {
            if (state == BehaviorState.Meeting) return "meeting";
            if (state == BehaviorState.Away) return "away";
            if (string.Equals(category, "沟通协作", StringComparison.OrdinalIgnoreCase)) return "communication";
            return "";
        }
    }
}
