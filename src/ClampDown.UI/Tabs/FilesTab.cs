using System.ComponentModel;
using System.Text;
using System.Windows.Forms;
using ClampDown.Core.Models;
using ClampDown.Win32;

namespace ClampDown.UI.Tabs;

public sealed class FilesTab : UserControl
{
    private readonly UiServices _services;

    private readonly TextBox _pathTextBox;
    private readonly CheckBox _recursiveCheckBox;
    private readonly Button _analyzeButton;
    private readonly DataGridView _lockersGrid;
    private readonly BindingList<ProcessLockDetail> _lockers = new();

    private readonly Button _closeSelectedButton;
    private readonly Button _forceKillSelectedButton;
    private readonly Button _unlockDeleteButton;
    private readonly Button _unlockRenameButton;
    private readonly Button _unlockMoveButton;
    private readonly Button _unlockCopyButton;
    private readonly Button _scheduleDeleteButton;
    private readonly Button _copyBlockersButton;

    private string? _lastAnalyzedPath;

    public FilesTab(UiServices services)
    {
        _services = services;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(12)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        var top = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = false };
        _pathTextBox = new TextBox { Width = 560, AllowDrop = true };
        _pathTextBox.TextChanged += (_, _) => UpdateButtons();
        _pathTextBox.DragEnter += PathTextBox_DragEnter;
        _pathTextBox.DragDrop += PathTextBox_DragDrop;

        var browseFile = new Button { Text = "Browse File…" };
        browseFile.Click += (_, _) => BrowseFile();
        var browseFolder = new Button { Text = "Browse Folder…" };
        browseFolder.Click += (_, _) => BrowseFolder();

        _analyzeButton = new Button { Text = "Analyze" };
        _analyzeButton.Click += async (_, _) => await AnalyzeAsync();

        _recursiveCheckBox = new CheckBox { Text = "Scan recursively", AutoSize = true };

        top.Controls.Add(new Label { Text = "Path:", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
        top.Controls.Add(_pathTextBox);
        top.Controls.Add(browseFile);
        top.Controls.Add(browseFolder);
        top.Controls.Add(_analyzeButton);
        top.Controls.Add(_recursiveCheckBox);
        root.Controls.Add(top);

        _lockersGrid = BuildLockersGrid();
        root.Controls.Add(_lockersGrid);

        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true };
        _closeSelectedButton = new Button { Text = "Close Selected Apps" };
        _closeSelectedButton.Click += async (_, _) => await CloseSelectedAsync(force: false);

        _forceKillSelectedButton = new Button { Text = "Force Kill Selected" };
        _forceKillSelectedButton.Click += async (_, _) => await CloseSelectedAsync(force: true);

        _unlockDeleteButton = new Button { Text = "Unlock && Delete" };
        _unlockDeleteButton.Click += async (_, _) => await UnlockDeleteAsync();

        _unlockRenameButton = new Button { Text = "Unlock && Rename" };
        _unlockRenameButton.Click += async (_, _) => await UnlockRenameAsync();

        _unlockMoveButton = new Button { Text = "Unlock && Move" };
        _unlockMoveButton.Click += async (_, _) => await UnlockMoveAsync();

        _unlockCopyButton = new Button { Text = "Unlock && Copy" };
        _unlockCopyButton.Click += async (_, _) => await UnlockCopyAsync();

        _scheduleDeleteButton = new Button { Text = "Schedule Delete at Reboot" };
        _scheduleDeleteButton.Click += (_, _) => ScheduleDeleteAtReboot();

        _copyBlockersButton = new Button { Text = "Copy Blockers" };
        _copyBlockersButton.Click += (_, _) => CopyBlockersToClipboard();

        bottom.Controls.AddRange(new Control[]
        {
            _closeSelectedButton,
            _forceKillSelectedButton,
            _unlockDeleteButton,
            _unlockRenameButton,
            _unlockMoveButton,
            _unlockCopyButton,
            _scheduleDeleteButton,
            _copyBlockersButton
        });
        root.Controls.Add(bottom);

        UpdateButtons();

        // Apply theme
        _services.ThemeManager.ApplyToControl(this);
        _services.ThemeManager.ThemeChanged += (_, _) => _services.ThemeManager.ApplyToControl(this);
    }

    private DataGridView BuildLockersGrid()
    {
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            ReadOnly = true,
            MultiSelect = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            DataSource = _lockers
        };
        grid.SelectionChanged += (_, _) => UpdateButtons();

        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(ProcessLockDetail.ProcessName), HeaderText = "Process" });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(ProcessLockDetail.ProcessId), HeaderText = "PID", Width = 60 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(ProcessLockDetail.Description), HeaderText = "Description", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        grid.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = nameof(ProcessLockDetail.IsCriticalProcess), HeaderText = "Critical", Width = 70 });

        return grid;
    }

    private void BrowseFile()
    {
        using var dialog = new OpenFileDialog { CheckFileExists = true, CheckPathExists = true };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            _pathTextBox.Text = dialog.FileName;
    }

    private void BrowseFolder()
    {
        using var dialog = new FolderBrowserDialog { ShowNewFolderButton = false };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            _pathTextBox.Text = dialog.SelectedPath;
    }

    private void PathTextBox_DragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
            e.Effect = DragDropEffects.Copy;
    }

    private void PathTextBox_DragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is not string[] items || items.Length == 0)
            return;

        _pathTextBox.Text = items[0];
    }

    private async Task AnalyzeAsync()
    {
        var path = _pathTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            _lockers.Clear();
            var lockers = _services.FileLockAnalysisService.AnalyzePath(path, _recursiveCheckBox.Checked);
            foreach (var locker in lockers)
                _lockers.Add(locker);

            _lastAnalyzedPath = Path.GetFullPath(path);
            _services.ActionLogger.Log(ActionType.FileAnalyze, _lastAnalyzedPath, ActionResult.Success);
        }
        catch (Exception ex)
        {
            _services.ActionLogger.Log(ActionType.FileAnalyze, path, ActionResult.Failed, details: ex.Message);
            MessageBox.Show(this, ex.Message, "Analyze failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UpdateButtons();
            await Task.CompletedTask;
        }
    }

    private async Task CloseSelectedAsync(bool force)
    {
        var selected = GetSelectedLockers();
        if (selected.Count == 0)
            return;

        if (force)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Force terminating processes can cause data loss.");
            sb.AppendLine();
            sb.AppendLine("Selected:");
            foreach (var p in selected)
                sb.AppendLine($"- {p.ProcessName} ({p.ProcessId})");

            if (!UiPrompt.PromptTypedConfirmation(this, "Force kill processes", sb.ToString(), "KILL"))
                return;
        }

        foreach (var locker in selected)
        {
            var result = force
                ? _services.ProcessTerminator.ForceTerminate(locker.ProcessId, killEntireTree: false)
                : _services.ProcessTerminator.TryGracefulClose(locker.ProcessId, TimeSpan.FromSeconds(5));

            _services.ActionLogger.Log(
                force ? ActionType.ProcessTerminate : ActionType.ProcessClose,
                $"{locker.ProcessName} ({locker.ProcessId})",
                result.IsSuccess ? ActionResult.Success : ActionResult.Failed,
                force ? EscalationLevel.Force : EscalationLevel.Graceful,
                result.ErrorMessage ?? "");
        }

        await ReAnalyzeIfPossibleAsync();
    }

    private async Task UnlockDeleteAsync()
    {
        var path = _pathTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            MessageBox.Show(this, "Select an existing file first.", "Unlock & Delete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (_lockers.Count > 0)
        {
            var closeFirst = MessageBox.Show(
                this,
                "ClampDown can request a graceful close of apps locking this file (safe-first). Close apps first?",
                "Close apps first?",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (closeFirst == DialogResult.Yes)
            {
                try
                {
                    _services.FileLockAnalysisService.RequestRestartManagerShutdown(path, _recursiveCheckBox.Checked, forceUnresponsive: false);
                    _services.ActionLogger.Log(ActionType.ProcessClose, path, ActionResult.Success, EscalationLevel.Graceful, "Restart Manager shutdown requested.");
                }
                catch (Exception ex)
                {
                    _services.ActionLogger.Log(ActionType.ProcessClose, path, ActionResult.Failed, EscalationLevel.Graceful, ex.Message);
                    MessageBox.Show(this, ex.Message, "Close apps failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        var result = _services.FileActionService.TryDelete(path, sendToRecycleBin: true, scheduleOnRebootIfBlocked: true);
        if (result.Status == FileOperationStatus.ScheduledForReboot)
        {
            MessageBox.Show(this, "File delete scheduled for next reboot.", "Scheduled", MessageBoxButtons.OK, MessageBoxIcon.Information);
            await AnalyzeAsync();
            return;
        }

        if (!result.Success)
        {
            MessageBox.Show(this, result.ErrorMessage ?? "Delete failed.", "Delete failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            await AnalyzeAsync();
            return;
        }

        MessageBox.Show(this, "File deleted.", "Deleted", MessageBoxButtons.OK, MessageBoxIcon.Information);
        await AnalyzeAsync();
    }

    private async Task UnlockRenameAsync()
    {
        var path = _pathTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            MessageBox.Show(this, "Select an existing file first.", "Unlock & Rename", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var currentName = Path.GetFileName(path);
        var newName = UiPrompt.PromptText(this, "Rename", "New file name:", currentName);
        if (string.IsNullOrWhiteSpace(newName))
            return;

        var dest = Path.Combine(Path.GetDirectoryName(path)!, newName);
        var result = _services.FileActionService.TryMove(path, dest, scheduleOnRebootIfBlocked: true);

        if (result.Status == FileOperationStatus.ScheduledForReboot)
        {
            MessageBox.Show(this, "Rename scheduled for next reboot.", "Scheduled", MessageBoxButtons.OK, MessageBoxIcon.Information);
            _pathTextBox.Text = dest;
            await AnalyzeAsync();
            return;
        }

        if (!result.Success)
        {
            MessageBox.Show(this, result.ErrorMessage ?? "Rename failed.", "Rename failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        _pathTextBox.Text = dest;
        await AnalyzeAsync();
    }

    private async Task UnlockMoveAsync()
    {
        var path = _pathTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            MessageBox.Show(this, "Select an existing file first.", "Unlock & Move", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var folder = new FolderBrowserDialog { ShowNewFolderButton = true };
        if (folder.ShowDialog(this) != DialogResult.OK)
            return;

        var dest = Path.Combine(folder.SelectedPath, Path.GetFileName(path));
        var result = _services.FileActionService.TryMove(path, dest, scheduleOnRebootIfBlocked: true);

        if (result.Status == FileOperationStatus.ScheduledForReboot)
        {
            MessageBox.Show(this, "Move scheduled for next reboot.", "Scheduled", MessageBoxButtons.OK, MessageBoxIcon.Information);
            _pathTextBox.Text = dest;
            await AnalyzeAsync();
            return;
        }

        if (!result.Success)
        {
            MessageBox.Show(this, result.ErrorMessage ?? "Move failed.", "Move failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        _pathTextBox.Text = dest;
        await AnalyzeAsync();
    }

    private async Task UnlockCopyAsync()
    {
        var path = _pathTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            MessageBox.Show(this, "Select an existing file first.", "Unlock & Copy", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var save = new SaveFileDialog { FileName = Path.GetFileName(path) };
        if (save.ShowDialog(this) != DialogResult.OK)
            return;

        var result = _services.FileActionService.TryCopy(path, save.FileName);
        if (!result.Success)
        {
            MessageBox.Show(this, result.ErrorMessage ?? "Copy failed.", "Copy failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        MessageBox.Show(this, $"Copied to: {save.FileName}", "Copied", MessageBoxButtons.OK, MessageBoxIcon.Information);
        await Task.CompletedTask;
    }

    private void ScheduleDeleteAtReboot()
    {
        var path = _pathTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            MessageBox.Show(this, "Select an existing file first.", "Schedule delete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var ok = MessageBox.Show(
            this,
            "This will delete the file on the next Windows restart.\r\n\r\nContinue?",
            "Schedule delete at reboot",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (ok != DialogResult.Yes)
            return;

        try
        {
            FileOperations.ScheduleDeleteOnReboot(Path.GetFullPath(path));
            _services.ActionLogger.Log(ActionType.ScheduleRebootDelete, path, ActionResult.RequiresReboot, EscalationLevel.Scheduled);
            MessageBox.Show(this, "Delete scheduled for next reboot.", "Scheduled", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            _services.ActionLogger.Log(ActionType.ScheduleRebootDelete, path, ActionResult.Failed, EscalationLevel.Scheduled, ex.Message);
            MessageBox.Show(this, ex.Message, "Schedule failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void CopyBlockersToClipboard()
    {
        if (_lockers.Count == 0)
            return;

        var sb = new StringBuilder();
        sb.AppendLine(_lastAnalyzedPath ?? _pathTextBox.Text);
        sb.AppendLine();
        foreach (var locker in _lockers)
            sb.AppendLine($"{locker.ProcessName}\t{locker.ProcessId}\t{locker.Description}");

        Clipboard.SetText(sb.ToString());
    }

    private IReadOnlyList<ProcessLockDetail> GetSelectedLockers()
    {
        var results = new List<ProcessLockDetail>();
        foreach (DataGridViewRow row in _lockersGrid.SelectedRows)
        {
            if (row.DataBoundItem is ProcessLockDetail detail)
                results.Add(detail);
        }
        return results;
    }

    private async Task ReAnalyzeIfPossibleAsync()
    {
        if (string.IsNullOrWhiteSpace(_lastAnalyzedPath))
            return;

        _pathTextBox.Text = _lastAnalyzedPath;
        await AnalyzeAsync();
    }

    private void UpdateButtons()
    {
        var hasSelection = _lockersGrid.SelectedRows.Count > 0;
        var isFile = File.Exists(_pathTextBox.Text.Trim());

        _closeSelectedButton.Enabled = hasSelection;
        _forceKillSelectedButton.Enabled = hasSelection;
        _unlockDeleteButton.Enabled = isFile;
        _unlockRenameButton.Enabled = isFile;
        _unlockMoveButton.Enabled = isFile;
        _unlockCopyButton.Enabled = isFile;
        _scheduleDeleteButton.Enabled = isFile;
        _copyBlockersButton.Enabled = _lockers.Count > 0;
    }
}

