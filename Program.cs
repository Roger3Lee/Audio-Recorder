using System;
using System.Threading;
using System.Windows;
using System.Diagnostics;
using System.Runtime.InteropServices;
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
                }

                // 尝试获取互斥锁
                _mutex = new Mutex(true, MUTEX_NAME, out _isFirstInstance);

                if (!_isFirstInstance)
                {
                    // 已有实例在运行
                    MessageBox.Show(
                        "AudioRecorder 已经在运行中！\n\n请检查系统托盘或任务栏。",
                        "AudioRecorder - 实例已运行",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
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
                if (!UrlProtocolHandler.IsProtocolRegistered())
                {
                    UrlProtocolHandler.RegisterProtocol();
                }

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
                
                // 设置应用程序关闭模式 - 主窗口关闭时退出应用程序
                app.ShutdownMode = ShutdownMode.OnMainWindowClose;
                
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