@echo off
echo ===============================================
echo Enable Windows Containers for Docker Desktop
echo ===============================================
echo.

echo This script will help you enable Windows containers in Docker Desktop.
echo.
echo MANUAL STEPS REQUIRED:
echo.
echo 1. Right-click on Docker Desktop icon in system tray
echo 2. Select "Switch to Windows containers..."
echo 3. Wait for Docker to restart (may take a few minutes)
echo 4. Run this script again to verify
echo.

echo Current Docker configuration:
docker version
echo.

echo Checking current OS type...
docker system info | findstr "OSType"
echo.

echo If OSType shows "windows", Windows containers are enabled!
echo If OSType shows "linux", you need to switch manually using Docker Desktop.
echo.

echo After enabling Windows containers, run:
echo   docker-compose up --build
echo.
pause