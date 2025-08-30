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

        public bool IsEnabled(LogLevel logLevel)
        {
            return true; // 总是启用日志
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
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

        private string GetLogLevelString(LogLevel logLevel)
        {
            return logLevel switch
            {
                LogLevel.Trace => "TRACE",
                LogLevel.Debug => "DEBUG",
                LogLevel.Information => "INFO ",
                LogLevel.Warning => "WARN ",
                LogLevel.Error => "ERROR",
                LogLevel.Critical => "CRIT ",
                _ => "UNKN "
            };
        }

        private ConsoleColor GetLogLevelColor(LogLevel logLevel)
        {
            return logLevel switch
            {
                LogLevel.Trace => ConsoleColor.Gray,
                LogLevel.Debug => ConsoleColor.DarkGray,
                LogLevel.Information => ConsoleColor.White,
                LogLevel.Warning => ConsoleColor.Yellow,
                LogLevel.Error => ConsoleColor.Red,
                LogLevel.Critical => ConsoleColor.DarkRed,
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
