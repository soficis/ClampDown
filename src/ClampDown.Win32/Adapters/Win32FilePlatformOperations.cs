using ClampDown.Core.Abstractions;

namespace ClampDown.Win32.Adapters;

public sealed class Win32FilePlatformOperations : IFilePlatformOperations
{
    public void SendToRecycleBin(string filePath)
    {
        FileOperations.SendToRecycleBin(filePath);
    }

    public void ScheduleDeleteOnReboot(string filePath)
    {
        FileOperations.ScheduleDeleteOnReboot(filePath);
    }

    public void ScheduleMoveOnReboot(string sourcePath, string destinationPath)
    {
        FileOperations.ScheduleMoveOnReboot(sourcePath, destinationPath);
    }
}
