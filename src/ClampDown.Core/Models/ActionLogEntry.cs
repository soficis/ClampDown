namespace ClampDown.Core.Models;

/// <summary>
/// Represents a log entry for an action taken by the application.
/// </summary>
public record ActionLogEntry
{
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public ActionType Type { get; init; }
    public string Target { get; init; } = "";
    public EscalationLevel EscalationLevel { get; init; }
    public int? Win32ErrorCode { get; init; }
    public bool ElevationUsed { get; init; }
    public string Details { get; init; } = "";
    public ActionResult Result { get; init; }
}

/// <summary>
/// Types of actions that can be logged.
/// </summary>
public enum ActionType
{
    FileAnalyze,
    FileDelete,
    FileRename,
    FileCopy,
    ProcessClose,
    ProcessTerminate,
    DriveAnalyze,
    DriveEject,
    VolumeDismount,
    ScheduleRebootDelete
}

/// <summary>
/// Escalation level of an action.
/// </summary>
public enum EscalationLevel
{
    /// <summary>Informational - analysis only.</summary>
    Info = 0,
    
    /// <summary>Graceful - Restart Manager shutdown.</summary>
    Graceful = 1,
    
    /// <summary>Forceful - Process termination.</summary>
    Force = 2,
    
    /// <summary>Scheduled - Deferred to reboot.</summary>
    Scheduled = 3,
    
    /// <summary>Critical - Volume dismount (risky).</summary>
    Critical = 4
}

/// <summary>
/// Result of an action.
/// </summary>
public enum ActionResult
{
    Success,
    PartialSuccess,
    Failed,
    Cancelled,
    RequiresReboot
}
