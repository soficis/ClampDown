using System.ComponentModel;
using System.Text;
using System.Windows.Forms;
using ClampDown.Core.Models;
using ClampDown.Core.Services;
using ClampDown.Win32;

namespace ClampDown.UI.Tabs;

public sealed class DrivesTab : UserControl
{
    private readonly UiServices _services;

    private readonly DataGridView _drivesGrid;
    private readonly BindingList<DriveRow> _drives = new();

    private readonly Button _refreshButton;
    private readonly Button _closeExplorerButton;
    private readonly Button _stopAppsButton;
    private readonly Button _showLockersButton;
    private readonly Button _safeEjectButton;

    public DrivesTab(UiServices services)
    {
        _services = services;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(0)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        // Header Section
        var header = new Panel { Dock = DockStyle.Top, Height = 100, Padding = new Padding(0, 20, 0, 0) };
        var title = new Label
        {
            Text = "Drive Management",
            AutoSize = true,
            Font = new Font("Segoe UI Light", 24),
            Tag = "Header",
            Location = new Point(0, 0)
        };
        
        _refreshButton = new Button 
        { 
            Text = "Refresh List", 
            Location = new Point(0, 55), 
            Size = new Size(120, 32), 
            FlatStyle = FlatStyle.Flat,
            BackColor = services.ThemeManager.CurrentTheme.Surface,
            Font = new Font("Segoe UI Semibold", 9)
        };
        _refreshButton.Click += (_, _) => ReloadDrives();
        
        header.Controls.Add(title);
        header.Controls.Add(_refreshButton);
        root.Controls.Add(header, 0, 0);

        _drivesGrid = BuildDrivesGrid();
        root.Controls.Add(_drivesGrid, 0, 1);

        // Actions Toolbar
        var actionsBar = new FlowLayoutPanel 
        { 
            Dock = DockStyle.Bottom, 
            AutoSize = true, 
            Padding = new Padding(0, 20, 0, 0),
            BackColor = Color.Transparent
        };
        
        _safeEjectButton = CreateActionBtn("Safe Eject", services.ThemeManager.CurrentTheme.Primary);
        _safeEjectButton.ForeColor = Color.White;
        _safeEjectButton.Click += async (_, _) => await SafeEjectSelectedDriveAsync();

        _showLockersButton = CreateActionBtn("Show Lockers", services.ThemeManager.CurrentTheme.Surface);
        _showLockersButton.Click += async (_, _) => await ShowDriveLockersAsync();

        _stopAppsButton = CreateActionBtn("Stop Apps", services.ThemeManager.CurrentTheme.Surface);
        _stopAppsButton.Click += async (_, _) => await StopAppsFromSelectedDriveAsync();

        _closeExplorerButton = CreateActionBtn("Close Explorer", services.ThemeManager.CurrentTheme.Surface);
        _closeExplorerButton.Click += (_, _) => CloseExplorerForSelectedDrive();

        actionsBar.Controls.AddRange(new Control[] 
        { 
            _safeEjectButton,
            _showLockersButton,
            _stopAppsButton, 
            _closeExplorerButton 
        });
        root.Controls.Add(actionsBar, 0, 2);

        ReloadDrives();
        UpdateButtons();

        _services.ThemeManager.ApplyToControl(this);
        _services.ThemeManager.ThemeChanged += (_, _) => _services.ThemeManager.ApplyToControl(this);
    }

    private Button CreateActionBtn(string text, Color backColor)
    {
        return new Button
        {
            Text = text,
            AutoSize = true,
            Height = 36,
            Padding = new Padding(10, 0, 10, 0),
            FlatStyle = FlatStyle.Flat,
            BackColor = backColor,
            Margin = new Padding(0, 0, 10, 0),
            Font = new Font("Segoe UI Semibold", 9)
        };
    }

    private DataGridView BuildDrivesGrid()
    {
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            ReadOnly = true,
            MultiSelect = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            DataSource = _drives
        };

        grid.SelectionChanged += (_, _) => UpdateButtons();

        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(DriveRow.DriveLetter), HeaderText = "Drive", Width = 70 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(DriveRow.Name), HeaderText = "Name", Width = 220 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(DriveRow.Summary), HeaderText = "Details", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(DriveRow.FreeSpace), HeaderText = "Free", Width = 120 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(DriveRow.TotalSize), HeaderText = "Total", Width = 120 });

        return grid;
    }

    private void ReloadDrives()
    {
        _drives.Clear();

        foreach (var drive in DriveDiscovery.GetRemovableDrives())
        {
            _drives.Add(new DriveRow
            {
                DriveLetter = drive.DriveLetter,
                Name = string.IsNullOrWhiteSpace(drive.VolumeLabel) ? drive.DriveLetter : drive.VolumeLabel,
                RootPath = drive.RootPath,
                DeviceInstanceId = drive.DeviceInstanceId,
                Summary = BuildDriveSummary(drive),
                FreeSpace = drive.FreeSpaceBytes.HasValue ? UiFormat.FormatBytes(drive.FreeSpaceBytes.Value) : "Unknown",
                TotalSize = drive.TotalSizeBytes.HasValue ? UiFormat.FormatBytes(drive.TotalSizeBytes.Value) : "Unknown"
            });
        }
    }

    private static string BuildDriveSummary(RemovableDriveInfo drive)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(drive.InterfaceType))
            parts.Add(drive.InterfaceType);
        if (!string.IsNullOrWhiteSpace(drive.FileSystem))
            parts.Add(drive.FileSystem);
        if (!string.IsNullOrWhiteSpace(drive.Model))
            parts.Add(drive.Model);
        return parts.Count == 0 ? "Removable" : string.Join(" • ", parts);
    }

    private DriveRow? GetSelectedDrive()
    {
        if (_drivesGrid.SelectedRows.Count == 0)
            return null;
        return _drivesGrid.SelectedRows[0].DataBoundItem as DriveRow;
    }

    private void UpdateButtons()
    {
        var hasSelection = GetSelectedDrive() != null;
        _closeExplorerButton.Enabled = hasSelection;
        _stopAppsButton.Enabled = hasSelection;
        _showLockersButton.Enabled = hasSelection;
        _safeEjectButton.Enabled = hasSelection;
    }

    private void CloseExplorerForSelectedDrive()
    {
        var drive = GetSelectedDrive();
        if (drive == null)
            return;

        try
        {
            var closed = ExplorerWindowService.CloseExplorerWindowsWithPathPrefix(drive.RootPath);
            _services.ActionLogger.Log(ActionType.DriveAnalyze, drive.DriveLetter, ActionResult.Success, details: $"Closed {closed} Explorer window(s).");
            MessageBox.Show(this, closed == 0 ? "No Explorer windows detected." : $"Closed {closed} Explorer window(s).", "Close Explorer", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            _services.ActionLogger.Log(ActionType.DriveAnalyze, drive.DriveLetter, ActionResult.Failed, details: ex.Message);
            MessageBox.Show(this, ex.Message, "Close Explorer failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task ShowDriveLockersAsync()
    {
        var drive = GetSelectedDrive();
        if (drive == null)
            return;

        try
        {
            var lockers = await Task.Run(() => _services.FileLockAnalysisService.AnalyzePath(drive.RootPath, scanRecursively: false));
            _services.ActionLogger.Log(ActionType.DriveAnalyze, drive.DriveLetter, ActionResult.Success, details: $"Found {lockers.Count} locker(s).");

            var sb = new StringBuilder();
            sb.AppendLine($"{drive.DriveLetter} ({drive.Name})");
            sb.AppendLine(drive.RootPath);
            sb.AppendLine();

            if (lockers.Count == 0)
            {
                sb.AppendLine("No lockers detected via Restart Manager.");
            }
            else
            {
                foreach (var locker in lockers)
                    sb.AppendLine($"{locker.ProcessName}\t{locker.ProcessId}\t{locker.Description}");
            }

            Clipboard.SetText(sb.ToString());
            MessageBox.Show(this, "Drive lockers copied to clipboard.", "Drive lockers", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            _services.ActionLogger.Log(ActionType.DriveAnalyze, drive.DriveLetter, ActionResult.Failed, details: ex.Message);
            MessageBox.Show(this, ex.Message, "Analyze failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task StopAppsFromSelectedDriveAsync()
    {
        var drive = GetSelectedDrive();
        if (drive == null)
            return;

        IReadOnlyList<RunningProcessInfo> processes;
        try
        {
            processes = await Task.Run(() => _services.ProcessDiscoveryService.FindProcessesWithExecutableUnderPath(drive.RootPath));
        }
        catch (Exception ex)
        {
            _services.ActionLogger.Log(ActionType.DriveAnalyze, drive.DriveLetter, ActionResult.Failed, details: ex.Message);
            MessageBox.Show(this, ex.Message, "Stop apps", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        if (processes.Count == 0)
        {
            MessageBox.Show(this, "No running apps detected as launched from this drive.", "Stop apps", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("Apps launched from this drive:");
        sb.AppendLine();
        foreach (var p in processes)
            sb.AppendLine($"{p.ProcessName} ({p.ProcessId})\t{p.ExecutablePath}");

        var action = MessageBox.Show(this, sb.ToString() + "\r\n\r\nClose apps? (No = Force kill)", "Stop apps", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
        if (action == DialogResult.Cancel)
            return;

        if (action == DialogResult.No)
        {
            if (!UiPrompt.PromptTypedConfirmation(this, "Force kill apps", "Force killing may lose unsaved work.", "KILL"))
                return;

            foreach (var p in processes)
            {
                var result = await Task.Run(() => _services.ProcessTerminator.ForceTerminate(p.ProcessId, killEntireTree: true));
                _services.ActionLogger.Log(ActionType.ProcessTerminate, $"{p.ProcessName} ({p.ProcessId})", result.IsSuccess ? ActionResult.Success : ActionResult.Failed, EscalationLevel.Force, result.ErrorMessage ?? "");
            }

            MessageBox.Show(this, "Requested termination for detected processes.", "Force kill", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        foreach (var p in processes)
        {
            var result = await Task.Run(() => _services.ProcessTerminator.TryGracefulClose(p.ProcessId, TimeSpan.FromSeconds(5)));
            _services.ActionLogger.Log(ActionType.ProcessClose, $"{p.ProcessName} ({p.ProcessId})", result.IsSuccess ? ActionResult.Success : ActionResult.Failed, EscalationLevel.Graceful, result.ErrorMessage ?? "");
        }

        MessageBox.Show(this, "Requested close for detected processes.", "Close apps", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async Task SafeEjectSelectedDriveAsync()
    {
        var drive = GetSelectedDrive();
        if (drive == null)
            return;

        if (string.IsNullOrWhiteSpace(drive.DeviceInstanceId))
        {
            MessageBox.Show(this, "Unable to map this drive to a device instance ID.", "Safe eject", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            var result = await Task.Run(() => DriveOperations.RequestDeviceEject(drive.DeviceInstanceId));
            if (result.Success)
            {
                _services.ActionLogger.Log(ActionType.DriveEject, drive.DriveLetter, ActionResult.Success);
                MessageBox.Show(this, "Eject request succeeded.", "Safe eject", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ReloadDrives();
                return;
            }

            _services.ActionLogger.Log(ActionType.DriveEject, drive.DriveLetter, ActionResult.Failed, details: result.ErrorMessage ?? "");
            MessageBox.Show(this, result.ErrorMessage ?? "The device could not be ejected.", "Eject was vetoed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (Exception ex)
        {
            _services.ActionLogger.Log(ActionType.DriveEject, drive.DriveLetter, ActionResult.Failed, details: ex.Message);
            MessageBox.Show(this, ex.Message, "Safe eject failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private sealed class DriveRow
    {
        public required string DriveLetter { get; init; }
        public required string Name { get; init; }
        public required string RootPath { get; init; }
        public required string Summary { get; init; }
        public required string FreeSpace { get; init; }
        public required string TotalSize { get; init; }
        public string? DeviceInstanceId { get; init; }
    }
}
