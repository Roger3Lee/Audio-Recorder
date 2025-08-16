# Audio Recorder URL协议使用说明

## 功能概述

现在您的Audio Recorder应用程序支持通过浏览器URL协议直接启动！这意味着您可以从任何网页或HTML文件中点击链接来启动桌面应用程序。

## 支持的协议格式

### 基本启动
```
audiorecorder://                    // 启动应用程序
```

### 录音控制
```
audiorecorder://action=start       // 启动并开始录音
audiorecorder://action=stop        // 停止录音
audiorecorder://action=status      // 获取状态
```

### 高级设置
```
audiorecorder://volume=50          // 设置音量到50%
audiorecorder://quality=high       // 设置高质量录音
audiorecorder://format=wav         // 设置WAV格式
```

## 使用方法

### 1. 自动注册（推荐）
首次运行Audio Recorder应用程序时，它会自动注册URL协议。无需手动操作。

### 2. 手动注册（如果需要）
如果自动注册失败，可以运行 `register_protocol.bat` 文件来手动注册协议。

**注意：** 手动注册需要管理员权限。

### 3. 测试协议
打开 `protocol_test.html` 文件，点击各种协议链接来测试功能。

## 在网页中使用

### HTML链接示例
```html
<!-- 基本启动 -->
<a href="audiorecorder://">启动录音器</a>

<!-- 启动并开始录音 -->
<a href="audiorecorder://action=start">开始录音</a>

<!-- 停止录音 -->
<a href="audiorecorder://action=stop">停止录音</a>

<!-- 带参数的启动 -->
<a href="audiorecorder://volume=80&quality=high">高质量录音</a>
```

### JavaScript调用示例
```javascript
// 启动应用程序
function launchApp() {
    window.location.href = 'audiorecorder://';
}

// 开始录音
function startRecording() {
    window.location.href = 'audiorecorder://action=start';
}

// 停止录音
function stopRecording() {
    window.location.href = 'audiorecorder://action=stop';
}

// 带参数启动
function launchWithSettings() {
    window.location.href = 'audiorecorder://volume=75&format=mp3';
}
```

## 工作原理

1. **协议注册**：应用程序在Windows注册表中注册 `audiorecorder://` 协议
2. **浏览器识别**：当浏览器遇到此协议时，会调用系统默认处理程序
3. **应用程序启动**：系统启动Audio Recorder.exe并传递URL参数
4. **参数处理**：应用程序解析URL参数并执行相应操作

## 故障排除

### 常见问题

1. **点击链接没有反应**
   - 确保Audio Recorder应用程序已经运行过（用于注册协议）
   - 检查协议是否已注册：运行 `register_protocol.bat`

2. **浏览器显示安全警告**
   - 这是正常现象，选择"允许"即可
   - 某些浏览器可能会阻止自定义协议

3. **协议未注册**
   - 以管理员权限运行 `register_protocol.bat`
   - 或者重新运行Audio Recorder应用程序

4. **应用程序无法启动**
   - 检查AudioRecorder.exe是否存在
   - 确认有足够的权限运行应用程序

### 验证协议注册
在命令提示符中运行：
```cmd
reg query "HKEY_CLASSES_ROOT\audiorecorder"
```

如果显示注册表项，说明协议已注册成功。

## 安全注意事项

- URL协议允许从网页启动本地应用程序
- 确保只信任可信的网站
- 某些浏览器可能会阻止或警告自定义协议
- 建议在受控环境中使用此功能

## 技术细节

### 注册表位置
```
HKEY_CLASSES_ROOT\audiorecorder\
├── (默认) = "Audio Recorder Protocol"
├── URL Protocol = ""
└── shell\open\command\
    └── (默认) = "C:\path\to\AudioRecorder.exe" "%1"
```

### 参数传递
- `%1` 是完整的URL参数
- 应用程序通过 `Main(string[] args)` 接收参数
- 支持查询字符串格式的参数解析

## 扩展功能

您可以根据需要扩展协议支持：

1. **添加更多动作**：如暂停、恢复、设置等
2. **支持复杂参数**：如录音时长、文件路径等
3. **集成WebSocket**：结合现有的WebSocket服务器功能
4. **多语言支持**：根据浏览器语言自动选择界面语言

## 示例应用场景

1. **远程控制**：从网页远程启动和控制录音
2. **快捷操作**：在文档或邮件中添加录音快捷链接
3. **自动化工作流**：与其他系统集成，自动启动录音
4. **用户友好**：提供多种启动应用程序的方式

## 版本信息

- 功能版本：1.1.0
- 支持平台：Windows 10/11
- 协议名称：audiorecorder://
- 开发环境：.NET 8.0
