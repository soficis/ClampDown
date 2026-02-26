namespace ClampDown.App;

public enum AppMode
{
    Ui = 0,
    Tray = 1,
    Cli = 2,
    Helper = 3,
    UsageError = 4
}

public sealed record AppModeParseResult
{
    public required AppMode Mode { get; init; }
    public string[] ModeArgs { get; init; } = [];
    public string UnknownCommand { get; init; } = string.Empty;
}

public static class AppModeParser
{
    private static readonly HashSet<string> CliCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "-h",
        "--help",
        "/?",
        "help",
        "analyze",
        "unlock-delete",
        "unlock-move",
        "unlock-copy",
        "drive-list",
        "eject"
    };

    public static AppModeParseResult Parse(string[] args)
    {
        if (args.Length == 0)
            return new AppModeParseResult { Mode = AppMode.Ui };

        if (string.Equals(args[0], "--helper", StringComparison.OrdinalIgnoreCase))
        {
            return new AppModeParseResult
            {
                Mode = AppMode.Helper,
                ModeArgs = args.Skip(1).ToArray()
            };
        }

        if (string.Equals(args[0], "--tray", StringComparison.OrdinalIgnoreCase))
        {
            return new AppModeParseResult
            {
                Mode = AppMode.Tray,
                ModeArgs = args.Skip(1).ToArray()
            };
        }

        if (CliCommands.Contains(args[0]))
        {
            return new AppModeParseResult
            {
                Mode = AppMode.Cli,
                ModeArgs = args
            };
        }

        return new AppModeParseResult
        {
            Mode = AppMode.UsageError,
            UnknownCommand = args[0],
            ModeArgs = args
        };
    }
}
