using System;
using System.IO;
using System.Reflection;
using Microsoft.Extensions.Logging;
using AudioRecorder.Services;

namespace AudioRecorder
{
    /// <summary>
    /// 卸载清理服务 - 负责在应用程序卸载时清理所有注册的资源
    /// </summary>
    public static class UninstallCleanupService
    {
        private static readonly ILogger _logger = LoggingServiceManager.CreateLogger("UninstallCleanupService");
        
        /// <summary>
        /// 执行完整的卸载清理
        /// </summary>
        public static void PerformUninstallCleanup()
        {
            try
            {
                _logger.LogInformation("开始执行卸载清理...");
                
                // 1. 注销URL协议
                CleanupUrlProtocol();
                
                // 2. 清理用户数据目录
                CleanupUserDataDirectories();
                
                // 3. 清理临时文件
                CleanupTempFiles();
                
                // 4. 清理日志文件
                CleanupLogFiles();
                
                _logger.LogInformation("卸载清理完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "卸载清理过程中发生错误");
            }
        }
        
        /// <summary>
        /// 清理URL协议注册
        /// </summary>
        private static void CleanupUrlProtocol()
        {
            try
            {
                _logger.LogInformation("正在清理URL协议注册...");
                
                // 调用UrlProtocolHandler的注销方法
                UrlProtocolHandler.UnregisterProtocol();
                
                // 额外清理可能存在的注册表项
                CleanupRegistryEntries();
                
                _logger.LogInformation("URL协议清理完成");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "清理URL协议时发生错误");
            }
        }
        
        /// <summary>
        /// 清理注册表项
        /// </summary>
        private static void CleanupRegistryEntries()
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("Software\\Classes", true))
                {
                    if (key != null)
                    {
                        // 删除 audiorecorder 协议
                        key.DeleteSubKeyTree("audiorecorder", false);
                        
                        // 删除文件关联
                        key.DeleteSubKeyTree(".audiorecord", false);
                        key.DeleteSubKeyTree("AudioRecorder.Document", false);
                    }
                }
                
                // 清理应用程序能力注册
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("Software\\AudioRecorder", true))
                {
                    if (key != null)
                    {
                        key.DeleteSubKeyTree("Capabilities", false);
                    }
                }
                
                _logger.LogInformation("注册表项清理完成");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "清理注册表项时发生错误");
            }
        }
        
        /// <summary>
        /// 清理用户数据目录
        /// </summary>
        private static void CleanupUserDataDirectories()
        {
            try
            {
                _logger.LogInformation("正在清理用户数据目录...");
                
                // 清理录音文件目录
                string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string audioRecorderPath = Path.Combine(documentsPath, "AudioRecorder");
                
                if (Directory.Exists(audioRecorderPath))
                {
                    // 询问用户是否删除录音文件
                    _logger.LogInformation("发现用户录音文件目录: {Path}", audioRecorderPath);
                    // 这里可以添加用户确认逻辑
                }
                
                _logger.LogInformation("用户数据目录清理完成");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "清理用户数据目录时发生错误");
            }
        }
        
        /// <summary>
        /// 清理临时文件
        /// </summary>
        private static void CleanupTempFiles()
        {
            try
            {
                _logger.LogInformation("正在清理临时文件...");
                
                string tempPath = Path.GetTempPath();
                string[] tempFiles = Directory.GetFiles(tempPath, "AudioRecorder_*", SearchOption.TopDirectoryOnly);
                
                foreach (string file in tempFiles)
                {
                    try
                    {
                        File.Delete(file);
                        _logger.LogDebug("删除临时文件: {File}", file);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "删除临时文件失败: {File}", file);
                    }
                }
                
                _logger.LogInformation("临时文件清理完成");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "清理临时文件时发生错误");
            }
        }
        
        /// <summary>
        /// 清理日志文件
        /// </summary>
        private static void CleanupLogFiles()
        {
            try
            {
                _logger.LogInformation("正在清理日志文件...");
                
                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string logPath = Path.Combine(appDataPath, "AudioRecorder", "logs");
                
                if (Directory.Exists(logPath))
                {
                    string[] logFiles = Directory.GetFiles(logPath, "*.log", SearchOption.TopDirectoryOnly);
                    
                    foreach (string file in logFiles)
                    {
                        try
                        {
                            File.Delete(file);
                            _logger.LogDebug("删除日志文件: {File}", file);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "删除日志文件失败: {File}", file);
                        }
                    }
                }
                
                _logger.LogInformation("日志文件清理完成");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "清理日志文件时发生错误");
            }
        }
        
        /// <summary>
        /// 检查是否正在卸载
        /// </summary>
        /// <returns>是否正在卸载</returns>
        public static bool IsUninstalling()
        {
            try
            {
                // 检查命令行参数
                string[] args = Environment.GetCommandLineArgs();
                return args.Any(arg => arg.Contains("uninstall") || arg.Contains("remove"));
            }
            catch
            {
                return false;
            }
        }
    }
}
