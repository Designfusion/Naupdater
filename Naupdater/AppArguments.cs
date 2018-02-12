using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Naupdater
{
    /// <summary>
    /// 启动参数
    /// </summary>
    public class AppArguments
    {
        /// <summary>
        /// 目标程序名
        /// </summary>
        public string TargetAppName { get; set; }

        /// <summary>
        /// 目标程序进程名
        /// </summary>
        public string TargetAppProcessName { get; set; }

        /// <summary>
        /// 目标程序目录路径
        /// </summary>
        public string TargetAppRootPath { get; set; }

        /// <summary>
        /// 更新完毕启动程序文件名
        /// </summary>
        public string LauchAppFileName { get; set; }

        /// <summary>
        /// 更新完毕启动程序参数
        /// </summary>
        public string LauchAppFileArgs { get; set; }

        /// <summary>
        /// 本地更新压缩包文件名
        /// </summary>
        public string SrcLocalFileName { get; set; }

        /// <summary>
        /// 更新资源下载 URL
        /// </summary>
        public string SrcDownloadUrl { get; set; }
        
        /// <summary>
        /// 下载代理
        /// </summary>
        public string SrcDownloadProxy { get; set; }

        /// <summary>
        ///  文件 MD5 值，防 DNS 劫持
        /// </summary>
        public string SrcFileMD5 { get; set; }

        /// <summary>
        /// 更新模式
        /// </summary>
        public UpdateMode UpdateMode { get; set; }
    }

    /// <summary>
    /// 升级模式
    /// </summary>
    public enum UpdateMode
    {
        OverwriteFiles = 1, // 覆盖文件升级
        DeleteAllFiles = 2, // 删除所有文件升级
    }
}
