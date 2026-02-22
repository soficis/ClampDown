namespace ClampDown.Core.Abstractions;

public interface IFilePlatformOperations
{
    void SendToRecycleBin(string filePath);
    void ScheduleDeleteOnReboot(string filePath);
    void ScheduleMoveOnReboot(string sourcePath, string destinationPath);
}
