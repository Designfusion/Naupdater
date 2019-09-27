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
        public static AppArguments Args; // Operating parameters

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] programArgs)
        {
            // Check system version
            if (!Utils.IsWinVistaOrHigher())
            {
                MessageBox.Show("Unsupported operating system version, the minimum requirement is Windows Vista",
                "Naupdater Can not run", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Detection .NET Framework version
            if (!Utils.IsSupportedRuntimeVersion())
            {
                MessageBox.Show("The current .NET Framework version is too low, please upgrade to 4.6.2 or newer",
                "Naupdater Can not run", MessageBoxButtons.OK, MessageBoxIcon.Error);

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
                    MessageBox.Show($"Naupdater is running... {Environment.NewLine}No need to open repeatedly");
                    return;
                }

                Directory.SetCurrentDirectory(Application.StartupPath);

                // Startup parameter
                ArgsHandle(programArgs);

                // Start the main interface
                Application.Run(new UpdaterForm());
            }
        }

        /// <summary>
        /// Start parameter processing
        /// </summary>
        /// <param name="programArgs"></param>
        private static void ArgsHandle(string[] programArgs)
        {
            // Program startup parameter
            var p = new FluentCommandLineParser<AppArguments>();

            Program.Args = p.Object;

            p.Setup(args => args.TargetAppName)
                .As("name")
                .SetDefault(Utils.GetAppConfig("TargetAppName", ""))
                .WithDescription("Target program name (for display)");

            p.Setup(args => args.TargetAppProcessName)
                .As("process")
                .SetDefault(Utils.GetAppConfig("TargetAppProcessName", ""))
                .WithDescription("Target program process name (used to force the application to close)");

            p.Setup(args => args.TargetAppRootPath)
                .As("root-path")
                .SetDefault(Utils.GetAppConfig("TargetAppRootPath", "./"))
                .WithDescription("Target program root directory (file operations will be performed in this directory)");

            p.Setup(args => args.SrcDownloadUrl)
                .As('u', "url")
                .WithDescription("Remote download upgrade resource file address (online upgrade)");

            p.Setup(args => args.SrcDownloadProxy)
                .As('p', "proxy")
                .SetDefault(Utils.GetAppConfig("DownloadProxy", ""))
                .WithDescription("Remote download agent");

            p.Setup(args => args.SrcFileMD5)
                .As('v', "md5")
                .WithDescription("Download the upgrade resource file MD5 value");

            p.Setup(args => args.SrcLocalFileName)
                .As("local-src")
                .SetDefault("")
                .WithDescription("Local upgrade resource file (offline upgrade)");

            p.Setup(args => args.LauchAppFileName)
                .As("lauch")
                .SetDefault(Utils.GetAppConfig("LauchAppFileName", ""))
                .WithDescription("Files started after the upgrade is completed");

            p.Setup(args => args.LauchAppFileArgs)
                .As("lauch-args")
                .SetDefault(Utils.GetAppConfig("LauchAppFileArgs", ""))
                .WithDescription("Files started after the upgrade is completed");
            
            p.Setup(args => args.UpdateMode)
                .As('m', "mode")
                .SetDefault(UpdateMode.OverwriteFiles)
                .WithDescription("Upgrade mode");

            p.SetupHelp("?", "help").Callback((text) => {
                MessageBox.Show(text, "Naupdater Startup parameter", MessageBoxButtons.OK);
                ExitApp();
            });
            
            if (p.Parse(programArgs).HasErrors)
                ReportErrorAndExit("Incorrect startup parameters");

            LauchErrorText = Utils.GetAppConfig("LauchErrorText", "The program can't start, please try to reload");

            // No Args
            if (string.IsNullOrWhiteSpace(Args.SrcDownloadUrl) && string.IsNullOrWhiteSpace(Args.SrcLocalFileName))
            {
                string filename = Utils.GetAppConfig("NoArgsLauchFile", "");
                if (!String.IsNullOrWhiteSpace(filename))
                    Program.LauchProgram(filename, Utils.GetAppConfig("NoArgsLauchFileArgs", ""));

                string msg = Utils.GetAppConfig("NoArgsMsgText", "");
                if (!String.IsNullOrWhiteSpace(msg))
                    MessageBox.Show(Utils.GetAppConfig("NoArgsMsgText", "Please specify the startup parameters"), $"{Args.TargetAppName} Upgrade procedure", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ExitApp();
            }

            // Path Handle
            Args.TargetAppRootPath = Utils.GetPathBasedOn(Application.StartupPath, Args.TargetAppRootPath);
        }

        public static string LauchErrorText; // Launcher error text

        /// <summary>
        /// starting program
        /// </summary>
        /// <param name="AppFilePath">Target program file path</param>
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
        /// Error handling
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

                MessageBox.Show($"{error}", $"{appName} Upgrade failed", MessageBoxButtons.OK, MessageBoxIcon.Error);

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
