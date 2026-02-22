namespace ClampDown.Cli;

internal enum TopLevelCommand
{
    Help = 0,
    Analyze = 1,
    UnlockDelete = 2,
    UnlockMove = 3,
    UnlockCopy = 4,
    DriveList = 5,
    Eject = 6,
    Unknown = 7
}

internal sealed record TopLevelParseResult
{
    public required TopLevelCommand Command { get; init; }
    public string RawCommand { get; init; } = string.Empty;
    public string[] RemainingArgs { get; init; } = [];
}

internal static class CliTopLevelParser
{
    public static TopLevelParseResult Parse(string[] args)
    {
        if (args.Length == 0 || IsHelp(args[0]))
            return new TopLevelParseResult { Command = TopLevelCommand.Help };

        var command = args[0].ToLowerInvariant();
        var remaining = args.Skip(1).ToArray();

        return command switch
        {
            "analyze" => new TopLevelParseResult { Command = TopLevelCommand.Analyze, RemainingArgs = remaining, RawCommand = command },
            "unlock-delete" => new TopLevelParseResult { Command = TopLevelCommand.UnlockDelete, RemainingArgs = remaining, RawCommand = command },
            "unlock-move" => new TopLevelParseResult { Command = TopLevelCommand.UnlockMove, RemainingArgs = remaining, RawCommand = command },
            "unlock-copy" => new TopLevelParseResult { Command = TopLevelCommand.UnlockCopy, RemainingArgs = remaining, RawCommand = command },
            "drive-list" => new TopLevelParseResult { Command = TopLevelCommand.DriveList, RemainingArgs = remaining, RawCommand = command },
            "eject" => new TopLevelParseResult { Command = TopLevelCommand.Eject, RemainingArgs = remaining, RawCommand = command },
            _ => new TopLevelParseResult { Command = TopLevelCommand.Unknown, RawCommand = command, RemainingArgs = remaining }
        };
    }

    private static bool IsHelp(string arg)
    {
        return arg is "-h" or "--help" or "/?" or "help";
    }
}
