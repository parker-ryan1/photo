using System;
using EDSDKLib;

namespace CameraCaptureService
{
    public class CameraModel
    {
        public IntPtr Camera { get; private set; }
        public EDSDK.EdsDeviceInfo deviceInfo;
        
        public CameraModel(IntPtr camera)
        {
            Camera = camera;
        }
    }
}