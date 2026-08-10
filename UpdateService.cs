using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;

namespace WorkMatePro
{
    public sealed class UpdatePackageManifest
    {
        public int SchemaVersion { get; set; }
        public string ProductId { get; set; }
        public string Kind { get; set; }
        public string FromVersion { get; set; }
        public string FromSha256 { get; set; }
        public string ToVersion { get; set; }
        public string ToSha256 { get; set; }
        public string PayloadFile { get; set; }
        public string PayloadSha256 { get; set; }
        public long PayloadSize { get; set; }
        public string CreatedUtc { get; set; }
        public string Notes { get; set; }
    }

    public sealed class UpdateCatalogPackage
    {
        public string Kind { get; set; }
        public string FromVersion { get; set; }
        public string FromSha256 { get; set; }
        public string FileName { get; set; }
        public string Url { get; set; }
        public long SizeBytes { get; set; }
        public string Sha256 { get; set; }
    }

    public sealed class UpdateCatalog
    {
        public int SchemaVersion { get; set; }
        public string ProductId { get; set; }
        public string LatestVersion { get; set; }
        public string PublishedUtc { get; set; }
        public string ReleaseUrl { get; set; }
        public string Notes { get; set; }
        public List<UpdateCatalogPackage> Packages { get; set; }
    }

    public sealed class UpdateCheckResult
    {
        public bool Success { get; set; }
        public bool Available { get; set; }
        public string LatestVersion { get; set; }
        public string Message { get; set; }
        public string RecommendedFileName { get; set; }
        public string PackagePath { get; set; }
        public string DownloadUrl { get; set; }
        public string ReleaseUrl { get; set; }
    }

    public sealed class StagedUpdate
    {
        public bool Success { get; set; }
        public string Error { get; set; }
        public string StageDirectory { get; set; }
        public string PayloadPath { get; set; }
        public string Kind { get; set; }
        public string FromVersion { get; set; }
        public string FromSha256 { get; set; }
        public string ToVersion { get; set; }
        public string ToSha256 { get; set; }
        public string Notes { get; set; }
    }

    /// <summary>
    /// Windows 自带的 MSDelta 二进制差分封装。发布端与客户端使用同一实现，
    /// 避免在公司电脑额外安装 Python、7-Zip 或补丁程序。
    /// </summary>
    public static class MsDeltaCodec
    {
        private const long DeltaFileTypeRaw = 1;
        private const long DeltaFlagNone = 0;
        private const long DeltaFlagIgnoreFileSizeLimit = 0x00020000;

        [StructLayout(LayoutKind.Sequential)]
        private struct DeltaInput
        {
            public IntPtr Start;
            public UIntPtr Size;
            public int Editable;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DeltaOutput
        {
            public IntPtr Start;
            public UIntPtr Size;
        }

        private sealed class PinnedInput : IDisposable
        {
            private GCHandle pin;
            public DeltaInput Value;

            public PinnedInput(byte[] bytes)
            {
                byte[] value = bytes ?? new byte[0];
                IntPtr pointer = IntPtr.Zero;
                if (value.Length > 0)
                {
                    pin = GCHandle.Alloc(value, GCHandleType.Pinned);
                    pointer = pin.AddrOfPinnedObject();
                }
                Value = new DeltaInput
                {
                    Start = pointer,
                    Size = new UIntPtr((ulong)value.LongLength),
                    Editable = 0
                };
            }

            public void Dispose()
            {
                if (pin.IsAllocated) pin.Free();
            }
        }

        [DllImport("msdelta.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CreateDeltaB(
            long fileTypeSet,
            long setFlags,
            long resetFlags,
            DeltaInput source,
            DeltaInput target,
            DeltaInput sourceOptions,
            DeltaInput targetOptions,
            DeltaInput globalOptions,
            IntPtr targetFileTime,
            uint hashAlgorithmId,
            out DeltaOutput delta);

        [DllImport("msdelta.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ApplyDeltaB(
            long applyFlags,
            DeltaInput source,
            DeltaInput delta,
            out DeltaOutput target);

        [DllImport("msdelta.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeltaFree(IntPtr memory);

        public static byte[] Create(byte[] source, byte[] target)
        {
            using (PinnedInput sourceInput = new PinnedInput(source))
            using (PinnedInput targetInput = new PinnedInput(target))
            using (PinnedInput empty = new PinnedInput(null))
            {
                DeltaOutput output;
                bool ok = CreateDeltaB(
                    DeltaFileTypeRaw,
                    DeltaFlagIgnoreFileSizeLimit,
                    DeltaFlagNone,
                    sourceInput.Value,
                    targetInput.Value,
                    empty.Value,
                    empty.Value,
                    empty.Value,
                    IntPtr.Zero,
                    0,
                    out output);
                if (!ok)
                {
                    int error = Marshal.GetLastWin32Error();
                    throw new Win32Exception(error, "MSDelta 创建差分失败（Win32 " + error + "）。");
                }
                return CopyAndFree(output);
            }
        }

        public static byte[] Apply(byte[] source, byte[] delta)
        {
            using (PinnedInput sourceInput = new PinnedInput(source))
            using (PinnedInput deltaInput = new PinnedInput(delta))
            {
                DeltaOutput output;
                bool ok = ApplyDeltaB(DeltaFlagNone, sourceInput.Value, deltaInput.Value, out output);
                if (!ok)
                {
                    int error = Marshal.GetLastWin32Error();
                    throw new Win32Exception(error, "MSDelta 应用差分失败（Win32 " + error + "）。");
                }
                return CopyAndFree(output);
            }
        }

        public static void CreateFile(string sourcePath, string targetPath, string deltaPath)
        {
            File.WriteAllBytes(deltaPath, Create(File.ReadAllBytes(sourcePath), File.ReadAllBytes(targetPath)));
        }

        public static void ApplyFile(string sourcePath, string deltaPath, string targetPath)
        {
            File.WriteAllBytes(targetPath, Apply(File.ReadAllBytes(sourcePath), File.ReadAllBytes(deltaPath)));
        }

        private static byte[] CopyAndFree(DeltaOutput output)
        {
            try
            {
                ulong length = output.Size.ToUInt64();
                if (length > int.MaxValue) throw new InvalidDataException("MSDelta 输出超过当前更新器支持的 2 GB 上限。");
                byte[] bytes = new byte[(int)length];
                if (bytes.Length > 0) Marshal.Copy(output.Start, bytes, 0, bytes.Length);
                return bytes;
            }
            finally
            {
                if (output.Start != IntPtr.Zero) DeltaFree(output.Start);
            }
        }
    }

    public static class UpdateTrust
    {
        // 发布密钥的公钥；私钥只保存在发布机的 C:\VibeData，不进入仓库或安装包。
        public const string PublicKeyXml = "<RSAKeyValue><Modulus>v9Y4lul3qH+on5kMYbI0sU1GB5i1VC97Gghu1Ucenz7kbMW/eOvXt3nyluP9arXsY8UZMiUUchQm1XPVh0LauDnpL3KhNoA450pXIq7pAnawQTwBfPSHD2/5J2CALTaZ0ximU6CfTn7rFVKnwQGnjX/cxbY0w6CZyWcxIyyK/3aNkmmPkCMdcgZZsPgCHqOOEUF0lDvBqwhF7c6C8U1EJJFiKQIFfO2+YwWpvGaTiv8hy+5GyxZDPCR4vQoexcAONMwL5RzZ9Fb41lI/LXshGyn7v15RGMxpQD+20rsOq5z5cvJaayrTBJG/73PKdJb4H6Fe2L2/26RkqGLo0D1X7mL8Hv/pbtjhPtfEJJc++2Lcw4r8rcJ/bopaE/oVrIee3zEBQT8Lajlm5WhZamPDfUHwJZOOG466mTD7irtzriQ3BeiAUMy0YA8y6uktRNAdRaiLbBhHREEla0hmxC+LdTTW/mKflfEc6sKTUrm2I99dHtoPGgBwwQo2h4fDilml</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";

        public static bool Verify(byte[] data, byte[] signature)
        {
            if (data == null || signature == null || PublicKeyXml.StartsWith("__")) return false;
            CspParameters parameters = new CspParameters { ProviderType = 24 };
            using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider(parameters))
            {
                rsa.PersistKeyInCsp = false;
                rsa.FromXmlString(PublicKeyXml);
                return rsa.VerifyData(data, CryptoConfig.MapNameToOID("SHA256"), signature);
            }
        }

        public static byte[] DecodeSignature(byte[] text)
        {
            string value = Encoding.UTF8.GetString(text ?? new byte[0]).Trim();
            return Convert.FromBase64String(value);
        }
    }

    /// <summary>
    /// 离线优先的更新入口：识别签名目录、校验更新包、暂存载荷，并启动独立副本完成替换。
    /// </summary>
    public sealed class UpdateManager
    {
        public const string ProductId = "WorkMate-Pro";
        public const string CatalogUrl = "https://github.com/TianLin0509/WorkMate-Pro/releases/latest/download/update-catalog.json";
        public const string CatalogSignatureUrl = "https://github.com/TianLin0509/WorkMate-Pro/releases/latest/download/update-catalog.sig";
        private const long MaximumManifestBytes = 256 * 1024;
        private const long MaximumSignatureBytes = 16 * 1024;
        private const long MaximumPayloadBytes = 512L * 1024 * 1024;
        private const long MaximumPackageBytes = 600L * 1024 * 1024;
        private readonly JavaScriptSerializer serializer = new JavaScriptSerializer();
        private readonly object currentHashSync = new object();
        private string cachedCurrentHash;

        public string RootDirectory { get; private set; }
        public string InboxDirectory { get; private set; }
        public string StageRoot { get; private set; }
        public string BackupDirectory { get; private set; }
        public string CurrentExecutable { get { return Path.GetFullPath(Assembly.GetExecutingAssembly().Location); } }
        public string CurrentVersion { get { return Assembly.GetExecutingAssembly().GetName().Version.ToString(3); } }

        public UpdateManager()
        {
            string testRoot = Environment.GetEnvironmentVariable("WORKMATE_TEST_DIR");
            RootDirectory = string.IsNullOrWhiteSpace(testRoot)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WorkMatePro", "Updates")
                : Path.Combine(testRoot, "Updates");
            InboxDirectory = Path.Combine(RootDirectory, "Inbox");
            StageRoot = Path.Combine(RootDirectory, "Staged");
            BackupDirectory = Path.Combine(RootDirectory, "Backups");
            Directory.CreateDirectory(InboxDirectory);
            Directory.CreateDirectory(StageRoot);
            Directory.CreateDirectory(BackupDirectory);
            serializer.MaxJsonLength = 2 * 1024 * 1024;
        }

        public StagedUpdate StagePackage(string packagePath)
        {
            StagedUpdate staged = new StagedUpdate();
            string stageDirectory = null;
            try
            {
                if (string.IsNullOrWhiteSpace(packagePath) || !File.Exists(packagePath))
                    throw new FileNotFoundException("找不到更新包。", packagePath);
                if (new FileInfo(packagePath).Length > MaximumPackageBytes)
                    throw new InvalidDataException("更新包超过 600 MB 安全上限。");
                UpdatePackageManifest manifest;
                byte[] manifestBytes;
                byte[] signatureBytes;
                using (FileStream file = File.Open(packagePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (ZipArchive archive = new ZipArchive(file, ZipArchiveMode.Read, false))
                {
                    ValidateFlatArchive(archive);
                    manifestBytes = ReadEntryBytes(RequireEntry(archive, "manifest.json"), MaximumManifestBytes);
                    signatureBytes = ReadEntryBytes(RequireEntry(archive, "manifest.sig"), MaximumSignatureBytes);
                    if (!UpdateTrust.Verify(manifestBytes, UpdateTrust.DecodeSignature(signatureBytes)))
                        throw new InvalidDataException("更新包签名无效，已拒绝导入。");
                    manifest = serializer.Deserialize<UpdatePackageManifest>(Encoding.UTF8.GetString(manifestBytes));
                    ValidateManifest(manifest, true);

                    ZipArchiveEntry payload = RequireEntry(archive, manifest.PayloadFile);
                    if (payload.Length < 0 || payload.Length > MaximumPayloadBytes)
                        throw new InvalidDataException("更新载荷大小不在允许范围内。");
                    if (manifest.PayloadSize != payload.Length)
                        throw new InvalidDataException("更新载荷长度与签名清单不一致。");

                    string safeVersion = Regex.Replace(manifest.ToVersion, "[^0-9A-Za-z._-]", "-");
                    stageDirectory = Path.Combine(StageRoot, safeVersion + "-" + Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(stageDirectory);
                    string payloadPath = Path.Combine(stageDirectory, manifest.PayloadFile);
                    using (Stream input = payload.Open())
                    using (FileStream output = new FileStream(payloadPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                        input.CopyTo(output);
                    if (!EqualsHash(Sha256File(payloadPath), manifest.PayloadSha256))
                        throw new InvalidDataException("更新载荷 SHA-256 校验失败。");
                    File.WriteAllBytes(Path.Combine(stageDirectory, "manifest.json"), manifestBytes);
                    File.WriteAllBytes(Path.Combine(stageDirectory, "manifest.sig"), signatureBytes);

                    staged.Success = true;
                    staged.StageDirectory = stageDirectory;
                    staged.PayloadPath = payloadPath;
                    staged.Kind = manifest.Kind;
                    staged.FromVersion = manifest.FromVersion;
                    staged.FromSha256 = manifest.FromSha256;
                    staged.ToVersion = manifest.ToVersion;
                    staged.ToSha256 = manifest.ToSha256;
                    staged.Notes = manifest.Notes ?? "";
                    return staged;
                }
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrEmpty(stageDirectory)) TryDeleteOwnedStage(stageDirectory);
                staged.Success = false;
                staged.Error = CleanError(ex);
                return staged;
            }
        }

        public UpdateCheckResult CheckLocal()
        {
            try
            {
                List<string> directories = CandidateDirectories();
                UpdateCheckResult bestCatalog = null;
                int invalidCatalogs = 0;
                bool sawValidCatalog = false;
                foreach (string directory in directories)
                {
                    string catalogPath = Path.Combine(directory, "update-catalog.json");
                    string signaturePath = Path.Combine(directory, "update-catalog.sig");
                    if (!File.Exists(catalogPath) || !File.Exists(signaturePath)) continue;
                    try
                    {
                        UpdateCheckResult candidate = CheckCatalog(
                            File.ReadAllBytes(catalogPath),
                            File.ReadAllBytes(signaturePath),
                            directories);
                        sawValidCatalog = true;
                        if (candidate.Available && (bestCatalog == null || CompareVersions(candidate.LatestVersion, bestCatalog.LatestVersion) > 0))
                            bestCatalog = candidate;
                    }
                    catch { invalidCatalogs++; }
                }
                if (bestCatalog != null) return bestCatalog;

                UpdateCheckResult bestPackage = null;
                foreach (string package in CandidatePackages(directories))
                {
                    UpdatePackageManifest manifest;
                    if (!TryReadPackageManifest(package, out manifest)) continue;
                    if (bestPackage == null || CompareVersions(manifest.ToVersion, bestPackage.LatestVersion) > 0)
                    {
                        bestPackage = new UpdateCheckResult
                        {
                            Success = true,
                            Available = true,
                            LatestVersion = manifest.ToVersion,
                            RecommendedFileName = Path.GetFileName(package),
                            PackagePath = package,
                            Message = "发现可用的本地更新包 v" + manifest.ToVersion + "。"
                        };
                    }
                }
                if (bestPackage != null) return bestPackage;
                if (invalidCatalogs > 0 && !sawValidCatalog)
                    return Failed("发现 " + invalidCatalogs + " 份无效或损坏的本地更新目录，已安全忽略；可删除后重新拷贝目录和签名文件。");
                return NoUpdate("本地未发现比 v" + CurrentVersion + " 更新的签名目录或更新包。");
            }
            catch (Exception ex)
            {
                return Failed("检查本地更新失败：" + CleanError(ex));
            }
        }

        public UpdateCheckResult CheckOnlineCatalog()
        {
            try
            {
                byte[] catalog;
                byte[] signature;
                using (TimeoutWebClient client = new TimeoutWebClient())
                {
                    client.Headers[HttpRequestHeader.UserAgent] = "WorkMate-Pro/" + CurrentVersion;
                    catalog = client.DownloadData(CatalogUrl);
                    signature = client.DownloadData(CatalogSignatureUrl);
                }
                return CheckCatalog(catalog, signature, CandidateDirectories());
            }
            catch (Exception ex)
            {
                return Failed("无法访问 GitHub 更新目录；公司网络受限时请使用“检查本地更新”或直接导入更新包。详情：" + CleanError(ex));
            }
        }

        public bool MarkNotificationIfNew(UpdateCheckResult result)
        {
            if (result == null || !result.Success || !result.Available || string.IsNullOrWhiteSpace(result.LatestVersion)) return false;
            string path = Path.Combine(RootDirectory, "last-notified-version.txt");
            try
            {
                string previous = File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8).Trim() : "";
                if (SameVersion(previous, result.LatestVersion)) return false;
                File.WriteAllText(path, result.LatestVersion, new UTF8Encoding(false));
                return true;
            }
            catch { return true; }
        }

        public void LaunchStagedUpdate(StagedUpdate staged)
        {
            if (staged == null || !staged.Success) throw new InvalidOperationException("更新尚未通过校验。");
            if (!File.Exists(staged.PayloadPath)) throw new FileNotFoundException("暂存的更新载荷不存在。", staged.PayloadPath);
            string currentHash = Sha256File(CurrentExecutable);
            if (staged.Kind == "delta" && !EqualsHash(currentHash, staged.FromSha256))
                throw new InvalidDataException("当前程序在导入后发生了变化，请重新导入匹配版本的更新包。");

            string updaterPath = Path.Combine(staged.StageDirectory, "WorkMateUpdater-" + Guid.NewGuid().ToString("N") + ".exe");
            File.Copy(CurrentExecutable, updaterPath, false);
            string markerPath = Path.Combine(staged.StageDirectory, "health.ok");
            string resultPath = Path.Combine(RootDirectory, "last-update.log");
            string backupPath = Path.Combine(
                BackupDirectory,
                "WorkMate-" + CurrentVersion + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".exe");
            ProcessStartInfo start = new ProcessStartInfo
            {
                FileName = updaterPath,
                WorkingDirectory = staged.StageDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                Arguments = "--apply-update"
                    + Named("--parent-pid", Process.GetCurrentProcess().Id.ToString())
                    + Named("--target", CurrentExecutable)
                    + Named("--payload", staged.PayloadPath)
                    + Named("--kind", staged.Kind)
                    + Named("--source-hash", currentHash)
                    + Named("--target-hash", staged.ToSha256)
                    + Named("--target-version", staged.ToVersion)
                    + Named("--backup", backupPath)
                    + Named("--marker", markerPath)
                    + Named("--result", resultPath)
            };
            Process process = Process.Start(start);
            if (process == null) throw new InvalidOperationException("无法启动独立更新器。");
        }

        public static string Sha256File(string path)
        {
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (SHA256 sha = SHA256.Create())
                return Hex(sha.ComputeHash(stream));
        }

        public static string ThreePartFileVersion(string path)
        {
            string raw = FileVersionInfo.GetVersionInfo(path).FileVersion;
            Version parsed;
            if (!Version.TryParse(raw, out parsed)) throw new InvalidDataException("EXE 文件版本无效：" + raw);
            return parsed.Major + "." + parsed.Minor + "." + parsed.Build;
        }

        public static bool VersionsEqual(string left, string right)
        {
            return SameVersion(left, right);
        }

        public static string Hex(byte[] bytes)
        {
            StringBuilder builder = new StringBuilder(bytes.Length * 2);
            foreach (byte value in bytes) builder.Append(value.ToString("X2"));
            return builder.ToString();
        }

        public static bool EqualsHash(string left, string right)
        {
            return !string.IsNullOrWhiteSpace(left)
                && !string.IsNullOrWhiteSpace(right)
                && string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private UpdateCheckResult CheckCatalog(byte[] catalogBytes, byte[] signatureText, List<string> directories)
        {
            if (catalogBytes == null || catalogBytes.LongLength > MaximumManifestBytes)
                throw new InvalidDataException("更新目录大小异常。");
            if (signatureText == null || signatureText.LongLength > MaximumSignatureBytes)
                throw new InvalidDataException("更新目录签名大小异常。");
            if (!UpdateTrust.Verify(catalogBytes, UpdateTrust.DecodeSignature(signatureText)))
                throw new InvalidDataException("更新目录签名无效。");
            UpdateCatalog catalog = serializer.Deserialize<UpdateCatalog>(Encoding.UTF8.GetString(catalogBytes));
            if (catalog == null || catalog.SchemaVersion != 1 || catalog.ProductId != ProductId)
                throw new InvalidDataException("更新目录格式或产品标识无效。");
            if (CompareVersions(catalog.LatestVersion, CurrentVersion) <= 0)
                return NoUpdate("当前已是最新版本 v" + CurrentVersion + "。");

            string currentHash = CurrentSha256();
            List<UpdateCatalogPackage> packages = catalog.Packages ?? new List<UpdateCatalogPackage>();
            UpdateCatalogPackage selected = packages.FirstOrDefault(delegate(UpdateCatalogPackage item)
            {
                return item != null && item.Kind == "delta"
                    && SameVersion(item.FromVersion, CurrentVersion)
                    && EqualsHash(item.FromSha256, currentHash);
            });
            if (selected == null) selected = packages.FirstOrDefault(delegate(UpdateCatalogPackage item) { return item != null && item.Kind == "full"; });
            if (selected == null) throw new InvalidDataException("更新目录没有适用于当前版本的差分包或全量兜底包。");
            ValidateSafeFileName(selected.FileName, "更新包文件名");
            if (!IsHash(selected.Sha256)) throw new InvalidDataException("更新目录中的更新包 SHA-256 无效。");
            if (selected.SizeBytes <= 0 || selected.SizeBytes > MaximumPackageBytes)
                throw new InvalidDataException("更新目录中的更新包大小无效。");
            Uri packageUri;
            if (!string.IsNullOrWhiteSpace(selected.Url)
                && (!Uri.TryCreate(selected.Url, UriKind.Absolute, out packageUri) || packageUri.Scheme != Uri.UriSchemeHttps))
                throw new InvalidDataException("更新目录中的下载地址不是有效的 HTTPS URL。");
            string localPath = FindFile(directories, selected.FileName);
            if (localPath != null)
            {
                if (new FileInfo(localPath).Length != selected.SizeBytes)
                    throw new InvalidDataException("本地更新包与签名目录中的文件大小不一致：" + selected.FileName);
                if (!EqualsHash(Sha256File(localPath), selected.Sha256))
                    throw new InvalidDataException("本地更新包与签名目录中的 SHA-256 不一致：" + selected.FileName);
            }
            string size = selected.SizeBytes > 0 ? FormatBytes(selected.SizeBytes) : "未知大小";
            return new UpdateCheckResult
            {
                Success = true,
                Available = true,
                LatestVersion = catalog.LatestVersion,
                RecommendedFileName = selected.FileName,
                PackagePath = localPath,
                DownloadUrl = selected.Url,
                ReleaseUrl = catalog.ReleaseUrl,
                Message = (selected.Kind == "delta" ? "可用差分更新" : "当前版本只能使用全量更新")
                    + "：v" + CurrentVersion + " → v" + catalog.LatestVersion + "（" + size + "）。"
                    + (localPath == null ? " 请在可联网电脑下载该文件并拷贝到更新收件箱。" : " 本地文件已就绪，可直接导入。")
            };
        }

        private bool TryReadPackageManifest(string packagePath, out UpdatePackageManifest manifest)
        {
            manifest = null;
            try
            {
                if (new FileInfo(packagePath).Length > MaximumPackageBytes) return false;
                using (FileStream file = File.Open(packagePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (ZipArchive archive = new ZipArchive(file, ZipArchiveMode.Read, false))
                {
                    ValidateFlatArchive(archive);
                    byte[] manifestBytes = ReadEntryBytes(RequireEntry(archive, "manifest.json"), MaximumManifestBytes);
                    byte[] signatureBytes = ReadEntryBytes(RequireEntry(archive, "manifest.sig"), MaximumSignatureBytes);
                    if (!UpdateTrust.Verify(manifestBytes, UpdateTrust.DecodeSignature(signatureBytes))) return false;
                    manifest = serializer.Deserialize<UpdatePackageManifest>(Encoding.UTF8.GetString(manifestBytes));
                    ValidateManifest(manifest, false);
                    return true;
                }
            }
            catch { manifest = null; return false; }
        }

        private void ValidateManifest(UpdatePackageManifest manifest, bool requirePayload)
        {
            if (manifest == null || manifest.SchemaVersion != 1 || manifest.ProductId != ProductId)
                throw new InvalidDataException("更新包格式或产品标识无效。");
            if (manifest.Kind != "delta" && manifest.Kind != "full")
                throw new InvalidDataException("不支持的更新包类型。");
            ValidateSafeFileName(manifest.PayloadFile, "更新载荷文件名");
            if (!IsHash(manifest.ToSha256) || !IsHash(manifest.PayloadSha256))
                throw new InvalidDataException("更新包缺少有效的 SHA-256。");
            if (manifest.PayloadSize < 0 || manifest.PayloadSize > MaximumPayloadBytes)
                throw new InvalidDataException("更新载荷长度无效。");
            if (CompareVersions(manifest.ToVersion, CurrentVersion) <= 0)
                throw new InvalidDataException("该更新包不是比当前 v" + CurrentVersion + " 更新的版本。");
            if (manifest.Kind == "delta")
            {
                if (!SameVersion(manifest.FromVersion, CurrentVersion))
                    throw new InvalidDataException("差分包适用于 v" + manifest.FromVersion + "，当前是 v" + CurrentVersion + "。");
                if (!IsHash(manifest.FromSha256) || !EqualsHash(CurrentSha256(), manifest.FromSha256))
                    throw new InvalidDataException("当前 EXE 与差分包基线哈希不一致；请下载精确匹配的差分包或全量包。");
            }
            if (requirePayload && manifest.PayloadSize == 0)
                throw new InvalidDataException("更新载荷为空。");
        }

        private List<string> CandidateDirectories()
        {
            List<string> result = new List<string>();
            AddDirectory(result, InboxDirectory);
            AddDirectory(result, Path.GetDirectoryName(CurrentExecutable));
            AddDirectory(result, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"));
            return result;
        }

        private string CurrentSha256()
        {
            lock (currentHashSync)
            {
                if (string.IsNullOrEmpty(cachedCurrentHash)) cachedCurrentHash = Sha256File(CurrentExecutable);
                return cachedCurrentHash;
            }
        }

        private static void AddDirectory(List<string> result, string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return;
            string full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
            if (!result.Any(delegate(string item) { return string.Equals(item, full, StringComparison.OrdinalIgnoreCase); })) result.Add(full);
        }

        private static IEnumerable<string> CandidatePackages(List<string> directories)
        {
            List<FileInfo> files = new List<FileInfo>();
            foreach (string directory in directories)
            {
                try
                {
                    files.AddRange(Directory.GetFiles(directory, "*.workmate-update.zip", SearchOption.TopDirectoryOnly)
                        .Select(delegate(string path) { return new FileInfo(path); }));
                }
                catch { }
            }
            return files.OrderByDescending(delegate(FileInfo file) { return file.LastWriteTimeUtc; })
                .Take(30)
                .Select(delegate(FileInfo file) { return file.FullName; });
        }

        private static string FindFile(List<string> directories, string fileName)
        {
            foreach (string directory in directories)
            {
                string candidate = Path.Combine(directory, fileName);
                if (File.Exists(candidate)) return candidate;
            }
            return null;
        }

        private static void ValidateFlatArchive(ZipArchive archive)
        {
            if (archive.Entries.Count < 3 || archive.Entries.Count > 8)
                throw new InvalidDataException("更新包条目数量异常。");
            HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name) || entry.FullName != entry.Name)
                    throw new InvalidDataException("更新包只允许根目录文件，已拒绝可疑路径。");
                if (!names.Add(entry.Name)) throw new InvalidDataException("更新包包含重复文件名。");
            }
        }

        private static ZipArchiveEntry RequireEntry(ZipArchive archive, string name)
        {
            ZipArchiveEntry entry = archive.GetEntry(name);
            if (entry == null) throw new InvalidDataException("更新包缺少文件：" + name);
            return entry;
        }

        private static byte[] ReadEntryBytes(ZipArchiveEntry entry, long maximum)
        {
            if (entry.Length < 0 || entry.Length > maximum) throw new InvalidDataException("更新包条目大小异常：" + entry.Name);
            using (Stream stream = entry.Open())
            using (MemoryStream buffer = new MemoryStream())
            {
                stream.CopyTo(buffer);
                if (buffer.Length > maximum) throw new InvalidDataException("更新包条目解压后过大：" + entry.Name);
                return buffer.ToArray();
            }
        }

        private void TryDeleteOwnedStage(string path)
        {
            try
            {
                string root = Path.GetFullPath(StageRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                string target = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (target.StartsWith(root, StringComparison.OrdinalIgnoreCase) && Directory.Exists(path)) Directory.Delete(path, true);
            }
            catch { }
        }

        private static void ValidateSafeFileName(string value, string label)
        {
            if (string.IsNullOrWhiteSpace(value) || value != Path.GetFileName(value)
                || !Regex.IsMatch(value, "^[A-Za-z0-9._-]{1,160}$"))
                throw new InvalidDataException(label + "无效。");
        }

        private static bool IsHash(string value)
        {
            return !string.IsNullOrWhiteSpace(value) && Regex.IsMatch(value.Trim(), "^[0-9A-Fa-f]{64}$");
        }

        private static int CompareVersions(string left, string right)
        {
            Version a;
            Version b;
            if (!Version.TryParse(left, out a)) throw new InvalidDataException("版本号无效：" + left);
            if (!Version.TryParse(right, out b)) throw new InvalidDataException("版本号无效：" + right);
            return a.CompareTo(b);
        }

        private static bool SameVersion(string left, string right)
        {
            try { return CompareVersions(left, right) == 0; }
            catch { return false; }
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes >= 1024L * 1024) return (bytes / (1024.0 * 1024.0)).ToString("0.0") + " MB";
            if (bytes >= 1024) return (bytes / 1024.0).ToString("0.0") + " KB";
            return bytes + " B";
        }

        private static UpdateCheckResult NoUpdate(string message)
        {
            return new UpdateCheckResult { Success = true, Available = false, Message = message };
        }

        private static UpdateCheckResult Failed(string message)
        {
            return new UpdateCheckResult { Success = false, Available = false, Message = message };
        }

        private static string Named(string name, string value)
        {
            return " " + name + " " + Quote(value);
        }

        private static string Quote(string value)
        {
            return "\"" + (value ?? "").Replace("\"", "\\\"") + "\"";
        }

        private static string CleanError(Exception ex)
        {
            string value = ex == null ? "未知错误" : ex.Message;
            value = (value ?? "未知错误").Replace("\r", " ").Replace("\n", " ").Trim();
            return value.Length <= 300 ? value : value.Substring(0, 300) + "…";
        }
    }

    /// <summary>在原进程退出后执行原子替换；若新版本未写入健康标记则恢复备份。</summary>
    public static class UpdateApplier
    {
        public static int Run(string[] args)
        {
            Dictionary<string, string> values;
            try
            {
                values = Parse(args);
                int parentPid = int.Parse(Required(values, "--parent-pid"));
                string target = Path.GetFullPath(Required(values, "--target"));
                string payload = Path.GetFullPath(Required(values, "--payload"));
                string kind = Required(values, "--kind");
                string sourceHash = Required(values, "--source-hash");
                string targetHash = Required(values, "--target-hash");
                string targetVersion = Required(values, "--target-version");
                string backup = Path.GetFullPath(Required(values, "--backup"));
                string marker = Path.GetFullPath(Required(values, "--marker"));
                string result = Path.GetFullPath(Required(values, "--result"));
                WaitForParent(parentPid);
                Directory.CreateDirectory(Path.GetDirectoryName(backup));
                Directory.CreateDirectory(Path.GetDirectoryName(result));
                TryDeleteFile(marker);

                if (!File.Exists(target) || !UpdateManager.EqualsHash(UpdateManager.Sha256File(target), sourceHash))
                    throw new InvalidDataException("替换前的源程序哈希不匹配，更新已取消。");
                if (!File.Exists(payload)) throw new FileNotFoundException("更新载荷不存在。", payload);

                string targetDirectory = Path.GetDirectoryName(target);
                string temporary = Path.Combine(targetDirectory, ".WorkMate-update-" + Guid.NewGuid().ToString("N") + ".tmp");
                string atomicBackup = Path.Combine(targetDirectory, ".WorkMate-backup-" + Guid.NewGuid().ToString("N") + ".exe");
                try
                {
                    if (kind == "delta") MsDeltaCodec.ApplyFile(target, payload, temporary);
                    else if (kind == "full") File.Copy(payload, temporary, false);
                    else throw new InvalidDataException("不支持的更新包类型。");
                    if (!UpdateManager.EqualsHash(UpdateManager.Sha256File(temporary), targetHash))
                        throw new InvalidDataException("生成的新程序 SHA-256 不匹配，更新已取消。");
                    if (Environment.GetEnvironmentVariable("WORKMATE_UPDATE_E2E") != "1"
                        && !UpdateManager.VersionsEqual(UpdateManager.ThreePartFileVersion(temporary), targetVersion))
                        throw new InvalidDataException("目标 EXE 的文件版本与签名清单不一致。");
                    File.Replace(temporary, target, atomicBackup, true);
                }
                finally { TryDeleteFile(temporary); }

                Process child = null;
                try
                {
                    child = Process.Start(new ProcessStartInfo
                    {
                        FileName = target,
                        WorkingDirectory = targetDirectory,
                        UseShellExecute = true,
                        Arguments = "--post-update-marker " + Quote(marker) + " --post-update-version " + Quote(targetVersion)
                            + (Environment.GetEnvironmentVariable("WORKMATE_UPDATE_E2E") == "1" ? " --update-health-exit" : "")
                    });
                    if (child == null) throw new InvalidOperationException("新版本未能启动。");
                    if (!WaitForMarker(marker, child, TimeSpan.FromSeconds(35)))
                        throw new InvalidOperationException("新版本未在 35 秒内通过启动健康检查。");
                    PersistBackup(atomicBackup, backup);
                    PruneBackups(Path.GetDirectoryName(backup), 3);
                    WriteResult(result, "SUCCESS v" + targetVersion + " " + DateTime.UtcNow.ToString("o"));
                    return 0;
                }
                catch (Exception healthError)
                {
                    if (child != null && !child.HasExited)
                    {
                        try { child.Kill(); child.WaitForExit(5000); } catch { }
                    }
                    string failed = target + ".failed-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N");
                    if (!File.Exists(atomicBackup)) throw;
                    try { File.Replace(atomicBackup, target, failed, true); }
                    catch
                    {
                        File.Copy(atomicBackup, target, true);
                        if (!UpdateManager.EqualsHash(UpdateManager.Sha256File(target), sourceHash)) throw;
                        TryDeleteFile(atomicBackup);
                    }
                    if (!UpdateManager.EqualsHash(UpdateManager.Sha256File(target), sourceHash))
                        throw new InvalidDataException("回滚后的源程序 SHA-256 不匹配。");
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = target,
                        WorkingDirectory = targetDirectory,
                        UseShellExecute = true,
                        Arguments = "--update-rollback " + Quote(targetVersion)
                            + (Environment.GetEnvironmentVariable("WORKMATE_UPDATE_E2E") == "1" ? " --update-health-exit" : "")
                    });
                    WriteResult(result, "ROLLED_BACK v" + targetVersion + " " + healthError.Message);
                    return 5;
                }
            }
            catch (Exception ex)
            {
                try
                {
                    string resultPath;
                    if (TryArgument(args, "--result", out resultPath)) WriteResult(Path.GetFullPath(resultPath), "FAILED " + ex);
                }
                catch { }
                return 4;
            }
        }

        public static void MarkHealthy(string[] args)
        {
            if (Environment.GetEnvironmentVariable("WORKMATE_UPDATE_FORCE_HEALTH_FAIL") == "1") return;
            string marker;
            if (!TryArgument(args, "--post-update-marker", out marker) || string.IsNullOrWhiteSpace(marker)) return;
            try
            {
                marker = Path.GetFullPath(marker);
                Directory.CreateDirectory(Path.GetDirectoryName(marker));
                File.WriteAllText(marker, "healthy " + Process.GetCurrentProcess().Id + " " + DateTime.UtcNow.ToString("o"), new UTF8Encoding(false));
            }
            catch { }
        }

        public static bool TryArgument(string[] args, string name, out string value)
        {
            value = null;
            if (args == null) return false;
            for (int i = 0; i + 1 < args.Length; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    value = args[i + 1];
                    return true;
                }
            }
            return false;
        }

        private static Dictionary<string, string> Parse(string[] args)
        {
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 1; args != null && i < args.Length; i++)
            {
                string name = args[i];
                if (!name.StartsWith("--") || i + 1 >= args.Length) continue;
                values[name] = args[++i];
            }
            return values;
        }

        private static string Required(Dictionary<string, string> values, string name)
        {
            string value;
            if (!values.TryGetValue(name, out value) || string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("缺少更新参数：" + name);
            return value;
        }

        private static void WaitForParent(int processId)
        {
            if (processId <= 0) return;
            try
            {
                using (Process process = Process.GetProcessById(processId))
                {
                    if (!process.WaitForExit(45000)) throw new TimeoutException("等待旧版本退出超时，未执行替换。");
                }
            }
            catch (ArgumentException) { }
        }

        private static bool WaitForMarker(string marker, Process child, TimeSpan timeout)
        {
            Stopwatch watch = Stopwatch.StartNew();
            while (watch.Elapsed < timeout)
            {
                if (File.Exists(marker)) return true;
                if (child.HasExited) return false;
                Thread.Sleep(200);
            }
            return File.Exists(marker);
        }

        private static void WriteResult(string path, string value)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, value + Environment.NewLine, new UTF8Encoding(false));
        }

        private static void PersistBackup(string atomicBackup, string durableBackup)
        {
            if (!File.Exists(atomicBackup)) return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(durableBackup));
                File.Copy(atomicBackup, durableBackup, true);
                TryDeleteFile(atomicBackup);
            }
            catch
            {
                // Keep the same-volume backup beside the target if archival copy fails.
            }
        }

        private static void PruneBackups(string directory, int keep)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) return;
                FileInfo[] files = new DirectoryInfo(directory).GetFiles("WorkMate-*.exe", SearchOption.TopDirectoryOnly)
                    .OrderByDescending(delegate(FileInfo file) { return file.LastWriteTimeUtc; })
                    .ToArray();
                for (int i = Math.Max(keep, 0); i < files.Length; i++) files[i].Delete();
            }
            catch { }
        }

        private static void TryDeleteFile(string path)
        {
            try { if (!string.IsNullOrWhiteSpace(path) && File.Exists(path)) File.Delete(path); } catch { }
        }

        private static string Quote(string value)
        {
            return "\"" + (value ?? "").Replace("\"", "\\\"") + "\"";
        }
    }
}
