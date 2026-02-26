using System.Drawing;
using System.Windows.Forms;
using ClampDown.Win32;

namespace ClampDown.UI;

internal static class TrayRunner
{
    public static int Run()
    {
        ApplicationConfiguration.Initialize();

        using var tray = new ClampDownTrayApplication();
        Application.Run();
        return 0;
    }
}

internal sealed class ClampDownTrayApplication : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _menu;
    private readonly Icon _icon;

    public ClampDownTrayApplication()
    {
        _menu = new ContextMenuStrip();
        _icon = AppIconLoader.Load();
        _notifyIcon = new NotifyIcon
        {
            Text = "ClampDown",
            Icon = _icon,
            Visible = true,
            ContextMenuStrip = _menu
        };

        _notifyIcon.MouseUp += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
                _menu.Show(Cursor.Position);
        };

        RebuildMenu();
    }

    private void RebuildMenu()
    {
        _menu.Items.Clear();

        var refresh = new ToolStripMenuItem("Refresh");
        refresh.Click += (_, _) => RebuildMenu();
        _menu.Items.Add(refresh);

        _menu.Items.Add(new ToolStripSeparator());

        var drives = DriveDiscovery.GetRemovableDrives();
        if (drives.Count == 0)
        {
            _menu.Items.Add(new ToolStripMenuItem("(No removable drives)") { Enabled = false });
        }
        else
        {
            foreach (var drive in drives)
            {
                var label = string.IsNullOrWhiteSpace(drive.VolumeLabel)
                    ? drive.DriveLetter
                    : $"{drive.VolumeLabel} ({drive.DriveLetter})";

                var item = new ToolStripMenuItem(label);
                var eject = new ToolStripMenuItem("Eject");
                eject.Click += (_, _) => EjectDrive(drive);

                item.DropDownItems.Add(eject);
                _menu.Items.Add(item);
            }
        }

        _menu.Items.Add(new ToolStripSeparator());

        var exit = new ToolStripMenuItem("Exit");
        exit.Click += (_, _) => Application.Exit();
        _menu.Items.Add(exit);
    }

    private void EjectDrive(RemovableDriveInfo drive)
    {
        if (string.IsNullOrWhiteSpace(drive.DeviceInstanceId))
        {
            ShowBalloon("Eject failed", "Drive could not be mapped to a device instance ID.");
            return;
        }

        try
        {
            var result = DriveOperations.RequestDeviceEject(drive.DeviceInstanceId);
            if (result.Success)
            {
                ShowBalloon("Ejected", $"{drive.DriveLetter} ejected.");
                RebuildMenu();
                return;
            }

            ShowBalloon("Eject vetoed", result.ErrorMessage ?? "Device could not be ejected.");
        }
        catch (Exception ex)
        {
            ShowBalloon("Eject failed", ex.Message);
        }
    }

    private void ShowBalloon(string title, string text)
    {
        _notifyIcon.ShowBalloonTip(4000, title, text, ToolTipIcon.Info);
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _menu.Dispose();
        _icon.Dispose();
    }
}
