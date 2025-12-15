using System.Runtime.InteropServices;

namespace ClampDown.Win32;

/// <summary>
/// Native P/Invoke declarations for Restart Manager API.
/// See: https://learn.microsoft.com/windows/win32/rstmgr/functions
/// </summary>
internal static class RestartManagerNativeMethods
{
    private const string RestartManagerDll = "rstrtmgr.dll";

    /// <summary>
    /// Starts a new Restart Manager session.
    /// </summary>
    [DllImport(RestartManagerDll, CharSet = CharSet.Unicode)]
    public static extern int RmStartSession(
        out uint pSessionHandle,
        int dwSessionFlags,
        string strSessionKey);

    /// <summary>
    /// Registers resources (files, processes, or services) to a Restart Manager session.
    /// </summary>
    [DllImport(RestartManagerDll, CharSet = CharSet.Unicode)]
    public static extern int RmRegisterResources(
        uint dwSessionHandle,
        uint nFiles,
        string[]? rgsFilenames,
        uint nApplications,
        RmUniqueProcess[]? rgApplications,
        uint nServices,
        string[]? rgsServiceNames);

    /// <summary>
    /// Gets a list of all applications and services using registered resources.
    /// </summary>
    [DllImport(RestartManagerDll)]
    public static extern int RmGetList(
        uint dwSessionHandle,
        out uint pnProcInfoNeeded,
        ref uint pnProcInfo,
        [In, Out] RmProcessInfo[]? rgAffectedApps,
        out uint lpdwRebootReasons);

    /// <summary>
    /// Shuts down and optionally restarts the applications registered with the session.
    /// </summary>
    [DllImport(RestartManagerDll)]
    public static extern int RmShutdown(
        uint dwSessionHandle,
        RmShutdownType lActionFlags,
        RmWriteStatusCallback? fnStatus);

    /// <summary>
    /// Restarts applications that were shut down by RmShutdown.
    /// </summary>
    [DllImport(RestartManagerDll)]
    public static extern int RmRestart(
        uint dwSessionHandle,
        int dwRestartFlags,
        RmWriteStatusCallback? fnStatus);

    /// <summary>
    /// Ends the Restart Manager session.
    /// </summary>
    [DllImport(RestartManagerDll)]
    public static extern int RmEndSession(uint dwSessionHandle);

    // Error codes
    public const int ErrorSuccess = 0;
    public const int ErrorMoreData = 234;
}

/// <summary>
/// Callback delegate for shutdown/restart status updates.
/// </summary>
public delegate void RmWriteStatusCallback(uint nPercentComplete);

/// <summary>
/// Uniquely identifies a process by PID and start time.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct RmUniqueProcess
{
    public uint ProcessId;
    public System.Runtime.InteropServices.ComTypes.FILETIME ProcessStartTime;
}

/// <summary>
/// Information about a process that is using registered resources.
/// </summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct RmProcessInfo
{
    public RmUniqueProcess Process;
    
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
    public string ApplicationName;
    
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
    public string ServiceShortName;
    
    public RmAppType ApplicationType;
    public uint AppStatus;
    public uint TsSessionId;
    
    [MarshalAs(UnmanagedType.Bool)]
    public bool Restartable;
}

/// <summary>
/// Type of application using the resource.
/// </summary>
public enum RmAppType
{
    Unknown = 0,
    MainWindow = 1,
    OtherWindow = 2,
    Service = 3,
    Explorer = 4,
    Console = 5,
    Critical = 1000
}

/// <summary>
/// Shutdown action flags.
/// </summary>
[Flags]
public enum RmShutdownType : uint
{
    /// <summary>Force unresponsive applications to shut down.</summary>
    ForceShutdown = 0x1,
    
    /// <summary>Shut down only applications that are registered for restart.</summary>
    ShutdownOnlyRegistered = 0x10
}

/// <summary>
/// Reasons a reboot might be required.
/// </summary>
[Flags]
public enum RmRebootReason : uint
{
    None = 0,
    PermissionDenied = 1,
    SessionMismatch = 2,
    CriticalProcess = 4,
    CriticalService = 8,
    DetectedSelf = 16
}
