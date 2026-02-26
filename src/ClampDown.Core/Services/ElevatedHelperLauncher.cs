using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using ClampDown.Core.Abstractions;
using ClampDown.Core.HelperIpc;

namespace ClampDown.Core.Services;

public sealed class ElevatedHelperLauncher : IElevatedHelperLauncher
{
    private readonly HelperSession _helperSession;
    private readonly Func<string?> _currentExecutablePathResolver;
    private readonly Func<ProcessStartInfo, Process?> _processStarter;

    public ElevatedHelperLauncher(HelperSession helperSession)
        : this(helperSession, ResolveCurrentExecutablePath, static startInfo => Process.Start(startInfo))
    {
    }

    internal ElevatedHelperLauncher(
        HelperSession helperSession,
        Func<string?> currentExecutablePathResolver,
        Func<ProcessStartInfo, Process?> processStarter)
    {
        _helperSession = helperSession;
        _currentExecutablePathResolver = currentExecutablePathResolver;
        _processStarter = processStarter;
    }

    public bool TryStart(out string? errorMessage)
    {
        if (!TryBuildStartInfo(out var startInfo, out errorMessage))
            return false;

        try
        {
            _processStarter(startInfo);
            errorMessage = null;
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

    internal bool TryBuildStartInfo(out ProcessStartInfo startInfo, out string? errorMessage)
    {
        var currentExecutablePath = _currentExecutablePathResolver();
        if (string.IsNullOrWhiteSpace(currentExecutablePath))
        {
            startInfo = new ProcessStartInfo();
            errorMessage = "Unable to resolve ClampDown executable path.";
            return false;
        }

        startInfo = new ProcessStartInfo
        {
            FileName = currentExecutablePath,
            UseShellExecute = true,
            Verb = "runas"
        };

        startInfo.ArgumentList.Add("--helper");
        startInfo.ArgumentList.Add("--pipe-name");
        startInfo.ArgumentList.Add(_helperSession.PipeName);
        startInfo.ArgumentList.Add("--auth-token");
        startInfo.ArgumentList.Add(_helperSession.AuthorizationToken);
        startInfo.ArgumentList.Add("--allow-client-pid");
        startInfo.ArgumentList.Add(_helperSession.AllowedClientProcessId.ToString(CultureInfo.InvariantCulture));

        errorMessage = null;
        return true;
    }

    private static string? ResolveCurrentExecutablePath()
    {
        return Process.GetCurrentProcess().MainModule?.FileName;
    }
}
