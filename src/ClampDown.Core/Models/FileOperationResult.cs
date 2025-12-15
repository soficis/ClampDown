namespace ClampDown.Core.Models;

/// <summary>
/// Result of an attempted file operation with detailed status.
/// </summary>
public record FileOperationResult
{
    public bool Success { get; init; }
    public string FilePath { get; init; } = "";
    public FileOperationStatus Status { get; init; }
    public string? ErrorMessage { get; init; }
    public List<ProcessLockDetail>? Lockers { get; init; }
}

/// <summary>
/// Status of a file operation attempt.
/// </summary>
public enum FileOperationStatus
{
    /// <summary>Operation completed successfully.</summary>
    Success,
    
    /// <summary>File is locked by one or more processes.</summary>
    LockedByProcess,
    
    /// <summary>Access denied - may require elevation.</summary>
    AccessDenied,
    
    /// <summary>File or path not found.</summary>
    NotFound,
    
    /// <summary>Scheduled for deletion at next reboot.</summary>
    ScheduledForReboot,
    
    /// <summary>Unknown error occurred.</summary>
    UnknownError
}

/// <summary>
/// Details about a process locking a file.
/// </summary>
public record ProcessLockDetail
{
    public int ProcessId { get; init; }
    public string ProcessName { get; init; } = "";
    public string Description { get; init; } = "";
    public ProcessLockType LockType { get; init; }
    public bool CanTerminate { get; init; }
    public bool IsCriticalProcess { get; init; }
}

/// <summary>
/// Type of process that is locking a resource.
/// </summary>
public enum ProcessLockType
{
    Application,
    Service,
    Explorer,
    SystemCritical,
    Unknown
}
