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
        CopyAllowed = 0x2,
        DelayUntilReboot = 0x4,
        WriteThrough = 0x8,
        CreateHardlink = 0x10,
        FailIfNotTrackable = 0x20
    }

    private enum FileOperationType : uint
    {
        FO_MOVE = 0x1,
        FO_COPY = 0x2,
        FO_DELETE = 0x3,
        FO_RENAME = 0x4
    }

    [Flags]
    private enum FileOperationFlags : ushort
    {
        FOF_ALLOWUNDO = 0x40,
        FOF_NOCONFIRMATION = 0x10,
        FOF_SILENT = 0x4,
        FOF_NOERRORUI = 0x400
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
    /// <param name="filePath">Full path to the file to delete.</param>
    /// <exception cref="Win32Exception">Thrown if the operation fails.</exception>
    public static void ScheduleDeleteOnReboot(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be empty.", nameof(filePath));

        bool success = MoveFileExW(filePath, null, MoveFileFlags.DelayUntilReboot);
        
        if (!success)
        {
            int error = Marshal.GetLastWin32Error();
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

        bool success = MoveFileExW(
            sourcePath,
            destinationPath,
            MoveFileFlags.DelayUntilReboot | MoveFileFlags.ReplaceExisting);

        if (!success)
        {
            int error = Marshal.GetLastWin32Error();
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
            wFunc = FileOperationType.FO_DELETE,
            pFrom = filePath + "\0\0",
            pTo = null,
            fFlags = FileOperationFlags.FOF_ALLOWUNDO | FileOperationFlags.FOF_NOCONFIRMATION | FileOperationFlags.FOF_NOERRORUI,
            fAnyOperationsAborted = false,
            hwnd = IntPtr.Zero,
            hNameMappings = IntPtr.Zero,
            lpszProgressTitle = null
        };

        int result = SHFileOperationW(ref op);
        if (result != 0)
            throw new Win32Exception(result, $"Failed to send to Recycle Bin: {filePath}");

        if (op.fAnyOperationsAborted)
            throw new OperationCanceledException("Recycle Bin operation was aborted.");
    }

    /// <summary>
    /// Attempts to delete a file, returning success status without throwing.
    /// </summary>
    public static bool TryDelete(string filePath, out string? errorMessage)
    {
        errorMessage = null;
        
        try
        {
            File.Delete(filePath);
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Attempts to rename/move a file, returning success status without throwing.
    /// </summary>
    public static bool TryMove(string sourcePath, string destinationPath, out string? errorMessage)
    {
        errorMessage = null;
        
        try
        {
            File.Move(sourcePath, destinationPath);
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }
}
