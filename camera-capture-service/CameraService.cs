using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using EDSDKLib;

namespace CameraCaptureService
{
    public class CameraService : IDisposable
    {
        private bool _isInitialized = false;
        private bool _isCapturing = false;
        private CameraController? _cameraController;
        private CameraModel? _cameraModel;
        
        // Event handlers (keep strong references to prevent GC)
        private EDSDK.EdsPropertyEventHandler? _propertyEventHandler;
        private EDSDK.EdsObjectEventHandler? _objectEventHandler;
        private EDSDK.EdsStateEventHandler? _stateEventHandler;
        
        // File tracking system
        private readonly HashSet<string> _processedFiles = new HashSet<string>();
        private readonly object _fileTrackingLock = new object();
        private string _lastProcessedFileName = string.Empty;
        private ulong _lastProcessedFileSize = 0;
        
        public bool IsInitialized => _isInitialized;
        
        public async Task InitializeAsync()
        {
            await Task.Run(InitializeCamera);
        }
        
        public CameraController? GetCameraController()
        {
            return _cameraController;
        }
        
        private void InitializeCamera()
        {
            try
            {
                Console.WriteLine("[CAMERA INIT] Starting camera initialization...");
                
                // Terminate interfering software
                TerminateCanonSoftware();
                
                // Initialize camera controller with retries
                CameraController? cameraController = null;
                int maxRetries = 5;
                int retryCount = 0;
                
                while (cameraController == null && retryCount < maxRetries)
                {
                    if (retryCount > 0)
                    {
                        Console.WriteLine($"[CAMERA INIT] Retry attempt {retryCount} of {maxRetries}...");
                        TerminateCanonSoftware();
                        
                        int delayMs = Math.Min(3000 + (retryCount * 1000), 10000);
                        Console.WriteLine($"[CAMERA INIT] Waiting {delayMs/1000} seconds before retry...");
                        Thread.Sleep(delayMs);
                    }
                    
                    cameraController = InitializeCameraController();
                    
                    if (cameraController == null)
                    {
                        retryCount++;
                        Console.WriteLine($"[CAMERA INIT] Attempt {retryCount} failed, will retry...");
                    }
                }
                
                if (cameraController != null)
                {
                    _cameraController = cameraController;
                    _cameraModel = _cameraController.GetModel();
                    _isInitialized = true;
                    Console.WriteLine($"[CAMERA INIT] SUCCESS: Camera initialized");
                }
                else
                {
                    _isInitialized = false;
                    Console.WriteLine("[CAMERA INIT] FAILED: Could not initialize camera after all retries");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CAMERA INIT] EXCEPTION: {ex.Message}");
                _isInitialized = false;
            }
        }
        
        private void TerminateCanonSoftware()
        {
            try
            {
                Console.WriteLine("[CAMERA INIT] Terminating Canon software...");
                
                string[] processesToKill = {
                    "EOS Utility", "EOS Utility.exe", "EOSUPNPSV", "EOSUPNPSV.exe",
                    "Canon EOS Utility", "CameraWindow_DVC", "CameraWindow_LaunchOnly"
                };
                
                bool anyProcessKilled = false;
                
                foreach (string processName in processesToKill)
                {
                    try
                    {
                        var processes = System.Diagnostics.Process.GetProcessesByName(processName.Replace(".exe", ""));
                        foreach (var proc in processes)
                        {
                            proc.Kill();
                            proc.WaitForExit(5000);
                            Console.WriteLine($"[CAMERA INIT] ✅ Terminated: {processName} (PID: {proc.Id})");
                            anyProcessKilled = true;
                        }
                    }
                    catch (Exception)
                    {
                        // Silently continue - process might not exist
                    }
                }
                
                if (anyProcessKilled)
                {
                    Console.WriteLine("[CAMERA INIT] ✅ Canon software terminated successfully");
                    Thread.Sleep(2000);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CAMERA INIT] Error terminating Canon software: {ex.Message}");
            }
        }
        
        private CameraController? InitializeCameraController()
        {
            try
            {
                Console.WriteLine("[CAMERA INIT] Starting SDK initialization...");
                
                // Terminate SDK first to clear any previous state
                try
                {
                    EDSDK.EdsTerminateSDK();
                    Thread.Sleep(1000);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[CAMERA INIT] SDK cleanup: {ex.Message}");
                }
                
                // Initialize SDK
                Console.WriteLine("[CAMERA INIT] Initializing Canon SDK...");
                uint err = EDSDK.EdsInitializeSDK();
                if (err != EDSDK.EDS_ERR_OK)
                {
                    Console.WriteLine($"[CAMERA INIT] Failed to initialize SDK: {err}");
                    return null;
                }
                
                Console.WriteLine("[CAMERA INIT] ✅ SDK initialized successfully");

                // Get camera list and first camera
                IntPtr cameraList = IntPtr.Zero;
                err = EDSDK.EdsGetCameraList(out cameraList);
                if (err != EDSDK.EDS_ERR_OK)
                {
                    Console.WriteLine($"[CAMERA INIT] Failed to get camera list: {err}");
                    return null;
                }

                int count = 0;
                err = EDSDK.EdsGetChildCount(cameraList, out count);
                if (err != EDSDK.EDS_ERR_OK || count == 0)
                {
                    Console.WriteLine("[CAMERA INIT] No cameras found");
                    Console.WriteLine("[CAMERA INIT] 💡 Make sure:");
                    Console.WriteLine("[CAMERA INIT]    1. Camera is connected via USB");
                    Console.WriteLine("[CAMERA INIT]    2. Camera is turned ON");
                    Console.WriteLine("[CAMERA INIT]    3. Camera is set to PC/Remote mode");
                    Console.WriteLine("[CAMERA INIT]    4. EOS Utility is completely closed");
                    EDSDK.EdsRelease(cameraList);
                    return null;
                }

                Console.WriteLine($"[CAMERA INIT] Found {count} camera(s)");

                IntPtr camera = IntPtr.Zero;
                err = EDSDK.EdsGetChildAtIndex(cameraList, 0, out camera);
                if (err != EDSDK.EDS_ERR_OK)
                {
                    Console.WriteLine($"[CAMERA INIT] Failed to get camera: {err}");
                    EDSDK.EdsRelease(cameraList);
                    return null;
                }

                // Get device info
                EDSDK.EdsDeviceInfo deviceInfo;
                err = EDSDK.EdsGetDeviceInfo(camera, out deviceInfo);
                if (err != EDSDK.EDS_ERR_OK)
                {
                    Console.WriteLine($"[CAMERA INIT] Failed to get device info: {err}");
                    EDSDK.EdsRelease(cameraList);
                    return null;
                }

                Console.WriteLine($"[CAMERA INIT] Camera found: {deviceInfo.szDeviceDescription}");

                // Open session with better error handling
                Console.WriteLine("[CAMERA INIT] Opening camera session...");
                err = EDSDK.EdsOpenSession(camera);
                if (err != EDSDK.EDS_ERR_OK)
                {
                    Console.WriteLine($"[CAMERA INIT] Failed to open session: {err}");
                    Console.WriteLine("[CAMERA INIT] 💡 Common solutions for error 192:");
                    Console.WriteLine("[CAMERA INIT]    1. Turn camera OFF, wait 5 seconds, turn ON");
                    Console.WriteLine("[CAMERA INIT]    2. Disconnect USB cable, reconnect");
                    Console.WriteLine("[CAMERA INIT]    3. Set camera dial to 'M' (Manual) mode");
                    Console.WriteLine("[CAMERA INIT]    4. Check camera menu: Communication -> USB -> PC Remote");
                    Console.WriteLine("[CAMERA INIT]    5. Restart this application");
                    EDSDK.EdsRelease(cameraList);
                    return null;
                }

                Console.WriteLine("[CAMERA INIT] ✅ Session opened successfully");

                // Wait for camera to be ready
                Console.WriteLine("[CAMERA INIT] Waiting for camera to be ready...");
                Thread.Sleep(3000);

                // Set up SD card mode
                Console.WriteLine("[CAMERA INIT] Setting up SD card mode...");
                SetupSDCardMode(camera);

                // Create model and controller
                var model = new CameraModel(camera);
                model.deviceInfo = deviceInfo;
                var controller = new CameraController(model);

                // Register event handlers for SD card events
                RegisterSDCardEventHandlers(camera);

                EDSDK.EdsRelease(cameraList);
                Console.WriteLine($"[CAMERA INIT] ✅ Camera initialized successfully: {deviceInfo.szDeviceDescription}");
                return controller;

            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CAMERA INIT] Exception: {ex.Message}");
                return null;
            }
        }
        
        private void SetupSDCardMode(IntPtr camera)
        {
            try
            {
                Console.WriteLine("[SD_MODE] Setting up SD card mode...");
                
                // Try to set to SD card mode
                uint err = EDSDK.EdsSetPropertyData(camera, EDSDK.PropID_SaveTo, 0, sizeof(uint), 1);
                
                if (err == EDSDK.EDS_ERR_OK)
                {
                    Console.WriteLine("[SD_MODE] ✅ Successfully set to SD card mode");
                }
                else
                {
                    Console.WriteLine($"[SD_MODE] ❌ Could not set SD card mode: {err}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SD_MODE] ❌ Error in SD card mode setup: {ex.Message}");
            }
        }
        
        private void RegisterSDCardEventHandlers(IntPtr camera)
        {
            try
            {
                // Create event handlers
                _objectEventHandler = new EDSDK.EdsObjectEventHandler(HandleObjectEvent);
                _stateEventHandler = new EDSDK.EdsStateEventHandler(HandleStateEvent);
                _propertyEventHandler = new EDSDK.EdsPropertyEventHandler(HandlePropertyEvent);
                
                // Register handlers
                EDSDK.EdsSetObjectEventHandler(camera, EDSDK.kEdsObjectEvent_DirItemCreated, _objectEventHandler, IntPtr.Zero);
                EDSDK.EdsSetCameraStateEventHandler(camera, EDSDK.kEdsStateEvent_Shutdown, _stateEventHandler, IntPtr.Zero);
                
                // Start continuous event processing
                StartEventProcessing();
                
                Console.WriteLine("[EVENTS] Event handlers registered");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EVENTS] Error registering event handlers: {ex.Message}");
            }
        }
        
        private void StartEventProcessing()
        {
            Task.Run(async () =>
            {
                Console.WriteLine("[EVENTS] Starting continuous event processing...");
                while (_isInitialized)
                {
                    try
                    {
                        EDSDK.EdsGetEvent();
                        await Task.Delay(50); // Process events every 50ms
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[EVENTS] Error processing events: {ex.Message}");
                        await Task.Delay(1000);
                    }
                }
                Console.WriteLine("[EVENTS] Event processing stopped");
            });
        }
        
        private uint HandleObjectEvent(uint inEvent, IntPtr inRef, IntPtr inContext)
        {
            try
            {
                if (inEvent == EDSDK.kEdsObjectEvent_DirItemCreated)
                {
                    Console.WriteLine("[EVENT] New file created on SD card");
                    // Instead of using the event reference, scan for new files
                    Task.Run(() => ScanAndDownloadNewFiles());
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EVENT] Error handling object event: {ex.Message}");
            }
            return EDSDK.EDS_ERR_OK;
        }
        
        private uint HandleStateEvent(uint inEvent, uint inEventData, IntPtr inContext)
        {
            Console.WriteLine($"[EVENT] State event: {inEvent}");
            return EDSDK.EDS_ERR_OK;
        }
        
        private uint HandlePropertyEvent(uint inEvent, uint inPropertyID, uint inParam, IntPtr inContext)
        {
            Console.WriteLine($"[EVENT] Property event: {inEvent}, Property: {inPropertyID}");
            return EDSDK.EDS_ERR_OK;
        }
        
        private void ProcessNewFile(IntPtr fileRef)
        {
            try
            {
                EDSDK.EdsDirectoryItemInfo itemInfo;
                uint err = EDSDK.EdsGetDirectoryItemInfo(fileRef, out itemInfo);
                
                if (err == EDSDK.EDS_ERR_OK && itemInfo.isFolder == 0 && IsImageFile(itemInfo))
                {
                    Console.WriteLine($"[FILE] Processing new file: {itemInfo.szFileName} ({itemInfo.Size} bytes)");
                    
                    // Download the file
                    bool downloadSuccess = DownloadFile(fileRef, itemInfo);
                    
                    if (downloadSuccess)
                    {
                        // Mark as processed only if download was successful
                        MarkFileAsProcessed(itemInfo.szFileName, itemInfo.Size);
                        Console.WriteLine($"[FILE] ✅ Successfully processed: {itemInfo.szFileName}");
                    }
                    else
                    {
                        Console.WriteLine($"[FILE] ❌ Failed to process: {itemInfo.szFileName}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FILE] Error processing new file: {ex.Message}");
            }
            finally
            {
                _isCapturing = false;
            }
        }
        
        private bool DownloadFile(IntPtr fileRef, EDSDK.EdsDirectoryItemInfo itemInfo)
        {
            try
            {
                // Create output directory if it doesn't exist
                string outputDir = Path.Combine(Directory.GetCurrentDirectory(), "captured_images");
                Directory.CreateDirectory(outputDir);
                
                string fileName = Path.Combine(outputDir, itemInfo.szFileName);
                Console.WriteLine($"[DOWNLOAD] Downloading to: {fileName}");
                
                // Ensure the file doesn't exist first
                if (File.Exists(fileName))
                {
                    try
                    {
                        File.Delete(fileName);
                        Console.WriteLine($"[DOWNLOAD] Deleted existing file: {fileName}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[DOWNLOAD] Could not delete existing file: {ex.Message}");
                    }
                }
                
                // Create file stream
                IntPtr stream = IntPtr.Zero;
                uint err = EDSDK.EdsCreateFileStream(fileName, EDSDK.kEdsFileCreateDisposition_CreateNew, EDSDK.kEdsAccess_Write, out stream);
                
                if (err == EDSDK.EDS_ERR_OK)
                {
                    Console.WriteLine($"[DOWNLOAD] File stream created successfully");
                    
                    // Download the file
                    err = EDSDK.EdsDownload(fileRef, itemInfo.Size, stream);
                    
                    if (err == EDSDK.EDS_ERR_OK)
                    {
                        // Complete the download
                        EDSDK.EdsDownloadComplete(fileRef);
                        Console.WriteLine($"[DOWNLOAD] ✅ Successfully downloaded: {fileName}");
                        
                        // Optionally delete from SD card
                        err = EDSDK.EdsDeleteDirectoryItem(fileRef);
                        if (err == EDSDK.EDS_ERR_OK)
                        {
                            Console.WriteLine($"[DOWNLOAD] ✅ Deleted from SD card: {itemInfo.szFileName}");
                        }
                        else
                        {
                            Console.WriteLine($"[DOWNLOAD] ⚠️ Could not delete from SD card: {err}");
                        }
                        
                        EDSDK.EdsRelease(stream);
                        return true;
                    }
                    else
                    {
                        Console.WriteLine($"[DOWNLOAD] ❌ Download failed: {err}");
                        EDSDK.EdsRelease(stream);
                        return false;
                    }
                }
                else
                {
                    Console.WriteLine($"[DOWNLOAD] ❌ Failed to create file stream: {err}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DOWNLOAD] Error downloading file: {ex.Message}");
                return false;
            }
        }
        
        public bool TriggerManualCapture(bool isAutomated = false)
        {
            if (_cameraModel?.Camera == IntPtr.Zero)
            {
                Console.WriteLine("[CAPTURE] Camera not available");
                return false;
            }
            
            try
            {
                string captureType = isAutomated ? "AUTOMATED" : "MANUAL";
                Console.WriteLine($"[CAPTURE] Starting {captureType} capture...");
                
                // Process any pending events first
                for (int i = 0; i < 10; i++)
                {
                    EDSDK.EdsGetEvent();
                    Thread.Sleep(10);
                }
                
                // Reset capture state
                _isCapturing = false;
                
                // Take picture
                uint err = EDSDK.EdsSendCommand(_cameraModel.Camera, EDSDK.CameraCommand_TakePicture, 0);
                
                if (err == EDSDK.EDS_ERR_OK)
                {
                    Console.WriteLine($"[CAPTURE] ✅ {captureType} capture command sent successfully");
                    _isCapturing = true;
                    
                    // Process events for a short time to handle the capture
                    Task.Run(async () =>
                    {
                        await Task.Delay(3000); // Wait 3 seconds for capture to complete
                        _isCapturing = false; // Reset capture state
                        Console.WriteLine($"[CAPTURE] {captureType} capture state reset");
                    });
                    
                    return true;
                }
                else
                {
                    Console.WriteLine($"[CAPTURE] ❌ {captureType} capture failed: {err}");
                    _isCapturing = false;
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CAPTURE] Exception during capture: {ex.Message}");
                _isCapturing = false;
                return false;
            }
        }
        
        public async Task<List<string>> GetCardFilesAsync()
        {
            return await Task.Run(() =>
            {
                var files = new List<string>();
                // Implementation would scan SD card for files
                return files;
            });
        }
        
        public async Task<bool> FormatSDCardAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (_cameraModel?.Camera == IntPtr.Zero) return false;
                    
                    // Get volume and format
                    IntPtr volume = IntPtr.Zero;
                    uint err = EDSDK.EdsGetChildAtIndex(_cameraModel.Camera, 0, out volume);
                    if (err == EDSDK.EDS_ERR_OK)
                    {
                        err = EDSDK.EdsFormatVolume(volume);
                        EDSDK.EdsRelease(volume);
                        return err == EDSDK.EDS_ERR_OK;
                    }
                    return false;
                }
                catch
                {
                    return false;
                }
            });
        }
        
        public void ResetFileTracking()
        {
            lock (_fileTrackingLock)
            {
                _processedFiles.Clear();
                _lastProcessedFileName = string.Empty;
                _lastProcessedFileSize = 0;
                Console.WriteLine("[FILE_TRACK] File tracking reset");
            }
        }
        
        public async Task<bool> DeleteLatestFileFromSDAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (_cameraModel?.Camera == IntPtr.Zero) return false;
                    
                    Console.WriteLine("[SD_DELETE] Scanning SD card for latest file to delete...");
                    
                    // Get volume
                    IntPtr volume = IntPtr.Zero;
                    uint err = EDSDK.EdsGetChildAtIndex(_cameraModel.Camera, 0, out volume);
                    if (err != EDSDK.EDS_ERR_OK) return false;
                    
                    // Get DCIM directory
                    IntPtr dcimDir = IntPtr.Zero;
                    err = EDSDK.EdsGetChildAtIndex(volume, 0, out dcimDir);
                    if (err != EDSDK.EDS_ERR_OK)
                    {
                        EDSDK.EdsRelease(volume);
                        return false;
                    }
                    
                    IntPtr latestFile = IntPtr.Zero;
                    string latestFileName = "";
                    
                    // Find the latest file in all folders
                    int dirCount = 0;
                    err = EDSDK.EdsGetChildCount(dcimDir, out dirCount);
                    if (err == EDSDK.EDS_ERR_OK)
                    {
                        for (int i = dirCount - 1; i >= 0; i--) // Start from the last folder (most recent)
                        {
                            IntPtr folder = IntPtr.Zero;
                            err = EDSDK.EdsGetChildAtIndex(dcimDir, i, out folder);
                            if (err == EDSDK.EDS_ERR_OK)
                            {
                                int fileCount = 0;
                                err = EDSDK.EdsGetChildCount(folder, out fileCount);
                                if (err == EDSDK.EDS_ERR_OK && fileCount > 0)
                                {
                                    // Get the last file in this folder (most recent)
                                    IntPtr fileItem = IntPtr.Zero;
                                    err = EDSDK.EdsGetChildAtIndex(folder, fileCount - 1, out fileItem);
                                    
                                    if (err == EDSDK.EDS_ERR_OK && fileItem != IntPtr.Zero)
                                    {
                                        EDSDK.EdsDirectoryItemInfo itemInfo;
                                        err = EDSDK.EdsGetDirectoryItemInfo(fileItem, out itemInfo);
                                        
                                        if (err == EDSDK.EDS_ERR_OK && itemInfo.isFolder == 0 && IsImageFile(itemInfo))
                                        {
                                            latestFile = fileItem;
                                            latestFileName = itemInfo.szFileName;
                                            Console.WriteLine($"[SD_DELETE] Found latest file to delete: {latestFileName}");
                                            break; // Found the most recent file
                                        }
                                        else
                                        {
                                            EDSDK.EdsRelease(fileItem);
                                        }
                                    }
                                }
                                EDSDK.EdsRelease(folder);
                                
                                if (latestFile != IntPtr.Zero) break; // Found a file, stop searching
                            }
                        }
                    }
                    
                    EDSDK.EdsRelease(dcimDir);
                    EDSDK.EdsRelease(volume);
                    
                    // Delete the latest file if found
                    if (latestFile != IntPtr.Zero)
                    {
                        err = EDSDK.EdsDeleteDirectoryItem(latestFile);
                        EDSDK.EdsRelease(latestFile);
                        
                        if (err == EDSDK.EDS_ERR_OK)
                        {
                            Console.WriteLine($"[SD_DELETE] ✅ Successfully deleted: {latestFileName}");
                            return true;
                        }
                        else
                        {
                            Console.WriteLine($"[SD_DELETE] ❌ Failed to delete {latestFileName}: {err}");
                            return false;
                        }
                    }
                    else
                    {
                        Console.WriteLine("[SD_DELETE] ⚠️ No files found on SD card to delete");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SD_DELETE] ❌ Error deleting file from SD: {ex.Message}");
                    return false;
                }
            });
        }
        
        private bool IsImageFile(EDSDK.EdsDirectoryItemInfo itemInfo)
        {
            string fileName = itemInfo.szFileName.ToLower();
            return fileName.EndsWith(".jpg") || fileName.EndsWith(".jpeg") || 
                   fileName.EndsWith(".cr2") || fileName.EndsWith(".cr3") || 
                   fileName.EndsWith(".raw");
        }
        
        private void ScanAndDownloadNewFiles()
        {
            try
            {
                if (_cameraModel?.Camera == IntPtr.Zero) return;
                
                Console.WriteLine("[SCAN] Scanning SD card for new files to download...");
                
                // Get volume
                IntPtr volume = IntPtr.Zero;
                uint err = EDSDK.EdsGetChildAtIndex(_cameraModel.Camera, 0, out volume);
                if (err != EDSDK.EDS_ERR_OK) return;
                
                // Get DCIM directory
                IntPtr dcimDir = IntPtr.Zero;
                err = EDSDK.EdsGetChildAtIndex(volume, 0, out dcimDir);
                if (err != EDSDK.EDS_ERR_OK)
                {
                    EDSDK.EdsRelease(volume);
                    return;
                }
                
                var allFiles = new List<(IntPtr handle, EDSDK.EdsDirectoryItemInfo info)>();
                
                // Collect all image files
                int dirCount = 0;
                err = EDSDK.EdsGetChildCount(dcimDir, out dirCount);
                if (err == EDSDK.EDS_ERR_OK)
                {
                    for (int i = 0; i < dirCount; i++)
                    {
                        IntPtr folder = IntPtr.Zero;
                        err = EDSDK.EdsGetChildAtIndex(dcimDir, i, out folder);
                        if (err == EDSDK.EDS_ERR_OK)
                        {
                            CollectFilesFromFolder(folder, allFiles);
                            EDSDK.EdsRelease(folder);
                        }
                    }
                }
                
                EDSDK.EdsRelease(dcimDir);
                EDSDK.EdsRelease(volume);
                
                Console.WriteLine($"[SCAN] Found {allFiles.Count} total image files on SD card");
                
                // Find and download new files
                int downloadedCount = 0;
                foreach (var (handle, info) in allFiles)
                {
                    try
                    {
                        var fileKey = $"{info.szFileName}_{info.Size}";
                        
                        lock (_fileTrackingLock)
                        {
                            if (!_processedFiles.Contains(fileKey))
                            {
                                Console.WriteLine($"[SCAN] Found new file: {info.szFileName} ({info.Size} bytes)");
                                
                                // Download the file
                                bool downloadSuccess = DownloadFile(handle, info);
                                
                                if (downloadSuccess)
                                {
                                    // Mark as processed only if download was successful
                                    _processedFiles.Add(fileKey);
                                    _lastProcessedFileName = info.szFileName;
                                    _lastProcessedFileSize = info.Size;
                                    downloadedCount++;
                                    Console.WriteLine($"[SCAN] ✅ Successfully downloaded: {info.szFileName}");
                                }
                                else
                                {
                                    Console.WriteLine($"[SCAN] ❌ Failed to download: {info.szFileName}");
                                }
                            }
                        }
                    }
                    finally
                    {
                        EDSDK.EdsRelease(handle);
                    }
                }
                
                if (downloadedCount > 0)
                {
                    Console.WriteLine($"[SCAN] ✅ Downloaded {downloadedCount} new files");
                }
                else
                {
                    Console.WriteLine("[SCAN] No new files found to download");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SCAN] Error scanning for new files: {ex.Message}");
            }
        }
        
        private void CollectFilesFromFolder(IntPtr folder, List<(IntPtr handle, EDSDK.EdsDirectoryItemInfo info)> allFiles)
        {
            try
            {
                int fileCount = 0;
                uint err = EDSDK.EdsGetChildCount(folder, out fileCount);
                if (err != EDSDK.EDS_ERR_OK) return;

                for (int i = 0; i < fileCount; i++)
                {
                    IntPtr fileItem = IntPtr.Zero;
                    err = EDSDK.EdsGetChildAtIndex(folder, i, out fileItem);
                    
                    if (err == EDSDK.EDS_ERR_OK && fileItem != IntPtr.Zero)
                    {
                        EDSDK.EdsDirectoryItemInfo itemInfo;
                        err = EDSDK.EdsGetDirectoryItemInfo(fileItem, out itemInfo);
                        
                        if (err == EDSDK.EDS_ERR_OK && itemInfo.isFolder == 0 && IsImageFile(itemInfo))
                        {
                            allFiles.Add((fileItem, itemInfo));
                        }
                        else
                        {
                            EDSDK.EdsRelease(fileItem);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SCAN] Error collecting files from folder: {ex.Message}");
            }
        }
        
        private void MarkFileAsProcessed(string fileName, ulong size)
        {
            lock (_fileTrackingLock)
            {
                var fileKey = $"{fileName}_{size}";
                _processedFiles.Add(fileKey);
                _lastProcessedFileName = fileName;
                _lastProcessedFileSize = size;
                Console.WriteLine($"[FILE_TRACK] ✅ File marked as processed: {fileName}");
            }
        }
        
        public void Dispose()
        {
            try
            {
                if (_cameraModel?.Camera != IntPtr.Zero)
                {
                    EDSDK.EdsCloseSession(_cameraModel.Camera);
                    EDSDK.EdsRelease(_cameraModel.Camera);
                }
                EDSDK.EdsTerminateSDK();
                Console.WriteLine("[CLEANUP] Camera service disposed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CLEANUP] Error during disposal: {ex.Message}");
            }
        }
    }
}