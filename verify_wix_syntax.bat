@echo off
echo 验证 WiX 安装包语法修复
echo =========================

echo.
echo 1. 检查 WiX 文件语法...
echo 正在验证 AudioRecorder.Setup.wxs 文件...

REM 使用 WiX 工具验证语法
wix build AudioRecorder.Setup.wxs --output test.msi

if %errorlevel% equ 0 (
    echo [√] WiX 语法验证通过
    echo [√] 安装包可以正常构建
) else (
    echo [×] WiX 语法验证失败
    echo 请检查错误信息
)

echo.
echo 2. 清理测试文件...
if exist test.msi del test.msi

echo.
echo 验证完成！
pause
