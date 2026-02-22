using System.Windows.Forms;

namespace ClampDown.UI;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        var services = new UiServices();
        using var mainForm = new MainForm(services);
        Application.Run(mainForm);
    }
}
