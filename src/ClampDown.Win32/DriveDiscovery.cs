using System.Management;

namespace ClampDown.Win32;

public static class DriveDiscovery
{
    public static IReadOnlyList<RemovableDriveInfo> GetRemovableDrives()
    {
        var results = new List<RemovableDriveInfo>();

        foreach (var logicalDisk in Query("SELECT DeviceID, VolumeName, FileSystem, Size, FreeSpace FROM Win32_LogicalDisk WHERE DriveType = 2"))
        {
            var deviceId = (string?)logicalDisk["DeviceID"];
            if (string.IsNullOrWhiteSpace(deviceId))
                continue;

            var driveLetter = deviceId.Trim();
            var rootPath = driveLetter.EndsWith(":", StringComparison.Ordinal) ? $"{driveLetter}\\" : driveLetter;

            var size = TryGetInt64(logicalDisk["Size"]);
            var freeSpace = TryGetInt64(logicalDisk["FreeSpace"]);

            var (pnpDeviceId, model, interfaceType, mediaType) = TryGetDiskDriveInfoForLogicalDisk(driveLetter);

            results.Add(new RemovableDriveInfo
            {
                DriveLetter = driveLetter,
                RootPath = rootPath,
                VolumeLabel = (string?)logicalDisk["VolumeName"] ?? "",
                FileSystem = (string?)logicalDisk["FileSystem"] ?? "",
                TotalSizeBytes = size,
                FreeSpaceBytes = freeSpace,
                DeviceInstanceId = pnpDeviceId,
                Model = model,
                InterfaceType = interfaceType,
                MediaType = mediaType
            });
        }

        return results
            .OrderBy(r => r.DriveLetter, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IEnumerable<ManagementObject> Query(string wql)
    {
        using var searcher = new ManagementObjectSearcher(wql);
        using var collection = searcher.Get();
        foreach (ManagementObject item in collection)
            yield return item;
    }

    private static long? TryGetInt64(object? value)
    {
        if (value == null)
            return null;

        if (value is long l)
            return l;

        if (long.TryParse(value.ToString(), out var parsed))
            return parsed;

        return null;
    }

    private static (string? PnpDeviceId, string? Model, string? InterfaceType, string? MediaType) TryGetDiskDriveInfoForLogicalDisk(string driveLetter)
    {
        try
        {
            var escaped = driveLetter.Replace("\\", "\\\\").Replace("'", "''");
            var partitions = Query($"ASSOCIATORS OF {{Win32_LogicalDisk.DeviceID='{escaped}'}} WHERE AssocClass=Win32_LogicalDiskToPartition");

            foreach (var partition in partitions)
            {
                var partitionId = (string?)partition["DeviceID"];
                if (string.IsNullOrWhiteSpace(partitionId))
                    continue;

                var partitionEscaped = partitionId.Replace("\\", "\\\\").Replace("'", "''");
                var drives = Query($"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{partitionEscaped}'}} WHERE AssocClass=Win32_DiskDriveToDiskPartition");

                foreach (var drive in drives)
                {
                    return (
                        (string?)drive["PNPDeviceID"],
                        (string?)drive["Model"],
                        (string?)drive["InterfaceType"],
                        (string?)drive["MediaType"]);
                }
            }
        }
        catch
        {
            // Best-effort only.
        }

        return (null, null, null, null);
    }
}

public sealed record RemovableDriveInfo
{
    public required string DriveLetter { get; init; }
    public required string RootPath { get; init; }
    public string VolumeLabel { get; init; } = "";
    public string FileSystem { get; init; } = "";
    public long? TotalSizeBytes { get; init; }
    public long? FreeSpaceBytes { get; init; }
    public string? DeviceInstanceId { get; init; }
    public string? Model { get; init; }
    public string? InterfaceType { get; init; }
    public string? MediaType { get; init; }
}

