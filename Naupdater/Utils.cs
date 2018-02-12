using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Security.Permissions;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Naupdater
{
    public class Utils
    {
        /// <summary>
        /// 关闭进程
        /// </summary>
        /// <param name="processName">进程名</param>
        public static void KillProcess(string processName)
        {
            Process[] myproc = Process.GetProcesses();
            foreach (Process item in myproc)
            {
                if (item.ProcessName == processName)
                {
                    item.Kill();
                }
            }
        }

        /// <summary>  
        /// 删除目录下的所有文件和文件夹
        /// </summary>  
        /// <param name="path">The specific path</param>
        /// <param name="excludeFileNameReg">正则表达式排除的文件名</param>  
        public static void DeleteFilesAndFolders(string path, string excludeFileNameReg=null)
        {
            // Delete files.  
            string[] files = Directory.GetFiles(path);
            foreach (var file in files)
            {
                if (!string.IsNullOrEmpty(excludeFileNameReg) && new Regex(excludeFileNameReg).IsMatch(Path.GetFileName(file))) continue;
                File.Delete(file);
            }

            // Delete folders.  
            string[] folders = Directory.GetDirectories(path);
            foreach (var folder in folders)
            {
                DeleteFilesAndFolders(folder, excludeFileNameReg);
                Directory.Delete(folder);
            }
        }

        /// <summary>  
        /// 获取时间戳  
        /// </summary>  
        /// <returns></returns>  
        public static string GetTimeStamp()
        {
            TimeSpan ts = DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, 0);
            return Convert.ToInt64(ts.TotalMilliseconds).ToString();
        }

        /// <summary>
        /// 获取程序配置
        /// </summary>
        /// <param name="key"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public static string GetAppConfig(string key, string defaultValue = null)
        {
            NameValueCollection configs = ConfigurationManager.AppSettings;

            string value = configs[key];
            if (string.IsNullOrEmpty(value))
                value = null;

            return value ?? defaultValue;
        }
        
        /// <summary>
        /// Base 64 编码
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string Base64Encode(string str)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(str));
        }

        /// <summary>
        /// 获取文件 MD5 值
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        public static string CalculateMD5(string filename)
        {
            using (var md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(filename))
                {
                    var hash = md5.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
        }

        /// <summary>
        /// 生成一个临时文件路径
        /// </summary>
        /// <param name="extension"></param>
        /// <returns></returns>
        public static string GetTempFilePathWithExtension(string extension)
        {
            var path = Path.GetTempPath();
            var fileName = Guid.NewGuid().ToString() + extension;
            return Path.Combine(path, fileName);
        }

        public static void ReleaseMemory(bool removePages)
        {
            // release any unused pages
            // making the numbers look good in task manager
            // this is totally nonsense in programming
            // but good for those users who care
            // making them happier with their everyday life
            // which is part of user experience
            GC.Collect(GC.MaxGeneration);
            GC.WaitForPendingFinalizers();
            if (removePages)
            {
                // as some users have pointed out
                // removing pages from working set will cause some IO
                // which lowered user experience for another group of users
                //
                // so we do 2 more things here to satisfy them:
                // 1. only remove pages once when configuration is changed
                // 2. add more comments here to tell users that calling
                //    this function will not be more frequent than
                //    IM apps writing chat logs, or web browsers writing cache files
                //    if they're so concerned about their disk, they should
                //    uninstall all IM apps and web browsers
                //
                // please open an issue if you're worried about anything else in your computer
                // no matter it's GPU performance, monitor contrast, audio fidelity
                // or anything else in the task manager
                // we'll do as much as we can to help you
                //
                // just kidding
                SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle,
                                         (UIntPtr)0xFFFFFFFF,
                                         (UIntPtr)0xFFFFFFFF);
            }
        }


        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetProcessWorkingSetSize(IntPtr process,
            UIntPtr minimumWorkingSetSize, UIntPtr maximumWorkingSetSize);

        public static string FormatBytes(long bytes)
        {
            const long K = 1024L;
            const long M = K * 1024L;
            const long G = M * 1024L;
            const long T = G * 1024L;
            const long P = T * 1024L;
            const long E = P * 1024L;

            if (bytes >= P * 990)
                return (bytes / (double)E).ToString("F5") + "EiB";
            if (bytes >= T * 990)
                return (bytes / (double)P).ToString("F5") + "PiB";
            if (bytes >= G * 990)
                return (bytes / (double)T).ToString("F5") + "TiB";
            if (bytes >= M * 990)
            {
                return (bytes / (double)G).ToString("F4") + "GiB";
            }
            if (bytes >= M * 100)
            {
                return (bytes / (double)M).ToString("F1") + "MiB";
            }
            if (bytes >= M * 10)
            {
                return (bytes / (double)M).ToString("F2") + "MiB";
            }
            if (bytes >= K * 990)
            {
                return (bytes / (double)M).ToString("F3") + "MiB";
            }
            if (bytes > K * 2)
            {
                return (bytes / (double)K).ToString("F1") + "KiB";
            }
            return bytes.ToString() + "B";
        }

        /// <summary>
        /// 获取 基于A 的 B 路径
        /// </summary>
        /// <param name="basePath">A</param>
        /// <param name="fileName">B</param>
        /// <returns>结果会始终包含 A</returns>
        public static string GetPathBasedOn(string basePath, string fileName)
        {
            string fullPath = GetFullPath(
                startPath: @"\", 
                partialPath: fileName
            ); // 处于 @"X:\" 解析相对路径为绝对路径

            fullPath = fullPath.Substring(Path.GetPathRoot(fullPath).Length); // 剔除 @"X:\", @"\", "/"
            
            return Path.GetFullPath(Path.Combine(basePath, fullPath));
        }

        /// <summary>
        /// 处于指定路径下 解析相对路径为绝对路径
        /// </summary>
        /// <param name="startPath"></param>
        /// <param name="partialPath"></param>
        /// <returns></returns>
        public static string GetFullPath(string startPath, string partialPath)
        {
            partialPath = partialPath.TrimStart('.', '\\', '/'); // 剔除开头字符

            string oldStartPath = Directory.GetCurrentDirectory();
            
            try
            {
                Directory.SetCurrentDirectory(startPath);

                if (String.IsNullOrWhiteSpace(partialPath))
                    return Path.GetFullPath(".");

                return Path.GetFullPath(partialPath);
            }
            finally { Directory.SetCurrentDirectory(oldStartPath); }
        }

        public static RegistryKey OpenRegKey(string name, bool writable, RegistryHive hive = RegistryHive.CurrentUser)
        {
            // we are building x86 binary for both x86 and x64, which will
            // cause problem when opening registry key
            // detect operating system instead of CPU
            if (string.IsNullOrEmpty(name)) throw new ArgumentException(nameof(name));
            try
            {
                RegistryKey userKey = RegistryKey.OpenBaseKey(hive,
                        Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Registry32)
                    .OpenSubKey(name, writable);
                return userKey;
            }
            catch (ArgumentException ae)
            {
                MessageBox.Show("OpenRegKey: " + ae.ToString());
                return null;
            }
            catch (Exception e)
            {
                Console.WriteLine($"[Error] {e}");
                return null;
            }
        }

        public static bool IsWinVistaOrHigher()
        {
            return Environment.OSVersion.Version.Major > 5;
        }

        // See: https://msdn.microsoft.com/en-us/library/hh925568(v=vs.110).aspx
        public static bool IsSupportedRuntimeVersion()
        {
            /*
             * +-----------------------------------------------------------------+----------------------------+
             * | Version                                                         | Value of the Release DWORD |
             * +-----------------------------------------------------------------+----------------------------+
             * | .NET Framework 4.6.2 installed on Windows 10 Anniversary Update | 394802                     |
             * | .NET Framework 4.6.2 installed on all other Windows OS versions | 394806                     |
             * +-----------------------------------------------------------------+----------------------------+
             */
            const int minSupportedRelease = 394802;

            const string subkey = @"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full\";
            using (var ndpKey = OpenRegKey(subkey, false, RegistryHive.LocalMachine))
            {
                if (ndpKey?.GetValue("Release") != null)
                {
                    var releaseKey = (int)ndpKey.GetValue("Release");

                    if (releaseKey >= minSupportedRelease)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
