using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;

namespace WorkMatePro
{
    /// <summary>把随单 EXE 发布的能力工具按内容哈希解压，避免覆盖正在运行的旧版本。</summary>
    public sealed partial class EmbeddedToolManager
    {
        public const string ScrollCaptureResource = "WorkMate.Tools.AutoPageCapture.exe";
        public const string OcrScriptResource = "WorkMate.Tools.workmate-ocr.ps1";

        public string ToolRoot { get; private set; }

        public EmbeddedToolManager()
        {
            string testRoot = Environment.GetEnvironmentVariable("WORKMATE_TEST_DIR");
            ToolRoot = string.IsNullOrWhiteSpace(testRoot)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WorkMatePro", "Tools")
                : Path.Combine(testRoot, "Tools");
        }

        public bool ResourceExists(string resourceName)
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
                return stream != null;
        }

        public string ExtractScrollCapture()
        {
            return ExtractResource(ScrollCaptureResource, "AutoPageCapture.exe", ScrollCaptureSha256);
        }

        public string ExtractOcrScript()
        {
            return ExtractResource(OcrScriptResource, "workmate-ocr.ps1", OcrScriptSha256);
        }

        public Process LaunchScrollCapture()
        {
            string path = ExtractScrollCapture();
            ProcessStartInfo start = new ProcessStartInfo
            {
                FileName = path,
                WorkingDirectory = Path.GetDirectoryName(path),
                UseShellExecute = true
            };
            return Process.Start(start);
        }

        private string ExtractResource(string resourceName, string fileName, string expectedHash)
        {
            byte[] bytes;
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
            {
                if (stream == null) throw new InvalidOperationException("内嵌能力资源不存在：" + resourceName);
                using (MemoryStream buffer = new MemoryStream())
                {
                    stream.CopyTo(buffer);
                    bytes = buffer.ToArray();
                }
            }

            string hash = Sha256(bytes);
            if (!string.IsNullOrEmpty(expectedHash) && !string.Equals(hash, expectedHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("内嵌能力资源校验失败：" + fileName);

            Directory.CreateDirectory(ToolRoot);
            string stem = Path.GetFileNameWithoutExtension(fileName);
            string extension = Path.GetExtension(fileName);
            string target = Path.Combine(ToolRoot, stem + "-" + hash.Substring(0, 12).ToLowerInvariant() + extension);
            if (File.Exists(target) && string.Equals(Sha256File(target), hash, StringComparison.OrdinalIgnoreCase)) return target;

            string temp = target + ".tmp-" + Guid.NewGuid().ToString("N");
            File.WriteAllBytes(temp, bytes);
            try
            {
                if (!File.Exists(target)) File.Move(temp, target);
                else File.Delete(temp);
            }
            catch
            {
                try { if (File.Exists(temp)) File.Delete(temp); } catch { }
                if (!File.Exists(target) || !string.Equals(Sha256File(target), hash, StringComparison.OrdinalIgnoreCase)) throw;
            }
            return target;
        }

        private static string Sha256(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create()) return Hex(sha.ComputeHash(bytes));
        }

        private static string Sha256File(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            using (SHA256 sha = SHA256.Create()) return Hex(sha.ComputeHash(stream));
        }

        private static string Hex(byte[] bytes)
        {
            StringBuilder builder = new StringBuilder(bytes.Length * 2);
            foreach (byte value in bytes) builder.Append(value.ToString("X2"));
            return builder.ToString();
        }
    }

    public sealed class OcrResponse
    {
        public bool Success;
        public string Text;
        public string OutputPath;
        public string Error;
    }

    /// <summary>调用 Windows 自带 Windows.Media.Ocr；识别在本机完成，不上传图片。</summary>
    public sealed class WindowsOcrService
    {
        private readonly EmbeddedToolManager tools;
        private readonly string outputRoot;

        public WindowsOcrService(EmbeddedToolManager tools, string dataRoot)
        {
            this.tools = tools;
            outputRoot = Path.Combine(dataRoot, "OCR");
        }

        public void RecognizeAsync(string imagePath, Action<OcrResponse> callback)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                OcrResponse response = new OcrResponse();
                try
                {
                    if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath)) throw new FileNotFoundException("找不到要识别的图片。", imagePath);
                    Directory.CreateDirectory(outputRoot);
                    string output = Path.Combine(outputRoot, "OCR-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".txt");
                    string script = tools.ExtractOcrScript();
                    ProcessStartInfo start = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = "-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File " + Quote(script)
                            + " -Image " + Quote(Path.GetFullPath(imagePath)) + " -Output " + Quote(output) + " -Language " + Quote("zh-Hans-CN"),
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };
                    using (Process process = Process.Start(start))
                    {
                        if (!process.WaitForExit(60000))
                        {
                            try { process.Kill(); } catch { }
                            try { process.WaitForExit(5000); } catch { }
                            throw new TimeoutException("Windows OCR 超过 60 秒仍未完成，已安全终止。");
                        }
                        string standardOutput = process.StandardOutput.ReadToEnd();
                        string standardError = process.StandardError.ReadToEnd();
                        if (process.ExitCode != 0)
                            throw new InvalidOperationException(CleanError(standardError, standardOutput));
                    }
                    if (!File.Exists(output)) throw new InvalidOperationException("Windows OCR 没有生成结果文件。");
                    response.Text = NormalizeHanSpacing(File.ReadAllText(output, Encoding.UTF8)).Trim();
                    File.WriteAllText(output, response.Text, new UTF8Encoding(false));
                    response.OutputPath = output;
                    response.Success = true;
                }
                catch (Exception ex)
                {
                    response.Success = false;
                    response.Error = CleanError(ex.Message, null);
                }
                try { if (callback != null) callback(response); } catch { }
            });
        }

        public static string NormalizeHanSpacing(string value)
        {
            if (string.IsNullOrEmpty(value)) return value ?? "";
            string current = value;
            string previous;
            do
            {
                previous = current;
                current = Regex.Replace(current, "(?<=[\\u3400-\\u9FFF])\\s+(?=[\\u3400-\\u9FFF])", "");
            } while (current != previous);
            return current;
        }

        private static string Quote(string value)
        {
            return "\"" + (value ?? "").Replace("\"", "\\\"") + "\"";
        }

        private static string CleanError(string first, string second)
        {
            string value = !string.IsNullOrWhiteSpace(first) ? first : second;
            if (string.IsNullOrWhiteSpace(value)) value = "未知错误";
            value = value.Replace("\r", " ").Replace("\n", " ").Trim();
            return value.Length <= 240 ? value : value.Substring(0, 240) + "…";
        }
    }

    public sealed class WeatherSnapshot
    {
        public string Location;
        public string Condition;
        public double Temperature;
        public double FeelsLike;
        public double High;
        public double Low;
        public double WindSpeed;
        public int RainChance;
        public int NextRainHours;
        public int NextRainChance;
        public string NextRainTime;
        public string Source;
        public DateTime RetrievedAt;

        public WeatherSnapshot()
        {
            NextRainHours = -1;
            NextRainTime = "";
        }

        public string Summary
        {
            get
            {
                return Location + " · " + Condition + " " + Temperature.ToString("0.#") + "°C"
                    + "（体感 " + FeelsLike.ToString("0.#") + "°C）\n"
                    + "今日 " + Low.ToString("0.#") + "–" + High.ToString("0.#") + "°C"
                    + " · 降水概率 " + RainChance + "% · 风速 " + WindSpeed.ToString("0.#") + " km/h"
                    + (NextRainHours >= 0 ? "\n" + RainTimingLabel(NextRainHours) + "降雨概率 " + NextRainChance + "%" : "");
            }
        }

        public static string RainTimingLabel(int hours)
        {
            if (hours <= 0) return "很快可能有雨，";
            return "约 " + hours + " 小时后可能有雨，";
        }
    }

    public sealed class OutdoorSnapshot
    {
        public double EuropeanAqi;
        public double Pm25;
        public double UvIndex;
        public double Pollen;
        public string DominantPollen;
        public DateTime RetrievedAt;

        public OutdoorSnapshot()
        {
            EuropeanAqi = double.NaN;
            Pm25 = double.NaN;
            UvIndex = double.NaN;
            Pollen = double.NaN;
            DominantPollen = "";
        }

        public bool HasAirQuality { get { return !double.IsNaN(EuropeanAqi); } }
        public bool HasUv { get { return !double.IsNaN(UvIndex); } }
        public bool HasPollen { get { return !double.IsNaN(Pollen); } }

        public string Summary
        {
            get
            {
                List<string> parts = new List<string>();
                if (HasAirQuality) parts.Add("空气指数 " + EuropeanAqi.ToString("0") + "（" + AirQualityLabel(EuropeanAqi) + "）");
                if (!double.IsNaN(Pm25)) parts.Add("PM2.5 " + Pm25.ToString("0.#") + " μg/m³");
                if (HasUv) parts.Add("紫外线 " + UvIndex.ToString("0.#") + "（" + UvLabel(UvIndex) + "）");
                if (HasPollen) parts.Add("花粉 " + Pollen.ToString("0.#") + " grains/m³" + (DominantPollen.Length > 0 ? " · " + DominantPollen : ""));
                return parts.Count == 0 ? "当前地点暂无可用的户外健康数据。" : string.Join(" · ", parts.ToArray());
            }
        }

        public static string AirQualityLabel(double value)
        {
            if (value <= 20) return "良好";
            if (value <= 40) return "尚可";
            if (value <= 60) return "中等";
            if (value <= 80) return "较差";
            if (value <= 100) return "很差";
            return "极差";
        }

        public static string UvLabel(double value)
        {
            if (value < 3) return "低";
            if (value < 6) return "中等";
            if (value < 8) return "高";
            if (value < 11) return "很高";
            return "极高";
        }
    }

    public sealed class AmbientSnapshot
    {
        public WeatherSnapshot Weather;
        public OutdoorSnapshot Outdoor;
        public string OutdoorError;
        public DateTime RetrievedAt;
    }

    public sealed class AmbientResponse
    {
        public bool Success;
        public AmbientSnapshot Snapshot;
        public string Error;
        public bool FromCache;
        public bool Partial;
    }

    public sealed class WeatherResponse
    {
        public bool Success;
        public WeatherSnapshot Snapshot;
        public string Error;
        public bool FromCache;
    }

    /// <summary>Open-Meteo 天气客户端：无需密钥，15 分钟本地缓存，失败不会阻塞桌宠。</summary>
    public sealed class OpenMeteoWeatherService
    {
        private sealed class CacheEntry
        {
            public DateTime At;
            public AmbientSnapshot Snapshot;
            public bool Partial;
            public long RequestId;
        }

        public static readonly TimeSpan CompleteCacheLifetime = TimeSpan.FromMinutes(15);
        public static readonly TimeSpan PartialCacheLifetime = TimeSpan.FromMinutes(2);
        private readonly object sync = new object();
        private readonly Dictionary<string, CacheEntry> cache = new Dictionary<string, CacheEntry>(StringComparer.OrdinalIgnoreCase);
        private long requestSequence;

        public void GetAsync(string city, Action<WeatherResponse> callback)
        {
            GetAmbientAsync(city, delegate(AmbientResponse ambient)
            {
                WeatherResponse response = new WeatherResponse
                {
                    Success = ambient.Success && ambient.Snapshot != null && ambient.Snapshot.Weather != null,
                    Snapshot = ambient.Snapshot == null ? null : ambient.Snapshot.Weather,
                    Error = ambient.Error,
                    FromCache = ambient.FromCache
                };
                try { if (callback != null) callback(response); } catch { }
            });
        }

        public WeatherResponse Get(string city)
        {
            AmbientResponse ambient = GetAmbient(city);
            return new WeatherResponse
            {
                Success = ambient.Success && ambient.Snapshot != null && ambient.Snapshot.Weather != null,
                Snapshot = ambient.Snapshot == null ? null : ambient.Snapshot.Weather,
                Error = ambient.Error,
                FromCache = ambient.FromCache
            };
        }

        public void GetAmbientAsync(string city, Action<AmbientResponse> callback)
        {
            GetAmbientAsync(city, false, callback);
        }

        public void GetAmbientAsync(string city, bool forceRefresh, Action<AmbientResponse> callback)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                AmbientResponse response = GetAmbient(city, forceRefresh);
                try { if (callback != null) callback(response); } catch { }
            });
        }

        public AmbientResponse GetAmbient(string city)
        {
            return GetAmbient(city, false);
        }

        public AmbientResponse GetAmbient(string city, bool forceRefresh)
        {
            string normalized = NormalizeCity(city);
            if (normalized.Length == 0) return new AmbientResponse { Success = false, Error = "请先填写城市。" };

            if (!forceRefresh) lock (sync)
            {
                CacheEntry hit;
                if (cache.TryGetValue(normalized, out hit))
                {
                    if (DateTime.Now - hit.At < CacheLifetimeFor(hit.Partial)) return new AmbientResponse
                    {
                        Success = true,
                        Snapshot = hit.Snapshot,
                        FromCache = true,
                        Partial = hit.Partial
                    };
                    cache.Remove(normalized);
                }
            }

            long requestId = Interlocked.Increment(ref requestSequence);
            AmbientResponse response = new AmbientResponse();
            try
            {
                string geocodeUrl = "https://geocoding-api.open-meteo.com/v1/search?name=" + Uri.EscapeDataString(normalized)
                    + "&count=1&language=zh&format=json";
                string geocode = Download(geocodeUrl);
                Dictionary<string, object> root = new JavaScriptSerializer().DeserializeObject(geocode) as Dictionary<string, object>;
                IList results = root == null ? null : AsList(Value(root, "results"));
                if (results == null || results.Count == 0) throw new InvalidOperationException("没有找到城市“" + normalized + "”。");
                Dictionary<string, object> place = results[0] as Dictionary<string, object>;
                if (place == null) throw new InvalidDataException("地理编码返回格式异常。");
                double latitude = RequiredNumber(Value(place, "latitude"), "城市纬度");
                double longitude = RequiredNumber(Value(place, "longitude"), "城市经度");
                string forecastUrl = "https://api.open-meteo.com/v1/forecast?latitude=" + latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + "&longitude=" + longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + "&current=temperature_2m,apparent_temperature,weather_code,wind_speed_10m"
                    + "&hourly=temperature_2m,precipitation_probability,weather_code&forecast_hours=6"
                    + "&daily=temperature_2m_max,temperature_2m_min,precipitation_probability_max&forecast_days=1&timezone=auto";
                string forecast = Download(forecastUrl);
                WeatherSnapshot weather = ParseWeather(place, forecast);
                OutdoorSnapshot outdoor = null;
                string outdoorError = "";
                try
                {
                    string airUrl = "https://air-quality-api.open-meteo.com/v1/air-quality?latitude=" + latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        + "&longitude=" + longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        + "&current=european_aqi,pm2_5,uv_index,alder_pollen,birch_pollen,grass_pollen,mugwort_pollen,ragweed_pollen&timezone=auto";
                    outdoor = ParseOutdoor(Download(airUrl));
                }
                catch (Exception ex)
                {
                    outdoorError = CleanError(ex.Message);
                }
                response.Snapshot = new AmbientSnapshot
                {
                    Weather = weather,
                    Outdoor = outdoor,
                    OutdoorError = outdoorError,
                    RetrievedAt = DateTime.Now
                };
                response.Success = true;
                response.Partial = outdoor == null;
                lock (sync)
                {
                    CacheEntry existing;
                    if (!cache.TryGetValue(normalized, out existing) || ShouldAcceptCacheWrite(existing.RequestId, requestId))
                        cache[normalized] = new CacheEntry { At = DateTime.Now, Snapshot = response.Snapshot, Partial = response.Partial, RequestId = requestId };
                }
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Error = CleanError(ex.Message);
            }
            return response;
        }

        public static TimeSpan CacheLifetimeFor(bool partial)
        {
            return partial ? PartialCacheLifetime : CompleteCacheLifetime;
        }

        public static bool ShouldAcceptCacheWrite(long existingRequestId, long candidateRequestId)
        {
            return candidateRequestId >= existingRequestId;
        }

        public static WeatherSnapshot ParseForTest(string placeJson, string forecastJson)
        {
            JavaScriptSerializer json = new JavaScriptSerializer();
            Dictionary<string, object> place = json.DeserializeObject(placeJson) as Dictionary<string, object>;
            return ParseWeather(place, forecastJson);
        }

        public static AmbientSnapshot ParseAmbientForTest(string placeJson, string forecastJson, string airJson)
        {
            JavaScriptSerializer json = new JavaScriptSerializer();
            Dictionary<string, object> place = json.DeserializeObject(placeJson) as Dictionary<string, object>;
            return new AmbientSnapshot
            {
                Weather = ParseWeather(place, forecastJson),
                Outdoor = ParseOutdoor(airJson),
                RetrievedAt = DateTime.Now
            };
        }

        private static WeatherSnapshot ParseWeather(Dictionary<string, object> place, string forecastJson)
        {
            JavaScriptSerializer json = new JavaScriptSerializer();
            Dictionary<string, object> root = json.DeserializeObject(forecastJson) as Dictionary<string, object>;
            Dictionary<string, object> current = root == null ? null : Value(root, "current") as Dictionary<string, object>;
            Dictionary<string, object> daily = root == null ? null : Value(root, "daily") as Dictionary<string, object>;
            Dictionary<string, object> hourly = root == null ? null : Value(root, "hourly") as Dictionary<string, object>;
            if (place == null || current == null || daily == null) throw new InvalidDataException("天气返回格式异常。");

            WeatherSnapshot snapshot = new WeatherSnapshot();
            snapshot.Location = JoinLocation(Text(Value(place, "name")), Text(Value(place, "admin1")), Text(Value(place, "country")));
            snapshot.Temperature = RequiredNumber(Value(current, "temperature_2m"), "当前温度");
            snapshot.FeelsLike = RequiredNumber(Value(current, "apparent_temperature"), "体感温度");
            snapshot.WindSpeed = RequiredNumber(Value(current, "wind_speed_10m"), "风速");
            snapshot.Condition = WeatherCode((int)RequiredNumber(Value(current, "weather_code"), "天气代码"));
            snapshot.High = RequiredFirstNumber(Value(daily, "temperature_2m_max"), "最高温度");
            snapshot.Low = RequiredFirstNumber(Value(daily, "temperature_2m_min"), "最低温度");
            snapshot.RainChance = (int)Math.Round(RequiredFirstNumber(Value(daily, "precipitation_probability_max"), "降水概率"));
            if (hourly != null)
            {
                IList times = AsList(Value(hourly, "time"));
                IList probabilities = AsList(Value(hourly, "precipitation_probability"));
                IList codes = AsList(Value(hourly, "weather_code"));
                int count = Math.Min(times == null ? 0 : times.Count,
                    Math.Min(probabilities == null ? 0 : probabilities.Count, codes == null ? 0 : codes.Count));
                for (int index = 0; index < count; index++)
                {
                    int probability = (int)Math.Round(Number(probabilities[index]));
                    int code = (int)Math.Round(Number(codes[index]));
                    if (probability < AmbientInsightPolicy.RainProbabilityThreshold && !IsRainCode(code)) continue;
                    snapshot.NextRainHours = index;
                    snapshot.NextRainChance = Math.Max(probability, IsRainCode(code) ? AmbientInsightPolicy.RainProbabilityThreshold : 0);
                    snapshot.NextRainTime = Text(times[index]);
                    break;
                }
            }
            snapshot.Source = "Open-Meteo";
            snapshot.RetrievedAt = DateTime.Now;
            return snapshot;
        }

        private static OutdoorSnapshot ParseOutdoor(string airJson)
        {
            JavaScriptSerializer json = new JavaScriptSerializer();
            Dictionary<string, object> root = json.DeserializeObject(airJson) as Dictionary<string, object>;
            Dictionary<string, object> current = root == null ? null : Value(root, "current") as Dictionary<string, object>;
            if (current == null) throw new InvalidDataException("户外健康数据返回格式异常。");

            OutdoorSnapshot snapshot = new OutdoorSnapshot();
            snapshot.EuropeanAqi = NumberOrNaN(Value(current, "european_aqi"));
            snapshot.Pm25 = NumberOrNaN(Value(current, "pm2_5"));
            snapshot.UvIndex = NumberOrNaN(Value(current, "uv_index"));
            string[] keys = { "alder_pollen", "birch_pollen", "grass_pollen", "mugwort_pollen", "ragweed_pollen" };
            string[] names = { "桤木", "桦木", "禾草", "艾蒿", "豚草" };
            double pollen = double.NaN;
            string dominant = "";
            for (int index = 0; index < keys.Length; index++)
            {
                double value = NumberOrNaN(Value(current, keys[index]));
                if (double.IsNaN(value) || (!double.IsNaN(pollen) && value <= pollen)) continue;
                pollen = value;
                dominant = names[index];
            }
            snapshot.Pollen = pollen;
            snapshot.DominantPollen = dominant;
            snapshot.RetrievedAt = DateTime.Now;
            return snapshot;
        }

        private static string Download(string url)
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            using (TimeoutWebClient client = new TimeoutWebClient())
            {
                client.Encoding = Encoding.UTF8;
                client.Headers[HttpRequestHeader.UserAgent] = "WorkMatePro/1.24 (Windows desktop companion)";
                client.Headers[HttpRequestHeader.Accept] = "application/json";
                return client.DownloadString(url);
            }
        }

        private static string NormalizeCity(string city)
        {
            string value = (city ?? "").Trim();
            if (value.Length > 50) value = value.Substring(0, 50);
            return value;
        }

        private static object Value(Dictionary<string, object> dictionary, string key)
        {
            object value;
            return dictionary != null && dictionary.TryGetValue(key, out value) ? value : null;
        }

        private static IList AsList(object value)
        {
            return value as IList;
        }

        private static double FirstNumber(object value)
        {
            IList list = AsList(value);
            if (list == null || list.Count == 0) return 0;
            return Number(list[0]);
        }

        private static double RequiredFirstNumber(object value, string field)
        {
            IList list = AsList(value);
            if (list == null || list.Count == 0) throw new InvalidDataException(field + "缺失。");
            return RequiredNumber(list[0], field);
        }

        private static double Number(object value)
        {
            if (value == null) return 0;
            return Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture);
        }

        private static double RequiredNumber(object value, string field)
        {
            if (value == null) throw new InvalidDataException(field + "缺失。");
            try { return Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture); }
            catch { throw new InvalidDataException(field + "格式异常。"); }
        }

        private static double NumberOrNaN(object value)
        {
            if (value == null) return double.NaN;
            try { return Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture); }
            catch { return double.NaN; }
        }

        private static string Text(object value)
        {
            return value == null ? "" : Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
        }

        private static string JoinLocation(params string[] parts)
        {
            List<string> unique = new List<string>();
            foreach (string part in parts)
            {
                string value = (part ?? "").Trim();
                if (value.Length > 0 && !unique.Contains(value)) unique.Add(value);
            }
            return unique.Count == 0 ? "所选城市" : string.Join(" · ", unique.ToArray());
        }

        public static string WeatherCode(int code)
        {
            if (code == 0) return "晴";
            if (code == 1) return "大致晴朗";
            if (code == 2) return "局部多云";
            if (code == 3) return "阴";
            if (code == 45 || code == 48) return "雾";
            if (code >= 51 && code <= 57) return "毛毛雨";
            if (code >= 61 && code <= 67) return "雨";
            if (code >= 71 && code <= 77) return "雪";
            if (code >= 80 && code <= 82) return "阵雨";
            if (code >= 85 && code <= 86) return "阵雪";
            if (code >= 95) return "雷雨";
            return "天气代码 " + code;
        }

        private static bool IsRainCode(int code)
        {
            return (code >= 51 && code <= 67) || (code >= 80 && code <= 82) || code >= 95;
        }

        private static string CleanError(string message)
        {
            string value = string.IsNullOrWhiteSpace(message) ? "未知错误" : message.Replace("\r", " ").Replace("\n", " ").Trim();
            return value.Length <= 180 ? value : value.Substring(0, 180) + "…";
        }
    }

    public sealed class CompanionNotice
    {
        public string Category;
        public string Key;
        public string Cue;
        public string Message;
        public int Priority;
        public DateTime ExpiresAt;

        public bool IsExpired(DateTime now)
        {
            return ExpiresAt != DateTime.MinValue && now >= ExpiresAt;
        }
    }

    /// <summary>每类只保留最新一条，并在真正展示前丢弃过期内容。</summary>
    public sealed class ProactiveNoticeQueue
    {
        private readonly List<CompanionNotice> items = new List<CompanionNotice>();

        public int Count { get { return items.Count; } }

        public void EnqueueLatest(CompanionNotice notice)
        {
            if (notice == null || string.IsNullOrWhiteSpace(notice.Category)) return;
            items.RemoveAll(delegate(CompanionNotice queued)
            {
                return string.Equals(queued.Category, notice.Category, StringComparison.OrdinalIgnoreCase);
            });
            items.Add(notice);
        }

        public CompanionNotice DequeueReady(DateTime now)
        {
            items.RemoveAll(delegate(CompanionNotice notice) { return notice == null || notice.IsExpired(now); });
            if (items.Count == 0) return null;
            items.Sort(delegate(CompanionNotice left, CompanionNotice right) { return right.Priority.CompareTo(left.Priority); });
            CompanionNotice next = items[0];
            items.RemoveAt(0);
            return next;
        }

        public void Purge(string category)
        {
            items.RemoveAll(delegate(CompanionNotice notice)
            {
                return notice == null || string.Equals(notice.Category, category, StringComparison.OrdinalIgnoreCase);
            });
        }
    }

    public static class AmbientInsightPolicy
    {
        public const int RainProbabilityThreshold = 60;
        public const double AirQualityAlertThreshold = 60;
        public const double UvAlertThreshold = 8;
        public const double PollenAlertThreshold = 50;

        public static List<CompanionNotice> Evaluate(AmbientSnapshot snapshot, bool weatherEnabled, bool outdoorEnabled, DateTime now)
        {
            List<CompanionNotice> notices = new List<CompanionNotice>();
            if (snapshot == null) return notices;
            string day = now.ToString("yyyyMMdd");
            if (weatherEnabled && snapshot.Weather != null && snapshot.Weather.NextRainHours >= 0 && snapshot.Weather.NextRainHours <= 2)
            {
                WeatherSnapshot weather = snapshot.Weather;
                notices.Add(new CompanionNotice
                {
                    Category = "weather",
                    Key = "rain|" + day + "|" + weather.Location + "|" + weather.NextRainTime,
                    Cue = "☂",
                    Priority = 100,
                    ExpiresAt = now.AddHours(Math.Max(1, weather.NextRainHours + 1)),
                    Message = WeatherSnapshot.RainTimingLabel(weather.NextRainHours) + "概率 " + weather.NextRainChance + "%——我把伞提醒叼来啦。"
                });
            }
            if (outdoorEnabled && snapshot.Outdoor != null)
            {
                OutdoorSnapshot outdoor = snapshot.Outdoor;
                CompanionNotice health = null;
                if (outdoor.HasAirQuality && outdoor.EuropeanAqi >= AirQualityAlertThreshold)
                {
                    health = new CompanionNotice
                    {
                        Category = "outdoor",
                        Key = "outdoor|aqi|" + day,
                        Cue = "◉",
                        Priority = 90,
                        ExpiresAt = now.AddHours(4),
                        Message = "今天空气指数 " + outdoor.EuropeanAqi.ToString("0") + "（" + OutdoorSnapshot.AirQualityLabel(outdoor.EuropeanAqi) + "），出门久待记得防护。"
                    };
                }
                else if (outdoor.HasUv && outdoor.UvIndex >= UvAlertThreshold)
                {
                    health = new CompanionNotice
                    {
                        Category = "outdoor",
                        Key = "outdoor|uv|" + day,
                        Cue = "☀",
                        Priority = 80,
                        ExpiresAt = now.AddHours(4),
                        Message = "紫外线已到 " + outdoor.UvIndex.ToString("0.#") + "（" + OutdoorSnapshot.UvLabel(outdoor.UvIndex) + "），我替你守着防晒提醒。"
                    };
                }
                else if (outdoor.HasPollen && outdoor.Pollen >= PollenAlertThreshold)
                {
                    health = new CompanionNotice
                    {
                        Category = "outdoor",
                        Key = "outdoor|pollen|" + day,
                        Cue = "✿",
                        Priority = 70,
                        ExpiresAt = now.AddHours(4),
                        Message = "花粉浓度偏高" + (outdoor.DominantPollen.Length > 0 ? "（以" + outdoor.DominantPollen + "为主）" : "") + "，敏感的话出门多留意。"
                    };
                }
                if (health != null) notices.Add(health);
            }
            notices.Sort(delegate(CompanionNotice left, CompanionNotice right) { return right.Priority.CompareTo(left.Priority); });
            return notices;
        }
    }

    public static class DailyBriefingBuilder
    {
        public static string Build(AmbientSnapshot ambient, MeetingInfo meeting, int openMemoCount, string anchorText, DateTime now)
        {
            List<string> lines = new List<string>();
            lines.Add(now.ToString("M月d日") + "，我把今天的信息叼来啦：");
            if (ambient != null && ambient.Weather != null)
            {
                WeatherSnapshot weather = ambient.Weather;
                string weatherLine = weather.Location + " " + weather.Condition + " " + weather.Temperature.ToString("0.#") + "°C，今日 "
                    + weather.Low.ToString("0.#") + "–" + weather.High.ToString("0.#") + "°C";
                if (weather.NextRainHours >= 0 && weather.NextRainHours <= 2)
                    weatherLine += "；" + WeatherSnapshot.RainTimingLabel(weather.NextRainHours) + weather.NextRainChance + "%";
                lines.Add(weatherLine);
            }
            else lines.Add("天气暂不可用；本地会议与待办仍可照常整理");
            if (ambient != null && ambient.Outdoor != null) lines.Add(ambient.Outdoor.Summary);
            else lines.Add("户外健康数据暂不可用");
            if (meeting != null && meeting.Start >= now)
                lines.Add("下一场会议 " + meeting.Start.ToString("HH:mm") + " · " + Formatters.Truncate(meeting.Subject, 28));
            else lines.Add("近期没有读取到待开始的 Outlook 会议");
            string taskLine = "未完成记录 " + openMemoCount + " 条";
            if (!string.IsNullOrWhiteSpace(anchorText)) taskLine += "；当前锚点：" + Formatters.Truncate(anchorText, 30);
            lines.Add(taskLine);
            return string.Join(Environment.NewLine, lines.ToArray());
        }

        public static string ToastText(string briefing)
        {
            string compact = (briefing ?? "").Replace("\r", " ").Replace("\n", "；");
            return Formatters.Truncate(compact, 82);
        }
    }

    public static class AmbientFreshnessPolicy
    {
        public static readonly TimeSpan BriefingMaximumAge = TimeSpan.FromMinutes(90);
        public static readonly TimeSpan PresenceMaximumAge = TimeSpan.FromMinutes(90);

        public static AmbientSnapshot ForBriefing(AmbientSnapshot snapshot, DateTime now)
        {
            if (snapshot == null) return null;
            TimeSpan age = now - snapshot.RetrievedAt;
            if (age < TimeSpan.FromMinutes(-5) || age > BriefingMaximumAge) return null;
            return snapshot;
        }

        public static AmbientSnapshot ForPresence(AmbientSnapshot snapshot, DateTime now)
        {
            if (snapshot == null) return null;
            TimeSpan age = now - snapshot.RetrievedAt;
            if (age < TimeSpan.FromMinutes(-5) || age > PresenceMaximumAge) return null;
            return snapshot;
        }
    }

    public sealed class AmbientPresenceStyle
    {
        public string Key;
        public string Label;
        public string Tooltip;
        public string Background;
        public string Foreground;
        public int Priority;
    }

    /// <summary>把联网数据压缩成宠物旁一个稳定、低打扰的环境信号，不额外弹窗。</summary>
    public static class AmbientPresencePolicy
    {
        public static AmbientPresenceStyle Resolve(AmbientSnapshot snapshot, bool enabled)
        {
            if (!enabled || snapshot == null) return null;
            if (snapshot.Weather != null && snapshot.Weather.NextRainHours >= 0 && snapshot.Weather.NextRainHours <= 2)
            {
                return new AmbientPresenceStyle
                {
                    Key = "rain|" + snapshot.Weather.NextRainTime,
                    Label = "雨 " + snapshot.Weather.NextRainChance + "%",
                    Tooltip = WeatherSnapshot.RainTimingLabel(snapshot.Weather.NextRainHours) + "降雨概率 " + snapshot.Weather.NextRainChance + "%",
                    Background = "#E4F2FF", Foreground = "#286A9B", Priority = 100
                };
            }
            if (snapshot.Outdoor != null && snapshot.Outdoor.HasAirQuality
                && snapshot.Outdoor.EuropeanAqi >= AmbientInsightPolicy.AirQualityAlertThreshold)
            {
                return new AmbientPresenceStyle
                {
                    Key = "aqi|" + snapshot.Outdoor.EuropeanAqi.ToString("0"),
                    Label = "AQI " + snapshot.Outdoor.EuropeanAqi.ToString("0"),
                    Tooltip = "空气指数 " + snapshot.Outdoor.EuropeanAqi.ToString("0") + " · " + OutdoorSnapshot.AirQualityLabel(snapshot.Outdoor.EuropeanAqi),
                    Background = "#FFF0D9", Foreground = "#9A5B19", Priority = 90
                };
            }
            if (snapshot.Outdoor != null && snapshot.Outdoor.HasUv
                && snapshot.Outdoor.UvIndex >= AmbientInsightPolicy.UvAlertThreshold)
            {
                return new AmbientPresenceStyle
                {
                    Key = "uv|" + snapshot.Outdoor.UvIndex.ToString("0.#"),
                    Label = "UV " + snapshot.Outdoor.UvIndex.ToString("0.#"),
                    Tooltip = "紫外线 " + snapshot.Outdoor.UvIndex.ToString("0.#") + " · " + OutdoorSnapshot.UvLabel(snapshot.Outdoor.UvIndex),
                    Background = "#FFE8DE", Foreground = "#A74E32", Priority = 80
                };
            }
            if (snapshot.Weather == null) return null;
            return new AmbientPresenceStyle
            {
                Key = "weather|" + snapshot.Weather.Condition + "|" + snapshot.Weather.Temperature.ToString("0"),
                Label = snapshot.Weather.Temperature.ToString("0") + "°",
                Tooltip = snapshot.Weather.Location + " · " + snapshot.Weather.Condition + " · " + snapshot.Weather.Temperature.ToString("0.#") + "°C",
                Background = "#F2ECE8", Foreground = "#675952", Priority = 10
            };
        }
    }

    public static class CompanionEnergyPolicy
    {
        public static string Normalize(string mode)
        {
            if (string.Equals(mode, "low", StringComparison.OrdinalIgnoreCase)) return "low";
            if (string.Equals(mode, "high", StringComparison.OrdinalIgnoreCase)) return "high";
            return "steady";
        }

        public static string Label(string mode)
        {
            mode = Normalize(mode);
            if (mode == "low") return "低能量";
            if (mode == "high") return "精力足";
            return "稳稳来";
        }

        public static bool AllowsAutonomousMotion(string mode) { return Normalize(mode) != "low"; }
        public static double MotionDelayMultiplier(string mode) { return Normalize(mode) == "high" ? 0.72 : 1.0; }
        public static int MinimumProactivePriority(string mode) { return Normalize(mode) == "low" ? 90 : 0; }
    }

    public static class DailyPriorityPromptPolicy
    {
        public static bool CanPrompt(DateTime now, string lastPromptDate, bool hasAnchor, int openMemoCount,
            BehaviorState state, bool flowActive, bool quiet, bool focus, bool retreat, bool demo, bool docked)
        {
            if (hasAnchor || openMemoCount <= 0 || lastPromptDate == now.ToString("yyyy-MM-dd")) return false;
            if (now.Hour < 8 || now.Hour > 14) return false;
            return ProactivePresentationPolicy.CanPresent(state, flowActive, quiet, focus, retreat, demo, docked);
        }
    }

    internal sealed class TimeoutWebClient : WebClient
    {
        protected override WebRequest GetWebRequest(Uri address)
        {
            WebRequest request = base.GetWebRequest(address);
            if (request != null) request.Timeout = 12000;
            return request;
        }
    }

    public static class WorkBreakPresentationPolicy
    {
        public static bool CanPresent(BehaviorState state, bool flowActive, bool quiet, bool focus, bool retreat, bool demo)
        {
            return state != BehaviorState.Meeting && !flowActive && !quiet && !focus && !retreat && !demo;
        }
    }

    public static class ProactivePresentationPolicy
    {
        public static bool CanPresent(BehaviorState state, bool flowActive, bool quiet, bool focus, bool retreat, bool demo, bool docked)
        {
            return state != BehaviorState.Meeting && state != BehaviorState.Typing && state != BehaviorState.Watching
                && state != BehaviorState.Away && !flowActive && !quiet && !focus && !retreat && !demo && !docked;
        }
    }
}
