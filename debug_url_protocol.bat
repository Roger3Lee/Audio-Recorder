@echo off
echo 调试 AudioRecorder URL 协议问题
echo ================================

echo.
echo 1. 检查当前进程...
tasklist /FI "IMAGENAME eq AudioRecorder.exe" /FO TABLE

echo.
echo 2. 检查注册表项...
reg query "HKCU\Software\Classes\audiorecorder" /s

echo.
echo 3. 测试基本 URL 协议调用...
echo 调用: audiorecorder://
start "" "audiorecorder://"

echo.
echo 4. 等待程序启动...
timeout /t 3 /nobreak >nul

echo.
echo 5. 再次检查进程...
tasklist /FI "IMAGENAME eq AudioRecorder.exe" /FO TABLE

echo.
echo 6. 测试带参数的 URL 协议调用...
echo 调用: audiorecorder://action=start
start "" "audiorecorder://action=start"

echo.
echo 7. 等待处理...
timeout /t 3 /nobreak >nul

echo.
echo 8. 最终进程检查...
tasklist /FI "IMAGENAME eq AudioRecorder.exe" /FO TABLE

echo.
echo 9. 调试信息...
echo - 如果进程数量增加，说明程序启动了
echo - 如果看不到窗口，可能是窗口显示问题
echo - 检查控制台输出和日志文件
echo.
echo 10. 测试完成！
pause
