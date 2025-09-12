@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

echo ========================================
echo AudioRecorder Installation Package Build Script
echo ========================================
echo.

:: Set version (centrally managed here)
set "VERSION=1.0.5"
set "BUILD_CONFIG=Release"
set "TARGET_FRAMEWORK=net8.0-windows"
set "RUNTIME=win-x64"

echo Current Version: %VERSION%
echo Build Config: %BUILD_CONFIG%
echo Target Framework: %TARGET_FRAMEWORK%
echo Runtime: %RUNTIME%
echo.

:: 1. Clean previous builds
echo 1. Cleaning previous builds...
if exist "bin\%BUILD_CONFIG%\%TARGET_FRAMEWORK%\%RUNTIME%\publish" (
    rmdir /s /q "bin\%BUILD_CONFIG%\%TARGET_FRAMEWORK%\%RUNTIME%\publish"
    echo [OK] Cleanup completed
) else (
    echo [OK] No cleanup needed
)
echo.

:: 2. Build application
echo 2. Building application...
dotnet publish AudioRecorder.csproj -c %BUILD_CONFIG% -r %RUNTIME% --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false -o "bin/%BUILD_CONFIG%/%TARGET_FRAMEWORK%/%RUNTIME%/publish"

if %errorlevel% neq 0 (
    echo [ERROR] Application build failed
    pause
    exit /b 1
)
echo [SUCCESS] Application build completed
echo.

:: 3. Verify build output
echo 3. Verifying build output...
if not exist "bin\%BUILD_CONFIG%\%TARGET_FRAMEWORK%\%RUNTIME%\publish\AudioRecorder.exe" (
    echo [ERROR] Main executable not found
    pause
    exit /b 1
)

if not exist "bin\%BUILD_CONFIG%\%TARGET_FRAMEWORK%\%RUNTIME%\publish\appsettings.json" (
    echo [ERROR] Configuration file not found
    pause
    exit /b 1
)

echo [SUCCESS] Build output verification passed
echo.

:: 4. Check WiX configuration version
echo 4. Checking WiX configuration...
findstr /C:"Version=\"%VERSION%\"" AudioRecorder.Setup.wxs >nul
if %errorlevel% neq 0 (
    echo [ERROR] Version mismatch in WiX configuration
    echo Please check Package Version in AudioRecorder.Setup.wxs
    pause
    exit /b 1
)

findstr /C:"Value=\"%VERSION%\"" AudioRecorder.Setup.wxs >nul
if %errorlevel% neq 0 (
    echo [ERROR] Registry version mismatch in WiX configuration
    echo Please check registry Version value in AudioRecorder.Setup.wxs
    pause
    exit /b 1
)

echo [SUCCESS] WiX configuration version check passed
echo.

:: 5. Build installer package
echo 5. Building installer package...
if not exist "wix" mkdir wix

:: Use WiX tools to build MSI
wix build AudioRecorder.Setup.wxs -o "wix\AudioRecorder-%VERSION%-Setup.msi"

if %errorlevel% neq 0 (
    echo [ERROR] Installer package build failed
    echo.
    echo Possible causes:
    echo 1. WiX tools not installed or not in PATH
    echo 2. Syntax errors in WiX configuration
    echo 3. Referenced files do not exist
    echo.
    echo Please check error messages and fix issues
    pause
    exit /b 1
)

echo [SUCCESS] Installer package build completed
echo.

:: 6. Verify installer package
echo 6. Verifying installer package...
if not exist "wix\AudioRecorder-%VERSION%-Setup.msi" (
    echo [ERROR] Installer package file not found
    pause
    exit /b 1
)

:: Get file size
for %%A in ("wix\AudioRecorder-%VERSION%-Setup.msi") do (
    set "FILE_SIZE=%%~zA"
)

echo [SUCCESS] Installer package verification passed
echo   File: wix\AudioRecorder-%VERSION%-Setup.msi
echo   Size: !FILE_SIZE! bytes
echo.

:: 7. Display build summary
echo ========================================
echo Build Summary
echo ========================================
echo Version: %VERSION%
echo Installer: wix\AudioRecorder-%VERSION%-Setup.msi
echo Size: !FILE_SIZE! bytes
echo.
echo Duplicate Installation Protection Features:
echo [OK] Version check
echo [OK] Registry check  
echo [OK] File existence check
echo [OK] MSI upgrade logic
echo.
echo Testing Recommendations:
echo 1. Run test_duplicate_install.bat to check current state
echo 2. Install this version
echo 3. Try installing again - should be blocked
echo 4. Uninstall then reinstall should work
echo ========================================

echo.
echo Build completed! Press any key to exit...
pause >nul