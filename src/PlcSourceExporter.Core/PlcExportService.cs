namespace PlcSourceExporter.Core;

public sealed class PlcExportService
{
    private readonly Func<DateTimeOffset> _utcNow;
    private readonly ISemanticPlcModelWriter _semanticModelWriter;

    public PlcExportService()
        : this(() => DateTimeOffset.UtcNow, InProcessSemanticPlcModelWriter.Instance)
    {
    }

    public PlcExportService(Func<DateTimeOffset> utcNow)
        : this(utcNow, InProcessSemanticPlcModelWriter.Instance)
    {
    }

    public PlcExportService(Func<DateTimeOffset> utcNow, ISemanticPlcModelWriter semanticModelWriter)
    {
        _utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
        _semanticModelWriter = semanticModelWriter ?? throw new ArgumentNullException(nameof(semanticModelWriter));
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
        var summary = new ExportSummary();
        var planner = new ExportPathPlanner(exportRoot);
        var metadataWriter = new ExportMetadataWriter(exportRoot, _utcNow());
        var completedObjects = 0;

        Report(progress, ExportPhase.ExportingObjects, 15, "Exporting PLC software objects", completedSteps: completedObjects);

        foreach (var block in plcSoftware.EnumerateBlocks())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var category = BlockCategoryResolver.ResolveBlockCategory(block.SiemensTypeName, block.Name);
            ReportCurrentObject(progress, block.ObjectPath, completedObjects);
            ExportOne(block, category, planner, summary, metadataWriter, logger);
            completedObjects++;
            ReportObjectProgress(progress, block.ObjectPath, completedObjects);
        }

        foreach (var type in plcSoftware.EnumerateTypes())
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReportCurrentObject(progress, type.ObjectPath, completedObjects);
            ExportOne(type, ExportCategory.UserDataType, planner, summary, metadataWriter, logger);
            completedObjects++;
            ReportObjectProgress(progress, type.ObjectPath, completedObjects);
        }

        foreach (var tagTable in plcSoftware.EnumerateTagTables())
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReportCurrentObject(progress, tagTable.ObjectPath, completedObjects);
            ExportOne(tagTable, ExportCategory.TagTable, planner, summary, metadataWriter, logger);
            completedObjects++;
            ReportObjectProgress(progress, tagTable.ObjectPath, completedObjects);
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
        var semanticModel = _semanticModelWriter.Write(exportRoot);
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

    private static void ReportCurrentObject(
        IProgress<ExportProgress>? progress,
        string currentItem,
        int completedItems)
    {
        Report(
            progress,
            ExportPhase.ExportingObjects,
            15,
            $"Exporting PLC software object {completedItems + 1}",
            currentItem,
            completedItems,
            0);
    }

    private static void ReportObjectProgress(
        IProgress<ExportProgress>? progress,
        string currentItem,
        int completedItems)
    {
        Report(
            progress,
            ExportPhase.ExportingObjects,
            15,
            $"Exported {completedItems} PLC software objects",
            currentItem,
            completedItems,
            0);
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
