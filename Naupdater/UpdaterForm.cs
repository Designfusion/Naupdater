using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Configuration;
using System.Collections.Specialized;
using SevenZipExtractor;
using Fclp;

namespace Naupdater
{
    /// <summary>
    /// Naupdater Updater Form
    /// </summary>
    public partial class UpdaterForm : Form
    {
        private static AppArguments Args; // 运行参数

        private static string DownloadingText; // 下载时显示的文字
        private static string ExtractingText; // 提取压缩包时的文字

        private static string ArchiveFilePath; // 更新压缩包文件路径

        public UpdaterForm()
        {
            Args = Program.Args;

            // 配置文件
            DownloadingText = Utils.GetAppConfig("DownloadingText", "正在下载更新...");
            ExtractingText = Utils.GetAppConfig("ExtractingText", "正在应用更新...");

            // 升级压缩包文件路径
            ArchiveFilePath = (string.IsNullOrWhiteSpace(Args.SrcLocalFileName))
                ? Utils.GetTempFilePathWithExtension(Path.GetExtension(Args.SrcDownloadUrl)) // 随机临时文件
                : Utils.GetPathBasedOn(Application.StartupPath, Args.SrcLocalFileName); // 使用参数指定的文件

            // 初始化界面
            InitializeComponent();
            Text = $"{Args.TargetAppName} 升级程序";
        }

        /// <summary>
        /// 操控一切 23333
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void UpdaterForm_Load(object sender, EventArgs e)
        {
            // 开始执行升级
            if (!string.IsNullOrEmpty(Args.SrcDownloadUrl))
            {
                // 在线升级
                await Task.Run(() => {
                    UpdateSrcDownload();
                    UpdateSrcExtract();

                    // 删除已下载的升级压缩包
                    if (File.Exists(ArchiveFilePath))
                        File.Delete(ArchiveFilePath);
                });
            }
            else
            {
                // 离线升级
                await Task.Run(() =>
                {
                    UpdateSrcExtract();
                });
            }

            // 启动程序
            if (!String.IsNullOrWhiteSpace(Args.LauchAppFileName))
            {
                Program.LauchProgram(Args.LauchAppFileName, Args.LauchAppFileArgs);
            }

            // 执行之后的操作


            // 退出升级程序
            this.Dispose();
            this.Close();
            Program.ExitApp();
        }

        /// <summary>
        /// 下载升级压缩包
        /// </summary>
        private void UpdateSrcDownload()
        {
            Uri SrcUri = new Uri(Args.SrcDownloadUrl);

            SetProgressDesc(DownloadingText
                .Replace("{SrcDownloadUrl}", Args.SrcDownloadUrl)
                .Replace("{SrcDownloadFile}", Path.GetFileName(Args.SrcDownloadUrl))
                .Replace("{SrcDownloadLocalFile}", ArchiveFilePath));
            SetCurrentProgram(0, "正在连接远程服务器...");
            
            if (File.Exists(ArchiveFilePath))
                File.Delete(ArchiveFilePath);

            // Init
            Stopwatch sw = new Stopwatch();
            WebClient wc = new WebClient();

            if (!String.IsNullOrWhiteSpace(Args.SrcDownloadProxy))
            {
                WebProxy wp = new WebProxy(Args.SrcDownloadProxy);
                wc.Proxy = wp;
            }
            
            // Events
            wc.DownloadProgressChanged += (s, e) =>
            {
                double receive = double.Parse(e.BytesReceived.ToString());
                double total = double.Parse(e.TotalBytesToReceive.ToString());
                double percentage = (total > 0) ? ((receive / total) * 100) : 0;

                string speed = string.Format("{0} KB/s", (e.BytesReceived / 1024d / sw.Elapsed.TotalSeconds).ToString("0.00"));
                SetCurrentProgram(percentage, $"已下载 {string.Format("{0:0.##}", percentage)}%  速度 {speed}");
            };
            wc.DownloadFileCompleted += (s, e) => {
                sw.Reset();
            };
            
            // Go
            sw.Start();
            try {
                Task.Run(async () =>
                {
                    await wc.DownloadFileTaskAsync(SrcUri, ArchiveFilePath);
                }).Wait();
            } catch (Exception e) {
                Program.ReportErrorAndExit($"下载错误：{e.Message}{Environment.NewLine}{e.InnerException.ToString()}");
                return;
            }

            // Done
            SetCurrentProgram(100, "下载已完成");

            // 检测 MD5 是否匹配
            if (!String.IsNullOrWhiteSpace(Args.SrcFileMD5))
            {
                SetProgressDesc("正在验证文件...");
                SetCurrentProgram(100, "请稍后...");
                string fileMD5 = Utils.CalculateMD5(ArchiveFilePath);
                if (fileMD5.ToUpper() != Args.SrcFileMD5.ToUpper())
                {
                    Program.ReportErrorAndExit("下载文件 MD5 值有误，请尝试重新升级");
                    return;
                }
            }
        }

        /// <summary>
        /// 解压升级压缩包
        /// </summary>
        private void UpdateSrcExtract()
        {
            SetProgressDesc("准备更新文件中...");
            SetCurrentProgram(0, "耐心等待一下吧");

            if (!File.Exists(ArchiveFilePath))
            {
                Program.ReportErrorAndExit($"未找到升级包");
                return;
            }

            // 杀死 目标程序 进程
            Utils.KillProcess(Args.TargetAppProcessName);

            // 升级模式：删除所有文件
            if (Args.UpdateMode == UpdateMode.DeleteAllFiles)
            {
                Utils.DeleteFilesAndFolders(Args.TargetAppRootPath, $"^(Naupdater.*|{Path.GetFileName(ArchiveFilePath)})");
            }

            try
            {
                using (ArchiveFile archiveFile = new ArchiveFile(ArchiveFilePath))
                {
                    double extracted = 0;
                    double entriesTotal = double.Parse(archiveFile.Entries.Count.ToString());

                    foreach (Entry entry in archiveFile.Entries)
                    {
                        string ProcessText = ExtractingText
                            .Replace("{ArchiveFileName}", ArchiveFilePath)
                            .Replace("{EntryFileName}", entry.FileName)
                            .Replace("{EntryFileSize}", Utils.FormatBytes(long.Parse(entry.Size.ToString())));

                        // 更新进度
                        if (!entry.IsFolder)
                            SetProgressDesc(ProcessText);

                        double percentage = (entriesTotal > 0) ? ((extracted / entriesTotal) * 100) : 0;
                        SetCurrentProgram(percentage, $"已完成 {string.Format("{0:0.##}", percentage)}%");

                        // 提取文件
                        Console.WriteLine($"准备提取文件 {entry.FileName}");
                        if (!String.IsNullOrWhiteSpace(entry.FileName))
                            entry.Extract(Utils.GetPathBasedOn(Args.TargetAppRootPath, entry.FileName));
                        Console.WriteLine($"文件 {entry.FileName} 已提取");

                        extracted++;
                    }
                }
            }
            catch (SevenZipException e)
            {
                Program.ReportErrorAndExit("解压升级包错误：" + e.Message.ToString());
                return;
            }
        }

        /// <summary>
        /// 设置当前进展
        /// </summary>
        /// <param name="percentage"></param>
        /// <param name="percentageDesc"></param>
        public void SetCurrentProgram(double percentage, string percentageDesc)
        {
            if (InvokeRequired) { Invoke(new SetCurrentProgramDelegate(SetCurrentProgram), new object[] { percentage, percentageDesc }); return; }

            if (percentage > 0)
            {
                UpdateProgressBar.Value = int.Parse(Math.Truncate(percentage).ToString());
                UpdateProgressBar.Style = ProgressBarStyle.Continuous;
            } else
            {
                UpdateProgressBar.Style = ProgressBarStyle.Marquee;
            }
            UpdatePercentageDesc.Text = percentageDesc;
        }
        public delegate void SetCurrentProgramDelegate(double percentage, string percentageDesc);


        /// <summary>
        /// 设置进度描述
        /// </summary>
        /// <param name="progressDesc"></param>
        public void SetProgressDesc(string progressDesc)
        {
            if (InvokeRequired) { Invoke(new SetProgressDescDelegate(SetProgressDesc), new object[] { progressDesc }); return; }

            UpdateProgressDesc.Text = progressDesc;
        }
        public delegate void SetProgressDescDelegate(string progressDesc);
    }
}
