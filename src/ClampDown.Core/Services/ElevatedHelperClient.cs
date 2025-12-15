using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using ClampDown.Core.HelperIpc;

namespace ClampDown.Core.Services;

public sealed class ElevatedHelperClient
{
    private readonly string _pipeName;

    public ElevatedHelperClient(string pipeName = "ClampDown.Helper")
    {
        _pipeName = pipeName;
    }

    public async Task<HelperResponse> SendAsync(HelperRequest request, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        using var client = new NamedPipeClientStream(
            serverName: ".",
            pipeName: _pipeName,
            direction: PipeDirection.InOut,
            options: PipeOptions.Asynchronous);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        await client.ConnectAsync(timeoutCts.Token);

        using var reader = new StreamReader(client, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 4096, leaveOpen: true);
        using var writer = new StreamWriter(client, Encoding.UTF8, bufferSize: 4096, leaveOpen: true) { AutoFlush = true };

        var json = JsonSerializer.Serialize(request);
        await writer.WriteLineAsync(json);

        var responseJson = await reader.ReadLineAsync(timeoutCts.Token);
        if (string.IsNullOrWhiteSpace(responseJson))
        {
            return new HelperResponse
            {
                CorrelationId = request.CorrelationId,
                Success = false,
                ErrorMessage = "No response from helper."
            };
        }

        var response = JsonSerializer.Deserialize<HelperResponse>(responseJson);
        return response ?? new HelperResponse
        {
            CorrelationId = request.CorrelationId,
            Success = false,
            ErrorMessage = "Invalid response from helper."
        };
    }
}

