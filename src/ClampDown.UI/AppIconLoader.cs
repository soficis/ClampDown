using System.Drawing;
using System.Windows.Forms;

namespace ClampDown.UI;

internal static class AppIconLoader
{
    public static Icon Load()
    {
        var icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        return icon ?? throw new InvalidOperationException($"Unable to load application icon from '{Application.ExecutablePath}'.");
    }
}
