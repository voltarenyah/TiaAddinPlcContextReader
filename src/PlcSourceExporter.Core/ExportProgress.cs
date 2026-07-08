namespace PlcSourceExporter.Core;

public enum ExportPhase
{
    Preparing,
    EnumeratingObjects,
    ExportingObjects,
    WritingDerivedArtifacts,
    Completed
}

public sealed class ExportProgress
{
    public ExportProgress(
        ExportPhase phase,
        int percentComplete,
        string message,
        string? currentItem = null,
        int completedItems = 0,
        int totalItems = 0)
    {
        Phase = phase;
        PercentComplete = Math.Max(0, Math.Min(100, percentComplete));
        Message = message ?? string.Empty;
        CurrentItem = currentItem;
        CompletedItems = completedItems;
        TotalItems = totalItems;
    }

    public ExportPhase Phase { get; }

    public int PercentComplete { get; }

    public string Message { get; }

    public string? CurrentItem { get; }

    public int CompletedItems { get; }

    public int TotalItems { get; }
}
