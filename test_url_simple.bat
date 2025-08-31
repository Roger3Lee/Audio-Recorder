@echo off
echo 简单测试 AudioRecorder URL 协议
echo ================================

echo.
echo 1. 测试基本 URL 协议调用...
echo 调用: audiorecorder://
start "" "audiorecorder://"

echo.
echo 2. 等待程序启动...
timeout /t 5 /nobreak >nul

echo.
echo 3. 检查进程...
tasklist /FI "IMAGENAME eq AudioRecorder.exe" /FO TABLE

echo.
echo 4. 测试完成！
echo 如果程序正常启动，你应该能看到 AudioRecorder 窗口。
echo.
pause
