using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Forms;
using ClampDown.Core.Models;
using ClampDown.Core.Services;
using Microsoft.Win32;

namespace ClampDown.UI.Tabs;

public sealed class SettingsTab : UserControl
{
    private readonly UiServices _services;
    private readonly CheckBox _autoStartCheckBox;

    public SettingsTab(UiServices services)
    {
        _services = services;

        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 11,
            Padding = new Padding(12)
        };
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(panel);

        // Theme Section
        panel.Controls.Add(new Label { Text = "Appearance", AutoSize = true, Font = new Font(Font.FontFamily, 10, FontStyle.Bold) });

        var themeButtons = new FlowLayoutPanel { AutoSize = true };
        var darkModeButton = new Button { Text = "Dark Mode", Width = 100 };
        darkModeButton.Click += (_, _) => _services.ThemeManager.SetTheme(Theme.Dark);

        var lightModeButton = new Button { Text = "Light Mode", Width = 100 };
        lightModeButton.Click += (_, _) => _services.ThemeManager.SetTheme(Theme.Light);

        themeButtons.Controls.Add(darkModeButton);
        themeButtons.Controls.Add(lightModeButton);
        panel.Controls.Add(themeButtons);

        panel.Controls.Add(new Label
        {
            Text = "Choose between dark and light color themes.",
            AutoSize = true,
            Padding = new Padding(0, 0, 0, 12)
        });

        panel.Controls.Add(new Label { Text = "Elevation", AutoSize = true, Font = new Font(Font.FontFamily, 10, FontStyle.Bold) });

        var elevationButtons = new FlowLayoutPanel { AutoSize = true };
        var startHelper = new Button { Text = "Start Elevated Helper" };
        startHelper.Click += async (_, _) => await StartHelperAsync();

        var restartAdmin = new Button { Text = "Restart as Administrator" };
        restartAdmin.Click += async (_, _) => await RestartAsAdminAsync();

        elevationButtons.Controls.Add(startHelper);
        elevationButtons.Controls.Add(restartAdmin);
        panel.Controls.Add(elevationButtons);

        panel.Controls.Add(new Label { Text = "Startup", AutoSize = true, Font = new Font(Font.FontFamily, 10, FontStyle.Bold), Padding = new Padding(0, 12, 0, 0) });

        _autoStartCheckBox = new CheckBox { Text = "Start ClampDown at Windows login (current user)", AutoSize = true };
        _autoStartCheckBox.Checked = IsAutoStartEnabled();
        _autoStartCheckBox.CheckedChanged += (_, _) => ToggleAutoStart();
        panel.Controls.Add(_autoStartCheckBox);

        panel.Controls.Add(new Label
        {
            Text = "This uses the current user Run key (no admin required).",
            AutoSize = true
        });

        // Apply theme
        _services.ThemeManager.ApplyToControl(this);
        _services.ThemeManager.ThemeChanged += (_, _) => _services.ThemeManager.ApplyToControl(this);
    }

    private async Task StartHelperAsync()
    {
        var helperExePath = HelperProcessLocator.FindHelperExecutablePath();
        if (string.IsNullOrWhiteSpace(helperExePath) || !File.Exists(helperExePath))
        {
            MessageBox.Show(this, "ClampDown.Helper.exe was not found.", "Helper not found", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = helperExePath,
                UseShellExecute = true,
                Verb = "runas"
            });

            _services.ActionLogger.Log(ActionType.ProcessTerminate, "ClampDown.Helper", ActionResult.Success, elevationUsed: true, details: "Requested elevated helper start.");
            MessageBox.Show(this, "Elevated helper start requested.", "Helper", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            MessageBox.Show(this, "UAC prompt was cancelled.", "Cancelled", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Failed to start helper", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        await Task.CompletedTask;
    }

    private async Task RestartAsAdminAsync()
    {
        var ok = MessageBox.Show(this, "ClampDown will restart with elevated privileges.", "Restart as administrator", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
        if (ok != DialogResult.OK)
            return;

        try
        {
            var currentExe = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrWhiteSpace(currentExe))
            {
                MessageBox.Show(this, "Unable to resolve current executable path.", "Restart failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = currentExe,
                UseShellExecute = true,
                Verb = "runas"
            });

            Process.GetCurrentProcess().Kill();
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            MessageBox.Show(this, "UAC prompt was cancelled.", "Cancelled", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Restart failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        await Task.CompletedTask;
    }

    private void ToggleAutoStart()
    {
        try
        {
            SetAutoStartEnabled(_autoStartCheckBox.Checked);
        }
        catch (Exception ex)
        {
            _autoStartCheckBox.Checked = IsAutoStartEnabled();
            MessageBox.Show(this, ex.Message, "Startup setting failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static bool IsAutoStartEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", writable: false);
        var value = key?.GetValue("ClampDown") as string;
        return !string.IsNullOrWhiteSpace(value);
    }

    private static void SetAutoStartEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", writable: true)
            ?? Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);

        if (!enabled)
        {
            key.DeleteValue("ClampDown", throwOnMissingValue: false);
            return;
        }

        var exePath = Process.GetCurrentProcess().MainModule?.FileName;
        if (string.IsNullOrWhiteSpace(exePath))
            throw new InvalidOperationException("Unable to resolve current executable path.");

        key.SetValue("ClampDown", $"\"{exePath}\"", RegistryValueKind.String);
    }
}
