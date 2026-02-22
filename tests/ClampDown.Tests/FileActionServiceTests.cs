using ClampDown.Core.Abstractions;
using ClampDown.Core.HelperIpc;
using ClampDown.Core.Models;
using ClampDown.Core.Policy;
using ClampDown.Core.Services;

namespace ClampDown.Tests;

public class FileActionServiceTests
{
    [Fact]
    public void Delete_FileNotFound_ReturnsNotFound()
    {
        var service = CreateService(out _);
        var missingPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("n"), "missing.txt");

        var result = service.Delete(new DeleteFileRequest
        {
            FilePath = missingPath,
            DeleteMode = DeleteMode.Permanent
        });

        Assert.False(result.Success);
        Assert.Equal(FileOperationStatus.NotFound, result.Status);
    }

    [Fact]
    public void Delete_PermanentDelete_RemovesFile()
    {
        var service = CreateService(out _);
        var tempFile = CreateTempFile();

        var result = service.Delete(new DeleteFileRequest
        {
            FilePath = tempFile,
            DeleteMode = DeleteMode.Permanent
        });

        Assert.True(result.Success);
        Assert.Equal(FileOperationStatus.Success, result.Status);
        Assert.False(File.Exists(tempFile));
    }

    [Fact]
    public void Delete_RecycleBinMode_UsesPlatformAdapter()
    {
        var service = CreateService(out var platform);
        var tempFile = CreateTempFile();

        var result = service.Delete(new DeleteFileRequest
        {
            FilePath = tempFile,
            DeleteMode = DeleteMode.RecycleBin
        });

        Assert.True(result.Success);
        Assert.True(platform.RecycleCalled);
    }

    [Fact]
    public void Copy_FileNotFound_ReturnsNotFound()
    {
        var service = CreateService(out _);
        var source = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("n"), "missing.txt");
        var dest = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("n"), "dest.txt");

        var result = service.Copy(new CopyFileRequest
        {
            SourcePath = source,
            DestinationPath = dest
        });

        Assert.False(result.Success);
        Assert.Equal(FileOperationStatus.NotFound, result.Status);
    }

    private static string CreateTempFile()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ClampDownTests", Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(dir);
        var filePath = Path.Combine(dir, "sample.txt");
        File.WriteAllText(filePath, "test");
        return filePath;
    }

    private static FileActionService CreateService(out FakeFilePlatformOperations platform)
    {
        platform = new FakeFilePlatformOperations();
        var logger = new ActionLogger();
        var analysis = new FileLockAnalysisService(new SafetyPolicy(), new FakeRestartManagerGateway());
        var reboot = new RebootScheduleService(platform, new FakeHelperCommandClient(), new FakeElevatedHelperLauncher());

        return new FileActionService(logger, analysis, reboot, platform);
    }

    private sealed class FakeFilePlatformOperations : IFilePlatformOperations
    {
        public bool RecycleCalled { get; private set; }

        public void SendToRecycleBin(string filePath)
        {
            RecycleCalled = true;
            if (File.Exists(filePath))
                File.Delete(filePath);
        }

        public void ScheduleDeleteOnReboot(string filePath)
        {
            // no-op
        }

        public void ScheduleMoveOnReboot(string sourcePath, string destinationPath)
        {
            // no-op
        }
    }

    private sealed class FakeRestartManagerGateway : IRestartManagerGateway
    {
        public IReadOnlyList<LockingProcessSnapshot> GetLockers(IReadOnlyList<string> resourcePaths) => [];

        public void RequestShutdown(IReadOnlyList<string> resourcePaths, bool forceUnresponsive)
        {
            // no-op
        }
    }

    private sealed class FakeHelperCommandClient : IHelperCommandClient
    {
        public Task<HelperResponse> SendAsync(HelperRequest request, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new HelperResponse
            {
                CorrelationId = request.CorrelationId,
                Success = true
            });
        }
    }

    private sealed class FakeElevatedHelperLauncher : IElevatedHelperLauncher
    {
        public bool TryStart(out string? errorMessage)
        {
            errorMessage = null;
            return true;
        }
    }
}
