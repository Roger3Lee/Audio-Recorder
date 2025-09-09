using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using AudioRecorder.Services;

namespace AudioRecorder
{
    /// <summary>
    /// 进程间通信管理器
    /// 用于在多个程序实例之间传递命令
    /// </summary>
    public class IPCManager : IDisposable
    {
        private const string PIPE_NAME = "AudioRecorderPipe";
        private const string EVENT_NAME = "AudioRecorderEvent";
        private const int TIMEOUT_MS = 3000;
        
        private static readonly ILogger _logger = LoggingServiceManager.CreateLogger("IPCManager");
        
        private NamedPipeServerStream _pipeServer;
        private EventWaitHandle _notificationEvent;
        private CancellationTokenSource _cancellationTokenSource;
        private Task _serverTask;
        private bool _disposed = false;
        
        // 事件：当收到IPC命令时触发
        public event EventHandler<IPCCommandEventArgs> CommandReceived;
        
        /// <summary>
        /// 启动IPC服务器（在主实例中调用）
        /// </summary>
        public void StartServer()
        {
            try
            {
                _logger.LogInformation("启动IPC服务器");
                
                // 创建命名事件
                _notificationEvent = new EventWaitHandle(false, EventResetMode.AutoReset, EVENT_NAME);
                
                // 创建取消令牌
                _cancellationTokenSource = new CancellationTokenSource();
                
                // 启动管道服务器任务
                _serverTask = Task.Run(() => RunServerAsync(_cancellationTokenSource.Token));
                
                _logger.LogInformation("IPC服务器启动成功");
            }
            catch (Exception ex)
            {
                _logger.LogError($"启动IPC服务器失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 停止IPC服务器
        /// </summary>
        public void StopServer()
        {
            try
            {
                _logger.LogInformation("停止IPC服务器");
                
                _cancellationTokenSource?.Cancel();
                
                _pipeServer?.Close();
                _pipeServer?.Dispose();
                
                _notificationEvent?.Set(); // 唤醒等待的线程
                _notificationEvent?.Dispose();
                
                _serverTask?.Wait(1000); // 等待1秒
                
                _logger.LogInformation("IPC服务器已停止");
            }
            catch (Exception ex)
            {
                _logger.LogError($"停止IPC服务器失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 发送命令给现有实例（在新实例中调用）
        /// </summary>
        /// <param name="command">要发送的命令</param>
        /// <returns>是否发送成功</returns>
        public static async Task<bool> SendCommandToExistingInstance(IPCCommand command)
        {
            try
            {
                _logger.LogInformation($"发送IPC命令: {command.Action}");
                
                // 序列化命令
                string jsonCommand = JsonSerializer.Serialize(command, new JsonSerializerOptions 
                { 
                    WriteIndented = false 
                });
                
                // 通过命名管道发送命令
                using (var pipeClient = new NamedPipeClientStream(".", PIPE_NAME, PipeDirection.Out))
                {
                    // 尝试连接到服务器
                    await pipeClient.ConnectAsync(TIMEOUT_MS);
                    
                    // 发送命令数据
                    byte[] data = Encoding.UTF8.GetBytes(jsonCommand);
                    await pipeClient.WriteAsync(data, 0, data.Length);
                    await pipeClient.FlushAsync();
                    
                    _logger.LogInformation("IPC命令发送成功");
                }
                
                // 通知现有实例有新命令
                using (var notificationEvent = EventWaitHandle.OpenExisting(EVENT_NAME))
                {
                    notificationEvent.Set();
                }
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"发送IPC命令失败: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// 检查是否存在现有实例
        /// </summary>
        /// <returns>是否存在现有实例</returns>
        public static bool IsExistingInstanceRunning()
        {
            try
            {
                // 尝试打开命名事件，如果成功说明有现有实例
                using (var existingEvent = EventWaitHandle.OpenExisting(EVENT_NAME))
                {
                    return true;
                }
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                // 事件不存在，说明没有现有实例
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"检查现有实例时出错: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// 运行管道服务器
        /// </summary>
        private async Task RunServerAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // 创建新的管道服务器实例
                    _pipeServer = new NamedPipeServerStream(
                        PIPE_NAME,
                        PipeDirection.In,
                        1, // 最大实例数
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);
                    
                    _logger.LogDebug("等待IPC客户端连接");
                    
                    // 等待客户端连接
                    await _pipeServer.WaitForConnectionAsync(cancellationToken);
                    
                    _logger.LogDebug("IPC客户端已连接");
                    
                    // 读取命令数据
                    using (var reader = new StreamReader(_pipeServer, Encoding.UTF8))
                    {
                        string jsonCommand = await reader.ReadToEndAsync();
                        
                        if (!string.IsNullOrEmpty(jsonCommand))
                        {
                            // 解析命令
                            var command = JsonSerializer.Deserialize<IPCCommand>(jsonCommand);
                            
                            _logger.LogInformation($"收到IPC命令: {command.Action}");
                            
                            // 触发命令事件
                            CommandReceived?.Invoke(this, new IPCCommandEventArgs { Command = command });
                        }
                    }
                    
                    // 断开连接
                    _pipeServer.Disconnect();
                }
                catch (OperationCanceledException)
                {
                    // 正常取消，退出循环
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError($"IPC服务器运行错误: {ex.Message}");
                    
                    // 短暂延迟后重试
                    await Task.Delay(1000, cancellationToken);
                }
                finally
                {
                    try
                    {
                        _pipeServer?.Dispose();
                        _pipeServer = null;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"清理管道服务器时出错: {ex.Message}");
                    }
                }
            }
            
            _logger.LogDebug("IPC服务器任务已退出");
        }
        
        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                StopServer();
                _cancellationTokenSource?.Dispose();
                _disposed = true;
            }
        }
    }
    
    /// <summary>
    /// IPC命令数据结构
    /// </summary>
    public class IPCCommand
    {
        public string Action { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
    }
    
    /// <summary>
    /// IPC命令事件参数
    /// </summary>
    public class IPCCommandEventArgs : EventArgs
    {
        public IPCCommand Command { get; set; }
    }
}