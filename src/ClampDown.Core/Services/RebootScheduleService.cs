using System.ComponentModel;
using System.Text;
using ClampDown.Core.HelperIpc;
using ClampDown.Win32;

namespace ClampDown.Core.Services;

public sealed class RebootScheduleService
{
    private const int ErrorAccessDenied = 5;
    private const int ErrorPrivilegeNotHeld = 1314;
    private const int ErrorElevationRequired = 740;

    private readonly ElevatedHelperClient _helperClient;

    public RebootScheduleService(ElevatedHelperClient helperClient)
    {
        _helperClient = helperClient;
    }

    public RebootScheduleResult TryScheduleDeleteOnReboot(string filePath, TimeSpan helperTimeout)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be empty.", nameof(filePath));

        var normalized = Path.GetFullPath(filePath);

        try
        {
            FileOperations.ScheduleDeleteOnReboot(normalized);
            return RebootScheduleResult.Succeeded();
        }
        catch (Win32Exception ex) when (RequiresElevation(ex.NativeErrorCode))
        {
            return TryScheduleViaHelper(
                new HelperRequest
                {
                    Command = HelperCommand.ScheduleDeleteOnReboot,
                    UserConfirmed = true,
                    SourcePath = normalized
                },
                helperTimeout,
                fallbackError: ex);
        }
        catch (Win32Exception ex)
        {
            return RebootScheduleResult.Failed(ex.Message, ex.NativeErrorCode);
        }
        catch (Exception ex)
        {
            return RebootScheduleResult.Failed(ex.Message);
        }
    }

    public RebootScheduleResult TryScheduleMoveOnReboot(string sourcePath, string destinationPath, TimeSpan helperTimeout)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
            throw new ArgumentException("Source path cannot be empty.", nameof(sourcePath));
        if (string.IsNullOrWhiteSpace(destinationPath))
            throw new ArgumentException("Destination path cannot be empty.", nameof(destinationPath));

        var normalizedSource = Path.GetFullPath(sourcePath);
        var normalizedDest = Path.GetFullPath(destinationPath);

        try
        {
            FileOperations.ScheduleMoveOnReboot(normalizedSource, normalizedDest);
            return RebootScheduleResult.Succeeded();
        }
        catch (Win32Exception ex) when (RequiresElevation(ex.NativeErrorCode))
        {
            return TryScheduleViaHelper(
                new HelperRequest
                {
                    Command = HelperCommand.ScheduleMoveOnReboot,
                    UserConfirmed = true,
                    SourcePath = normalizedSource,
                    DestinationPath = normalizedDest
                },
                helperTimeout,
                fallbackError: ex);
        }
        catch (Win32Exception ex)
        {
            return RebootScheduleResult.Failed(ex.Message, ex.NativeErrorCode);
        }
        catch (Exception ex)
        {
            return RebootScheduleResult.Failed(ex.Message);
        }
    }

    private RebootScheduleResult TryScheduleViaHelper(HelperRequest request, TimeSpan helperTimeout, Win32Exception fallbackError)
    {
        try
        {
            var response = _helperClient.SendAsync(request, timeout: TimeSpan.FromMilliseconds(300)).GetAwaiter().GetResult();
            if (response.Success)
                return RebootScheduleResult.Succeeded(elevationUsed: true, win32ErrorCode: response.Win32ErrorCode);

            return RebootScheduleResult.Failed(
                message: response.ErrorMessage ?? "Elevated helper failed to schedule the operation.",
                win32ErrorCode: response.Win32ErrorCode,
                elevationUsed: true);
        }
        catch (Exception firstSendEx)
        {
            var helperPath = HelperProcessLocator.FindHelperExecutablePath();
            if (string.IsNullOrWhiteSpace(helperPath))
            {
                return RebootScheduleResult.Failed(
                    BuildElevationRequiredMessage(fallbackError, firstSendEx),
                    win32ErrorCode: fallbackError.NativeErrorCode);
            }

            var startError = HelperProcessLocator.TryStartElevatedHelper(helperPath, out var startErrorMessage);
            if (!startError)
            {
                return RebootScheduleResult.Failed(
                    startErrorMessage ?? BuildElevationRequiredMessage(fallbackError, firstSendEx),
                    win32ErrorCode: fallbackError.NativeErrorCode);
            }

            try
            {
                var response = _helperClient.SendAsync(request, helperTimeout).GetAwaiter().GetResult();
                if (response.Success)
                    return RebootScheduleResult.Succeeded(elevationUsed: true, win32ErrorCode: response.Win32ErrorCode);

                return RebootScheduleResult.Failed(
                    message: response.ErrorMessage ?? "Elevated helper failed to schedule the operation.",
                    win32ErrorCode: response.Win32ErrorCode,
                    elevationUsed: true);
            }
            catch (Exception secondSendEx)
            {
                return RebootScheduleResult.Failed(
                    BuildElevationRequiredMessage(fallbackError, secondSendEx),
                    win32ErrorCode: fallbackError.NativeErrorCode);
            }
        }
    }

    private static bool RequiresElevation(int win32ErrorCode)
    {
        return win32ErrorCode is ErrorAccessDenied or ErrorPrivilegeNotHeld or ErrorElevationRequired;
    }

    private static string BuildElevationRequiredMessage(Win32Exception scheduleError, Exception helperError)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Scheduling file operations for reboot requires elevation.");
        sb.AppendLine();
        sb.AppendLine($"Schedule error: {scheduleError.Message}");
        sb.AppendLine($"Helper error: {helperError.Message}");
        sb.AppendLine();
        sb.AppendLine("Run ClampDown as Administrator, or start ClampDown.Helper elevated and try again.");
        return sb.ToString().Trim();
    }
}

public sealed record RebootScheduleResult
{
    public bool Success { get; init; }
    public int? Win32ErrorCode { get; init; }
    public bool ElevationUsed { get; init; }
    public string? ErrorMessage { get; init; }

    public static RebootScheduleResult Succeeded(bool elevationUsed = false, int? win32ErrorCode = null) => new()
    {
        Success = true,
        ElevationUsed = elevationUsed,
        Win32ErrorCode = win32ErrorCode
    };

    public static RebootScheduleResult Failed(string message, int? win32ErrorCode = null, bool elevationUsed = false) => new()
    {
        Success = false,
        ErrorMessage = message,
        Win32ErrorCode = win32ErrorCode,
        ElevationUsed = elevationUsed
    };
}
