using ClampDown.Cli;

namespace ClampDown.Tests;

public class CliTopLevelParserTests
{
    [Fact]
    public void Parse_NoArgs_ReturnsHelp()
    {
        var result = CliTopLevelParser.Parse([]);
        Assert.Equal(TopLevelCommand.Help, result.Command);
    }

    [Fact]
    public void Parse_UnknownCommand_ReturnsUnknown()
    {
        var result = CliTopLevelParser.Parse(["do-something", "x"]);
        Assert.Equal(TopLevelCommand.Unknown, result.Command);
        Assert.Equal("do-something", result.RawCommand);
    }

    [Fact]
    public void Parse_UnlockDelete_ParsesRemainingArguments()
    {
        var result = CliTopLevelParser.Parse(["unlock-delete", "C:\\temp\\a.txt", "--schedule", "--json"]);

        Assert.Equal(TopLevelCommand.UnlockDelete, result.Command);
        Assert.Equal(3, result.RemainingArgs.Length);
        Assert.Equal("C:\\temp\\a.txt", result.RemainingArgs[0]);
    }
}
