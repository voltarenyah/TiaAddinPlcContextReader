using PlcSourceExporter.Core;
using PlcSourceExporter.AddInShared;
using Siemens.Engineering;
using Siemens.Engineering.AddIn.Menu;
using Siemens.Engineering.HW;

namespace PlcSourceExporter.AddIn;

public sealed class ExportPlcSourceDataContextMenu : ContextMenuAddIn
{
    private const string MenuText = "Export PLC Source Data";

    public ExportPlcSourceDataContextMenu()
        : base(MenuText)
    {
    }

    protected override void BuildContextMenuItems(ContextMenuAddInRoot addInRoot)
    {
        addInRoot.Items.AddActionItem<DeviceItem>(MenuText, OnClick, OnUpdateStatus);
    }

    private static MenuStatus OnUpdateStatus(MenuSelectionProvider<DeviceItem> provider)
    {
        var deviceItem = provider.GetSelection<DeviceItem>().SingleOrDefault();
        return deviceItem != null && AddInPlcSoftwareSource.TryResolvePlcSoftware(deviceItem) != null
            ? MenuStatus.Enabled
            : MenuStatus.Hidden;
    }

    private static void OnClick(MenuSelectionProvider<DeviceItem> provider)
    {
        var deviceItem = provider.GetSelection<DeviceItem>().SingleOrDefault();
        if (deviceItem == null)
        {
            return;
        }

        var plcSoftware = AddInPlcSoftwareSource.TryResolvePlcSoftware(deviceItem);
        if (plcSoftware == null)
        {
            return;
        }

        var project = ResolveProject(deviceItem);
        var exportRoot = AddInProjectPaths.GetDefaultExportRoot(project);
        var logFile = Path.Combine(exportRoot, "PlcSourceExporter.log");
        var logger = new FileExportLogger(logFile);

        ExportAddInWorkflow.Run(
            deviceItem.Name,
            exportRoot,
            logFile,
            logger,
            new AddInSemanticPlcModelWriter(GetSemanticModelHelperPath()),
            progress => AddInPlcSoftwareSource.Create(plcSoftware, logger, progress),
            () => TryAcquireExclusiveAccess(deviceItem));
    }

    private static string GetSemanticModelHelperPath()
    {
        return Path.Combine(
            @"C:\Program Files\Siemens\Automation\Portal V20\AddIns\PlcSourceExporter",
            "ExportAnalyzer",
            "PlcSourceExporter.ExportAnalyzer.exe");
    }

    private static Project ResolveProject(IEngineeringObject selectedObject)
    {
        var current = selectedObject;
        while (current != null)
        {
            if (current is Project project)
            {
                return project;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Unable to resolve the selected PLC's owning TIA project.");
    }

    private static IDisposable? TryAcquireExclusiveAccess(IEngineeringObject selectedObject)
    {
        object? current = selectedObject;
        while (current != null)
        {
            try
            {
                var method = current.GetType().GetMethod("ExclusiveAccess", new[] { typeof(string) });
                if (method?.Invoke(current, new object[] { "PlcSourceExporter is exporting PLC source data." }) is IDisposable exclusiveAccess)
                {
                    return exclusiveAccess;
                }

                current = current.GetType().GetProperty("Parent")?.GetValue(current);
            }
            catch
            {
                return null;
            }
        }

        return null;
    }
}
