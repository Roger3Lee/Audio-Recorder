using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Microsoft.Win32;
using Microsoft.Extensions.Logging;
using AudioRecorder.Services;

namespace AudioRecorder
{
    /// <summary>
    /// URL协议处理器，用于从浏览器启动应用程序
    /// </summary>
    public static class UrlProtocolHandler
    {
        private const string PROTOCOL_NAME = "audiorecorder";
        private const string PROTOCOL_DESCRIPTION = "Audio Recorder Protocol";
        private static readonly ILogger _logger = LoggingServiceManager.CreateLogger("UrlProtocolHandler");
        
        // 事件：当收到URL协议调用时触发
        public static event EventHandler<ProtocolActionEventArgs>? ProtocolActionReceived;
        
        /// <summary>
        /// 注册URL协议
        /// </summary>
        public static void RegisterProtocol()
        {
            try
            {
                string exePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrEmpty(exePath))
                {
                    exePath = Assembly.GetExecutingAssembly().Location;
                }

                if (string.IsNullOrEmpty(exePath))
                {
                    throw new InvalidOperationException("无法获取可执行文件路径");
                }

                // 使用HKCU注册表，避免权限问题
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey($"Software\\Classes\\{PROTOCOL_NAME}"))
                {
                    key.SetValue("", PROTOCOL_DESCRIPTION);
                    key.SetValue("URL Protocol", "");

                    using (RegistryKey commandKey = key.CreateSubKey("shell\\open\\command"))
                    {
                        commandKey.SetValue("", $"\"{exePath}\" \"%1\"");
                    }
                }

                _logger.LogInformation($"URL协议 {PROTOCOL_NAME}:// 注册成功到用户注册表");
            }
            catch (Exception ex)
            {
                _logger.LogError($"注册URL协议失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 注销URL协议
        /// </summary>
        public static void UnregisterProtocol()
        {
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree($"Software\\Classes\\{PROTOCOL_NAME}", false);
                _logger.LogInformation($"URL协议 {PROTOCOL_NAME}:// 注销成功");
            }
            catch (Exception ex)
            {
                _logger.LogError($"注销URL协议失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 处理URL协议参数
        /// </summary>
        /// <param name="url">完整的URL</param>
        public static void HandleProtocolUrl(string url)
        {
            try
            {
                if (string.IsNullOrEmpty(url))
                {
                    _logger.LogWarning("收到空的URL协议调用");
                    return;
                }

                _logger.LogInformation($"收到URL协议调用: {url}");

                // 解析URL参数
                if (url.StartsWith($"{PROTOCOL_NAME}://"))
                {
                    string parameters = url.Substring($"{PROTOCOL_NAME}://".Length);
                    ProcessProtocolParameters(parameters);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"处理URL协议失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 处理协议参数
        /// </summary>
        /// <param name="parameters">URL参数</param>
        private static void ProcessProtocolParameters(string parameters)
        {
            if (string.IsNullOrEmpty(parameters))
            {
                _logger.LogInformation("没有参数需要处理");
                return;
            }

            parameters = parameters.Replace("/", "");
            _logger.LogInformation($"处理参数: {parameters}");
            
            // 解析参数，支持多种格式
            if (parameters.Contains("action="))
            {
                // 解析action参数
                if (parameters.Contains("action=start"))
                {
                    _logger.LogInformation("收到启动录音命令");
                    // 触发事件，通知主窗口启动录音
                    ProtocolActionReceived?.Invoke(null, new ProtocolActionEventArgs { Action = "start" });
                }
                else if (parameters.Contains("action=stop"))
                {
                    _logger.LogInformation("收到停止录音命令");
                    // 触发事件，通知主窗口停止录音
                    ProtocolActionReceived?.Invoke(null, new ProtocolActionEventArgs { Action = "stop" });
                }
                else if (parameters.Contains("action=pause"))
                {
                    _logger.LogInformation("收到暂停录音命令");
                    // 触发事件，通知主窗口暂停录音
                    ProtocolActionReceived?.Invoke(null, new ProtocolActionEventArgs { Action = "pause" });
                }
                else if (parameters.Contains("action=resume"))
                {
                    _logger.LogInformation("收到恢复录音命令");
                    // 触发事件，通知主窗口恢复录音
                    ProtocolActionReceived?.Invoke(null, new ProtocolActionEventArgs { Action = "resume" });
                }
            }
            else if (parameters.Contains("show"))
            {
                _logger.LogInformation("收到显示窗口命令");
                // 触发事件，通知主窗口显示
                ProtocolActionReceived?.Invoke(null, new ProtocolActionEventArgs { Action = "show" });
            }
            else
            {
                _logger.LogInformation("收到未知命令，默认显示窗口");
                // 触发事件，通知主窗口显示
                ProtocolActionReceived?.Invoke(null, new ProtocolActionEventArgs { Action = "show" });
            }
        }

        /// <summary>
        /// 检查协议是否已注册
        /// </summary>
        /// <returns>是否已注册</returns>
        public static bool IsProtocolRegistered()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey($"Software\\Classes\\{PROTOCOL_NAME}"))
                {
                    return key != null;
                }
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// URL协议事件参数
    /// </summary>
    public class ProtocolActionEventArgs : EventArgs
    {
        public string Action { get; set; } = string.Empty;
        public Dictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>();
    }
}
