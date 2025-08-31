using System;
using System.Windows;
using AudioRecorder.Services;
using Microsoft.Extensions.Logging;

namespace AudioRecorder
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            try
            {
                // 初始化日志服务
                var logger = LoggingServiceManager.CreateLogger("Program");
                logger.LogInformation("AudioRecorder应用程序启动");
                
                // 检查是否正在卸载
                if (UninstallCleanupService.IsUninstalling())
                {
                    logger.LogInformation("检测到卸载操作，执行清理...");
                    UninstallCleanupService.PerformUninstallCleanup();
                    return;
                }
                
                // 处理URL协议调用
                if (args.Length > 0 && !string.IsNullOrEmpty(args[0]))
                {
                    string url = args[0];
                    if (url.StartsWith("audiorecorder://"))
                    {
                        // 处理URL协议调用
                        UrlProtocolHandler.HandleProtocolUrl(url);
                        
                        // 如果应用已经在运行，只处理协议调用
                        // 如果应用没有运行，则启动应用
                        if (!IsApplicationRunning())
                        {
                            var wpfApp1 = new System.Windows.Application();
                            wpfApp1.Run(new RecorderWindow());
                        }
                        return;
                    }
                }

                // 正常启动WPF应用
                // 注册URL协议（如果还没有注册）
                if (!UrlProtocolHandler.IsProtocolRegistered())
                {
                    UrlProtocolHandler.RegisterProtocol();
                }
                
                var wpfApp = new System.Windows.Application();
                
                // 注册应用程序退出事件
                wpfApp.Exit += OnApplicationExit;
                
                wpfApp.Run(new RecorderWindow());
            }
            catch (Exception ex)
            {
                var logger = LoggingServiceManager.CreateLogger("Program");
                logger.LogError(ex, "应用程序启动失败");
                throw;
            }
            finally
            {
                // 清理日志服务资源
                LoggingServiceManager.Dispose();
            }
        }

        /// <summary>
        /// 应用程序退出事件处理
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">退出事件参数</param>
        private static void OnApplicationExit(object sender, ExitEventArgs e)
        {
            try
            {
                var logger = LoggingServiceManager.CreateLogger("Program");
                logger.LogInformation("应用程序正在退出，执行清理操作...");
                
                // 执行卸载清理（如果适用）
                UninstallCleanupService.PerformUninstallCleanup();
                
                logger.LogInformation("应用程序退出清理完成");
            }
            catch (Exception ex)
            {
                var logger = LoggingServiceManager.CreateLogger("Program");
                logger.LogError(ex, "应用程序退出清理时发生错误");
            }
        }

        /// <summary>
        /// 检查应用程序是否已经在运行
        /// </summary>
        /// <returns>是否已经在运行</returns>
        private static bool IsApplicationRunning()
        {
            string processName = "AudioRecorder";
            System.Diagnostics.Process[] processes = System.Diagnostics.Process.GetProcessesByName(processName);
            return processes.Length > 1; // 当前进程也算一个
        }
    }
} 