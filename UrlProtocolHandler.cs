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
        
        /// <summary>
        /// 注册URL协议
        /// </summary>
        public static void RegisterProtocol()
        {
            try
            {
                string exePath = Assembly.GetExecutingAssembly().Location;
                if (string.IsNullOrEmpty(exePath))
                {
                    exePath = Process.GetCurrentProcess().MainModule?.FileName;
                }

                if (string.IsNullOrEmpty(exePath))
                {
                    throw new InvalidOperationException("无法获取可执行文件路径");
                }

                // 注册协议到注册表
                using (RegistryKey key = Registry.ClassesRoot.CreateSubKey(PROTOCOL_NAME))
                {
                    key.SetValue("", PROTOCOL_DESCRIPTION);
                    key.SetValue("URL Protocol", "");

                    using (RegistryKey commandKey = key.CreateSubKey("shell\\open\\command"))
                    {
                        commandKey.SetValue("", $"\"{exePath}\" \"%1\"");
                    }
                }

                _logger.LogInformation($"URL协议 {PROTOCOL_NAME}:// 注册成功");
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
                Registry.ClassesRoot.DeleteSubKeyTree(PROTOCOL_NAME, false);
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

            // 解析参数，支持多种格式
            if (parameters.Contains("action="))
            {
                // 解析action参数
                if (parameters.Contains("action=start"))
                {
                    _logger.LogInformation("收到启动录音命令");
                    // 这里可以触发启动录音的逻辑
                }
                else if (parameters.Contains("action=stop"))
                {
                    _logger.LogInformation("收到停止录音命令");
                    // 这里可以触发停止录音的逻辑
                }
            }

            // 可以添加更多参数处理逻辑
            _logger.LogInformation($"处理参数: {parameters}");
        }

        /// <summary>
        /// 检查协议是否已注册
        /// </summary>
        /// <returns>是否已注册</returns>
        public static bool IsProtocolRegistered()
        {
            try
            {
                using (RegistryKey key = Registry.ClassesRoot.OpenSubKey(PROTOCOL_NAME))
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
}
