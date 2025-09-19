using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using Microsoft.Extensions.Logging;

namespace AudioRecorder.Services
{
    /// <summary>
    /// 端口生成器 - 在指定范围内顺序生成可用的端口
    /// </summary>
    public static class PortGenerator
    {
        private static readonly ILogger _logger = LoggingServiceManager.CreateLogger("PortGenerator");

        /// <summary>
        /// 默认端口范围
        /// </summary>
        public const int DefaultMinPort = 8081;
        public const int DefaultMaxPort = 8999;

        /// <summary>
        /// 获取一个可用的顺序端口（从最小端口开始顺序查找）
        /// </summary>
        /// <param name="minPort">最小端口号</param>
        /// <param name="maxPort">最大端口号</param>
        /// <param name="maxAttempts">最大尝试次数</param>
        /// <returns>可用的端口号，如果找不到则返回-1</returns>
        public static int GetAvailablePort(int minPort = DefaultMinPort, int maxPort = DefaultMaxPort, int maxAttempts = 100)
        {
            if (minPort > maxPort)
            {
                throw new ArgumentException("最小端口号不能大于最大端口号");
            }

            if (minPort < 1 || maxPort > 65535)
            {
                throw new ArgumentException("端口号必须在1-65535范围内");
            }

            _logger.LogInformation($"🔍 开始顺序查找可用端口，范围: {minPort}-{maxPort}");

            var usedPorts = GetUsedPorts();
            int attempts = 0;

            // 从最小端口开始顺序查找
            for (int port = minPort; port <= maxPort && attempts < maxAttempts; port++, attempts++)
            {
                // 检查端口是否被占用
                if (!usedPorts.Contains(port) && IsPortAvailable(port))
                {
                    _logger.LogInformation($"✅ 找到可用端口: {port} (尝试 {attempts + 1}/{maxAttempts})");
                    return port;
                }

                _logger.LogDebug($"⚠️ 端口 {port} 不可用，继续尝试...");
            }

            _logger.LogError($"❌ 在 {attempts} 次尝试后未找到可用端口，范围: {minPort}-{maxPort}");
            return -1;
        }

        /// <summary>
        /// 检查端口是否可用
        /// </summary>
        /// <param name="port">要检查的端口号</param>
        /// <returns>端口是否可用</returns>
        public static bool IsPortAvailable(int port)
        {
            try
            {
                // 尝试绑定端口
                using (var listener = new HttpListener())
                {
                    listener.Prefixes.Add($"http://localhost:{port}/");
                    listener.Start();
                    listener.Stop();
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug($"端口 {port} 不可用: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取系统当前使用的端口列表
        /// </summary>
        /// <returns>正在使用的端口集合</returns>
        private static HashSet<int> GetUsedPorts()
        {
            var usedPorts = new HashSet<int>();

            try
            {
                // 获取TCP连接
                var tcpConnections = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners();
                foreach (var connection in tcpConnections)
                {
                    usedPorts.Add(connection.Port);
                }

                // 获取UDP连接
                var udpConnections = IPGlobalProperties.GetIPGlobalProperties().GetActiveUdpListeners();
                foreach (var connection in udpConnections)
                {
                    usedPorts.Add(connection.Port);
                }

                _logger.LogDebug($"系统当前使用的端口数量: {usedPorts.Count}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"获取系统端口信息失败: {ex.Message}");
            }

            return usedPorts;
        }

        /// <summary>
        /// 验证端口号是否在有效范围内
        /// </summary>
        /// <param name="port">端口号</param>
        /// <param name="minPort">最小端口号</param>
        /// <param name="maxPort">最大端口号</param>
        /// <returns>是否有效</returns>
        public static bool IsValidPort(int port, int minPort = DefaultMinPort, int maxPort = DefaultMaxPort)
        {
            return port >= minPort && port <= maxPort && port >= 1 && port <= 65535;
        }

        /// <summary>
        /// 生成多个可用端口（顺序分配）
        /// </summary>
        /// <param name="count">需要的端口数量</param>
        /// <param name="minPort">最小端口号</param>
        /// <param name="maxPort">最大端口号</param>
        /// <returns>可用端口列表</returns>
        public static List<int> GetMultipleAvailablePorts(int count, int minPort = DefaultMinPort, int maxPort = DefaultMaxPort)
        {
            var ports = new List<int>();
            var usedPorts = GetUsedPorts();
            var allocatedPorts = new HashSet<int>();

            int currentPort = minPort;
            while (ports.Count < count && currentPort <= maxPort)
            {
                if (!usedPorts.Contains(currentPort) && 
                    !allocatedPorts.Contains(currentPort) && 
                    IsPortAvailable(currentPort))
                {
                    ports.Add(currentPort);
                    allocatedPorts.Add(currentPort);
                    _logger.LogDebug($"分配端口: {currentPort}");
                }
                currentPort++;
            }

            if (ports.Count < count)
            {
                _logger.LogWarning($"只能分配 {ports.Count}/{count} 个端口");
            }

            return ports;
        }
    }
}