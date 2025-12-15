using System.Runtime.InteropServices;

namespace ClampDown.Win32;

public static class ExplorerWindowService
{
    public static int CloseExplorerWindowsWithPathPrefix(string pathPrefix)
    {
        if (string.IsNullOrWhiteSpace(pathPrefix))
            throw new ArgumentException("Path prefix cannot be empty.", nameof(pathPrefix));

        var normalizedPrefix = NormalizePrefix(pathPrefix);

        object? shell = null;
        object? windows = null;

        try
        {
            var shellType = Type.GetTypeFromProgID("Shell.Application");
            if (shellType == null)
                return 0;

            shell = Activator.CreateInstance(shellType);
            if (shell == null)
                return 0;

            windows = shellType.InvokeMember("Windows", System.Reflection.BindingFlags.InvokeMethod, null, shell, null);
            if (windows == null)
                return 0;

            var countObj = windows.GetType().InvokeMember("Count", System.Reflection.BindingFlags.GetProperty, null, windows, null);
            var count = Convert.ToInt32(countObj);

            var closed = 0;
            for (var i = count - 1; i >= 0; i--)
            {
                object? window = null;
                try
                {
                    window = windows.GetType().InvokeMember("Item", System.Reflection.BindingFlags.InvokeMethod, null, windows, new object[] { i });
                    if (window == null)
                        continue;

                    var path = TryGetExplorerWindowPath(window);
                    if (path == null)
                        continue;

                    if (!path.StartsWith(normalizedPrefix, StringComparison.OrdinalIgnoreCase))
                        continue;

                    window.GetType().InvokeMember("Quit", System.Reflection.BindingFlags.InvokeMethod, null, window, null);
                    closed++;
                }
                catch
                {
                    // Best-effort only.
                }
                finally
                {
                    if (window != null && Marshal.IsComObject(window))
                        Marshal.FinalReleaseComObject(window);
                }
            }

            return closed;
        }
        finally
        {
            if (windows != null && Marshal.IsComObject(windows))
                Marshal.FinalReleaseComObject(windows);
            if (shell != null && Marshal.IsComObject(shell))
                Marshal.FinalReleaseComObject(shell);
        }
    }

    private static string NormalizePrefix(string pathPrefix)
    {
        var full = Path.GetFullPath(pathPrefix);
        if (!full.EndsWith(Path.DirectorySeparatorChar))
            full += Path.DirectorySeparatorChar;
        return full;
    }

    private static string? TryGetExplorerWindowPath(object window)
    {
        try
        {
            var document = window.GetType().InvokeMember("Document", System.Reflection.BindingFlags.GetProperty, null, window, null);
            if (document == null)
                return null;

            try
            {
                var folder = document.GetType().InvokeMember("Folder", System.Reflection.BindingFlags.GetProperty, null, document, null);
                if (folder == null)
                    return null;

                var self = folder.GetType().InvokeMember("Self", System.Reflection.BindingFlags.GetProperty, null, folder, null);
                if (self == null)
                    return null;

                return (string?)self.GetType().InvokeMember("Path", System.Reflection.BindingFlags.GetProperty, null, self, null);
            }
            finally
            {
                if (Marshal.IsComObject(document))
                    Marshal.FinalReleaseComObject(document);
            }
        }
        catch
        {
            return null;
        }
    }
}

