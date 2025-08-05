using System;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Text.RegularExpressions;

namespace CameraCaptureService
{
    class Program
    {
        private static AutomatedCaptureSystem? _captureSystem;
        private static CameraService? _cameraService;
        private static bool _isRunning = true;

        static async Task Main(string[] args)
        {
            Console.WriteLine("=== Camera Capture Service Starting ===");
            Console.WriteLine($"Started at: {DateTime.Now}");
            
            // Handle Ctrl+C gracefully
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                Console.WriteLine("\nShutdown requested...");
                _isRunning = false;
            };

            try
            {
                // Load configuration
                var config = CaptureConfig.Load("capture_config.json");
                Console.WriteLine($"Configuration loaded:");
                Console.WriteLine($"  Site: {config.site_name}");
                Console.WriteLine($"  Operating hours: {config.start_hour}:00 - {config.end_hour}:00");
                Console.WriteLine($"  Capture interval: {config.sequence_interval_minutes} minutes");
                Console.WriteLine($"  Frames per sequence: {config.frames_per_sequence}");
                Console.WriteLine($"  Base directory: {config.base_directory}");

                // Initialize camera service
                _cameraService = new CameraService();
                await _cameraService.InitializeAsync();

                if (!_cameraService.IsInitialized)
                {
                    Console.WriteLine("ERROR: Camera initialization failed. Exiting...");
                    return;
                }

                // Initialize automated capture system
                var cameraController = _cameraService.GetCameraController();
                if (cameraController != null)
                {
                    _captureSystem = new AutomatedCaptureSystem(config, cameraController, _cameraService);
                    _captureSystem.Start();
                    Console.WriteLine("✅ Automated capture system started");
                }
                else
                {
                    Console.WriteLine("ERROR: Could not get camera controller. Exiting...");
                    return;
                }

                // Keep the service running
                Console.WriteLine("Service is running. Press Ctrl+C to stop.");
                while (_isRunning)
                {
                    await Task.Delay(1000);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FATAL ERROR: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
            finally
            {
                // Cleanup
                Console.WriteLine("Shutting down...");
                _captureSystem?.Stop();
                _cameraService?.Dispose();
                Console.WriteLine("Service stopped.");
            }
        }
    }


    public class AutomatedCaptureSystem
    {
        private readonly CaptureConfig _config;
        private readonly CameraController _controller;
        private readonly CameraService _cameraService;
        private System.Threading.Timer? _timer;
        private DateTime _lastCaptureTime = DateTime.MinValue;
        private bool _isRunning = false;
        
        // Prevent overlapping sequences
        private readonly object _sequenceLock = new object();
        private volatile bool _sequenceInProgress = false;

        public AutomatedCaptureSystem(CaptureConfig config, CameraController controller, CameraService cameraService)
        {
            _config = config;
            _controller = controller;
            _cameraService = cameraService;
        }

        public void Start()
        {
            if (_isRunning) return;
            _isRunning = true;
            _lastCaptureTime = DateTime.MinValue;
            Console.WriteLine("[AUTOMATION] Starting automated capture system");
            _timer = new System.Threading.Timer(async _ => await CheckAndCaptureAsync(), null, TimeSpan.Zero, TimeSpan.FromMinutes(1));
        }

        public void Stop()
        {
            _isRunning = false;
            
            lock (_sequenceLock)
            {
                _sequenceInProgress = false;
            }
            
            if (_timer != null)
            {
                Console.WriteLine("[AUTOMATION] Stopping automated capture system");
                _timer.Dispose();
                _timer = null;
            }
        }

        private async Task CheckAndCaptureAsync()
        {
            if (!_isRunning) return;

            var now = DateTime.Now;
            Console.WriteLine($"[AUTOMATION] Checking capture at {now:HH:mm:ss}");
            
            // Prevent overlapping sequences
            lock (_sequenceLock)
            {
                if (_sequenceInProgress)
                {
                    Console.WriteLine("[AUTOMATION] ⏭️ Sequence already in progress - skipping check");
                    return;
                }
            }
            
            if (now.Hour < _config.start_hour || now.Hour >= _config.end_hour)
            {
                Console.WriteLine($"[AUTOMATION] Outside operating hours ({_config.start_hour}:00-{_config.end_hour}:00)");
                return;
            }

            if (_lastCaptureTime == DateTime.MinValue || (now - _lastCaptureTime).TotalMinutes >= _config.sequence_interval_minutes)
            {
                // Mark sequence as starting
                lock (_sequenceLock)
                {
                    _sequenceInProgress = true;
                }
                
                try
                {
                    Console.WriteLine($"[AUTOMATION] 🚀 Starting capture sequence at {now:HH:mm:ss}");
                    await CaptureSequenceAsync(now);
                    _lastCaptureTime = now;
                    Console.WriteLine($"[AUTOMATION] ✅ Capture sequence completed at {now:HH:mm:ss}");
                }
                finally
                {
                    lock (_sequenceLock)
                    {
                        _sequenceInProgress = false;
                    }
                }
            }
            else
            {
                var minutesUntilNext = _config.sequence_interval_minutes - (now - _lastCaptureTime).TotalMinutes;
                Console.WriteLine($"[AUTOMATION] Waiting {minutesUntilNext:F1} minutes until next capture");
                
                // Keep camera alive with periodic test shots (only if we've had a successful sequence)
                if (_lastCaptureTime != DateTime.MinValue && minutesUntilNext > 5)
                {
                    await KeepCameraAlive();
                }
            }
        }

        private async Task CaptureSequenceAsync(DateTime sessionTime)
        {
            try
            {
                Console.WriteLine($"[AUTOMATION] 🚀 Starting automated capture sequence of {_config.frames_per_sequence} frames");
                
                // Clear SD card first
                await ClearSDCardBeforeSequence();
                
                // Take the sequence of photos
                for (int i = 1; i <= _config.frames_per_sequence; i++)
                {
                    if (!_isRunning || !_sequenceInProgress) break;
                    
                    Console.WriteLine($"[AUTOMATION] Taking automated picture {i}/{_config.frames_per_sequence}");
                    
                    bool captureSuccess = _cameraService.TriggerManualCapture(true);
                    
                    if (!captureSuccess)
                    {
                        Console.WriteLine($"[AUTOMATION] ❌ Failed to capture frame {i}, continuing with sequence");
                    }
                    else
                    {
                        Console.WriteLine($"[AUTOMATION] ✅ Frame {i} capture triggered successfully");
                        
                        // Wait for capture processing
                        Console.WriteLine("[AUTOMATION] ⏳ Waiting for capture processing...");
                        await Task.Delay(5000);
                        
                        // Verify the photo was taken
                        var cardFiles = await _cameraService.GetCardFilesAsync();
                        Console.WriteLine($"[AUTOMATION] 📊 After photo {i}: {cardFiles.Count} files on SD card");
                    }
                    
                    // Wait between frames (except for the last one)
                    if (i < _config.frames_per_sequence)
                    {
                        Console.WriteLine($"[AUTOMATION] ⏳ Waiting {_config.frame_delay_seconds} seconds before next picture");
                        await Task.Delay(TimeSpan.FromSeconds(_config.frame_delay_seconds));
                        
                        // Additional delay to ensure camera is ready
                        Console.WriteLine("[AUTOMATION] ⏳ Additional camera ready delay...");
                        await Task.Delay(2000);
                    }
                }
                Console.WriteLine($"[AUTOMATION] ✅ Automated capture sequence completed successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AUTOMATION] Error during automated capture sequence: {ex.Message}");
                Console.WriteLine($"[AUTOMATION] Stack trace: {ex.StackTrace}");
            }
        }
        
        private async Task ClearSDCardBeforeSequence()
        {
            try
            {
                Console.WriteLine("[AUTOMATION] 🧹 Clearing SD card before sequence...");
                
                bool formatSuccess = await _cameraService.FormatSDCardAsync();
                
                if (formatSuccess)
                {
                    Console.WriteLine("[AUTOMATION] ✅ SD card formatted successfully");
                    _cameraService.ResetFileTracking();
                    Console.WriteLine("[AUTOMATION] ✅ File tracking reset");
                }
                else
                {
                    Console.WriteLine("[AUTOMATION] ⚠️ SD card format failed, trying alternative...");
                    _cameraService.ResetFileTracking();
                    Console.WriteLine("[AUTOMATION] ✅ File tracking reset (format failed but continuing)");
                }
                
                await Task.Delay(2000);
                
                var cardFiles = await _cameraService.GetCardFilesAsync();
                Console.WriteLine($"[AUTOMATION] 📊 SD card status after clearing: {cardFiles.Count} files remaining");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AUTOMATION] ❌ Error clearing SD card: {ex.Message}");
                _cameraService.ResetFileTracking();
                Console.WriteLine("[AUTOMATION] 🔄 File tracking reset (exception occurred but continuing)");
            }
        }
        
        private async Task KeepCameraAlive()
        {
            try
            {
                Console.WriteLine("[KEEP_ALIVE] 📸 Taking keep-alive photo to maintain camera connection...");
                
                // Take a test photo
                bool captureSuccess = _cameraService.TriggerManualCapture(true);
                
                if (captureSuccess)
                {
                    Console.WriteLine("[KEEP_ALIVE] ✅ Keep-alive photo captured successfully");
                    
                    // Wait for the photo to be processed
                    await Task.Delay(3000);
                    
                    // Delete the photo from SD card to keep it clean
                    await DeleteLatestPhotoFromSD();
                }
                else
                {
                    Console.WriteLine("[KEEP_ALIVE] ⚠️ Keep-alive photo failed - camera may need attention");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[KEEP_ALIVE] ❌ Error during keep-alive: {ex.Message}");
            }
        }
        
        private async Task DeleteLatestPhotoFromSD()
        {
            try
            {
                Console.WriteLine("[KEEP_ALIVE] 🗑️ Deleting keep-alive photo from SD card...");
                
                bool deleteSuccess = await _cameraService.DeleteLatestFileFromSDAsync();
                
                if (deleteSuccess)
                {
                    Console.WriteLine("[KEEP_ALIVE] ✅ Keep-alive photo deleted from SD card");
                }
                else
                {
                    Console.WriteLine("[KEEP_ALIVE] ⚠️ Could not delete keep-alive photo from SD card");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[KEEP_ALIVE] ⚠️ Error deleting keep-alive photo: {ex.Message}");
            }
        }
    }

    public class CaptureConfig
    {
        public string site_name { get; set; } = "Waveland";
        public string base_directory { get; set; } = "C:/SeaStateImages/";
        public int start_hour { get; set; } = 6;
        public int end_hour { get; set; } = 18;
        public int sequence_interval_minutes { get; set; } = 60;
        public int frames_per_sequence { get; set; } = 50;
        public int frame_delay_seconds { get; set; } = 10;

        public static CaptureConfig Load(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException($"Config file not found: {path}");
            var json = File.ReadAllText(path);
            return ParseJson(json);
        }

        private static CaptureConfig ParseJson(string json)
        {
            var config = new CaptureConfig();
            
            // Simple JSON parsing
            var siteNameMatch = Regex.Match(json, @"""site_name""\s*:\s*""([^""]+)""");
            if (siteNameMatch.Success)
                config.site_name = siteNameMatch.Groups[1].Value;

            var baseDirMatch = Regex.Match(json, @"""base_directory""\s*:\s*""([^""]+)""");
            if (baseDirMatch.Success)
                config.base_directory = baseDirMatch.Groups[1].Value.Replace("\\", "/");

            var startHourMatch = Regex.Match(json, @"""start_hour""\s*:\s*(\d+)");
            if (startHourMatch.Success)
                config.start_hour = int.Parse(startHourMatch.Groups[1].Value);

            var endHourMatch = Regex.Match(json, @"""end_hour""\s*:\s*(\d+)");
            if (endHourMatch.Success)
                config.end_hour = int.Parse(endHourMatch.Groups[1].Value);

            var intervalMatch = Regex.Match(json, @"""sequence_interval_minutes""\s*:\s*(\d+)");
            if (intervalMatch.Success)
                config.sequence_interval_minutes = int.Parse(intervalMatch.Groups[1].Value);

            var framesMatch = Regex.Match(json, @"""frames_per_sequence""\s*:\s*(\d+)");
            if (framesMatch.Success)
                config.frames_per_sequence = int.Parse(framesMatch.Groups[1].Value);

            var delayMatch = Regex.Match(json, @"""frame_delay_seconds""\s*:\s*(\d+)");
            if (delayMatch.Success)
                config.frame_delay_seconds = int.Parse(delayMatch.Groups[1].Value);

            return config;
        }
    }
}