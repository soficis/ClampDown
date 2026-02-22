using System.ComponentModel;

namespace ClampDown.Win32;

/// <summary>
/// High-level client for the Windows Restart Manager API.
/// Identifies processes/services locking files and provides graceful shutdown.
/// </summary>
public sealed class RestartManagerClient : IDisposable
{
    private uint _sessionHandle;
    private bool _sessionStarted;
    private bool _disposed;

    /// <summary>
    /// Starts a new Restart Manager session.
    /// </summary>
    public void StartSession()
    {
        ThrowIfDisposed();

        if (_sessionStarted)
            throw new InvalidOperationException("Session already started.");

        string sessionKey = Guid.NewGuid().ToString();
        int result = RestartManagerNativeMethods.RmStartSession(
            out _sessionHandle,
            0,
            sessionKey);

        if (result != RestartManagerNativeMethods.ErrorSuccess)
            throw new Win32Exception(result, "Failed to start Restart Manager session.");

        _sessionStarted = true;
    }

    /// <summary>
    /// Ends the current Restart Manager session.
    /// </summary>
    public void EndSession()
    {
        ThrowIfDisposed();

        if (!_sessionStarted)
            return;

        RestartManagerNativeMethods.RmEndSession(_sessionHandle);
        _sessionStarted = false;
    }

    /// <summary>
    /// Registers file paths to track which processes are using them.
    /// </summary>
    public void RegisterResources(IEnumerable<string> filePaths)
    {
        ThrowIfDisposed();
        EnsureSessionStarted();

        var files = filePaths.ToArray();
        int result = RestartManagerNativeMethods.RmRegisterResources(
            _sessionHandle,
            (uint)files.Length,
            files,
            0, null,
            0, null);

        if (result != RestartManagerNativeMethods.ErrorSuccess)
            throw new Win32Exception(result, "Failed to register resources.");
    }

    /// <summary>
    /// Gets information about all processes using the registered resources.
    /// </summary>
    public List<ProcessLockInfo> GetLockers()
    {
        ThrowIfDisposed();
        EnsureSessionStarted();

        uint needed = 0;
        uint count = 0;

        // First call to get count
        int result = RestartManagerNativeMethods.RmGetList(
            _sessionHandle,
            out needed,
            ref count,
            null,
            out _);

        if (result == RestartManagerNativeMethods.ErrorSuccess && needed == 0)
            return new List<ProcessLockInfo>();

        if (result != RestartManagerNativeMethods.ErrorMoreData)
            throw new Win32Exception(result, "Failed to get locker list size.");

        // Allocate and get actual data
        var processInfos = new RmProcessInfo[needed];
        count = needed;

        result = RestartManagerNativeMethods.RmGetList(
            _sessionHandle,
            out needed,
            ref count,
            processInfos,
            out uint rebootReasons);

        if (result != RestartManagerNativeMethods.ErrorSuccess)
            throw new Win32Exception(result, "Failed to get locker list.");

        return processInfos
            .Take((int)count)
            .Select(MapToProcessLockInfo)
            .ToList();
    }

    /// <summary>
    /// Requests graceful shutdown of applications using registered resources.
    /// </summary>
    public void RequestShutdown(bool forceUnresponsive = false)
    {
        ThrowIfDisposed();
        EnsureSessionStarted();

        var flags = forceUnresponsive
            ? RmShutdownType.ForceShutdown
            : (RmShutdownType)0;

        int result = RestartManagerNativeMethods.RmShutdown(_sessionHandle, flags, null);

        if (result != RestartManagerNativeMethods.ErrorSuccess)
            throw new Win32Exception(result, "Failed to shutdown applications.");
    }

    private static ProcessLockInfo MapToProcessLockInfo(RmProcessInfo info)
    {
        return new ProcessLockInfo
        {
            ProcessId = (int)info.Process.ProcessId,
            ProcessName = info.ApplicationName,
            ServiceName = info.ServiceShortName,
            ApplicationType = info.ApplicationType,
            IsRestartable = info.Restartable,
            SessionId = (int)info.TsSessionId
        };
    }

    private void EnsureSessionStarted()
    {
        if (!_sessionStarted)
            throw new InvalidOperationException("Session not started. Call StartSession() first.");
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(RestartManagerClient));
    }

    public void Dispose()
    {
        if (_disposed) return;

        EndSession();

        _disposed = true;
    }
}

/// <summary>
/// Information about a process that is locking a file.
/// </summary>
public class ProcessLockInfo
{
    public int ProcessId { get; init; }
    public string ProcessName { get; init; } = "";
    public string ServiceName { get; init; } = "";
    public RmAppType ApplicationType { get; init; }
    public bool IsRestartable { get; init; }
    public int SessionId { get; init; }

    public string Description => ApplicationType switch
    {
        RmAppType.Service => $"Service: {ServiceName}",
        RmAppType.Explorer => "Windows Explorer",
        RmAppType.Critical => "Critical System Process",
        _ => ProcessName
    };
}
