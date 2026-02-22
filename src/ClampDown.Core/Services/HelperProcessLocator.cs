using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using ClampDown.Core.HelperIpc;

namespace ClampDown.Core.Services;

public static class HelperProcessLocator
{
    public static string? FindHelperExecutablePath()
    {
        var baseDir = AppContext.BaseDirectory;

        var direct = Path.Combine(baseDir, "ClampDown.Helper.exe");
        if (File.Exists(direct))
            return direct;

        var slnRoot = FindSolutionRoot(baseDir);
        if (slnRoot == null)
            return null;

        var (configuration, targetFramework, platform) = TryInferBuildLayout(baseDir);

        if (!string.IsNullOrWhiteSpace(platform))
        {
            var platformCandidate = Path.Combine(
                slnRoot,
                "src",
                "ClampDown.Helper",
                "bin",
                platform,
                configuration,
                targetFramework,
                "ClampDown.Helper.exe");

            if (File.Exists(platformCandidate))
                return platformCandidate;
        }

        var candidate = Path.Combine(
            slnRoot,
            "src",
            "ClampDown.Helper",
            "bin",
            configuration,
            targetFramework,
            "ClampDown.Helper.exe");

        if (File.Exists(candidate))
            return candidate;

        var helperBin = Path.Combine(slnRoot, "src", "ClampDown.Helper", "bin");
        if (!Directory.Exists(helperBin))
            return null;

        return Directory.EnumerateFiles(helperBin, "ClampDown.Helper.exe", SearchOption.AllDirectories)
            .OrderByDescending(p => p.Contains($"\\{configuration}\\", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(p => p.Contains($"\\{targetFramework}\\", StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault();
    }

    public static bool TryStartElevatedHelper(string helperExePath, HelperSession helperSession, out string? errorMessage)
    {
        errorMessage = null;

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = helperExePath,
                UseShellExecute = true,
                Verb = "runas"
            };

            startInfo.ArgumentList.Add("--pipe-name");
            startInfo.ArgumentList.Add(helperSession.PipeName);
            startInfo.ArgumentList.Add("--auth-token");
            startInfo.ArgumentList.Add(helperSession.AuthorizationToken);
            startInfo.ArgumentList.Add("--allow-client-pid");
            startInfo.ArgumentList.Add(helperSession.AllowedClientProcessId.ToString(CultureInfo.InvariantCulture));

            Process.Start(startInfo);

            return true;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            errorMessage = "UAC prompt was cancelled.";
            return false;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    private static string? FindSolutionRoot(string startDir)
    {
        var dir = new DirectoryInfo(startDir);
        for (var i = 0; i < 12 && dir != null; i++)
        {
            var sln = Path.Combine(dir.FullName, "ClampDown.sln");
            if (File.Exists(sln))
                return dir.FullName;

            dir = dir.Parent;
        }

        return null;
    }

    private static (string Configuration, string TargetFramework, string? Platform) TryInferBuildLayout(string baseDir)
    {
        var tfmDir = new DirectoryInfo(baseDir);
        var targetFramework = tfmDir.Name;

        var configDir = tfmDir.Parent;
        var configuration = configDir?.Name ?? "Debug";

        string? platform = null;
        var platformDir = configDir?.Parent;
        if (platformDir?.Name is "x86" or "x64" or "arm64")
        {
            var parent = platformDir.Parent;
            if (parent != null && string.Equals(parent.Name, "bin", StringComparison.OrdinalIgnoreCase))
                platform = platformDir.Name;
        }

        return (configuration, targetFramework, platform);
    }
}
