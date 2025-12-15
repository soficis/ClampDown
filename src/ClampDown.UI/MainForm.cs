using System.Windows.Forms;
using ClampDown.UI.Tabs;

namespace ClampDown.UI;

public sealed class MainForm : Form
{
    private readonly UiServices _services;
    private readonly TabControl _tabs;

    public MainForm(UiServices services)
    {
        _services = services;

        Text = "ClampDown";
        Width = 1100;
        Height = 720;
        StartPosition = FormStartPosition.CenterScreen;

        _tabs = new TabControl { Dock = DockStyle.Fill };
        Controls.Add(_tabs);

        _tabs.TabPages.Add(new TabPage("Overview") { Controls = { new OverviewTab(_services, SwitchToTab) { Dock = DockStyle.Fill } } });
        _tabs.TabPages.Add(new TabPage("Files") { Controls = { new FilesTab(_services) { Dock = DockStyle.Fill } } });
        _tabs.TabPages.Add(new TabPage("Drives") { Controls = { new DrivesTab(_services) { Dock = DockStyle.Fill } } });
        _tabs.TabPages.Add(new TabPage("Activity Log") { Controls = { new LogsTab(_services) { Dock = DockStyle.Fill } } });
        _tabs.TabPages.Add(new TabPage("Settings") { Controls = { new SettingsTab(_services) { Dock = DockStyle.Fill } } });

        // Apply theme
        _services.ThemeManager.ApplyToForm(this);
        _services.ThemeManager.ThemeChanged += (_, _) => _services.ThemeManager.ApplyToForm(this);
    }

    public void SwitchToTab(int index)
    {
        if (index >= 0 && index < _tabs.TabPages.Count)
            _tabs.SelectedIndex = index;
    }
}

