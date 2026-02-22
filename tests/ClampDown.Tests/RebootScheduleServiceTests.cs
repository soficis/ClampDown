using System.ComponentModel;
using ClampDown.Core.Abstractions;
using ClampDown.Core.HelperIpc;
using ClampDown.Core.Services;

namespace ClampDown.Tests;

public class RebootScheduleServiceTests
{
    [Fact]
    public void TryScheduleDeleteOnReboot_DirectSuccess_DoesNotUseHelper()
    {
        var platform = new FakeFilePlatformOperations();
        var helperClient = new FakeHelperCommandClient();
        var launcher = new FakeElevatedHelperLauncher();
        var service = new RebootScheduleService(platform, helperClient, launcher);

        var result = service.TryScheduleDeleteOnReboot(@"C:\temp\file.txt", TimeSpan.FromSeconds(2));

        Assert.True(result.Success);
        Assert.False(result.ElevationUsed);
        Assert.Equal(0, helperClient.CallCount);
        Assert.Equal(0, launcher.StartCount);
    }

    [Fact]
    public void TryScheduleDeleteOnReboot_AccessDenied_UsesHelper()
    {
        var platform = new FakeFilePlatformOperations
        {
            OnScheduleDelete = _ => throw new Win32Exception(5, "Access is denied.")
        };

        var helperClient = new FakeHelperCommandClient
        {
            OnSendAsync = (_, _, _) => Task.FromResult(new HelperResponse
            {
                Success = true,
                CorrelationId = Guid.NewGuid()
            })
        };

        var launcher = new FakeElevatedHelperLauncher();
        var service = new RebootScheduleService(platform, helperClient, launcher);

        var result = service.TryScheduleDeleteOnReboot(@"C:\temp\file.txt", TimeSpan.FromSeconds(2));

        Assert.True(result.Success);
        Assert.True(result.ElevationUsed);
        Assert.Equal(1, helperClient.CallCount);
        Assert.Equal(0, launcher.StartCount);
    }

    [Fact]
    public void TryScheduleDeleteOnReboot_HelperUnavailable_StartsHelperAndRetries()
    {
        var platform = new FakeFilePlatformOperations
        {
            OnScheduleDelete = _ => throw new Win32Exception(5, "Access is denied.")
        };

        var helperResponses = new Queue<Func<HelperRequest, TimeSpan, CancellationToken, Task<HelperResponse>>>();
        helperResponses.Enqueue((_, _, _) => throw new IOException("Pipe not available."));
        helperResponses.Enqueue((request, _, _) => Task.FromResult(new HelperResponse
        {
            CorrelationId = request.CorrelationId,
            Success = true
        }));

        var helperClient = new FakeHelperCommandClient
        {
            OnSendAsync = (request, timeout, cancellationToken) => helperResponses.Dequeue()(request, timeout, cancellationToken)
        };

        var launcher = new FakeElevatedHelperLauncher
        {
            OnTryStart = () => (true, null)
        };

        var service = new RebootScheduleService(platform, helperClient, launcher);
        var result = service.TryScheduleDeleteOnReboot(@"C:\temp\file.txt", TimeSpan.FromSeconds(2));

        Assert.True(result.Success);
        Assert.True(result.ElevationUsed);
        Assert.Equal(2, helperClient.CallCount);
        Assert.Equal(1, launcher.StartCount);
    }

    [Fact]
    public void TryScheduleDeleteOnReboot_LaunchFailure_ReturnsFailureMessage()
    {
        var platform = new FakeFilePlatformOperations
        {
            OnScheduleDelete = _ => throw new Win32Exception(5, "Access is denied.")
        };

        var helperClient = new FakeHelperCommandClient
        {
            OnSendAsync = (_, _, _) => throw new IOException("Pipe not available.")
        };

        var launcher = new FakeElevatedHelperLauncher
        {
            OnTryStart = () => (false, "UAC prompt was cancelled.")
        };

        var service = new RebootScheduleService(platform, helperClient, launcher);
        var result = service.TryScheduleDeleteOnReboot(@"C:\temp\file.txt", TimeSpan.FromSeconds(2));

        Assert.False(result.Success);
        Assert.Contains("cancelled", result.ErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, helperClient.CallCount);
        Assert.Equal(1, launcher.StartCount);
    }

    private sealed class FakeFilePlatformOperations : IFilePlatformOperations
    {
        public Action<string> OnScheduleDelete { get; init; } = _ => { };
        public Action<string, string> OnScheduleMove { get; init; } = (_, _) => { };
        public Action<string> OnRecycle { get; init; } = _ => { };

        public void SendToRecycleBin(string filePath) => OnRecycle(filePath);
        public void ScheduleDeleteOnReboot(string filePath) => OnScheduleDelete(filePath);
        public void ScheduleMoveOnReboot(string sourcePath, string destinationPath) => OnScheduleMove(sourcePath, destinationPath);
    }

    private sealed class FakeHelperCommandClient : IHelperCommandClient
    {
        public int CallCount { get; private set; }
        public Func<HelperRequest, TimeSpan, CancellationToken, Task<HelperResponse>> OnSendAsync { get; init; } =
            (request, _, _) => Task.FromResult(new HelperResponse { CorrelationId = request.CorrelationId, Success = true });

        public Task<HelperResponse> SendAsync(HelperRequest request, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return OnSendAsync(request, timeout, cancellationToken);
        }
    }

    private sealed class FakeElevatedHelperLauncher : IElevatedHelperLauncher
    {
        public int StartCount { get; private set; }
        public Func<(bool Success, string? Error)> OnTryStart { get; init; } = () => (true, null);

        public bool TryStart(out string? errorMessage)
        {
            StartCount++;
            var result = OnTryStart();
            errorMessage = result.Error;
            return result.Success;
        }
    }
}
