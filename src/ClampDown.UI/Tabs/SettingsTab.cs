using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Forms;
using ClampDown.Core.Models;
using Microsoft.Win32;

namespace ClampDown.UI.Tabs;

public sealed class SettingsTab : UserControl
{
    private readonly UiServices _services;

    public SettingsTab(UiServices services)
    {
        _services = services;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(0)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);

        // Header Section
        var header = new Panel { Dock = DockStyle.Top, Height = 80, Padding = new Padding(0, 20, 0, 0) };
        var title = new Label
        {
            Text = "Preferences",
            AutoSize = true,
            Font = new Font("Segoe UI Light", 24),
            Tag = "Header",
            Location = new Point(0, 0)
        };
        header.Controls.Add(title);
        root.Controls.Add(header, 0, 0);

        var content = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(0, 20, 0, 0)
        };
        root.Controls.Add(content, 0, 1);

        // Section: Appearance
        AddSectionHeader(content, "Appearance");
        var themeRow = AddSettingRow(content, "Theme", "Switch between Dark and Light mode.");
        var themeButtons = new FlowLayoutPanel { AutoSize = true, BackColor = Color.Transparent };
        
        var darkBtn = CreateActionBtn("Dark", services.ThemeManager.CurrentTheme.Surface);
        darkBtn.Click += (_, _) => services.ThemeManager.SetTheme(Theme.Dark);
        
        var lightBtn = CreateActionBtn("Light", services.ThemeManager.CurrentTheme.Surface);
        lightBtn.Click += (_, _) => services.ThemeManager.SetTheme(Theme.Light);
        
        themeButtons.Controls.Add(darkBtn);
        themeButtons.Controls.Add(lightBtn);
        themeRow.Controls.Add(themeButtons);

        // Section: System
        AddSectionHeader(content, "System");
        var autoStartRow = AddSettingRow(content, "Startup", "Launch ClampDown when you log in.");
        var autoStartCheckBox = new CheckBox { Text = "Enabled", AutoSize = true, Margin = new Padding(0, 8, 0, 0) };
        autoStartCheckBox.Checked = IsAutoStartEnabled();
        autoStartCheckBox.CheckedChanged += (_, _) => ToggleAutoStart(autoStartCheckBox);
        autoStartRow.Controls.Add(autoStartCheckBox);

        // Section: Elevation
        AddSectionHeader(content, "Privileges");
        var elevRow = AddSettingRow(content, "Elevation", "Run operations with administrative rights.");
        var elevButtons = new FlowLayoutPanel { AutoSize = true, BackColor = Color.Transparent };
        
        var helperBtn = CreateActionBtn("Start Helper", services.ThemeManager.CurrentTheme.Surface);
        helperBtn.Click += (_, _) => StartHelper();
        
        var adminBtn = CreateActionBtn("Restart as Admin", services.ThemeManager.CurrentTheme.Surface);
        adminBtn.Click += (_, _) => RestartAsAdmin();
        
        elevButtons.Controls.Add(helperBtn);
        elevButtons.Controls.Add(adminBtn);
        elevRow.Controls.Add(elevButtons);

        // Section: About
        AddSectionHeader(content, "About");
        var aboutRow = AddSettingRow(content, "ClampDown v0.4.0", "Experimental file lock manager.\r\nUse with caution.");
        content.Controls.Add(aboutRow);

        _services.ThemeManager.ApplyToControl(this);
        _services.ThemeManager.ThemeChanged += (_, _) => _services.ThemeManager.ApplyToControl(this);
    }

    private void AddSectionHeader(FlowLayoutPanel parent, string text)
    {
        parent.Controls.Add(new Label
        {
            Text = text.ToUpper(),
            Font = new Font("Segoe UI Semibold", 9),
            ForeColor = _services.ThemeManager.CurrentTheme.Primary,
            Margin = new Padding(0, 30, 0, 10),
            AutoSize = true,
            Tag = "Accent"
        });
    }

    private FlowLayoutPanel AddSettingRow(FlowLayoutPanel parent, string title, string description)
    {
        var row = new FlowLayoutPanel { Width = 600, AutoSize = true, FlowDirection = FlowDirection.TopDown, Margin = new Padding(0, 0, 0, 20) };
        row.Controls.Add(new Label { Text = title, Font = new Font("Segoe UI Semibold", 11), AutoSize = true });
        row.Controls.Add(new Label { Text = description, Font = new Font("Segoe UI", 9.5f), ForeColor = _services.ThemeManager.CurrentTheme.SecondaryText, AutoSize = true, MaximumSize = new Size(580, 0) });
        parent.Controls.Add(row);
        return row;
    }

    private Button CreateActionBtn(string text, Color backColor)
    {
        return new Button
        {
            Text = text,
            AutoSize = true,
            Height = 32,
            Padding = new Padding(10, 0, 10, 0),
            FlatStyle = FlatStyle.Flat,
            BackColor = backColor,
            Margin = new Padding(0, 8, 10, 0),
            Font = new Font("Segoe UI Semibold", 9)
        };
    }

    private void ToggleAutoStart(CheckBox cb)
    {
        try
        {
            SetAutoStartEnabled(cb.Checked);
        }
        catch (Exception ex)
        {
            cb.Checked = IsAutoStartEnabled();
            MessageBox.Show(this, ex.Message, "Startup setting failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void StartHelper()
    {
        if (!_services.ElevatedHelperLauncher.TryStart(out var errorMessage))
        {
            MessageBox.Show(this, errorMessage ?? "Failed to start elevated helper mode.", "Helper start failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        _services.ActionLogger.Log(ActionType.HelperStart, "ClampDown", ActionResult.Success, elevationUsed: true, details: "Requested elevated helper mode start.");
        MessageBox.Show(this, "Elevated helper start requested.", "Helper", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void RestartAsAdmin()
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
