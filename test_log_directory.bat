@echo off
echo 测试日志目录权限修复
echo ========================

echo.
echo 1. 检查用户AppData目录...
set "APPDATA_PATH=%APPDATA%\AudioRecorder\logs"
echo 日志目录路径: %APPDATA_PATH%

if exist "%APPDATA_PATH%" (
    echo [√] 日志目录已存在
) else (
    echo [×] 日志目录不存在，将尝试创建
)

echo.
echo 2. 尝试创建日志目录...
mkdir "%APPDATA_PATH%" 2>nul
if %errorlevel% equ 0 (
    echo [√] 日志目录创建成功
) else (
    echo [×] 日志目录创建失败
)

echo.
echo 3. 测试写入权限...
set "TEST_FILE=%APPDATA_PATH%\test_permissions.txt"
echo 测试文件: %TEST_FILE%

echo 权限测试 > "%TEST_FILE%" 2>nul
if %errorlevel% equ 0 (
    echo [√] 写入权限测试通过
    del "%TEST_FILE%" 2>nul
    echo [√] 删除权限测试通过
) else (
    echo [×] 写入权限测试失败
)

echo.
echo 4. 检查备用目录...
set "DOCUMENTS_PATH=%USERPROFILE%\Documents\AudioRecorder\logs"
echo 备用日志目录: %DOCUMENTS_PATH%

if exist "%DOCUMENTS_PATH%" (
    echo [√] 备用日志目录已存在
) else (
    echo [×] 备用日志目录不存在
)

echo.
echo 5. 测试完成！
echo 如果所有测试都显示 [√]，则日志目录权限问题已修复。
echo.
pause
