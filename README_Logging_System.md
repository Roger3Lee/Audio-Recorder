# 📝 AudioRecorder 日志系统说明

本文档介绍AudioRecorder应用的完整日志系统，包括日志记录、文件存储、日志轮转等功能。

## 🎯 功能特性

### 1. 多级别日志支持
- **Trace**: 最详细的跟踪信息
- **Debug**: 调试信息
- **Information**: 一般信息
- **Warning**: 警告信息
- **Error**: 错误信息
- **Critical**: 严重错误信息

### 2. 文件日志记录
- 自动创建日志目录 (`Logs/`)
- 按日期生成日志文件 (`AudioRecorder_YYYYMMDD.log`)
- 支持日志文件轮转（超过10MB自动轮转）
- 自动清理旧日志文件（保留30个文件）

### 3. 异步写入
- 使用后台线程异步写入日志
- 批量处理提高性能
- 错误日志立即写入文件

### 4. 结构化日志
- 时间戳（精确到毫秒）
- 日志级别
- 分类标识
- 线程ID
- 详细异常信息

## 🔧 配置说明

### 日志级别设置
```csharp
// 设置最小日志级别
LoggingService.Instance.SetMinLogLevel(LogLevel.Debug);

// 获取当前日志统计信息
var stats = LoggingService.Instance.GetLogStatistics();
```

### 日志文件配置
- **日志目录**: `{应用目录}/Logs/`
- **文件命名**: `AudioRecorder_YYYYMMDD.log`
- **最大文件大小**: 10MB
- **保留文件数量**: 30个
- **编码格式**: UTF-8

## 📚 使用方法

### 1. 基本日志记录
```csharp
var logger = LoggingService.Instance;

// 记录不同级别的日志
logger.LogTrace("跟踪信息");
logger.LogDebug("调试信息");
logger.LogInformation("一般信息");
logger.LogWarning("警告信息");
logger.LogError("错误信息");
logger.LogCritical("严重错误信息");
```

### 2. 带分类的日志
```csharp
// 指定日志分类
logger.LogInformation("用户登录成功", "Authentication");
logger.LogError("网络连接失败", "Network");
logger.LogWarning("配置加载失败", "Configuration");
```

### 3. 异常日志记录
```csharp
try
{
    // 业务逻辑
}
catch (Exception ex)
{
    logger.LogError("操作失败", "BusinessLogic", ex);
}
```

### 4. 使用扩展方法
```csharp
// OAuth相关日志
logger.LogOAuth(LogLevel.Information, "GitHub授权完成");

// 音频录制相关日志
logger.LogAudioRecorder(LogLevel.Debug, "开始录制音频");

// 网络相关日志
logger.LogNetwork(LogLevel.Warning, "网络连接超时");

// 配置相关日志
logger.LogConfiguration(LogLevel.Information, "配置文件已加载");

// 用户界面相关日志
logger.LogUI(LogLevel.Debug, "按钮点击事件");
```

## 🏗️ 架构设计

### 核心组件
1. **LoggingService**: 日志服务主类
2. **日志队列**: 使用ConcurrentQueue存储待写入的日志
3. **后台线程**: 异步写入日志文件
4. **文件管理**: 日志轮转和清理

### 设计模式
- **单例模式**: LoggingService全局唯一实例
- **生产者-消费者模式**: 日志生产者和写入线程
- **策略模式**: 不同日志级别的处理策略

## 📊 日志格式示例

### 标准日志条目
```
[2024-01-15 14:30:25.123] [INFO    ] [OAuthLoginService] [TID:0001] GitHub OAuth提供商初始化成功
```

### 异常日志条目
```
[2024-01-15 14:30:25.456] [ERROR   ] [ConfigurationService] [TID:0001] 加载配置文件失败: 文件不存在
  Exception Type: FileNotFoundException
  Message: 文件不存在
  Source: System.IO.File
  Stack Trace: at System.IO.File.ReadAllText(String path)
```

## 🔍 日志查看和分析

### 1. 实时日志监控
```csharp
// 订阅日志消息事件
LoggingService.Instance.LogMessageReceived += (sender, logMessage) =>
{
    Console.WriteLine($"实时日志: {logMessage}");
};
```

### 2. 日志文件位置
- 当前日志: `Logs/AudioRecorder_YYYYMMDD.log`
- 历史日志: `Logs/AudioRecorder_YYYYMMDD_HHMMSS.log`

### 3. 日志统计信息
```csharp
var stats = LoggingService.Instance.GetLogStatistics();
// 输出: "日志文件数量: 5, 总大小: 45.67 MB, 队列中待写入: 12"
```

## 🚀 性能优化

### 1. 异步写入
- 日志记录不阻塞主线程
- 批量写入减少I/O操作
- 错误日志立即写入

### 2. 内存管理
- 使用队列缓存日志条目
- 自动清理过期日志文件
- 限制单个日志文件大小

### 3. 磁盘空间管理
- 自动轮转大文件
- 清理旧日志文件
- 可配置保留策略

## 🛠️ 故障排除

### 常见问题

#### 1. 日志文件无法创建
- 检查应用目录权限
- 确认磁盘空间充足
- 验证文件路径有效性

#### 2. 日志写入失败
- 检查文件是否被占用
- 确认磁盘空间
- 查看异常详细信息

#### 3. 日志级别不生效
- 确认SetMinLogLevel调用
- 检查日志级别设置
- 验证日志记录代码

### 调试技巧
```csharp
// 启用详细日志
LoggingService.Instance.SetMinLogLevel(LogLevel.Trace);

// 强制刷新日志
LoggingService.Instance.FlushLogs();

// 获取日志服务状态
var logPath = LoggingService.Instance.GetLogFilePath();
var logDir = LoggingService.Instance.GetLogDirectory();
```

## 📈 最佳实践

### 1. 日志级别使用
- **Trace**: 详细的执行流程跟踪
- **Debug**: 开发调试信息
- **Information**: 重要的业务事件
- **Warning**: 需要注意的情况
- **Error**: 错误但不影响系统运行
- **Critical**: 严重错误，可能影响系统

### 2. 日志内容规范
- 使用清晰、简洁的描述
- 包含必要的上下文信息
- 避免记录敏感信息
- 使用统一的命名规范

### 3. 性能考虑
- 避免在循环中记录大量日志
- 合理设置日志级别
- 定期清理旧日志文件
- 监控日志文件大小

## 🔄 扩展功能

### 1. 自定义日志格式
```csharp
// 可以扩展LoggingService支持自定义格式
public class CustomLoggingService : LoggingService
{
    protected override string CreateLogEntry(LogLevel level, string message, string? category, Exception? exception)
    {
        // 自定义日志格式
        return $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";
    }
}
```

### 2. 日志过滤
```csharp
// 可以添加日志过滤功能
public void AddLogFilter(Func<string, bool> filter)
{
    // 实现日志过滤逻辑
}
```

### 3. 远程日志
```csharp
// 可以扩展支持远程日志服务器
public void SendToRemoteServer(string logEntry)
{
    // 实现远程日志发送
}
```

## 📝 总结

AudioRecorder的日志系统提供了：

✅ **完整的日志级别支持**
✅ **异步文件写入**
✅ **自动日志轮转和清理**
✅ **结构化日志格式**
✅ **异常详细信息记录**
✅ **性能优化的设计**
✅ **易于使用的API**
✅ **可扩展的架构**

该日志系统为应用提供了全面的运行状态监控和问题诊断能力，是开发和运维的重要工具。
