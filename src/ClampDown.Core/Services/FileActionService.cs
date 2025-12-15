using System.ComponentModel;
using ClampDown.Core.Models;
using ClampDown.Win32;

namespace ClampDown.Core.Services;

public sealed class FileActionService
{
    private readonly ActionLogger _actionLogger;
    private readonly FileLockAnalysisService _analysisService;

    public FileActionService(ActionLogger actionLogger, FileLockAnalysisService analysisService)
    {
        _actionLogger = actionLogger;
        _analysisService = analysisService;
    }

    public FileOperationResult TryDelete(string filePath, bool sendToRecycleBin, bool scheduleOnRebootIfBlocked)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be empty.", nameof(filePath));

        var normalizedPath = Path.GetFullPath(filePath);

        if (!File.Exists(normalizedPath))
        {
            _actionLogger.Log(ActionType.FileDelete, normalizedPath, ActionResult.Failed, details: "File not found.");
            return new FileOperationResult
            {
                Success = false,
                FilePath = normalizedPath,
                Status = FileOperationStatus.NotFound,
                ErrorMessage = "File not found."
            };
        }

        try
        {
            if (sendToRecycleBin)
            {
                FileOperations.SendToRecycleBin(normalizedPath);
            }
            else
            {
                File.Delete(normalizedPath);
            }

            _actionLogger.Log(ActionType.FileDelete, normalizedPath, ActionResult.Success);
            return new FileOperationResult
            {
                Success = true,
                FilePath = normalizedPath,
                Status = FileOperationStatus.Success
            };
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
                scheduleOnRebootIfBlocked,
                () => FileOperations.ScheduleDeleteOnReboot(normalizedPath));
        }
        catch (Win32Exception ex)
        {
            return HandleIoFailure(
                ActionType.FileDelete,
                normalizedPath,
                analysisPath: normalizedPath,
                ex,
                scheduleOnRebootIfBlocked,
                () => FileOperations.ScheduleDeleteOnReboot(normalizedPath));
        }
    }

    public FileOperationResult TryMove(string sourcePath, string destinationPath, bool scheduleOnRebootIfBlocked)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
            throw new ArgumentException("Source path cannot be empty.", nameof(sourcePath));
        if (string.IsNullOrWhiteSpace(destinationPath))
            throw new ArgumentException("Destination path cannot be empty.", nameof(destinationPath));

        var normalizedSource = Path.GetFullPath(sourcePath);
        var normalizedDest = Path.GetFullPath(destinationPath);

        if (!File.Exists(normalizedSource))
        {
            _actionLogger.Log(ActionType.FileRename, normalizedSource, ActionResult.Failed, details: "File not found.");
            return Failure(normalizedSource, FileOperationStatus.NotFound, "File not found.");
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(normalizedDest)!);
            File.Move(normalizedSource, normalizedDest, overwrite: true);

            _actionLogger.Log(ActionType.FileRename, $"{normalizedSource} -> {normalizedDest}", ActionResult.Success);
            return new FileOperationResult
            {
                Success = true,
                FilePath = normalizedSource,
                Status = FileOperationStatus.Success
            };
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
                scheduleOnRebootIfBlocked,
                () => FileOperations.ScheduleMoveOnReboot(normalizedSource, normalizedDest));
        }
        catch (Win32Exception ex)
        {
            return HandleIoFailure(
                ActionType.FileRename,
                $"{normalizedSource} -> {normalizedDest}",
                analysisPath: normalizedSource,
                ex,
                scheduleOnRebootIfBlocked,
                () => FileOperations.ScheduleMoveOnReboot(normalizedSource, normalizedDest));
        }
    }

    public FileOperationResult TryCopy(string sourcePath, string destinationPath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
            throw new ArgumentException("Source path cannot be empty.", nameof(sourcePath));
        if (string.IsNullOrWhiteSpace(destinationPath))
            throw new ArgumentException("Destination path cannot be empty.", nameof(destinationPath));

        var normalizedSource = Path.GetFullPath(sourcePath);
        var normalizedDest = Path.GetFullPath(destinationPath);

        if (!File.Exists(normalizedSource))
        {
            _actionLogger.Log(ActionType.FileCopy, normalizedSource, ActionResult.Failed, details: "File not found.");
            return Failure(normalizedSource, FileOperationStatus.NotFound, "File not found.");
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(normalizedDest)!);
            File.Copy(normalizedSource, normalizedDest, overwrite: true);

            _actionLogger.Log(ActionType.FileCopy, $"{normalizedSource} -> {normalizedDest}", ActionResult.Success);
            return new FileOperationResult
            {
                Success = true,
                FilePath = normalizedSource,
                Status = FileOperationStatus.Success
            };
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
        bool scheduleOnRebootIfBlocked,
        Action? scheduleAction = null)
    {
        var lockers = _analysisService.AnalyzePath(analysisPath, scanRecursively: false);

        if (scheduleOnRebootIfBlocked && scheduleAction != null)
        {
            try
            {
                scheduleAction();
                _actionLogger.Log(type, target, ActionResult.RequiresReboot, EscalationLevel.Scheduled, ex.Message);
                return new FileOperationResult
                {
                    Success = true,
                    FilePath = target,
                    Status = FileOperationStatus.ScheduledForReboot,
                    ErrorMessage = ex.Message,
                    Lockers = lockers.ToList()
                };
            }
            catch (Exception scheduleEx)
            {
                _actionLogger.Log(type, target, ActionResult.Failed, EscalationLevel.Scheduled, scheduleEx.Message);
                return Failure(target, FileOperationStatus.LockedByProcess, scheduleEx.Message, lockers);
            }
        }

        _actionLogger.Log(type, target, ActionResult.Failed, EscalationLevel.Info, ex.Message);
        return Failure(target, FileOperationStatus.LockedByProcess, ex.Message, lockers);
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
