# AudioRecorder 配置使用说明

## 🎯 配置概述

AudioRecorder现在支持通过`appsettings.json`配置文件来管理所有重要设置，包括OAuth认证、音频录制、实时保存、文件上传等功能。这使得应用程序更加灵活和可配置。

## 📁 配置文件结构

### 完整配置文件示例

```json
{
    "Logging": {
        "LogLevel": {
            "Default": "Information",
            "Microsoft.AspNetCore": "Warning"
        }
    },
    "AllowedHosts": "*",
    "OAuthSettings": {
        "EnableAuthentication": false,
        "GitHub": {
            "ClientId": "your-github-client-id",
            "ClientSecret": "your-github-client-secret",
            "RedirectUri": "http://localhost:8081/auth/callback",
            "Scopes": [ "user", "user:email" ]
        },
        "Google": {
            "ClientId": "your-google-client-id",
            "ClientSecret": "your-google-client-secret",
            "RedirectUri": "http://localhost:8081/auth/callback",
            "Scopes": [ "openid", "profile", "email" ]
        }
    },
    "UploadSettings": {
        "EnableAutoUpload": false,
        "UploadUrl": "",
        "ApiKey": "",
        "MaxFileSizeMB": 100,
        "AllowedFormats": [ "wav", "mp3", "m4a", "flac" ]
    },
    "AudioSettings": {
        "SampleRate": 16000,
        "Channels": 1,
        "BitsPerSample": 16,
        "BufferDuration": 2
    },
    "RealTimeSaveSettings": {
        "EnableRealTimeSave": true,
        "ProcessingIntervalMs": 50,
        "FlushIntervalMs": 50,
        "BufferSize": 1024,
        "EnablePerformanceMonitoring": true,
        "StatusUpdateIntervalMs": 5000
    }
}
```

## ⚙️ 配置项详解

### 1. **OAuthSettings** - OAuth认证配置

#### 主要设置
- **EnableAuthentication**: 是否启用OAuth认证功能
- **GitHub/Google**: 各提供商的OAuth配置

#### 配置示例
```json
"OAuthSettings": {
    "EnableAuthentication": true,
    "GitHub": {
        "ClientId": "your-actual-github-client-id",
        "ClientSecret": "your-actual-github-client-secret",
        "RedirectUri": "http://localhost:8081/auth/callback",
        "Scopes": [ "user", "user:email" ]
    }
}
```

#### 使用说明
1. 在GitHub/Google开发者控制台创建OAuth应用
2. 将ClientId和ClientSecret填入配置文件
3. 设置EnableAuthentication为true启用认证

### 2. **AudioSettings** - 音频录制配置

#### 主要设置
- **SampleRate**: 音频采样率（Hz），推荐16000
- **Channels**: 声道数，推荐1（单声道）
- **BitsPerSample**: 位深度，推荐16
- **BufferDuration**: 缓冲区持续时间（秒），推荐2

#### 配置示例
```json
"AudioSettings": {
    "SampleRate": 16000,
    "Channels": 1,
    "BitsPerSample": 16,
    "BufferDuration": 2
}
```

#### 性能影响
- **采样率**: 越高音质越好，但文件更大
- **声道数**: 单声道文件更小，适合语音录制
- **位深度**: 16位适合大多数应用场景

### 3. **RealTimeSaveSettings** - 实时保存配置

#### 主要设置
- **EnableRealTimeSave**: 是否启用实时保存功能
- **ProcessingIntervalMs**: 音频处理间隔（毫秒）
- **FlushIntervalMs**: 文件刷新间隔（毫秒）
- **BufferSize**: 实时缓冲区大小
- **EnablePerformanceMonitoring**: 是否启用性能监控
- **StatusUpdateIntervalMs**: 状态更新间隔（毫秒）

#### 配置示例
```json
"RealTimeSaveSettings": {
    "EnableRealTimeSave": true,
    "ProcessingIntervalMs": 50,
    "FlushIntervalMs": 50,
    "BufferSize": 1024,
    "EnablePerformanceMonitoring": true,
    "StatusUpdateIntervalMs": 5000
}
```

#### 性能调优建议
- **低延迟需求**: ProcessingIntervalMs = 25-50ms
- **高稳定性需求**: ProcessingIntervalMs = 100ms
- **平衡性能**: FlushIntervalMs = ProcessingIntervalMs

### 4. **UploadSettings** - 文件上传配置

#### 主要设置
- **EnableAutoUpload**: 是否启用自动上传
- **UploadUrl**: 上传服务器地址
- **ApiKey**: 上传API密钥
- **MaxFileSizeMB**: 最大文件大小限制
- **AllowedFormats**: 允许的文件格式

#### 配置示例
```json
"UploadSettings": {
    "EnableAutoUpload": true,
    "UploadUrl": "http://your-server.com/upload",
    "ApiKey": "your-api-key",
    "MaxFileSizeMB": 100,
    "AllowedFormats": [ "wav", "mp3" ]
}
```

## 🔧 配置管理

### 1. **配置文件位置**
```
应用程序目录/
└── appsettings.json
```

### 2. **配置热重载**
- 修改配置文件后需要重启应用程序
- 配置在应用程序启动时加载

### 3. **配置验证**
```csharp
// 检查配置是否有效
var config = ConfigurationService.Instance;
if (config.RealTimeSaveSettings.IsValid())
{
    _logger.LogInformation("配置有效");
}
else
{
    _logger.LogInformation("配置无效");
}
```

### 4. **配置摘要**
```csharp
// 获取配置摘要信息
var summary = config.RealTimeSaveSettings.GetSummary();
_logger.LogInformation(summary);
// 输出: "实时保存: 启用, 处理间隔: 50ms, 刷新间隔: 50ms, 缓冲区: 1024, 性能监控: 启用"
```

## 📊 配置性能影响

### 实时保存性能对比

| 配置 | 延迟 | 稳定性 | CPU使用 | 内存使用 |
|------|------|--------|---------|----------|
| 25ms处理间隔 | 极低 | 一般 | 高 | 中 |
| 50ms处理间隔 | 低 | 好 | 中 | 中 |
| 100ms处理间隔 | 中 | 很好 | 低 | 低 |

### 音频质量配置对比

| 配置 | 文件大小 | 音质 | 处理速度 | 存储需求 |
|------|----------|------|----------|----------|
| 16kHz/16bit/单声道 | 32KB/s | 好 | 快 | 低 |
| 44.1kHz/16bit/立体声 | 176KB/s | 很好 | 中 | 中 |
| 96kHz/24bit/立体声 | 576KB/s | 优秀 | 慢 | 高 |

## 🚀 最佳实践

### 1. **生产环境配置**
```json
{
    "RealTimeSaveSettings": {
        "EnableRealTimeSave": true,
        "ProcessingIntervalMs": 100,
        "FlushIntervalMs": 100,
        "BufferSize": 2048,
        "EnablePerformanceMonitoring": true,
        "StatusUpdateIntervalMs": 10000
    }
}
```

### 2. **开发环境配置**
```json
{
    "RealTimeSaveSettings": {
        "EnableRealTimeSave": true,
        "ProcessingIntervalMs": 50,
        "FlushIntervalMs": 50,
        "BufferSize": 1024,
        "EnablePerformanceMonitoring": true,
        "StatusUpdateIntervalMs": 5000
    }
}
```

### 3. **测试环境配置**
```json
{
    "RealTimeSaveSettings": {
        "EnableRealTimeSave": false,
        "ProcessingIntervalMs": 200,
        "FlushIntervalMs": 1000,
        "BufferSize": 4096,
        "EnablePerformanceMonitoring": false,
        "StatusUpdateIntervalMs": 30000
    }
}
```

## 🔍 故障排除

### 常见配置问题

#### 1. **配置文件格式错误**
- 检查JSON语法是否正确
- 确保所有引号和括号匹配
- 验证数值类型是否正确

#### 2. **配置加载失败**
- 检查文件路径是否正确
- 确认文件权限设置
- 查看应用程序日志

#### 3. **性能问题**
- 降低ProcessingIntervalMs
- 增加BufferSize
- 禁用性能监控

### 调试配置
```csharp
// 启用详细日志
var config = ConfigurationService.Instance;
_logger.LogInformation($"音频配置: {config.AudioSettings.SampleRate}Hz, {config.AudioSettings.Channels}声道");
_logger.LogInformation($"实时保存: {config.RealTimeSaveSettings.GetSummary()}");
```

## 🎉 总结

AudioRecorder的配置系统提供了：

1. **灵活的配置管理** - 通过JSON文件轻松调整设置
2. **性能优化选项** - 根据需求调整实时保存参数
3. **音频质量控制** - 平衡音质和性能需求
4. **环境适配** - 支持开发、测试、生产不同环境
5. **配置验证** - 内置配置有效性检查

通过合理配置，你可以获得最佳的音频录制体验和系统性能！🎯
