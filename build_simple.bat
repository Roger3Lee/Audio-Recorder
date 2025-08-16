@echo off
echo ========================================
echo Audio Recorder Install Package Builder
echo ========================================
echo.

echo Setting environment variables...
set WIX_TEMP=%TEMP%\wix_build
if not exist "%WIX_TEMP%" mkdir "%WIX_TEMP%"
set WIX_CACHE=%TEMP%\wix_cache
if not exist "%WIX_CACHE%" mkdir "%WIX_CACHE%"

echo Checking WiX Toolset v4...
wix --version >nul 2>&1
if %errorlevel% neq 0 (
    echo [ERROR] WiX Toolset v4 not found
    echo Please install first:
    echo dotnet tool install --global wix
    pause
    exit /b 1
)

echo [OK] Found WiX Toolset v4
wix --version
echo.

echo Building application...
dotnet publish AudioRecorder.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true

if %errorlevel% neq 0 (
    echo [ERROR] Application build failed!
    pause
    exit /b 1
)

echo [OK] Application build successful!
echo.

echo Checking application icon...
if exist "icon.ico" (
    echo [OK] Found icon file: icon.ico
) else (
    echo [WARNING] Icon file not found, may affect installer appearance
)

echo Checking WiX source files...
if not exist "AudioRecorder.Setup.wxs" (
    echo [ERROR] AudioRecorder.Setup.wxs file not found
    echo Please ensure WiX source file exists
    pause
    exit /b 1
)

echo [OK] WiX source file check successful!
echo.

echo Cleaning temporary files...
if exist "%WIX_TEMP%" rmdir /s /q "%WIX_TEMP%" 2>nul
if exist "%WIX_CACHE%" rmdir /s /q "%WIX_CACHE%" 2>nul

echo Cleaning old installer packages...
if exist "AudioRecorder*.msi" del "AudioRecorder*.msi" /q
if exist "*.wixpdb" del "*.wixpdb" /q

echo Building complete installer package...
wix build AudioRecorder.Setup.wxs -out AudioRecorder_Complete.msi

if %errorlevel% neq 0 (
    echo [ERROR] Installer package build failed!
    echo.
    echo Trying alternative method...
    echo Using MSBuild...
    
    dotnet build AudioRecorder.Setup.wixproj
    
    if %errorlevel% neq 0 (
        echo [ERROR] Alternative build method also failed!
        pause
        exit /b 1
    )
)

echo [OK] Installer package build successful!
echo.

echo Looking for generated installer package...
if exist "AudioRecorder_Complete.msi" (
    echo Generated installer package: AudioRecorder_Complete.msi
    echo Package size:
    for %%A in ("AudioRecorder_Complete.msi") do echo    %%~zA bytes
    echo.
    echo Installer package features:
    echo    User can choose installation directory
    echo    Complete install/uninstall interface
    echo    Start menu shortcuts
    echo    Desktop shortcuts
    echo    Prevent duplicate installation
    echo    Automatic version detection and upgrade
    echo    Custom application icon
    echo    audiorecorder:// protocol support
    echo    .audiorecord file association
    echo    Auto-register protocol after installation
) else (
    echo [ERROR] Generated installer package file not found
    pause
    exit /b 1
)

:found
echo.
echo ========================================
echo Build Complete!
echo ========================================
echo.
echo Installer package has been generated in current directory
echo.
echo Next steps:
echo 1. Test installer package
echo 2. Distribute to users
echo 3. Verify URL protocol registration functionality
echo.
echo Press any key to exit...
pause >nul