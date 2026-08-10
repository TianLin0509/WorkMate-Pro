using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
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
        public CustomPetValidationResult Validation;
    }

    public sealed class CustomPetAssetSummary
    {
        public string Action;
        public string Path;
        public int Width;
        public int Height;
        public double TransparentRatio;
        public double OpaqueRatio;
        public double CenterX;
        public double CenterY;
        public double Bottom;
    }

    public sealed class CustomPetValidationResult
    {
        public bool Success;
        public string Error;
        public string RecoveryHint;
        public int Width;
        public int Height;
        public int CheckedAssets;
        public List<CustomPetAssetSummary> Assets = new List<CustomPetAssetSummary>();
    }

    public sealed class CustomPetPhotoInfo
    {
        public string Path;
        public string FileName;
        public int Width;
        public int Height;
        public long SizeBytes;
    }

    public sealed class CustomPetReferenceValidationResult
    {
        public bool Success;
        public string Error;
        public string RecoveryHint;
        public List<CustomPetPhotoInfo> Photos = new List<CustomPetPhotoInfo>();
    }

    public sealed class CustomPetProjectInfo
    {
        public bool Success;
        public string Error;
        public string Id;
        public string Name;
        public string Species;
        public string Status;
        public string ProjectDirectory;
        public string PromptPath;
        public string WorkflowPath;
        public DateTime UpdatedAt;
        public List<string> ReferencePaths = new List<string>();
        public Dictionary<string, string> GeneratedPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public int GeneratedCount;
        public int AssetCount;
        public bool Ready { get { return string.Equals(Status, "ready", StringComparison.OrdinalIgnoreCase) && AssetCount == CustomPetService.RequiredActions.Length; } }
    }

    /// <summary>
    /// 照片只复制到用户本机项目目录。生成模型由用户自行选择；WorkMate 负责提示词、文件契约、
    /// 透明图质量门禁与运行时导入，避免把第三方密钥或照片上传责任偷偷塞进桌宠进程。
    /// </summary>
    public sealed class CustomPetService
    {
        public static readonly string[] RequiredActions = { "idle", "typing", "happy", "sleep" };
        public const int MaximumProjects = 100;
        private readonly JavaScriptSerializer serializer = new JavaScriptSerializer();
        private readonly object mutationSync = new object();
        private readonly string customRoot;

        public string CustomRoot { get { return customRoot; } }

        public CustomPetService(string dataRoot)
        {
            customRoot = Path.Combine(dataRoot, "CustomPets");
            Directory.CreateDirectory(customRoot);
            EnsureSafeDirectory(customRoot, "CustomPets");
            serializer.MaxJsonLength = 4 * 1024 * 1024;
        }

        public CustomPetResult CreateProject(string name, string species, IList<string> photoPaths)
        {
            lock (mutationSync)
            {
                CustomPetResult result = new CustomPetResult();
                string cleanName = CleanText(name, 24);
                string cleanSpecies = CleanText(species, 24);
                if (cleanName.Length == 0) { result.Error = "请先给自定义伙伴起个名字。"; return result; }
                if (cleanSpecies.Length == 0) cleanSpecies = "自定义宠物";
                CustomPetReferenceValidationResult referencesCheck = ValidateReferencePhotos(photoPaths);
                if (!referencesCheck.Success)
                {
                    result.Error = referencesCheck.Error + (string.IsNullOrWhiteSpace(referencesCheck.RecoveryHint) ? "" : " " + referencesCheck.RecoveryHint);
                    return result;
                }
                if (SafeProjectDirectories(customRoot).Count >= MaximumProjects)
                {
                    result.Error = "自定义宠物项目已达到 " + MaximumProjects + " 个上限；请先在项目目录中归档不再使用的旧项目。";
                    return result;
                }

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
                    for (int i = 0; i < referencesCheck.Photos.Count; i++)
                    {
                        string extension = Path.GetExtension(referencesCheck.Photos[i].Path).ToLowerInvariant();
                        string fileName = "reference-" + (i + 1).ToString("00") + extension;
                        File.Copy(referencesCheck.Photos[i].Path, Path.Combine(references, fileName), true);
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
        }

        public string FindLatestProject()
        {
            CustomPetProjectInfo latest = ListProjects().FirstOrDefault();
            return latest == null ? "" : latest.ProjectDirectory;
        }

        public List<CustomPetProjectInfo> ListProjects()
        {
            lock (mutationSync)
            {
                List<CustomPetProjectInfo> projects = new List<CustomPetProjectInfo>();
                foreach (string directory in SafeProjectDirectories(customRoot))
                {
                    CustomPetProjectInfo info = BuildProjectInfo(directory);
                    if (info.Success) projects.Add(info);
                }
                return projects.OrderByDescending(delegate(CustomPetProjectInfo info) { return info.UpdatedAt; })
                    .Take(MaximumProjects).ToList();
            }
        }

        public CustomPetProjectInfo GetProjectInfo(string projectDirectory)
        {
            lock (mutationSync)
            {
                try { return BuildProjectInfo(ResolveOwnedProject(projectDirectory)); }
                catch (Exception ex) { return new CustomPetProjectInfo { Error = CleanError(ex.Message), ProjectDirectory = projectDirectory ?? "" }; }
            }
        }

        public string ReadPrompt(string projectDirectory)
        {
            lock (mutationSync)
            {
                string project = ResolveOwnedProject(projectDirectory);
                string path = Path.Combine(project, "prompt.md");
                if (!File.Exists(path)) throw new FileNotFoundException("项目缺少 prompt.md。", path);
                if (new FileInfo(path).Length > 512 * 1024) throw new InvalidDataException("prompt.md 大小异常。");
                return File.ReadAllText(path, Encoding.UTF8);
            }
        }

        public CustomPetValidationResult ValidateProjectAssets(string projectDirectory)
        {
            lock (mutationSync)
            {
                try
                {
                    string project = ResolveOwnedProject(projectDirectory);
                    CustomPetManifest manifest = ReadManifest(Path.Combine(project, "manifest.json"), serializer);
                    ValidateManifestIdentity(project, manifest);
                    string folderName = string.Equals(manifest.Status, "ready", StringComparison.OrdinalIgnoreCase) ? "assets" : "generated";
                    string assets = Path.Combine(project, folderName);
                    EnsureSafeDirectory(assets, folderName);
                    return ValidateAssets(ActionPaths(assets));
                }
                catch (Exception ex)
                {
                    return FailedValidation(ex.Message);
                }
            }
        }

        public CustomPetResult PrepareGeneratedAssets(string projectDirectory, IDictionary<string, string> sourcePaths)
        {
            lock (mutationSync)
            {
                CustomPetResult result = new CustomPetResult { ProjectDirectory = projectDirectory ?? "" };
                string staging = "";
                string backup = "";
                string generated = "";
                try
                {
                    string project = ResolveOwnedProject(projectDirectory);
                    CustomPetManifest manifest = ReadManifest(Path.Combine(project, "manifest.json"), serializer);
                    ValidateManifestIdentity(project, manifest);
                    CustomPetValidationResult sourceValidation = ValidateAssets(sourcePaths);
                    if (!sourceValidation.Success)
                    {
                        result.Error = sourceValidation.Error;
                        result.Validation = sourceValidation;
                        return result;
                    }

                    staging = Path.Combine(project, "generated.importing-" + Guid.NewGuid().ToString("N").Substring(0, 8));
                    Directory.CreateDirectory(staging);
                    foreach (string action in RequiredActions)
                        File.Copy(sourcePaths[action], Path.Combine(staging, action + ".png"), true);
                    CustomPetValidationResult stagedValidation = ValidateAssets(ActionPaths(staging));
                    if (!stagedValidation.Success) throw new InvalidDataException(stagedValidation.Error);

                    generated = Path.Combine(project, "generated");
                    if (Directory.Exists(generated))
                    {
                        EnsureSafeDirectory(generated, "generated");
                        backup = Path.Combine(project, "generated.backup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")
                            + "-" + Guid.NewGuid().ToString("N").Substring(0, 4));
                        Directory.Move(generated, backup);
                    }
                    try { Directory.Move(staging, generated); }
                    catch
                    {
                        if (!Directory.Exists(generated) && backup.Length > 0 && Directory.Exists(backup))
                            Directory.Move(backup, generated);
                        throw;
                    }
                    PruneBackups(project, "generated.backup-*", 2);
                    foreach (CustomPetAssetSummary asset in stagedValidation.Assets)
                        asset.Path = Path.Combine(generated, asset.Action + ".png");

                    CustomPetProjectInfo info = BuildProjectInfo(project);
                    result.Success = true;
                    result.PetId = info.Id;
                    result.ProjectDirectory = project;
                    result.PromptPath = info.PromptPath;
                    result.WorkflowPath = info.WorkflowPath;
                    result.Validation = stagedValidation;
                    return result;
                }
                catch (Exception ex)
                {
                    try { if (staging.Length > 0 && Directory.Exists(staging)) Directory.Delete(staging, true); } catch { }
                    result.Error = "准备生成图片失败：" + CleanError(ex.Message);
                    if (result.Validation == null) result.Validation = FailedValidation(ex.Message);
                    return result;
                }
            }
        }

        public CustomPetResult ImportGeneratedAssets(string projectDirectory)
        {
            lock (mutationSync)
            {
                CustomPetResult result = new CustomPetResult { ProjectDirectory = projectDirectory ?? "" };
                string importing = "";
                try
                {
                    string project = ResolveOwnedProject(projectDirectory);
                    string manifestPath = Path.Combine(project, "manifest.json");
                    CustomPetManifest manifest = ReadManifest(manifestPath, serializer);
                    ValidateManifestIdentity(project, manifest);

                    string generated = Path.Combine(project, "generated");
                    EnsureSafeDirectory(generated, "generated");
                    Dictionary<string, string> sources = ActionPaths(generated);
                    CustomPetValidationResult validation = ValidateAssets(sources);
                    result.Validation = validation;
                    if (!validation.Success) { result.Error = validation.Error; return result; }

                    importing = Path.Combine(project, "assets.importing-" + Guid.NewGuid().ToString("N").Substring(0, 8));
                    Directory.CreateDirectory(importing);
                    foreach (string action in RequiredActions) File.Copy(sources[action], Path.Combine(importing, action + ".png"), true);
                    CustomPetValidationResult copiedValidation = ValidateAssets(ActionPaths(importing));
                    if (!copiedValidation.Success) throw new InvalidDataException(copiedValidation.Error);

                    string assets = Path.Combine(project, "assets");
                    string backup = "";
                    if (Directory.Exists(assets))
                    {
                        EnsureSafeDirectory(assets, "assets");
                        backup = Path.Combine(project, "assets.backup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")
                            + "-" + Guid.NewGuid().ToString("N").Substring(0, 4));
                        Directory.Move(assets, backup);
                    }
                    try
                    {
                        Directory.Move(importing, assets);
                        manifest.Status = "ready";
                        manifest.UpdatedAt = DateTime.Now.ToString("o");
                        WriteManifest(manifestPath, manifest, serializer);
                    }
                    catch
                    {
                        try { if (Directory.Exists(assets)) Directory.Delete(assets, true); } catch { }
                        if (backup.Length > 0 && Directory.Exists(backup))
                        {
                            try { Directory.Move(backup, assets); } catch { }
                        }
                        throw;
                    }

                    PetCatalog.UpsertCustom(new PetDefinition(manifest.Id, CleanText(manifest.Name, 24), CleanText(manifest.Species, 24),
                        ValidAccent(manifest.Accent), CleanText(manifest.Tagline, 60), true, assets));
                    PruneBackups(project, "assets.backup-*", 3);
                    foreach (CustomPetAssetSummary asset in copiedValidation.Assets)
                        asset.Path = Path.Combine(assets, asset.Action + ".png");

                    result.Success = true;
                    result.PetId = manifest.Id;
                    result.ProjectDirectory = project;
                    result.PromptPath = Path.Combine(project, "prompt.md");
                    result.WorkflowPath = Path.Combine(project, "workflow.html");
                    result.Validation = copiedValidation;
                    return result;
                }
                catch (Exception ex)
                {
                    try { if (importing.Length > 0 && Directory.Exists(importing)) Directory.Delete(importing, true); } catch { }
                    result.Error = "导入失败：" + CleanError(ex.Message);
                    return result;
                }
            }
        }

        public static List<PetDefinition> LoadReadyDefinitions(string root)
        {
            List<PetDefinition> result = new List<PetDefinition>();
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) return result;
            JavaScriptSerializer json = new JavaScriptSerializer { MaxJsonLength = 4 * 1024 * 1024 };
            try
            {
                EnsureSafeDirectory(root, "CustomPets");
                foreach (string directory in SafeProjectDirectories(root).Take(MaximumProjects))
                {
                    CustomPetManifest manifest = ReadManifest(Path.Combine(directory, "manifest.json"), json);
                    if (manifest == null || !string.Equals(manifest.Status, "ready", StringComparison.OrdinalIgnoreCase)
                        || string.IsNullOrWhiteSpace(manifest.Id)) continue;
                    try { ValidateManifestIdentity(directory, manifest); } catch { continue; }
                    string assets = Path.Combine(directory, "assets");
                    try { EnsureSafeDirectory(assets, "assets"); } catch { continue; }
                    if (RequiredActions.Any(delegate(string action) { return !File.Exists(Path.Combine(assets, action + ".png")); })) continue;
                    Dictionary<string, string> sources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (string action in RequiredActions) sources[action] = Path.Combine(assets, action + ".png");
                    if (!ValidateAssets(sources).Success) continue;
                    string name = CleanText(manifest.Name, 24);
                    string species = CleanText(manifest.Species, 24);
                    result.Add(new PetDefinition(manifest.Id, name.Length == 0 ? "未命名伙伴" : name,
                        species.Length == 0 ? "自定义宠物" : species,
                        ValidAccent(manifest.Accent), CleanText(manifest.Tagline, 60), true, assets));
                }
            }
            catch { }
            return result.OrderBy(delegate(PetDefinition pet) { return pet.Name; }, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        public static CustomPetReferenceValidationResult ValidateReferencePhotos(IList<string> paths)
        {
            CustomPetReferenceValidationResult result = new CustomPetReferenceValidationResult();
            try
            {
                List<string> unique = paths == null ? new List<string>() : paths
                    .Where(delegate(string path) { return !string.IsNullOrWhiteSpace(path); })
                    .Select(Path.GetFullPath)
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                if (unique.Count < 1 || unique.Count > 3)
                    throw new InvalidDataException("请选择 1–3 张参考照片。");
                foreach (string path in unique)
                {
                    if (!IsSupportedPhoto(path))
                        throw new InvalidDataException("不支持或无法读取参考照片：" + Path.GetFileName(path));
                    ValidatePhotoContainer(path);
                    FileInfo file = new FileInfo(path);
                    int width;
                    int height;
                    using (FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        BitmapDecoder decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnDemand);
                        if (decoder.Frames.Count == 0) throw new InvalidDataException("参考照片没有可用图像帧：" + file.Name);
                        width = decoder.Frames[0].PixelWidth;
                        height = decoder.Frames[0].PixelHeight;
                    }
                    if (width < 128 || height < 128 || width > 8192 || height > 8192)
                        throw new InvalidDataException("参考照片边长需在 128–8192 px 之间：" + file.Name);
                    using (FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        BitmapImage decoded = new BitmapImage();
                        decoded.BeginInit();
                        decoded.CacheOption = BitmapCacheOption.OnLoad;
                        decoded.DecodePixelWidth = Math.Min(512, width);
                        decoded.StreamSource = stream;
                        decoded.EndInit();
                        if (decoded.PixelWidth <= 0 || decoded.PixelHeight <= 0)
                            throw new InvalidDataException("参考照片无法完整解码：" + file.Name);
                    }
                    result.Photos.Add(new CustomPetPhotoInfo
                    {
                        Path = path,
                        FileName = file.Name,
                        Width = width,
                        Height = height,
                        SizeBytes = file.Length
                    });
                }
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Error = CleanError(ex.Message);
                result.RecoveryHint = "请重新选择清晰、未损坏的 PNG/JPG/JPEG/WEBP/BMP；单张不超过 25 MB。";
            }
            return result;
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
                result.Assets = probes.Select(delegate(AssetProbe probe)
                {
                    return new CustomPetAssetSummary
                    {
                        Action = probe.Action,
                        Path = probe.Path,
                        Width = probe.Width,
                        Height = probe.Height,
                        TransparentRatio = probe.TransparentRatio,
                        OpaqueRatio = probe.OpaqueRatio,
                        CenterX = probe.CenterX,
                        CenterY = probe.CenterY,
                        Bottom = probe.Bottom
                    };
                }).ToList();
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
            catch (Exception ex)
            {
                result.Error = CleanError(ex.Message);
                result.RecoveryHint = ValidationRecovery(result.Error);
            }
            return result;
        }

        private sealed class AssetProbe
        {
            public string Action;
            public string Path;
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
                Path = path,
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

        private CustomPetProjectInfo BuildProjectInfo(string project)
        {
            CustomPetProjectInfo info = new CustomPetProjectInfo { ProjectDirectory = project ?? "" };
            try
            {
                project = ResolveOwnedProject(project);
                string manifestPath = Path.Combine(project, "manifest.json");
                CustomPetManifest manifest = ReadManifest(manifestPath, serializer);
                ValidateManifestIdentity(project, manifest);
                info.Id = manifest.Id;
                info.Name = CleanText(manifest.Name, 24);
                if (info.Name.Length == 0) info.Name = "未命名伙伴";
                info.Species = CleanText(manifest.Species, 24);
                if (info.Species.Length == 0) info.Species = "自定义宠物";
                info.Status = string.IsNullOrWhiteSpace(manifest.Status) ? "draft" : manifest.Status.Trim().ToLowerInvariant();
                info.ProjectDirectory = project;
                info.PromptPath = Path.Combine(project, "prompt.md");
                info.WorkflowPath = Path.Combine(project, "workflow.html");
                DateTime updated;
                info.UpdatedAt = DateTime.TryParse(manifest.UpdatedAt, out updated) ? updated : File.GetLastWriteTime(manifestPath);

                foreach (string relative in manifest.ReferenceFiles ?? new List<string>())
                {
                    string path = ResolveSafeChildPath(project, relative);
                    if (File.Exists(path)) info.ReferencePaths.Add(path);
                }
                string generated = Path.Combine(project, "generated");
                if (Directory.Exists(generated))
                {
                    EnsureSafeDirectory(generated, "generated");
                    foreach (string action in RequiredActions)
                    {
                        string path = Path.Combine(generated, action + ".png");
                        if (File.Exists(path)) { info.GeneratedPaths[action] = path; info.GeneratedCount++; }
                    }
                }
                string assets = Path.Combine(project, "assets");
                if (Directory.Exists(assets))
                {
                    EnsureSafeDirectory(assets, "assets");
                    info.AssetCount = RequiredActions.Count(delegate(string action) { return File.Exists(Path.Combine(assets, action + ".png")); });
                }
                info.Success = true;
            }
            catch (Exception ex) { info.Error = CleanError(ex.Message); }
            return info;
        }

        private string ResolveOwnedProject(string projectDirectory)
        {
            if (string.IsNullOrWhiteSpace(projectDirectory)) throw new InvalidOperationException("尚未选择自定义宠物项目。");
            string root = Path.GetFullPath(customRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string project = Path.GetFullPath(projectDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            DirectoryInfo parent = Directory.GetParent(project);
            if (parent == null || !string.Equals(parent.FullName.TrimEnd(Path.DirectorySeparatorChar), root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("项目必须是 WorkMate 自定义宠物目录中的直接子目录。");
            if (!Directory.Exists(project)) throw new DirectoryNotFoundException("自定义宠物项目不存在。");
            EnsureSafeDirectory(project, "项目");
            if (!System.Text.RegularExpressions.Regex.IsMatch(new DirectoryInfo(project).Name, "^custom-[0-9]{8}-[0-9]{6}-[0-9a-f]{6}$"))
                throw new InvalidDataException("项目目录名称无效。");
            return project;
        }

        private static List<string> SafeProjectDirectories(string root)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) return new List<string>();
                return Directory.GetDirectories(root)
                    .Where(delegate(string path)
                    {
                        try
                        {
                            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0
                                && System.Text.RegularExpressions.Regex.IsMatch(new DirectoryInfo(path).Name, "^custom-[0-9]{8}-[0-9]{6}-[0-9a-f]{6}$");
                        }
                        catch { return false; }
                    }).Take(MaximumProjects + 1).ToList();
            }
            catch { return new List<string>(); }
        }

        private static void EnsureSafeDirectory(string path, string label)
        {
            if (!Directory.Exists(path)) throw new DirectoryNotFoundException(label + " 文件夹不存在。");
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException(label + " 文件夹不能是符号链接或目录联接。");
        }

        private static string ResolveSafeChildPath(string project, string relative)
        {
            if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative))
                throw new InvalidDataException("manifest.json 包含无效的相对文件路径。");
            string root = Path.GetFullPath(project).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string path = Path.GetFullPath(Path.Combine(project, relative));
            if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("manifest.json 的文件路径越过了项目目录。");
            string parent = Path.GetDirectoryName(path);
            if (Directory.Exists(parent)) EnsureSafeDirectory(parent, "项目子目录");
            return path;
        }

        private static void ValidateManifestIdentity(string project, CustomPetManifest manifest)
        {
            if (manifest == null || string.IsNullOrWhiteSpace(manifest.Id)) throw new InvalidDataException("manifest.json 无效。");
            string expectedId = new DirectoryInfo(project).Name;
            if (!string.Equals(manifest.Id, expectedId, StringComparison.Ordinal)
                || !System.Text.RegularExpressions.Regex.IsMatch(manifest.Id, "^custom-[0-9]{8}-[0-9]{6}-[0-9a-f]{6}$"))
                throw new InvalidDataException("manifest.json 的宠物 ID 与项目目录不一致。");
        }

        private static Dictionary<string, string> ActionPaths(string directory)
        {
            Dictionary<string, string> paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string action in RequiredActions) paths[action] = Path.Combine(directory, action + ".png");
            return paths;
        }

        private static void PruneBackups(string project, string pattern, int keep)
        {
            try
            {
                string projectFull = Path.GetFullPath(project).TrimEnd(Path.DirectorySeparatorChar);
                foreach (string directory in Directory.GetDirectories(project, pattern, SearchOption.TopDirectoryOnly)
                    .OrderByDescending(Directory.GetLastWriteTimeUtc).Skip(Math.Max(0, keep)))
                {
                    DirectoryInfo parent = Directory.GetParent(directory);
                    if (parent == null || !string.Equals(parent.FullName.TrimEnd(Path.DirectorySeparatorChar), projectFull, StringComparison.OrdinalIgnoreCase)) continue;
                    if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) continue;
                    Directory.Delete(directory, true);
                }
            }
            catch { }
        }

        private static CustomPetValidationResult FailedValidation(string error)
        {
            string clean = CleanError(error);
            return new CustomPetValidationResult { Error = clean, RecoveryHint = ValidationRecovery(clean) };
        }

        private static string ValidationRecovery(string error)
        {
            string value = error ?? "";
            if (value.Contains("缺少")) return "回到第 3 步，为四个姿态分别选择图片，或把同名文件放进 generated 文件夹。";
            if (value.Contains("透明") || value.Contains("RGBA")) return "重新导出带 Alpha 通道的 RGBA PNG；不要使用白底或棋盘格截图。";
            if (value.Contains("尺寸") || value.Contains("画布") || value.Contains("正方形")) return "把四张图统一为同一张近似正方形画布，推荐 1024×1024。";
            if (value.Contains("中心") || value.Contains("基线") || value.Contains("尺度")) return "固定同一镜头、主体中心和脚底基线后重新生成；只改变动作，不改变缩放。";
            if (value.Contains("文件大小") || value.Contains("图像帧") || value.Contains("Invalid") || value.Contains("参数"))
                return "重新导出标准 PNG，并确认文件可以在 Windows 图片查看器中正常打开。";
            return "检查四个文件名和图片质量后重试；原项目会保留，不需要重新开始。";
        }

        private static bool IsSupportedPhoto(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return false;
            FileInfo file = new FileInfo(path);
            if (file.Length <= 0 || file.Length > 25L * 1024 * 1024) return false;
            string extension = Path.GetExtension(path).ToLowerInvariant();
            return extension == ".png" || extension == ".jpg" || extension == ".jpeg" || extension == ".webp" || extension == ".bmp";
        }

        private static void ValidatePhotoContainer(string path)
        {
            string extension = Path.GetExtension(path).ToLowerInvariant();
            using (FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                if (extension == ".png")
                {
                    if (stream.Length < 20 || !reader.ReadBytes(8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
                        throw new InvalidDataException("PNG 文件头无效：" + Path.GetFileName(path));
                    stream.Position = stream.Length - 12;
                    byte[] tail = reader.ReadBytes(12);
                    if (!tail.SequenceEqual(new byte[] { 0, 0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130 }))
                        throw new InvalidDataException("PNG 文件不完整：" + Path.GetFileName(path));
                    return;
                }
                if (extension == ".jpg" || extension == ".jpeg")
                {
                    if (stream.Length < 4 || reader.ReadByte() != 0xFF || reader.ReadByte() != 0xD8)
                        throw new InvalidDataException("JPEG 文件头无效：" + Path.GetFileName(path));
                    stream.Position = stream.Length - 2;
                    if (reader.ReadByte() != 0xFF || reader.ReadByte() != 0xD9)
                        throw new InvalidDataException("JPEG 文件不完整：" + Path.GetFileName(path));
                    return;
                }
                if (extension == ".webp")
                {
                    if (stream.Length < 12 || Encoding.ASCII.GetString(reader.ReadBytes(4)) != "RIFF")
                        throw new InvalidDataException("WEBP 文件头无效：" + Path.GetFileName(path));
                    uint declared = reader.ReadUInt32();
                    if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "WEBP" || declared + 8L > stream.Length)
                        throw new InvalidDataException("WEBP 文件不完整：" + Path.GetFileName(path));
                    return;
                }
                if (extension == ".bmp")
                {
                    if (stream.Length < 14 || reader.ReadByte() != (byte)'B' || reader.ReadByte() != (byte)'M')
                        throw new InvalidDataException("BMP 文件头无效：" + Path.GetFileName(path));
                    uint declared = reader.ReadUInt32();
                    if (declared > stream.Length || declared < 14)
                        throw new InvalidDataException("BMP 文件不完整：" + Path.GetFileName(path));
                }
            }
        }

        private static CustomPetManifest ReadManifest(string path, JavaScriptSerializer json)
        {
            try
            {
                if (!File.Exists(path)) return null;
                if (new FileInfo(path).Length <= 0 || new FileInfo(path).Length > 512 * 1024) return null;
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
                + "<section class=\"card\"><h2><span class=\"step\">1</span>身份与照片</h2><p>项目和参考照片已经由 WorkMate 保存在本机；关掉 App 后也可以继续。</p></section>"
                + "<section class=\"card\"><h2><span class=\"step\">2</span>生成指引</h2><ol><li>回到 App 内向导复制完整提示词，或打开同目录的 <code>prompt.md</code>。</li><li>把提示词与 <code>references/</code> 照片交给支持参考图的生成工具。</li><li>先锁定角色身份，再分别导出 idle / typing / happy / sleep 四张透明 PNG；不要直接使用四宫格截图。</li></ol></section>"
                + "<section class=\"card\"><h2><span class=\"step\">3</span>四张图片</h2><p>在 App 中逐张选择或拖放 PNG，原文件名可以不同；WorkMate 会映射为四个姿态并原子写入 <code>generated/</code>。也可以手动放入 <code>idle.png</code>、<code>typing.png</code>、<code>happy.png</code>、<code>sleep.png</code> 后点“从 generated 自动识别”。</p></section>"
                + "<section class=\"card\"><h2><span class=\"step\">4</span>校验并启用</h2><p>WorkMate 会检查透明背景、统一尺寸、主体中心、尺度和脚底基线；失败时按 App 内修复建议调整，原项目不会丢失。全部通过后点“启用这只伙伴”。</p><p class=\"ok\">启用后，四个工作状态会直接使用你的图片；复杂逐帧动作会安全回退到轻量程序动效。</p></section></main></body></html>";
        }

        private static string CleanText(string value, int maxLength)
        {
            string clean = new string((value ?? "").Select(delegate(char valueChar)
            {
                return char.IsControl(valueChar) ? ' ' : valueChar;
            }).ToArray()).Trim();
            while (clean.Contains("  ")) clean = clean.Replace("  ", " ");
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
