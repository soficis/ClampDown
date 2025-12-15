using System.ComponentModel;
using System.Runtime.InteropServices;

namespace ClampDown.Win32;

/// <summary>
/// Drive operations for safe removal and volume management.
/// See: https://learn.microsoft.com/windows/win32/api/cfgmgr32/
/// </summary>
public static class DriveOperations
{
    #region Configuration Manager (cfgmgr32)
    
    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    private static extern int CM_Get_Parent(
        out uint pdnDevInst,
        uint dnDevInst,
        uint ulFlags);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    private static extern int CM_Request_Device_EjectW(
        uint dnDevInst,
        out PnpVetoType pVetoType,
        [Out] char[]? pszVetoName,
        int ulNameLength,
        uint ulFlags);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    private static extern int CM_Locate_DevNodeW(
        out uint pdnDevInst,
        string? pDeviceID,
        uint ulFlags);

    private const int CrSuccess = 0;
    private const int CrRemoveVetoed = 0x17;

    #endregion

    #region Volume IOCTLs
    
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateFileW(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(
        IntPtr hDevice,
        uint dwIoControlCode,
        IntPtr lpInBuffer,
        uint nInBufferSize,
        IntPtr lpOutBuffer,
        uint nOutBufferSize,
        out uint lpBytesReturned,
        IntPtr lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint FileShareReadWrite = 0x3;
    private const uint OpenExisting = 3;
    private const uint FsctlLockVolume = 0x00090018;
    private const uint FsctlDismountVolume = 0x00090020;
    private const uint FsctlUnlockVolume = 0x0009001C;

    #endregion

    /// <summary>
    /// Attempts to safely eject a device using Configuration Manager.
    /// </summary>
    public static EjectResult RequestDeviceEject(string deviceInstanceId)
    {
        int result = CM_Locate_DevNodeW(out uint devInst, deviceInstanceId, 0);
        if (result != CrSuccess)
        {
            return new EjectResult 
            { 
                Success = false, 
                ErrorMessage = $"Device not found: {deviceInstanceId}" 
            };
        }

        // Get parent device (the actual removable device)
        CM_Get_Parent(out uint parentDevInst, devInst, 0);

        var vetoName = new char[256];
        result = CM_Request_Device_EjectW(
            parentDevInst,
            out PnpVetoType vetoType,
            vetoName,
            vetoName.Length,
            0);

        if (result == CrSuccess)
        {
            return new EjectResult { Success = true };
        }

        var vetoString = new string(vetoName).TrimEnd('\0');
        return new EjectResult
        {
            Success = false,
            VetoType = vetoType,
            VetoName = vetoString,
            ErrorMessage = $"Ejection vetoed by {vetoType}: {vetoString}"
        };
    }

    /// <summary>
    /// Attempts to lock a volume (required before dismount).
    /// </summary>
    public static bool TryLockVolume(string driveLetter, out string? errorMessage)
    {
        errorMessage = null;
        string volumePath = $"\\\\.\\{driveLetter.TrimEnd(':')}:";

        IntPtr handle = CreateFileW(
            volumePath,
            GenericRead | GenericWrite,
            FileShareReadWrite,
            IntPtr.Zero,
            OpenExisting,
            0,
            IntPtr.Zero);

        if (handle == new IntPtr(-1))
        {
            errorMessage = $"Failed to open volume: {Marshal.GetLastWin32Error()}";
            return false;
        }

        try
        {
            bool result = DeviceIoControl(
                handle,
                FsctlLockVolume,
                IntPtr.Zero, 0,
                IntPtr.Zero, 0,
                out _,
                IntPtr.Zero);

            if (!result)
            {
                errorMessage = $"Failed to lock volume: {Marshal.GetLastWin32Error()}";
                return false;
            }

            return true;
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    /// <summary>
    /// Forces volume dismount. WARNING: Can have unpredictable results.
    /// Only use as absolute last resort with explicit user confirmation.
    /// </summary>
    public static bool ForceDismountVolume(string driveLetter, out string? errorMessage)
    {
        errorMessage = null;
        string volumePath = $"\\\\.\\{driveLetter.TrimEnd(':')}:";

        IntPtr handle = CreateFileW(
            volumePath,
            GenericRead | GenericWrite,
            FileShareReadWrite,
            IntPtr.Zero,
            OpenExisting,
            0,
            IntPtr.Zero);

        if (handle == new IntPtr(-1))
        {
            errorMessage = $"Failed to open volume: {Marshal.GetLastWin32Error()}";
            return false;
        }

        try
        {
            // Try to lock first (recommended)
            DeviceIoControl(handle, FsctlLockVolume, IntPtr.Zero, 0, IntPtr.Zero, 0, out _, IntPtr.Zero);

            // Dismount
            bool result = DeviceIoControl(
                handle,
                FsctlDismountVolume,
                IntPtr.Zero, 0,
                IntPtr.Zero, 0,
                out _,
                IntPtr.Zero);

            if (!result)
            {
                errorMessage = $"Failed to dismount volume: {Marshal.GetLastWin32Error()}";
                return false;
            }

            return true;
        }
        finally
        {
            CloseHandle(handle);
        }
    }
}

/// <summary>
/// Result of a device eject attempt.
/// </summary>
public record EjectResult
{
    public bool Success { get; init; }
    public PnpVetoType VetoType { get; init; }
    public string? VetoName { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Reasons a device eject request may be vetoed.
/// </summary>
public enum PnpVetoType
{
    Ok = 0,
    TypeUnknown = 1,
    LegacyDevice = 2,
    PendingClose = 3,
    WindowsApp = 4,
    WindowsService = 5,
    OutstandingOpen = 6,
    Device = 7,
    Driver = 8,
    IllegalDeviceRequest = 9,
    InsufficientPower = 10,
    NonDisableable = 11,
    LegacyDriver = 12,
    InsufficientRights = 13
}
