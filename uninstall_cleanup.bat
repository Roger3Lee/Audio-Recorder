@echo off
echo AudioRecorder 卸载清理脚本
echo ==============================

echo.
echo 1. 检查当前安装状态...
reg query "HKCU\Software\Classes\audiorecorder" >nul 2>&1
if %errorlevel% equ 0 (
    echo [√] 发现 audiorecorder:// 协议注册
) else (
    echo [×] 未发现 audiorecorder:// 协议注册
)

echo.
echo 2. 执行卸载清理...
echo 正在调用应用程序执行清理...

REM 编译项目
dotnet build AudioRecorder.csproj --configuration Release

REM 以卸载模式运行应用程序
"bin\Release\net8.0-windows\win-x64\AudioRecorder.exe" --uninstall

echo.
echo 3. 验证清理结果...
reg query "HKCU\Software\Classes\audiorecorder" >nul 2>&1
if %errorlevel% equ 0 (
    echo [×] audiorecorder:// 协议仍然存在，需要手动清理
) else (
    echo [√] audiorecorder:// 协议已成功清理
)

echo.
echo 4. 手动清理注册表项（如果需要）...
echo 正在清理剩余的注册表项...

REM 清理协议注册
reg delete "HKCU\Software\Classes\audiorecorder" /f >nul 2>&1
reg delete "HKCU\Software\Classes\.audiorecord" /f >nul 2>&1
reg delete "HKCU\Software\Classes\AudioRecorder.Document" /f >nul 2>&1

REM 清理应用程序注册
reg delete "HKCU\Software\AudioRecorder\Capabilities" /f >nul 2>&1
reg delete "HKCU\Software\AudioRecorder" /f >nul 2>&1

echo.
echo 5. 最终验证...
reg query "HKCU\Software\Classes\audiorecorder" >nul 2>&1
if %errorlevel% equ 0 (
    echo [×] 仍有注册表项残留
) else (
    echo [√] 所有注册表项已清理完成
)

echo.
echo 卸载清理完成！
echo 注意：用户数据目录中的录音文件需要手动处理
echo 位置：%USERPROFILE%\Documents\AudioRecorder\
echo.
pause
