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

    #endregion

    /// <summary>
    /// Attempts to safely eject a device using Configuration Manager.
    /// </summary>
    public static EjectResult RequestDeviceEject(string deviceInstanceId)
    {
        if (string.IsNullOrWhiteSpace(deviceInstanceId))
        {
            return new EjectResult
            {
                Success = false,
                ErrorMessage = "Device instance ID cannot be empty."
            };
        }

        var locateResult = CM_Locate_DevNodeW(out var devInst, deviceInstanceId, 0);
        if (locateResult != CrSuccess)
        {
            return new EjectResult
            {
                Success = false,
                ErrorMessage = $"Device not found: {deviceInstanceId} (CM error {locateResult})"
            };
        }

        var parentResult = CM_Get_Parent(out var parentDevInst, devInst, 0);
        if (parentResult != CrSuccess)
        {
            return new EjectResult
            {
                Success = false,
                ErrorMessage = $"Unable to locate parent device node. (CM error {parentResult})"
            };
        }

        var vetoName = new char[256];
        var ejectResult = CM_Request_Device_EjectW(
            parentDevInst,
            out var vetoType,
            vetoName,
            vetoName.Length,
            0);

        if (ejectResult == CrSuccess)
            return new EjectResult { Success = true };

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
    /// Forces volume dismount. WARNING: Can have unpredictable results.
    /// Only use as absolute last resort with explicit user confirmation.
    /// </summary>
    public static bool ForceDismountVolume(string driveLetter, out string? errorMessage)
    {
        errorMessage = null;

        if (!TryNormalizeDriveLetter(driveLetter, out var normalizedDrive))
        {
            errorMessage = $"Invalid drive letter: {driveLetter}";
            return false;
        }

        var volumePath = $"\\\\.\\{normalizedDrive}";

        var handle = CreateFileW(
            volumePath,
            GenericRead | GenericWrite,
            FileShareReadWrite,
            IntPtr.Zero,
            OpenExisting,
            0,
            IntPtr.Zero);

        if (handle == new IntPtr(-1))
        {
            var openError = Marshal.GetLastWin32Error();
            errorMessage = $"Failed to open volume: {new Win32Exception(openError).Message} ({openError})";
            return false;
        }

        try
        {
            // Try to lock first (recommended). The lock call may fail if handles are open.
            DeviceIoControl(handle, FsctlLockVolume, IntPtr.Zero, 0, IntPtr.Zero, 0, out _, IntPtr.Zero);

            var dismountResult = DeviceIoControl(
                handle,
                FsctlDismountVolume,
                IntPtr.Zero,
                0,
                IntPtr.Zero,
                0,
                out _,
                IntPtr.Zero);

            if (!dismountResult)
            {
                var dismountError = Marshal.GetLastWin32Error();
                errorMessage = $"Failed to dismount volume: {new Win32Exception(dismountError).Message} ({dismountError})";
                return false;
            }

            return true;
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    private static bool TryNormalizeDriveLetter(string input, out string normalizedDriveLetterWithColon)
    {
        normalizedDriveLetterWithColon = string.Empty;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var trimmed = input.Trim();
        if (trimmed.EndsWith("\\", StringComparison.Ordinal))
            trimmed = trimmed.TrimEnd('\\');

        if (trimmed.Length == 1 && char.IsLetter(trimmed[0]))
            trimmed = $"{char.ToUpperInvariant(trimmed[0])}:";

        if (trimmed.Length == 2 && char.IsLetter(trimmed[0]) && trimmed[1] == ':')
        {
            normalizedDriveLetterWithColon = $"{char.ToUpperInvariant(trimmed[0])}:";
            return true;
        }

        return false;
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
