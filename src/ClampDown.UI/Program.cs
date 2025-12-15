using System.Windows.Forms;

namespace ClampDown.UI;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        var services = new UiServices();
        Application.Run(new MainForm(services));
    }
}

