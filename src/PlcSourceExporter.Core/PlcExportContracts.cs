namespace PlcSourceExporter.Core;

public interface IPlcSoftwareSource
{
    IEnumerable<IPlcExportableObject> EnumerateBlocks();

    IEnumerable<IPlcExportableObject> EnumerateTypes();

    IEnumerable<IPlcExportableObject> EnumerateTagTables();
}

public interface IPlcExportableObject
{
    string Name { get; }

    string ObjectPath { get; }

    string SiemensTypeName { get; }

    string? SkipReason { get; }

    PlcExportableMetadata Metadata { get; }

    void ExportTo(string filePath);
}

public sealed class PlcExportableMetadata
{
    public PlcExportableMetadata(
        string? programmingLanguage,
        string? tiaIdentifier,
        int? number,
        bool? isKnowHowProtected,
        DateTimeOffset? creationDate,
        DateTimeOffset? modifiedDate,
        DateTimeOffset? codeModifiedDate,
        DateTimeOffset? interfaceModifiedDate)
    {
        ProgrammingLanguage = programmingLanguage;
        TiaIdentifier = tiaIdentifier;
        Number = number;
        IsKnowHowProtected = isKnowHowProtected;
        CreationDate = creationDate;
        ModifiedDate = modifiedDate;
        CodeModifiedDate = codeModifiedDate;
        InterfaceModifiedDate = interfaceModifiedDate;
    }

    public static PlcExportableMetadata Empty { get; } = new PlcExportableMetadata(null, null, null, null, null, null, null, null);

    public string? ProgrammingLanguage { get; }

    public string? TiaIdentifier { get; }

    public int? Number { get; }

    public bool? IsKnowHowProtected { get; }

    public DateTimeOffset? CreationDate { get; }

    public DateTimeOffset? ModifiedDate { get; }

    public DateTimeOffset? CodeModifiedDate { get; }

    public DateTimeOffset? InterfaceModifiedDate { get; }
}

public interface IExportLogger
{
    void Info(string message);

    void Warning(string message);

    void Error(string message);
}

public interface ISemanticPlcModelWriter
{
    SemanticPlcModelWriteResult Write(string exportRoot);
}

public sealed class NullExportLogger : IExportLogger
{
    public static readonly NullExportLogger Instance = new NullExportLogger();

    private NullExportLogger()
    {
    }

    public void Info(string message)
    {
    }

    public void Warning(string message)
    {
    }

    public void Error(string message)
    {
    }
}
