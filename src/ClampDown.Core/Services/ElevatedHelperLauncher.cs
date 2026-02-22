using ClampDown.Core.Abstractions;
using ClampDown.Core.HelperIpc;

namespace ClampDown.Core.Services;

public sealed class ElevatedHelperLauncher : IElevatedHelperLauncher
{
    private readonly HelperSession _helperSession;

    public ElevatedHelperLauncher(HelperSession helperSession)
    {
        _helperSession = helperSession;
    }

    public bool TryStart(out string? errorMessage)
    {
        var helperPath = HelperProcessLocator.FindHelperExecutablePath();
        if (string.IsNullOrWhiteSpace(helperPath))
        {
            errorMessage = "ClampDown.Helper.exe was not found.";
            return false;
        }

        return HelperProcessLocator.TryStartElevatedHelper(helperPath, _helperSession, out errorMessage);
    }
}
