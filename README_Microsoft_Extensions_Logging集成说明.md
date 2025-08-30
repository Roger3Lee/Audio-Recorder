# AudioRecorder Microsoft.Extensions.Logging 集成说明

## 🎯 概述

AudioRecorder现在使用**Microsoft.Extensions.Logging**作为统一的日志记录框架，配合**Serilog**作为底层日志提供程序，实现了专业的日志记录功能。

## 🚀 主要特性

### **1. 统一的日志接口**
- 使用Microsoft.Extensions.Logging作为标准日志接口
- 支持结构化日志记录
- 统一的日志级别和格式

### **2. 多种输出目标**
- **控制台输出**: 实时显示日志信息
- **文件输出**: 自动保存到安装目录的`log/app.log`文件
- **日志轮转**: 按日期和大小自动轮转日志文件

### **3. 高级日志功能**
- **结构化日志**: 支持参数化日志记录
- **异常记录**: 自动记录异常堆栈信息
- **性能优化**: 异步日志写入，不影响主程序性能

## 📦 依赖包

```xml
<PackageReference Include="Microsoft.Extensions.Logging" Version="8.0.0" />
<PackageReference Include="Serilog.Extensions.Logging" Version="8.0.0" />
<PackageReference Include="Serilog.Sinks.File" Version="5.0.0" />
<PackageReference Include="Serilog.Sinks.Console" Version="5.0.1" />
```

## 🏗️ 架构设计

### **核心组件**

#### 1. **MicrosoftLoggingServiceFactory**
```csharp
public class MicrosoftLoggingServiceFactory : ILoggerFactory
{
    // 配置Serilog日志系统
    // 管理日志文件和控制台输出
    // 实现ILoggerFactory接口
}
```

#### 2. **LoggingServiceManager**
```csharp
public static class LoggingServiceManager
{
    // 单例模式管理日志服务
    // 提供统一的日志记录器创建接口
    // 管理日志服务生命周期
}
```

### **日志配置**

```csharp
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
```

## 📁 日志文件管理

### **文件位置**
- **默认路径**: `{安装目录}/log/app.log`
- **备用路径**: `{当前目录}/log/app.log`（如果获取安装目录失败）

### **日志轮转策略**
- **按日期轮转**: 每天创建新的日志文件
- **按大小轮转**: 单个日志文件超过10MB时自动轮转
- **保留策略**: 最多保留30个历史日志文件
- **自动清理**: 定期清理过期日志文件

### **日志格式**
```
[2024-01-15 14:30:25.123 +08:00 INF] ConfigurationService - 配置文件加载成功: D:\work\audio\C#\appsettings.json
[2024-01-15 14:30:25.124 +08:00 INF] ConfigurationService - OAuth认证状态: 已启用
[2024-01-15 14:30:25.125 +08:00 INF] ConfigurationService - GitHub OAuth配置: 已配置
```

## 🔧 使用方法

### **1. 创建日志记录器**

```csharp
// 在类中声明
private readonly ILogger _logger;

// 在构造函数中初始化
public MyClass()
{
    _logger = LoggingServiceManager.CreateLogger("MyClass");
}
```

### **2. 记录不同级别的日志**

```csharp
// 信息日志
_logger.LogInformation("应用程序启动成功");

// 调试日志
_logger.LogDebug("处理用户请求: {UserId}", userId);

// 警告日志
_logger.LogWarning("配置项 {ConfigName} 使用默认值", configName);

// 错误日志
_logger.LogError(ex, "处理请求时发生错误");

// 严重错误日志
_logger.LogCritical(ex, "系统严重错误，需要立即处理");
```

### **3. 结构化日志记录**

```csharp
// 使用命名占位符
_logger.LogInformation("用户 {UserName} 登录成功，IP地址: {IpAddress}", userName, ipAddress);

// 记录复杂对象
_logger.LogDebug("音频配置: {SampleRate}Hz, {Channels}声道, {BitsPerSample}位", 
    config.SampleRate, config.Channels, config.BitsPerSample);
```

## 📊 日志级别

| 级别 | 说明 | 使用场景 |
|------|------|----------|
| **Trace** | 最详细的跟踪信息 | 详细的调试信息 |
| **Debug** | 调试信息 | 开发调试阶段 |
| **Information** | 一般信息 | 正常的操作信息 |
| **Warning** | 警告信息 | 需要注意但不影响运行 |
| **Error** | 错误信息 | 操作失败但可恢复 |
| **Critical** | 严重错误 | 系统严重问题 |

## 🔄 集成到现有代码

### **1. 替换Console.WriteLine**

```csharp
// 旧代码
Console.WriteLine($"⚠️ 初始化上传服务失败: {ex.Message}");

// 新代码
_logger.LogWarning(ex, "初始化上传服务失败");
```

### **2. 替换System.Diagnostics.Debug.WriteLine**

```csharp
// 旧代码
System.Diagnostics.Debug.WriteLine($"音频缓冲区 - 系统: {systemBuffer.Length}样本");

// 新代码
_logger.LogDebug("音频缓冲区 - 系统: {BufferSize}样本", systemBuffer.Length);
```

### **3. 异常记录**

```csharp
// 旧代码
catch (Exception ex)
{
    ErrorOccurred?.Invoke(this, new Exception("暂停录制时出错", ex));
}

// 新代码
catch (Exception ex)
{
    _logger.LogError(ex, "暂停录制时出错");
    ErrorOccurred?.Invoke(this, new Exception("暂停录制时出错", ex));
}
```

## 🚀 性能优化

### **1. 异步日志写入**
- 日志写入在后台线程执行
- 不阻塞主程序执行
- 自动缓冲和批量写入

### **2. 智能日志级别**
- 生产环境可以设置更高的日志级别
- 开发环境保留详细日志信息
- 动态调整日志详细程度

### **3. 内存管理**
- 自动管理日志缓冲区
- 定期刷新日志到磁盘
- 避免内存泄漏

## 🧪 测试和调试

### **1. 日志文件检查**
```bash
# 检查日志文件是否存在
ls -la log/app.log

# 查看最新日志
tail -f log/app.log

# 搜索特定日志
grep "ERROR" log/app.log
```

### **2. 日志级别调整**
```csharp
// 在代码中动态调整日志级别
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information  // 只记录Information及以上级别
    .WriteTo.Console()
    .WriteTo.File("app.log")
    .CreateLogger();
```

### **3. 性能监控**
- 监控日志文件大小增长
- 检查日志写入性能
- 验证日志轮转是否正常

## 🔒 安全性考虑

### **1. 敏感信息保护**
```csharp
// 避免记录敏感信息
_logger.LogInformation("用户登录: {UserName}", userName); // ✅ 安全
_logger.LogInformation("用户登录: {Password}", password); // ❌ 不安全
```

### **2. 日志文件权限**
- 日志文件存储在应用程序目录下
- 限制日志文件的访问权限
- 定期清理敏感日志信息

### **3. 生产环境配置**
```csharp
// 生产环境配置
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Warning  // 只记录警告和错误
    .WriteTo.File("app.log")
    .CreateLogger();
```

## 📈 监控和维护

### **1. 日志文件监控**
- 监控日志文件大小
- 检查日志轮转状态
- 验证日志写入权限

### **2. 性能指标**
- 日志写入延迟
- 日志文件大小增长
- 日志轮转频率

### **3. 故障排查**
- 日志文件损坏恢复
- 磁盘空间不足处理
- 日志服务异常重启

## 🎉 总结

通过集成Microsoft.Extensions.Logging，AudioRecorder现在具备了：

1. **专业的日志记录能力** - 结构化日志、多级别记录、异常跟踪
2. **灵活的日志输出** - 控制台和文件双重输出，自动轮转管理
3. **优秀的性能表现** - 异步写入、智能缓冲、内存优化
4. **易于维护和扩展** - 标准接口、模块化设计、配置灵活

这些改进大大提升了AudioRecorder的可靠性和可维护性，为生产环境部署提供了强有力的支持！🎯

## 🔗 相关文档

- [配置使用说明](README_配置使用说明.md) - 配置参数说明
- [实时音频保存功能](README_实时音频保存功能.md) - 实时保存技术细节
- [录音文件上传功能](README_录音文件上传功能.md) - 上传功能说明
- [暂停恢复录音问题修复](README_暂停恢复录音问题修复.md) - 基础问题修复说明
