namespace ClampDown.Core.HelperIpc;

public enum HelperCommand
{
    KillProcess = 1,
    RequestDeviceEject = 2,
    ForceDismountVolume = 3,
    ScheduleDeleteOnReboot = 4,
    ScheduleMoveOnReboot = 5
}

public sealed record HelperRequest
{
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
    public HelperCommand Command { get; init; }
    public bool UserConfirmed { get; init; }
    public string? ConfirmationToken { get; init; }

    public int? ProcessId { get; init; }
    public bool? KillProcessTree { get; init; }

    public string? SourcePath { get; init; }
    public string? DestinationPath { get; init; }

    public string? DeviceInstanceId { get; init; }
    public string? DriveLetter { get; init; }
}

public sealed record HelperResponse
{
    public Guid CorrelationId { get; init; }
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public int? Win32ErrorCode { get; init; }
    public string? Details { get; init; }
}
