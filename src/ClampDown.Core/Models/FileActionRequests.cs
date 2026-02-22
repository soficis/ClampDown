namespace ClampDown.Core.Models;

public enum DeleteMode
{
    RecycleBin = 0,
    Permanent = 1
}

public enum OnBlockedBehavior
{
    Fail = 0,
    ScheduleOnReboot = 1
}

public sealed record DeleteFileRequest
{
    public required string FilePath { get; init; }
    public DeleteMode DeleteMode { get; init; } = DeleteMode.RecycleBin;
    public OnBlockedBehavior OnBlocked { get; init; } = OnBlockedBehavior.Fail;
}

public sealed record MoveFileRequest
{
    public required string SourcePath { get; init; }
    public required string DestinationPath { get; init; }
    public OnBlockedBehavior OnBlocked { get; init; } = OnBlockedBehavior.Fail;
}

public sealed record CopyFileRequest
{
    public required string SourcePath { get; init; }
    public required string DestinationPath { get; init; }
}
