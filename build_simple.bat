@echo off
chcp 65001 >nul
echo ========================================
echo Audio Recorder 安装包构建工具
echo ========================================
echo.

echo 设置环境变量...
set WIX_TEMP=%TEMP%\wix_build
if not exist "%WIX_TEMP%" mkdir "%WIX_TEMP%"
set WIX_CACHE=%TEMP%\wix_cache
if not exist "%WIX_CACHE%" mkdir "%WIX_CACHE%"

echo 检查 WiX Toolset v4...
wix --version >nul 2>&1
if %errorlevel% neq 0 (
    echo ❌ 错误：未找到 WiX Toolset v4
    echo 请先安装：
    echo dotnet tool install --global wix
    pause
    exit /b 1
)

echo ✅ 找到 WiX Toolset v4
wix --version
echo.

echo 构建应用程序...
dotnet publish AudioRecorder.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true

if %errorlevel% neq 0 (
    echo ❌ 应用程序构建失败！
    pause
    exit /b 1
)

echo ✅ 应用程序构建成功！
echo.

echo 创建应用程序图标...
call create_icon_simple.bat
if %errorlevel% neq 0 (
    echo ⚠️ 图标创建失败，使用占位符图标
)

echo 检查WiX源文件...
if not exist "AudioRecorder.Setup.wxs" (
    echo ❌ 错误：未找到 AudioRecorder.Setup.wxs 文件
    echo 请确保 WiX 源文件存在
    pause
    exit /b 1
)

echo ✅ WiX源文件检查成功！
echo.

echo 清理临时文件...
if exist "%WIX_TEMP%" rmdir /s /q "%WIX_TEMP%" 2>nul
if exist "%WIX_CACHE%" rmdir /s /q "%WIX_CACHE%" 2>nul

echo 清理旧安装包...
if exist "AudioRecorder*.msi" del "AudioRecorder*.msi" /q
if exist "*.wixpdb" del "*.wixpdb" /q

echo 构建完整版安装包...
wix build AudioRecorder.Setup.wxs -out AudioRecorder_Complete.msi

if %errorlevel% neq 0 (
    echo ❌ 安装包构建失败！
    echo.
    echo 尝试使用备用方法...
    echo 使用 MSBuild 构建...
    
    dotnet build AudioRecorder.Setup.wixproj
    
    if %errorlevel% neq 0 (
        echo ❌ 备用构建方法也失败了！
        pause
        exit /b 1
    )
)

echo ✅ 安装包构建成功！
echo.

echo 查找生成的安装包...
if exist "AudioRecorder_Complete.msi" (
    echo 📦 生成的安装包：AudioRecorder_Complete.msi
    echo 📁 安装包大小：
    for %%A in ("AudioRecorder_Complete.msi") do echo    %%~zA 字节
    echo.
    echo ✨ 安装包功能特性：
    echo    ✅ 用户可选择安装目录
    echo    ✅ 完整的安装/卸载界面
    echo    ✅ 开始菜单快捷方式
    echo    ✅ 桌面快捷方式
    echo    ✅ 防止重复安装
    echo    ✅ 自动版本检测和升级
    echo    ✅ 自定义应用程序图标
    echo    ✅ audiorecorder:// 协议支持
    echo    ✅ .audiorecord 文件关联
    echo    ✅ 安装完成后自动注册协议
) else (
    echo ❌ 未找到生成的安装包文件
    pause
    exit /b 1
)

:found
echo.
echo ========================================
echo 🎉 构建完成！
echo ========================================
echo.
echo 安装包已生成在当前目录中
echo.
echo 下一步：
echo 1. 测试安装包
echo 2. 分发给用户
echo 3. 验证URL协议注册功能
echo.
echo 按任意键退出...
pause >nul
