using System;
using EDSDKLib;

namespace CameraCaptureService
{
    public class CameraController
    {
        private CameraModel _model;

        public CameraController(CameraModel model)
        {
            _model = model;
        }

        public CameraModel GetModel()
        {
            return _model;
        }

        public bool TakePicture()
        {
            try
            {
                if (_model.Camera == IntPtr.Zero)
                {
                    Console.WriteLine("[CONTROLLER] Camera not available");
                    return false;
                }

                uint err = EDSDK.EdsSendCommand(_model.Camera, EDSDK.CameraCommand_TakePicture, 0);
                
                if (err == EDSDK.EDS_ERR_OK)
                {
                    Console.WriteLine("[CONTROLLER] ✅ Picture command sent successfully");
                    return true;
                }
                else
                {
                    Console.WriteLine($"[CONTROLLER] ❌ Picture command failed: {err}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CONTROLLER] Exception taking picture: {ex.Message}");
                return false;
            }
        }
    }
}