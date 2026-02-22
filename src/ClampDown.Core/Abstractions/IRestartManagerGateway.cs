namespace ClampDown.Core.Abstractions;

public interface IRestartManagerGateway
{
    IReadOnlyList<LockingProcessSnapshot> GetLockers(IReadOnlyList<string> resourcePaths);
    void RequestShutdown(IReadOnlyList<string> resourcePaths, bool forceUnresponsive);
}

public sealed record LockingProcessSnapshot
{
    public required int ProcessId { get; init; }
    public required string ProcessName { get; init; }
    public string ServiceName { get; init; } = string.Empty;
    public LockingProcessType ProcessType { get; init; } = LockingProcessType.Unknown;
}

public enum LockingProcessType
{
    Unknown = 0,
    Application = 1,
    Service = 2,
    Explorer = 3,
    Critical = 4
}
