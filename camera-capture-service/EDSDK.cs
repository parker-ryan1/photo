using System;
using System.Runtime.InteropServices;

namespace EDSDKLib
{
    public class EDSDK
    {
        // Error codes
        public const uint EDS_ERR_OK = 0x00000000;
        
        // Property IDs
        public const uint PropID_SaveTo = 0x00000001;
        public const uint PropID_ProductName = 0x00000002;
        
        // Save destinations
        public enum EdsSaveTo : uint
        {
            Camera = 1,
            Host = 2,
            Both = Camera | Host
        }
        
        // Shutter button states
        public enum EdsShutterButton : uint
        {
            CameraCommand_ShutterButton_OFF = 0x00000000,
            CameraCommand_ShutterButton_Halfway = 0x00000001,
            CameraCommand_ShutterButton_Completely = 0x00000003
        }
        
        // Device info structure
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct EdsDeviceInfo
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string szPortName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string szDeviceDescription;
            public uint DeviceSubType;
            public uint Reserved;
        }
        
        // Directory item info
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct EdsDirectoryItemInfo
        {
            public ulong Size;
            public byte isFolder;
            public uint GroupID;
            public uint Option;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string szFileName;
            public EdsDateTime DateTime;
        }
        
        // Date time structure
        [StructLayout(LayoutKind.Sequential)]
        public struct EdsDateTime
        {
            public uint Year;
            public uint Month;
            public uint Day;
            public uint Hour;
            public uint Minute;
            public uint Second;
            public uint Milliseconds;
        }
        
        // Point structure
        [StructLayout(LayoutKind.Sequential)]
        public struct EdsPoint
        {
            public int x;
            public int y;
        }
        
        // Event handlers
        public delegate uint EdsPropertyEventHandler(uint inEvent, uint inPropertyID, uint inParam, IntPtr inContext);
        public delegate uint EdsObjectEventHandler(uint inEvent, IntPtr inRef, IntPtr inContext);
        public delegate uint EdsStateEventHandler(uint inEvent, uint inEventData, IntPtr inContext);
        
        // SDK Functions
        [DllImport("EDSDK.dll")]
        public static extern uint EdsInitializeSDK();
        
        [DllImport("EDSDK.dll")]
        public static extern uint EdsTerminateSDK();
        
        [DllImport("EDSDK.dll")]
        public static extern uint EdsGetCameraList(out IntPtr outCameraListRef);
        
        [DllImport("EDSDK.dll")]
        public static extern uint EdsGetChildCount(IntPtr inRef, out int outCount);
        
        [DllImport("EDSDK.dll")]
        public static extern uint EdsGetChildAtIndex(IntPtr inRef, int inIndex, out IntPtr outRef);
        
        [DllImport("EDSDK.dll")]
        public static extern uint EdsRelease(IntPtr inRef);
        
        [DllImport("EDSDK.dll")]
        public static extern uint EdsGetDeviceInfo(IntPtr inCameraRef, out EdsDeviceInfo outDeviceInfo);
        
        [DllImport("EDSDK.dll")]
        public static extern uint EdsOpenSession(IntPtr inCameraRef);
        
        [DllImport("EDSDK.dll")]
        public static extern uint EdsCloseSession(IntPtr inCameraRef);
        
        [DllImport("EDSDK.dll")]
        public static extern uint EdsGetPropertyData(IntPtr inRef, uint inPropertyID, int inParam, out uint outPropertyData);
        
        [DllImport("EDSDK.dll")]
        public static extern uint EdsSetPropertyData(IntPtr inRef, uint inPropertyID, int inParam, int inPropertySize, uint inPropertyData);
        
        [DllImport("EDSDK.dll")]
        public static extern uint EdsSendCommand(IntPtr inCameraRef, uint inCommand, int inParam);
        
        [DllImport("EDSDK.dll")]
        public static extern uint EdsGetDirectoryItemInfo(IntPtr inDirItemRef, out EdsDirectoryItemInfo outDirItemInfo);
        
        [DllImport("EDSDK.dll")]
        public static extern uint EdsDownload(IntPtr inDirItemRef, ulong inReadSize, IntPtr outStream);
        
        [DllImport("EDSDK.dll")]
        public static extern uint EdsDownloadComplete(IntPtr inDirItemRef);
        
        [DllImport("EDSDK.dll")]
        public static extern uint EdsDeleteDirectoryItem(IntPtr inDirItemRef);
        
        [DllImport("EDSDK.dll")]
        public static extern uint EdsSetPropertyEventHandler(IntPtr inCameraRef, uint inEvent, EdsPropertyEventHandler inPropertyEventHandler, IntPtr inContext);
        
        [DllImport("EDSDK.dll")]
        public static extern uint EdsSetObjectEventHandler(IntPtr inCameraRef, uint inEvent, EdsObjectEventHandler inObjectEventHandler, IntPtr inContext);
        
        [DllImport("EDSDK.dll")]
        public static extern uint EdsSetCameraStateEventHandler(IntPtr inCameraRef, uint inEvent, EdsStateEventHandler inStateEventHandler, IntPtr inContext);
        
        [DllImport("EDSDK.dll")]
        public static extern uint EdsGetEvent();
        
        [DllImport("EDSDK.dll")]
        public static extern uint EdsCreateFileStream([MarshalAs(UnmanagedType.LPStr)] string inFileName, uint inCreateDisposition, uint inDesiredAccess, out IntPtr outStream);
        
        [DllImport("EDSDK.dll")]
        public static extern uint EdsFormatVolume(IntPtr inVolumeRef);
        
        // Camera commands
        public const uint CameraCommand_TakePicture = 0x00000000;
        public const uint CameraCommand_PressShutterButton = 0x00000004;
        
        // File creation flags
        public const uint kEdsFileCreateDisposition_CreateNew = 1;
        public const uint kEdsFileCreateDisposition_CreateAlways = 2;
        public const uint kEdsFileCreateDisposition_OpenExisting = 3;
        public const uint kEdsFileCreateDisposition_OpenAlways = 4;
        public const uint kEdsFileCreateDisposition_TruncateExisting = 5;
        
        // File access flags
        public const uint kEdsAccess_Read = 1;
        public const uint kEdsAccess_Write = 2;
        public const uint kEdsAccess_ReadWrite = 3;
        
        // Events
        public const uint kEdsObjectEvent_DirItemCreated = 0x00000201;
        public const uint kEdsObjectEvent_DirItemRemoved = 0x00000202;
        public const uint kEdsObjectEvent_DirItemInfoChanged = 0x00000203;
        public const uint kEdsObjectEvent_DirItemContentChanged = 0x00000204;
        public const uint kEdsObjectEvent_DirItemRequestTransfer = 0x00000205;
        public const uint kEdsObjectEvent_DirItemRequestTransferDT = 0x00000206;
        public const uint kEdsObjectEvent_DirItemCancelTransferDT = 0x00000207;
        public const uint kEdsObjectEvent_VolumeInfoChanged = 0x00000208;
        public const uint kEdsObjectEvent_VolumeUpdateItems = 0x00000209;
        public const uint kEdsObjectEvent_FolderUpdateItems = 0x0000020A;
        public const uint kEdsObjectEvent_DirItemCreatedInCard = 0x0000020B;
        
        public const uint kEdsStateEvent_Shutdown = 0x00000301;
        public const uint kEdsStateEvent_JobStatusChanged = 0x00000302;
        public const uint kEdsStateEvent_WillSoonShutDown = 0x00000303;
        public const uint kEdsStateEvent_ShutDownTimerUpdate = 0x00000304;
        public const uint kEdsStateEvent_CaptureError = 0x00000305;
        public const uint kEdsStateEvent_InternalError = 0x00000306;
        public const uint kEdsStateEvent_AfResult = 0x00000309;
        public const uint kEdsStateEvent_BulbExposureTime = 0x00000310;
    }
}