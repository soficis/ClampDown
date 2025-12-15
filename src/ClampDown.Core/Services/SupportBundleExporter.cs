using System.IO.Compression;
using System.Text;

namespace ClampDown.Core.Services;

public sealed class SupportBundleExporter
{
    private readonly ActionLogger _actionLogger;

    public SupportBundleExporter(ActionLogger actionLogger)
    {
        _actionLogger = actionLogger;
    }

    public async Task ExportZipAsync(string zipFilePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(zipFilePath))
            throw new ArgumentException("Zip file path cannot be empty.", nameof(zipFilePath));

        var normalizedZip = Path.GetFullPath(zipFilePath);
        Directory.CreateDirectory(Path.GetDirectoryName(normalizedZip)!);

        var tempDir = Path.Combine(Path.GetTempPath(), "ClampDown", Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(tempDir);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var jsonPath = Path.Combine(tempDir, "activity-log.json");
            var mdPath = Path.Combine(tempDir, "activity-log.md");
            var envPath = Path.Combine(tempDir, "environment.txt");

            await File.WriteAllTextAsync(jsonPath, _actionLogger.ExportToJson(), cancellationToken);
            await File.WriteAllTextAsync(mdPath, _actionLogger.ExportToMarkdown(), cancellationToken);
            await File.WriteAllTextAsync(envPath, BuildEnvironmentText(), cancellationToken);

            if (File.Exists(normalizedZip))
                File.Delete(normalizedZip);

            ZipFile.CreateFromDirectory(tempDir, normalizedZip, CompressionLevel.Optimal, includeBaseDirectory: false);
        }
        finally
        {
            try
            {
                Directory.Delete(tempDir, recursive: true);
            }
            catch
            {
                // Best-effort cleanup only.
            }
        }
    }

    private static string BuildEnvironmentText()
    {
        var sb = new StringBuilder();
        sb.AppendLine("ClampDown Support Bundle");
        sb.AppendLine();
        sb.AppendLine($"GeneratedUtc: {DateTime.UtcNow:O}");
        sb.AppendLine($"MachineName: {Environment.MachineName}");
        sb.AppendLine($"UserName: {Environment.UserName}");
        sb.AppendLine($"OSVersion: {Environment.OSVersion}");
        sb.AppendLine($".NET: {Environment.Version}");
        sb.AppendLine($"ProcessArch: {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}");
        sb.AppendLine($"OSArch: {System.Runtime.InteropServices.RuntimeInformation.OSArchitecture}");
        return sb.ToString();
    }
}

