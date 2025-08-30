using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Collections.Concurrent;
using System.Collections.Generic; // Added for List
using System.Linq; // Added for Sum and Select

namespace AudioRecorder.Services
{
    /// <summary>
    /// 日志级别枚举
    /// </summary>
    public enum LogLevel
    {
        Trace = 0,
        Debug = 1,
        Information = 2,
        Warning = 3,
        Error = 4,
        Critical = 5
    }

    /// <summary>
    /// 日志服务 - 支持文件日志记录和日志轮转
    /// </summary>
    public class LoggingService : IDisposable
    {
        private static LoggingService? _instance;
        private static readonly object _lock = new object();
        private readonly string _logDirectory;
        private readonly string _logFilePath;
        private readonly int _maxLogFileSizeMB;
        private readonly int _maxLogFiles;
        private LogLevel _minLogLevel;
        private readonly ConcurrentQueue<string> _logQueue;
        private readonly Thread _logWriterThread;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly object _fileLock = new object();
        private bool _disposed = false;

        public event EventHandler<string>? LogMessageReceived;

        private LoggingService()
        {
            _logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            _logFilePath = Path.Combine(_logDirectory, $"AudioRecorder_{DateTime.Now:yyyyMMdd}.log");
            _maxLogFileSizeMB = 10; // 最大日志文件大小10MB
            _maxLogFiles = 30; // 保留30个日志文件
            _minLogLevel = LogLevel.Information; // 最小日志级别
            _logQueue = new ConcurrentQueue<string>();
            _cancellationTokenSource = new CancellationTokenSource();

            // 确保日志目录存在
            Directory.CreateDirectory(_logDirectory);

            // 启动日志写入线程
            _logWriterThread = new Thread(LogWriterWorker)
            {
                IsBackground = true,
                Name = "LogWriterThread"
            };
            _logWriterThread.Start();

            // 记录服务启动日志
            Log(LogLevel.Information, "日志服务已启动", "LoggingService");
        }

        public static LoggingService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new LoggingService();
                        }
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// 记录跟踪级别日志
        /// </summary>
        public void LogTrace(string message, string? category = null, Exception? exception = null)
        {
            Log(LogLevel.Trace, message, category, exception);
        }

        /// <summary>
        /// 记录调试级别日志
        /// </summary>
        public void LogDebug(string message, string? category = null, Exception? exception = null)
        {
            Log(LogLevel.Debug, message, category, exception);
        }

        /// <summary>
        /// 记录信息级别日志
        /// </summary>
        public void LogInformation(string message, string? category = null, Exception? exception = null)
        {
            Log(LogLevel.Information, message, category, exception);
        }

        /// <summary>
        /// 记录警告级别日志
        /// </summary>
        public void LogWarning(string message, string? category = null, Exception? exception = null)
        {
            Log(LogLevel.Warning, message, category, exception);
        }

        /// <summary>
        /// 记录错误级别日志
        /// </summary>
        public void LogError(string message, string? category = null, Exception? exception = null)
        {
            Log(LogLevel.Error, message, category, exception);
        }

        /// <summary>
        /// 记录严重错误级别日志
        /// </summary>
        public void LogCritical(string message, string? category = null, Exception? exception = null)
        {
            Log(LogLevel.Critical, message, category, exception);
        }

        /// <summary>
        /// 记录日志的核心方法
        /// </summary>
        public void Log(LogLevel level, string message, string? category = null, Exception? exception = null)
        {
            if (level < _minLogLevel)
                return;

            try
            {
                var logEntry = CreateLogEntry(level, message, category, exception);
                _logQueue.Enqueue(logEntry);

                // 触发日志消息事件
                LogMessageReceived?.Invoke(this, logEntry);

                // 对于错误和严重错误，立即写入文件
                if (level >= LogLevel.Error)
                {
                    FlushLogs();
                }
            }
            catch (Exception ex)
            {
                // 如果日志记录失败，至少尝试写入控制台
                Console.WriteLine($"日志记录失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 创建日志条目
        /// </summary>
        private string CreateLogEntry(LogLevel level, string message, string? category, Exception? exception)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var levelStr = level.ToString().ToUpper().PadRight(8);
            var categoryStr = string.IsNullOrEmpty(category) ? "GLOBAL" : category.PadRight(15);
            var threadId = Thread.CurrentThread.ManagedThreadId;

            var logEntry = $"[{timestamp}] [{levelStr}] [{categoryStr}] [TID:{threadId:D4}] {message}";

            if (exception != null)
            {
                logEntry += $"\n{GetExceptionDetails(exception)}";
            }

            return logEntry;
        }

        /// <summary>
        /// 获取异常详细信息
        /// </summary>
        private string GetExceptionDetails(Exception exception)
        {
            var sb = new StringBuilder();
            var currentException = exception;
            var depth = 0;

            while (currentException != null && depth < 5)
            {
                sb.AppendLine($"  Exception Type: {currentException.GetType().Name}");
                sb.AppendLine($"  Message: {currentException.Message}");
                sb.AppendLine($"  Source: {currentException.Source}");
                sb.AppendLine($"  Stack Trace: {currentException.StackTrace}");

                if (currentException.InnerException != null)
                {
                    sb.AppendLine("  Inner Exception:");
                    currentException = currentException.InnerException;
                    depth++;
                }
                else
                {
                    break;
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// 日志写入工作线程
        /// </summary>
        private void LogWriterWorker()
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    // 批量处理日志条目
                    var logEntries = new List<string>();
                    var count = 0;

                    while (_logQueue.TryDequeue(out var logEntry) && count < 100)
                    {
                        logEntries.Add(logEntry);
                        count++;
                    }

                    if (logEntries.Count > 0)
                    {
                        WriteLogsToFile(logEntries);
                    }

                    // 休眠一段时间
                    Thread.Sleep(100);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"日志写入线程错误: {ex.Message}");
                    Thread.Sleep(1000); // 出错时等待更长时间
                }
            }
        }

        /// <summary>
        /// 将日志写入文件
        /// </summary>
        private void WriteLogsToFile(List<string> logEntries)
        {
            lock (_fileLock)
            {
                try
                {
                    // 检查文件大小，如果超过限制则轮转
                    CheckAndRotateLogFile();

                    // 写入日志条目
                    File.AppendAllLines(_logFilePath, logEntries, Encoding.UTF8);

                    // 清理旧日志文件
                    CleanupOldLogFiles();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"写入日志文件失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 检查并轮转日志文件
        /// </summary>
        private void CheckAndRotateLogFile()
        {
            try
            {
                if (File.Exists(_logFilePath))
                {
                    var fileInfo = new FileInfo(_logFilePath);
                    if (fileInfo.Length > _maxLogFileSizeMB * 1024 * 1024)
                    {
                        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                        var newFileName = $"AudioRecorder_{timestamp}.log";
                        var newFilePath = Path.Combine(_logDirectory, newFileName);

                        File.Move(_logFilePath, newFilePath);
                        LogInformation($"日志文件已轮转: {newFileName}", "LoggingService");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"日志文件轮转失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 清理旧日志文件
        /// </summary>
        private void CleanupOldLogFiles()
        {
            try
            {
                var logFiles = Directory.GetFiles(_logDirectory, "AudioRecorder_*.log")
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.CreationTime)
                    .ToList();

                if (logFiles.Count > _maxLogFiles)
                {
                    var filesToDelete = logFiles.Skip(_maxLogFiles);
                    foreach (var file in filesToDelete)
                    {
                        try
                        {
                            file.Delete();
                            LogInformation($"已删除旧日志文件: {file.Name}", "LoggingService");
                        }
                        catch (Exception ex)
                        {
                            LogWarning($"删除旧日志文件失败: {file.Name}, 错误: {ex.Message}", "LoggingService");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"清理旧日志文件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 立即刷新日志
        /// </summary>
        public void FlushLogs()
        {
            try
            {
                var logEntries = new List<string>();
                while (_logQueue.TryDequeue(out var logEntry))
                {
                    logEntries.Add(logEntry);
                }

                if (logEntries.Count > 0)
                {
                    WriteLogsToFile(logEntries);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"刷新日志失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取日志文件路径
        /// </summary>
        public string GetLogFilePath()
        {
            return _logFilePath;
        }

        /// <summary>
        /// 获取日志目录路径
        /// </summary>
        public string GetLogDirectory()
        {
            return _logDirectory;
        }

        /// <summary>
        /// 设置最小日志级别
        /// </summary>
        public void SetMinLogLevel(LogLevel level)
        {
            _minLogLevel = level;
            LogInformation($"最小日志级别已设置为: {level}", "LoggingService");
        }

        /// <summary>
        /// 获取当前日志统计信息
        /// </summary>
        public string GetLogStatistics()
        {
            try
            {
                var logFiles = Directory.GetFiles(_logDirectory, "AudioRecorder_*.log");
                var totalSize = logFiles.Sum(f => new FileInfo(f).Length);
                var totalSizeMB = totalSize / (1024.0 * 1024.0);

                return $"日志文件数量: {logFiles.Length}, 总大小: {totalSizeMB:F2} MB, 队列中待写入: {_logQueue.Count}";
            }
            catch
            {
                return "无法获取日志统计信息";
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;

                try
                {
                    // 停止日志写入线程
                    _cancellationTokenSource.Cancel();
                    
                    // 等待线程结束
                    if (_logWriterThread.IsAlive)
                    {
                        _logWriterThread.Join(TimeSpan.FromSeconds(5));
                    }

                    // 刷新剩余日志
                    FlushLogs();

                    // 释放资源
                    _cancellationTokenSource.Dispose();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"释放日志服务时出错: {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    /// 日志扩展方法，方便使用
    /// </summary>
    public static class LoggingExtensions
    {
        /// <summary>
        /// 记录OAuth相关日志
        /// </summary>
        public static void LogOAuth(this LoggingService logger, LogLevel level, string message, Exception? exception = null)
        {
            logger.Log(level, message, "OAuth", exception);
        }

        /// <summary>
        /// 记录音频录制相关日志
        /// </summary>
        public static void LogAudioRecorder(this LoggingService logger, LogLevel level, string message, Exception? exception = null)
        {
            logger.Log(level, message, "AudioRecorder", exception);
        }

        /// <summary>
        /// 记录网络相关日志
        /// </summary>
        public static void LogNetwork(this LoggingService logger, LogLevel level, string message, Exception? exception = null)
        {
            logger.Log(level, message, "Network", exception);
        }

        /// <summary>
        /// 记录配置相关日志
        /// </summary>
        public static void LogConfiguration(this LoggingService logger, LogLevel level, string message, Exception? exception = null)
        {
            logger.Log(level, message, "Configuration", exception);
        }

        /// <summary>
        /// 记录用户界面相关日志
        /// </summary>
        public static void LogUI(this LoggingService logger, LogLevel level, string message, Exception? exception = null)
        {
            logger.Log(level, message, "UI", exception);
        }
    }
}
