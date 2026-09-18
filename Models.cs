using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.Script.Serialization;

namespace WorkMatePro
{
    public sealed class MemoItem
    {
        public string Id { get; set; }
        public string Text { get; set; }
        public string Term { get; set; }
        public bool IsDone { get; set; }
        public bool Important { get; set; }
        public bool Urgent { get; set; }
        public string CreatedAt { get; set; }
        public string DoneAt { get; set; }
        public string LastNudgedAt { get; set; }
        public bool DailyPriorityBonusAwarded { get; set; }

        public MemoItem()
        {
            Id = Guid.NewGuid().ToString("N");
            Text = "";
            Term = "short";
            CreatedAt = DateTime.Now.ToString("o");
            DoneAt = "";
            LastNudgedAt = "";
        }
    }

    public sealed class CarryItem
    {
        public string Id { get; set; }
        public string Kind { get; set; }
        public string Value { get; set; }
        public string DisplayName { get; set; }
        public string AddedAt { get; set; }

        public CarryItem()
        {
            Id = Guid.NewGuid().ToString("N");
            Kind = "file";
            Value = "";
            DisplayName = "";
            AddedAt = DateTime.Now.ToString("o");
        }
    }

    public sealed class InterruptionDay
    {
        public int Count { get; set; }
        public int MeetingCount { get; set; }
        public int AwayCount { get; set; }
        public int CommunicationCount { get; set; }
        public int TotalSeconds { get; set; }
    }

    public sealed class WorkMateData
    {
        public int SchemaVersion { get; set; }
        public string PetId { get; set; }
        public List<MemoItem> Memos { get; set; }
        public Dictionary<string, Dictionary<string, int>> Stats { get; set; }
        public Dictionary<string, int> DailyWorkAwards { get; set; }
        public int CompanionValue { get; set; }
        public bool TrackEnabled { get; set; }
        public bool FourQuadrantEnabled { get; set; }
        public bool AlwaysOnTop { get; set; }
        public bool ReducedMotion { get; set; }
        public bool StartWithWindows { get; set; }
        public bool FirstRun { get; set; }
        public double PetLeft { get; set; }
        public double PetTop { get; set; }
        public int PetSize { get; set; }
        /// <summary>
        /// 宠物相对 190 DIP 美术基准的显示比例。v1.10 默认 0.36，并继续结合当前屏幕工作区
        /// 等比折算；这样 4K/小屏看到的是相同占屏比例，而不是相同 DIP 常数。
        /// </summary>
        public double PetScaleRatio { get; set; }
        public string DockSide { get; set; }
        public string AnchorMemoId { get; set; }
        public Dictionary<string, int> FlowSeconds { get; set; }
        public string LastGreetDate { get; set; }
        public string FirstCompanionDate { get; set; }
        public string LastAnniversaryKey { get; set; }
        public string LastFarewellDate { get; set; }
        public bool BehaviorEnabled { get; set; }
        public bool EdgeSnapEnabled { get; set; }
        public bool FollowMonitorEnabled { get; set; }
        public bool PresentationGuardEnabled { get; set; }
        public bool HideFromCaptureEnabled { get; set; }
        public Dictionary<string, Dictionary<string, int>> HourlyActiveSeconds { get; set; }
        public Dictionary<string, InterruptionDay> Interruptions { get; set; }
        public List<CarryItem> CarryItems { get; set; }
        public bool MeetingRadarEnabled { get; set; }
        public bool MemoNudgesEnabled { get; set; }
        public bool StretchEnabled { get; set; }
        public string LastMemoNudgeAt { get; set; }
        public bool WorkBreakReminderEnabled { get; set; }
        public int WorkBreakMinutes { get; set; }
        public string WeatherCity { get; set; }
        public bool WeatherNetworkAllowed { get; set; }
        public bool WeatherSentinelEnabled { get; set; }
        public bool OutdoorAdvisorEnabled { get; set; }
        public bool DailyBriefingEnabled { get; set; }
        public int DailyBriefingHour { get; set; }
        public string LastWeatherAlertKey { get; set; }
        public string LastOutdoorAlertKey { get; set; }
        public string LastDailyBriefingDate { get; set; }
        public bool AmbientPresenceEnabled { get; set; }
        public string DailyPriorityDate { get; set; }
        public string LastPriorityPromptDate { get; set; }
        public string LastPriorityRewardDate { get; set; }
        public string LastPriorityRewardMemoId { get; set; }
        public string EnergyMode { get; set; }
        public string EnergyModeDate { get; set; }

        public WorkMateData()
        {
            SchemaVersion = 13;
            PetId = "01-cat";
            Memos = new List<MemoItem>();
            Stats = new Dictionary<string, Dictionary<string, int>>();
            DailyWorkAwards = new Dictionary<string, int>();
            CompanionValue = 0;
            TrackEnabled = true;
            FourQuadrantEnabled = false;
            AlwaysOnTop = true;
            ReducedMotion = false;
            StartWithWindows = false;
            FirstRun = true;
            PetLeft = -100000;
            PetTop = -100000;
            PetSize = 190;
            PetScaleRatio = ResponsivePetSizing.DefaultScaleRatio;
            DockSide = "";
            BehaviorEnabled = true;
            EdgeSnapEnabled = true;
            FollowMonitorEnabled = true;
            PresentationGuardEnabled = true;
            HideFromCaptureEnabled = false;
            HourlyActiveSeconds = new Dictionary<string, Dictionary<string, int>>();
            Interruptions = new Dictionary<string, InterruptionDay>();
            CarryItems = new List<CarryItem>();
            MeetingRadarEnabled = false;
            MemoNudgesEnabled = true;
            StretchEnabled = true;
            LastMemoNudgeAt = "";
            WorkBreakReminderEnabled = true;
            WorkBreakMinutes = 60;
            WeatherCity = "";
            WeatherSentinelEnabled = false;
            OutdoorAdvisorEnabled = false;
            DailyBriefingEnabled = false;
            DailyBriefingHour = 9;
            LastWeatherAlertKey = "";
            LastOutdoorAlertKey = "";
            LastDailyBriefingDate = "";
            AmbientPresenceEnabled = false;
            DailyPriorityDate = "";
            LastPriorityPromptDate = "";
            LastPriorityRewardDate = "";
            LastPriorityRewardMemoId = "";
            EnergyMode = "steady";
            EnergyModeDate = "";
            AnchorMemoId = "";
            FlowSeconds = new Dictionary<string, int>();
            LastGreetDate = "";
            FirstCompanionDate = "";
            LastAnniversaryKey = "";
            LastFarewellDate = "";
        }
    }

    public sealed class DataStore
    {
        private readonly object sync = new object();
        private readonly JavaScriptSerializer serializer = new JavaScriptSerializer();
        private bool normalizeRequiresSave;
        private bool preserveBackup;
        public string StorageNotice { get; private set; }
        public event Action<string> StorageFailed;
        public event Action StatisticsReset;

        public WorkMateData Data { get; private set; }
        public int LoadedSchemaVersion { get; private set; }
        public string RootDirectory { get; private set; }
        public string DataPath { get { return Path.Combine(RootDirectory, "data.json"); } }
        public event EventHandler Changed;

        public DataStore()
        {
            string testRoot = Environment.GetEnvironmentVariable("WORKMATE_TEST_DIR");
            RootDirectory = string.IsNullOrWhiteSpace(testRoot)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WorkMatePro")
                : testRoot;
            PetCatalog.ConfigureCustomRoot(Path.Combine(RootDirectory, "CustomPets"));
            serializer.MaxJsonLength = 16 * 1024 * 1024;
            Load();
        }

        public void Load()
        {
            lock (sync)
            {
                Data = null;
                StorageNotice = "";
                preserveBackup = false;
                try
                {
                    if (File.Exists(DataPath))
                    {
                        string json = File.ReadAllText(DataPath, Encoding.UTF8);
                        Data = ReadData(json);
                        if (Data == null) throw new InvalidDataException("数据文件内容为空。");
                    }
                }
                catch (Exception ex)
                {
                    string bad = DataPath + ".corrupt-" + Guid.NewGuid().ToString("N");
                    StorageNotice = "主数据读取失败：" + ex.Message;
                    try { if (File.Exists(DataPath)) File.Copy(DataPath, bad, false); }
                    catch (Exception copyError) { StorageNotice += "；损坏副本保留失败：" + copyError.Message; }
                    Data = null;
                    preserveBackup = true;
                }
                if (Data == null && File.Exists(DataPath + ".bak"))
                {
                    try
                    {
                        Data = ReadData(File.ReadAllText(DataPath + ".bak", Encoding.UTF8));
                        if (Data == null) throw new InvalidDataException("备份内容为空。");
                        StorageNotice += "；已从最后有效备份恢复，原备份保留。";
                        preserveBackup = true;
                    }
                    catch (Exception ex)
                    {
                        StorageNotice += "；备份读取失败：" + ex.Message;
                        try { File.Copy(DataPath + ".bak", DataPath + ".bak.corrupt-" + Guid.NewGuid().ToString("N"), false); }
                        catch (Exception copyError) { StorageNotice += "；损坏备份保留失败：" + copyError.Message; }
                    }
                }
                if (Data == null && StorageNotice.Length > 0) StorageNotice += "；暂以空白数据启动，原文件保留，请先检查备份。";
                if (Data == null) Data = new WorkMateData();
                normalizeRequiresSave = false;
                Normalize();
                // 迁移不是只存在内存里：首次启动新版本就把尺寸比例与 schema 原子落盘。
                if (!preserveBackup && (LoadedSchemaVersion != Data.SchemaVersion || normalizeRequiresSave)) Save();
            }
        }

        private WorkMateData ReadData(string json)
        {
            WorkMateData value = serializer.Deserialize<WorkMateData>(json);
            if (value == null || (value.Memos != null && value.Memos.Any(item => item == null))
                || (value.CarryItems != null && value.CarryItems.Any(item => item == null))
                || (value.Stats != null && value.Stats.Values.Any(item => item == null))
                || (value.HourlyActiveSeconds != null && value.HourlyActiveSeconds.Values.Any(item => item == null))
                || (value.Interruptions != null && value.Interruptions.Values.Any(item => item == null)))
                throw new InvalidDataException("数据结构无效。");
            return value;
        }

        private void Normalize()
        {
            int loadedSchemaVersion = Data.SchemaVersion;
            LoadedSchemaVersion = loadedSchemaVersion;
            if (Data.Memos == null) Data.Memos = new List<MemoItem>();
            if (Data.Stats == null) Data.Stats = new Dictionary<string, Dictionary<string, int>>();
            if (Data.DailyWorkAwards == null) Data.DailyWorkAwards = new Dictionary<string, int>();
            if (Data.FlowSeconds == null) Data.FlowSeconds = new Dictionary<string, int>();
            if (Data.HourlyActiveSeconds == null) Data.HourlyActiveSeconds = new Dictionary<string, Dictionary<string, int>>();
            if (Data.Interruptions == null) Data.Interruptions = new Dictionary<string, InterruptionDay>();
            if (Data.CarryItems == null) Data.CarryItems = new List<CarryItem>();
            if (Data.LastMemoNudgeAt == null) Data.LastMemoNudgeAt = "";
            if (Data.WeatherCity == null) Data.WeatherCity = "";
            if (Data.LastWeatherAlertKey == null) Data.LastWeatherAlertKey = "";
            if (Data.LastOutdoorAlertKey == null) Data.LastOutdoorAlertKey = "";
            if (Data.LastDailyBriefingDate == null) Data.LastDailyBriefingDate = "";
            if (Data.AnchorMemoId == null) Data.AnchorMemoId = "";
            if (Data.DailyPriorityDate == null) Data.DailyPriorityDate = "";
            if (Data.LastPriorityPromptDate == null) Data.LastPriorityPromptDate = "";
            if (Data.LastPriorityRewardDate == null) Data.LastPriorityRewardDate = "";
            if (Data.LastPriorityRewardMemoId == null) Data.LastPriorityRewardMemoId = "";
            if (Data.EnergyMode == null) Data.EnergyMode = "steady";
            if (Data.EnergyModeDate == null) Data.EnergyModeDate = "";
            if (Data.LastGreetDate == null) Data.LastGreetDate = "";
            if (Data.LastAnniversaryKey == null) Data.LastAnniversaryKey = "";
            if (Data.LastFarewellDate == null) Data.LastFarewellDate = "";
            if (string.IsNullOrWhiteSpace(Data.FirstCompanionDate))
            {
                DateTime firstSeen = File.Exists(DataPath) ? File.GetCreationTime(DataPath).Date : DateTime.Today;
                if (firstSeen.Year < 2020 || firstSeen > DateTime.Today) firstSeen = DateTime.Today;
                Data.FirstCompanionDate = firstSeen.ToString("yyyy-MM-dd");
            }
            if (DockGeometry.ParsePersistedEdge(Data.DockSide) == DockEdge.None) Data.DockSide = "";
            if (string.IsNullOrEmpty(Data.PetId) || PetCatalog.Find(Data.PetId) == null) Data.PetId = "01-cat";
            if (Data.PetSize != 190 && Data.PetSize != 240 && Data.PetSize != 290) Data.PetSize = 190;
            if (loadedSchemaVersion < 8 || Data.PetScaleRatio < 0.35 || Data.PetScaleRatio > 1.20)
                Data.PetScaleRatio = ResponsivePetSizing.ComfortScaleRatio;
            if (loadedSchemaVersion < 9)
            {
                // v1.9 的三档靠 190/240/290 基准值表达。v1.10 统一回 190 美术基准，
                // 再把旧档位映射成 0.36/0.60/0.85；用户当前的 190 档因此按要求再缩小 40%。
                if (Data.PetSize <= 190) Data.PetScaleRatio = ResponsivePetSizing.CompactScaleRatio;
                else if (Data.PetSize <= 240) Data.PetScaleRatio = ResponsivePetSizing.ComfortScaleRatio;
                else Data.PetScaleRatio = ResponsivePetSizing.LargeScaleRatio;
                Data.PetSize = 190;
            }
            else
            {
                Data.PetSize = 190;
                Data.PetScaleRatio = ResponsivePetSizing.NearestPreset(Data.PetScaleRatio);
            }
            foreach (MemoItem memo in Data.Memos)
            {
                if (string.IsNullOrEmpty(memo.Id)) memo.Id = Guid.NewGuid().ToString("N");
                if (memo.Text == null) memo.Text = "";
                if (memo.Term != "long") memo.Term = "short";
                if (string.IsNullOrEmpty(memo.CreatedAt)) memo.CreatedAt = DateTime.Now.ToString("o");
                if (memo.DoneAt == null) memo.DoneAt = "";
                if (memo.LastNudgedAt == null) memo.LastNudgedAt = "";
            }
            // v1.2/v1.3 曾默认开启捕获排除；在远程桌面/部分共享链路中会让用户自己也看不见。
            // v5 改为显式 opt-in，并为旧数据执行一次安全迁移。
            if (loadedSchemaVersion < 5) Data.HideFromCaptureEnabled = false;
            if (loadedSchemaVersion < 7)
            {
                // Local-only legacy defaults; Outlook requires an explicit stored choice.
                Data.MemoNudgesEnabled = true;
                Data.StretchEnabled = true;
            }
            if (loadedSchemaVersion < 11)
            {
                // Preserve the historical local work-break defaults.
                Data.WorkBreakReminderEnabled = true;
                Data.WorkBreakMinutes = 60;
            }
            if (loadedSchemaVersion < 12)
            {
                // Preserve explicit weather choices; missing flags remain opt-in.
                Data.DailyBriefingHour = 9;
                Data.LastWeatherAlertKey = "";
                Data.LastOutdoorAlertKey = "";
                Data.LastDailyBriefingDate = "";
            }
            if (loadedSchemaVersion < 13)
            {
                // Preserve explicit ambient choices; reset legacy daily-priority state.
                Data.AnchorMemoId = "";
                Data.DailyPriorityDate = "";
                Data.LastPriorityPromptDate = "";
                Data.LastPriorityRewardDate = "";
                Data.LastPriorityRewardMemoId = "";
                Data.EnergyMode = "steady";
                Data.EnergyModeDate = "";
            }
            if (Data.WorkBreakMinutes < 15 || Data.WorkBreakMinutes > 240) Data.WorkBreakMinutes = 60;
            if (Data.DailyBriefingHour < 6 || Data.DailyBriefingHour > 12) Data.DailyBriefingHour = 9;
            Data.WeatherCity = (Data.WeatherCity ?? "").Trim();
            if (Data.WeatherCity.Length > 50) Data.WeatherCity = Data.WeatherCity.Substring(0, 50);
            if (Data.EnergyMode != "low" && Data.EnergyMode != "high") Data.EnergyMode = "steady";
            if (Data.DailyPriorityDate.Length > 0 && Data.DailyPriorityDate != DateTime.Today.ToString("yyyy-MM-dd"))
            {
                Data.AnchorMemoId = "";
                Data.DailyPriorityDate = "";
                normalizeRequiresSave = true;
            }
            if (Data.EnergyModeDate.Length > 0 && Data.EnergyModeDate != DateTime.Today.ToString("yyyy-MM-dd"))
            {
                Data.EnergyMode = "steady";
                Data.EnergyModeDate = "";
                normalizeRequiresSave = true;
            }
            Data.SchemaVersion = 13;
        }

        public void Save()
        {
            lock (sync)
            {
                string temp = DataPath + ".tmp-" + Guid.NewGuid().ToString("N");
                try
                {
                    Directory.CreateDirectory(RootDirectory);
                    Data.SchemaVersion = 13;
                    string json = serializer.Serialize(Data);
                    using (FileStream stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        byte[] bytes = new UTF8Encoding(false).GetBytes(json);
                        stream.Write(bytes, 0, bytes.Length);
                        stream.Flush(true);
                    }
                    if (File.Exists(DataPath))
                    {
                        string backup = preserveBackup ? null : DataPath + ".bak";
                        File.Replace(temp, DataPath, backup, true);
                    }
                    else File.Move(temp, DataPath);
                    preserveBackup = false;
                }
                catch (Exception ex)
                {
                    StorageNotice = "保存失败，原数据未替换。本次修改尚未保存：" + ex.Message;
                    Action<string> failed = StorageFailed;
                    if (failed != null) failed(StorageNotice);
                    throw new IOException(StorageNotice, ex);
                }
                finally { try { if (File.Exists(temp)) File.Delete(temp); } catch { } }
            }
            RaiseChanged();
        }

        public MemoItem AddMemo(string text, string term, bool important, bool urgent)
        {
            string clean = (text ?? "").Trim();
            if (clean.Length == 0) return null;
            MemoItem memo = new MemoItem();
            memo.Text = clean;
            memo.Term = term == "long" ? "long" : "short";
            memo.Important = important;
            memo.Urgent = urgent;
            Data.Memos.Insert(0, memo);
            try { Save(); }
            catch { Data.Memos.Remove(memo); throw; }
            return memo;
        }

        public void SetMemoDone(MemoItem memo, bool done)
        {
            if (memo == null || memo.IsDone == done) return;
            string today = DateTime.Today.ToString("yyyy-MM-dd");
            string previousDoneAt = memo.DoneAt ?? "";
            bool completesDailyPriority = done && memo.Id == Data.AnchorMemoId
                && Data.DailyPriorityDate == today && Data.LastPriorityRewardDate != today;
            memo.IsDone = done;
            memo.DoneAt = done ? DateTime.Now.ToString("o") : "";
            if (done) AddCompanionValue(4);
            else Data.CompanionValue = Math.Max(0, Data.CompanionValue - 4);
            if (!done && memo.DailyPriorityBonusAwarded)
            {
                Data.CompanionValue = Math.Max(0, Data.CompanionValue - 2);
                memo.DailyPriorityBonusAwarded = false;
                if (Data.LastPriorityRewardMemoId == memo.Id
                    && Data.LastPriorityRewardDate.Length > 0 && previousDoneAt.StartsWith(Data.LastPriorityRewardDate))
                {
                    Data.LastPriorityRewardDate = "";
                    Data.LastPriorityRewardMemoId = "";
                }
            }
            if (done && memo.Id == Data.AnchorMemoId)
            {
                if (completesDailyPriority)
                {
                    Data.CompanionValue += 2;
                    memo.DailyPriorityBonusAwarded = true;
                    Data.LastPriorityRewardDate = today;
                    Data.LastPriorityRewardMemoId = memo.Id;
                }
                Data.AnchorMemoId = "";
                Data.DailyPriorityDate = "";
            }
            Save();
        }

        public void DeleteMemo(MemoItem memo)
        {
            if (memo == null) return;
            if (memo.Id == Data.AnchorMemoId)
            {
                Data.AnchorMemoId = "";
                Data.DailyPriorityDate = "";
            }
            Data.Memos.Remove(memo);
            Save();
        }

        public void AddCompanionValue(int amount)
        {
            if (amount <= 0) return;
            Data.CompanionValue += amount;
        }

        public int Level { get { return Growth.LevelFor(Data.CompanionValue); } }

        public int TodayActiveSeconds
        {
            get
            {
                Dictionary<string, int> day;
                if (!Data.Stats.TryGetValue(DateTime.Today.ToString("yyyy-MM-dd"), out day)) return 0;
                return day.Values.Sum();
            }
        }

        public int TodayCompletedCount
        {
            get
            {
                string today = DateTime.Today.ToString("yyyy-MM-dd");
                return Data.Memos.Count(delegate(MemoItem memo)
                {
                    return memo.IsDone && !string.IsNullOrEmpty(memo.DoneAt) && memo.DoneAt.StartsWith(today);
                });
            }
        }

        public Dictionary<string, int> TodayStats()
        {
            Dictionary<string, int> day;
            if (Data.Stats.TryGetValue(DateTime.Today.ToString("yyyy-MM-dd"), out day))
                return new Dictionary<string, int>(day);
            return new Dictionary<string, int>();
        }

        public void ClearTodayStats()
        {
            string today = DateTime.Today.ToString("yyyy-MM-dd");
            Data.Stats.Remove(today);
            Data.FlowSeconds.Remove(today);
            Data.HourlyActiveSeconds.Remove(today);
            Data.Interruptions.Remove(today);
            if (StatisticsReset != null) StatisticsReset();
            Save();
        }

        public void SetTrackingEnabled(bool enabled)
        {
            Data.TrackEnabled = enabled;
            if (!enabled && StatisticsReset != null) StatisticsReset();
            Save();
        }

        public void AddStatSeconds(string category, int seconds)
        {
            if (!Data.TrackEnabled || seconds <= 0) return;
            string today = DateTime.Today.ToString("yyyy-MM-dd");
            Dictionary<string, int> day;
            if (!Data.Stats.TryGetValue(today, out day))
            {
                day = new Dictionary<string, int>();
                Data.Stats[today] = day;
            }
            int old;
            day.TryGetValue(category, out old);
            day[category] = old + seconds;

            Dictionary<string, int> hourly;
            if (!Data.HourlyActiveSeconds.TryGetValue(today, out hourly))
            {
                hourly = new Dictionary<string, int>();
                Data.HourlyActiveSeconds[today] = hourly;
            }
            string hour = DateTime.Now.Hour.ToString("00");
            int hourOld;
            hourly.TryGetValue(hour, out hourOld);
            hourly[hour] = hourOld + seconds;

            int active = day.Values.Sum();
            int earned = Math.Min(12, active / 600);
            int awarded;
            Data.DailyWorkAwards.TryGetValue(today, out awarded);
            if (earned > awarded)
            {
                Data.CompanionValue += earned - awarded;
                Data.DailyWorkAwards[today] = earned;
            }
        }

        private void RaiseChanged()
        {
            EventHandler handler = Changed;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        // ---------------- 当前任务锚点（v1.5）----------------

        public MemoItem AnchorMemo()
        {
            if (string.IsNullOrEmpty(Data.AnchorMemoId)) return null;
            return Data.Memos.FirstOrDefault(delegate(MemoItem memo) { return memo.Id == Data.AnchorMemoId && !memo.IsDone; });
        }

        public void SetAnchor(string memoId)
        {
            string requested = memoId ?? "";
            MemoItem target = Data.Memos.FirstOrDefault(delegate(MemoItem memo) { return memo.Id == requested && !memo.IsDone; });
            Data.AnchorMemoId = target == null ? "" : target.Id;
            Data.DailyPriorityDate = string.IsNullOrEmpty(Data.AnchorMemoId) ? "" : DateTime.Today.ToString("yyyy-MM-dd");
            Save();
        }

        public bool IsDailyPriority(MemoItem memo)
        {
            return memo != null && memo.Id == Data.AnchorMemoId
                && Data.DailyPriorityDate == DateTime.Today.ToString("yyyy-MM-dd");
        }

        public bool ClearExpiredDailyPriority(DateTime now)
        {
            string today = now.ToString("yyyy-MM-dd");
            if (Data.DailyPriorityDate.Length == 0 || Data.DailyPriorityDate == today) return false;
            Data.AnchorMemoId = "";
            Data.DailyPriorityDate = "";
            Save();
            return true;
        }

        public string TodayEnergyMode
        {
            get
            {
                return Data.EnergyModeDate == DateTime.Today.ToString("yyyy-MM-dd")
                    ? CompanionEnergyPolicy.Normalize(Data.EnergyMode) : "steady";
            }
        }

        public bool ClearExpiredEnergyMode(DateTime now)
        {
            string today = now.ToString("yyyy-MM-dd");
            if (Data.EnergyModeDate.Length == 0 || Data.EnergyModeDate == today) return false;
            Data.EnergyMode = "steady";
            Data.EnergyModeDate = "";
            Save();
            return true;
        }

        public void SetTodayEnergyMode(string mode)
        {
            Data.EnergyMode = CompanionEnergyPolicy.Normalize(mode);
            Data.EnergyModeDate = DateTime.Today.ToString("yyyy-MM-dd");
            Save();
        }

        /// <summary>完成当前锚点任务，返回被完成的备忘（无锚点返回 null）。</summary>
        public MemoItem CompleteAnchor()
        {
            MemoItem anchor = AnchorMemo();
            if (anchor == null) { Data.AnchorMemoId = ""; Data.DailyPriorityDate = ""; Save(); return null; }
            SetMemoDone(anchor, true);
            return anchor;
        }

        // ---------------- 心流时长（不触发工作奖励，单独记账）----------------

        public void AddFlowSeconds(int seconds)
        {
            if (!Data.TrackEnabled || seconds <= 0) return;
            string today = DateTime.Today.ToString("yyyy-MM-dd");
            int old;
            Data.FlowSeconds.TryGetValue(today, out old);
            Data.FlowSeconds[today] = old + seconds;
        }

        public int TodayFlowSeconds
        {
            get
            {
                int value;
                Data.FlowSeconds.TryGetValue(DateTime.Today.ToString("yyyy-MM-dd"), out value);
                return value;
            }
        }

        public int WeekFlowSeconds(DateTime weekStart)
        {
            int total = 0;
            for (int i = 0; i < 7; i++)
            {
                int value;
                if (Data.FlowSeconds.TryGetValue(weekStart.AddDays(i).ToString("yyyy-MM-dd"), out value)) total += value;
            }
            return total;
        }

        public void AddCarryItem(string kind, string value, string displayName)
        {
            string clean = (value ?? "").Trim();
            if (clean.Length == 0) return;
            CarryItem existing = Data.CarryItems.FirstOrDefault(delegate(CarryItem item)
            {
                return string.Equals(item.Kind, kind, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(item.Value, clean, StringComparison.OrdinalIgnoreCase);
            });
            if (existing != null) Data.CarryItems.Remove(existing);
            CarryItem carry = new CarryItem { Kind = kind ?? "file", Value = clean, DisplayName = displayName ?? clean };
            Data.CarryItems.Insert(0, carry);
            while (Data.CarryItems.Count > 8) Data.CarryItems.RemoveAt(Data.CarryItems.Count - 1);
            Save();
        }

        public void RemoveCarryItem(CarryItem item)
        {
            if (item == null) return;
            Data.CarryItems.Remove(item);
            Save();
        }

        public void MarkMemoNudged(MemoItem memo, DateTime now)
        {
            if (memo == null) return;
            memo.LastNudgedAt = now.ToString("o");
            Data.LastMemoNudgeAt = now.ToString("o");
            Save();
        }

        public void RecordInterruption(string kind, int seconds, DateTime now)
        {
            if (!Data.TrackEnabled) return;
            string dayKey = now.ToString("yyyy-MM-dd");
            InterruptionDay day;
            if (!Data.Interruptions.TryGetValue(dayKey, out day))
            {
                day = new InterruptionDay();
                Data.Interruptions[dayKey] = day;
            }
            day.Count++;
            day.TotalSeconds += Math.Max(0, seconds);
            if (kind == "meeting") day.MeetingCount++;
            else if (kind == "communication") day.CommunicationCount++;
            else day.AwayCount++;
            Save();
        }

        public InterruptionDay TodayInterruptions()
        {
            InterruptionDay day;
            return Data.Interruptions.TryGetValue(DateTime.Today.ToString("yyyy-MM-dd"), out day) ? day : new InterruptionDay();
        }
    }

    public static class Growth
    {
        public static int ThresholdForLevel(int level)
        {
            if (level <= 1) return 0;
            int total = 0;
            for (int current = 2; current <= level; current++) total += 40 + (current - 2) * 15;
            return total;
        }

        public static int LevelFor(int value)
        {
            int level = 1;
            while (level < 99 && value >= ThresholdForLevel(level + 1)) level++;
            return level;
        }

        public static double LevelProgress(int value)
        {
            int level = LevelFor(value);
            int start = ThresholdForLevel(level);
            int end = ThresholdForLevel(level + 1);
            if (end <= start) return 0;
            return Math.Max(0, Math.Min(1, (value - start) / (double)(end - start)));
        }
    }

    public sealed class PetDefinition
    {
        public string Id;
        public string Name;
        public string Species;
        public string Accent;
        public string Tagline;
        public bool IsCustom;
        public string AssetRoot;

        public PetDefinition(string id, string name, string species, string accent, string tagline)
            : this(id, name, species, accent, tagline, false, "")
        {
        }

        public PetDefinition(string id, string name, string species, string accent, string tagline, bool isCustom, string assetRoot)
        {
            Id = id;
            Name = name;
            Species = species;
            Accent = accent;
            Tagline = tagline;
            IsCustom = isCustom;
            AssetRoot = assetRoot ?? "";
        }
    }

    public static class PetCatalog
    {
        private static readonly object Sync = new object();
        private static readonly List<PetDefinition> BuiltIns = new List<PetDefinition>
        {
            new PetDefinition("03-penguin", "阿企 Neo", "围巾企鹅", "#E95D4F", "最像桌宠的回忆杀"),
            new PetDefinition("01-cat", "小满", "奶茶三花", "#E77959", "安静治愈，陪伴感最强"),
            new PetDefinition("07-shiba", "赤丸", "柴犬", "#D66A3D", "可靠、耐看，像开朗同事"),
            new PetDefinition("09-hamster", "豆包", "仓鼠", "#D59642", "小尺寸轮廓最清楚"),
            new PetDefinition("05-rabbit", "眠眠", "垂耳兔", "#8871BD", "柔和低打扰，睡姿突出"),
            new PetDefinition("11-cockatiel", "啾啾", "玄凤", "#2C8E94", "技术宅气质最鲜明")
        };
        private static List<PetDefinition> custom = new List<PetDefinition>();
        private static string customRoot = "";

        public static List<PetDefinition> All
        {
            get
            {
                lock (Sync)
                {
                    List<PetDefinition> result = new List<PetDefinition>(BuiltIns);
                    result.AddRange(custom);
                    return result;
                }
            }
        }

        public static void ConfigureCustomRoot(string root)
        {
            lock (Sync)
            {
                customRoot = root ?? "";
                custom = CustomPetService.LoadReadyDefinitions(customRoot);
            }
        }

        public static void ReloadCustom()
        {
            ConfigureCustomRoot(customRoot);
        }

        public static void UpsertCustom(PetDefinition definition)
        {
            if (definition == null || !definition.IsCustom || string.IsNullOrWhiteSpace(definition.Id)) return;
            lock (Sync)
            {
                custom.RemoveAll(delegate(PetDefinition pet) { return string.Equals(pet.Id, definition.Id, StringComparison.OrdinalIgnoreCase); });
                custom.Add(definition);
                custom = custom.OrderBy(delegate(PetDefinition pet) { return pet.Name; }, StringComparer.CurrentCultureIgnoreCase).ToList();
            }
        }

        public static PetDefinition Find(string id)
        {
            return All.FirstOrDefault(delegate(PetDefinition pet) { return string.Equals(pet.Id, id, StringComparison.OrdinalIgnoreCase); });
        }

        public static PetDefinition Next(string id)
        {
            List<PetDefinition> all = All;
            int index = all.FindIndex(delegate(PetDefinition pet) { return pet.Id == id; });
            return all[(index + 1 + all.Count) % all.Count];
        }
    }

    public enum PetState
    {
        Idle,
        Typing,
        Happy,
        Sleep
    }

    public sealed class ActivitySnapshot
    {
        public bool Active;
        public int IdleSeconds;
        public string Category;
        public string ProcessName;
    }

    public static class ActivityClassifier
    {
        private static readonly HashSet<string> Development = Set("devenv", "code", "codium", "rider64", "idea64", "pycharm64", "webstorm64", "eclipse", "hbuilderx", "notepad++", "windowsterminal", "powershell", "pwsh", "cmd", "conhost", "git-bash", "matlab", "jupyter-notebook");
        private static readonly HashSet<string> Communication = Set("welink", "feishu", "lark", "teams", "outlook", "dingtalk", "wxwork", "wechat", "qq", "slack", "zoom", "skype");
        private static readonly HashSet<string> Browsers = Set("chrome", "msedge", "firefox", "iexplore", "opera", "brave", "arc", "360chrome", "sogouexplorer");
        private static readonly HashSet<string> Documents = Set("winword", "excel", "powerpnt", "onenote", "wps", "et", "wpp", "acrord32", "acrobat", "notepad");
        private static readonly HashSet<string> Design = Set("figma", "photoshop", "illustrator", "xd", "mspaint", "snippingtool", "blender", "sketchbook");
        private static readonly HashSet<string> SystemTools = Set("explorer", "taskmgr", "control", "systemsettings", "mmc", "regedit", "everything");

        private static HashSet<string> Set(params string[] names)
        {
            return new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
        }

        public static string Classify(string processName)
        {
            string name = (processName ?? "").ToLowerInvariant();
            if (Development.Contains(name)) return "开发工具";
            if (Communication.Contains(name)) return "沟通协作";
            if (Browsers.Contains(name)) return "浏览器";
            if (Documents.Contains(name)) return "文档办公";
            if (Design.Contains(name)) return "设计创作";
            if (SystemTools.Contains(name)) return "系统工具";
            return "其他";
        }
    }

    public sealed class ActivityTracker : IDisposable
    {
        private readonly DataStore store;
        private readonly System.Windows.Threading.DispatcherTimer timer;
        private int ticksSinceSave;
        private int focusedSeconds;
        private bool breakRewardPending;

        public event Action<ActivitySnapshot> Snapshot;
        public event Action<string> Reward;

        public ActivityTracker(DataStore store)
        {
            this.store = store;
            store.StatisticsReset += ResetPending;
            timer = new System.Windows.Threading.DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(5);
            timer.Tick += Tick;
        }

        public void Start() { timer.Start(); }

        private void ResetPending() { focusedSeconds = 0; breakRewardPending = false; ticksSinceSave = 0; }

        private void Tick(object sender, EventArgs e)
        {
            ActivitySnapshot snapshot = ReadSnapshot();
            if (!store.Data.TrackEnabled) ResetPending();
            if (store.Data.TrackEnabled && snapshot.Active && !string.Equals(snapshot.ProcessName, Process.GetCurrentProcess().ProcessName, StringComparison.OrdinalIgnoreCase))
            {
                store.AddStatSeconds(snapshot.Category, 5);
                focusedSeconds += 5;
                if (focusedSeconds >= 50 * 60) breakRewardPending = true;
                ticksSinceSave++;
                if (ticksSinceSave >= 12)
                {
                    ticksSinceSave = 0;
                    store.Save();
                }
            }

            if (breakRewardPending && snapshot.IdleSeconds >= 5 * 60)
            {
                breakRewardPending = false;
                focusedSeconds = 0;
                store.AddCompanionValue(6);
                store.Save();
                Action<string> reward = Reward;
                if (reward != null) reward("专注后休息，陪伴值 +6");
            }
            else if (snapshot.IdleSeconds >= 15 * 60 && !breakRewardPending) focusedSeconds = 0;

            Action<ActivitySnapshot> handler = Snapshot;
            if (handler != null) handler(snapshot);
        }

        public static ActivitySnapshot ReadSnapshot()
        {
            ActivitySnapshot result = new ActivitySnapshot();
            NativeMethods.LASTINPUTINFO info = new NativeMethods.LASTINPUTINFO();
            info.cbSize = (uint)Marshal.SizeOf(info);
            int idle = 0;
            if (NativeMethods.GetLastInputInfo(ref info))
            {
                uint elapsed = unchecked((uint)Environment.TickCount) - info.dwTime;
                idle = (int)(elapsed / 1000);
            }
            result.IdleSeconds = idle;
            result.Active = idle < 300;
            result.ProcessName = "";
            try
            {
                IntPtr handle = NativeMethods.GetForegroundWindow();
                uint processId;
                NativeMethods.GetWindowThreadProcessId(handle, out processId);
                using (Process process = Process.GetProcessById((int)processId)) result.ProcessName = process.ProcessName;
            }
            catch { }
            result.Category = ActivityClassifier.Classify(result.ProcessName);
            return result;
        }

        public void Dispose()
        {
            timer.Stop();
            store.StatisticsReset -= ResetPending;
        }
    }

    public static class Formatters
    {
        public static string Duration(int seconds)
        {
            if (seconds < 60) return "不足 1 分钟";
            int hours = seconds / 3600;
            int minutes = (seconds % 3600) / 60;
            if (hours > 0 && minutes > 0) return hours + " 小时 " + minutes + " 分钟";
            if (hours > 0) return hours + " 小时";
            return minutes + " 分钟";
        }

        public static string DateLabel(string iso)
        {
            DateTime value;
            if (!DateTime.TryParse(iso, out value)) return "";
            if (value.Date == DateTime.Today) return "今天 " + value.ToString("HH:mm");
            if (value.Date == DateTime.Today.AddDays(-1)) return "昨天 " + value.ToString("HH:mm");
            return value.ToString("M 月 d 日");
        }

        public static string Truncate(string text, int max)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= max) return text ?? "";
            return text.Substring(0, max) + "…";
        }
    }
}
