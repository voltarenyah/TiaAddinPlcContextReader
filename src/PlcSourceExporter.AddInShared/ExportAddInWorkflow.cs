using PlcSourceExporter.Core;

namespace PlcSourceExporter.AddInShared;

internal static class ExportAddInWorkflow
{
    public static void Run(
        string deviceName,
        string exportRoot,
        string logFile,
        IExportLogger logger,
        Func<IProgress<ExportProgress>, IPlcSoftwareSource> createSource,
        Func<IDisposable?> acquireExclusiveAccess)
    {
        ExportProgressWindow? progressWindow = null;
        try
        {
            logger.Info($"Add-in export started for {deviceName}");
            logger.Info($"Export root: {exportRoot}");

            progressWindow = new ExportProgressWindow("Export PLC Source Data", exportRoot, logFile);
            progressWindow.Report(new ExportProgress(ExportPhase.EnumeratingObjects, 5, "Enumerating PLC software objects"));
            using var exclusiveAccess = acquireExclusiveAccess();
            var source = createSource(progressWindow);

            logger.Info("Starting PLC source export");
            var summary = new PlcExportService().Export(
                source,
                exportRoot,
                logger,
                progressWindow,
                progressWindow.CancellationToken);
            foreach (var line in summary.ToDisplayString())
            {
                logger.Info(line);
            }

            logger.Info("Add-in export finished");
            progressWindow.Complete(summary);
        }
        catch (OperationCanceledException)
        {
            logger.Warning("Add-in export canceled; returning control to TIA Portal.");
            progressWindow?.Canceled();
        }
        catch (Exception ex)
        {
            logger.Error(ex.ToString());
            logger.Error("Add-in export failed; returning control to TIA Portal.");
            progressWindow?.Failed(ex);
        }
    }
}
