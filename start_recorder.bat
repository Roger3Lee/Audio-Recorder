@echo off
echo 正在启动录音控制器...
echo.
echo 系统要求：
echo - Windows 10/11
echo - .NET 8.0 Runtime
echo - 管理员权限（用于访问系统音频）
echo.
echo 功能说明：
echo - 桌面置顶录音控制窗口
echo - 实时音频电平显示
echo - WebSocket远程控制 (端口: 8080)
echo - 测试页面: http://localhost:8080
echo.
echo 按任意键继续...
pause > nul

dotnet run

echo.
echo 程序已退出，按任意键关闭此窗口...
pause > nul