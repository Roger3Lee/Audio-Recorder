using Microsoft.Extensions.Logging;

namespace AudioRecorder.Services
{
    /// <summary>
    /// 简单的控制台日志记录器
    /// </summary>
    public class ConsoleLogger : ILogger
    {
        private readonly string _categoryName;

        public ConsoleLogger(string categoryName = "AudioRecorder")
        {
            _categoryName = categoryName;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel)
        {
            return true; // 总是启用日志
        }

        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var message = formatter(state, exception);
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var level = GetLogLevelString(logLevel);
            
            var logMessage = $"[{timestamp}] [{level}] [{_categoryName}] {message}";
            
            if (exception != null)
            {
                logMessage += $"\nException: {exception}";
            }

            // 根据日志级别设置颜色
            var originalColor = Console.ForegroundColor;
            Console.ForegroundColor = GetLogLevelColor(logLevel);
            Console.WriteLine(logMessage);
            Console.ForegroundColor = originalColor;
        }

        private string GetLogLevelString(Microsoft.Extensions.Logging.LogLevel logLevel)
        {
            return logLevel switch
            {
                Microsoft.Extensions.Logging.LogLevel.Trace => "TRACE",
                Microsoft.Extensions.Logging.LogLevel.Debug => "DEBUG",
                Microsoft.Extensions.Logging.LogLevel.Information => "INFO ",
                Microsoft.Extensions.Logging.LogLevel.Warning => "WARN ",
                Microsoft.Extensions.Logging.LogLevel.Error => "ERROR",
                Microsoft.Extensions.Logging.LogLevel.Critical => "CRIT ",
                _ => "UNKN "
            };
        }

        private ConsoleColor GetLogLevelColor(Microsoft.Extensions.Logging.LogLevel logLevel)
        {
            return logLevel switch
            {
                Microsoft.Extensions.Logging.LogLevel.Trace => ConsoleColor.Gray,
                Microsoft.Extensions.Logging.LogLevel.Debug => ConsoleColor.DarkGray,
                Microsoft.Extensions.Logging.LogLevel.Information => ConsoleColor.White,
                Microsoft.Extensions.Logging.LogLevel.Warning => ConsoleColor.Yellow,
                Microsoft.Extensions.Logging.LogLevel.Error => ConsoleColor.Red,
                Microsoft.Extensions.Logging.LogLevel.Critical => ConsoleColor.DarkRed,
                _ => ConsoleColor.White
            };
        }
    }

    /// <summary>
    /// 控制台日志记录器工厂
    /// </summary>
    public class ConsoleLoggerFactory : ILoggerFactory
    {
        public void Dispose()
        {
            // 无需特殊清理
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new ConsoleLogger(categoryName);
        }

        public void AddProvider(ILoggerProvider provider)
        {
            // 不支持添加提供程序
        }
    }
}
