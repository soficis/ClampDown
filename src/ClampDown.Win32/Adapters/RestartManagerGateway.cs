using ClampDown.Core.Abstractions;

namespace ClampDown.Win32.Adapters;

public sealed class RestartManagerGateway : IRestartManagerGateway
{
    public IReadOnlyList<LockingProcessSnapshot> GetLockers(IReadOnlyList<string> resourcePaths)
    {
        using var client = new RestartManagerClient();
        client.StartSession();
        client.RegisterResources(resourcePaths);

        return client
            .GetLockers()
            .Select(Map)
            .ToList();
    }

    public void RequestShutdown(IReadOnlyList<string> resourcePaths, bool forceUnresponsive)
    {
        using var client = new RestartManagerClient();
        client.StartSession();
        client.RegisterResources(resourcePaths);
        client.RequestShutdown(forceUnresponsive);
    }

    private static LockingProcessSnapshot Map(ProcessLockInfo processLockInfo)
    {
        return new LockingProcessSnapshot
        {
            ProcessId = processLockInfo.ProcessId,
            ProcessName = processLockInfo.ProcessName,
            ServiceName = processLockInfo.ServiceName,
            ProcessType = processLockInfo.ApplicationType switch
            {
                RmAppType.Service => LockingProcessType.Service,
                RmAppType.Explorer => LockingProcessType.Explorer,
                RmAppType.Critical => LockingProcessType.Critical,
                RmAppType.MainWindow or RmAppType.OtherWindow or RmAppType.Console => LockingProcessType.Application,
                _ => LockingProcessType.Unknown
            }
        };
    }
}
