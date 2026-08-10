using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WorkMatePro
{
    public sealed class CustomPetManifest
    {
        public int SchemaVersion { get; set; }
        public string Id { get; set; }
        public string Name { get; set; }
        public string Species { get; set; }
        public string Accent { get; set; }
        public string Tagline { get; set; }
        public string Status { get; set; }
        public string CreatedAt { get; set; }
        public string UpdatedAt { get; set; }
        public List<string> ReferenceFiles { get; set; }

        public CustomPetManifest()
        {
            SchemaVersion = 1;
            Id = "";
            Name = "";
            Species = "自定义伙伴";
            Accent = "#7B6BE6";
            Tagline = "由你的宠物照片生成";
            Status = "draft";
            CreatedAt = DateTime.Now.ToString("o");
            UpdatedAt = CreatedAt;
            ReferenceFiles = new List<string>();
        }
    }

    public sealed class CustomPetResult
    {
        public bool Success;
        public string Error;
        public string PetId;
        public string ProjectDirectory;
        public string PromptPath;
        public string WorkflowPath;
    }

    public sealed class CustomPetValidationResult
    {
        public bool Success;
        public string Error;
        public int Width;
        public int Height;
        public int CheckedAssets;
    }

    /// <summary>
    /// 照片只复制到用户本机项目目录。生成模型由用户自行选择；WorkMate 负责提示词、文件契约、
    /// 透明图质量门禁与运行时导入，避免把第三方密钥或照片上传责任偷偷塞进桌宠进程。
    /// </summary>
    public sealed class CustomPetService
    {
        public static readonly string[] RequiredActions = { "idle", "typing", "happy", "sleep" };
        private readonly JavaScriptSerializer serializer = new JavaScriptSerializer();
        private readonly string customRoot;

        public string CustomRoot { get { return customRoot; } }

        public CustomPetService(string dataRoot)
        {
            customRoot = Path.Combine(dataRoot, "CustomPets");
            Directory.CreateDirectory(customRoot);
            serializer.MaxJsonLength = 4 * 1024 * 1024;
        }

        public CustomPetResult CreateProject(string name, string species, IList<string> photoPaths)
        {
            CustomPetResult result = new CustomPetResult();
            string cleanName = CleanText(name, 24);
            string cleanSpecies = CleanText(species, 24);
            if (cleanName.Length == 0) { result.Error = "请先给自定义伙伴起个名字。"; return result; }
            if (cleanSpecies.Length == 0) cleanSpecies = "自定义宠物";
            List<string> photos = photoPaths == null ? new List<string>() : photoPaths.Where(IsSupportedPhoto).Distinct(StringComparer.OrdinalIgnoreCase).Take(3).ToList();
            if (photos.Count == 0) { result.Error = "请选择 1–3 张清晰的宠物照片。"; return result; }

            string id = "custom-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6);
            string project = Path.Combine(customRoot, id);
            string staging = project + ".creating-" + Guid.NewGuid().ToString("N").Substring(0, 6);
            try
            {
                Directory.CreateDirectory(staging);
                string references = Path.Combine(staging, "references");
                string generated = Path.Combine(staging, "generated");
                Directory.CreateDirectory(references);
                Directory.CreateDirectory(generated);

                CustomPetManifest manifest = new CustomPetManifest
                {
                    Id = id,
                    Name = cleanName,
                    Species = cleanSpecies,
                    Status = "draft"
                };
                for (int i = 0; i < photos.Count; i++)
                {
                    string extension = Path.GetExtension(photos[i]).ToLowerInvariant();
                    string fileName = "reference-" + (i + 1).ToString("00") + extension;
                    File.Copy(photos[i], Path.Combine(references, fileName), true);
                    manifest.ReferenceFiles.Add(Path.Combine("references", fileName));
                }

                string promptPath = Path.Combine(staging, "prompt.md");
                File.WriteAllText(promptPath, BuildPrompt(manifest), new UTF8Encoding(false));
                string workflowPath = Path.Combine(staging, "workflow.html");
                File.WriteAllText(workflowPath, BuildWorkflowHtml(manifest), new UTF8Encoding(false));
                WriteManifest(Path.Combine(staging, "manifest.json"), manifest, serializer);
                Directory.Move(staging, project);

                result.Success = true;
                result.PetId = id;
                result.ProjectDirectory = project;
                result.PromptPath = Path.Combine(project, "prompt.md");
                result.WorkflowPath = Path.Combine(project, "workflow.html");
                return result;
            }
            catch (Exception ex)
            {
                try { if (Directory.Exists(staging)) Directory.Delete(staging, true); } catch { }
                result.Error = "创建自定义宠物项目失败：" + CleanError(ex.Message);
                return result;
            }
        }

        public string FindLatestProject()
        {
            try
            {
                return Directory.GetDirectories(customRoot)
                    .Where(delegate(string path) { return File.Exists(Path.Combine(path, "manifest.json")); })
                    .OrderByDescending(delegate(string path) { return File.GetLastWriteTimeUtc(Path.Combine(path, "manifest.json")); })
                    .FirstOrDefault() ?? "";
            }
            catch { return ""; }
        }

        public CustomPetResult ImportGeneratedAssets(string projectDirectory)
        {
            CustomPetResult result = new CustomPetResult { ProjectDirectory = projectDirectory ?? "" };
            try
            {
                string project = Path.GetFullPath(projectDirectory ?? "");
                string root = Path.GetFullPath(customRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (!project.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("项目不在 WorkMate 自定义宠物目录内。");
                string manifestPath = Path.Combine(project, "manifest.json");
                CustomPetManifest manifest = ReadManifest(manifestPath, serializer);
                if (manifest == null || string.IsNullOrWhiteSpace(manifest.Id)) throw new InvalidDataException("manifest.json 无效。");
                string expectedId = new DirectoryInfo(project).Name;
                if (!string.Equals(manifest.Id, expectedId, StringComparison.Ordinal)
                    || !System.Text.RegularExpressions.Regex.IsMatch(manifest.Id, "^custom-[0-9]{8}-[0-9]{6}-[0-9a-f]{6}$"))
                    throw new InvalidDataException("manifest.json 的宠物 ID 与项目目录不一致。");

                string generated = Path.Combine(project, "generated");
                Dictionary<string, string> sources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (string action in RequiredActions) sources[action] = Path.Combine(generated, action + ".png");
                CustomPetValidationResult validation = ValidateAssets(sources);
                if (!validation.Success) { result.Error = validation.Error; return result; }

                string importing = Path.Combine(project, "assets.importing-" + Guid.NewGuid().ToString("N").Substring(0, 6));
                Directory.CreateDirectory(importing);
                foreach (string action in RequiredActions) File.Copy(sources[action], Path.Combine(importing, action + ".png"), true);
                string assets = Path.Combine(project, "assets");
                string backup = "";
                try
                {
                    if (Directory.Exists(assets))
                    {
                        backup = Path.Combine(project, "assets.backup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")
                            + "-" + Guid.NewGuid().ToString("N").Substring(0, 4));
                        Directory.Move(assets, backup);
                    }
                    Directory.Move(importing, assets);
                }
                catch
                {
                    if (!Directory.Exists(assets) && backup.Length > 0 && Directory.Exists(backup))
                    {
                        try { Directory.Move(backup, assets); } catch { }
                    }
                    try { if (Directory.Exists(importing)) Directory.Delete(importing, true); } catch { }
                    throw;
                }
                manifest.Status = "ready";
                manifest.UpdatedAt = DateTime.Now.ToString("o");
                WriteManifest(manifestPath, manifest, serializer);
                PetCatalog.ConfigureCustomRoot(customRoot);
                PetAssets.InvalidatePet(manifest.Id);

                result.Success = true;
                result.PetId = manifest.Id;
                result.ProjectDirectory = project;
                result.PromptPath = Path.Combine(project, "prompt.md");
                result.WorkflowPath = Path.Combine(project, "workflow.html");
                return result;
            }
            catch (Exception ex)
            {
                result.Error = "导入失败：" + CleanError(ex.Message);
                return result;
            }
        }

        public static List<PetDefinition> LoadReadyDefinitions(string root)
        {
            List<PetDefinition> result = new List<PetDefinition>();
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) return result;
            JavaScriptSerializer json = new JavaScriptSerializer { MaxJsonLength = 4 * 1024 * 1024 };
            try
            {
                foreach (string directory in Directory.GetDirectories(root))
                {
                    CustomPetManifest manifest = ReadManifest(Path.Combine(directory, "manifest.json"), json);
                    if (manifest == null || manifest.Status != "ready" || string.IsNullOrWhiteSpace(manifest.Id)) continue;
                    if (!string.Equals(manifest.Id, new DirectoryInfo(directory).Name, StringComparison.Ordinal)
                        || !System.Text.RegularExpressions.Regex.IsMatch(manifest.Id, "^custom-[0-9]{8}-[0-9]{6}-[0-9a-f]{6}$")) continue;
                    string assets = Path.Combine(directory, "assets");
                    if (RequiredActions.Any(delegate(string action) { return !File.Exists(Path.Combine(assets, action + ".png")); })) continue;
                    Dictionary<string, string> sources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (string action in RequiredActions) sources[action] = Path.Combine(assets, action + ".png");
                    if (!ValidateAssets(sources).Success) continue;
                    result.Add(new PetDefinition(manifest.Id, CleanText(manifest.Name, 24), CleanText(manifest.Species, 24),
                        ValidAccent(manifest.Accent), CleanText(manifest.Tagline, 60), true, assets));
                }
            }
            catch { }
            return result.OrderBy(delegate(PetDefinition pet) { return pet.Name; }, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        public static CustomPetValidationResult ValidateAssets(IDictionary<string, string> paths)
        {
            CustomPetValidationResult result = new CustomPetValidationResult();
            try
            {
                List<AssetProbe> probes = new List<AssetProbe>();
                foreach (string action in RequiredActions)
                {
                    string path;
                    if (paths == null || !paths.TryGetValue(action, out path) || !File.Exists(path))
                        throw new InvalidDataException("generated 文件夹缺少 " + action + ".png。");
                    probes.Add(Probe(action, path));
                }
                AssetProbe baseline = probes[0];
                if (baseline.Width < 256 || baseline.Width > 2048 || baseline.Height < 256 || baseline.Height > 2048)
                    throw new InvalidDataException("图片边长需在 256–2048 px 之间。");
                if (baseline.Width / (double)baseline.Height < 0.80 || baseline.Width / (double)baseline.Height > 1.20)
                    throw new InvalidDataException("四张图应使用近似正方形画布。");
                foreach (AssetProbe probe in probes)
                {
                    if (probe.Width != baseline.Width || probe.Height != baseline.Height)
                        throw new InvalidDataException("四张图的画布尺寸必须完全一致。");
                    if (probe.TransparentRatio < 0.08)
                        throw new InvalidDataException(probe.Action + ".png 没有足够透明背景；请导出 RGBA PNG。");
                    if (probe.OpaqueRatio < 0.03 || probe.OpaqueRatio > 0.88)
                        throw new InvalidDataException(probe.Action + ".png 的宠物主体占比异常。");
                    if (Math.Abs(probe.CenterX - baseline.CenterX) > 0.13 || Math.Abs(probe.CenterY - baseline.CenterY) > 0.13)
                        throw new InvalidDataException(probe.Action + ".png 与 idle.png 的主体中心偏差过大。");
                    if (Math.Abs(probe.Bottom - baseline.Bottom) > 0.09)
                        throw new InvalidDataException(probe.Action + ".png 与 idle.png 的脚底基线不一致。");
                    if (Math.Abs(probe.BoundsWidth - baseline.BoundsWidth) > 0.22
                        || Math.Abs(probe.BoundsHeight - baseline.BoundsHeight) > 0.22)
                        throw new InvalidDataException(probe.Action + ".png 与 idle.png 的主体尺度差异过大。");
                }
                result.Success = true;
                result.Width = baseline.Width;
                result.Height = baseline.Height;
                result.CheckedAssets = probes.Count;
            }
            catch (Exception ex) { result.Error = CleanError(ex.Message); }
            return result;
        }

        private sealed class AssetProbe
        {
            public string Action;
            public int Width;
            public int Height;
            public double TransparentRatio;
            public double OpaqueRatio;
            public double CenterX;
            public double CenterY;
            public double Bottom;
            public double BoundsWidth;
            public double BoundsHeight;
        }

        private static AssetProbe Probe(string action, string path)
        {
            FileInfo file = new FileInfo(path);
            if (file.Length <= 0 || file.Length > 25L * 1024 * 1024)
                throw new InvalidDataException(action + ".png 文件大小异常（上限 25 MB）。");
            BitmapSource source;
            int width;
            int height;
            using (FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                BitmapDecoder decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnDemand);
                if (decoder.Frames.Count == 0) throw new InvalidDataException(action + ".png 无可用图像帧。");
                source = decoder.Frames[0];
                width = source.PixelWidth;
                height = source.PixelHeight;
                if (width < 256 || width > 2048 || height < 256 || height > 2048)
                    throw new InvalidDataException(action + ".png 边长需在 256–2048 px 之间。");
                BitmapSource decoded = source;
                if (source.Format != PixelFormats.Bgra32 && source.Format != PixelFormats.Pbgra32)
                    decoded = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
                int decodedStride = width * 4;
                byte[] decodedPixels = new byte[decodedStride * height];
                decoded.CopyPixels(decodedPixels, decodedStride, 0);
                source = BitmapSource.Create(width, height, source.DpiX, source.DpiY, PixelFormats.Bgra32, null, decodedPixels, decodedStride);
                source.Freeze();
            }
            int stride = width * 4;
            byte[] pixels = new byte[stride * height];
            source.CopyPixels(pixels, stride, 0);
            long transparent = 0;
            long opaque = 0;
            int minX = width, minY = height, maxX = -1, maxY = -1;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    byte alpha = pixels[y * stride + x * 4 + 3];
                    if (alpha <= 8) transparent++;
                    if (alpha < 20) continue;
                    opaque++;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
            if (maxX < minX || maxY < minY) throw new InvalidDataException(action + ".png 没有可见宠物主体。");
            double count = width * (double)height;
            double x0 = minX / (double)width;
            double x1 = (maxX + 1) / (double)width;
            double y0 = minY / (double)height;
            double y1 = (maxY + 1) / (double)height;
            return new AssetProbe
            {
                Action = action,
                Width = width,
                Height = height,
                TransparentRatio = transparent / count,
                OpaqueRatio = opaque / count,
                CenterX = (x0 + x1) / 2,
                CenterY = (y0 + y1) / 2,
                Bottom = y1,
                BoundsWidth = x1 - x0,
                BoundsHeight = y1 - y0
            };
        }

        private static bool IsSupportedPhoto(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return false;
            FileInfo file = new FileInfo(path);
            if (file.Length <= 0 || file.Length > 25L * 1024 * 1024) return false;
            string extension = Path.GetExtension(path).ToLowerInvariant();
            return extension == ".png" || extension == ".jpg" || extension == ".jpeg" || extension == ".webp" || extension == ".bmp";
        }

        private static CustomPetManifest ReadManifest(string path, JavaScriptSerializer json)
        {
            try
            {
                if (!File.Exists(path)) return null;
                return json.Deserialize<CustomPetManifest>(File.ReadAllText(path, Encoding.UTF8));
            }
            catch { return null; }
        }

        private static void WriteManifest(string path, CustomPetManifest manifest, JavaScriptSerializer json)
        {
            string temp = path + ".tmp";
            File.WriteAllText(temp, json.Serialize(manifest), new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(temp, path, path + ".bak", true);
            else File.Move(temp, path);
        }

        private static string BuildPrompt(CustomPetManifest manifest)
        {
            string references = string.Join("、", manifest.ReferenceFiles.ToArray());
            return "# WorkMate 自定义桌宠生成提示词\n\n"
                + "## 参考与目标\n\n参考照片：" + references + "。把照片中的同一只" + manifest.Species + "转绘为统一身份的二次元 Q 版桌宠，名字为“" + manifest.Name + "”。必须保留最有辨识度的毛色/斑纹、耳形、眼色、脸型和体态，不增加照片中没有的项圈、衣服或花纹。\n\n"
                + "## 固定美术契约（四张都必须一致）\n\n"
                + "- 1024×1024 RGBA PNG，背景完全透明；不要场景、文字、水印、边框、投影或地面。\n"
                + "- 温暖、精致、现代日系 2D 动画质感；轮廓干净，柔和赛璐璐上色，2–3 层明暗，避免写实毛发噪点。\n"
                + "- 同一角色设定、同一三分之四正面视角、同一镜头高度、同一线宽、同一色板和光源。\n"
                + "- 主体水平中心固定在画布 50%；可见包围盒约占宽 58%、高 76%；脚底/身体最低点固定在画布高度 88%。\n"
                + "- 四肢和尾巴完整，不裁切；四张之间不得改变头身比、毛色、斑纹位置、眼睛大小或配饰。\n\n"
                + "## 先锁定身份，再输出四个独立文件\n\n"
                + "1. idle.png：自然坐/站，神情放松，正作为所有姿态的身份与尺度基准。\n"
                + "2. typing.png：同一角色在小键盘前轻敲，键盘必须贴近身体且不遮挡脸；身体中心和脚底基线不变。\n"
                + "3. happy.png：同一角色开心但不过度跳跃，眼神明亮，可轻抬一只前爪；主体尺度不变。\n"
                + "4. sleep.png：同一角色安稳蜷睡，仍保持主体视觉中心与底部基线接近 idle；不要床、毯子或场景。\n\n"
                + "## 负面约束\n\n不同宠物、身份漂移、毛色漂移、斑纹移动、额外肢体、少肢体、畸形爪、多个角色、裁切、透视突变、镜头缩放、背景、白底、棋盘格、文字、签名、水印、光晕、复杂道具、写实照片、3D 塑料感、过度细碎毛发。\n\n"
                + "## 推荐执行顺序\n\n先根据参考图生成一张角色设定确认图；确认身份后，把它连同原始照片作为后续四个姿态的共同参考。每次只改动作，不改角色。最后逐张去背并按上述文件名放入 generated/。如果工具支持 seed / reference strength / character reference，四张固定同一 seed，并把身份参考权重设为高。\n";
        }

        private static string BuildWorkflowHtml(CustomPetManifest manifest)
        {
            string name = Html(manifest.Name);
            return "<!doctype html><html lang=\"zh-CN\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>" + name + " · 自定义桌宠</title>"
                + "<style>body{margin:0;background:#f6f1ed;color:#2f2926;font:15px/1.7 'Segoe UI','Microsoft YaHei',sans-serif}.wrap{max-width:820px;margin:40px auto;padding:0 24px}.card{background:white;border:1px solid #e6dcd5;border-radius:22px;padding:26px;margin:18px 0;box-shadow:0 12px 36px #4a342514}h1{font-size:30px}h2{font-size:19px;margin-top:0}.step{display:inline-grid;place-items:center;width:30px;height:30px;border-radius:50%;background:#e77959;color:white;font-weight:700;margin-right:9px}code{background:#f3ece7;padding:2px 7px;border-radius:7px}li{margin:7px 0}.ok{color:#237a57;font-weight:700}</style></head><body><main class=\"wrap\"><h1>把真实宠物变成 WorkMate 伙伴</h1><p>项目：<b>" + name + "</b>。照片只保存在这个本地项目中；使用哪家图片模型由你决定。</p>"
                + "<section class=\"card\"><h2><span class=\"step\">1</span>生成</h2><ol><li>打开 <code>prompt.md</code>，把其中完整提示词和 <code>references/</code> 里的照片交给支持参考图的生成工具。</li><li>先确认角色设定，再生成 idle / typing / happy / sleep 四个姿态。</li><li>不要把“四格图”直接当成最终文件；应分别导出四张透明 PNG。</li></ol></section>"
                + "<section class=\"card\"><h2><span class=\"step\">2</span>放置</h2><p>把四张图放进 <code>generated/</code>，文件名必须为：</p><ul><li><code>idle.png</code></li><li><code>typing.png</code></li><li><code>happy.png</code></li><li><code>sleep.png</code></li></ul></section>"
                + "<section class=\"card\"><h2><span class=\"step\">3</span>校验并启用</h2><p>回到 WorkMate → 设置 → 自定义宠物工坊，点击“校验并启用最近项目”。WorkMate 会检查透明背景、统一尺寸、主体中心、尺度和脚底基线；通过后才会进入宠物列表。</p><p class=\"ok\">门禁通过后，四个工作状态会直接使用你的图片；复杂逐帧动作会安全回退到轻量程序动效。</p></section></main></body></html>";
        }

        private static string CleanText(string value, int maxLength)
        {
            string clean = (value ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
            return clean.Length <= maxLength ? clean : clean.Substring(0, maxLength);
        }

        private static string ValidAccent(string value)
        {
            string clean = (value ?? "").Trim();
            return System.Text.RegularExpressions.Regex.IsMatch(clean, "^#[0-9A-Fa-f]{6}$") ? clean : "#7B6BE6";
        }

        private static string CleanError(string value)
        {
            string clean = string.IsNullOrWhiteSpace(value) ? "未知错误" : value.Replace("\r", " ").Replace("\n", " ").Trim();
            return clean.Length <= 180 ? clean : clean.Substring(0, 180) + "…";
        }

        private static string Html(string value)
        {
            return (value ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
        }
    }
}
