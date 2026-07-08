namespace PlcSourceExporter.Core;

public sealed class ExportSummary
{
    private readonly List<ExportRecord> _records = new();

    public IReadOnlyList<ExportRecord> Records => _records;

    public string? MetadataFilePath { get; set; }

    public string? SemanticModelSqliteFilePath { get; set; }

    public string? SemanticModelSchemaFilePath { get; set; }

    public string? SemanticModelAgentGuideFilePath { get; set; }

    public string? BlockProfilesFilePath { get; set; }

    public string? OptimizationHintsFilePath { get; set; }

    public string? ProgramBlockTranslationFilePath { get; set; }

    public string? AiExportGuideFilePath { get; set; }

    public int SuccessCount => _records.Count(record => record.Status == ExportRecordStatus.Success);

    public int SkippedCount => _records.Count(record => record.Status == ExportRecordStatus.Skipped);

    public int FailureCount => _records.Count(record => record.Status == ExportRecordStatus.Failed);

    public IReadOnlyDictionary<ExportCategory, int> SuccessesByCategory => _records
        .Where(record => record.Status == ExportRecordStatus.Success)
        .GroupBy(record => record.Category)
        .ToDictionary(group => group.Key, group => group.Count());

    public void AddSuccess(ExportCategory category, string objectName, string filePath)
    {
        _records.Add(new ExportRecord(category, objectName, ExportRecordStatus.Success, filePath, null));
    }

    public void AddSkipped(ExportCategory category, string objectName, string reason)
    {
        _records.Add(new ExportRecord(category, objectName, ExportRecordStatus.Skipped, null, reason));
    }

    public void AddFailure(ExportCategory category, string objectName, string error)
    {
        _records.Add(new ExportRecord(category, objectName, ExportRecordStatus.Failed, null, error));
    }

    public IReadOnlyList<string> ToDisplayString()
    {
        var lines = new List<string>();
        foreach (var category in ExportCategories.All)
        {
            var count = _records.Count(record => record.Category == category.Category && record.Status == ExportRecordStatus.Success);
            lines.Add($"{category.DisplayName}: {count} exported");
        }

        lines.Add($"{SkippedCount} skipped");
        lines.Add($"{FailureCount} failed");
        return lines;
    }
}

public sealed class ExportRecord
{
    public ExportRecord(ExportCategory category, string objectName, ExportRecordStatus status, string? filePath, string? message)
    {
        Category = category;
        ObjectName = objectName;
        Status = status;
        FilePath = filePath;
        Message = message;
    }

    public ExportCategory Category { get; }

    public string ObjectName { get; }

    public ExportRecordStatus Status { get; }

    public string? FilePath { get; }

    public string? Message { get; }
}

public enum ExportRecordStatus
{
    Success,
    Skipped,
    Failed
}
