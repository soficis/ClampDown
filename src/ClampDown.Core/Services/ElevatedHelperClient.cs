using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using ClampDown.Core.Abstractions;
using ClampDown.Core.HelperIpc;

namespace ClampDown.Core.Services;

public sealed class ElevatedHelperClient : IHelperCommandClient
{
    private readonly HelperSession _session;

    public ElevatedHelperClient(HelperSession session)
    {
        _session = session;
    }

    public async Task<HelperResponse> SendAsync(HelperRequest request, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_session.PipeName))
            throw new InvalidOperationException("Helper pipe name is not configured.");
        if (string.IsNullOrWhiteSpace(_session.AuthorizationToken))
            throw new InvalidOperationException("Helper authorization token is not configured.");

        using var client = new NamedPipeClientStream(
            serverName: ".",
            pipeName: _session.PipeName,
            direction: PipeDirection.InOut,
            options: PipeOptions.Asynchronous);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        await client.ConnectAsync(timeoutCts.Token);

        using var reader = new StreamReader(client, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 4096, leaveOpen: true);
        using var writer = new StreamWriter(client, Encoding.UTF8, bufferSize: 4096, leaveOpen: true) { AutoFlush = true };

        var authorizedRequest = request with { AuthorizationToken = _session.AuthorizationToken };
        var json = JsonSerializer.Serialize(authorizedRequest);
        await writer.WriteLineAsync(json);

        var responseJson = await reader.ReadLineAsync(timeoutCts.Token);
        if (string.IsNullOrWhiteSpace(responseJson))
        {
            return new HelperResponse
            {
                CorrelationId = authorizedRequest.CorrelationId,
                Success = false,
                ErrorMessage = "No response from helper."
            };
        }

        var response = JsonSerializer.Deserialize<HelperResponse>(responseJson);
        return response ?? new HelperResponse
        {
            CorrelationId = authorizedRequest.CorrelationId,
            Success = false,
            ErrorMessage = "Invalid response from helper."
        };
    }
}
