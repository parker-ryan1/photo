@echo off
echo Starting Camera Capture Service...
echo.
echo Make sure:
echo 1. Canon camera is connected via USB
echo 2. Camera is turned ON
echo 3. EOS Utility is CLOSED
echo 4. Camera is set to PC connection mode
echo.
pause
echo.
echo Starting service...
cd /d "%~dp0"
publish\CameraCaptureService.exe
pause