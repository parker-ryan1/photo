@echo off
echo ===============================================
echo Camera Capture Service - Docker Alternative
echo ===============================================
echo.

REM Check if service is already running
tasklist /FI "IMAGENAME eq CameraCaptureService.exe" 2>NUL | find /I /N "CameraCaptureService.exe">NUL
if "%ERRORLEVEL%"=="0" (
    echo Service is already running!
    echo To stop: taskkill /F /IM CameraCaptureService.exe
    pause
    exit /b 1
)

echo Building application...
dotnet publish CameraCaptureService.csproj -c Release -o publish
if errorlevel 1 (
    echo ERROR: Failed to build application
    pause
    exit /b 1
)

echo.
echo Creating captured_images directory...
if not exist "captured_images" mkdir captured_images

echo.
echo Starting Camera Capture Service...
echo.
echo Service Configuration:
echo - Save Location: %cd%\captured_images
echo - Operating Hours: 6:00 AM - 6:00 PM
echo - Capture Interval: 60 minutes
echo - Frames per Sequence: 50
echo.
echo Press Ctrl+C to stop the service
echo.

REM Run the service
cd /d "%~dp0"
publish\CameraCaptureService.exe

echo.
echo Service stopped.
pause