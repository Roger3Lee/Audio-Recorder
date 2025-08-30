@echo off
echo 🎵 AudioRecorder OAuth测试程序
echo ==================================
echo.

echo 正在编译项目...
dotnet build --configuration Release

if %ERRORLEVEL% NEQ 0 (
    echo ❌ 编译失败，请检查代码错误
    pause
    exit /b 1
)

echo ✅ 编译成功
echo.

echo 正在运行OAuth测试...
dotnet run --project . --configuration Release

echo.
echo 测试完成，按任意键退出...
pause > nul
