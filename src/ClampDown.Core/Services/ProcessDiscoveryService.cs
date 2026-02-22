using System.Diagnostics;
using System.ComponentModel;
using ClampDown.Core.Policy;

namespace ClampDown.Core.Services;

public sealed class ProcessDiscoveryService
{
    private readonly SafetyPolicy _safetyPolicy;

    public ProcessDiscoveryService(SafetyPolicy safetyPolicy)
    {
        _safetyPolicy = safetyPolicy;
    }

    public IReadOnlyList<RunningProcessInfo> FindProcessesWithExecutableUnderPath(string pathPrefix)
    {
        if (string.IsNullOrWhiteSpace(pathPrefix))
            throw new ArgumentException("Path prefix cannot be empty.", nameof(pathPrefix));

        var normalizedPrefix = NormalizePrefix(pathPrefix);
        var results = new List<RunningProcessInfo>();

        foreach (var process in Process.GetProcesses())
        {
            string? executablePath = null;
            try
            {
                executablePath = process.MainModule?.FileName;
            }
            catch (Win32Exception)
            {
                continue;
            }
            catch (InvalidOperationException)
            {
                continue;
            }
            catch (NotSupportedException)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(executablePath))
                continue;

            if (!executablePath.StartsWith(normalizedPrefix, StringComparison.OrdinalIgnoreCase))
                continue;

            var approval = _safetyPolicy.CanTerminate(process.ProcessName, process.Id);

            results.Add(new RunningProcessInfo
            {
                ProcessId = process.Id,
                ProcessName = process.ProcessName,
                ExecutablePath = executablePath,
                CanTerminate = approval.IsAllowed,
                RequiresWarning = approval.RequiresWarning,
                WarningMessage = approval.Message
            });
        }

        return results
            .OrderBy(p => p.CanTerminate)
            .ThenBy(p => p.ProcessName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.ProcessId)
            .ToList();
    }

    private static string NormalizePrefix(string prefix)
    {
        var full = Path.GetFullPath(prefix);
        if (!full.EndsWith(Path.DirectorySeparatorChar))
            full += Path.DirectorySeparatorChar;
        return full;
    }
}

public sealed record RunningProcessInfo
{
    public required int ProcessId { get; init; }
    public required string ProcessName { get; init; }
    public required string ExecutablePath { get; init; }
    public required bool CanTerminate { get; init; }
    public required bool RequiresWarning { get; init; }
    public string? WarningMessage { get; init; }
}
