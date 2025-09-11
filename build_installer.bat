@echo off
setlocal enabledelayedexpansion

echo ========================================
echo AudioRecorder 安装包构建脚本
echo ========================================
echo.

:: 设置版本号（在这里统一管理）
set "VERSION=1.0.3"
set "BUILD_CONFIG=Release"
set "TARGET_FRAMEWORK=net8.0-windows"
set "RUNTIME=win-x64"

echo 当前版本: %VERSION%
echo 构建配置: %BUILD_CONFIG%
echo 目标框架: %TARGET_FRAMEWORK%
echo 运行时: %RUNTIME%
echo.

:: 1. 清理之前的构建
echo 1. 清理之前的构建...
if exist "bin\%BUILD_CONFIG%\%TARGET_FRAMEWORK%\%RUNTIME%\publish" (
    rmdir /s /q "bin\%BUILD_CONFIG%\%TARGET_FRAMEWORK%\%RUNTIME%\publish"
    echo ✓ 清理完成
) else (
    echo ✓ 无需清理
)
echo.

:: 2. 构建应用程序
echo 2. 构建应用程序...
dotnet publish AudioRecorder.csproj -c %BUILD_CONFIG% -r %RUNTIME% --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false -o "bin/%BUILD_CONFIG%/%TARGET_FRAMEWORK%/%RUNTIME%/publish"

if %errorlevel% neq 0 (
    echo ❌ 应用程序构建失败
    pause
    exit /b 1
)
echo ✅ 应用程序构建成功
echo.

:: 3. 验证构建输出
echo 3. 验证构建输出...
if not exist "bin\%BUILD_CONFIG%\%TARGET_FRAMEWORK%\%RUNTIME%\publish\AudioRecorder.exe" (
    echo ❌ 主程序文件不存在
    pause
    exit /b 1
)

if not exist "bin\%BUILD_CONFIG%\%TARGET_FRAMEWORK%\%RUNTIME%\publish\appsettings.json" (
    echo ❌ 配置文件不存在
    pause
    exit /b 1
)

echo ✅ 构建输出验证通过
echo.

:: 4. 检查WiX配置中的版本号
echo 4. 检查WiX配置...
findstr /C:"Version=\"%VERSION%\"" AudioRecorder.Setup.wxs >nul
if %errorlevel% neq 0 (
    echo ❌ WiX配置文件中的版本号与当前版本不匹配
    echo 请检查 AudioRecorder.Setup.wxs 中的 Package Version 属性
    pause
    exit /b 1
)

findstr /C:"Value=\"%VERSION%\"" AudioRecorder.Setup.wxs >nul
if %errorlevel% neq 0 (
    echo ❌ WiX配置文件中的注册表版本号与当前版本不匹配
    echo 请检查 AudioRecorder.Setup.wxs 中的注册表 Version 值
    pause
    exit /b 1
)

echo ✅ WiX配置版本号检查通过
echo.

:: 5. 构建安装包
echo 5. 构建安装包...
if not exist "wix" mkdir wix

:: 使用WiX工具构建MSI
wix build AudioRecorder.Setup.wxs -o "wix\AudioRecorder-%VERSION%-Setup.msi"

if %errorlevel% neq 0 (
    echo ❌ 安装包构建失败
    echo.
    echo 可能的原因：
    echo 1. WiX工具未安装或未在PATH中
    echo 2. WiX配置文件有语法错误
    echo 3. 引用的文件不存在
    echo.
    echo 请检查错误信息并修复问题
    pause
    exit /b 1
)

echo ✅ 安装包构建成功
echo.

:: 6. 验证安装包
echo 6. 验证安装包...
if not exist "wix\AudioRecorder-%VERSION%-Setup.msi" (
    echo ❌ 安装包文件不存在
    pause
    exit /b 1
)

:: 获取文件大小
for %%A in ("wix\AudioRecorder-%VERSION%-Setup.msi") do (
    set "FILE_SIZE=%%~zA"
)

echo ✅ 安装包验证通过
echo   文件: wix\AudioRecorder-%VERSION%-Setup.msi
echo   大小: !FILE_SIZE! 字节
echo.

:: 7. 显示构建摘要
echo ========================================
echo 构建摘要
echo ========================================
echo 版本: %VERSION%
echo 安装包: wix\AudioRecorder-%VERSION%-Setup.msi
echo 大小: !FILE_SIZE! 字节
echo.
echo 重复安装防护功能：
echo ✓ 版本检查
echo ✓ 注册表检查  
echo ✓ 文件存在检查
echo ✓ MSI升级逻辑
echo.
echo 测试建议：
echo 1. 运行 test_duplicate_install.bat 检查当前状态
echo 2. 安装此版本
echo 3. 再次尝试安装应该被阻止
echo 4. 卸载后可以重新安装
echo ========================================

echo.
echo 构建完成！按任意键退出...
pause >nul