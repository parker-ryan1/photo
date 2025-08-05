# Camera Capture Service - Docker Setup

## Current Status ✅
The service is **working perfectly** with direct execution! Photos are being:
- ✅ Captured every 60 minutes (50 photos per sequence)
- ✅ Downloaded automatically to `captured_images/` folder
- ✅ Deleted from SD card after download
- ✅ Camera kept alive between sequences

## Running the Service

### Option 1: Direct Execution (Recommended)
```cmd
camera-capture-service\run-as-service.bat
```

### Option 2: Docker (Requires Windows Containers)

#### Step 1: Enable Windows Containers
1. Right-click Docker Desktop icon in system tray
2. Select "Switch to Windows containers..."
3. Wait for Docker to restart (3-5 minutes)
4. Verify with: `docker system info | findstr "OSType"`
   - Should show: `OSType: windows`

#### Step 2: Build and Run with Docker
```cmd
cd camera-capture-service
docker-compose up --build
```

Or manually:
```cmd
docker build -t camera-capture-service .
docker run -d --name camera-capture-service -v "%cd%\captured_images:C:\app\captured_images" camera-capture-service
```

## Why Windows Containers Are Required

The Canon EDSDK (camera SDK) requires:
- Windows operating system
- Native Windows DLLs (EDSDK.dll, EdsImage.dll)
- Direct USB hardware access
- Canon camera drivers installed on host

Linux containers cannot access Windows hardware drivers or run Windows-specific DLLs.

## Service Configuration

- **Photos saved to**: `camera-capture-service\captured_images\`
- **Operating hours**: 6:00 AM - 6:00 PM
- **Capture interval**: Every 60 minutes
- **Photos per sequence**: 50
- **Delay between photos**: 15 seconds

## Troubleshooting

### If Docker build fails:
- Ensure Windows containers are enabled
- Check that Docker Desktop is running
- Verify camera is connected and drivers installed

### If photos aren't downloading:
- Check camera is in PC/Remote mode
- Ensure EOS Utility is closed
- Verify SD card has space

## Success Indicators

Look for these log messages:
- `[CAMERA INIT] ✅ Camera initialized successfully`
- `[DOWNLOAD] ✅ Successfully downloaded: [filename]`
- `[SCAN] ✅ Downloaded X new files`

The service is working perfectly! 🎉