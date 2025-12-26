using System.ComponentModel;
using System.Windows.Forms;

namespace ClampDown.UI.Tabs;

public sealed class LogsTab : UserControl
{
    private readonly UiServices _services;

    private readonly DataGridView _grid;
    private readonly BindingList<LogRow> _rows = new();

    public LogsTab(UiServices services)
    {
        _services = services;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(12)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        _grid = BuildGrid();
        root.Controls.Add(_grid);

        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true };
        var exportJson = new Button { Text = "Export JSON…", AutoSize = true };
        exportJson.Click += async (_, _) => await ExportLogsAsync("json");

        var exportMarkdown = new Button { Text = "Export Markdown…", AutoSize = true };
        exportMarkdown.Click += async (_, _) => await ExportLogsAsync("md");

        var exportBundle = new Button { Text = "Export Support Bundle…", AutoSize = true };
        exportBundle.Click += async (_, _) => await ExportSupportBundleAsync();

        var clear = new Button { Text = "Clear Log", AutoSize = true };
        clear.Click += (_, _) => ClearLogs();

        bottom.Controls.AddRange(new Control[] { exportJson, exportMarkdown, exportBundle, clear });
        root.Controls.Add(bottom);

        _services.ActionLogger.Changed += (_, _) => Reload();
        Reload();

        // Apply theme
        _services.ThemeManager.ApplyToControl(this);
        _services.ThemeManager.ThemeChanged += (_, _) => _services.ThemeManager.ApplyToControl(this);
    }

    private DataGridView BuildGrid()
    {
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            ReadOnly = true,
            MultiSelect = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            DataSource = _rows
        };

        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(LogRow.Time), HeaderText = "Time", Width = 160 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(LogRow.Action), HeaderText = "Action", Width = 180 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(LogRow.Target), HeaderText = "Target", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(LogRow.Result), HeaderText = "Result", Width = 120 });

        return grid;
    }

    private void Reload()
    {
        if (InvokeRequired)
        {
            BeginInvoke(Reload);
            return;
        }

        _rows.Clear();
        foreach (var entry in _services.ActionLogger.Entries.OrderByDescending(e => e.Timestamp))
        {
            _rows.Add(new LogRow
            {
                Time = entry.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                Action = entry.Type.ToString(),
                Target = entry.Target,
                Result = entry.Result.ToString()
            });
        }
    }

    private async Task ExportLogsAsync(string extension)
    {
        using var save = new SaveFileDialog
        {
            Filter = extension == "json" ? "JSON (*.json)|*.json" : "Markdown (*.md)|*.md",
            FileName = $"ClampDown-ActivityLog-{DateTime.Now:yyyyMMdd-HHmmss}.{extension}"
        };

        if (save.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            await _services.ActionLogger.SaveToFileAsync(save.FileName);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Export failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task ExportSupportBundleAsync()
    {
        using var save = new SaveFileDialog
        {
            Filter = "ZIP (*.zip)|*.zip",
            FileName = $"ClampDown-SupportBundle-{DateTime.Now:yyyyMMdd-HHmmss}.zip"
        };

        if (save.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            await _services.SupportBundleExporter.ExportZipAsync(save.FileName);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Export failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ClearLogs()
    {
        var ok = MessageBox.Show(this, "Clear all in-memory log entries?", "Clear log", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (ok == DialogResult.Yes)
            _services.ActionLogger.Clear();
    }

    private sealed class LogRow
    {
        public required string Time { get; init; }
        public required string Action { get; init; }
        public required string Target { get; init; }
        public required string Result { get; init; }
    }
}

