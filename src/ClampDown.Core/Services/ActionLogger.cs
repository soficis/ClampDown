using ClampDown.Core.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClampDown.Core.Services;

/// <summary>
/// Manages action logging for audit trail and activity history.
/// </summary>
public class ActionLogger
{
    private readonly List<ActionLogEntry> _entries = new();
    private readonly object _lock = new();

    public event EventHandler? Changed;

    /// <summary>
    /// Gets all log entries.
    /// </summary>
    public IReadOnlyList<ActionLogEntry> Entries
    {
        get
        {
            lock (_lock)
            {
                return _entries.ToList();
            }
        }
    }

    /// <summary>
    /// Logs an action.
    /// </summary>
    public void Log(ActionLogEntry entry)
    {
        lock (_lock)
        {
            _entries.Add(entry);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Creates and logs a new entry.
    /// </summary>
    public void Log(
        ActionType type,
        string target,
        ActionResult result,
        EscalationLevel level = EscalationLevel.Info,
        string details = "",
        int? win32Error = null,
        bool elevationUsed = false)
    {
        Log(new ActionLogEntry
        {
            Timestamp = DateTime.UtcNow,
            Type = type,
            Target = target,
            Result = result,
            EscalationLevel = level,
            Details = details,
            Win32ErrorCode = win32Error,
            ElevationUsed = elevationUsed
        });
    }

    /// <summary>
    /// Clears all log entries.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _entries.Clear();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Exports logs to JSON format.
    /// </summary>
    public string ExportToJson()
    {
        return JsonSerializer.Serialize(Entries, new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        });
    }

    /// <summary>
    /// Exports logs to Markdown format.
    /// </summary>
    public string ExportToMarkdown()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("# ClampDown Activity Log");
        sb.AppendLine();
        sb.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine();
        sb.AppendLine("| Time | Action | Target | Result | Details |");
        sb.AppendLine("|------|--------|--------|--------|---------|");

        foreach (var entry in Entries)
        {
            sb.AppendLine($"| {entry.Timestamp:HH:mm:ss} | {entry.Type} | {entry.Target} | {entry.Result} | {entry.Details} |");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Saves logs to a file.
    /// </summary>
    public async Task SaveToFileAsync(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        var content = extension == ".json" ? ExportToJson() : ExportToMarkdown();
        await File.WriteAllTextAsync(filePath, content);
    }
}
