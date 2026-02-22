using System.ComponentModel;
using System.Runtime.InteropServices;

namespace ClampDown.Win32;

/// <summary>
/// File operations including scheduling deletion at reboot.
/// </summary>
public static class FileOperations
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool MoveFileExW(
        string lpExistingFileName,
        string? lpNewFileName,
        MoveFileFlags dwFlags);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperationW(ref ShFileOpStruct lpFileOp);

    [Flags]
    private enum MoveFileFlags : uint
    {
        ReplaceExisting = 0x1,
        DelayUntilReboot = 0x4
    }

    private enum FileOperationType : uint
    {
        Delete = 0x3
    }

    [Flags]
    private enum FileOperationFlags : ushort
    {
        AllowUndo = 0x40,
        NoConfirmation = 0x10,
        NoErrorUi = 0x400
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShFileOpStruct
    {
        public IntPtr hwnd;
        public FileOperationType wFunc;
        public string pFrom;
        public string? pTo;
        public FileOperationFlags fFlags;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        public string? lpszProgressTitle;
    }

    /// <summary>
    /// Schedules a file for deletion when the system restarts.
    /// Useful when a file cannot be deleted due to active locks.
    /// </summary>
    public static void ScheduleDeleteOnReboot(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be empty.", nameof(filePath));

        var success = MoveFileExW(filePath, null, MoveFileFlags.DelayUntilReboot);
        if (!success)
        {
            var error = Marshal.GetLastWin32Error();
            throw new Win32Exception(error, $"Failed to schedule delete for: {filePath}");
        }
    }

    /// <summary>
    /// Schedules a move/rename to occur at the next system restart.
    /// </summary>
    public static void ScheduleMoveOnReboot(string sourcePath, string destinationPath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
            throw new ArgumentException("Source path cannot be empty.", nameof(sourcePath));
        if (string.IsNullOrWhiteSpace(destinationPath))
            throw new ArgumentException("Destination path cannot be empty.", nameof(destinationPath));

        var success = MoveFileExW(
            sourcePath,
            destinationPath,
            MoveFileFlags.DelayUntilReboot | MoveFileFlags.ReplaceExisting);

        if (!success)
        {
            var error = Marshal.GetLastWin32Error();
            throw new Win32Exception(error, $"Failed to schedule move for reboot: {sourcePath} -> {destinationPath}");
        }
    }

    /// <summary>
    /// Sends a file to the Recycle Bin (recoverable delete).
    /// </summary>
    public static void SendToRecycleBin(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be empty.", nameof(filePath));

        var op = new ShFileOpStruct
        {
            wFunc = FileOperationType.Delete,
            pFrom = filePath + "\0\0",
            pTo = null,
            fFlags = FileOperationFlags.AllowUndo | FileOperationFlags.NoConfirmation | FileOperationFlags.NoErrorUi,
            fAnyOperationsAborted = false,
            hwnd = IntPtr.Zero,
            hNameMappings = IntPtr.Zero,
            lpszProgressTitle = null
        };

        var result = SHFileOperationW(ref op);
        if (result != 0)
            throw new Win32Exception(result, $"Failed to send to Recycle Bin: {filePath}");

        if (op.fAnyOperationsAborted)
            throw new OperationCanceledException("Recycle Bin operation was aborted.");
    }
}
