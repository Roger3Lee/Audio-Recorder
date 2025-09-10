using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using AudioRecorder.Services;
using Microsoft.Extensions.Logging;

namespace AudioRecorder
{
    /// <summary>
    /// 程序入口点
    /// </summary>
    public class Program
    {
        // 互斥锁名称 - 确保全局唯一
        private const string MUTEX_NAME = "Global\\AudioRecorder_SingleInstance_Mutex";
        private static Mutex? _mutex;
        private static bool _isFirstInstance = false;
        private static string? _pendingProtocolUrl;
        private static ILogger? _logger;

        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        public static void Main(string[] args)
        {
            try
            {
                // 检查命令行参数
                if (args.Length > 0)
                {
                    var arg = args[0].ToLower();
                    if (arg == "--uninstall" || arg == "--remove")
                    {
                        Console.WriteLine("执行卸载清理...");
                        UninstallCleanupService.PerformUninstallCleanup();
                        return;
                    }
                    
                    // 检查是否是检测命令 - 在互斥锁检查之前处理
                    if (arg.StartsWith("audiorecorder://") && arg.Contains("action=detect"))
                    {
                        // 初始化最小日志服务
                        _logger = LoggingServiceManager.CreateLogger("Program");
                        _logger.LogInformation($"处理检测命令: {arg}");
                        
                        // 直接处理检测命令
                        UrlProtocolHandler.HandleProtocolUrl(arg);
                        return; // 处理完检测后直接退出，不占用互斥锁
                    }
                }

                // 尝试获取互斥锁
                _mutex = new Mutex(true, MUTEX_NAME, out _isFirstInstance);

                if (!_isFirstInstance)
                {
                    // 已有实例在运行
                    _logger = LoggingServiceManager.CreateLogger("Program");
                    _logger.LogInformation("检测到现有实例正在运行");
                    
                    // 如果有URL协议调用，尝试发送给现有实例
                    if (args.Length > 0 && !string.IsNullOrEmpty(args[0]))
                    {
                        string url = args[0];
                        if (url.StartsWith("audiorecorder://"))
                        {
                            _logger.LogInformation($"尝试将协议调用发送给现有实例: {url}");
                            
                            // 解析URL协议并创建IPC命令
                            var command = ParseUrlToIPCCommand(url);
                            if (command != null)
                            {
                                // 使用ConfigureAwait(false)避免死锁
                                bool sent = IPCManager.SendCommandToExistingInstance(command).ConfigureAwait(false).GetAwaiter().GetResult();
                                if (sent)
                                {
                                    _logger.LogInformation("命令已成功发送给现有实例");
                                    return;
                                }
                                else
                                {
                                    _logger.LogWarning("发送命令给现有实例失败，尝试激活现有实例");
                                }
                            }
                        }
                    }
                    
                    // 如果没有协议调用或发送失败，激活现有实例
                    System.Windows.MessageBox.Show(
                        "AudioRecorder 已经在运行中！\n\n请检查系统托盘或任务栏。",
                        "AudioRecorder - 实例已运行",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information
                    );
                    
                    // 尝试激活已运行的实例
                    ActivateExistingInstance();
                    return;
                }

                // 第一个实例，继续运行
                Console.WriteLine("🚀 AudioRecorder 启动中...");
                
                // 初始化日志服务
                _logger = LoggingServiceManager.CreateLogger("Program");
                _logger.LogInformation("AudioRecorder应用程序启动");
                
                // 检查是否正在卸载
                if (UninstallCleanupService.IsUninstalling())
                {
                    _logger.LogInformation("检测到卸载操作，执行清理...");
                    UninstallCleanupService.PerformUninstallCleanup();
                    return;
                }
                
                // 注册URL协议（如果还没有注册）
                //if (!UrlProtocolHandler.IsProtocolRegistered())
                //{
                //    UrlProtocolHandler.RegisterProtocol();
                //}

                // 处理URL协议调用
                if (args.Length > 0 && !string.IsNullOrEmpty(args[0]))
                {
                    string url = args[0];
                    if (url.StartsWith("audiorecorder://"))
                    {
                        // 保存URL协议调用，稍后处理
                        _pendingProtocolUrl = url;
                        _logger.LogInformation($"收到URL协议调用: {url}");
                    }
                }

                // 启动 WPF 应用程序
                var app = new System.Windows.Application();
                
                // 设置应用程序关闭模式 - 显式关闭时才退出应用程序（支持托盘运行）
                app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                
                // 添加应用程序退出事件处理
                app.Exit += (sender, e) =>
                {
                    _logger?.LogInformation("应用程序正在退出...");
                    try
                    {
                        // 清理日志服务资源
                        LoggingServiceManager.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"清理日志服务失败: {ex.Message}");
                    }
                };
                
                app.Startup += (sender, e) =>
                {
                    // 创建主窗口
                    var mainWindow = new RecorderWindow();
                    app.MainWindow = mainWindow;
                    
                    // 确保窗口可见和激活
                    mainWindow.Show();
                    mainWindow.Activate();
                    mainWindow.WindowState = System.Windows.WindowState.Normal;
                    
                    // 如果有待处理的URL协议调用，延迟处理
                    if (!string.IsNullOrEmpty(_pendingProtocolUrl))
                    {
                        _logger.LogInformation($"延迟处理URL协议调用: {_pendingProtocolUrl}");
                        mainWindow.Dispatcher.BeginInvoke(() =>
                        {
                            try
                            {
                                UrlProtocolHandler.HandleProtocolUrl(_pendingProtocolUrl!);
                                _logger.LogInformation("URL协议调用处理完成");
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError($"处理URL协议调用失败: {ex.Message}");
                            }
                        }, System.Windows.Threading.DispatcherPriority.Normal);
                    }
                };
                
                app.Run();
            }
            catch (Exception ex)
            {
                var logger = LoggingServiceManager.CreateLogger("Program");
                logger.LogError(ex, "应用程序启动失败");
                throw;
            }
            finally
            {
                try
                {
                    // 清理日志服务资源
                    LoggingServiceManager.Dispose();
                    // 释放互斥锁
                    _mutex?.ReleaseMutex();
                    _mutex?.Dispose();
                    
                    _logger?.LogInformation("应用程序清理完成");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"最终清理失败: {ex.Message}");
                }
                
                // 确保进程完全退出
                Environment.Exit(0);
            }
        }

        /// <summary>
        /// 解析URL协议为IPC命令
        /// </summary>
        /// <param name="url">URL协议字符串</param>
        /// <returns>IPC命令对象</returns>
        private static IPCCommand ParseUrlToIPCCommand(string url)
        {
            try
            {
                if (!url.StartsWith("audiorecorder://"))
                    return null;

                string parameters = url.Substring("audiorecorder://".Length);
                parameters = parameters.Replace("/", "");

                // 默认action为show
                string action = "show";
                var paramDict = new Dictionary<string, object>();

                if (parameters.Contains("action="))
                {
                    // 解析action参数
                    if (parameters.Contains("action=start"))
                        action = "start";
                    else if (parameters.Contains("action=stop"))
                        action = "stop";
                    else if (parameters.Contains("action=pause"))
                        action = "pause";
                    else if (parameters.Contains("action=resume"))
                        action = "resume";
                    else if (parameters.Contains("action=show"))
                        action = "show";
                }

                return new IPCCommand
                {
                    Action = action,
                    Timestamp = DateTime.Now,
                    Parameters = paramDict
                };
            }
            catch (Exception ex)
            {
                _logger?.LogError($"解析URL协议失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 激活已运行的实例
        /// </summary>
        private static void ActivateExistingInstance()
        {
            try
            {
                // 查找已运行的 AudioRecorder 进程
                var processes = Process.GetProcessesByName("AudioRecorder");
                
                if (processes.Length > 0)
                {
                    var existingProcess = processes[0];
                    
                    // 尝试激活窗口
                    if (existingProcess.MainWindowHandle != IntPtr.Zero)
                    {
                        // 显示窗口
                        ShowWindow(existingProcess.MainWindowHandle, SW_SHOW);
                        
                        // 激活窗口
                        SetForegroundWindow(existingProcess.MainWindowHandle);
                        
                        // 如果窗口最小化，恢复它
                        if (IsIconic(existingProcess.MainWindowHandle))
                        {
                            ShowWindow(existingProcess.MainWindowHandle, SW_RESTORE);
                        }
                        
                        Console.WriteLine("✅ 已激活现有实例");
                    }
                    else
                    {
                        Console.WriteLine("⚠️ 找到进程但无法激活窗口");
                    }
                }
                else
                {
                    Console.WriteLine("⚠️ 未找到已运行的实例");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ 激活现有实例失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 应用程序退出事件处理
        /// </summary>
        private static void OnApplicationExit(object? sender, EventArgs e)
        {
            try
            {
                Console.WriteLine("🔄 应用程序正在退出，执行清理...");
                UninstallCleanupService.PerformUninstallCleanup();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ 退出清理失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 控制台取消键事件处理
        /// </summary>
        private static void OnConsoleCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
        {
            try
            {
                Console.WriteLine("🔄 收到退出信号，执行清理...");
                UninstallCleanupService.PerformUninstallCleanup();
                
                // 允许正常退出
                e.Cancel = false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ 退出清理失败: {ex.Message}");
            }
        }

        #region Windows API 声明

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        // 窗口显示命令常量
        private const int SW_HIDE = 0;
        private const int SW_SHOWNORMAL = 1;
        private const int SW_SHOWMINIMIZED = 2;
        private const int SW_SHOWMAXIMIZED = 3;
        private const int SW_SHOWNOACTIVATE = 4;
        private const int SW_SHOW = 5;
        private const int SW_MINIMIZE = 6;
        private const int SW_SHOWMINNOACTIVE = 7;
        private const int SW_SHOWNA = 8;
        private const int SW_RESTORE = 9;
        private const int SW_SHOWDEFAULT = 10;
        private const int SW_FORCEMINIMIZE = 11;

        #endregion
    }
} 