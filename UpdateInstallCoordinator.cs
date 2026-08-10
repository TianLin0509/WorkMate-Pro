using System;
using System.IO;
using System.Threading;
using System.Windows;

namespace WorkMatePro
{
    /// <summary>
    /// 桌宠拖放与能力中心共用的单一安装入口：后台验签和暂存，前台只确认一次，
    /// 随后交给独立更新器完成退出、原子替换、健康检查与回滚。
    /// </summary>
    public sealed class UpdateInstallCoordinator
    {
        private readonly WorkMateApp app;
        private readonly UpdateManager updates;
        private int importInProgress;

        public UpdateInstallCoordinator(WorkMateApp app, UpdateManager updates)
        {
            this.app = app;
            this.updates = updates;
        }

        public bool IsBusy { get { return Volatile.Read(ref importInProgress) != 0; } }

        public void BeginImport(string packagePath, Window owner, Action<string> report)
        {
            BeginImportCore(packagePath, owner, report, false);
        }

        internal void BeginImportForE2E(string packagePath, Window owner, Action<string> report)
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WORKMATE_TEST_DIR"))
                || Environment.GetEnvironmentVariable("WORKMATE_UPDATE_E2E") != "1")
                throw new InvalidOperationException("更新拖放 E2E 只能在隔离测试目录中运行。");
            BeginImportCore(packagePath, owner, report, true);
        }

        private void BeginImportCore(string packagePath, Window owner, Action<string> report, bool autoConfirmForE2E)
        {
            if (string.IsNullOrWhiteSpace(packagePath) || !File.Exists(packagePath))
            {
                Report(report, "找不到拖入的更新包。");
                return;
            }
            if (Interlocked.CompareExchange(ref importInProgress, 1, 0) != 0)
            {
                Report(report, "正在验证另一个更新包，请稍候。");
                return;
            }

            Report(report, "正在验证更新包签名、版本基线和 SHA-256…");
            try
            {
                bool queued = ThreadPool.QueueUserWorkItem(delegate
                {
                    StagedUpdate staged = updates.StagePackage(packagePath);
                    try
                    {
                        app.Dispatcher.BeginInvoke(new Action(delegate
                        {
                            CompleteImport(staged, owner, report, autoConfirmForE2E);
                        }));
                    }
                    catch
                    {
                        updates.DiscardStagedUpdate(staged);
                        Interlocked.Exchange(ref importInProgress, 0);
                    }
                });
                if (!queued)
                {
                    Interlocked.Exchange(ref importInProgress, 0);
                    Report(report, "系统繁忙，无法排队验证更新包，请稍后重试。");
                }
            }
            catch (Exception ex)
            {
                Interlocked.Exchange(ref importInProgress, 0);
                Report(report, "无法启动更新验证：" + CleanError(ex));
            }
        }

        private void CompleteImport(StagedUpdate staged, Window owner, Action<string> report, bool autoConfirmForE2E)
        {
            bool updaterStarted = false;
            try
            {
                if (staged == null || !staged.Success)
                {
                    Report(report, "更新包已拒绝：" + (staged == null ? "没有返回验证结果。" : staged.Error));
                    return;
                }

                string kind = staged.Kind == "delta" ? "差分更新" : "全量兜底更新";
                string transition = staged.Kind == "delta"
                    ? "v" + staged.FromVersion + " → v" + staged.ToVersion
                    : "安装 v" + staged.ToVersion + "（全量包，不要求旧版哈希）";
                string message = kind + "已通过签名与哈希校验。\n\n"
                    + transition
                    + (string.IsNullOrWhiteSpace(staged.Notes) ? "" : "\n\n" + staged.Notes)
                    + "\n\nWorkMate 将正常退出并自动重启；若新版启动失败会恢复旧版。现在更新吗？";
                MessageBoxResult choice = autoConfirmForE2E
                    ? MessageBoxResult.Yes
                    : owner != null && owner.IsVisible
                        ? MessageBox.Show(owner, message, "立即更新 WorkMate", MessageBoxButton.YesNo, MessageBoxImage.Question)
                        : MessageBox.Show(message, "立即更新 WorkMate", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (choice != MessageBoxResult.Yes)
                {
                    updates.DiscardStagedUpdate(staged);
                    Report(report, "已取消更新，临时文件已清理。");
                    return;
                }

                updates.LaunchStagedUpdate(staged);
                updaterStarted = true;
                Report(report, "更新器已启动，WorkMate 即将自动重启。");
                app.ExitApp();
            }
            catch (Exception ex)
            {
                if (!updaterStarted) updates.DiscardStagedUpdate(staged);
                Report(report, "无法启动更新：" + CleanError(ex));
            }
            finally
            {
                Interlocked.Exchange(ref importInProgress, 0);
            }
        }

        private static void Report(Action<string> report, string message)
        {
            if (report == null) return;
            try { report(message); }
            catch { }
        }

        private static string CleanError(Exception ex)
        {
            string value = ex == null ? "未知错误" : ex.Message;
            value = (value ?? "未知错误").Replace("\r", " ").Replace("\n", " ").Trim();
            return value.Length <= 180 ? value : value.Substring(0, 180) + "…";
        }
    }
}
