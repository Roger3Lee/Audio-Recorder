# AudioRecorder 上传功能说明文档

## 📋 功能概述

根据Req2.md的需求，AudioRecorder现在支持将录制的音频文件自动上传到指定的服务器。该功能实现了以下核心要求：

- ✅ **同时上传系统音频和麦克风音频**：录音完成后自动上传两个分离的音频文件
- ✅ **录音完成后自动上传**：录音停止后自动上传完整的音频文件
- ✅ **配置上传接口地址**：通过配置文件管理上传服务器设置
- ✅ **打包时包含配置文件**：MSI安装包自动包含配置文件

## 🏗️ 架构设计

### 新增组件

```
AudioRecorder/
├── 新增模型
│   ├── Models/UploadSettings.cs - 上传配置模型
│   └── Models/AudioSettings.cs - 音频设置模型
├── 新增服务
│   ├── Services/ConfigurationService.cs - 配置管理服务
│   ├── Services/AudioFileUploadService.cs - 文件上传服务
│   └── Services/ConsoleLogger.cs - 日志记录服务
├── 配置文件
│   └── appsettings.json - 应用程序配置
└── 测试页面
    └── upload_test.html - 上传功能测试页面
```

### 核心流程

1. **录音开始** → 创建音频文件并记录路径
2. **录音进行中** → 持续录制音频到文件
3. **录音停止** → 保存完整音频文件并自动上传
4. **状态反馈** → 显示上传进度和结果

## ⚙️ 配置说明

### 配置文件结构

```json
{
  "UploadSettings": {
    "ServerUrl": "http://10.10.21.67:38080",
    "ApiEndpoint": "/admin-api/asr/file/upload-multiple",
    "AuthorizationToken": "01809869aa1b4c98903495da6e00e11c",
    "BizType": "asr",
    "MergeAudio": true,
    "EnableAutoUpload": true,
    "UploadTimeout": 30000,
    "RetryCount": 3,
    "RetryDelay": 5000
  },
  "AudioSettings": {
    "SampleRate": 16000,
    "Channels": 1,
    "BitsPerSample": 16,
    "BufferDuration": 2
  }
}
```

### 配置参数说明

| 参数 | 说明 | 默认值 |
|------|------|--------|
| `ServerUrl` | 上传服务器基础地址 | `http://10.10.21.67:38080` |
| `ApiEndpoint` | API端点路径 | `/admin-api/asr/file/upload-multiple` |
| `AuthorizationToken` | 授权令牌 | `01809869aa1b4c98903495da6e00e11c` |
| `BizType` | 业务类型标识 | `asr` |
| `MergeAudio` | 是否合并音频 | `true` |
| `EnableAutoUpload` | 是否启用自动上传 | `true` |

| `UploadTimeout` | 上传超时时间(ms) | `30000` |
| `RetryCount` | 重试次数 | `3` |
| `RetryDelay` | 重试延迟(ms) | `5000` |

## 🚀 使用方法

### 1. 自动上传（推荐）

录音完成后，系统会自动上传音频文件：

1. 开始录音 → 系统开始录制系统音频和麦克风音频
2. 录音进行中 → 持续录制音频到文件
3. 停止录音 → 系统保存音频文件并自动开始上传
4. 上传完成 → 在控制台显示上传结果

### 2. 手动控制

通过WebSocket接口手动控制：

```javascript
// 开始录音
ws.send(JSON.stringify({ Command: "start_recording" }));

// 停止录音（会自动触发上传）
ws.send(JSON.stringify({ Command: "stop_recording" }));

// 获取状态
ws.send(JSON.stringify({ Command: "get_status" }));
```

### 3. 配置管理

#### 运行时更新配置

```csharp
var config = ConfigurationService.Instance;
var newSettings = new UploadSettings
{
    ServerUrl = "http://new-server:8080",
    ApiEndpoint = "/api/upload",
    AuthorizationToken = "new-token",
    // ... 其他设置
};
config.UpdateUploadSettings(newSettings);
```

#### 重新加载配置

```csharp
ConfigurationService.Instance.ReloadConfiguration();
```

## 📤 上传实现细节

### 文件上传流程

1. **文件准备**
   - 等待录音文件写入完成（1秒延迟）
   - 验证文件存在性和完整性

2. **HTTP请求构建**
   - 使用 `multipart/form-data` 格式
   - 同时上传两个音频文件
   - 设置正确的Content-Type和授权头

### 文件上传请求示例

```bash
curl --request POST \
  --url http://10.10.21.67:38080/admin-api/asr/file/upload-multiple \
  --header 'Authorization: 01809869aa1b4c98903495da6e00e11c' \
  --header 'content-type: multipart/form-data' \
  --form 'files=@SystemAudio_20241201_143022.wav' \
  --form 'files=@Microphone_20241201_143022.wav' \
  --form bizType=asr \
  --form mergeAudio=true
```

## 🧪 测试方法

### 1. 使用测试页面

打开 `upload_test.html` 文件，可以：

- 测试WebSocket连接
- 控制录音开始/停止
- 验证上传配置
- 查看操作日志

### 2. 控制台监控

运行程序后，控制台会显示：

```
✅ 配置文件加载成功: C:\Program Files\AudioRecorder\appsettings.json
🚀 开始上传音频文件...
📤 上传尝试 1/3...
📡 正在上传到服务器...
✅ 音频文件上传成功！
系统音频: SystemAudio_20241201_143022.wav
麦克风: Microphone_20241201_143022.wav
```

### 3. 文件验证

检查桌面 `AudioRecordings` 文件夹：

```
AudioRecordings/
├── SystemAudio_20241201_143022.wav
├── Microphone_20241201_143022.wav
└── ...
```

## 🔧 故障排除

### 常见问题

1. **上传失败**
   - 检查网络连接和服务器状态
   - 验证授权令牌是否有效
   - 确认服务器地址和端口正确

2. **配置文件问题**
   - 确保 `appsettings.json` 存在且格式正确
   - 检查文件权限和路径设置
   - 验证JSON语法是否正确

3. **音频文件问题**
   - 确认录音功能正常工作
   - 检查磁盘空间是否充足
   - 验证音频文件格式支持

### 调试方法

1. **启用详细日志**
   - 查看控制台输出
   - 检查WebSocket连接状态
   - 监控HTTP请求响应

2. **配置验证**
   - 使用测试页面验证配置
   - 检查网络连接
   - 验证服务器接口

## 📦 部署说明

### 安装包配置

MSI安装包会自动：

1. 包含 `appsettings.json` 配置文件
2. 安装到 `C:\Program Files\AudioRecorder\` 目录
3. 创建开始菜单和桌面快捷方式
4. 注册URL协议和WebSocket服务

### 配置文件位置

```
C:\Program Files\AudioRecorder\appsettings.json
```

### 运行时配置

程序启动时会：

1. 自动加载配置文件
2. 如果配置文件不存在，创建默认配置
3. 初始化上传服务（如果启用）
4. 注册WebSocket服务器

## 🔮 未来扩展

### 计划功能

1. **批量上传**
   - 支持多个录音文件批量上传
   - 队列管理和进度跟踪

2. **高级配置**
   - 支持环境变量配置
   - 动态配置更新
   - 配置验证和测试

3. **上传历史**
   - 记录上传历史
   - 失败重试管理
   - 统计信息显示

4. **安全增强**
   - 支持HTTPS上传
   - 证书验证
   - 加密传输

## 📞 技术支持

如遇到问题，请：

1. 查看控制台日志输出
2. 检查配置文件设置
3. 验证网络连接状态
4. 确认服务器接口可用性

---

**版本**: 1.1.0  
**更新日期**: 2024-12-01  
**功能状态**: ✅ 已完成并测试通过
