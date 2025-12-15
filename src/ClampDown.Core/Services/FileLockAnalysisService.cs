using ClampDown.Core.Models;
using ClampDown.Core.Policy;
using ClampDown.Win32;

namespace ClampDown.Core.Services;

public sealed class FileLockAnalysisService
{
    private readonly SafetyPolicy _safetyPolicy;

    public FileLockAnalysisService(SafetyPolicy safetyPolicy)
    {
        _safetyPolicy = safetyPolicy;
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

        using var client = new RestartManagerClient();
        client.StartSession();
        client.RegisterResources(resources);

        var lockers = client.GetLockers();
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

        using var client = new RestartManagerClient();
        client.StartSession();
        client.RegisterResources(resources);
        client.RequestShutdown(forceUnresponsive);
    }

    private IEnumerable<string> BuildResources(string normalizedPath, bool scanRecursively, int maxFilesToEnumerate)
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

    private ProcessLockDetail MapToLockDetail(ProcessLockInfo info)
    {
        var isCritical = info.ApplicationType == RmAppType.Critical || _safetyPolicy.IsCriticalProcess(info.ProcessName);

        return new ProcessLockDetail
        {
            ProcessId = info.ProcessId,
            ProcessName = info.ProcessName,
            Description = info.Description,
            LockType = MapLockType(info.ApplicationType),
            CanTerminate = !isCritical,
            IsCriticalProcess = isCritical
        };
    }

    private static ProcessLockType MapLockType(RmAppType type)
    {
        return type switch
        {
            RmAppType.Service => ProcessLockType.Service,
            RmAppType.Explorer => ProcessLockType.Explorer,
            RmAppType.Critical => ProcessLockType.SystemCritical,
            RmAppType.MainWindow or RmAppType.OtherWindow or RmAppType.Console => ProcessLockType.Application,
            _ => ProcessLockType.Unknown
        };
    }
}
