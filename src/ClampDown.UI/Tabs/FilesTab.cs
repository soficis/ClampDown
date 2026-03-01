using System.ComponentModel;
using System.Text;
using System.Windows.Forms;
using ClampDown.Core.Models;

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
            Padding = new Padding(0)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        // Header Section
        var header = new Panel { Dock = DockStyle.Top, Height = 140, Padding = new Padding(0, 20, 0, 0) };
        var title = new Label
        {
            Text = "File Analysis",
            AutoSize = true,
            Font = new Font("Segoe UI Light", 24),
            Tag = "Header",
            Location = new Point(0, 0)
        };

        var inputContainer = new Panel { Location = new Point(0, 60), Size = new Size(800, 60) };
        _pathTextBox = new TextBox
        {
            Location = new Point(0, 10),
            Width = 500,
            Font = new Font("Segoe UI", 11),
            PlaceholderText = "Paste file or folder path here...",
            BorderStyle = BorderStyle.FixedSingle,
            AllowDrop = true
        };
        _pathTextBox.TextChanged += (_, _) => UpdateButtons();
        _pathTextBox.DragEnter += PathTextBox_DragEnter;
        _pathTextBox.DragDrop += PathTextBox_DragDrop;

        var browseFile = new Button { Text = "File...", Location = new Point(510, 8), Size = new Size(80, 32), FlatStyle = FlatStyle.Flat };
        browseFile.Click += (_, _) => BrowseFile();

        var browseFolder = new Button { Text = "Folder...", Location = new Point(595, 8), Size = new Size(80, 32), FlatStyle = FlatStyle.Flat };
        browseFolder.Click += (_, _) => BrowseFolder();

        _analyzeButton = new Button
        {
            Text = "Analyze",
            Location = new Point(685, 8),
            Size = new Size(100, 32),
            FlatStyle = FlatStyle.Flat,
            BackColor = services.ThemeManager.CurrentTheme.Primary,
            ForeColor = Color.White
        };
        _analyzeButton.Click += async (_, _) => await AnalyzeAsync();

        _recursiveCheckBox = new CheckBox { Text = "Include subfolders", Font = new Font("Segoe UI", 9), Location = new Point(2, 42), AutoSize = true };

        inputContainer.Controls.Add(_pathTextBox);
        inputContainer.Controls.Add(browseFile);
        inputContainer.Controls.Add(browseFolder);
        inputContainer.Controls.Add(_analyzeButton);
        inputContainer.Controls.Add(_recursiveCheckBox);

        header.Controls.Add(title);
        header.Controls.Add(inputContainer);
        root.Controls.Add(header, 0, 0);

        _lockersGrid = BuildLockersGrid();
        root.Controls.Add(_lockersGrid, 0, 1);

        // Actions Toolbar
        var actionsBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            Padding = new Padding(0, 20, 0, 0),
            BackColor = Color.Transparent
        };

        _closeSelectedButton = CreateActionBtn("Close Apps", services.ThemeManager.CurrentTheme.Surface);
        _closeSelectedButton.Click += async (_, _) => await CloseSelectedAsync(force: false);

        _forceKillSelectedButton = CreateActionBtn("Force Kill", services.ThemeManager.CurrentTheme.Surface);
        _forceKillSelectedButton.Click += async (_, _) => await CloseSelectedAsync(force: true);

        _unlockDeleteButton = CreateActionBtn("Unlock & Delete", services.ThemeManager.CurrentTheme.Primary);
        _unlockDeleteButton.ForeColor = Color.White;
        _unlockDeleteButton.Click += async (_, _) => await UnlockDeleteAsync();

        _unlockRenameButton = CreateActionBtn("Rename", services.ThemeManager.CurrentTheme.Surface);
        _unlockRenameButton.Click += async (_, _) => await UnlockRenameAsync();

        _unlockMoveButton = CreateActionBtn("Move", services.ThemeManager.CurrentTheme.Surface);
        _unlockMoveButton.Click += async (_, _) => await UnlockMoveAsync();

        _unlockCopyButton = CreateActionBtn("Copy", services.ThemeManager.CurrentTheme.Surface);
        _unlockCopyButton.Click += async (_, _) => await UnlockCopyAsync();

        _scheduleDeleteButton = CreateActionBtn("Reboot-Delete", services.ThemeManager.CurrentTheme.Surface);
        _scheduleDeleteButton.Click += (_, _) => ScheduleDeleteAtReboot();

        _copyBlockersButton = CreateActionBtn("Copy Info", services.ThemeManager.CurrentTheme.Surface);
        _copyBlockersButton.Click += (_, _) => CopyBlockersToClipboard();

        actionsBar.Controls.AddRange(new Control[]
        {
            _unlockDeleteButton,
            _closeSelectedButton,
            _forceKillSelectedButton,
            _unlockRenameButton,
            _unlockMoveButton,
            _unlockCopyButton,
            _scheduleDeleteButton,
            _copyBlockersButton
        });
        root.Controls.Add(actionsBar, 0, 2);

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
            var lockers = await Task.Run(() => _services.FileLockAnalysisService.AnalyzePath(path, _recursiveCheckBox.Checked));
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
            var result = await Task.Run(() => force
                ? _services.ProcessTerminator.ForceTerminate(locker.ProcessId, killEntireTree: false)
                : _services.ProcessTerminator.TryGracefulClose(locker.ProcessId, TimeSpan.FromSeconds(5)));

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

        var result = await Task.Run(() => _services.FileActionService.Delete(new DeleteFileRequest
        {
            FilePath = path,
            DeleteMode = DeleteMode.RecycleBin,
            OnBlocked = OnBlockedBehavior.ScheduleOnReboot
        }));

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
        var result = await Task.Run(() => _services.FileActionService.Move(new MoveFileRequest
        {
            SourcePath = path,
            DestinationPath = dest,
            OnBlocked = OnBlockedBehavior.ScheduleOnReboot
        }));

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
        var result = await Task.Run(() => _services.FileActionService.Move(new MoveFileRequest
        {
            SourcePath = path,
            DestinationPath = dest,
            OnBlocked = OnBlockedBehavior.ScheduleOnReboot
        }));

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

        var result = await Task.Run(() => _services.FileActionService.Copy(new CopyFileRequest
        {
            SourcePath = path,
            DestinationPath = save.FileName
        }));

        if (!result.Success)
        {
            MessageBox.Show(this, result.ErrorMessage ?? "Copy failed.", "Copy failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        MessageBox.Show(this, $"Copied to: {save.FileName}", "Copied", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

        var result = _services.FileActionService.ScheduleDeleteOnReboot(path);
        if (result.Success)
        {
            MessageBox.Show(this, "Delete scheduled for next reboot.", "Scheduled", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        MessageBox.Show(this, result.ErrorMessage ?? "Scheduling failed.", "Schedule failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
