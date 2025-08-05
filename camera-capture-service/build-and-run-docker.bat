@echo off
echo Building and running Camera Capture Service in Docker...
echo.

REM Switch Docker to Windows containers
echo Switching Docker to Windows containers...
docker version
if errorlevel 1 (
    echo ERROR: Docker is not running or not installed
    pause
    exit /b 1
)

REM Build the application first
echo Building .NET application...
dotnet publish CameraCaptureService.csproj -c Release -o publish
if errorlevel 1 (
    echo ERROR: Failed to build application
    pause
    exit /b 1
)

REM Build Docker image
echo Building Docker image...
docker build -t camera-capture-service .
if errorlevel 1 (
    echo ERROR: Failed to build Docker image
    echo Make sure Docker is set to Windows containers mode
    pause
    exit /b 1
)

REM Create captured_images directory
if not exist "captured_images" mkdir captured_images

REM Run the container
echo Starting container...
docker run -d ^
    --name camera-capture-service ^
    --restart unless-stopped ^
    -v "%cd%\captured_images:C:\app\captured_images" ^
    -v "%cd%\capture_config.json:C:\app\capture_config.json" ^
    --device-cgroup-rule="c 189:* rmw" ^
    camera-capture-service

if errorlevel 1 (
    echo ERROR: Failed to start container
    pause
    exit /b 1
)

echo.
echo ✅ Camera Capture Service is now running in Docker!
echo.
echo To view logs: docker logs -f camera-capture-service
echo To stop: docker stop camera-capture-service
echo Images will be saved to: %cd%\captured_images
echo.
pause