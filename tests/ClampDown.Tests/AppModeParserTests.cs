using ClampDown.App;

namespace ClampDown.Tests;

public class AppModeParserTests
{
    [Fact]
    public void Parse_NoArgs_ReturnsUiMode()
    {
        var result = AppModeParser.Parse([]);

        Assert.Equal(AppMode.Ui, result.Mode);
        Assert.Empty(result.ModeArgs);
    }

    [Fact]
    public void Parse_TrayFlag_ReturnsTrayMode()
    {
        var result = AppModeParser.Parse(["--tray"]);

        Assert.Equal(AppMode.Tray, result.Mode);
        Assert.Empty(result.ModeArgs);
    }

    [Fact]
    public void Parse_HelperFlag_ReturnsHelperModeWithRemainingArgs()
    {
        var result = AppModeParser.Parse(["--helper", "--pipe-name", "p", "--auth-token", "t", "--allow-client-pid", "42"]);

        Assert.Equal(AppMode.Helper, result.Mode);
        Assert.Equal("--pipe-name", result.ModeArgs[0]);
        Assert.Equal("42", result.ModeArgs[^1]);
    }

    [Fact]
    public void Parse_CliCommand_ReturnsCliMode()
    {
        var result = AppModeParser.Parse(["analyze", "C:\\temp\\a.txt"]);

        Assert.Equal(AppMode.Cli, result.Mode);
        Assert.Equal(2, result.ModeArgs.Length);
        Assert.Equal("analyze", result.ModeArgs[0]);
    }

    [Fact]
    public void Parse_UnknownCommand_ReturnsUsageError()
    {
        var result = AppModeParser.Parse(["do-something"]);

        Assert.Equal(AppMode.UsageError, result.Mode);
        Assert.Equal("do-something", result.UnknownCommand);
    }
}
