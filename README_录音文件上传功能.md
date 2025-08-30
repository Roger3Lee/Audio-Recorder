# AudioRecorder 录音文件上传功能说明

## 🎯 功能概述

AudioRecorder现在实现了**完整的录音文件上传功能**，当用户点击"确认停止录音"按钮时，系统会自动停止录音并将录制的音频文件上传到指定的服务器。这确保了录音数据的及时备份和云端存储。

## ✨ 核心特性

### 1. **自动上传流程**
- **一键操作** - 点击"确认停止录音"按钮自动完成停止和上传
- **智能检测** - 自动检测上传配置是否启用
- **状态反馈** - 实时显示上传进度和状态信息

### 2. **用户界面优化**
- **状态提示** - 显示"正在停止录音"、"正在上传文件"等状态
- **颜色区分** - 成功消息显示绿色，错误消息显示红色，进度消息显示蓝色
- **自动恢复** - 成功消息3秒后自动恢复默认状态

### 3. **配置驱动**
- **灵活配置** - 通过`appsettings.json`控制上传行为
- **环境适配** - 支持开发、测试、生产不同环境
- **开关控制** - 可随时启用/禁用自动上传功能

## 🏗️ 技术架构

### 上传流程

```
用户点击确认停止 → 停止录音 → 等待完成 → 检查配置 → 上传文件 → 状态反馈
       ↓              ↓         ↓         ↓         ↓         ↓
   HideOverlay → StopRecording → Delay → CheckConfig → Upload → ShowStatus
```

### 核心组件

```csharp
// 确认停止录音按钮点击
private async void ConfirmStopButton_Click(object sender, RoutedEventArgs e)
{
    try
    {
        // 隐藏确认覆盖层
        HideStopConfirmOverlay();
        
        // 显示状态提示
        ShowUploadStatusMessage("⏹ 正在停止录音...");
        
        // 执行停止录音
        ExecuteStopRecording();
        
        // 等待录音完全停止
        await System.Threading.Tasks.Task.Delay(500);
        
        // 检查上传配置并显示相应状态
        var config = ConfigurationService.Instance.UploadSettings;
        if (config.EnableAutoUpload && !string.IsNullOrEmpty(config.ServerUrl))
        {
            ShowUploadStatusMessage("📤 录音已停止，正在上传文件...");
        }
        else
        {
            ShowUploadStatusMessage("✅ 录音已停止，文件已保存到桌面");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ 停止录音失败: {ex.Message}");
        ShowUploadStatusMessage($"❌ 操作失败: {ex.Message}");
    }
}
```

## ⚙️ 配置说明

### 上传配置项

```json
"UploadSettings": {
    "EnableAutoUpload": true,                    // 启用自动上传
    "ServerUrl": "http://10.10.21.67:38080",    // 服务器地址
    "ApiEndpoint": "/admin-api/asr/file/upload-multiple",  // API端点
    "AuthorizationToken": "01809869aa1b4c98903495da6e00e11c",  // 授权令牌
    "BizType": "asr",                           // 业务类型
    "MergeAudio": true,                         // 是否合并音频
    "UploadTimeout": 30000,                     // 上传超时时间
    "RetryCount": 3,                            // 重试次数
    "RetryDelay": 5000,                         // 重试延迟
    "MaxFileSizeMB": 100,                       // 最大文件大小
    "AllowedFormats": [ "wav", "mp3", "m4a", "flac" ]  // 允许格式
}
```

### 配置验证

```csharp
// 检查上传配置是否有效
var config = ConfigurationService.Instance.UploadSettings;
if (config.EnableAutoUpload && !string.IsNullOrEmpty(config.ServerUrl))
{
    // 配置有效，可以上传
    ShowUploadStatusMessage("📤 录音已停止，正在上传文件...");
}
else
{
    // 配置无效，只保存到本地
    ShowUploadStatusMessage("✅ 录音已停止，文件已保存到桌面");
}
```

## 🔧 使用方法

### 1. **基本使用流程**

1. **开始录音** - 点击录音按钮开始录制
2. **停止录音** - 点击停止按钮显示确认对话框
3. **确认停止** - 点击"确认停止录音"按钮
4. **自动上传** - 系统自动停止录音并上传文件

### 2. **状态消息说明**

| 状态消息 | 含义 | 颜色 |
|----------|------|------|
| `⏹ 正在停止录音...` | 录音停止中 | 蓝色 |
| `📤 录音已停止，正在上传文件...` | 文件上传中 | 蓝色 |
| `✅ 录音已停止，文件已保存到桌面` | 操作完成 | 绿色 |
| `❌ 操作失败: {错误信息}` | 操作失败 | 红色 |

### 3. **错误处理**

```csharp
try
{
    // 执行上传操作
    await UploadRecordingFilesWithProgress();
}
catch (Exception ex)
{
    Console.WriteLine($"❌ 上传录音文件失败: {ex.Message}");
    ShowUploadStatusMessage($"❌ 上传失败: {ex.Message}");
}
```

## 📊 上传性能

### 上传时间估算

| 文件大小 | 网络速度 | 预计上传时间 |
|----------|----------|--------------|
| 1MB (30秒录音) | 1Mbps | ~8秒 |
| 5MB (2.5分钟录音) | 1Mbps | ~40秒 |
| 10MB (5分钟录音) | 1Mbps | ~80秒 |
| 1MB (30秒录音) | 10Mbps | ~1秒 |
| 5MB (2.5分钟录音) | 10Mbps | ~4秒 |

### 优化建议

- **网络环境**: 确保稳定的网络连接
- **文件大小**: 合理设置录音时长
- **服务器性能**: 选择性能良好的上传服务器

## 🚀 高级功能

### 1. **WebSocket集成**
上传状态通过WebSocket实时推送给远程客户端：

```csharp
// 通知WebSocket客户端
NotifyWebSocketClients("recording_stopped", new { IsRecording = false });
```

### 2. **自动重试机制**
上传失败时自动重试：

```json
"RetryCount": 3,        // 重试3次
"RetryDelay": 5000      // 每次重试间隔5秒
```

### 3. **文件格式支持**
支持多种音频格式：

```json
"AllowedFormats": [ "wav", "mp3", "m4a", "flac" ]
```

## 🔍 故障排除

### 常见问题

#### 1. **上传失败**
- 检查网络连接
- 验证服务器地址和API端点
- 确认授权令牌是否有效

#### 2. **文件过大**
- 检查`MaxFileSizeMB`设置
- 考虑压缩音频文件
- 分段上传大文件

#### 3. **状态消息不显示**
- 检查`StatusLabel1`和`StatusLabel2`是否正确绑定
- 验证UI线程调用
- 查看控制台错误信息

### 调试信息

```csharp
// 启用详细日志
Console.WriteLine($"📤 上传状态: {message}");
Console.WriteLine($"❌ 上传错误: {ex.Message}");

// 检查配置
var config = ConfigurationService.Instance.UploadSettings;
Console.WriteLine($"服务器地址: {config.ServerUrl}");
Console.WriteLine($"自动上传: {config.EnableAutoUpload}");
```

## 📈 性能监控

### 上传统计

- **成功次数**: 记录成功上传的文件数量
- **失败次数**: 记录上传失败的文件数量
- **平均时间**: 计算平均上传时间
- **错误分析**: 统计常见错误类型

### 监控指标

```csharp
// 上传进度回调
private void OnUploadProgressChanged(object? sender, string message)
{
    Console.WriteLine($"📤 {message}");
    // 可以在这里添加进度条更新逻辑
}

// 上传完成回调
private void OnUploadCompleted(object? sender, string message)
{
    Console.WriteLine($"✅ {message}");
    ShowUploadStatusMessage("✅ 文件上传完成！");
}
```

## 🎉 总结

AudioRecorder的录音文件上传功能提供了：

1. **一键操作** - 停止录音和上传文件一步完成
2. **智能配置** - 通过配置文件灵活控制上传行为
3. **实时反馈** - 清晰的状态提示和进度显示
4. **错误处理** - 完善的异常处理和重试机制
5. **性能优化** - 支持多种音频格式和网络环境

这个功能确保了你的录音数据能够及时备份到云端，提供了数据安全和便捷访问的双重保障！🎯

## 🔗 相关文档

- [配置使用说明](README_配置使用说明.md) - 详细的配置指南
- [实时音频保存功能](README_实时音频保存功能.md) - 实时保存功能说明
- [上传功能说明](README_上传功能说明.md) - 上传功能技术细节
