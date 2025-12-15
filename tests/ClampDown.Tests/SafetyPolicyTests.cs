using ClampDown.Core.Policy;
using Xunit;

namespace ClampDown.Tests;

/// <summary>
/// Unit tests for SafetyPolicy - ensures critical processes are protected.
/// </summary>
public class SafetyPolicyTests
{
    private readonly SafetyPolicy _policy = new();

    [Theory]
    [InlineData("csrss.exe")]
    [InlineData("lsass.exe")]
    [InlineData("services.exe")]
    [InlineData("smss.exe")]
    [InlineData("wininit.exe")]
    [InlineData("System")]
    public void CanTerminate_CriticalProcess_ReturnsBlocked(string processName)
    {
        // Act
        var result = _policy.CanTerminate(processName, 100);

        // Assert
        Assert.False(result.IsAllowed);
        Assert.NotNull(result.Message);
        Assert.Contains("critical", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("notepad.exe")]
    [InlineData("chrome.exe")]
    [InlineData("code.exe")]
    [InlineData("MyApp")]
    public void CanTerminate_RegularProcess_ReturnsAllowed(string processName)
    {
        // Act
        var result = _policy.CanTerminate(processName, 1234);

        // Assert
        Assert.True(result.IsAllowed);
        Assert.False(result.RequiresWarning);
    }

    [Fact]
    public void CanTerminate_Explorer_ReturnsAllowedWithWarning()
    {
        // Act
        var result = _policy.CanTerminate("explorer.exe", 5678);

        // Assert
        Assert.True(result.IsAllowed);
        Assert.True(result.RequiresWarning);
        Assert.NotNull(result.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public void CanTerminate_SystemPid_ReturnsBlocked(int pid)
    {
        // Act
        var result = _policy.CanTerminate("SomeProcess", pid);

        // Assert
        Assert.False(result.IsAllowed);
    }

    [Theory]
    [InlineData("csrss.exe", true)]
    [InlineData("csrss", true)]
    [InlineData("notepad.exe", false)]
    [InlineData("Chrome", false)]
    public void IsCriticalProcess_ReturnsCorrectValue(string processName, bool expected)
    {
        // Act
        var result = _policy.IsCriticalProcess(processName);

        // Assert
        Assert.Equal(expected, result);
    }
}
