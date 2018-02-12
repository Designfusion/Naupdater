using Fclp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Naupdater
{
    static class Program
    {
        public static AppArguments Args; // 运行参数

        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        static void Main(string[] programArgs)
        {
            // 检查系统版本
            if (!Utils.IsWinVistaOrHigher())
            {
                MessageBox.Show("不支持的操作系统版本，最低需求为 Windows Vista",
                "Naupdater 无法运行", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // 检测 .NET Framework 版本
            if (!Utils.IsSupportedRuntimeVersion())
            {
                MessageBox.Show("当前 .NET Framework 版本过低，请升级至 4.6.2 或更新版本",
                "Naupdater 无法运行", MessageBoxButtons.OK, MessageBoxIcon.Error);

                Process.Start(
                    "http://dotnetsocial.cloudapp.net/GetDotnet?tfm=.NETFramework,Version=v4.6.2");
                return;
            }

            Utils.ReleaseMemory(true);
            using (Mutex mutex = new Mutex(false, $"Global\\Naupdater_{Application.StartupPath.GetHashCode()}"))
            {
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += Application_ThreadException;
                AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                if (!mutex.WaitOne(0, false))
                {
                    Process[] oldProcesses = Process.GetProcessesByName("Naupdater");
                    if (oldProcesses.Length > 0)
                    {
                        Process oldProcess = oldProcesses[0];
                    }
                    MessageBox.Show($"Naupdater 正在运行中... {Environment.NewLine}无需重复打开");
                    return;
                }

                Directory.SetCurrentDirectory(Application.StartupPath);

                // 启动参数
                ArgsHandle(programArgs);

                // 启动主界面
                Application.Run(new UpdaterForm());
            }
        }

        /// <summary>
        /// 启动参数处理
        /// </summary>
        /// <param name="programArgs"></param>
        private static void ArgsHandle(string[] programArgs)
        {
            // 程序启动参数
            var p = new FluentCommandLineParser<AppArguments>();

            Program.Args = p.Object;

            p.Setup(args => args.TargetAppName)
                .As("name")
                .SetDefault(Utils.GetAppConfig("TargetAppName", ""))
                .WithDescription("目标程序名（用于显示）");

            p.Setup(args => args.TargetAppProcessName)
                .As("process")
                .SetDefault(Utils.GetAppConfig("TargetAppProcessName", ""))
                .WithDescription("目标程序进程名（用于强制关闭应用）");

            p.Setup(args => args.TargetAppRootPath)
                .As("root-path")
                .SetDefault(Utils.GetAppConfig("TargetAppRootPath", "./"))
                .WithDescription("目标程序根目录（文件操作将在该目录中进行）");

            p.Setup(args => args.SrcDownloadUrl)
                .As('u', "url")
                .WithDescription("远程下载升级资源文件地址（在线升级）");

            p.Setup(args => args.SrcDownloadProxy)
                .As('p', "proxy")
                .SetDefault(Utils.GetAppConfig("DownloadProxy", ""))
                .WithDescription("远程下载代理");

            p.Setup(args => args.SrcFileMD5)
                .As('v', "md5")
                .WithDescription("下载升级资源文件 MD5 值");

            p.Setup(args => args.SrcLocalFileName)
                .As("local-src")
                .SetDefault("")
                .WithDescription("本地升级资源文件（离线升级）");

            p.Setup(args => args.LauchAppFileName)
                .As("lauch")
                .SetDefault(Utils.GetAppConfig("LauchAppFileName", ""))
                .WithDescription("升级完成后启动的文件");

            p.Setup(args => args.LauchAppFileArgs)
                .As("lauch-args")
                .SetDefault(Utils.GetAppConfig("LauchAppFileArgs", ""))
                .WithDescription("升级完成后启动的文件");
            
            p.Setup(args => args.UpdateMode)
                .As('m', "mode")
                .SetDefault(UpdateMode.OverwriteFiles)
                .WithDescription("升级模式");

            p.SetupHelp("?", "help").Callback((text) => {
                MessageBox.Show(text, "Naupdater 启动参数", MessageBoxButtons.OK);
                ExitApp();
            });
            
            if (p.Parse(programArgs).HasErrors)
                ReportErrorAndExit("启动参数有误");

            LauchErrorText = Utils.GetAppConfig("LauchErrorText", "程序无法启动，请尝试重装");

            // No Args
            if (string.IsNullOrWhiteSpace(Args.SrcDownloadUrl) && string.IsNullOrWhiteSpace(Args.SrcLocalFileName))
            {
                string filename = Utils.GetAppConfig("NoArgsLauchFile", "");
                if (!String.IsNullOrWhiteSpace(filename))
                    Program.LauchProgram(filename, Utils.GetAppConfig("NoArgsLauchFileArgs", ""));

                string msg = Utils.GetAppConfig("NoArgsMsgText", "");
                if (!String.IsNullOrWhiteSpace(msg))
                    MessageBox.Show(Utils.GetAppConfig("NoArgsMsgText", "请指定启动参数"), $"{Args.TargetAppName} 升级程序", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ExitApp();
            }

            // Path Handle
            Args.TargetAppRootPath = Utils.GetPathBasedOn(Application.StartupPath, Args.TargetAppRootPath);
        }

        public static string LauchErrorText; // 启动程序错误文字

        /// <summary>
        /// 启动程序
        /// </summary>
        /// <param name="AppFilePath">目标程序文件路径</param>
        public static void LauchProgram(string fileName, string args = null)
        {
            try
            {
                Process.Start(fileName, args);
            }
            catch (Exception e)
            {
                Program.ReportErrorAndExit($"{LauchErrorText.Replace("{ExceptionMessage}", e.Message)}");
            }
        }

        /// <summary>
        /// 错误处理
        /// </summary>
        private static int exited = 0;

        private static void Application_ThreadException(object sender, ThreadExceptionEventArgs e)
        {
            ReportErrorAndExit($"Naupdater UI Error{Environment.NewLine}{e.Exception.ToString()}");
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            ReportErrorAndExit($"Naupdater non-UI Error{Environment.NewLine}{e.ExceptionObject.ToString()}");
        }

        public static void ReportErrorAndExit(string error)
        {
            if (Interlocked.Increment(ref exited) == 1)
            {
                string appName = !String.IsNullOrWhiteSpace(Args.TargetAppName) ? Args.TargetAppName + " " : "";

                MessageBox.Show($"{error}", $"{appName} 升级失败", MessageBoxButtons.OK, MessageBoxIcon.Error);

                ExitApp();
            }
        }

        public static void ExitApp()
        {
            Application.Exit();
            Environment.Exit(Environment.ExitCode);
        }
    }
}
