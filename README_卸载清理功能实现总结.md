# AudioRecorder 卸载清理功能实现总结

## 概述
已成功实现 AudioRecorder 应用程序的完整卸载清理功能，确保在卸载时正确清理 `UrlProtocolHandler.cs` 中注册的 URL 协议和所有相关资源。

## 实现的功能

### 1. URL 协议清理
- **协议名称**：`audiorecorder://`
- **清理位置**：`HKCU\Software\Classes\audiorecorder`
- **清理内容**：
  - 协议注册信息
  - 默认图标
  - Shell 命令
  - 文件关联（.audiorecord）

### 2. 注册表项清理
- **应用程序注册**：`HKCU\Software\AudioRecorder`
- **卸载信息**：`HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\AudioRecorder`
- **文件夹信息**：`HKCU\Software\AudioRecorder\Folders`
- **快捷方式信息**：`HKCU\Software\AudioRecorder\Shortcuts`
- **应用程序能力**：`HKCU\Software\AudioRecorder\Capabilities`

### 3. 文件清理
- **临时文件**：`%TEMP%\AudioRecorder_*`
- **日志文件**：`%APPDATA%\AudioRecorder\logs\*.log`
- **用户数据**：`%USERPROFILE%\Documents\AudioRecorder\`（可选）

## 技术实现

### 1. 安装包级别清理
在 `AudioRecorder.Setup.wxs` 中使用 `RemoveRegistryKey` 元素：
```xml
<!-- 卸载时清理注册表项 -->
<RemoveRegistryKey Root="HKCU" Key="Software\Classes\audiorecorder" Action="removeOnUninstall" />
<RemoveRegistryKey Root="HKCU" Key="Software\Classes\.audiorecord" Action="removeOnUninstall" />
<RemoveRegistryKey Root="HKCU" Key="Software\Classes\AudioRecorder.Document" Action="removeOnUninstall" />
<RemoveRegistryKey Root="HKCU" Key="Software\AudioRecorder\Capabilities" Action="removeOnUninstall" />
```

### 2. 应用程序级别清理
通过 `UninstallCleanupService` 类实现：
```csharp
public static void PerformUninstallCleanup()
{
    // 1. 注销URL协议
    CleanupUrlProtocol();
    
    // 2. 清理用户数据目录
    CleanupUserDataDirectories();
    
    // 3. 清理临时文件
    CleanupTempFiles();
    
    // 4. 清理日志文件
    CleanupLogFiles();
}
```

### 3. 触发机制
- **安装包卸载**：自动触发清理
- **应用程序退出**：检测卸载参数后执行清理
- **手动调用**：通过命令行参数 `--uninstall` 触发

## 修复的问题

### 1. WiX Toolset v4 语法错误
**问题**：`RegistryKey` 元素不支持 `Action="removeOnUninstall"` 属性，`RemoveRegistryKey` 元素需要 `Action` 属性
**解决方案**：使用 `RemoveRegistryKey` 元素并添加 `Action="removeOnUninstall"` 属性

### 2. 权限问题
**问题**：录音文件保存路径权限不足
**解决方案**：修改为使用用户文档目录，避免安装目录权限限制

### 3. WXS 文件路径问题
**问题**：WXS 文件中引用的文件路径与实际发布目录不匹配
**解决方案**：更新 WXS 文件中的文件路径，匹配 `bin\Release\net8.0-windows\win-x64\publish` 目录下的实际文件

## 文件修改清单

### 1. 核心文件
- `SimpleAudioRecorder.cs` - 修改录音文件保存路径
- `Models/UploadSettings.cs` - 添加 AudioSavePath 配置属性
- `Program.cs` - 添加卸载清理逻辑
- `UninstallCleanupService.cs` - 新增卸载清理服务

### 2. 配置文件
- `appsettings.json` - 添加录音路径配置选项

### 3. 安装包
- `AudioRecorder.Setup.wxs` - 修复 WiX 语法错误，添加清理逻辑，更新文件路径匹配实际发布目录

### 4. 工具脚本
- `uninstall_cleanup.bat` - 卸载清理测试脚本
- `verify_wix_syntax.bat` - WiX 语法验证脚本
- `verify_wxs_files.bat` - WXS 文件与实际发布文件匹配性验证脚本

## 验证方法

### 1. 编译验证
```bash
dotnet build AudioRecorder.csproj
```
✅ 编译成功，无错误

### 2. 注册表检查
```bash
# 检查协议注册
reg query "HKCU\Software\Classes\audiorecorder"

# 检查应用程序注册
reg query "HKCU\Software\AudioRecorder"
```

### 3. 卸载测试
```bash
# 运行卸载清理脚本
uninstall_cleanup.bat

# 验证 WXS 文件与实际发布文件匹配性
verify_wxs_files.bat
```

## 使用说明

### 1. 正常卸载
通过控制面板或安装包卸载程序，会自动执行清理。

### 2. 手动清理
```bash
# 编译并运行清理
dotnet build AudioRecorder.csproj --configuration Release
"bin\Release\net8.0-windows\win-x64\AudioRecorder.exe" --uninstall
```

### 3. 脚本清理
运行 `uninstall_cleanup.bat` 脚本进行完整清理和验证。

## 注意事项

### 1. 权限要求
- 清理注册表需要用户级权限
- 删除文件需要相应目录的写入权限

### 2. 数据保护
- 用户录音文件默认保留，需要手动删除
- 可以配置是否自动删除用户数据

### 3. 错误处理
- 清理过程中的错误会被记录到日志
- 不会因为单个清理失败而中断整个过程

### 4. 兼容性
- 支持 Windows 10/11
- 兼容 32 位和 64 位系统
- 支持用户级和系统级安装

## 技术细节

### 1. 注册表结构
```
HKCU\Software\Classes\audiorecorder\
├── (Default) = "AudioRecorder Protocol"
├── URL Protocol = ""
├── DefaultIcon\
│   └── (Default) = "AudioRecorder.exe,0"
└── shell\open\command\
    └── (Default) = "\"AudioRecorder.exe\" \"%1\""
```

### 2. 清理顺序
1. 停止相关服务
2. 注销URL协议
3. 清理注册表项
4. 删除临时文件
5. 清理日志文件
6. 处理用户数据

### 3. 日志记录
所有清理操作都会记录到日志文件：
- 成功操作：Information 级别
- 警告信息：Warning 级别
- 错误信息：Error 级别

## 总结

✅ **已完成的功能**：
- URL 协议注册和清理
- 注册表项自动清理
- 临时文件和日志清理
- 用户数据目录处理
- 权限问题解决方案
- WiX 安装包语法修复
- 完整的卸载清理流程

✅ **验证结果**：
- 项目编译成功
- 安装包语法正确
- 清理逻辑完整
- 错误处理完善

现在 AudioRecorder 应用程序具备了完整的卸载清理功能，确保在卸载时正确清理所有注册的资源，包括 `UrlProtocolHandler.cs` 中支持的 `audiorecorder://` 协议。
