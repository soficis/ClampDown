using System.Text.Json;
using System.Text.Json.Serialization;
using ClampDown.Cli;
using ClampDown.Core.HelperIpc;
using ClampDown.Core.Models;
using ClampDown.Core.Policy;
using ClampDown.Core.Services;
using ClampDown.Win32;
using ClampDown.Win32.Adapters;

namespace ClampDown.UI;

internal static class CliRunner
{
    public static int Run(string[] args)
    {
        var parsed = CliTopLevelParser.Parse(args);
        if (parsed.Command == TopLevelCommand.Help)
        {
            PrintHelp();
            return 0;
        }

        var safetyPolicy = new SafetyPolicy();
        var actionLogger = new ActionLogger();
        var helperSession = HelperSessionFactory.CreateForCurrentProcess();
        var filePlatformOperations = new Win32FilePlatformOperations();
        var restartManagerGateway = new RestartManagerGateway();
        var helperClient = new ElevatedHelperClient(helperSession);
        var helperLauncher = new ElevatedHelperLauncher(helperSession);
        var rebootScheduleService = new RebootScheduleService(filePlatformOperations, helperClient, helperLauncher);
        var analysisService = new FileLockAnalysisService(safetyPolicy, restartManagerGateway);
        var fileActionService = new FileActionService(actionLogger, analysisService, rebootScheduleService, filePlatformOperations);

        var remaining = parsed.RemainingArgs;

        try
        {
            return parsed.Command switch
            {
                TopLevelCommand.Analyze => Analyze(analysisService, remaining),
                TopLevelCommand.UnlockDelete => UnlockDelete(fileActionService, remaining),
                TopLevelCommand.UnlockMove => UnlockMove(fileActionService, remaining),
                TopLevelCommand.UnlockCopy => UnlockCopy(fileActionService, remaining),
                TopLevelCommand.DriveList => DriveList(remaining),
                TopLevelCommand.Eject => Eject(remaining),
                _ => PrintUnknownUsageError(parsed.RawCommand)
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    public static int PrintUnknownUsageError(string command)
    {
        Console.Error.WriteLine($"Unknown command: {command}");
        PrintHelp();
        return 2;
    }

    public static void PrintHelp()
    {
        Console.WriteLine("ClampDown CLI");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  analyze <path> [--recursive] [--json]");
        Console.WriteLine("  unlock-delete <filePath> [--recycle-bin] [--permanent] [--schedule] [--json]");
        Console.WriteLine("  unlock-move <sourcePath> <destinationPath> [--schedule] [--json]");
        Console.WriteLine("  unlock-copy <sourcePath> <destinationPath> [--json]");
        Console.WriteLine("  drive-list [--json]");
        Console.WriteLine("  eject <driveLetter> [--json]");
    }

    private static int Analyze(FileLockAnalysisService analysisService, string[] args)
    {
        if (args.Length == 0 || IsHelp(args[0]))
        {
            Console.WriteLine("Usage: clampdown analyze <path> [--recursive] [--json]");
            return 2;
        }

        var path = args[0];
        var recursive = args.Contains("--recursive", StringComparer.OrdinalIgnoreCase);
        var json = args.Contains("--json", StringComparer.OrdinalIgnoreCase);

        var lockers = analysisService.AnalyzePath(path, recursive);

        if (json)
        {
            WriteJson(lockers);
            return 0;
        }

        Console.WriteLine(path);
        Console.WriteLine();
        if (lockers.Count == 0)
        {
            Console.WriteLine("No lockers detected.");
            return 0;
        }

        foreach (var locker in lockers)
            Console.WriteLine($"{locker.ProcessName}\t{locker.ProcessId}\t{locker.Description}");
        return 0;
    }

    private static int UnlockDelete(FileActionService fileActionService, string[] args)
    {
        if (args.Length == 0 || IsHelp(args[0]))
        {
            Console.WriteLine("Usage: clampdown unlock-delete <filePath> [--recycle-bin] [--schedule]");
            return 2;
        }

        var filePath = args[0];
        var recycle = args.Contains("--recycle-bin", StringComparer.OrdinalIgnoreCase) || !args.Contains("--permanent", StringComparer.OrdinalIgnoreCase);
        var schedule = args.Contains("--schedule", StringComparer.OrdinalIgnoreCase);

        var result = fileActionService.Delete(new DeleteFileRequest
        {
            FilePath = filePath,
            DeleteMode = recycle ? DeleteMode.RecycleBin : DeleteMode.Permanent,
            OnBlocked = schedule ? OnBlockedBehavior.ScheduleOnReboot : OnBlockedBehavior.Fail
        });

        return PrintFileResult(result, json: args.Contains("--json", StringComparer.OrdinalIgnoreCase));
    }

    private static int UnlockMove(FileActionService fileActionService, string[] args)
    {
        if (args.Length < 2 || IsHelp(args[0]))
        {
            Console.WriteLine("Usage: clampdown unlock-move <sourcePath> <destinationPath> [--schedule] [--json]");
            return 2;
        }

        var source = args[0];
        var dest = args[1];
        var schedule = args.Contains("--schedule", StringComparer.OrdinalIgnoreCase);

        var result = fileActionService.Move(new MoveFileRequest
        {
            SourcePath = source,
            DestinationPath = dest,
            OnBlocked = schedule ? OnBlockedBehavior.ScheduleOnReboot : OnBlockedBehavior.Fail
        });

        return PrintFileResult(result, json: args.Contains("--json", StringComparer.OrdinalIgnoreCase));
    }

    private static int UnlockCopy(FileActionService fileActionService, string[] args)
    {
        if (args.Length < 2 || IsHelp(args[0]))
        {
            Console.WriteLine("Usage: clampdown unlock-copy <sourcePath> <destinationPath> [--json]");
            return 2;
        }

        var source = args[0];
        var dest = args[1];

        var result = fileActionService.Copy(new CopyFileRequest
        {
            SourcePath = source,
            DestinationPath = dest
        });

        return PrintFileResult(result, json: args.Contains("--json", StringComparer.OrdinalIgnoreCase));
    }

    private static int DriveList(string[] args)
    {
        var json = args.Contains("--json", StringComparer.OrdinalIgnoreCase);
        var drives = DriveDiscovery.GetRemovableDrives();

        if (json)
        {
            WriteJson(drives);
            return 0;
        }

        foreach (var d in drives)
            Console.WriteLine($"{d.DriveLetter}\t{d.VolumeLabel}\t{d.FileSystem}\t{d.Model}");
        return 0;
    }

    private static int Eject(string[] args)
    {
        if (args.Length == 0 || IsHelp(args[0]))
        {
            Console.WriteLine("Usage: clampdown eject <driveLetter> [--json]");
            return 2;
        }

        var driveLetter = NormalizeDriveLetter(args[0]);
        var json = args.Contains("--json", StringComparer.OrdinalIgnoreCase);

        var drive = DriveDiscovery.GetRemovableDrives()
            .FirstOrDefault(d => string.Equals(d.DriveLetter, driveLetter, StringComparison.OrdinalIgnoreCase));

        if (drive == null)
        {
            Console.Error.WriteLine($"Drive not found: {driveLetter}");
            return 1;
        }

        if (string.IsNullOrWhiteSpace(drive.DeviceInstanceId))
        {
            Console.Error.WriteLine($"Drive has no device mapping: {driveLetter}");
            return 1;
        }

        var result = DriveOperations.RequestDeviceEject(drive.DeviceInstanceId);
        if (json)
        {
            WriteJson(result);
            return result.Success ? 0 : 1;
        }

        Console.WriteLine(result.Success ? "Eject request succeeded." : (result.ErrorMessage ?? "Eject failed."));
        return result.Success ? 0 : 1;
    }

    private static int PrintFileResult(FileOperationResult result, bool json)
    {
        if (json)
        {
            WriteJson(result);
            return result.Success ? 0 : 1;
        }

        Console.WriteLine($"{result.Status}: {result.FilePath}");
        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
            Console.WriteLine(result.ErrorMessage);

        if (result.Lockers is { Count: > 0 })
        {
            Console.WriteLine();
            foreach (var locker in result.Lockers)
                Console.WriteLine($"{locker.ProcessName}\t{locker.ProcessId}\t{locker.Description}");
        }

        return result.Success ? 0 : 1;
    }

    private static void WriteJson<T>(T obj)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };

        Console.WriteLine(JsonSerializer.Serialize(obj, options));
    }

    private static string NormalizeDriveLetter(string input)
    {
        var trimmed = input.Trim();
        if (trimmed.EndsWith("\\", StringComparison.Ordinal))
            trimmed = trimmed.TrimEnd('\\');

        if (!trimmed.EndsWith(":", StringComparison.Ordinal))
            trimmed += ":";

        return trimmed;
    }

    private static bool IsHelp(string arg)
    {
        return arg is "-h" or "--help" or "/?" or "help";
    }
}
