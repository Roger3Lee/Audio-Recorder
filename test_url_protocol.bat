@echo off
echo 测试 AudioRecorder URL 协议功能
echo =================================

echo.
echo 1. 检查 URL 协议注册状态...
reg query "HKCU\Software\Classes\audiorecorder" /s

echo.
echo 2. 测试 URL 协议调用...
echo 启动录音: audiorecorder://action=start
start "" "audiorecorder://action=start"

timeout /t 2 /nobreak >nul

echo.
echo 暂停录音: audiorecorder://action=pause
start "" "audiorecorder://action=pause"

timeout /t 2 /nobreak >nul

echo.
echo 恢复录音: audiorecorder://action=resume
start "" "audiorecorder://action=resume"

timeout /t 2 /nobreak >nul

echo.
echo 停止录音: audiorecorder://action=stop
start "" "audiorecorder://action=stop"

timeout /t 2 /nobreak >nul

echo.
echo 显示窗口: audiorecorder://show
start "" "audiorecorder://show"

echo.
echo 3. 测试完成！
echo 如果 URL 协议功能正常工作：
echo - 每个命令都会触发相应的录音操作
echo - 应用程序会响应每个命令
echo - 控制台会显示相应的日志信息
echo.
pause
