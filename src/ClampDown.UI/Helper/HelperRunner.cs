using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ClampDown.Core.HelperIpc;
using ClampDown.Win32;
using Microsoft.Win32.SafeHandles;

namespace ClampDown.UI;

internal static class HelperRunner
{
    private const string DefaultPipeName = "ClampDown";

    public static async Task<int> RunAsync(string[] args)
    {
        var pipeName = GetArgValue(args, "--pipe-name") ?? DefaultPipeName;
        var authToken = GetArgValue(args, "--auth-token");
        var allowClientPidText = GetArgValue(args, "--allow-client-pid");

        if (string.IsNullOrWhiteSpace(authToken))
        {
            Console.Error.WriteLine("Missing required argument: --auth-token <token>");
            return 2;
        }

        if (!int.TryParse(allowClientPidText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var allowedClientPid) || allowedClientPid <= 0)
        {
            Console.Error.WriteLine("Missing or invalid required argument: --allow-client-pid <pid>");
            return 2;
        }

        await RunServerAsync(pipeName, authToken, allowedClientPid);
        return 0;
    }

    private static string? GetArgValue(string[] args, string key)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }

        return null;
    }

    private static async Task RunServerAsync(string pipeName, string expectedAuthToken, int allowedClientPid)
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

            using var reader = new StreamReader(server, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 4096, leaveOpen: true);
            using var writer = new StreamWriter(server, Encoding.UTF8, bufferSize: 4096, leaveOpen: true) { AutoFlush = true };

            try
            {
                var requestJson = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(requestJson))
                    continue;

                var request = JsonSerializer.Deserialize<HelperRequest>(requestJson);
                if (request == null)
                {
                    await writer.WriteLineAsync(JsonSerializer.Serialize(new HelperResponse
                    {
                        Success = false,
                        ErrorMessage = "Request payload is invalid JSON."
                    }));

                    continue;
                }

                var authError = ValidateAuthorization(server, request, expectedAuthToken, allowedClientPid);
                if (!string.IsNullOrWhiteSpace(authError))
                {
                    await writer.WriteLineAsync(JsonSerializer.Serialize(new HelperResponse
                    {
                        CorrelationId = request.CorrelationId,
                        Success = false,
                        ErrorMessage = authError
                    }));

                    continue;
                }

                var response = await HandleAsync(request);
                var responseJson = JsonSerializer.Serialize(response);
                await writer.WriteLineAsync(responseJson);
            }
            catch (IOException)
            {
                // Client disconnected mid-request.
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
            }
        }
    }

    private static string? ValidateAuthorization(
        NamedPipeServerStream server,
        HelperRequest request,
        string expectedAuthToken,
        int allowedClientPid)
    {
        if (!TokensMatch(request.AuthorizationToken, expectedAuthToken))
            return "Unauthorized request token.";

        var gotClientPid = GetNamedPipeClientProcessId(server.SafePipeHandle, out var clientPid);
        if (!gotClientPid)
            return "Unable to validate caller process identity.";

        if (clientPid != allowedClientPid)
            return $"Unauthorized caller PID {clientPid}.";

        return null;
    }

    private static bool TokensMatch(string providedToken, string expectedToken)
    {
        if (string.IsNullOrWhiteSpace(providedToken) || string.IsNullOrWhiteSpace(expectedToken))
            return false;

        var provided = Encoding.UTF8.GetBytes(providedToken);
        var expected = Encoding.UTF8.GetBytes(expectedToken);

        return CryptographicOperations.FixedTimeEquals(provided, expected);
    }

    private static Task<HelperResponse> HandleAsync(HelperRequest request)
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

    private static HelperResponse HandleScheduleDelete(HelperRequest request)
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

    private static HelperResponse HandleScheduleMove(HelperRequest request)
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

    private static HelperResponse HandleKillProcess(HelperRequest request)
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

    private static HelperResponse HandleDeviceEject(HelperRequest request)
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

    private static HelperResponse HandleDismount(HelperRequest request)
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

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeClientProcessId(SafePipeHandle pipe, out uint clientProcessId);
}
