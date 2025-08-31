@echo off
echo 完整发布 AudioRecorder 应用程序
echo ================================

echo.
echo 1. 清理发布目录...
if exist "bin\Release\net8.0-windows\win-x64\publish" (
    echo 删除现有发布目录...
    rmdir /s /q "bin\Release\net8.0-windows\win-x64\publish"
    echo [√] 发布目录已清理
) else (
    echo [√] 发布目录不存在，无需清理
)

echo.
echo 2. 执行完整发布...
echo 正在发布到: bin\Release\net8.0-windows\win-x64\publish
echo.

dotnet publish AudioRecorder.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false -o "bin/Release/net8.0-windows/win-x64/publish"

if %errorlevel% equ 0 (
    echo [√] 发布成功！
) else (
    echo [×] 发布失败！
    pause
    exit /b 1
)

echo.
echo 3. 验证发布结果...
echo 发布目录内容:
dir "bin\Release\net8.0-windows\win-x64\publish"

echo.
echo 4. 检查关键文件...
set "PUBLISH_DIR=bin\Release\net8.0-windows\win-x64\publish"

if exist "%PUBLISH_DIR%\AudioRecorder.exe" (
    echo [√] AudioRecorder.exe 存在
) else (
    echo [×] AudioRecorder.exe 不存在
)

if exist "%PUBLISH_DIR%\appsettings.json" (
    echo [√] appsettings.json 存在
) else (
    echo [×] appsettings.json 不存在
)

if exist "%PUBLISH_DIR%\AudioRecorder.pdb" (
    echo [√] AudioRecorder.pdb 存在
) else (
    echo [×] AudioRecorder.pdb 不存在
)

if exist "%PUBLISH_DIR%\wpfgfx_cor3.dll" (
    echo [√] wpfgfx_cor3.dll 存在
) else (
    echo [×] wpfgfx_cor3.dll 不存在
)

if exist "%PUBLISH_DIR%\PenImc_cor3.dll" (
    echo [√] PenImc_cor3.dll 存在
) else (
    echo [×] PenImc_cor3.dll 不存在
)

if exist "%PUBLISH_DIR%\vcruntime140_cor3.dll" (
    echo [√] vcruntime140_cor3.dll 存在
) else (
    echo [×] vcruntime140_cor3.dll 不存在
)

if exist "%PUBLISH_DIR%\PresentationNative_cor3.dll" (
    echo [√] PresentationNative_cor3.dll 存在
) else (
    echo [×] PresentationNative_cor3.dll 不存在
)

if exist "%PUBLISH_DIR%\D3DCompiler_47_cor3.dll" (
    echo [√] D3DCompiler_47_cor3.dll 存在
) else (
    echo [×] D3DCompiler_47_cor3.dll 不存在
)

echo.
echo 5. 发布完成！
echo 现在可以使用 verify_wxs_files.bat 验证 WXS 文件匹配性
echo.
pause
