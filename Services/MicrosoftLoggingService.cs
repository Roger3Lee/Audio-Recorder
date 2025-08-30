using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using System;
using System.IO;

namespace AudioRecorder.Services
{
    /// <summary>
    /// Microsoft.Extensions.Logging日志服务工厂
    /// </summary>
    public class MicrosoftLoggingServiceFactory : ILoggerFactory
    {
        private readonly ILoggerFactory _loggerFactory;
        private bool _disposed = false;

        public MicrosoftLoggingServiceFactory()
        {
            // 配置Serilog
            var logPath = GetLogFilePath();
            var logDirectory = Path.GetDirectoryName(logPath);
            
            if (!string.IsNullOrEmpty(logDirectory) && !Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
                .WriteTo.File(
                    path: logPath,
                    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Category} - {Message:lj} {Properties:j}{NewLine}{Exception}",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30,
                    fileSizeLimitBytes: 10 * 1024 * 1024, // 10MB
                    rollOnFileSizeLimit: true,
                    shared: true,
                    flushToDiskInterval: TimeSpan.FromSeconds(1))
                .Enrich.FromLogContext()
                .CreateLogger();

            _loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddSerilog(dispose: true);
            });
        }

        private string GetLogFilePath()
        {
            try
            {
                // 获取应用程序安装目录
                var appDirectory = AppContext.BaseDirectory;
                var logDirectory = Path.Combine(appDirectory, "log");
                return Path.Combine(logDirectory, "app.log");
            }
            catch
            {
                // 如果获取安装目录失败，使用当前目录
                var currentDirectory = Directory.GetCurrentDirectory();
                var logDirectory = Path.Combine(currentDirectory, "log");
                return Path.Combine(logDirectory, "app.log");
            }
        }

        public void AddProvider(ILoggerProvider provider)
        {
            if (!_disposed)
            {
                _loggerFactory.AddProvider(provider);
            }
        }

        public Microsoft.Extensions.Logging.ILogger CreateLogger(string categoryName)
        {
            return _disposed ? _loggerFactory.CreateLogger("Null") : _loggerFactory.CreateLogger(categoryName);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _loggerFactory?.Dispose();
                Log.CloseAndFlush();
            }
        }
    }

    /// <summary>
    /// 日志服务管理器
    /// </summary>
    public static class LoggingServiceManager
    {
        private static MicrosoftLoggingServiceFactory? _loggerFactory;
        private static readonly object _lock = new object();

        /// <summary>
        /// 获取日志服务工厂实例
        /// </summary>
        public static MicrosoftLoggingServiceFactory Instance
        {
            get
            {
                if (_loggerFactory == null)
                {
                    lock (_lock)
                    {
                        if (_loggerFactory == null)
                        {
                            _loggerFactory = new MicrosoftLoggingServiceFactory();
                        }
                    }
                }
                return _loggerFactory;
            }
        }

        /// <summary>
        /// 创建指定类别的日志记录器
        /// </summary>
        /// <param name="categoryName">类别名称</param>
        /// <returns>日志记录器</returns>
        public static Microsoft.Extensions.Logging.ILogger CreateLogger(string categoryName)
        {
            return Instance.CreateLogger(categoryName);
        }

        /// <summary>
        /// 创建指定类型的日志记录器
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <returns>日志记录器</returns>
        public static Microsoft.Extensions.Logging.ILogger<T> CreateLogger<T>()
        {
            return Instance.CreateLogger<T>();
        }

        /// <summary>
        /// 释放日志服务资源
        /// </summary>
        public static void Dispose()
        {
            if (_loggerFactory != null)
            {
                lock (_lock)
                {
                    if (_loggerFactory != null)
                    {
                        _loggerFactory.Dispose();
                        _loggerFactory = null;
                    }
                }
            }
        }
    }
}
