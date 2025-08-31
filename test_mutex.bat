@echo off
echo 测试 AudioRecorder 互斥功能
echo =============================

echo.
echo 1. 启动第一个实例...
start "" "bin\Release\net8.0-windows\win-x64\publish\AudioRecorder.exe"
timeout /t 3 /nobreak >nul

echo.
echo 2. 尝试启动第二个实例...
start "" "bin\Release\net8.0-windows\win-x64\publish\AudioRecorder.exe"
timeout /t 3 /nobreak >nul

echo.
echo 3. 检查进程数量...
tasklist /FI "IMAGENAME eq AudioRecorder.exe" /FO TABLE

echo.
echo 4. 测试完成！
echo 如果互斥功能正常工作：
echo - 只会有一个 AudioRecorder 进程运行
echo - 第二个实例会显示"已在运行中"的提示
echo - 第一个实例会被激活
echo.
pause
