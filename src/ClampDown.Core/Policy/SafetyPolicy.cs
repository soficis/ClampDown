namespace ClampDown.Core.Policy;

/// <summary>
/// Safety policy for process termination decisions.
/// Protects critical system processes and enforces confirmation requirements.
/// </summary>
public class SafetyPolicy
{
    /// <summary>
    /// Critical system processes that should NEVER be terminated.
    /// </summary>
    private static readonly HashSet<string> CriticalProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "System",
        "System.exe",
        "smss.exe",
        "csrss.exe",
        "wininit.exe",
        "services.exe",
        "lsass.exe",
        "winlogon.exe",
        "dwm.exe",
        "svchost.exe",
        "fontdrvhost.exe",
        "sihost.exe",
        "taskhostw.exe"
    };

    /// <summary>
    /// Processes that should trigger warnings before termination.
    /// </summary>
    private static readonly HashSet<string> WarningProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "explorer.exe",
        "SearchHost.exe",
        "StartMenuExperienceHost.exe",
        "ShellExperienceHost.exe"
    };

    /// <summary>
    /// Evaluates whether a process can be terminated and returns approval status.
    /// </summary>
    public TerminationApproval CanTerminate(string processName, int processId)
    {
        if (string.IsNullOrEmpty(processName))
            return TerminationApproval.Blocked("Unknown process");

        // Normalize name to include .exe
        var normalizedName = processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? processName
            : $"{processName}.exe";

        // Block critical system processes
        if (CriticalProcesses.Contains(normalizedName))
        {
            return TerminationApproval.Blocked(
                $"{processName} is a critical system process and cannot be terminated.");
        }

        // Warn for shell/explorer processes
        if (WarningProcesses.Contains(normalizedName))
        {
            return TerminationApproval.AllowedWithWarning(
                $"Terminating {processName} may affect your desktop experience. " +
                "Windows Explorer will restart automatically.");
        }

        // System process (PID 0 or 4)
        if (processId is 0 or 4)
        {
            return TerminationApproval.Blocked("Cannot terminate System process.");
        }

        return TerminationApproval.Allowed();
    }

    /// <summary>
    /// Checks if a process is on the critical blocklist.
    /// </summary>
    public bool IsCriticalProcess(string processName)
    {
        var normalizedName = processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? processName
            : $"{processName}.exe";
            
        return CriticalProcesses.Contains(normalizedName);
    }
}

/// <summary>
/// Result of a process termination approval check.
/// </summary>
public record TerminationApproval
{
    public bool IsAllowed { get; init; }
    public bool RequiresWarning { get; init; }
    public string? Message { get; init; }

    public static TerminationApproval Allowed() => 
        new() { IsAllowed = true };
    
    public static TerminationApproval AllowedWithWarning(string message) => 
        new() { IsAllowed = true, RequiresWarning = true, Message = message };
    
    public static TerminationApproval Blocked(string reason) => 
        new() { IsAllowed = false, Message = reason };
}
