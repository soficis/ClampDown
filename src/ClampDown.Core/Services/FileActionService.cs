using System.ComponentModel;
using ClampDown.Core.Abstractions;
using ClampDown.Core.Models;

namespace ClampDown.Core.Services;

public sealed class FileActionService
{
    private readonly ActionLogger _actionLogger;
    private readonly FileLockAnalysisService _analysisService;
    private readonly RebootScheduleService _rebootScheduleService;
    private readonly IFilePlatformOperations _filePlatformOperations;

    public FileActionService(
        ActionLogger actionLogger,
        FileLockAnalysisService analysisService,
        RebootScheduleService rebootScheduleService,
        IFilePlatformOperations filePlatformOperations)
    {
        _actionLogger = actionLogger;
        _analysisService = analysisService;
        _rebootScheduleService = rebootScheduleService;
        _filePlatformOperations = filePlatformOperations;
    }

    public FileOperationResult ScheduleDeleteOnReboot(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be empty.", nameof(filePath));

        var normalizedPath = Path.GetFullPath(filePath);
        if (!File.Exists(normalizedPath))
        {
            _actionLogger.Log(ActionType.ScheduleRebootDelete, normalizedPath, ActionResult.Failed, details: "File not found.");
            return Failure(normalizedPath, FileOperationStatus.NotFound, "File not found.");
        }

        var schedule = _rebootScheduleService.TryScheduleDeleteOnReboot(normalizedPath, helperTimeout: TimeSpan.FromSeconds(8));
        if (!schedule.Success)
        {
            _actionLogger.Log(
                ActionType.ScheduleRebootDelete,
                normalizedPath,
                ActionResult.Failed,
                EscalationLevel.Scheduled,
                schedule.ErrorMessage ?? "Scheduling failed.",
                win32Error: schedule.Win32ErrorCode,
                elevationUsed: schedule.ElevationUsed);

            return Failure(normalizedPath, FileOperationStatus.LockedByProcess, schedule.ErrorMessage ?? "Scheduling failed.");
        }

        _actionLogger.Log(
            ActionType.ScheduleRebootDelete,
            normalizedPath,
            ActionResult.RequiresReboot,
            EscalationLevel.Scheduled,
            details: "Scheduled delete at reboot.",
            win32Error: schedule.Win32ErrorCode,
            elevationUsed: schedule.ElevationUsed);

        return new FileOperationResult
        {
            Success = true,
            FilePath = normalizedPath,
            Status = FileOperationStatus.ScheduledForReboot
        };
    }

    public FileOperationResult Delete(DeleteFileRequest request)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        var normalizedPath = Path.GetFullPath(request.FilePath);

        if (!File.Exists(normalizedPath))
        {
            _actionLogger.Log(ActionType.FileDelete, normalizedPath, ActionResult.Failed, details: "File not found.");
            return Failure(normalizedPath, FileOperationStatus.NotFound, "File not found.");
        }

        try
        {
            if (request.DeleteMode == DeleteMode.RecycleBin)
                _filePlatformOperations.SendToRecycleBin(normalizedPath);
            else
                File.Delete(normalizedPath);

            _actionLogger.Log(ActionType.FileDelete, normalizedPath, ActionResult.Success);
            return Success(normalizedPath);
        }
        catch (UnauthorizedAccessException ex)
        {
            _actionLogger.Log(ActionType.FileDelete, normalizedPath, ActionResult.Failed, EscalationLevel.Force, ex.Message);
            return Failure(normalizedPath, FileOperationStatus.AccessDenied, ex.Message);
        }
        catch (IOException ex)
        {
            return HandleIoFailure(
                ActionType.FileDelete,
                normalizedPath,
                analysisPath: normalizedPath,
                ex,
                request.OnBlocked,
                scheduleAction: () => _rebootScheduleService.TryScheduleDeleteOnReboot(normalizedPath, helperTimeout: TimeSpan.FromSeconds(8)));
        }
        catch (Win32Exception ex)
        {
            return HandleIoFailure(
                ActionType.FileDelete,
                normalizedPath,
                analysisPath: normalizedPath,
                ex,
                request.OnBlocked,
                scheduleAction: () => _rebootScheduleService.TryScheduleDeleteOnReboot(normalizedPath, helperTimeout: TimeSpan.FromSeconds(8)));
        }
    }

    public FileOperationResult Move(MoveFileRequest request)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        var normalizedSource = Path.GetFullPath(request.SourcePath);
        var normalizedDest = Path.GetFullPath(request.DestinationPath);

        if (!File.Exists(normalizedSource))
        {
            _actionLogger.Log(ActionType.FileRename, normalizedSource, ActionResult.Failed, details: "File not found.");
            return Failure(normalizedSource, FileOperationStatus.NotFound, "File not found.");
        }

        try
        {
            var destinationDirectory = Path.GetDirectoryName(normalizedDest);
            if (string.IsNullOrWhiteSpace(destinationDirectory))
                throw new ArgumentException("Destination directory is invalid.", nameof(request));

            Directory.CreateDirectory(destinationDirectory);
            File.Move(normalizedSource, normalizedDest, overwrite: true);

            _actionLogger.Log(ActionType.FileRename, $"{normalizedSource} -> {normalizedDest}", ActionResult.Success);
            return Success(normalizedSource);
        }
        catch (UnauthorizedAccessException ex)
        {
            _actionLogger.Log(ActionType.FileRename, normalizedSource, ActionResult.Failed, EscalationLevel.Force, ex.Message);
            return Failure(normalizedSource, FileOperationStatus.AccessDenied, ex.Message);
        }
        catch (IOException ex)
        {
            return HandleIoFailure(
                ActionType.FileRename,
                $"{normalizedSource} -> {normalizedDest}",
                analysisPath: normalizedSource,
                ex,
                request.OnBlocked,
                scheduleAction: () => _rebootScheduleService.TryScheduleMoveOnReboot(normalizedSource, normalizedDest, helperTimeout: TimeSpan.FromSeconds(8)));
        }
        catch (Win32Exception ex)
        {
            return HandleIoFailure(
                ActionType.FileRename,
                $"{normalizedSource} -> {normalizedDest}",
                analysisPath: normalizedSource,
                ex,
                request.OnBlocked,
                scheduleAction: () => _rebootScheduleService.TryScheduleMoveOnReboot(normalizedSource, normalizedDest, helperTimeout: TimeSpan.FromSeconds(8)));
        }
    }

    public FileOperationResult Copy(CopyFileRequest request)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        var normalizedSource = Path.GetFullPath(request.SourcePath);
        var normalizedDest = Path.GetFullPath(request.DestinationPath);

        if (!File.Exists(normalizedSource))
        {
            _actionLogger.Log(ActionType.FileCopy, normalizedSource, ActionResult.Failed, details: "File not found.");
            return Failure(normalizedSource, FileOperationStatus.NotFound, "File not found.");
        }

        try
        {
            var destinationDirectory = Path.GetDirectoryName(normalizedDest);
            if (string.IsNullOrWhiteSpace(destinationDirectory))
                throw new ArgumentException("Destination directory is invalid.", nameof(request));

            Directory.CreateDirectory(destinationDirectory);
            File.Copy(normalizedSource, normalizedDest, overwrite: true);

            _actionLogger.Log(ActionType.FileCopy, $"{normalizedSource} -> {normalizedDest}", ActionResult.Success);
            return Success(normalizedSource);
        }
        catch (UnauthorizedAccessException ex)
        {
            _actionLogger.Log(ActionType.FileCopy, normalizedSource, ActionResult.Failed, EscalationLevel.Force, ex.Message);
            return Failure(normalizedSource, FileOperationStatus.AccessDenied, ex.Message);
        }
        catch (IOException ex)
        {
            var lockers = _analysisService.AnalyzePath(normalizedSource, scanRecursively: false);
            _actionLogger.Log(ActionType.FileCopy, normalizedSource, ActionResult.Failed, EscalationLevel.Info, ex.Message);
            return Failure(normalizedSource, FileOperationStatus.LockedByProcess, ex.Message, lockers);
        }
    }

    private FileOperationResult HandleIoFailure(
        ActionType type,
        string target,
        string analysisPath,
        Exception ex,
        OnBlockedBehavior onBlockedBehavior,
        Func<RebootScheduleResult> scheduleAction)
    {
        var lockers = _analysisService.AnalyzePath(analysisPath, scanRecursively: false);

        if (onBlockedBehavior == OnBlockedBehavior.ScheduleOnReboot)
        {
            var scheduleResult = scheduleAction();
            if (scheduleResult.Success)
            {
                _actionLogger.Log(
                    type,
                    target,
                    ActionResult.RequiresReboot,
                    EscalationLevel.Scheduled,
                    ex.Message,
                    win32Error: scheduleResult.Win32ErrorCode,
                    elevationUsed: scheduleResult.ElevationUsed);

                return new FileOperationResult
                {
                    Success = true,
                    FilePath = target,
                    Status = FileOperationStatus.ScheduledForReboot,
                    ErrorMessage = ex.Message,
                    Lockers = lockers.ToList()
                };
            }

            _actionLogger.Log(
                type,
                target,
                ActionResult.Failed,
                EscalationLevel.Scheduled,
                scheduleResult.ErrorMessage ?? "Scheduling failed.",
                win32Error: scheduleResult.Win32ErrorCode,
                elevationUsed: scheduleResult.ElevationUsed);

            return Failure(target, FileOperationStatus.LockedByProcess, scheduleResult.ErrorMessage ?? ex.Message, lockers);
        }

        _actionLogger.Log(type, target, ActionResult.Failed, EscalationLevel.Info, ex.Message);
        return Failure(target, FileOperationStatus.LockedByProcess, ex.Message, lockers);
    }

    private static FileOperationResult Success(string filePath)
    {
        return new FileOperationResult
        {
            Success = true,
            FilePath = filePath,
            Status = FileOperationStatus.Success
        };
    }

    private static FileOperationResult Failure(
        string filePath,
        FileOperationStatus status,
        string message,
        IReadOnlyList<ProcessLockDetail>? lockers = null)
    {
        return new FileOperationResult
        {
            Success = false,
            FilePath = filePath,
            Status = status,
            ErrorMessage = message,
            Lockers = lockers?.ToList()
        };
    }
}
