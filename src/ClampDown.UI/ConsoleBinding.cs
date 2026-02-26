using System.Runtime.InteropServices;

namespace ClampDown.UI;

internal static class ConsoleBinding
{
    private const uint AttachParentProcess = 0xFFFFFFFF;

    private static StreamReader? _boundInput;
    private static StreamWriter? _boundOutput;
    private static StreamWriter? _boundError;

    public static void EnsureConsole()
    {
        if (!OperatingSystem.IsWindows())
            return;

        if (GetConsoleWindow() == IntPtr.Zero)
        {
            if (!AttachConsole(AttachParentProcess))
                _ = AllocConsole();
        }

        RebindStandardStreams();
    }

    private static void RebindStandardStreams()
    {
        try
        {
            _boundInput?.Dispose();
            _boundOutput?.Dispose();
            _boundError?.Dispose();

            _boundInput = new StreamReader(
                Console.OpenStandardInput(),
                Console.InputEncoding,
                detectEncodingFromByteOrderMarks: false,
                bufferSize: 1024,
                leaveOpen: false);

            _boundOutput = new StreamWriter(Console.OpenStandardOutput(), Console.OutputEncoding)
            {
                AutoFlush = true
            };

            _boundError = new StreamWriter(Console.OpenStandardError(), Console.OutputEncoding)
            {
                AutoFlush = true
            };

            Console.SetIn(_boundInput);
            Console.SetOut(_boundOutput);
            Console.SetError(_boundError);
        }
        catch
        {
            // Ignore stream rebinding failures to preserve command behavior where possible.
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachConsole(uint dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AllocConsole();

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();
}
