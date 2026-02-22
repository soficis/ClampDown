using ClampDown.Core.Abstractions;
using ClampDown.Core.HelperIpc;
using ClampDown.Core.Policy;
using ClampDown.Core.Services;
using ClampDown.Win32.Adapters;

namespace ClampDown.UI;

public sealed class UiServices
{
    public UiServices()
    {
        HelperSession = HelperSessionFactory.CreateForCurrentProcess();

        SafetyPolicy = new SafetyPolicy();
        ActionLogger = new ActionLogger();
        ThemeManager = new ThemeManager();

        FilePlatformOperations = new Win32FilePlatformOperations();
        RestartManagerGateway = new RestartManagerGateway();

        ElevatedHelperClient = new ElevatedHelperClient(HelperSession);
        ElevatedHelperLauncher = new ElevatedHelperLauncher(HelperSession);

        RebootScheduleService = new RebootScheduleService(FilePlatformOperations, ElevatedHelperClient, ElevatedHelperLauncher);
        FileLockAnalysisService = new FileLockAnalysisService(SafetyPolicy, RestartManagerGateway);
        FileActionService = new FileActionService(ActionLogger, FileLockAnalysisService, RebootScheduleService, FilePlatformOperations);

        ProcessTerminator = new ProcessTerminator(SafetyPolicy);
        ProcessDiscoveryService = new ProcessDiscoveryService(SafetyPolicy);
        SupportBundleExporter = new SupportBundleExporter(ActionLogger);
    }

    public HelperSession HelperSession { get; }
    public SafetyPolicy SafetyPolicy { get; }
    public ActionLogger ActionLogger { get; }
    public ProcessTerminator ProcessTerminator { get; }
    public FileLockAnalysisService FileLockAnalysisService { get; }
    public RebootScheduleService RebootScheduleService { get; }
    public FileActionService FileActionService { get; }
    public SupportBundleExporter SupportBundleExporter { get; }
    public ProcessDiscoveryService ProcessDiscoveryService { get; }
    public ElevatedHelperClient ElevatedHelperClient { get; }
    public IElevatedHelperLauncher ElevatedHelperLauncher { get; }
    public IFilePlatformOperations FilePlatformOperations { get; }
    public IRestartManagerGateway RestartManagerGateway { get; }
    public ThemeManager ThemeManager { get; }
}
