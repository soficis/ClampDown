using ClampDown.Core.Abstractions;
using ClampDown.Core.Models;
using ClampDown.Core.Policy;

namespace ClampDown.Core.Services;

public sealed class FileLockAnalysisService
{
    private readonly SafetyPolicy _safetyPolicy;
    private readonly IRestartManagerGateway _restartManagerGateway;

    public FileLockAnalysisService(SafetyPolicy safetyPolicy, IRestartManagerGateway restartManagerGateway)
    {
        _safetyPolicy = safetyPolicy;
        _restartManagerGateway = restartManagerGateway;
    }

    public IReadOnlyList<ProcessLockDetail> AnalyzePath(
        string path,
        bool scanRecursively,
        int maxFilesToEnumerate = 5000)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path cannot be empty.", nameof(path));

        var normalizedPath = Path.GetFullPath(path);
        var resources = BuildResources(normalizedPath, scanRecursively, maxFilesToEnumerate);
        var lockers = _restartManagerGateway.GetLockers(resources);

        return lockers
            .Select(MapToLockDetail)
            .OrderBy(d => d.IsCriticalProcess)
            .ThenBy(d => d.ProcessName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(d => d.ProcessId)
            .ToList();
    }

    public void RequestRestartManagerShutdown(
        string path,
        bool scanRecursively,
        bool forceUnresponsive,
        int maxFilesToEnumerate = 5000)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path cannot be empty.", nameof(path));

        var normalizedPath = Path.GetFullPath(path);
        var resources = BuildResources(normalizedPath, scanRecursively, maxFilesToEnumerate);
        _restartManagerGateway.RequestShutdown(resources, forceUnresponsive);
    }

    private static IReadOnlyList<string> BuildResources(string normalizedPath, bool scanRecursively, int maxFilesToEnumerate)
    {
        if (File.Exists(normalizedPath))
            return new[] { normalizedPath };

        if (!Directory.Exists(normalizedPath))
            throw new FileNotFoundException("File or directory not found.", normalizedPath);

        if (!scanRecursively)
            return new[] { normalizedPath };

        var files = Directory.EnumerateFiles(normalizedPath, "*", SearchOption.AllDirectories)
            .Take(maxFilesToEnumerate + 1)
            .ToList();

        if (files.Count <= maxFilesToEnumerate)
            return files;

        return new[] { normalizedPath };
    }

    private ProcessLockDetail MapToLockDetail(LockingProcessSnapshot info)
    {
        var isCritical = info.ProcessType == LockingProcessType.Critical || _safetyPolicy.IsCriticalProcess(info.ProcessName);

        return new ProcessLockDetail
        {
            ProcessId = info.ProcessId,
            ProcessName = info.ProcessName,
            Description = MapDescription(info),
            LockType = MapLockType(info.ProcessType),
            CanTerminate = !isCritical,
            IsCriticalProcess = isCritical
        };
    }

    private static string MapDescription(LockingProcessSnapshot info)
    {
        return info.ProcessType switch
        {
            LockingProcessType.Service => string.IsNullOrWhiteSpace(info.ServiceName) ? "Service" : $"Service: {info.ServiceName}",
            LockingProcessType.Explorer => "Windows Explorer",
            LockingProcessType.Critical => "Critical System Process",
            _ => info.ProcessName
        };
    }

    private static ProcessLockType MapLockType(LockingProcessType type)
    {
        return type switch
        {
            LockingProcessType.Service => ProcessLockType.Service,
            LockingProcessType.Explorer => ProcessLockType.Explorer,
            LockingProcessType.Critical => ProcessLockType.SystemCritical,
            LockingProcessType.Application => ProcessLockType.Application,
            _ => ProcessLockType.Unknown
        };
    }
}
