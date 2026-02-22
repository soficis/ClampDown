using System.Diagnostics;
using ClampDown.Core.Policy;

namespace ClampDown.Core.Services;

/// <summary>
/// Handles process termination with safety checks.
/// </summary>
public class ProcessTerminator
{
    private readonly SafetyPolicy _safetyPolicy;

    public ProcessTerminator(SafetyPolicy safetyPolicy)
    {
        _safetyPolicy = safetyPolicy;
    }

    /// <summary>
    /// Attempts to gracefully close a process by closing its main window.
    /// </summary>
    public TerminationResult TryGracefulClose(int processId, TimeSpan timeout)
    {
        try
        {
            var process = Process.GetProcessById(processId);
            var approval = _safetyPolicy.CanTerminate(process.ProcessName, processId);

            if (!approval.IsAllowed)
                return TerminationResult.Blocked(approval.Message!);

            if (process.MainWindowHandle == IntPtr.Zero)
                return TerminationResult.Failed("Process has no main window.");

            process.CloseMainWindow();
            bool exited = process.WaitForExit((int)timeout.TotalMilliseconds);

            return exited
                ? TerminationResult.Success()
                : TerminationResult.Failed("Process did not respond to close request.");
        }
        catch (ArgumentException)
        {
            return TerminationResult.Success(); // Process already exited
        }
        catch (Exception ex)
        {
            return TerminationResult.Failed(ex.Message);
        }
    }

    /// <summary>
    /// Force terminates a process. Requires explicit user confirmation.
    /// </summary>
    public TerminationResult ForceTerminate(int processId, bool killEntireTree = false)
    {
        try
        {
            var process = Process.GetProcessById(processId);
            var approval = _safetyPolicy.CanTerminate(process.ProcessName, processId);

            if (!approval.IsAllowed)
                return TerminationResult.Blocked(approval.Message!);

            if (killEntireTree)
            {
                process.Kill(entireProcessTree: true);
            }
            else
            {
                process.Kill();
            }

            process.WaitForExit(5000);
            return TerminationResult.Success();
        }
        catch (ArgumentException)
        {
            return TerminationResult.Success(); // Process already exited
        }
        catch (Exception ex)
        {
            return TerminationResult.Failed(ex.Message);
        }
    }
}

/// <summary>
/// Result of a termination attempt.
/// </summary>
public record TerminationResult
{
    public bool IsSuccess { get; init; }
    public bool WasBlocked { get; init; }
    public string? ErrorMessage { get; init; }

    public static TerminationResult Success() =>
        new() { IsSuccess = true };

    public static TerminationResult Failed(string message) =>
        new() { ErrorMessage = message };

    public static TerminationResult Blocked(string reason) =>
        new() { WasBlocked = true, ErrorMessage = reason };
}
