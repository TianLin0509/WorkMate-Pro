using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace WorkMatePro
{
    public static class SelfTest
    {
        public static int Run()
        {
            List<string> log = new List<string>();
            int failures = 0;
            DataStore store = null;
            try
            {
                store = new DataStore();
                MemoItem shortMemo = store.AddMemo("回复评审意见", "short", false, false);
                MemoItem longMemo = store.AddMemo("整理季度技术思路", "long", true, false);
                Check(shortMemo != null && longMemo != null, "memo-create", log, ref failures);
                Check(store.Data.Memos.Count == 2, "memo-count", log, ref failures);
                Check(shortMemo.Term == "short" && longMemo.Term == "long", "memo-term", log, ref failures);

                int beforeDone = store.Data.CompanionValue;
                store.SetMemoDone(shortMemo, true);
                Check(shortMemo.IsDone && !string.IsNullOrEmpty(shortMemo.DoneAt), "memo-complete", log, ref failures);
                Check(store.Data.CompanionValue == beforeDone + 4, "completion-growth", log, ref failures);

                int beforeWork = store.Data.CompanionValue;
                store.AddStatSeconds("开发工具", 600);
                store.Save();
                Check(store.TodayActiveSeconds == 600, "stats-seconds", log, ref failures);
                Check(store.Data.CompanionValue == beforeWork + 1, "capped-work-growth", log, ref failures);

                DataStore reloaded = new DataStore();
                Check(reloaded.Data.Memos.Count == 2, "json-reload-memos", log, ref failures);
                Check(reloaded.TodayActiveSeconds == 600, "json-reload-stats", log, ref failures);
                Check(ActivityClassifier.Classify("code") == "开发工具", "classify-development", log, ref failures);
                Check(ActivityClassifier.Classify("welink") == "沟通协作", "classify-communication", log, ref failures);
                Check(ActivityClassifier.Classify("chrome") == "浏览器", "classify-browser", log, ref failures);
                Check(Growth.LevelFor(0) == 1 && Growth.LevelFor(40) == 2, "growth-levels", log, ref failures);

                int imageCount = 0;
                foreach (PetDefinition pet in PetCatalog.All)
                {
                    foreach (string action in new[] { "idle", "typing", "happy", "sleep" })
                    {
                        if (PetAssets.Get(pet.Id, action) != null) imageCount++;
                    }
                }
                Check(imageCount == 24, "embedded-sprite-count", log, ref failures);
                EmbeddedToolManager embeddedTools = new EmbeddedToolManager();
                Check(embeddedTools.ResourceExists(EmbeddedToolManager.ScrollCaptureResource)
                    && embeddedTools.ResourceExists(EmbeddedToolManager.OcrScriptResource),
                    "embedded-capability-resources", log, ref failures);
                string extractedCapture = embeddedTools.ExtractScrollCapture();
                string extractedOcr = embeddedTools.ExtractOcrScript();
                Check(File.Exists(extractedCapture) && new FileInfo(extractedCapture).Length > 1024 * 1024
                    && Path.GetFileName(extractedCapture).IndexOf(EmbeddedToolManager.ScrollCaptureSha256.Substring(0, 12), StringComparison.OrdinalIgnoreCase) >= 0
                    && File.Exists(extractedOcr) && new FileInfo(extractedOcr).Length > 1000,
                    "embedded-capability-extraction-and-hash", log, ref failures);

                byte[] deltaSource = new byte[1024 * 1024];
                for (int i = 0; i < deltaSource.Length; i++) deltaSource[i] = (byte)((i * 31 + i / 97) & 0xff);
                byte[] deltaTarget = new byte[deltaSource.Length + 8192];
                Buffer.BlockCopy(deltaSource, 0, deltaTarget, 0, 420000);
                for (int i = 420000; i < 428192; i++) deltaTarget[i] = (byte)(255 - (i & 0xff));
                Buffer.BlockCopy(deltaSource, 420000, deltaTarget, 428192, deltaSource.Length - 420000);
                for (int i = 700000; i < 701500; i++) deltaTarget[i] ^= 0x5a;
                byte[] deltaPatch = MsDeltaCodec.Create(deltaSource, deltaTarget);
                byte[] deltaRestored = MsDeltaCodec.Apply(deltaSource, deltaPatch);
                Check(deltaRestored.SequenceEqual(deltaTarget) && deltaPatch.Length < deltaTarget.Length / 8,
                    "msdelta-roundtrip-and-effective-size", log, ref failures);
                byte[] trustProbe = Encoding.UTF8.GetBytes("WorkMate update trust self-test v1");
                byte[] trustSignature = Convert.FromBase64String("EOe2Yz1SW1mHccjSAGT+RlGUuTCJ8I6hqtj+DD9greIDQVlxgAf/fyvsDi2UBwURZ1Mby6ONKNXhCPr2gbu2JIRZ/CKpvekdKr7AWAoJuL9cQ3DgQHkDXz0jIk0bK0AlG79XqbSZo8wGmP1EZeNy0m9f5ehs0Iz4sj0wgUPy6OUcaWDePK3Z0+6XiLuLSMpmo/EescZpF/bCGB+/sqzrNKAP6U8qX4RAuqFqgcXYXE3H5mr19fjhiwe4liGVs8veuN5fxeaK52HNAn9R/mETv/jxOO8I5DLdRkoR38JRBHKirptNJIhKzbdxzz2217MtKHg+YdeAQW8jQRHXv3QZLWiP6Tph7LZMlthsaj6qcE5gRzBD0RfSy43P/aEqni6ozl7C4XNwjMFPm0v+Z47pREbWX8txWk8em51lhJGzkoBKO8tQV50hJbBgFlAPvg0iW1UOZOOhLV/YJ1XFCcVSs2f9C9gmFDhmOXg7EikVhqpTkoElGndF4tOr/hxpn8wM");
                bool trusted = UpdateTrust.Verify(trustProbe, trustSignature);
                trustProbe[0] ^= 0x01;
                Check(trusted && !UpdateTrust.Verify(trustProbe, trustSignature),
                    "update-rsa-signature-accepts-authentic-and-rejects-tamper", log, ref failures);
                System.Windows.Media.Imaging.BitmapSource alphaProbe = PetAssets.Get("01-cat", "idle");
                Check(!PetAssets.IsOpaque(alphaProbe, 0.01, 0.01, 12), "sprite-alpha-corner-transparent", log, ref failures);
                Check(PetAssets.IsOpaque(alphaProbe, 0.50, 0.58, 12), "sprite-alpha-body-opaque", log, ref failures);
                System.Windows.Rect opaqueBounds = PetAssets.OpaqueBounds(alphaProbe, 12);
                Check(opaqueBounds.Left > 0.03 && opaqueBounds.Right < 0.97 && opaqueBounds.Width > 0.25,
                    "sprite-alpha-subject-bounds", log, ref failures);

                // ---------- 状态引擎：表驱动合成样本（Kimi 轮新增）----------
                // 1. 真打字 → Typing
                StateEngine engine = new StateEngine();
                BehaviorState state = Feed(engine, delegate(SignalSample s) { s.KeyPerMin = 150; return s; }, 6);
                Check(state == BehaviorState.Typing, "engine-typing", log, ref failures);

                // 2. 用户痛点回归：只滚鼠标/移动，绝不能误判成 Typing
                engine = new StateEngine();
                bool everTyping = false;
                state = BehaviorState.Idle;
                for (int i = 0; i < 12; i++)
                {
                    state = FeedOnce(engine, delegate(SignalSample s) { s.WheelPerMin = 45; s.MovePerMin = 800; return s; });
                    if (state == BehaviorState.Typing) everTyping = true;
                }
                Check(!everTyping, "engine-mouse-never-typing", log, ref failures);
                Check(state == BehaviorState.Reading, "engine-reading", log, ref failures);

                // 3. 麦克风占用 → 会议，且优先级高于打字
                engine = new StateEngine();
                state = Feed(engine, delegate(SignalSample s) { s.MicInUse = true; s.KeyPerMin = 200; return s; }, 5);
                Check(state == BehaviorState.Meeting, "engine-meeting-priority", log, ref failures);

                // 4. 摄像头占用也算开会（视频会议）
                engine = new StateEngine();
                state = Feed(engine, delegate(SignalSample s) { s.CameraInUse = true; return s; }, 5);
                Check(state == BehaviorState.Meeting, "engine-camera-meeting", log, ref failures);

                // 5. 会议结束后有驻留，不会立刻变脸
                engine = new StateEngine();
                Feed(engine, delegate(SignalSample s) { s.MicInUse = true; return s; }, 6);
                state = FeedOnce(engine, delegate(SignalSample s) { return s; });
                Check(state == BehaviorState.Meeting, "engine-meeting-dwell", log, ref failures);

                // 6. 音频持续播放 + 几乎无有意输入 → 看视频（纯鼠标移动不误伤）
                engine = new StateEngine();
                state = Feed(engine, delegate(SignalSample s) { s.AudioActive = true; s.IdleSeconds = 30; s.IdleIntentionalSeconds = 30; s.ForegroundCategory = "浏览器"; return s; }, 10);
                Check(state == BehaviorState.Watching, "engine-watching", log, ref failures);

                // 6b. 看视频时手抖动了下鼠标（无意输入）→ 仍是 Watching
                engine = new StateEngine();
                state = Feed(engine, delegate(SignalSample s) { s.AudioActive = true; s.IdleSeconds = 0; s.IdleIntentionalSeconds = 40; s.MovePerMin = 500; s.ForegroundCategory = "浏览器"; return s; }, 10);
                Check(state == BehaviorState.Watching, "engine-watching-mouse-jiggle", log, ref failures);

                // 7. IDE 前台但有意输入全静 → 思考（陪你卡壳）
                engine = new StateEngine();
                state = Feed(engine, delegate(SignalSample s) { s.ForegroundCategory = "开发工具"; s.IdleSeconds = 25; s.IdleIntentionalSeconds = 25; return s; }, 6);
                Check(state == BehaviorState.Thinking, "engine-thinking", log, ref failures);

                // 8. 长时间无输入 → 打盹
                engine = new StateEngine();
                state = Feed(engine, delegate(SignalSample s) { s.IdleSeconds = 200; return s; }, 6);
                Check(state == BehaviorState.Sleepy, "engine-sleepy", log, ref failures);

                // 9. 滞回：打字停顿 2 个采样不会立刻掉出 Typing
                engine = new StateEngine();
                Feed(engine, delegate(SignalSample s) { s.KeyPerMin = 150; return s; }, 6);
                state = Feed(engine, delegate(SignalSample s) { s.KeyPerMin = 20; return s; }, 2);
                Check(state == BehaviorState.Typing, "engine-typing-hysteresis", log, ref failures);

                // 10. 正反馈抬升 valence
                engine = new StateEngine();
                engine.NotifyPositive();
                EngineOutput output = FeedOutput(engine, delegate(SignalSample s) { s.Now = new DateTime(2026, 7, 15, 12, 0, 0); return s; }, 4);
                Check(output.Valence > 0.56, "engine-valence-positive v=" + output.Valence.ToString("0.000"), log, ref failures);

                // ---------- 星期节奏 ----------
                double friday = RhythmBaseline.ValenceBoost(new DateTime(2026, 7, 17, 18, 0, 0)); // 周五
                double wednesday = RhythmBaseline.ValenceBoost(new DateTime(2026, 7, 15, 18, 0, 0)); // 周三
                double monday = RhythmBaseline.ValenceBoost(new DateTime(2026, 7, 20, 9, 0, 0)); // 周一
                Check(friday > wednesday && monday < 0, "rhythm-weekday", log, ref failures);

                // 下班问候：18 点整触发一次，当天不重复，午休/回家判定不受影响
                string occasionKind;
                string offworkToast = RhythmBaseline.OccasionToast(new DateTime(2026, 7, 22, 18, 5, 0), "", "", out occasionKind, "");
                Check(offworkToast == "下班啦，今天辛苦了" && occasionKind == "offwork", "occasion-offwork", log, ref failures);
                string noRepeat = RhythmBaseline.OccasionToast(new DateTime(2026, 7, 22, 18, 5, 0), "", "", out occasionKind, "2026-07-22");
                Check(noRepeat == null, "occasion-offwork-no-repeat", log, ref failures);
                Check(new WorkMateData().LastGreetDate == "", "greet-date-default", log, ref failures);
                int milestoneDay;
                string day7 = CompanionMilestones.KeyFor("2026-07-17", new DateTime(2026, 7, 23), "", out milestoneDay);
                Check(day7 == "2026-07-17:7" && milestoneDay == 7,
                    "companion-anniversary-day7", log, ref failures);
                Check(CompanionMilestones.KeyFor("2026-07-17", new DateTime(2026, 7, 23), day7, out milestoneDay) == null
                    && CompanionMilestones.KeyFor("2026-07-17", new DateTime(2026, 7, 24), "", out milestoneDay) == null,
                    "companion-anniversary-once-and-rare", log, ref failures);
                Check(CompanionMilestones.KeyFor("2025-07-24", new DateTime(2026, 7, 23), "", out milestoneDay) != null
                    && milestoneDay == 365,
                    "companion-anniversary-day365", log, ref failures);
                Check(CompanionMilestones.FarewellMessage(new DateTime(2026, 7, 23, 22, 0, 0)).StartsWith("晚安")
                    && CompanionMilestones.FarewellMessage(new DateTime(2026, 7, 23, 14, 0, 0)).StartsWith("先去忙"),
                    "companion-farewell-time-aware", log, ref failures);
                Check(!ShyGazePolicy.ShouldTrigger(12, 18, 2.99, 90)
                    && ShyGazePolicy.ShouldTrigger(12, 18, 3.0, 90),
                    "shy-gaze-needs-three-second-dwell", log, ref failures);
                Check(!ShyGazePolicy.ShouldTrigger(40, 18, 4.0, 90)
                    && !ShyGazePolicy.ShouldTrigger(12, 18, 4.0, 30),
                    "shy-gaze-far-and-cooldown-rejected", log, ref failures);
                PatAffectionFrame patRight = PatAffectionPolicy.Sample(0.30, 0.8);
                PatAffectionFrame patLeft = PatAffectionPolicy.Sample(0.30, -0.8);
                PatAffectionFrame patDone = PatAffectionPolicy.Sample(0.90, 0.8);
                Check(patRight.Intensity == 1 && patRight.LeanXDip > 0 && patRight.TiltDegrees > 0
                    && patLeft.LeanXDip < 0 && patLeft.TiltDegrees < 0
                    && patDone.Intensity == 0 && patDone.LeanXDip == 0,
                    "pat-affection-leans-toward-the-hand-and-settles", log, ref failures);
                BalloonToyState balloonState = BalloonToyPhysics.Initial();
                double balloonStartY = balloonState.Y;
                for (int i = 0; i < 24; i++) balloonState = BalloonToyPhysics.Step(balloonState, 0.04);
                Check(balloonState.X >= 0.18 && balloonState.X <= 0.82
                    && balloonState.Y >= 0.10 && balloonState.Y <= 0.43,
                    "balloon-physics-stays-in-pet-window", log, ref failures);
                BalloonToyState bopped = BalloonToyPhysics.Bop(balloonState, 0.5);
                Check(bopped.VY < -0.7 && bopped.VX > balloonState.VX && balloonStartY >= 0.1,
                    "balloon-click-bops-upward", log, ref failures);
                Random patrolRandom = new Random(23);
                int patrolDelay = PatrolPolicy.NextDelayMs(patrolRandom);
                Check(patrolDelay >= PatrolPolicy.MinDelaySeconds * 1000
                    && patrolDelay <= PatrolPolicy.MaxDelaySeconds * 1000,
                    "patrol-is-rare", log, ref failures);
                System.Windows.Rect patrolArea = new System.Windows.Rect(0, 0, 1600, 900);
                System.Windows.Rect patrolPet = new System.Windows.Rect(1100, 720, 100, 110);
                PatrolPlan shortPatrol = PatrolPolicy.Resolve(patrolArea, patrolPet, System.Windows.Rect.Empty);
                Check(shortPatrol.Valid && !shortPatrol.WindowPerch
                    && patrolArea.Contains(new System.Windows.Rect(shortPatrol.Target.X, shortPatrol.Target.Y, patrolPet.Width, patrolPet.Height))
                    && Math.Abs(shortPatrol.Target.X - patrolPet.Left) <= patrolPet.Width * 1.56,
                    "patrol-short-and-within-workarea", log, ref failures);
                System.Windows.Rect nearbyWindow = new System.Windows.Rect(980, 700, 340, 180);
                PatrolPlan perchPatrol = PatrolPolicy.Resolve(patrolArea, patrolPet, nearbyWindow);
                Check(perchPatrol.Valid && perchPatrol.WindowPerch
                    && Math.Abs(perchPatrol.Target.Y - (nearbyWindow.Top - patrolPet.Height * 0.72)) < 0.1,
                    "patrol-nearby-window-perch", log, ref failures);
                Check(!AmbientInterruptionPolicy.ShouldCancel(true, 10.19, 10.20)
                    && AmbientInterruptionPolicy.ShouldCancel(true, 10.20, 10.20)
                    && !AmbientInterruptionPolicy.ShouldCancel(false, 20, 10)
                    && AmbientInterruptionPolicy.UserInvitationGraceSeconds >= 0.90
                    && AmbientInterruptionPolicy.AutomaticActionGraceSeconds < AmbientInterruptionPolicy.UserInvitationGraceSeconds
                    && AmbientInterruptionPolicy.DemoGraceSeconds >= 10.0,
                    "ambient-input-grace-prevents-trigger-tail-cancel", log, ref failures);
                Check(!PetInteractionPolicy.FileCarryEnabled && !PetInteractionPolicy.HasActiveCarry(3),
                    "file-carry-interaction-rolled-back", log, ref failures);
                Check(PetIdentityChipPolicy.CompactLabel("阿企 Neo", 4, "专注 · 24:09", "", 0, true) == "专注 · 24:09"
                    && PetIdentityChipPolicy.CompactLabel("阿企 Neo", 4, "", "整理技术方案", 0, true).StartsWith("在做 · 整理技术")
                    && PetIdentityChipPolicy.CompactLabel("阿企 Neo", 4, "", "", 0, true) == "阿企 · Lv.4"
                    && PetIdentityChipPolicy.CompactLabel("阿企 Neo", 4, "专注", "", 0, false) == "统计暂停",
                    "compact-pet-chip-prioritizes-readable-live-state", log, ref failures);
                Check(IdentityHoverPolicy.RevealDelayMs >= 180 && IdentityHoverPolicy.RevealDelayMs <= 280
                    && IdentityHoverPolicy.AllowsReveal(true, false, false, false)
                    && !IdentityHoverPolicy.AllowsReveal(false, false, false, false)
                    && !IdentityHoverPolicy.AllowsReveal(true, true, false, false)
                    && !IdentityHoverPolicy.AllowsReveal(true, false, true, false)
                    && !IdentityHoverPolicy.AllowsReveal(true, false, false, true),
                    "identity-chip-requires-deliberate-hover", log, ref failures);
                CursorGreetingFrame greetingPeak = CursorGreetingPolicy.Sample(0.35, 0.8);
                CursorGreetingFrame greetingDone = CursorGreetingPolicy.Sample(1.1, 0.8);
                Check(CursorGreetingPolicy.ShouldTrigger(115, 100, false, 1.2, 20, false, false)
                    && !CursorGreetingPolicy.ShouldTrigger(115, 100, true, 1.2, 20, false, false)
                    && !CursorGreetingPolicy.ShouldTrigger(115, 100, false, 1.2, 20, true, false)
                    && !CursorGreetingPolicy.ShouldTrigger(115, 100, false, 1.2, 20, false, true)
                    && !CursorGreetingPolicy.ShouldTrigger(230, 100, false, 1.2, 20, false, false)
                    && greetingPeak.Intensity == 1 && greetingPeak.TiltDegrees > 0
                    && greetingPeak.LiftDip < 0
                    && greetingDone.Intensity == 0,
                    "cursor-greeting-is-nearby-quiet-and-rate-limited", log, ref failures);
                Check(CursorGreetingPolicy.IsBusy(false, false, true, false, false, false, false, false, false)
                    && CursorGreetingPolicy.IsBusy(false, true, false, false, false, false, false, false, false)
                    && !CursorGreetingPolicy.IsBusy(false, false, false, false, false, false, false, false, false),
                    "cursor-greeting-treats-user-focus-as-busy", log, ref failures);
                WakeStretchFrame wakePeak = WakeStretchPolicy.Sample(0.35);
                WakeStretchFrame wakeDone = WakeStretchPolicy.Sample(1.4);
                Check(WakeStretchPolicy.ShouldStart(BehaviorState.Away, BehaviorState.Idle, 90, false, false)
                    && WakeStretchPolicy.ShouldStart(BehaviorState.Sleepy, BehaviorState.Idle, 60, false, false)
                    && !WakeStretchPolicy.ShouldStart(BehaviorState.Away, BehaviorState.Typing, 90, false, false)
                    && !WakeStretchPolicy.ShouldStart(BehaviorState.Away, BehaviorState.Idle, 30, false, false)
                    && !WakeStretchPolicy.ShouldStart(BehaviorState.Away, BehaviorState.Idle, 90, true, false)
                    && !WakeStretchPolicy.ShouldStart(BehaviorState.Away, BehaviorState.Idle, 90, false, true)
                    && wakePeak.Intensity == 1 && wakePeak.ScaleYDelta > 0
                    && wakePeak.ScaleXDelta < 0 && wakePeak.LiftDip < 0 && wakeDone.Intensity == 0,
                    "wake-stretch-is-long-away-only-and-single-shot", log, ref failures);
                System.Windows.Rect attentionPet = new System.Windows.Rect(900, 500, 120, 126);
                CursorGreetingFrame bubbleLook = BubbleAttentionPolicy.Sample(0.30, -1);
                CursorGreetingFrame bubbleSettled = BubbleAttentionPolicy.Sample(1.1, -1);
                Check(BubbleAttentionPolicy.HorizontalBias(attentionPet, new System.Windows.Rect(608, 320, 280, 548)) == -1
                    && BubbleAttentionPolicy.HorizontalBias(attentionPet, new System.Windows.Rect(1032, 320, 280, 548)) == 1
                    && BubbleAttentionPolicy.HorizontalBias(attentionPet, new System.Windows.Rect(820, 320, 280, 548)) == 0
                    && bubbleLook.Intensity == 1 && bubbleLook.TiltDegrees < 0
                    && bubbleSettled.Intensity == 0,
                    "bubble-attention-follows-final-clamped-side", log, ref failures);
                Check(AttentionRitualPolicy.BlocksAutonomousAction(true, false, false)
                    && AttentionRitualPolicy.BlocksAutonomousAction(false, true, false)
                    && AttentionRitualPolicy.BlocksAutonomousAction(false, false, true)
                    && !AttentionRitualPolicy.BlocksAutonomousAction(false, false, false),
                    "attention-ritual-defers-autonomous-scenes", log, ref failures);
                Check(AttentionRitualPolicy.ShouldYieldToUser(true, true)
                    && !AttentionRitualPolicy.ShouldYieldToUser(true, false)
                    && !AttentionRitualPolicy.ShouldYieldToUser(false, true),
                    "attention-ritual-yields-only-to-explicit-user-action", log, ref failures);
                TreatMotionFrame treatOffer = TreatMotionPolicy.Sample(0.42);
                TreatMotionFrame treatBite = TreatMotionPolicy.Sample(1.12);
                TreatMotionFrame treatJoy = TreatMotionPolicy.Sample(1.80);
                Check(treatOffer.Offer > 0.35 && treatOffer.Offer < 0.75 && treatOffer.CookieOpacity > 0.95,
                    "treat-three-stage-offer", log, ref failures);
                Check(treatBite.Bite > 0.65 && treatBite.CookieOpacity < 0.35 && treatJoy.Joy > 0.9,
                    "treat-three-stage-bite-and-joy", log, ref failures);
                Check(TreatSnackPolicy.ForPet("03-penguin") == TreatSnackKind.Fish
                    && TreatSnackPolicy.ForPet("01-cat") == TreatSnackKind.Fish
                    && TreatSnackPolicy.ForPet("05-rabbit") == TreatSnackKind.Carrot
                    && TreatSnackPolicy.ForPet("07-shiba") == TreatSnackKind.Biscuit
                    && TreatSnackPolicy.ForPet("09-hamster") == TreatSnackKind.Seeds
                    && TreatSnackPolicy.ForPet("11-cockatiel") == TreatSnackKind.Seeds,
                    "treat-snack-matches-all-six-species", log, ref failures);
                Check(HighFivePolicy.CanRespond(12.59, 10.0, true)
                    && !HighFivePolicy.CanRespond(12.61, 10.0, true)
                    && !HighFivePolicy.CanRespond(10.5, 10.0, false),
                    "high-five-only-accepts-one-invited-click-window", log, ref failures);
                Check(HighFivePolicy.InviteProgress(0) == 0
                    && HighFivePolicy.InviteProgress(0.34) == 1
                    && HighFivePolicy.CelebrateProgress(HighFivePolicy.CelebrateSeconds) == 1
                    && HighFivePolicy.UsesWing("03-penguin") && HighFivePolicy.UsesWing("11-cockatiel")
                    && !HighFivePolicy.UsesWing("01-cat") && !HighFivePolicy.UsesWing("05-rabbit")
                    && !HighFivePolicy.UsesWing("07-shiba") && !HighFivePolicy.UsesWing("09-hamster")
                    && HighFivePolicy.CapturesPointer(true, false)
                    && !HighFivePolicy.CapturesPointer(true, true)
                    && !HighFivePolicy.CapturesPointer(false, false)
                    && HighFivePolicy.ShouldTimeout(true, true, false)
                    && !HighFivePolicy.ShouldTimeout(true, false, false)
                    && !HighFivePolicy.ShouldTimeout(true, true, true),
                    "high-five-motion-progress-is-bounded", log, ref failures);
                System.Windows.Rect summonArea = new System.Windows.Rect(0, 0, 1200, 800);
                SummonPlan summonRight = SummonGeometry.Resolve(summonArea, new System.Windows.Point(300, 420), 120, 132);
                SummonPlan summonLeft = SummonGeometry.Resolve(summonArea, new System.Windows.Point(1150, 40), 120, 132);
                Check(summonRight.Valid && !summonRight.FacingRight && summonRight.Target.X > 300
                    && summonArea.Contains(new System.Windows.Rect(summonRight.Target.X, summonRight.Target.Y, 120, 132)),
                    "summon-lands-beside-cursor", log, ref failures);
                Check(summonLeft.Valid && summonLeft.FacingRight && summonLeft.Target.X < 1030 && summonLeft.Target.Y >= 6,
                    "summon-flips-and-clamps-at-edge", log, ref failures);
                Check(ContextScenePolicy.Resolve(BehaviorState.Reading, false, false) == ContextSceneKind.Book
                    && ContextScenePolicy.Resolve(BehaviorState.Meeting, false, false) == ContextSceneKind.Headset
                    && ContextScenePolicy.Resolve(BehaviorState.Typing, false, true) == ContextSceneKind.FocusHalo
                    && ContextScenePolicy.Resolve(BehaviorState.Idle, false, true) == ContextSceneKind.FocusHalo
                    && ContextScenePolicy.Resolve(BehaviorState.Thinking, false, true) == ContextSceneKind.FocusHalo
                    && ContextScenePolicy.Resolve(BehaviorState.Idle, true, false) == ContextSceneKind.Hourglass
                    && ContextScenePolicy.Resolve(BehaviorState.Idle, true, true) == ContextSceneKind.Hourglass
                    && ContextScenePolicy.Resolve(BehaviorState.Meeting, true, false) == ContextSceneKind.Headset
                    && ContextScenePolicy.Resolve(BehaviorState.Idle, false, false) == ContextSceneKind.None,
                    "context-scene-priority-and-mapping", log, ref failures);
                Check(!ContextScenePolicy.AllowsAutonomousAction(true)
                    && ContextScenePolicy.AllowsAutonomousAction(false),
                    "build-wait-suppresses-autonomous-actions-and-nodoff", log, ref failures);
                DateTime quietNow = new DateTime(2026, 7, 24, 1, 0, 0);
                Check(QuietCompanionPolicy.IsActive(quietNow, quietNow.AddMinutes(30))
                    && !QuietCompanionPolicy.IsActive(quietNow, quietNow)
                    && QuietCompanionPolicy.RemainingLabel(quietNow, quietNow.AddSeconds(61)).Contains("2分"),
                    "quiet-companion-active-and-countdown", log, ref failures);
                Check(FocusRitualPolicy.IsActive(quietNow, quietNow.AddMinutes(25))
                    && FocusRitualPolicy.RemainingLabel(quietNow, quietNow.AddMinutes(24).AddSeconds(9)).Contains("24:09")
                    && FocusRitualPolicy.RemainingClock(quietNow, quietNow.AddMinutes(24).AddSeconds(9)) == "24:09"
                    && FocusRitualPolicy.RemainingClock(quietNow.AddSeconds(2), quietNow) == "00:00"
                    && Math.Abs(FocusRitualPolicy.ElapsedProgress(quietNow.AddMinutes(5), quietNow, quietNow.AddMinutes(25)) - 0.20) < 0.001
                    && FocusRitualPolicy.ElapsedProgress(quietNow.AddMinutes(30), quietNow, quietNow.AddMinutes(25)) == 1
                    && FocusRitualPolicy.CanStart(BehaviorState.Typing, false, false, false)
                    && !FocusRitualPolicy.CanStart(BehaviorState.Meeting, false, false, false)
                    && !FocusRitualPolicy.CanStart(BehaviorState.Idle, true, false, false)
                    && !FocusRitualPolicy.CanStart(BehaviorState.Idle, false, true, false)
                    && !FocusRitualPolicy.CanStart(BehaviorState.Idle, false, false, true),
                    "focus-ritual-countdown-and-director-guards", log, ref failures);
                Check(FocusRitualPolicy.CanPresentCompletion(BehaviorState.Idle, false, false, false, false, false)
                    && !FocusRitualPolicy.CanPresentCompletion(BehaviorState.Meeting, false, false, false, false, false)
                    && !FocusRitualPolicy.CanPresentCompletion(BehaviorState.Idle, true, false, false, false, false)
                    && !FocusRitualPolicy.CanPresentCompletion(BehaviorState.Idle, false, true, false, false, false)
                    && !FocusRitualPolicy.CanPresentCompletion(BehaviorState.Idle, false, false, true, false, false)
                    && !FocusRitualPolicy.CanPresentCompletion(BehaviorState.Idle, false, false, false, true, false)
                    && !FocusRitualPolicy.CanPresentCompletion(BehaviorState.Idle, false, false, false, false, true),
                    "focus-completion-waits-for-director-slot", log, ref failures);
                Check(AnimationPolicy.AllowsIdleGesture(false, false)
                    && !AnimationPolicy.AllowsIdleGesture(true, false)
                    && !AnimationPolicy.AllowsIdleGesture(false, true),
                    "quiet-and-reduced-motion-suppress-idle-gesture", log, ref failures);
                Check(AnimationPolicy.ShouldPlayAutomaticStretch(true, false, false)
                    && !AnimationPolicy.ShouldPlayAutomaticStretch(true, true, false)
                    && !AnimationPolicy.ShouldPlayAutomaticStretch(true, false, true)
                    && !AnimationPolicy.ShouldPlayAutomaticStretch(false, false, false),
                    "automatic-stretch-respects-setting-reduced-motion-and-quiet", log, ref failures);
                Check(AnimationPolicy.ShouldPlayStretchFallback(false, false)
                    && !AnimationPolicy.ShouldPlayStretchFallback(true, false)
                    && !AnimationPolicy.ShouldPlayStretchFallback(false, true),
                    "stretch-fallback-respects-reduced-motion-and-quiet", log, ref failures);
                Random nodScheduleRandom = new Random(2407);
                bool nodScheduleInRange = true;
                for (int i = 0; i < 50; i++)
                {
                    double delay = NodOffSchedule.NextDelaySeconds(nodScheduleRandom);
                    if (delay < NodOffSchedule.MinDelaySeconds || delay > NodOffSchedule.MaxDelaySeconds) nodScheduleInRange = false;
                }
                Check(nodScheduleInRange && NodOffSchedule.NextDelaySeconds(null) == NodOffSchedule.MinDelaySeconds,
                    "nodoff-initial-and-repeat-delay-range", log, ref failures);
                Check(NodOffSchedule.AllowsAutonomousScene(false, false)
                    && !NodOffSchedule.AllowsAutonomousScene(true, false)
                    && !NodOffSchedule.AllowsAutonomousScene(false, true),
                    "nodoff-respects-reduced-motion-and-build-wait", log, ref failures);

                // 拎起摇晃晕眩判定：1 秒内甩动反转 ≥3 触发，慢速移动不误触
                Check(EdgeLogic.ShouldDizzy(
                    new List<double> { 0, 60, 0, 60, 0, 60 },
                    new List<double> { 0, 0.15, 0.3, 0.45, 0.6, 0.75 }, 0.75), "dizzy-trigger", log, ref failures);
                Check(!EdgeLogic.ShouldDizzy(
                    new List<double> { 0, 60, 0, 60, 0, 60 },
                    new List<double> { 0, 0.5, 1.0, 1.5, 2.0, 2.5 }, 2.5), "dizzy-slow-no-trigger", log, ref failures);
                DragInertiaPlan fastGlide = DragInertiaPolicy.Resolve(
                    new List<double> { 100, 132, 170 }, new List<double> { 100, 103, 107 },
                    new List<double> { 10.00, 10.08, 10.16 }, 10.17,
                    new System.Windows.Rect(0, 0, 800, 600), 170, 107, 68, 74, false);
                DragInertiaPlan slowGlide = DragInertiaPolicy.Resolve(
                    new List<double> { 100, 108 }, new List<double> { 100, 101 },
                    new List<double> { 10.00, 10.15 }, 10.16,
                    new System.Windows.Rect(0, 0, 800, 600), 108, 101, 68, 74, false);
                DragInertiaPlan reducedGlide = DragInertiaPolicy.Resolve(
                    new List<double> { 100, 170 }, new List<double> { 100, 107 },
                    new List<double> { 10.00, 10.16 }, 10.17,
                    new System.Windows.Rect(0, 0, 800, 600), 170, 107, 68, 74, true);
                Check(fastGlide.Valid && fastGlide.Target.X > 170
                    && fastGlide.Target.X <= 170 + DragInertiaPolicy.MaximumTravelDip
                    && fastGlide.DurationMs >= 120 && fastGlide.DurationMs <= 210
                    && !slowGlide.Valid && !reducedGlide.Valid,
                    "drag-release-inertia-is-bounded-and-reduced-safe", log, ref failures);
                DragLiftPose fastRightLift = DragLiftPosePolicy.Resolve(720, -300);
                DragLiftPose fastLeftLift = DragLiftPosePolicy.Resolve(-720, 300);
                DragLiftPose settledLift = DragLiftPosePolicy.Resolve(0, 0);
                Check(fastRightLift.TiltDegrees < 0 && fastRightLift.LagXDip < 0
                    && fastLeftLift.TiltDegrees > 0 && fastLeftLift.LagXDip > 0
                    && Math.Abs(fastRightLift.TiltDegrees) <= DragLiftPosePolicy.MaxTiltDegrees
                    && Math.Abs(fastLeftLift.LagXDip) <= DragLiftPosePolicy.MaxLagDip
                    && settledLift.TiltDegrees == 0 && settledLift.LagXDip == 0
                    && settledLift.LiftDip == -4.0,
                    "drag-lift-pose-follows-speed-and-settles", log, ref failures);

                // ---------- 困倦点头分镜（Top1 小剧场）----------
                // 阶段时序：点头 2.4s → 惊醒 0.5s → 深垂 1.8s → 蜷睡 → 伸懒腰 1.4s
                NodOffDirector nodOff = new NodOffDirector();
                nodOff.Start(10);
                NodOffStage seen1 = Advance(nodOff, 2.5);
                NodOffStage seen2 = Advance(nodOff, 0.6);
                NodOffStage seen3 = Advance(nodOff, 1.9);
                NodOffStage seen4 = Advance(nodOff, 10.1);
                NodOffStage seen5 = Advance(nodOff, 1.5);
                Check(seen1 == NodOffStage.Startled && seen2 == NodOffStage.DroopingDeep
                    && seen3 == NodOffStage.Curled && seen4 == NodOffStage.WakeStretch && seen5 == NodOffStage.Done,
                    "nodoff-stage-sequence", log, ref failures);
                NodOffPose droop = NodOffDirector.PoseAt(NodOffStage.Drooping, 2.4);
                NodOffPose startle = NodOffDirector.PoseAt(NodOffStage.Startled, 0.1);
                Check(droop.Tilt > 4.9 && droop.ScaleY < 0.95 && startle.StartleFlash && startle.Tilt < 0,
                    "nodoff-pose-curves", log, ref failures);
                Check(!nodOff.Active, "nodoff-completes", log, ref failures);

                // 哈欠窗口：深夜低兴奋 + 清晨中低兴奋，白天不哈欠
                Check(PetMotion.YawnWindow(new DateTime(2026, 7, 24, 22, 30, 0), 0.2)
                    && PetMotion.YawnWindow(new DateTime(2026, 7, 24, 8, 30, 0), 0.4)
                    && !PetMotion.YawnWindow(new DateTime(2026, 7, 24, 8, 30, 0), 0.6)
                    && !PetMotion.YawnWindow(new DateTime(2026, 7, 24, 12, 0, 0), 0.2),
                    "yawn-window", log, ref failures);

                // 午后犯困窗口：14:00-15:00 也允许分镜，13 点和 15 点不行
                Check(NodOffDirector.DrowsyWindow(new DateTime(2026, 7, 23, 14, 30, 0))
                    && NodOffDirector.DrowsyWindow(new DateTime(2026, 7, 23, 22, 30, 0))
                    && !NodOffDirector.DrowsyWindow(new DateTime(2026, 7, 23, 13, 0, 0))
                    && !NodOffDirector.DrowsyWindow(new DateTime(2026, 7, 23, 15, 0, 0)),
                    "nodoff-drowsy-window", log, ref failures);

                // 追尾巴转圈：枚举与时长挂进演出体系
                Check(AnimationPolicy.DurationFor(RareIdleShowType.TailChase) == AnimationPolicy.TailChaseShowDurationMs
                    && AnimationPolicy.TailChaseShowDurationMs >= 2000 && AnimationPolicy.TailChaseSpinDegrees == 720,
                    "tailchase-policy", log, ref failures);

                // ---------- 边缘判定 ----------
                Check(EdgeLogic.ClassifyRelease(1.5, 0.1, 0, false) == ReleaseAction.Snap, "edge-snap-on-subject-contact", log, ref failures);
                Check(EdgeLogic.ClassifyRelease(18, 0.1, 0, false) == ReleaseAction.None, "edge-no-early-snap", log, ref failures);
                Check(EdgeLogic.ClassifyRelease(1.5, 0.8, 0, false) == ReleaseAction.Fin, "edge-fin-on-contact", log, ref failures);
                Check(EdgeLogic.ClassifyRelease(200, 0, 4, false) == ReleaseAction.Retreat, "edge-retreat", log, ref failures);
                Check(EdgeLogic.ClassifyRelease(10, 1.0, 0, true) == ReleaseAction.None, "edge-corridor-exempt", log, ref failures);
                Check(EdgeLogic.CountShakeReversals(new List<double> { 0, 30, 0, 30, 0, 30 }, 18) == 4, "edge-shake-count", log, ref failures);
                double vRight = EdgeLogic.EstimateVelocity(
                    new List<double> { 100, 140, 180 }, new List<double> { 9.7, 9.85, 10.0 }, 10.0, true);
                double vLeft = EdgeLogic.EstimateVelocity(
                    new List<double> { 100, 140, 180 }, new List<double> { 9.7, 9.85, 10.0 }, 10.0, false);
                Check(vRight > 0.15 && vLeft < 0, "edge-velocity-sign", log, ref failures);

                // 新数据字段默认值（老 data.json 无这些字段，由构造器兜底）
                Check(store.Data.BehaviorEnabled && store.Data.EdgeSnapEnabled
                    && store.Data.FollowMonitorEnabled && store.Data.PresentationGuardEnabled,
                    "behavior-defaults-on", log, ref failures);
                Check(!store.Data.HideFromCaptureEnabled, "capture-default-off", log, ref failures);
                Check(store.Data.SchemaVersion == 13 && store.Data.PetSize == 190
                    && !string.IsNullOrEmpty(store.Data.FirstCompanionDate)
                    && Math.Abs(store.Data.PetScaleRatio - ResponsivePetSizing.CompactScaleRatio) < 0.001,
                    "schema-v13-companion-presence-fields", log, ref failures);
                Check(store.Data.MeetingRadarEnabled && store.Data.MemoNudgesEnabled && store.Data.StretchEnabled,
                    "v17-feature-defaults-on", log, ref failures);
                Check(store.Data.WorkBreakReminderEnabled && store.Data.WorkBreakMinutes == 60 && store.Data.WeatherCity == "上海",
                    "v122-capability-defaults", log, ref failures);
                Check(store.Data.WeatherSentinelEnabled && store.Data.OutdoorAdvisorEnabled && store.Data.DailyBriefingEnabled
                    && store.Data.DailyBriefingHour == 9 && store.Data.LastWeatherAlertKey == ""
                    && store.Data.LastOutdoorAlertKey == "" && store.Data.LastDailyBriefingDate == "",
                    "v123-active-companion-defaults", log, ref failures);
                Check(store.Data.AmbientPresenceEnabled && store.Data.EnergyMode == "steady"
                    && store.Data.DailyPriorityDate == "" && store.Data.LastPriorityPromptDate == "",
                    "v124-adaptive-companion-defaults", log, ref failures);
                Check(WindowPrivacy.WdaExcludeFromCapture == 0x11, "capture-affinity-constant", log, ref failures);

                // ---------- v1.3 审核轮新增（Kimi）----------
                // 高精度滚轮按"格数"换算（触控板惯性滚动不虚报）
                Check(RawInputMonitor.WheelNotches(120) == 1.0 && RawInputMonitor.WheelNotches(-240) == 2.0
                    && RawInputMonitor.WheelNotches(60) == 0.5, "wheel-notches", log, ref failures);

                // TickCount 负值（开机 >24.8 天）回归：空闲计算不被负数哨兵卡死
                Check(RawInputMonitor.ComputeIdle(double.MinValue, -1244500.0) == 0, "idle-sentinel-negative-clock", log, ref failures);
                Check(RawInputMonitor.ComputeIdle(-1244500.0, -1244490.0) == 10, "idle-negative-clock", log, ref failures);
                Check(RawInputMonitor.ComputeIdle(100.0, 105.0) == 5, "idle-normal-clock", log, ref failures);

                // ConsentStore 判定规则（终审 P0 的判定核）
                Check(MediaStateMonitor.ScanConsentValues(10L, 0L), "consent-in-use", log, ref failures);
                Check(!MediaStateMonitor.ScanConsentValues(0L, 0L), "consent-never-used", log, ref failures);
                Check(!MediaStateMonitor.ScanConsentValues(10L, 20L), "consent-released", log, ref failures);

                // 摸头检测：慢速来回抚摸 → true；快速甩（拖拽甩退）→ false
                Check(PatLogic.IsPat(
                    new List<double> { 0, 20, 40, 20, 0, 20, 40 },
                    new List<double> { 0, 0.2, 0.4, 0.6, 0.8, 1.0, 1.2 }), "pat-gentle-strokes", log, ref failures);
                Check(!PatLogic.IsPat(
                    new List<double> { 0, 90, 0, 90, 0, 90, 0, 90, 0 },
                    new List<double> { 0, 0.0625, 0.125, 0.1875, 0.25, 0.3125, 0.375, 0.4375, 0.5 }), "pat-fast-shake-not-pat", log, ref failures);
                Check(!PatLogic.IsPat(
                    new List<double> { 0, 5 },
                    new List<double> { 0, 0.1 }), "pat-too-few-samples", log, ref failures);

                // say 截断
                Check(Formatters.Truncate("短消息", 64) == "短消息", "truncate-short", log, ref failures);
                Check(Formatters.Truncate(new string('长', 100), 64).Length == 65, "truncate-long", log, ref failures);

                // 久坐提醒 + 休息奖励闭环（阈值降到 3 秒做快测）
                engine = new StateEngine();
                engine.LongSittingThresholdSec = 3;
                System.Collections.Generic.List<string> evts = FeedEvents(engine, delegate(SignalSample s) { s.IdleIntentionalSeconds = 0; return s; }, 8);
                Check(evts != null && evts.Contains("long-sitting"), "long-sitting-fired", log, ref failures);
                evts = FeedEvents(engine, delegate(SignalSample s) { s.IdleSeconds = 200; s.IdleIntentionalSeconds = 200; return s; }, 2);
                Check(evts != null && evts.Contains("break-reward"), "break-reward-fired", log, ref failures);
                engine = new StateEngine();
                engine.LongSittingEnabled = false;
                engine.LongSittingThresholdSec = 1;
                evts = FeedEvents(engine, delegate(SignalSample s) { s.IdleIntentionalSeconds = 0; return s; }, 8);
                Check(evts == null && engine.ContinuousActiveSec == 0, "long-sitting-disabled-resets", log, ref failures);
                Check(WorkBreakPresentationPolicy.CanPresent(BehaviorState.Typing, false, false, false, false, false)
                    && !WorkBreakPresentationPolicy.CanPresent(BehaviorState.Meeting, false, false, false, false, false)
                    && !WorkBreakPresentationPolicy.CanPresent(BehaviorState.Typing, true, false, false, false, false)
                    && !WorkBreakPresentationPolicy.CanPresent(BehaviorState.Typing, false, true, false, false, false)
                    && !WorkBreakPresentationPolicy.CanPresent(BehaviorState.Typing, false, false, true, false, false)
                    && !WorkBreakPresentationPolicy.CanPresent(BehaviorState.Typing, false, false, false, true, false),
                    "work-break-deferred-in-busy-contexts", log, ref failures);
                Check(ProactivePresentationPolicy.CanPresent(BehaviorState.Idle, false, false, false, false, false, false)
                    && !ProactivePresentationPolicy.CanPresent(BehaviorState.Typing, false, false, false, false, false, false)
                    && !ProactivePresentationPolicy.CanPresent(BehaviorState.Meeting, false, false, false, false, false, false)
                    && !ProactivePresentationPolicy.CanPresent(BehaviorState.Idle, true, false, false, false, false, false)
                    && !ProactivePresentationPolicy.CanPresent(BehaviorState.Idle, false, false, false, false, false, true),
                    "proactive-notices-deferred-in-busy-contexts", log, ref failures);
                Check(WindowsOcrService.NormalizeHanSpacing("中 文 OCR A B") == "中文 OCR A B",
                    "ocr-han-spacing-normalization", log, ref failures);
                WeatherSnapshot weatherFixture = OpenMeteoWeatherService.ParseForTest(
                    "{\"name\":\"上海\",\"admin1\":\"上海\",\"country\":\"中国\"}",
                    "{\"current\":{\"temperature_2m\":28.5,\"apparent_temperature\":30.1,\"weather_code\":2,\"wind_speed_10m\":12.3},\"daily\":{\"temperature_2m_max\":[32.0],\"temperature_2m_min\":[24.0],\"precipitation_probability_max\":[75]},\"hourly\":{\"time\":[\"2026-08-09T10:00\",\"2026-08-09T11:00\"],\"precipitation_probability\":[10,75],\"weather_code\":[2,61]}}");
                Check(weatherFixture.Location == "上海 · 中国" && weatherFixture.Condition == "局部多云"
                    && Math.Abs(weatherFixture.Temperature - 28.5) < 0.01 && weatherFixture.RainChance == 75
                    && weatherFixture.NextRainHours == 1 && weatherFixture.NextRainChance == 75,
                    "weather-json-parser", log, ref failures);
                AmbientSnapshot ambientFixture = OpenMeteoWeatherService.ParseAmbientForTest(
                    "{\"name\":\"上海\",\"admin1\":\"上海\",\"country\":\"中国\"}",
                    "{\"current\":{\"temperature_2m\":28.5,\"apparent_temperature\":30.1,\"weather_code\":2,\"wind_speed_10m\":12.3},\"daily\":{\"temperature_2m_max\":[32.0],\"temperature_2m_min\":[24.0],\"precipitation_probability_max\":[75]},\"hourly\":{\"time\":[\"2026-08-09T10:00\",\"2026-08-09T11:00\"],\"precipitation_probability\":[10,75],\"weather_code\":[2,61]}}",
                    "{\"current\":{\"european_aqi\":65,\"pm2_5\":30.5,\"uv_index\":9.1,\"alder_pollen\":null,\"birch_pollen\":null,\"grass_pollen\":null,\"mugwort_pollen\":null,\"ragweed_pollen\":null}}");
                Check(ambientFixture.Outdoor.HasAirQuality && Math.Abs(ambientFixture.Outdoor.Pm25 - 30.5) < 0.01
                    && ambientFixture.Outdoor.HasUv && !ambientFixture.Outdoor.HasPollen
                    && ambientFixture.Outdoor.Summary.Contains("空气指数 65") && ambientFixture.Outdoor.Summary.Contains("紫外线 9.1"),
                    "outdoor-json-parser-null-pollen-is-unavailable", log, ref failures);
                AmbientPresenceStyle rainPresence = AmbientPresencePolicy.Resolve(ambientFixture, true);
                AmbientSnapshot healthOnlyAmbient = new AmbientSnapshot
                {
                    Weather = new WeatherSnapshot { Location = "上海", Condition = "晴", Temperature = 29, NextRainHours = -1 },
                    Outdoor = ambientFixture.Outdoor,
                    RetrievedAt = DateTime.Now
                };
                AmbientPresenceStyle healthPresence = AmbientPresencePolicy.Resolve(healthOnlyAmbient, true);
                Check(rainPresence != null && rainPresence.Priority == 100 && rainPresence.Label.Contains("雨")
                    && healthPresence != null && healthPresence.Priority == 90 && healthPresence.Label.Contains("AQI")
                    && AmbientPresencePolicy.Resolve(ambientFixture, false) == null,
                    "ambient-presence-prefers-actionable-signal", log, ref failures);
                Check(!CompanionEnergyPolicy.AllowsAutonomousMotion("low")
                    && CompanionEnergyPolicy.AllowsAutonomousMotion("steady")
                    && CompanionEnergyPolicy.MotionDelayMultiplier("high") < 1
                    && CompanionEnergyPolicy.MinimumProactivePriority("low") == 90,
                    "energy-checkin-adapts-motion-and-notice-threshold", log, ref failures);
                DateTime priorityTime = new DateTime(2026, 8, 9, 9, 0, 0);
                Check(DailyPriorityPromptPolicy.CanPrompt(priorityTime, "", false, 2, BehaviorState.Idle,
                        false, false, false, false, false, false)
                    && !DailyPriorityPromptPolicy.CanPrompt(priorityTime, "", false, 2, BehaviorState.Typing,
                        false, false, false, false, false, false)
                    && !DailyPriorityPromptPolicy.CanPrompt(priorityTime, "2026-08-09", false, 2, BehaviorState.Idle,
                        false, false, false, false, false, false),
                    "daily-priority-prompt-is-once-and-low-interruption", log, ref failures);
                List<CompanionNotice> activeNotices = AmbientInsightPolicy.Evaluate(ambientFixture, true, true,
                    new DateTime(2026, 8, 9, 10, 0, 0));
                Check(activeNotices.Count == 2 && activeNotices[0].Category == "weather" && activeNotices[0].Cue == "☂"
                    && activeNotices[0].ExpiresAt > new DateTime(2026, 8, 9, 10, 0, 0)
                    && activeNotices[1].Category == "outdoor" && activeNotices[1].Message.Contains("空气指数 65"),
                    "ambient-insight-priority-and-rain-window", log, ref failures);
                Check(AmbientInsightPolicy.Evaluate(ambientFixture, false, false, new DateTime(2026, 8, 9)).Count == 0,
                    "ambient-insight-respects-user-toggles", log, ref failures);
                ProactiveNoticeQueue noticeQueue = new ProactiveNoticeQueue();
                noticeQueue.EnqueueLatest(activeNotices[0]);
                noticeQueue.EnqueueLatest(new CompanionNotice
                {
                    Category = "weather", Key = "rain-newer", Cue = "☂", Message = "较新的降雨提醒", Priority = 100,
                    ExpiresAt = new DateTime(2026, 8, 9, 12, 0, 0)
                });
                noticeQueue.EnqueueLatest(activeNotices[1]);
                CompanionNotice firstQueued = noticeQueue.DequeueReady(new DateTime(2026, 8, 9, 10, 5, 0));
                CompanionNotice secondQueued = noticeQueue.DequeueReady(new DateTime(2026, 8, 9, 10, 5, 0));
                Check(noticeQueue.Count == 0 && firstQueued != null && firstQueued.Key == "rain-newer"
                    && secondQueued != null && secondQueued.Category == "outdoor",
                    "proactive-queue-supersedes-category-and-keeps-priority", log, ref failures);
                noticeQueue.EnqueueLatest(new CompanionNotice
                {
                    Category = "weather", Key = "stale", Priority = 100,
                    ExpiresAt = new DateTime(2026, 8, 9, 9, 0, 0)
                });
                Check(noticeQueue.DequeueReady(new DateTime(2026, 8, 9, 10, 0, 0)) == null && noticeQueue.Count == 0,
                    "proactive-queue-drops-expired-notice", log, ref failures);
                Check(OpenMeteoWeatherService.CacheLifetimeFor(true) == TimeSpan.FromMinutes(2)
                    && OpenMeteoWeatherService.CacheLifetimeFor(false) == TimeSpan.FromMinutes(15),
                    "partial-ambient-cache-retries-sooner", log, ref failures);
                Check(!OpenMeteoWeatherService.ShouldAcceptCacheWrite(9, 8)
                    && OpenMeteoWeatherService.ShouldAcceptCacheWrite(9, 9)
                    && OpenMeteoWeatherService.ShouldAcceptCacheWrite(9, 10),
                    "ambient-cache-rejects-older-late-response", log, ref failures);
                bool incompleteWeatherRejected = false;
                try
                {
                    OpenMeteoWeatherService.ParseForTest(
                        "{\"name\":\"上海\",\"latitude\":31.2,\"longitude\":121.5}",
                        "{\"current\":{\"weather_code\":2},\"daily\":{\"temperature_2m_max\":[32]}}");
                }
                catch (InvalidDataException) { incompleteWeatherRejected = true; }
                Check(incompleteWeatherRejected, "weather-parser-rejects-missing-numbers-instead-of-zero", log, ref failures);
                string briefingFixture = DailyBriefingBuilder.Build(ambientFixture,
                    new MeetingInfo { Subject = "技术方案评审", Start = new DateTime(2026, 8, 9, 11, 30, 0), End = new DateTime(2026, 8, 9, 12, 0, 0) },
                    3, "完善主动天气提醒", new DateTime(2026, 8, 9, 10, 0, 0));
                Check(briefingFixture.Contains("上海 · 中国") && briefingFixture.Contains("11:30 · 技术方案评审")
                    && briefingFixture.Contains("未完成记录 3 条") && briefingFixture.Contains("当前锚点：完善主动天气提醒"),
                    "daily-briefing-combines-external-and-local-context", log, ref failures);
                string offlineBriefing = DailyBriefingBuilder.Build(null, null, 2, "整理发布说明", new DateTime(2026, 8, 9, 10, 0, 0));
                Check(offlineBriefing.Contains("天气暂不可用") && offlineBriefing.Contains("户外健康数据暂不可用")
                    && offlineBriefing.Contains("未完成记录 2 条") && offlineBriefing.Contains("整理发布说明"),
                    "daily-briefing-keeps-local-context-when-network-fails", log, ref failures);
                DateTime freshnessNow = new DateTime(2026, 8, 9, 10, 0, 0);
                Check(AmbientFreshnessPolicy.ForBriefing(new AmbientSnapshot { RetrievedAt = freshnessNow.AddMinutes(-89) }, freshnessNow) != null
                    && AmbientFreshnessPolicy.ForBriefing(new AmbientSnapshot { RetrievedAt = freshnessNow.AddMinutes(-91) }, freshnessNow) == null,
                    "daily-briefing-rejects-stale-ambient-data", log, ref failures);
                Check(AmbientFreshnessPolicy.ForPresence(new AmbientSnapshot { RetrievedAt = freshnessNow.AddMinutes(-89) }, freshnessNow) != null
                    && AmbientFreshnessPolicy.ForPresence(new AmbientSnapshot { RetrievedAt = freshnessNow.AddMinutes(-91) }, freshnessNow) == null,
                    "ambient-presence-rejects-stale-status", log, ref failures);

                // ---------- v1.5 高 ROI 轮新增 ----------
                // 四边缩入几何
                System.Windows.Rect area = new System.Windows.Rect(0, 0, 1707, 912);
                System.Windows.Point sl = DockGeometry.SliverTarget(DockEdge.Right, area, 240, 254, 600);
                System.Windows.Point sl2 = DockGeometry.SliverTarget(DockEdge.Bottom, area, 240, 254, 600);
                System.Windows.Point pk = DockGeometry.PeekTarget(DockEdge.Right, area, 240, 254, 600);
                System.Windows.Point slLeft = DockGeometry.SliverTarget(DockEdge.Left, area, 240, 254, 600);
                System.Windows.Point slTop = DockGeometry.SliverTarget(DockEdge.Top, area, 240, 254, 600);
                double visible = DockGeometry.SliverVisibleFor(240, 254);
                Check(sl.X == 1707 - visible && sl.Y == 600, "dock-sliver-right", log, ref failures);
                Check(sl2.Y == 912 - visible && sl2.X == 0, "dock-sliver-bottom", log, ref failures);
                Check(slLeft.X == visible - 240 && slLeft.Y == 600, "dock-sliver-left", log, ref failures);
                Check(slTop.Y == visible - 254 && slTop.X == 0, "dock-sliver-top", log, ref failures);
                Check(Math.Abs(pk.X - (1707 - 240 * 0.65)) < 0.001, "dock-peek-right", log, ref failures);
                Check(DockGeometry.ParsePersistedEdge("RIGHT") == DockEdge.Right &&
                    DockGeometry.ParsePersistedEdge("unknown") == DockEdge.None &&
                    DockGeometry.PersistedEdge(DockEdge.Top) == "Top", "dock-persistence-values", log, ref failures);
                Check(DockGeometry.InferFlushEdge(area, 1707 - 240 - 4, 600, 240, 254, 8) == DockEdge.Right &&
                    DockGeometry.InferFlushEdge(area, 400, 400, 240, 254, 8) == DockEdge.None,
                    "dock-v15-flush-inference", log, ref failures);

                // 打字节奏：限频 + 左右交替
                TypingRhythm rhythm = new TypingRhythm();
                Check(rhythm.Accept(10.00) && !rhythm.Accept(10.02) && rhythm.Accept(10.10), "rhythm-rate-limit", log, ref failures);
                int signA = rhythm.Sign;
                rhythm.Accept(10.20);
                Check(rhythm.Sign == -signA, "rhythm-alternate", log, ref failures);

                TypingPawDirector pawDirector = new TypingPawDirector();
                TypingPawPose pawA;
                TypingPawPose pawB;
                Check(pawDirector.TryPress(20.000, out pawA) && pawA == TypingPawPose.LeftDown
                    && pawDirector.PoseAt(20.080) == TypingPawPose.LeftSoft
                    && pawDirector.PoseAt(20.401) == TypingPawPose.Rest,
                    "typing-paw-press-hold-return", log, ref failures);
                Check(pawDirector.TryPress(20.500, out pawB) && pawB == TypingPawPose.RightDown,
                    "typing-paw-left-right-exclusive", log, ref failures);
                Check(AnimationPolicy.TypingPressHoldMs < AnimationPolicy.TypingReturnToRestMs
                    && AnimationPolicy.TypingReturnToRestMs == 400,
                    "typing-paw-return-window-400ms", log, ref failures);

                // 心流：连续打字 3 秒进入，离开 90 秒宽限后退出
                engine = new StateEngine();
                engine.FlowThresholdSec = 3;
                BehaviorState flowState = Feed(engine, delegate(SignalSample s) { s.KeyPerMin = 150; return s; }, 8);
                EngineOutput flowOut = FeedOutput(engine, delegate(SignalSample s) { s.KeyPerMin = 150; return s; }, 1);
                Check(flowState == BehaviorState.Typing && flowOut.FlowActive, "flow-active", log, ref failures);
                flowOut = FeedOutput(engine, delegate(SignalSample s) { return s; }, 4);
                Check(flowOut.FlowActive, "flow-grace-holds", log, ref failures);

                // 中断恢复书签：先记备忘正文，回来给一句话
                MemoItem pending = store.Data.Memos.FirstOrDefault(delegate(MemoItem m) { return !m.IsDone; });
                MemoItem newerPending = store.AddMemo("刚刚插入但不是当前任务", "short", false, false);
                store.SetAnchor(pending == null ? "" : pending.Id);
                BookmarkService bookmark = new BookmarkService();
                bookmark.Capture(store, "开发工具");
                string resume = bookmark.BuildMeetingResume(47);
                Check(resume != null && resume.Contains("会议结束（47 分钟）") && pending != null && resume.Contains(Formatters.Truncate(pending.Text, 20).TrimEnd('…')), "bookmark-meeting-resume", log, ref failures);
                Check(resume == null || !resume.Contains(newerPending.Text), "bookmark-prefers-anchor", log, ref failures);
                Check(bookmark.BuildAwayResume(23) == null, "bookmark-cleared", log, ref failures);

                InterruptionSession interruption = new InterruptionSession();
                DateTime interruptedAt = new DateTime(2026, 7, 20, 10, 0, 0);
                interruption.Begin(store, "开发工具", false, interruptedAt);
                interruption.Continue(true);
                string chainedResume = interruption.End(interruptedAt.AddMinutes(12));
                Check(chainedResume != null && chainedResume.Contains("会议结束（12 分钟）") && !interruption.Active,
                    "bookmark-defers-across-away-meeting-chain", log, ref failures);

                // 锚点：钉住 → 完成 → 清空
                if (pending != null)
                {
                    int beforePriority = store.Data.CompanionValue;
                    store.SetAnchor(pending.Id);
                    store.Data.DailyPriorityDate = DateTime.Today.AddDays(-1).ToString("yyyy-MM-dd");
                    store.Save();
                    Check(store.ClearExpiredDailyPriority(DateTime.Now) && store.AnchorMemo() == null
                        && store.Data.DailyPriorityDate == "" && !pending.IsDone,
                        "daily-priority-cross-day-reset-keeps-memo", log, ref failures);
                    store.Data.EnergyMode = "low";
                    store.Data.EnergyModeDate = DateTime.Today.AddDays(-1).ToString("yyyy-MM-dd");
                    store.Save();
                    Check(store.ClearExpiredEnergyMode(DateTime.Now) && store.TodayEnergyMode == "steady"
                        && store.Data.EnergyMode == "steady" && store.Data.EnergyModeDate == "",
                        "energy-mode-cross-day-resets-to-steady", log, ref failures);
                    store.SetAnchor(pending.Id);
                    Check(store.AnchorMemo() != null && store.AnchorMemo().Id == pending.Id
                        && store.IsDailyPriority(pending), "anchor-set", log, ref failures);
                    MemoItem completed = store.CompleteAnchor();
                    Check(completed != null && completed.IsDone && store.AnchorMemo() == null
                        && store.Data.CompanionValue == beforePriority + 6
                        && store.Data.LastPriorityRewardDate == DateTime.Today.ToString("yyyy-MM-dd"),
                        "anchor-complete", log, ref failures);
                    store.SetMemoDone(completed, false);
                    Check(!completed.IsDone && store.Data.CompanionValue == beforePriority
                        && !completed.DailyPriorityBonusAwarded
                        && store.Data.LastPriorityRewardDate == "" && store.Data.LastPriorityRewardMemoId == "",
                        "daily-priority-undo-reverses-full-reward", log, ref failures);

                    store.SetAnchor(completed.Id);
                    store.CompleteAnchor();
                    int afterHistoricalPriority = store.Data.CompanionValue;
                    store.Data.LastPriorityRewardDate = "2099-01-01";
                    store.Data.LastPriorityRewardMemoId = "newer-priority";
                    store.Save();
                    store.SetMemoDone(completed, false);
                    Check(store.Data.CompanionValue == afterHistoricalPriority - 6
                        && store.Data.LastPriorityRewardDate == "2099-01-01"
                        && store.Data.LastPriorityRewardMemoId == "newer-priority"
                        && !completed.DailyPriorityBonusAwarded,
                        "historical-daily-priority-undo-keeps-newer-ledger", log, ref failures);
                    store.Data.LastPriorityRewardDate = "";
                    store.Data.LastPriorityRewardMemoId = "";
                    store.Save();
                }

                // 心流记账与周报素材
                store.AddFlowSeconds(600);
                store.Data.DockSide = "Bottom";
                store.Save();
                DataStore dockReloaded = new DataStore();
                Check(dockReloaded.Data.SchemaVersion == 13 && dockReloaded.Data.DockSide == "Bottom", "dock-side-json-reload", log, ref failures);
                Check(store.TodayFlowSeconds == 600, "flow-seconds-store", log, ref failures);
                string weekly = WeeklyReport.BuildText(store, DateTime.Now);
                Check(weekly.Contains("心流时间：10 分钟") && weekly.Contains("回复评审意见") && weekly.Contains("本周小结"), "weekly-report-text", log, ref failures);
                Check(WeeklyReport.WeekStart(new DateTime(2026, 7, 19)) == new DateTime(2026, 7, 13), "weekly-weekstart", log, ref failures);

                string oldTestRoot = Environment.GetEnvironmentVariable("WORKMATE_TEST_DIR");
                string legacyRoot = Path.Combine(store.RootDirectory, "legacy-v3");
                Directory.CreateDirectory(legacyRoot);
                File.WriteAllText(Path.Combine(legacyRoot, "data.json"), "{\"SchemaVersion\":3,\"PetId\":\"01-cat\"}", new UTF8Encoding(false));
                try
                {
                    Environment.SetEnvironmentVariable("WORKMATE_TEST_DIR", legacyRoot);
                    DataStore migrated = new DataStore();
                    Check(migrated.LoadedSchemaVersion == 3 && migrated.Data.SchemaVersion == 13 && string.IsNullOrEmpty(migrated.Data.DockSide) && !migrated.Data.HideFromCaptureEnabled
                        && migrated.Data.MeetingRadarEnabled && migrated.Data.MemoNudgesEnabled && migrated.Data.StretchEnabled
                        && migrated.Data.WorkBreakReminderEnabled && migrated.Data.WorkBreakMinutes == 60 && migrated.Data.WeatherCity == "上海"
                        && migrated.Data.WeatherSentinelEnabled && migrated.Data.OutdoorAdvisorEnabled && migrated.Data.DailyBriefingEnabled && migrated.Data.DailyBriefingHour == 9
                        && migrated.Data.AmbientPresenceEnabled && migrated.Data.EnergyMode == "steady"
                        && migrated.Data.PetSize == 190 && Math.Abs(migrated.Data.PetScaleRatio - ResponsivePetSizing.CompactScaleRatio) < 0.001,
                        "schema-v3-capture-safe-migration", log, ref failures);
                }
                finally { Environment.SetEnvironmentVariable("WORKMATE_TEST_DIR", oldTestRoot); }

                string legacyV12Root = Path.Combine(store.RootDirectory, "legacy-v12-anchor");
                Directory.CreateDirectory(legacyV12Root);
                File.WriteAllText(Path.Combine(legacyV12Root, "data.json"),
                    "{\"SchemaVersion\":12,\"PetId\":\"01-cat\",\"AnchorMemoId\":\"legacy-anchor\",\"Memos\":[{\"Id\":\"legacy-anchor\",\"Text\":\"旧版普通锚点\",\"Term\":\"short\"}]}",
                    new UTF8Encoding(false));
                try
                {
                    Environment.SetEnvironmentVariable("WORKMATE_TEST_DIR", legacyV12Root);
                    DataStore migratedV12 = new DataStore();
                    Check(migratedV12.LoadedSchemaVersion == 12 && migratedV12.Data.SchemaVersion == 13
                        && migratedV12.Data.AnchorMemoId == "" && migratedV12.Data.DailyPriorityDate == ""
                        && migratedV12.Data.AmbientPresenceEnabled && migratedV12.TodayEnergyMode == "steady",
                        "schema-v12-clears-legacy-anchor-before-daily-priority", log, ref failures);
                }
                finally { Environment.SetEnvironmentVariable("WORKMATE_TEST_DIR", oldTestRoot); }

                string staleV13Root = Path.Combine(store.RootDirectory, "schema-v13-stale-daily-state");
                Directory.CreateDirectory(staleV13Root);
                File.WriteAllText(Path.Combine(staleV13Root, "data.json"),
                    "{\"SchemaVersion\":13,\"PetId\":\"01-cat\",\"AnchorMemoId\":\"stale-anchor\",\"DailyPriorityDate\":\"2000-01-01\",\"EnergyMode\":\"low\",\"EnergyModeDate\":\"2000-01-01\",\"Memos\":[{\"Id\":\"stale-anchor\",\"Text\":\"跨日任务\",\"Term\":\"short\"}]}",
                    new UTF8Encoding(false));
                try
                {
                    Environment.SetEnvironmentVariable("WORKMATE_TEST_DIR", staleV13Root);
                    DataStore normalizedV13 = new DataStore();
                    string normalizedV13Json = File.ReadAllText(normalizedV13.DataPath, Encoding.UTF8);
                    Check(normalizedV13.LoadedSchemaVersion == 13 && normalizedV13.Data.AnchorMemoId == ""
                        && normalizedV13.Data.DailyPriorityDate == "" && normalizedV13.Data.EnergyMode == "steady"
                        && normalizedV13.Data.EnergyModeDate == "" && normalizedV13Json.Contains("\"EnergyMode\":\"steady\"")
                        && normalizedV13Json.Contains("\"AnchorMemoId\":\"\""),
                        "schema-v13-persists-cross-day-normalization", log, ref failures);
                }
                finally { Environment.SetEnvironmentVariable("WORKMATE_TEST_DIR", oldTestRoot); }

                string legacyV4Root = Path.Combine(store.RootDirectory, "legacy-v4-capture-on");
                Directory.CreateDirectory(legacyV4Root);
                File.WriteAllText(Path.Combine(legacyV4Root, "data.json"),
                    "{\"SchemaVersion\":4,\"PetId\":\"01-cat\",\"HideFromCaptureEnabled\":true}", new UTF8Encoding(false));
                try
                {
                    Environment.SetEnvironmentVariable("WORKMATE_TEST_DIR", legacyV4Root);
                    DataStore migratedV4 = new DataStore();
                    Check(migratedV4.LoadedSchemaVersion == 4 && migratedV4.Data.SchemaVersion == 13 && string.IsNullOrEmpty(migratedV4.Data.DockSide) && !migratedV4.Data.HideFromCaptureEnabled,
                        "schema-v4-capture-safe-migration", log, ref failures);
                }
                finally { Environment.SetEnvironmentVariable("WORKMATE_TEST_DIR", oldTestRoot); }

                // ---------- v1.7：小满/阿企 300 帧 / 催办 / 叼物 / 热力图 / 打断日报 ----------
                Check(PetAssets.SupportsAnimation("03-penguin") && PetAssets.SupportsAnimation("01-cat")
                    && !PetAssets.SupportsAnimation("05-rabbit")
                    && PetAssets.AnimationFrameCount * PetAssets.AnimationSheetCount * PetAssets.AnimatedPetCount == 300,
                    "animation-300-distinct-cells", log, ref failures);
                System.Windows.Media.Imaging.BitmapSource firstAnimationFrame = PetAssets.GetAnimationFrame("03-penguin", "dock-right", 0);
                System.Windows.Media.Imaging.BitmapSource lastAnimationFrame = PetAssets.GetAnimationFrame("03-penguin", "idle-life", 24);
                System.Windows.Media.Imaging.BitmapSource catAnimationFrame = PetAssets.GetAnimationFrame("01-cat", "typing", 12);
                Check(firstAnimationFrame != null && firstAnimationFrame.PixelWidth == 251 && lastAnimationFrame != null
                    && catAnimationFrame != null && catAnimationFrame.PixelHeight == 251,
                    "animation-frame-load-both-pets", log, ref failures);
                List<System.Windows.Point> penguinStretchAnchors = new List<System.Windows.Point>();
                for (int frame = 0; frame <= AnimationPolicy.StretchPeakFrame("03-penguin"); frame++)
                    penguinStretchAnchors.Add(PetAssets.AnimationGroundAnchor(PetAssets.GetAnimationFrame("03-penguin", "stretch", frame)));
                List<System.Windows.Point> catStretchAnchors = new List<System.Windows.Point>();
                for (int frame = 0; frame <= AnimationPolicy.StretchPeakFrame("01-cat"); frame++)
                    catStretchAnchors.Add(PetAssets.AnimationGroundAnchor(PetAssets.GetAnimationFrame("01-cat", "stretch", frame)));
                double penguinGroundSpread = penguinStretchAnchors.Max(x => x.Y) - penguinStretchAnchors.Min(x => x.Y);
                double catGroundSpread = catStretchAnchors.Max(x => x.Y) - catStretchAnchors.Min(x => x.Y);
                double penguinCenterSpread = penguinStretchAnchors.Max(x => x.X) - penguinStretchAnchors.Min(x => x.X);
                double catCenterSpread = catStretchAnchors.Max(x => x.X) - catStretchAnchors.Min(x => x.X);
                log.Add(string.Format("METRIC ground-anchor penguinY={0:0.00} catY={1:0.00} penguinX={2:0.00} catX={3:0.00}",
                    penguinGroundSpread, catGroundSpread, penguinCenterSpread, catCenterSpread));
                Check(penguinGroundSpread <= 1.1 && catGroundSpread <= 1.1
                    && penguinCenterSpread <= 3.0 && catCenterSpread <= 3.0,
                    "animation-ground-anchor-stable", log, ref failures);

                System.Diagnostics.Stopwatch typingLatency = System.Diagnostics.Stopwatch.StartNew();
                PetAssets.WarmTypingSoftFrames("03-penguin");
                typingLatency.Restart();
                System.Windows.Media.Imaging.BitmapSource typingSoft = PetAssets.GetTypingSoftFrame("03-penguin", TypingPawPose.LeftDown);
                typingLatency.Stop();
                Check(typingSoft != null && typingLatency.ElapsedMilliseconds < 50,
                    "typing-soft-cached-response-under-50ms", log, ref failures);
                Check(PetAssets.TypingSoftOutsidePawDifferenceRatio("03-penguin", TypingPawPose.LeftDown) < 0.02
                    && PetAssets.TypingSoftOutsidePawDifferenceRatio("01-cat", TypingPawPose.RightDown) < 0.02,
                    "typing-soft-body-pixels-locked", log, ref failures);
                Check(PetAssets.TypingSoftOverallDifferenceRatio("03-penguin", TypingPawPose.LeftDown) > 0.003
                    && PetAssets.TypingSoftOverallDifferenceRatio("01-cat", TypingPawPose.RightDown) > 0.003,
                    "typing-soft-paw-motion-visible", log, ref failures);

                // ---------- v1.8：协调动画节律 ----------
                Check(AnimationPolicy.DockTuckDurationMs <= 300
                    && AnimationPolicy.DockTuckStartFrame == 14
                    && AnimationPolicy.DockTuckEndFrame == 19
                    && (AnimationPolicy.DockTuckEndFrame - AnimationPolicy.DockTuckStartFrame) % AnimationPolicy.DockTuckFrameStep == 0,
                    "animation-dock-under-300ms", log, ref failures);
                Check(!AnimationPolicy.ShouldLoop(BehaviorState.Typing)
                    && !AnimationPolicy.ShouldLoop(BehaviorState.Idle)
                    && !AnimationPolicy.ShouldLoop(BehaviorState.Meeting)
                    && !AnimationPolicy.ShouldLoop(BehaviorState.Reading),
                    "animation-no-unconditional-loops", log, ref failures);
                Check(AnimationPolicy.IdleGestureMinSeconds >= 25
                    && AnimationPolicy.IdleGestureMaxSeconds >= AnimationPolicy.IdleGestureMinSeconds,
                    "animation-idle-gesture-breathing-gap", log, ref failures);
                Check(AnimationPolicy.CarryFrameIntervalMs >= 95 && AnimationPolicy.CarryHoldMs >= 3000,
                    "animation-carry-visible-and-held", log, ref failures);
                Check(AnimationPolicy.MeetingEnterEndFrame < AnimationPolicy.MeetingExitStartFrame
                    && AnimationPolicy.MeetingExitEndFrame == 24
                    && AnimationPolicy.MeetingReminderHoldMs >= 800,
                    "choreo-meeting-enter-hold-exit", log, ref failures);
                Check(AnimationPolicy.StretchPeakFrame("03-penguin") == 8
                    && AnimationPolicy.StretchExitStartFrame("03-penguin") == 7
                    && AnimationPolicy.StretchExitEndFrame("03-penguin") == 3
                    && AnimationPolicy.StretchPeakFrame("01-cat") == 16
                    && AnimationPolicy.StretchExitStartFrame("01-cat") == 17
                    && AnimationPolicy.StretchExitEndFrame("01-cat") == 18
                    && AnimationPolicy.StretchPeakHoldMs >= 350,
                    "choreo-stretch-peak-readable", log, ref failures);
                Check(AnimationPolicy.FrameStep(7, 3) == -1
                    && AnimationPolicy.FrameStep(17, 18) == 1
                    && AnimationPolicy.FrameCount(7, 3) == 5,
                    "choreography-supports-reverse-exit", log, ref failures);
                int patStart;
                int patEnd;
                int[] penguinIdle = AnimationPolicy.SafeIdleGestureStarts("03-penguin");
                int[] catIdle = AnimationPolicy.SafeIdleGestureStarts("01-cat");
                Check(!penguinIdle.Contains(15) && !penguinIdle.Contains(20) && catIdle.Contains(20)
                    && !AnimationPolicy.TryGetPatClip("03-penguin", out patStart, out patEnd),
                    "choreo-pat-only-on-real-interaction", log, ref failures);
                Check(AnimationPolicy.CarryHoldFrame < AnimationPolicy.CarryReleaseStartFrame
                    && AnimationPolicy.CarryReleaseEndFrame == 24,
                    "choreo-carry-persistent-and-release", log, ref failures);
                Check(AnimationPolicy.IdleGestureHoldMs >= 180 && AnimationPolicy.IdleGestureHoldMs <= 400,
                    "choreo-idle-gesture-breathes-before-outro", log, ref failures);
                Random rareDelayRandom = new Random(20260722);
                int rareDelayMin = int.MaxValue;
                int rareDelayMax = 0;
                for (int i = 0; i < 64; i++)
                {
                    int delay = AnimationPolicy.NextRareIdleShowDelayMs(rareDelayRandom);
                    rareDelayMin = Math.Min(rareDelayMin, delay);
                    rareDelayMax = Math.Max(rareDelayMax, delay);
                }
                Check(rareDelayMin >= AnimationPolicy.RareIdleShowMinSeconds * 1000
                    && rareDelayMax <= AnimationPolicy.RareIdleShowMaxSeconds * 1000
                    && rareDelayMax > rareDelayMin,
                    "playful-show-is-rare-and-jittered", log, ref failures);
                RareIdleDirector rareDirector = new RareIdleDirector();
                Random rarePickRandom = new Random(1);
                HashSet<RareIdleShowType> rareCoverage = new HashSet<RareIdleShowType>();
                bool rareAvoidedRecent = true;
                for (int i = 0; i < 20; i++)
                {
                    List<RareIdleShowType> before = new List<RareIdleShowType>(rareDirector.Recent);
                    RareIdleShowType selected = rareDirector.Next(rarePickRandom);
                    if (before.Contains(selected)) rareAvoidedRecent = false;
                    rareCoverage.Add(selected);
                }
                Check(rareAvoidedRecent && rareCoverage.Count == 4,
                    "playful-director-avoids-two-recent-and-covers-library", log, ref failures);
                Check(AnimationPolicy.DurationFor(RareIdleShowType.ButterflyChase) == AnimationPolicy.ButterflyShowDurationMs
                    && AnimationPolicy.DurationFor(RareIdleShowType.BubbleBlow) == AnimationPolicy.BubbleShowDurationMs,
                    "playful-new-scenes-have-explicit-duration", log, ref failures);
                GroundingShadowStyle groundedShadow = GroundingShadowPolicy.Resolve(BehaviorState.Idle, false, false, false);
                GroundingShadowStyle airborneShadow = GroundingShadowPolicy.Resolve(BehaviorState.Idle, true, false, false);
                GroundingShadowStyle sleepingShadow = GroundingShadowPolicy.Resolve(BehaviorState.Sleepy, false, false, false);
                GroundingShadowStyle dockedShadow = GroundingShadowPolicy.Resolve(BehaviorState.Idle, false, false, true);
                Check(groundedShadow.Opacity > airborneShadow.Opacity
                    && groundedShadow.WidthRatio > airborneShadow.WidthRatio
                    && sleepingShadow.WidthRatio > groundedShadow.WidthRatio
                    && dockedShadow.Opacity == 0,
                    "grounding-shadow-reacts-to-air-and-sleep", log, ref failures);
                Check(AnimationPolicy.HulaMinStrokeDip >= 3.0,
                    "hula-readable-at-compact-size", log, ref failures);
                List<double> orbitX = new List<double>();
                List<double> orbitY = new List<double>();
                List<double> orbitT = new List<double>();
                for (int i = 0; i < 24; i++)
                {
                    double angle = i * Math.PI * 2 / 23.0;
                    orbitX.Add(Math.Cos(angle) * 64);
                    orbitY.Add(Math.Sin(angle) * 64);
                    orbitT.Add(i * 0.045);
                }
                Check(CursorOrbitPolicy.IsDeliberateOrbit(orbitX, orbitY, orbitT),
                    "cursor-orbit-deliberate-circle-accepted", log, ref failures);
                List<double> sweepX = new List<double>();
                List<double> sweepY = new List<double>();
                List<double> sweepT = new List<double>();
                for (int i = 0; i < 24; i++)
                {
                    sweepX.Add(-80 + (i % 12) * 14);
                    sweepY.Add(54);
                    sweepT.Add(i * 0.045);
                }
                Check(!CursorOrbitPolicy.IsDeliberateOrbit(sweepX, sweepY, sweepT),
                    "cursor-orbit-office-sweep-rejected", log, ref failures);
                AttentionVector nearRight = CursorAttentionPolicy.Resolve(90, 0, 220, 20);
                AttentionVector farAway = CursorAttentionPolicy.Resolve(500, 0, 220, 20);
                AttentionVector justInsideDeadZone = CursorAttentionPolicy.Resolve(17.9, 0, 219, 18);
                AttentionVector justOutsideDeadZone = CursorAttentionPolicy.Resolve(18.1, 0, 219, 18);
                double dampOne = CursorAttentionPolicy.Damp(0, 1, 0.05);
                double dampMany = 0;
                for (int i = 0; i < 20; i++) dampMany = CursorAttentionPolicy.Damp(dampMany, 1, 0.05);
                Check(nearRight.X > 0.25 && Math.Abs(nearRight.Y) < 0.001 && farAway.X == 0
                    && justInsideDeadZone.X == 0 && justOutsideDeadZone.X >= 0 && justOutsideDeadZone.X < 0.01
                    && dampOne > 0.15 && dampOne < 0.35 && dampMany > 0.99,
                    "cursor-attention-damped-not-snapped", log, ref failures);

                // ---------- v1.9：分辨率比例 + 视觉导演 ----------
                Check(Math.Abs(ResponsivePetSizing.Resolve(912, 190, ResponsivePetSizing.CompactScaleRatio) - 68.4) < 0.01,
                    "responsive-size-v110-compact-68dip", log, ref failures);
                Check(ResponsivePetSizing.Resolve(720, 190, ResponsivePetSizing.CompactScaleRatio) < 68.4
                    && ResponsivePetSizing.Resolve(1440, 190, ResponsivePetSizing.CompactScaleRatio) > 68.4,
                    "responsive-size-follows-workarea", log, ref failures);
                double physicalRatio100 = ResponsivePetSizing.Resolve(1040, 190, ResponsivePetSizing.CompactScaleRatio) / 1040;
                double physicalRatio225 = (ResponsivePetSizing.Resolve(2052 / 2.25, 190, ResponsivePetSizing.CompactScaleRatio) * 2.25) / 2052;
                Check(Math.Abs(physicalRatio100 - physicalRatio225) < 0.0015,
                    "mixed-dpi-100-to-225-keeps-relative-pet-size", log, ref failures);
                Check(BubbleSizing.Resolve(0) == BubbleSizing.MinimumHeight
                    && BubbleSizing.Resolve(510) == 548
                    && BubbleSizing.Resolve(900) == BubbleSizing.MaximumHeight,
                    "bubble-height-hugs-content-with-safe-bounds", log, ref failures);
                Check(Math.Abs(DockGeometry.SliverVisibleFor(68.4, 73.4) - 13.0) < 0.11
                    && DockGeometry.SliverVisibleFor(68.4, 73.4) < DockGeometry.SliverVisibleFor(114, 122),
                    "dock-sliver-scales-with-pet", log, ref failures);
                Check(DockGeometry.SpriteArtifactInsetRatio >= 0.08
                    && DockGeometry.SpriteArtifactInsetRatio <= 0.10
                    && DockGeometry.NeedsSpriteArtifactClip(DockEdge.Left)
                    && DockGeometry.NeedsSpriteArtifactClip(DockEdge.Right)
                    && !DockGeometry.NeedsSpriteArtifactClip(DockEdge.Top)
                    && !DockGeometry.NeedsSpriteArtifactClip(DockEdge.Bottom),
                    "dock-sprite-edge-artifact-safe-inset", log, ref failures);

                string schema8Root = Path.Combine(store.RootDirectory, "legacy-v8-size-190");
                Directory.CreateDirectory(schema8Root);
                File.WriteAllText(Path.Combine(schema8Root, "data.json"),
                    "{\"SchemaVersion\":8,\"PetId\":\"03-penguin\",\"PetSize\":190,\"PetScaleRatio\":0.60}", new UTF8Encoding(false));
                try
                {
                    Environment.SetEnvironmentVariable("WORKMATE_TEST_DIR", schema8Root);
                    DataStore migratedV8 = new DataStore();
                    Check(migratedV8.LoadedSchemaVersion == 8 && migratedV8.Data.SchemaVersion == 13
                        && migratedV8.Data.PetSize == 190
                        && Math.Abs(migratedV8.Data.PetScaleRatio - ResponsivePetSizing.CompactScaleRatio) < 0.001,
                        "schema-v8-current-small-migrates-minus-40pct", log, ref failures);
                }
                finally { Environment.SetEnvironmentVariable("WORKMATE_TEST_DIR", oldTestRoot); }

                // ---------- v1.24：照片项目 → 四姿态门禁 → 自定义宠物运行时 ----------
                PetCatalog.ConfigureCustomRoot(Path.Combine(store.RootDirectory, "CustomPets"));
                CustomPetService customPets = new CustomPetService(store.RootDirectory);
                string customReference = Path.Combine(store.RootDirectory, "custom-pet-reference.png");
                SavePng(PetAssets.Get("01-cat", "idle"), customReference);
                CustomPetResult customProject = customPets.CreateProject("团子", "测试猫", new[] { customReference });
                Check(customProject.Success && File.Exists(customProject.PromptPath) && File.Exists(customProject.WorkflowPath)
                    && File.ReadAllText(customProject.PromptPath, Encoding.UTF8).Contains("1024×1024 RGBA PNG")
                    && File.ReadAllText(customProject.WorkflowPath, Encoding.UTF8).Contains("generated/"),
                    "custom-pet-project-generates-local-prompt-and-workflow", log, ref failures);
                string customManifestPath = Path.Combine(customProject.ProjectDirectory, "manifest.json");
                string customManifestJson = File.ReadAllText(customManifestPath, Encoding.UTF8);
                File.WriteAllText(customManifestPath, customManifestJson.Replace(customProject.PetId, "01-cat"), new UTF8Encoding(false));
                CustomPetResult tamperedImport = customPets.ImportGeneratedAssets(customProject.ProjectDirectory);
                Check(!tamperedImport.Success && tamperedImport.Error.Contains("ID"),
                    "custom-pet-import-rejects-tampered-or-built-in-id", log, ref failures);
                File.WriteAllText(customManifestPath, customManifestJson, new UTF8Encoding(false));
                CustomPetResult prematureImport = customPets.ImportGeneratedAssets(customProject.ProjectDirectory);
                Check(!prematureImport.Success && prematureImport.Error.Contains("idle.png"),
                    "custom-pet-import-rejects-incomplete-four-pose-set", log, ref failures);
                string generatedRoot = Path.Combine(customProject.ProjectDirectory, "generated");
                foreach (string action in CustomPetService.RequiredActions)
                    SavePng(PetAssets.Get("01-cat", "idle"), Path.Combine(generatedRoot, action + ".png"));
                CustomPetResult customImported = customPets.ImportGeneratedAssets(customProject.ProjectDirectory);
                PetDefinition importedDefinition = PetCatalog.Find(customProject.PetId);
                Check(customImported.Success && importedDefinition != null && importedDefinition.IsCustom
                    && PetAssets.Get(customProject.PetId, "happy") != null
                    && !PetAssets.SupportsAnimation(customProject.PetId),
                    "custom-pet-four-pose-import-and-runtime-load", log, ref failures);
                CustomPetResult customReimported = customPets.ImportGeneratedAssets(customProject.ProjectDirectory);
                Check(customReimported.Success
                    && Directory.GetDirectories(customProject.ProjectDirectory, "assets.backup-*").Length == 1
                    && PetAssets.Get(customProject.PetId, "idle") != null,
                    "custom-pet-reimport-keeps-recoverable-asset-backup", log, ref failures);
                File.WriteAllText(Path.Combine(customProject.ProjectDirectory, "assets", "happy.png"), "broken", new UTF8Encoding(false));
                PetCatalog.ConfigureCustomRoot(Path.Combine(store.RootDirectory, "CustomPets"));
                Check(PetCatalog.Find(customProject.PetId) == null,
                    "custom-pet-startup-skips-corrupted-ready-assets", log, ref failures);

                VisualBehaviorDirector director = new VisualBehaviorDirector();
                Check(director.Observe(BehaviorState.Typing, 0.0) == BehaviorState.Idle
                    && director.Observe(BehaviorState.Typing, 0.79) == BehaviorState.Idle
                    && director.Observe(BehaviorState.Typing, 0.81) == BehaviorState.Typing,
                    "director-typing-stable-entry", log, ref failures);
                Check(director.Observe(BehaviorState.Idle, 1.0) == BehaviorState.Typing
                    && director.Observe(BehaviorState.Idle, 4.7) == BehaviorState.Typing
                    && director.Observe(BehaviorState.Idle, 5.02) == BehaviorState.Idle,
                    "director-typing-exit-grace", log, ref failures);
                director = new VisualBehaviorDirector();
                for (int cycle = 0; cycle < 6; cycle++)
                {
                    director.Observe(BehaviorState.Typing, cycle * 5.0);
                    director.Observe(BehaviorState.Typing, cycle * 5.0 + 1.0);
                    director.Observe(BehaviorState.Idle, cycle * 5.0 + 2.0);
                    director.Observe(BehaviorState.Idle, cycle * 5.0 + 4.9);
                }
                Check(director.SwitchCount <= 1, "director-normal-typing-pauses-do-not-flap", log, ref failures);

                store.AddStatSeconds("开发工具", 5);
                Dictionary<string, int> hourly;
                Check(store.Data.HourlyActiveSeconds.TryGetValue(DateTime.Today.ToString("yyyy-MM-dd"), out hourly)
                    && hourly.ContainsKey(DateTime.Now.Hour.ToString("00")), "hourly-heatmap-store", log, ref failures);

                MemoItem nudgeMemo = store.AddMemo("需要温和提醒的短期事项", "short", false, false);
                nudgeMemo.CreatedAt = DateTime.Now.AddHours(-5).ToString("o");
                store.Data.LastMemoNudgeAt = "";
                MemoNudgeService nudges = new MemoNudgeService();
                Check(nudges.TryPick(store, DateTime.Now, BehaviorState.Idle, false) != null,
                    "memo-nudge-eligible", log, ref failures);
                Check(nudges.TryPick(store, DateTime.Now, BehaviorState.Typing, false) == null,
                    "memo-nudge-suppressed-while-typing", log, ref failures);
                store.MarkMemoNudged(nudgeMemo, DateTime.Now);
                Check(nudges.TryPick(store, DateTime.Now.AddMinutes(1), BehaviorState.Idle, false) == null,
                    "memo-nudge-global-cooldown", log, ref failures);

                string carryFile = Path.Combine(store.RootDirectory, "carry-probe.txt");
                File.WriteAllText(carryFile, "probe", new UTF8Encoding(false));
                CarryService carry = new CarryService(store);
                string normalizedCarryFile = Path.GetFullPath(carryFile);
                Check(carry.AddFiles(new[] { carryFile }) == 1 && store.Data.CarryItems.Any(delegate(CarryItem item) { return item.Value == normalizedCarryFile; }),
                    "carry-file-add", log, ref failures);

                InterruptionAnalytics analytics = new InterruptionAnalytics();
                DateTime focusAt = new DateTime(2026, 7, 21, 9, 0, 0);
                analytics.Observe(store, BehaviorState.Typing, "开发工具", focusAt);
                analytics.Observe(store, BehaviorState.Typing, "开发工具", focusAt.AddSeconds(91));
                analytics.Observe(store, BehaviorState.Meeting, "沟通协作", focusAt.AddSeconds(92));
                analytics.Observe(store, BehaviorState.Meeting, "沟通协作", focusAt.AddSeconds(140));
                analytics.Observe(store, BehaviorState.Typing, "开发工具", focusAt.AddSeconds(141));
                InterruptionDay interruptionDay;
                Check(store.Data.Interruptions.TryGetValue(focusAt.AddSeconds(141).ToString("yyyy-MM-dd"), out interruptionDay)
                    && interruptionDay.Count == 1 && interruptionDay.MeetingCount == 1,
                    "interruption-qualified-return", log, ref failures);

                Check(EventBridge.ActivationMessageFor(new string[0]) == EventBridge.ActivateWorkbench,
                    "activation-default-workbench", log, ref failures);
                Check(EventBridge.ActivationMessageFor(new[] { "--quick" }) == EventBridge.ActivateQuickCapture,
                    "activation-quick-route", log, ref failures);

                string bridgeMessage = null;
                using (ManualResetEvent bridgeReceived = new ManualResetEvent(false))
                using (EventBridge bridge = new EventBridge(delegate(string value) { bridgeMessage = value; bridgeReceived.Set(); }))
                {
                    bridge.Start();
                    bool sent = EventBridge.TrySendWithRetry(EventBridge.ActivateWorkbench, 5, 60);
                    bool arrived = bridgeReceived.WaitOne(1600);
                    Check(sent && arrived && bridgeMessage == EventBridge.ActivateWorkbench,
                        "activation-pipe-roundtrip", log, ref failures);
                }
            }
            catch (Exception ex)
            {
                failures++;
                log.Add("FAIL unhandled: " + ex);
            }

            string root = store == null
                ? (Environment.GetEnvironmentVariable("WORKMATE_TEST_DIR") ?? Environment.CurrentDirectory)
                : store.RootDirectory;
            Directory.CreateDirectory(root);
            log.Add("RESULT failures=" + failures);
            File.WriteAllText(Path.Combine(root, "self-test.log"), string.Join(Environment.NewLine, log.ToArray()), new UTF8Encoding(false));
            return failures == 0 ? 0 : 1;
        }

        private static SignalSample NewSample()
        {
            SignalSample sample = new SignalSample();
            sample.ForegroundCategory = "其他";
            sample.Now = DateTime.Now;
            return sample;
        }

        private static BehaviorState FeedOnce(StateEngine engine, Func<SignalSample, SignalSample> mutator)
        {
            SignalSample sample = NewSample();
            sample = mutator(sample);
            return engine.Tick(sample).State;
        }

        private static BehaviorState Feed(StateEngine engine, Func<SignalSample, SignalSample> mutator, int ticks)
        {
            BehaviorState state = BehaviorState.Idle;
            for (int i = 0; i < ticks; i++) state = FeedOnce(engine, mutator);
            return state;
        }

        private static EngineOutput FeedOutput(StateEngine engine, Func<SignalSample, SignalSample> mutator, int ticks)
        {
            EngineOutput output = null;
            for (int i = 0; i < ticks; i++)
            {
                SignalSample sample = NewSample();
                sample = mutator(sample);
                output = engine.Tick(sample);
            }
            return output;
        }

        private static System.Collections.Generic.List<string> FeedEvents(StateEngine engine, Func<SignalSample, SignalSample> mutator, int ticks)
        {
            System.Collections.Generic.List<string> all = null;
            for (int i = 0; i < ticks; i++)
            {
                SignalSample sample = NewSample();
                sample = mutator(sample);
                EngineOutput output = engine.Tick(sample);
                if (output.Events != null)
                {
                    if (all == null) all = new System.Collections.Generic.List<string>();
                    all.AddRange(output.Events);
                }
            }
            return all;
        }

        private static void Check(bool condition, string name, List<string> log, ref int failures)
        {
            if (condition) log.Add("PASS " + name);
            else
            {
                failures++;
                log.Add("FAIL " + name);
            }
        }

        private static void SavePng(System.Windows.Media.Imaging.BitmapSource source, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            System.Windows.Media.Imaging.PngBitmapEncoder encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(source));
            using (FileStream stream = File.Create(path)) encoder.Save(stream);
        }

        private static NodOffStage Advance(NodOffDirector director, double seconds)
        {
            int steps = (int)Math.Ceiling(seconds / 0.05);
            for (int i = 0; i < steps; i++) director.Tick(0.05);
            return director.Stage;
        }
    }
}
