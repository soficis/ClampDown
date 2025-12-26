using System.ComponentModel;
using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using ClampDown.Core.HelperIpc;
using ClampDown.Win32;

const string defaultPipeName = "ClampDown.Helper";
var pipeName = GetArgValue(args, "--pipe-name") ?? defaultPipeName;

await RunServerAsync(pipeName);

static string? GetArgValue(string[] args, string key)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
            return args[i + 1];
    }

    return null;
}

static async Task RunServerAsync(string pipeName)
{
    while (true)
    {
        using var server = new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            maxNumberOfServerInstances: 1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await server.WaitForConnectionAsync();

        try
        {
            using var reader = new StreamReader(server, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 4096, leaveOpen: true);
            using var writer = new StreamWriter(server, Encoding.UTF8, bufferSize: 4096, leaveOpen: true) { AutoFlush = true };

            var requestJson = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(requestJson))
                continue;

            var request = JsonSerializer.Deserialize<HelperRequest>(requestJson);
            if (request == null)
                continue;

            var response = await HandleAsync(request);
            var responseJson = JsonSerializer.Serialize(response);
            await writer.WriteLineAsync(responseJson);
        }
        catch
        {
            // The client may have disconnected. Continue serving.
        }
    }
}

static Task<HelperResponse> HandleAsync(HelperRequest request)
{
    return request.Command switch
    {
        HelperCommand.KillProcess => Task.FromResult(HandleKillProcess(request)),
        HelperCommand.RequestDeviceEject => Task.FromResult(HandleDeviceEject(request)),
        HelperCommand.ForceDismountVolume => Task.FromResult(HandleDismount(request)),
        HelperCommand.ScheduleDeleteOnReboot => Task.FromResult(HandleScheduleDelete(request)),
        HelperCommand.ScheduleMoveOnReboot => Task.FromResult(HandleScheduleMove(request)),
        _ => Task.FromResult(new HelperResponse
        {
            CorrelationId = request.CorrelationId,
            Success = false,
            ErrorMessage = $"Unknown command: {request.Command}"
        })
    };
}

static HelperResponse HandleScheduleDelete(HelperRequest request)
{
    if (!request.UserConfirmed)
    {
        return new HelperResponse
        {
            CorrelationId = request.CorrelationId,
            Success = false,
            ErrorMessage = "UserConfirmed was not set."
        };
    }

    if (string.IsNullOrWhiteSpace(request.SourcePath))
    {
        return new HelperResponse
        {
            CorrelationId = request.CorrelationId,
            Success = false,
            ErrorMessage = "SourcePath was not provided."
        };
    }

    try
    {
        FileOperations.ScheduleDeleteOnReboot(request.SourcePath);
        return new HelperResponse
        {
            CorrelationId = request.CorrelationId,
            Success = true,
            Details = "Delete scheduled for next reboot."
        };
    }
    catch (Win32Exception ex)
    {
        return new HelperResponse
        {
            CorrelationId = request.CorrelationId,
            Success = false,
            ErrorMessage = ex.Message,
            Win32ErrorCode = ex.NativeErrorCode
        };
    }
    catch (Exception ex)
    {
        return new HelperResponse
        {
            CorrelationId = request.CorrelationId,
            Success = false,
            ErrorMessage = ex.Message
        };
    }
}

static HelperResponse HandleScheduleMove(HelperRequest request)
{
    if (!request.UserConfirmed)
    {
        return new HelperResponse
        {
            CorrelationId = request.CorrelationId,
            Success = false,
            ErrorMessage = "UserConfirmed was not set."
        };
    }

    if (string.IsNullOrWhiteSpace(request.SourcePath))
    {
        return new HelperResponse
        {
            CorrelationId = request.CorrelationId,
            Success = false,
            ErrorMessage = "SourcePath was not provided."
        };
    }

    if (string.IsNullOrWhiteSpace(request.DestinationPath))
    {
        return new HelperResponse
        {
            CorrelationId = request.CorrelationId,
            Success = false,
            ErrorMessage = "DestinationPath was not provided."
        };
    }

    try
    {
        FileOperations.ScheduleMoveOnReboot(request.SourcePath, request.DestinationPath);
        return new HelperResponse
        {
            CorrelationId = request.CorrelationId,
            Success = true,
            Details = "Move scheduled for next reboot."
        };
    }
    catch (Win32Exception ex)
    {
        return new HelperResponse
        {
            CorrelationId = request.CorrelationId,
            Success = false,
            ErrorMessage = ex.Message,
            Win32ErrorCode = ex.NativeErrorCode
        };
    }
    catch (Exception ex)
    {
        return new HelperResponse
        {
            CorrelationId = request.CorrelationId,
            Success = false,
            ErrorMessage = ex.Message
        };
    }
}

static HelperResponse HandleKillProcess(HelperRequest request)
{
    if (!request.UserConfirmed)
    {
        return new HelperResponse
        {
            CorrelationId = request.CorrelationId,
            Success = false,
            ErrorMessage = "UserConfirmed was not set."
        };
    }

    if (request.ProcessId is null)
    {
        return new HelperResponse
        {
            CorrelationId = request.CorrelationId,
            Success = false,
            ErrorMessage = "ProcessId was not provided."
        };
    }

    try
    {
        var process = Process.GetProcessById(request.ProcessId.Value);
        var killTree = request.KillProcessTree ?? false;
        process.Kill(entireProcessTree: killTree);
        process.WaitForExit(5000);

        return new HelperResponse
        {
            CorrelationId = request.CorrelationId,
            Success = true,
            Details = $"Killed PID {request.ProcessId.Value} (tree={killTree})."
        };
    }
    catch (Win32Exception ex)
    {
        return new HelperResponse
        {
            CorrelationId = request.CorrelationId,
            Success = false,
            ErrorMessage = ex.Message,
            Win32ErrorCode = ex.NativeErrorCode
        };
    }
    catch (Exception ex)
    {
        return new HelperResponse
        {
            CorrelationId = request.CorrelationId,
            Success = false,
            ErrorMessage = ex.Message
        };
    }
}

static HelperResponse HandleDeviceEject(HelperRequest request)
{
    if (!request.UserConfirmed)
    {
        return new HelperResponse
        {
            CorrelationId = request.CorrelationId,
            Success = false,
            ErrorMessage = "UserConfirmed was not set."
        };
    }

    if (string.IsNullOrWhiteSpace(request.DeviceInstanceId))
    {
        return new HelperResponse
        {
            CorrelationId = request.CorrelationId,
            Success = false,
            ErrorMessage = "DeviceInstanceId was not provided."
        };
    }

    try
    {
        var result = DriveOperations.RequestDeviceEject(request.DeviceInstanceId);
        return new HelperResponse
        {
            CorrelationId = request.CorrelationId,
            Success = result.Success,
            ErrorMessage = result.ErrorMessage,
            Details = result.Success ? "Eject request succeeded." : $"Veto: {result.VetoType} ({result.VetoName})"
        };
    }
    catch (Win32Exception ex)
    {
        return new HelperResponse
        {
            CorrelationId = request.CorrelationId,
            Success = false,
            ErrorMessage = ex.Message,
            Win32ErrorCode = ex.NativeErrorCode
        };
    }
    catch (Exception ex)
    {
        return new HelperResponse
        {
            CorrelationId = request.CorrelationId,
            Success = false,
            ErrorMessage = ex.Message
        };
    }
}

static HelperResponse HandleDismount(HelperRequest request)
{
    if (!request.UserConfirmed)
    {
        return new HelperResponse
        {
            CorrelationId = request.CorrelationId,
            Success = false,
            ErrorMessage = "UserConfirmed was not set."
        };
    }

    if (string.IsNullOrWhiteSpace(request.DriveLetter))
    {
        return new HelperResponse
        {
            CorrelationId = request.CorrelationId,
            Success = false,
            ErrorMessage = "DriveLetter was not provided."
        };
    }

    try
    {
        var ok = DriveOperations.ForceDismountVolume(request.DriveLetter, out var errorMessage);
        return new HelperResponse
        {
            CorrelationId = request.CorrelationId,
            Success = ok,
            ErrorMessage = errorMessage,
            Details = ok ? "Dismount succeeded." : "Dismount failed."
        };
    }
    catch (Win32Exception ex)
    {
        return new HelperResponse
        {
            CorrelationId = request.CorrelationId,
            Success = false,
            ErrorMessage = ex.Message,
            Win32ErrorCode = ex.NativeErrorCode
        };
    }
    catch (Exception ex)
    {
        return new HelperResponse
        {
            CorrelationId = request.CorrelationId,
            Success = false,
            ErrorMessage = ex.Message
        };
    }
}
