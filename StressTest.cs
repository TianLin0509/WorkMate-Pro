using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace WorkMatePro
{
    /// <summary>外发前的高频纯逻辑/持久化压力门禁；必须在 WORKMATE_TEST_DIR 隔离目录运行。</summary>
    public static class StressTest
    {
        public static int Run()
        {
            List<string> log = new List<string>();
            Stopwatch total = Stopwatch.StartNew();
            int failures = 0;
            try
            {
                string root = Environment.GetEnvironmentVariable("WORKMATE_TEST_DIR");
                if (string.IsNullOrWhiteSpace(root)) throw new InvalidOperationException("WORKMATE_TEST_DIR is required.");
                Directory.CreateDirectory(root);

                Stopwatch timer = Stopwatch.StartNew();
                StateEngine engine = new StateEngine();
                Random random = new Random(124042);
                DateTime clock = new DateTime(2026, 8, 9, 8, 0, 0);
                for (int i = 0; i < 250000; i++)
                {
                    SignalSample sample = new SignalSample
                    {
                        KeyPerMin = random.NextDouble() * 900,
                        WheelPerMin = random.NextDouble() * 1200,
                        ClickPerMin = random.NextDouble() * 500,
                        MovePerMin = random.NextDouble() * 5000,
                        IdleSeconds = random.Next(0, 2400),
                        IdleIntentionalSeconds = random.Next(0, 2400),
                        MicInUse = random.Next(0, 30) == 0,
                        CameraInUse = random.Next(0, 60) == 0,
                        AudioActive = random.Next(0, 4) == 0,
                        AudioPeak = (float)random.NextDouble(),
                        ForegroundCategory = random.Next(0, 3) == 0 ? "开发工具" : (random.Next(0, 2) == 0 ? "沟通协作" : "浏览器"),
                        Now = clock.AddMilliseconds(i * 500L)
                    };
                    EngineOutput output = engine.Tick(sample);
                    if (output == null || !Enum.IsDefined(typeof(BehaviorState), output.State))
                        throw new InvalidDataException("State engine emitted invalid output at iteration " + i);
                }
                timer.Stop();
                log.Add("PASS state-engine-fuzz iterations=250000 ms=" + timer.ElapsedMilliseconds);

                timer.Restart();
                ProactiveNoticeQueue queue = new ProactiveNoticeQueue();
                DateTime noticeNow = new DateTime(2026, 8, 9, 10, 0, 0);
                for (int i = 0; i < 100000; i++)
                {
                    string category = i % 2 == 0 ? "weather" : "outdoor";
                    queue.EnqueueLatest(new CompanionNotice
                    {
                        Category = category,
                        Key = category + "-" + i,
                        Priority = category == "weather" ? 100 : 80,
                        ExpiresAt = noticeNow.AddMinutes(i % 7 == 0 ? -1 : 30)
                    });
                    if (queue.Count > 2) throw new InvalidDataException("Proactive queue grew beyond category bound.");
                }
                CompanionNotice queued;
                int drained = 0;
                while ((queued = queue.DequeueReady(noticeNow)) != null) drained++;
                if (drained > 2 || queue.Count != 0) throw new InvalidDataException("Proactive queue drain invariant failed.");
                timer.Stop();
                log.Add("PASS proactive-queue-storm enqueue=100000 drained=" + drained + " ms=" + timer.ElapsedMilliseconds);

                timer.Restart();
                DataStore store = new DataStore();
                for (int i = 0; i < 500; i++)
                {
                    if (i % 5 == 0) store.Data.Memos.Add(new MemoItem { Text = "压力记录 " + i, Term = i % 10 == 0 ? "long" : "short" });
                    store.Data.CompanionValue = i;
                    store.Data.WeatherCity = "压力城市" + (i % 17);
                    store.Save();
                    if (i % 25 == 0)
                    {
                        DataStore reloaded = new DataStore();
                        if (reloaded.Data.CompanionValue != i || reloaded.Data.SchemaVersion != 13)
                            throw new InvalidDataException("Atomic persistence reload mismatch at iteration " + i);
                    }
                }
                DataStore finalReload = new DataStore();
                if (finalReload.Data.CompanionValue != 499 || finalReload.Data.Memos.Count != 100
                    || File.Exists(finalReload.DataPath + ".tmp"))
                    throw new InvalidDataException("Final persistence invariant failed.");
                timer.Stop();
                log.Add("PASS atomic-json-persistence saves=500 reloads=21 bytes=" + new FileInfo(finalReload.DataPath).Length + " ms=" + timer.ElapsedMilliseconds);

                timer.Restart();
                for (int i = 0; i < 20000; i++)
                {
                    string place = "{\"name\":\"上海\",\"admin1\":\"上海\",\"country\":\"中国\"}";
                    string forecast = "{\"current\":{\"temperature_2m\":28.5,\"apparent_temperature\":30.1,\"weather_code\":2,\"wind_speed_10m\":12.3},\"daily\":{\"temperature_2m_max\":[32.0],\"temperature_2m_min\":[24.0],\"precipitation_probability_max\":[75]},\"hourly\":{\"time\":[\"2026-08-09T10:00\",\"2026-08-09T11:00\"],\"precipitation_probability\":[10,75],\"weather_code\":[2,61]}}";
                    WeatherSnapshot weather = OpenMeteoWeatherService.ParseForTest(place, forecast);
                    if (weather.NextRainHours != 1 || weather.RainChance != 75) throw new InvalidDataException("Weather parser drift.");
                }
                timer.Stop();
                log.Add("PASS weather-parser-stress iterations=20000 ms=" + timer.ElapsedMilliseconds);

                timer.Restart();
                for (int i = 0; i < 30000; i++)
                {
                    string petId = i % 2 == 0 ? "01-cat" : "03-penguin";
                    string action = CustomPetService.RequiredActions[i % CustomPetService.RequiredActions.Length];
                    if (PetAssets.Get(petId, action) == null) throw new InvalidDataException("Sprite cache returned null.");
                }
                timer.Stop();
                log.Add("PASS sprite-cache-stress reads=30000 ms=" + timer.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                failures++;
                log.Add("FAIL unhandled: " + ex);
            }
            total.Stop();
            string outputRoot = Environment.GetEnvironmentVariable("WORKMATE_TEST_DIR") ?? Environment.CurrentDirectory;
            Directory.CreateDirectory(outputRoot);
            log.Add("METRIC workingSetBytes=" + Process.GetCurrentProcess().WorkingSet64);
            log.Add("RESULT failures=" + failures + " elapsedMs=" + total.ElapsedMilliseconds);
            File.WriteAllText(Path.Combine(outputRoot, "stress-test.log"), string.Join(Environment.NewLine, log.ToArray()), new UTF8Encoding(false));
            return failures == 0 ? 0 : 1;
        }
    }
}
