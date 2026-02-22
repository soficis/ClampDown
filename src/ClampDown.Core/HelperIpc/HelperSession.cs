using System.Security.Cryptography;

namespace ClampDown.Core.HelperIpc;

public sealed record HelperSession
{
    public required string PipeName { get; init; }
    public required string AuthorizationToken { get; init; }
    public required int AllowedClientProcessId { get; init; }
}

public static class HelperSessionFactory
{
    public static HelperSession CreateForCurrentProcess(string pipePrefix = "ClampDown.Helper")
    {
        if (string.IsNullOrWhiteSpace(pipePrefix))
            throw new ArgumentException("Pipe prefix cannot be empty.", nameof(pipePrefix));

        var processId = Environment.ProcessId;
        var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(12)).ToLowerInvariant();
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        return new HelperSession
        {
            PipeName = $"{pipePrefix}.{processId}.{nonce}",
            AuthorizationToken = token,
            AllowedClientProcessId = processId
        };
    }
}
