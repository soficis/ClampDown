using ClampDown.Core.Policy;
using ClampDown.Core.Services;

namespace ClampDown.UI;

public sealed class UiServices
{
    public UiServices()
    {
        SafetyPolicy = new SafetyPolicy();
        ActionLogger = new ActionLogger();
        ProcessTerminator = new ProcessTerminator(SafetyPolicy);
        FileLockAnalysisService = new FileLockAnalysisService(SafetyPolicy);
        FileActionService = new FileActionService(ActionLogger, FileLockAnalysisService);
        SupportBundleExporter = new SupportBundleExporter(ActionLogger);
        ProcessDiscoveryService = new ProcessDiscoveryService(SafetyPolicy);
        ElevatedHelperClient = new ElevatedHelperClient();
        ThemeManager = new ThemeManager();
    }

    public SafetyPolicy SafetyPolicy { get; }
    public ActionLogger ActionLogger { get; }
    public ProcessTerminator ProcessTerminator { get; }
    public FileLockAnalysisService FileLockAnalysisService { get; }
    public FileActionService FileActionService { get; }
    public SupportBundleExporter SupportBundleExporter { get; }
    public ProcessDiscoveryService ProcessDiscoveryService { get; }
    public ElevatedHelperClient ElevatedHelperClient { get; }
    public ThemeManager ThemeManager { get; }
}

