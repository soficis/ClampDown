using ClampDown.App;
using System.Windows.Forms;

namespace ClampDown.UI;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var mode = AppModeParser.Parse(args);

        return mode.Mode switch
        {
            AppMode.Helper => RunHelper(mode.ModeArgs),
            AppMode.Tray => TrayRunner.Run(),
            AppMode.Cli => RunCli(mode.ModeArgs),
            AppMode.Ui => RunUi(),
            AppMode.UsageError => RunUsageError(mode.UnknownCommand),
            _ => RunUsageError(mode.UnknownCommand)
        };
    }

    private static int RunUi()
    {
        ApplicationConfiguration.Initialize();
        var services = new UiServices();
        using var mainForm = new MainForm(services);
        Application.Run(mainForm);
        return 0;
    }

    private static int RunCli(string[] args)
    {
        ConsoleBinding.EnsureConsole();
        return CliRunner.Run(args);
    }

    private static int RunHelper(string[] args)
    {
        return HelperRunner.RunAsync(args).GetAwaiter().GetResult();
    }

    private static int RunUsageError(string unknownCommand)
    {
        ConsoleBinding.EnsureConsole();
        return CliRunner.PrintUnknownUsageError(unknownCommand);
    }
}
