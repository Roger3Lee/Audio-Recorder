using System;
using System.Windows;

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
            wpfApp.Run(new RecorderWindow());
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