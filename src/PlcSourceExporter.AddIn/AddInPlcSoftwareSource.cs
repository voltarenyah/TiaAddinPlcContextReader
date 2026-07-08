using PlcSourceExporter.Core;
using Siemens.Engineering;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;
using Siemens.Engineering.SW;

namespace PlcSourceExporter.AddIn;

internal static class AddInPlcSoftwareSource
{
    public static IPlcSoftwareSource Create(
        PlcSoftware plcSoftware,
        IExportLogger? logger = null,
        IProgress<ExportProgress>? progress = null)
    {
        return SiemensPlcSoftwareSource.Create(plcSoftware, ExportViaReflection, logger, progress);
    }

    public static PlcSoftware? TryResolvePlcSoftware(DeviceItem deviceItem)
    {
        var softwareContainer = deviceItem.GetService<SoftwareContainer>();
        return softwareContainer?.Software as PlcSoftware;
    }

    private static void ExportViaReflection(object exportable, string filePath)
    {
        exportable.GetType().GetMethod("Export", new[] { typeof(FileInfo), typeof(ExportOptions) })!
            .Invoke(exportable, new object[] { new FileInfo(filePath), ExportOptions.WithDefaults });
    }
}

internal static class AddInProjectPaths
{
    public static string GetDefaultExportRoot(Project project)
    {
        var projectPath = project.Path;
        if (projectPath == null || projectPath.DirectoryName == null)
        {
            throw new InvalidOperationException("Unable to resolve project folder for export.");
        }

        return Path.Combine(projectPath.DirectoryName, "UserFiles", "export");
    }
}
