using System;
using System.IO;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace WorkMatePro
{
    /// <summary>
    /// 技术员工事件桥：让构建/测试脚本显式告诉小满发生了什么。
    /// 接入成本只有一个 shell 别名，不需要任何 SDK：
    ///   WorkMate.exe tell build-start / build-ok / build-fail / deploy-ok / dance-demo / hula-demo / farewell-demo / say 文本
    /// 第二个实例只负责把消息写进命名管道就退出。
    /// </summary>
    public sealed class EventBridge : IDisposable
    {
        public static string PipeName
        {
            get
            {
                string testRoot = Environment.GetEnvironmentVariable("WORKMATE_TEST_DIR");
                if (string.IsNullOrWhiteSpace(testRoot)) return "WorkMatePro.Events";
                using (SHA256 sha = SHA256.Create())
                {
                    byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(testRoot.Trim().ToLowerInvariant()));
                    StringBuilder suffix = new StringBuilder(16);
                    for (int index = 0; index < 8; index++) suffix.Append(hash[index].ToString("x2"));
                    return "WorkMatePro.Events.Test." + suffix;
                }
            }
        }
        public const string ActivateWorkbench = "__activate_workbench__";
        public const string ActivateQuickCapture = "__activate_quick__";
        private readonly Action<string> onEvent;
        private Thread thread;
        private volatile bool stopping;

        public EventBridge(Action<string> onEventMessage)
        {
            onEvent = onEventMessage;
        }

        public void Start()
        {
            thread = new Thread(ListenLoop);
            thread.IsBackground = true;
            thread.Name = "WorkMatePro.EventBridge";
            thread.Start();
        }

        private void ListenLoop()
        {
            while (!stopping)
            {
                NamedPipeServerStream server = null;
                try
                {
                    server = new NamedPipeServerStream(PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.None);
                    server.WaitForConnection();
                    using (StreamReader reader = new StreamReader(server, new UTF8Encoding(false)))
                    {
                        string line = reader.ReadLine();
                        server = null; // reader 已接管
                        if (!string.IsNullOrWhiteSpace(line)) onEvent(line.Trim());
                    }
                }
                catch
                {
                    Thread.Sleep(400);
                }
                finally
                {
                    try { if (server != null) server.Dispose(); } catch { }
                }
            }
        }

        /// <summary>CLI 侧：把一条消息发给正在运行的桌宠。成功 true。</summary>
        public static bool TrySend(string message)
        {
            try
            {
                using (NamedPipeClientStream client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out))
                {
                    client.Connect(600);
                    using (StreamWriter writer = new StreamWriter(client, new UTF8Encoding(false)))
                    {
                        writer.AutoFlush = true;
                        writer.WriteLine(message);
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>启动竞争时管道可能尚未进入 WaitForConnection；短暂重试避免“首个实例刚启动，第二次双击仍无响应”。</summary>
        public static bool TrySendWithRetry(string message, int attempts, int delayMs)
        {
            attempts = Math.Max(1, attempts);
            for (int i = 0; i < attempts; i++)
            {
                if (TrySend(message)) return true;
                if (i + 1 < attempts) Thread.Sleep(Math.Max(0, delayMs));
            }
            return false;
        }

        public static string ActivationMessageFor(string[] args)
        {
            if (args != null && Array.IndexOf(args, "--quick") >= 0) return ActivateQuickCapture;
            return ActivateWorkbench;
        }

        public void Dispose()
        {
            stopping = true;
            // 用一次自连把阻塞中的 WaitForConnection 唤醒，让线程退出
            try { TrySend("__bye__"); } catch { }
        }
    }
}
