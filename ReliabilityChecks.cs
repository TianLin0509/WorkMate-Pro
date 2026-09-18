using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Web.Script.Serialization;

namespace WorkMatePro
{
    internal static class ReliabilityChecks
    {
        private static void Check(bool ok, string name, List<string> log, ref int failures)
        {
            log.Add((ok ? "PASS " : "FAIL ") + "reliability-" + name);
            if (!ok) failures++;
        }

        public static void Run(List<string> log, ref int failures)
        {
            string original = Environment.GetEnvironmentVariable("WORKMATE_TEST_DIR");
            if (string.IsNullOrEmpty(original)) throw new InvalidOperationException("Tests require isolation.");
            string root = Path.Combine(original, "reliability");
            Directory.CreateDirectory(root);
            string path = Path.Combine(root, "data.json");
            JavaScriptSerializer json = new JavaScriptSerializer();
            WorkMateData good = new WorkMateData();
            good.Memos.Add(new MemoItem { Text = "recovered memo" });
            good.CompanionValue = 27;
            string backup = json.Serialize(good);
            try
            {
                Environment.SetEnvironmentVariable("WORKMATE_TEST_DIR", root);
                File.WriteAllText(path + ".bak", backup, Encoding.UTF8);
                File.WriteAllText(path, "{bad", Encoding.UTF8);
                DataStore recovered = new DataStore();
                Check(recovered.Data.Memos.Count == 1 && recovered.Data.CompanionValue == 27 && recovered.StorageNotice.Length > 0,
                    "bad-primary-recovers-valid-backup-with-notice", log, ref failures);
                recovered.Save();
                Check(File.ReadAllText(path + ".bak", Encoding.UTF8) == backup && new DataStore().Data.CompanionValue == 27,
                    "recovery-save-preserves-last-good-backup-and-restarts", log, ref failures);
                File.Delete(path);
                Check(new DataStore().Data.Memos.Count == 1, "missing-primary-recovers-backup", log, ref failures);
                File.WriteAllText(path, "{\"Memos\":[null]}", Encoding.UTF8);
                Check(new DataStore().Data.Memos.Count == 1, "invalid-structure-recovers-backup", log, ref failures);
                File.WriteAllText(path + ".bak", "{bad-backup", Encoding.UTF8);
                File.WriteAllText(path, "{bad-primary", Encoding.UTF8);
                DataStore empty = new DataStore();
                Check(empty.Data.Memos.Count == 0 && empty.StorageNotice.Contains("备份读取失败") && File.ReadAllText(path).Contains("bad-primary"),
                    "both-bad-preserved-and-visible", log, ref failures);
                empty.Save(); empty.Save();
                Check(Directory.GetFiles(root, "data.json.bak.corrupt-*").Any(p => File.ReadAllText(p).Contains("bad-backup")),
                    "both-bad-backup-retained-after-two-saves", log, ref failures);
                File.WriteAllText(path, backup, Encoding.UTF8);
                File.WriteAllText(path + ".bak", backup, Encoding.UTF8);
                DataStore store = new DataStore();
                string prior = File.ReadAllText(path);
                int notices = 0;
                store.StorageFailed += delegate { notices++; };
                bool failed = false;
                using (FileStream locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    store.Data.CompanionValue = 99;
                    try { store.Save(); } catch (IOException) { failed = true; }
                }
                Check(failed && notices == 1 && File.ReadAllText(path) == prior && Directory.GetFiles(root, "*.tmp-*").Length == 0,
                    "locked-save-preserves-original-signals-failure-cleans-temp", log, ref failures);
                File.SetAttributes(path, FileAttributes.ReadOnly);
                failed = false;
                try { store.Save(); } catch (IOException) { failed = true; }
                finally { File.SetAttributes(path, FileAttributes.Normal); }
                Check(failed && notices == 2 && File.ReadAllText(path) == prior, "readonly-save-preserves-original", log, ref failures);
                int memoCount = store.Data.Memos.Count;
                failed = false;
                using (FileStream locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    try { store.AddMemo("retry exactly once", "long", false, false); } catch (IOException) { failed = true; }
                }
                Check(failed && store.Data.Memos.Count == memoCount, "failed-memo-save-rolls-back-memory-insert", log, ref failures);
                store.AddMemo("retry exactly once", "long", false, false);
                Check(new DataStore().Data.Memos.Count(m => m.Text == "retry exactly once") == 1,
                    "retry-memo-save-persists-exactly-one-item", log, ref failures);

                store = new DataStore();
                DateTime now = DateTime.Now;
                InterruptionAnalytics analytics = new InterruptionAnalytics();
                store.StatisticsReset += analytics.Reset;
                analytics.Observe(store, BehaviorState.Typing, "开发工具", now);
                analytics.Observe(store, BehaviorState.Meeting, "沟通协作", now.AddSeconds(100));
                Check(analytics.CandidateActive, "interruption-candidate-established", log, ref failures);
                store.SetTrackingEnabled(false);
                store.AddStatSeconds("开发工具", 600);
                store.AddFlowSeconds(20);
                store.RecordInterruption("meeting", 60, now);
                analytics.Observe(store, BehaviorState.Typing, "开发工具", now.AddSeconds(170));
                Check(store.TodayActiveSeconds == 0 && store.TodayFlowSeconds == 0 && store.TodayInterruptions().Count == 0
                    && store.Data.HourlyActiveSeconds.Count == 0 && !analytics.CandidateActive,
                    "pause-blocks-all-statistics-and-discards-pending", log, ref failures);
                store.SetTrackingEnabled(true);
                analytics.Observe(store, BehaviorState.Typing, "开发工具", now.AddSeconds(180));
                Check(store.TodayInterruptions().Count == 0, "resume-does-not-count-paused-fragment", log, ref failures);
                store.AddStatSeconds("开发工具", 600);
                store.AddFlowSeconds(20);
                store.RecordInterruption("meeting", 60, now);
                int value = store.Data.CompanionValue;
                store.ClearTodayStats();
                Check(store.TodayActiveSeconds == 0 && store.TodayFlowSeconds == 0 && store.TodayInterruptions().Count == 0
                    && store.Data.HourlyActiveSeconds.Count == 0, "clear-includes-flow-hours-and-interruptions", log, ref failures);
                store.AddStatSeconds("开发工具", 600);
                Check(store.Data.CompanionValue == value, "clear-does-not-reissue-earned-rewards", log, ref failures);

                WorkMateData defaults = new WorkMateData();
                Check(!defaults.WeatherNetworkAllowed && !defaults.WeatherSentinelEnabled && !defaults.OutdoorAdvisorEnabled
                    && !defaults.DailyBriefingEnabled && !defaults.AmbientPresenceEnabled && !defaults.MeetingRadarEnabled
                    && defaults.WeatherCity == "", "new-install-explicit-opt-in", log, ref failures);
                File.WriteAllText(path, "{\"SchemaVersion\":13,\"WeatherSentinelEnabled\":true,\"OutdoorAdvisorEnabled\":false,\"MeetingRadarEnabled\":true,\"WeatherCity\":\"杭州\"}", Encoding.UTF8);
                DataStore legacy = new DataStore();
                Check(legacy.Data.WeatherSentinelEnabled && !legacy.Data.OutdoorAdvisorEnabled && legacy.Data.MeetingRadarEnabled
                    && legacy.Data.WeatherCity == "杭州" && !legacy.Data.WeatherNetworkAllowed,
                    "upgrade-preserves-preferences-requires-new-weather-consent", log, ref failures);
                legacy.Data.WeatherNetworkAllowed = true;
                legacy.Save();
                Check(new DataStore().Data.WeatherNetworkAllowed, "explicit-weather-consent-persists", log, ref failures);
                foreach (int invalid in new[] { -1, 0, 14, 241, int.MaxValue })
                {
                    legacy.Data.WorkBreakMinutes = invalid;
                    legacy.Save();
                    Check(new DataStore().Data.WorkBreakMinutes == 60, "invalid-work-break-normalized-" + invalid, log, ref failures);
                }
                OpenMeteoWeatherService weather = new OpenMeteoWeatherService();
                Check(!weather.GetAmbient("Shanghai", true).Success, "actual-weather-entry-rejects-missing-consent", log, ref failures);
                using (OutlookMeetingRadar radar = new OutlookMeetingRadar())
                {
                    radar.PollIfDue(true);
                    Check(radar.Status == "尚未读取" && radar.Snapshot().Count == 0, "calendar-no-com-without-consent", log, ref failures);
                    radar.SetEnabled(false);
                    Check(radar.Snapshot().Count == 0 && !radar.OutlookAvailable, "calendar-disable-clears-cache", log, ref failures);
                }

                List<MeetingInfo> meetings = Enumerable.Range(0, 700).Select(i => new MeetingInfo { Id = "past" + i, Start = now.AddDays(-10), End = now.AddDays(-9) }).ToList();
                meetings.Add(new MeetingInfo { Id = "recurring", Start = now.AddHours(1), End = now.AddHours(2) });
                meetings.Add(new MeetingInfo { Id = "recurring", Start = now.AddDays(1), End = now.AddDays(1).AddHours(1) });
                meetings.Add(new MeetingInfo { Id = "overnight", Start = now.AddDays(-1), End = now.AddHours(1) });
                meetings.Add(meetings[700]);
                List<MeetingInfo> selected = OutlookMeetingRadar.SelectUpcoming(meetings, now, now.AddDays(2));
                Check(selected.Count == 3 && selected[0].Id == "overnight", "calendar-filters-history-before-cap-keeps-occurrences-and-overlap", log, ref failures);
                int cursor = 0, released = 0, errors; bool capped;
                List<MeetingInfo> cursorResult = OutlookMeetingRadar.ReadCursor(() => (object)0, () => ++cursor < 3 ? (object)cursor : null,
                    item => (int)item == 0 ? null : new MeetingInfo { Id = item.ToString() }, item => released++, () => false, out errors, out capped);
                Check(cursorResult.Count == 2 && errors == 0 && released == 3 && !capped,
                    "calendar-cursor-advances-after-first-filtered-row", log, ref failures);
                cursor = 0; released = 0;
                cursorResult = OutlookMeetingRadar.ReadCursor(() => (object)0, () => ++cursor < 3 ? (object)cursor : null,
                    item => { if ((int)item == 0) throw new IOException("fixture COM row failure"); return new MeetingInfo(); },
                    item => released++, () => false, out errors, out capped);
                Check(cursorResult.Count == 2 && errors == 1 && released == 3,
                    "calendar-cursor-keeps-later-results-and-reports-errors", log, ref failures);
                double boundary = RawInputMonitor.MessageTimeSeconds(int.MaxValue - 4999, (ulong)int.MaxValue + 5001);
                Check(RawInputMonitor.ComputeIdle(boundary, ((ulong)int.MaxValue + 5001) / 1000.0) == 10,
                    "input-message-and-sample-share-clock-at-signed-wrap", log, ref failures);
                Check(RawInputMonitor.MessageTimeSeconds(-5000, 4294972296UL) == 4294962.296,
                    "input-event-clock-at-unsigned-wrap", log, ref failures);
                Check(RawInputMonitor.ComputeIdle(-20, -10) == 10 && RawInputMonitor.ComputeIdle(10, 5) == 0
                    && RawInputMonitor.ComputeIdle(2147480, -2147477.296) == 10, "idle-normal-negative-and-wrap", log, ref failures);
                Queue<double> queue = new Queue<double>(); queue.Enqueue(boundary - 70); queue.Enqueue(boundary);
                typeof(RawInputMonitor).GetMethod("Prune", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { queue, boundary + 10, 60.0 });
                Check(queue.Count == 1, "input-queue-prunes-on-shared-monotonic-clock", log, ref failures);
                System.Windows.Rect leftMonitor = new System.Windows.Rect(-1920, 0, 1920, 1080);
                System.Windows.Rect crossing = new System.Windows.Rect(-100, 50, 980, 700);
                System.Windows.Rect dragging = WorkbenchWindow.CalculateWorkAreaPlacement(crossing, leftMonitor, false);
                System.Windows.Rect releasedPlacement = WorkbenchWindow.CalculateWorkAreaPlacement(crossing,
                    new System.Windows.Rect(0, 0, 640, 460), true);
                Check(dragging.Left == -100 && dragging.Top == 50 && releasedPlacement.Left == 0 && releasedPlacement.Top == 0
                    && releasedPlacement.Width == 640 && releasedPlacement.Height == 460,
                    "workbench-negative-monitor-crossing-unclamped-until-release", log, ref failures);
            }
            finally
            {
                Environment.SetEnvironmentVariable("WORKMATE_TEST_DIR", original);
                PetCatalog.ConfigureCustomRoot(Path.Combine(original, "CustomPets"));
            }
        }
    }
}
