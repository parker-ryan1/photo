@echo off
echo ===============================================
echo Windows Containers Setup for Camera Service
echo ===============================================
echo.

echo Step 1: Checking current Docker status...
docker version
if errorlevel 1 (
    echo ERROR: Docker is not running or not installed
    echo Please start Docker Desktop and try again
    pause
    exit /b 1
)

echo.
echo Step 2: Checking current container mode...
docker system info | findstr "OSType"

echo.
echo Step 3: Attempting to switch to Windows containers...
echo.
echo MANUAL ACTION REQUIRED:
echo 1. Right-click on Docker Desktop icon in system tray (bottom-right corner)
echo 2. Look for "Switch to Windows containers..." option
echo 3. Click it and wait for Docker to restart (may take 3-5 minutes)
echo 4. If you don't see this option, Windows containers may not be available
echo.

pause
echo.

echo Step 4: Verifying Windows containers are enabled...
docker system info | findstr "OSType"
echo.

echo If OSType shows "windows", continue to Step 5
echo If OSType shows "linux", Windows containers are not enabled yet
echo.

pause
echo.

echo Step 5: Pulling Windows base images...
echo This may take several minutes for the first time...
echo.

echo Pulling .NET Runtime base image...
docker pull mcr.microsoft.com/dotnet/runtime:8.0-windowsservercore-ltsc2022
if errorlevel 1 (
    echo ERROR: Failed to pull .NET runtime image
    echo Make sure Windows containers are enabled
    pause
    exit /b 1
)

echo.
echo Pulling .NET SDK base image...
docker pull mcr.microsoft.com/dotnet/sdk:8.0-windowsservercore-ltsc2022
if errorlevel 1 (
    echo ERROR: Failed to pull .NET SDK image
    pause
    exit /b 1
)

echo.
echo ===============================================
echo SUCCESS: Windows containers are ready!
echo ===============================================
echo.
echo You can now run:
echo   docker-compose up --build
echo.
echo Or build manually:
echo   docker build -t camera-capture-service .
echo.
pause