using System.ComponentModel;
using ClampDown.Core.HelperIpc;
using ClampDown.Core.Services;

namespace ClampDown.Tests;

public class ElevatedHelperLauncherTests
{
    [Fact]
    public void TryBuildStartInfo_BuildsUnifiedExecutableHelperArguments()
    {
        var session = new HelperSession
        {
            PipeName = "pipe.123",
            AuthorizationToken = "token-value",
            AllowedClientProcessId = 321
        };

        var launcher = new ElevatedHelperLauncher(
            session,
            currentExecutablePathResolver: () => @"C:\Tools\ClampDown\ClampDown.exe",
            processStarter: _ => null);

        var ok = launcher.TryBuildStartInfo(out var startInfo, out var errorMessage);

        Assert.True(ok);
        Assert.Null(errorMessage);
        Assert.Equal(@"C:\Tools\ClampDown\ClampDown.exe", startInfo.FileName);
        Assert.True(startInfo.UseShellExecute);
        Assert.Equal("runas", startInfo.Verb);

        Assert.Collection(
            startInfo.ArgumentList,
            arg => Assert.Equal("--helper", arg),
            arg => Assert.Equal("--pipe-name", arg),
            arg => Assert.Equal("pipe.123", arg),
            arg => Assert.Equal("--auth-token", arg),
            arg => Assert.Equal("token-value", arg),
            arg => Assert.Equal("--allow-client-pid", arg),
            arg => Assert.Equal("321", arg));
    }

    [Fact]
    public void TryStart_MissingExecutablePath_ReturnsFailure()
    {
        var session = new HelperSession
        {
            PipeName = "pipe.123",
            AuthorizationToken = "token-value",
            AllowedClientProcessId = 321
        };

        var launcher = new ElevatedHelperLauncher(
            session,
            currentExecutablePathResolver: () => null,
            processStarter: _ => null);

        var ok = launcher.TryStart(out var errorMessage);

        Assert.False(ok);
        Assert.Contains("Unable to resolve", errorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryStart_UacCancelled_ReturnsFailureMessage()
    {
        var session = new HelperSession
        {
            PipeName = "pipe.123",
            AuthorizationToken = "token-value",
            AllowedClientProcessId = 321
        };

        var launcher = new ElevatedHelperLauncher(
            session,
            currentExecutablePathResolver: () => @"C:\Tools\ClampDown\ClampDown.exe",
            processStarter: _ => throw new Win32Exception(1223, "The operation was canceled by the user."));

        var ok = launcher.TryStart(out var errorMessage);

        Assert.False(ok);
        Assert.Contains("cancelled", errorMessage, StringComparison.OrdinalIgnoreCase);
    }
}
