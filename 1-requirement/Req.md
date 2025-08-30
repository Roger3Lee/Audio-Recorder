# AudioRecorder项目需求文档 (CLEAR框架)

## 按照CLEAR框架总结的AudioRecorder项目提示词

### **C - Context (上下文)**
这是一个专业的桌面音频录制控制器项目，基于C# .NET 8.0开发，专门用于Windows系统的音频录制和管理。项目集成了多种控制方式，包括桌面GUI、WebSocket远程控制、URL协议调用等，为音频录制提供了完整的解决方案。

### **L - Language (语言)**
- **主要语言**: C# (.NET 8.0)
- **标记语言**: XML (WiX安装包配置)
- **脚本语言**: Batch (.bat构建脚本)
- **前端**: HTML/JavaScript (WebSocket测试页面)
- **文档**: Markdown (中文)

### **E - Environment (环境)**
- **操作系统**: Windows 10/11 (x64)
- **运行时**: .NET 8.0 Runtime
- **开发环境**: Visual Studio / .NET CLI
- **依赖库**: NAudio (音频处理), Newtonsoft.Json (JSON序列化)
- **构建工具**: WiX Toolset (MSI安装包)

### **A - Architecture (架构)**
```
AudioRecorder/
├── 核心组件
│   ├── SimpleAudioRecorder.cs - 音频录制引擎
│   ├── DesktopRecorderWindow.cs - 桌面GUI界面
│   ├── SimpleWebSocketServer.cs - WebSocket服务器
│   └── UrlProtocolHandler.cs - URL协议处理器
├── 音频处理
│   ├── 实时音频监控
│   ├── 自动增益控制(AGC)
│   ├── 智能降噪算法
│   └── 动态压缩处理
├── 控制接口
│   ├── 桌面置顶窗口
│   ├── WebSocket远程控制
│   ├── URL协议启动
│   └── 命令行参数
└── 部署包
    ├── MSI安装包
    ├── 自动协议注册
    └── 系统集成
```

### **R - Requirements (需求)**
- **功能需求**:
  - 系统音频和麦克风分离录制
  - 实时音频电平监控
  - 多种启动和控制方式
  - 专业级音频处理
  - 自动音量平衡

- **非功能需求**:
  - 高性能音频处理 (16kHz, 16bit, 单声道)
  - 低延迟实时监控
  - 跨平台兼容性 (Windows)
  - 用户友好的安装体验
  - 完整的卸载清理

- **集成需求**:
  - 系统音频设备访问
  - Windows注册表集成
  - WebSocket网络通信
  - URL协议系统集成
  - 开始菜单和桌面快捷方式

## 项目概述

这个项目是一个功能完整、架构清晰的音频录制解决方案，特别适合需要专业音频录制和多种控制方式的用户场景。

### 主要特性
- **桌面置顶窗口**: 小窗口始终置顶，不影响其他程序使用
- **实时音频监控**: 显示系统音频和麦克风的实时电平
- **一键录音控制**: 简单的开始/停止录音按钮
- **WebSocket远程控制**: 支持通过WebSocket进行远程控制
- **专业音频处理**: 内置降噪、AGC、压缩等音频处理功能
- **URL协议支持**: 支持通过浏览器URL协议直接启动和控制
- **完整安装包**: 专业的MSI安装包，支持自动协议注册

### 技术架构
- 基于.NET 8.0的现代C#应用程序
- 使用NAudio库进行专业音频处理
- 集成WebSocket服务器提供网络控制接口
- 支持Windows URL协议系统集成
- 使用WiX Toolset构建专业安装包

### 应用场景
- 远程音频录制控制
- 专业音频内容制作
- 系统音频监控和录制
- 自动化音频处理工作流
- 多平台音频控制集成
