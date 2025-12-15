using System.Text.Json;
using System.Text.Json.Serialization;
using ClampDown.Core.Models;
using ClampDown.Core.Policy;
using ClampDown.Core.Services;
using ClampDown.Win32;

var exitCode = Run(args);
Environment.Exit(exitCode);

static int Run(string[] args)
{
    if (args.Length == 0 || IsHelp(args[0]))
    {
        PrintHelp();
        return 0;
    }

    var safetyPolicy = new SafetyPolicy();
    var actionLogger = new ActionLogger();
    var analysisService = new FileLockAnalysisService(safetyPolicy);
    var fileActionService = new FileActionService(actionLogger, analysisService);

    var command = args[0].ToLowerInvariant();
    var remaining = args.Skip(1).ToArray();

    try
    {
        return command switch
        {
            "analyze" => Analyze(analysisService, remaining),
            "unlock-delete" => UnlockDelete(fileActionService, remaining),
            "unlock-move" => UnlockMove(fileActionService, remaining),
            "unlock-copy" => UnlockCopy(fileActionService, remaining),
            "drive-list" => DriveList(remaining),
            "eject" => Eject(remaining),
            _ => Unknown(command)
        };
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine(ex.Message);
        return 1;
    }
}

static int Analyze(FileLockAnalysisService analysisService, string[] args)
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

static int UnlockDelete(FileActionService fileActionService, string[] args)
{
    if (args.Length == 0 || IsHelp(args[0]))
    {
        Console.WriteLine("Usage: clampdown unlock-delete <filePath> [--recycle-bin] [--schedule]");
        return 2;
    }

    var filePath = args[0];
    var recycle = args.Contains("--recycle-bin", StringComparer.OrdinalIgnoreCase) || !args.Contains("--permanent", StringComparer.OrdinalIgnoreCase);
    var schedule = args.Contains("--schedule", StringComparer.OrdinalIgnoreCase);

    var result = fileActionService.TryDelete(filePath, recycle, schedule);
    return PrintFileResult(result, json: args.Contains("--json", StringComparer.OrdinalIgnoreCase));
}

static int UnlockMove(FileActionService fileActionService, string[] args)
{
    if (args.Length < 2 || IsHelp(args[0]))
    {
        Console.WriteLine("Usage: clampdown unlock-move <sourcePath> <destinationPath> [--schedule] [--json]");
        return 2;
    }

    var source = args[0];
    var dest = args[1];
    var schedule = args.Contains("--schedule", StringComparer.OrdinalIgnoreCase);

    var result = fileActionService.TryMove(source, dest, schedule);
    return PrintFileResult(result, json: args.Contains("--json", StringComparer.OrdinalIgnoreCase));
}

static int UnlockCopy(FileActionService fileActionService, string[] args)
{
    if (args.Length < 2 || IsHelp(args[0]))
    {
        Console.WriteLine("Usage: clampdown unlock-copy <sourcePath> <destinationPath> [--json]");
        return 2;
    }

    var source = args[0];
    var dest = args[1];

    var result = fileActionService.TryCopy(source, dest);
    return PrintFileResult(result, json: args.Contains("--json", StringComparer.OrdinalIgnoreCase));
}

static int DriveList(string[] args)
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

static int Eject(string[] args)
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

static int PrintFileResult(FileOperationResult result, bool json)
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

static void WriteJson<T>(T obj)
{
    var options = new JsonSerializerOptions
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };
    Console.WriteLine(JsonSerializer.Serialize(obj, options));
}

static string NormalizeDriveLetter(string input)
{
    var trimmed = input.Trim();
    if (trimmed.EndsWith("\\", StringComparison.Ordinal))
        trimmed = trimmed.TrimEnd('\\');

    if (!trimmed.EndsWith(":", StringComparison.Ordinal))
        trimmed += ":";

    return trimmed;
}

static bool IsHelp(string arg)
{
    return arg is "-h" or "--help" or "/?" or "help";
}

static int Unknown(string command)
{
    Console.Error.WriteLine($"Unknown command: {command}");
    PrintHelp();
    return 2;
}

static void PrintHelp()
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
