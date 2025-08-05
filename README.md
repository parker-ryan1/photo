# Automated Camera Capture Service

A .NET-based automated camera capture service for Canon cameras using the Canon EDSDK. This service automatically captures photos at 60-minute intervals and downloads them to your computer.

## Features

✅ **Automated Photo Capture** - Takes 50 photos every 60 minutes during operating hours (6 AM - 6 PM)  
✅ **Automatic Download** - Photos are automatically downloaded from SD card to computer  
✅ **Camera Keep-Alive** - Prevents camera timeout by taking periodic test shots  
✅ **SD Card Management** - Automatically deletes photos from SD card after download  
✅ **Robust Error Handling** - Handles camera disconnections and retries  
✅ **Docker Support** - Ready for containerization with Windows containers  

## Quick Start

### Prerequisites
- Canon camera (tested with Canon EOS Rebel T100)
- Canon EDSDK drivers installed
- .NET 8.0 Runtime
- Camera connected via USB and set to PC/Remote mode

### Running the Service

**Option 1: Direct Execution (Recommended)**
```cmd
cd camera-capture-service
run-as-service.bat
```

**Option 2: Docker (Requires Windows Containers)**
```cmd
cd camera-capture-service
setup-windows-containers.bat
docker-compose up --build
```

## Configuration

Edit `camera-capture-service/capture_config.json`:

```json
{
  "site_name": "CoastalSite",
  "start_hour": 6,
  "end_hour": 18,
  "sequence_interval_minutes": 60,
  "frames_per_sequence": 50,
  "frame_delay_seconds": 15
}
```

## Output

Photos are saved to: `camera-capture-service/captured_images/`

## Project Structure

```
camera-capture-service/
├── Program.cs                    # Main application entry point
├── CameraService.cs             # Camera control and SDK interface
├── CameraController.cs          # Camera command handling
├── CameraModel.cs              # Camera data model
├── EDSDK.cs                    # Canon SDK wrapper
├── capture_config.json         # Service configuration
├── run-as-service.bat          # Direct execution script
├── setup-windows-containers.bat # Docker setup helper
├── Dockerfile                  # Docker container definition
├── docker-compose.yml          # Docker Compose configuration
└── captured_images/            # Output directory for photos
```

## Technical Details

- **Framework**: .NET 8.0
- **Camera SDK**: Canon EDSDK
- **Architecture**: Event-driven with automated scheduling
- **File Management**: Automatic download and cleanup
- **Error Recovery**: Automatic retry and reconnection

## Troubleshooting

### Camera Not Detected
- Ensure camera is turned ON
- Set camera to PC/Remote mode
- Close Canon EOS Utility software
- Check USB connection

### Photos Not Downloading
- Verify SD card has space
- Check camera is in SD card mode
- Ensure proper file permissions

### Docker Issues
- Enable Windows containers in Docker Desktop
- Ensure Canon drivers are installed on host
- Check camera USB passthrough permissions

## Success Indicators

Look for these log messages:
- `[CAMERA INIT] ✅ Camera initialized successfully`
- `[DOWNLOAD] ✅ Successfully downloaded: [filename]`
- `[AUTOMATION] ✅ Capture sequence completed`

## License

This project uses the Canon EDSDK which requires compliance with Canon's licensing terms.

---

**Status**: ✅ Fully functional and tested with Canon EOS Rebel T100