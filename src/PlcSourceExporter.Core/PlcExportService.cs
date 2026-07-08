namespace PlcSourceExporter.Core;

public sealed class PlcExportService
{
    private static readonly TimeSpan DefaultObjectCountTimeout = TimeSpan.FromSeconds(15);
    private readonly Func<DateTimeOffset> _utcNow;
    private readonly TimeSpan _objectCountTimeout;

    public PlcExportService()
        : this(() => DateTimeOffset.UtcNow)
    {
    }

    public PlcExportService(Func<DateTimeOffset> utcNow, TimeSpan? objectCountTimeout = null)
    {
        _utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
        _objectCountTimeout = objectCountTimeout ?? DefaultObjectCountTimeout;
    }

    public ExportSummary Export(
        IPlcSoftwareSource plcSoftware,
        string exportRoot,
        IExportLogger? logger = null,
        IProgress<ExportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (plcSoftware == null)
        {
            throw new ArgumentNullException(nameof(plcSoftware));
        }

        if (string.IsNullOrWhiteSpace(exportRoot))
        {
            throw new ArgumentException("Export root is required.", nameof(exportRoot));
        }

        logger ??= NullExportLogger.Instance;
        Report(progress, ExportPhase.Preparing, 0, "Preparing export folder");
        ExportDirectoryPreparer.Prepare(exportRoot);
        logger.Info($"Prepared export folder: {exportRoot}");

        cancellationToken.ThrowIfCancellationRequested();
        var expectedTotalObjects = TryCountObjects(plcSoftware, logger, progress);
        var discoveredObjects = 0;
        Report(
            progress,
            ExportPhase.EnumeratingObjects,
            5,
            expectedTotalObjects.HasValue
                ? $"Reading metadata for {expectedTotalObjects.Value} PLC software objects"
                : "Enumerating PLC software objects",
            completedSteps: 0,
            totalItems: expectedTotalObjects ?? 0);
        var blocks = EnumerateWithProgress(
            plcSoftware.EnumerateBlocks(),
            "PLC block",
            ref discoveredObjects,
            expectedTotalObjects,
            progress,
            cancellationToken);
        var types = EnumerateWithProgress(
            plcSoftware.EnumerateTypes(),
            "PLC user data type",
            ref discoveredObjects,
            expectedTotalObjects,
            progress,
            cancellationToken);
        var tagTables = EnumerateWithProgress(
            plcSoftware.EnumerateTagTables(),
            "PLC tag table",
            ref discoveredObjects,
            expectedTotalObjects,
            progress,
            cancellationToken);
        var totalObjects = blocks.Count + types.Count + tagTables.Count;
        Report(
            progress,
            ExportPhase.EnumeratingObjects,
            15,
            $"Enumerated {totalObjects} PLC software objects",
            completedSteps: totalObjects,
            totalItems: totalObjects);
        Report(progress, ExportPhase.ExportingObjects, totalObjects == 0 ? 75 : 15, $"Exporting {totalObjects} PLC software objects", totalItems: totalObjects);

        var summary = new ExportSummary();
        var planner = new ExportPathPlanner(exportRoot);
        var metadataWriter = new ExportMetadataWriter(exportRoot, _utcNow());
        var completedObjects = 0;

        foreach (var block in blocks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var category = BlockCategoryResolver.ResolveBlockCategory(block.SiemensTypeName, block.Name);
            ExportOne(block, category, planner, summary, metadataWriter, logger);
            completedObjects++;
            ReportObjectProgress(progress, block.ObjectPath, completedObjects, totalObjects);
        }

        foreach (var type in types)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ExportOne(type, ExportCategory.UserDataType, planner, summary, metadataWriter, logger);
            completedObjects++;
            ReportObjectProgress(progress, type.ObjectPath, completedObjects, totalObjects);
        }

        foreach (var tagTable in tagTables)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ExportOne(tagTable, ExportCategory.TagTable, planner, summary, metadataWriter, logger);
            completedObjects++;
            ReportObjectProgress(progress, tagTable.ObjectPath, completedObjects, totalObjects);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var artifactStep = 0;
        const int artifactSteps = 5;
        ReportArtifactProgress(progress, "Writing component metadata", artifactStep++, artifactSteps);
        summary.MetadataFilePath = metadataWriter.Write(_utcNow());
        logger.Info($"Wrote component metadata: {summary.MetadataFilePath}");
        ReportArtifactProgress(progress, "Writing program block logic translation", artifactStep++, artifactSteps);
        var programBlockTranslation = ProgramBlockLogicYamlWriter.Write(exportRoot, _utcNow());
        summary.ProgramBlockTranslationFilePath = programBlockTranslation.FilePath;
        logger.Info($"Wrote program block logic translation: {summary.ProgramBlockTranslationFilePath}");
        ReportArtifactProgress(progress, "Writing semantic PLC model", artifactStep++, artifactSteps);
        var semanticModel = SemanticPlcModelWriter.Write(exportRoot);
        summary.SemanticModelSqliteFilePath = semanticModel.SqliteFilePath;
        summary.SemanticModelSchemaFilePath = semanticModel.SchemaFilePath;
        summary.SemanticModelAgentGuideFilePath = semanticModel.AgentGuideFilePath;
        logger.Info($"Wrote semantic PLC graph database: {summary.SemanticModelSqliteFilePath}");
        logger.Info($"Wrote semantic PLC graph schema: {summary.SemanticModelSchemaFilePath}");
        logger.Info($"Wrote semantic PLC graph agent guide: {summary.SemanticModelAgentGuideFilePath}");
        ReportArtifactProgress(progress, "Writing block profiles", artifactStep++, artifactSteps);
        var profiles = ProgramBlockProfileBuilder.Write(exportRoot, _utcNow());
        summary.BlockProfilesFilePath = profiles.BlockProfilesFilePath;
        summary.OptimizationHintsFilePath = profiles.OptimizationHintsFilePath;
        logger.Info($"Wrote block profiles: {summary.BlockProfilesFilePath}");
        logger.Info($"Wrote optimization hints: {summary.OptimizationHintsFilePath}");
        ReportArtifactProgress(progress, "Writing AI export guide", artifactStep++, artifactSteps);
        summary.AiExportGuideFilePath = AiExportGuideBuilder.Write(exportRoot);
        logger.Info($"Wrote AI export guide: {summary.AiExportGuideFilePath}");
        logger.Info($"Export complete: {summary.SuccessCount} exported, {summary.SkippedCount} skipped, {summary.FailureCount} failed");
        Report(progress, ExportPhase.Completed, 100, $"Export complete: {summary.SuccessCount} exported, {summary.SkippedCount} skipped, {summary.FailureCount} failed");
        return summary;
    }

    private int? TryCountObjects(
        IPlcSoftwareSource plcSoftware,
        IExportLogger logger,
        IProgress<ExportProgress>? progress)
    {
        if (plcSoftware is not IPlcSoftwareObjectCounter counter)
        {
            return null;
        }

        Report(progress, ExportPhase.EnumeratingObjects, 3, "Counting PLC software objects");
        try
        {
            if (counter.TryCountObjects(_objectCountTimeout, logger, progress, out var totalObjects))
            {
                totalObjects = Math.Max(0, totalObjects);
                logger.Info($"Counted PLC software objects before export: {totalObjects}");
                Report(
                    progress,
                    ExportPhase.EnumeratingObjects,
                    5,
                    $"Counted {totalObjects} PLC software objects",
                    completedSteps: 0,
                    totalItems: totalObjects);
                return totalObjects;
            }
        }
        catch (Exception ex)
        {
            logger.Warning($"Unable to count PLC software objects before export: {ExceptionMessages.GetMeaningfulMessage(ex)}");
        }

        logger.Warning("Unable to count PLC software objects before export; using discovered-count progress.");
        Report(progress, ExportPhase.EnumeratingObjects, 5, "Counting unavailable; discovering PLC software objects");
        return null;
    }

    private static List<IPlcExportableObject> EnumerateWithProgress(
        IEnumerable<IPlcExportableObject> objects,
        string objectKind,
        ref int discoveredObjects,
        int? expectedTotalObjects,
        IProgress<ExportProgress>? progress,
        CancellationToken cancellationToken)
    {
        var results = new List<IPlcExportableObject>();
        foreach (var exportable in objects)
        {
            cancellationToken.ThrowIfCancellationRequested();
            discoveredObjects++;
            ReportEnumerationProgress(progress, objectKind, exportable.ObjectPath, discoveredObjects, expectedTotalObjects);
            results.Add(exportable);
        }

        return results;
    }

    private static void ReportEnumerationProgress(
        IProgress<ExportProgress>? progress,
        string objectKind,
        string currentItem,
        int discoveredObjects,
        int? expectedTotalObjects)
    {
        if (expectedTotalObjects is > 0)
        {
            var percent = 5 + (int)Math.Round(Math.Min(discoveredObjects, expectedTotalObjects.Value) * 10.0 / expectedTotalObjects.Value);
            Report(
                progress,
                ExportPhase.EnumeratingObjects,
                percent,
                $"Read metadata for {discoveredObjects} of {expectedTotalObjects.Value} PLC software objects",
                currentItem,
                discoveredObjects,
                expectedTotalObjects.Value);
            return;
        }

        Report(
            progress,
            ExportPhase.EnumeratingObjects,
            5,
            $"Discovered {discoveredObjects} PLC software objects ({objectKind})",
            currentItem,
            discoveredObjects,
            0);
    }

    private static string? ExportOne(
        IPlcExportableObject exportable,
        ExportCategory category,
        ExportPathPlanner planner,
        ExportSummary summary,
        ExportMetadataWriter metadataWriter,
        IExportLogger logger)
    {
        var skipReason = exportable.SkipReason;
        if (!string.IsNullOrWhiteSpace(skipReason))
        {
            summary.AddSkipped(category, exportable.ObjectPath, skipReason!);
            metadataWriter.Add(exportable, category, ExportRecordStatus.Skipped, null, skipReason);
            logger.Warning($"Skipped {ExportCategories.GetDisplayName(category)} {exportable.ObjectPath}: {skipReason}");
            return null;
        }

        var path = planner.NextFilePath(category, exportable.Name);
        try
        {
            exportable.ExportTo(path);
            summary.AddSuccess(category, exportable.ObjectPath, path);
            metadataWriter.Add(exportable, category, ExportRecordStatus.Success, path, null);
            logger.Info($"Exported {ExportCategories.GetDisplayName(category)} {exportable.ObjectPath} -> {path}");
            return path;
        }
        catch (Exception ex)
        {
            var message = ExceptionMessages.GetMeaningfulMessage(ex);
            var nonExportableReason = ExportEligibility.GetNonExportableFailureReason(message);
            if (!string.IsNullOrWhiteSpace(nonExportableReason))
            {
                summary.AddSkipped(category, exportable.ObjectPath, nonExportableReason!);
                metadataWriter.Add(exportable, category, ExportRecordStatus.Skipped, null, nonExportableReason);
                logger.Warning($"Skipped {ExportCategories.GetDisplayName(category)} {exportable.ObjectPath}: {nonExportableReason}");
                return null;
            }

            summary.AddFailure(category, exportable.ObjectPath, message);
            metadataWriter.Add(exportable, category, ExportRecordStatus.Failed, null, message);
            logger.Error($"Failed {ExportCategories.GetDisplayName(category)} {exportable.ObjectPath}: {message}");
            return null;
        }
    }

    private static void ReportObjectProgress(
        IProgress<ExportProgress>? progress,
        string currentItem,
        int completedItems,
        int totalItems)
    {
        var percent = totalItems == 0
            ? 75
            : 15 + (int)Math.Round(completedItems * 60.0 / totalItems);
        Report(
            progress,
            ExportPhase.ExportingObjects,
            percent,
            $"Exported {completedItems} of {totalItems} PLC software objects",
            currentItem,
            completedItems,
            totalItems);
    }

    private static void ReportArtifactProgress(
        IProgress<ExportProgress>? progress,
        string message,
        int completedSteps,
        int totalSteps)
    {
        var percent = 75 + (int)Math.Round(completedSteps * 20.0 / totalSteps);
        Report(progress, ExportPhase.WritingDerivedArtifacts, percent, message, completedSteps: completedSteps, totalItems: totalSteps);
    }

    private static void Report(
        IProgress<ExportProgress>? progress,
        ExportPhase phase,
        int percentComplete,
        string message,
        string? currentItem = null,
        int completedSteps = 0,
        int totalItems = 0)
    {
        progress?.Report(new ExportProgress(phase, percentComplete, message, currentItem, completedSteps, totalItems));
    }
}
