@echo off
echo 验证 WXS 文件与实际发布文件的匹配性
echo ===========================================

echo.
echo 1. 检查发布目录中的实际文件...
echo 发布目录: bin\Release\net8.0-windows\win-x64\publish\
echo.

if exist "bin\Release\net8.0-windows\win-x64\publish\AudioRecorder.exe" (
    echo [√] AudioRecorder.exe 存在
) else (
    echo [×] AudioRecorder.exe 不存在
)

if exist "bin\Release\net8.0-windows\win-x64\publish\appsettings.json" (
    echo [√] appsettings.json 存在
) else (
    echo [×] appsettings.json 不存在
)

if exist "bin\Release\net8.0-windows\win-x64\publish\AudioRecorder.pdb" (
    echo [√] AudioRecorder.pdb 存在
) else (
    echo [×] AudioRecorder.pdb 不存在
)

if exist "bin\Release\net8.0-windows\win-x64\publish\wpfgfx_cor3.dll" (
    echo [√] wpfgfx_cor3.dll 存在
) else (
    echo [×] wpfgfx_cor3.dll 不存在
)

if exist "bin\Release\net8.0-windows\win-x64\publish\PenImc_cor3.dll" (
    echo [√] PenImc_cor3.dll 存在
) else (
    echo [×] PenImc_cor3.dll 不存在
)

if exist "bin\Release\net8.0-windows\win-x64\publish\vcruntime140_cor3.dll" (
    echo [√] vcruntime140_cor3.dll 存在
) else (
    echo [×] vcruntime140_cor3.dll 不存在
)

if exist "bin\Release\net8.0-windows\win-x64\publish\PresentationNative_cor3.dll" (
    echo [√] PresentationNative_cor3.dll 存在
) else (
    echo [×] PresentationNative_cor3.dll 不存在
)

if exist "bin\Release\net8.0-windows\win-x64\publish\D3DCompiler_47_cor3.dll" (
    echo [√] D3DCompiler_47_cor3.dll 存在
) else (
    echo [×] D3DCompiler_47_cor3.dll 不存在
)

echo.
echo 2. 检查 WXS 文件中引用的文件...
echo 正在验证 AudioRecorder.Setup.wxs 文件...

REM 检查 WXS 文件中的文件引用
findstr /C:"Source=" AudioRecorder.Setup.wxs | findstr /C:"publish"

echo.
echo 3. 文件大小信息...
if exist "bin\Release\net8.0-windows\win-x64\publish\AudioRecorder.exe" (
    for %%A in ("bin\Release\net8.0-windows\win-x64\publish\AudioRecorder.exe") do (
        echo AudioRecorder.exe 大小: %%~zA 字节
    )
)

echo.
echo 4. 验证完成！
echo 如果所有文件都显示 [√]，则 WXS 文件配置正确。
echo.
pause
