using System.Security.Cryptography;
using System.Text;

namespace PlcSourceExporter.Core;

public sealed class ExportMetadataDocument
{
    public ExportMetadataDocument(
        string schemaVersion,
        DateTimeOffset exportStartedUtc,
        DateTimeOffset exportFinishedUtc,
        string exportRoot,
        IReadOnlyList<ExportMetadataRecord> components)
    {
        SchemaVersion = schemaVersion;
        ExportStartedUtc = exportStartedUtc;
        ExportFinishedUtc = exportFinishedUtc;
        ExportRoot = exportRoot;
        Components = components;
    }

    public string SchemaVersion { get; }

    public DateTimeOffset ExportStartedUtc { get; }

    public DateTimeOffset ExportFinishedUtc { get; }

    public string ExportRoot { get; }

    public IReadOnlyList<ExportMetadataRecord> Components { get; }
}

public sealed class ExportMetadataRecord
{
    public ExportMetadataRecord(
        string id,
        string name,
        string sourcePath,
        string category,
        string folder,
        string siemensTypeName,
        string status,
        string? exportedFile,
        string? message,
        PlcExportableMetadata metadata)
    {
        Id = id;
        Name = name;
        SourcePath = sourcePath;
        Category = category;
        Folder = folder;
        SiemensTypeName = siemensTypeName;
        Status = status;
        ExportedFile = exportedFile;
        Message = message;
        ProgrammingLanguage = metadata.ProgrammingLanguage;
        TiaIdentifier = metadata.TiaIdentifier;
        Number = metadata.Number;
        IsKnowHowProtected = metadata.IsKnowHowProtected;
        CreationDate = metadata.CreationDate;
        ModifiedDate = metadata.ModifiedDate;
        CodeModifiedDate = metadata.CodeModifiedDate;
        InterfaceModifiedDate = metadata.InterfaceModifiedDate;
    }

    public string Id { get; }

    public string Name { get; }

    public string SourcePath { get; }

    public string Category { get; }

    public string Folder { get; }

    public string SiemensTypeName { get; }

    public string Status { get; }

    public string? ExportedFile { get; }

    public string? Message { get; }

    public string? ProgrammingLanguage { get; }

    public string? TiaIdentifier { get; }

    public int? Number { get; }

    public bool? IsKnowHowProtected { get; }

    public DateTimeOffset? CreationDate { get; }

    public DateTimeOffset? ModifiedDate { get; }

    public DateTimeOffset? CodeModifiedDate { get; }

    public DateTimeOffset? InterfaceModifiedDate { get; }
}

public sealed class ExportMetadataWriter
{
    public const string MetadataFileName = "metadata.json";

    private readonly string _exportRoot;
    private readonly DateTimeOffset _exportStartedUtc;
    private readonly List<ExportMetadataRecord> _records = new();

    public ExportMetadataWriter(string exportRoot, DateTimeOffset exportStartedUtc)
    {
        _exportRoot = exportRoot;
        _exportStartedUtc = exportStartedUtc;
    }

    public void Add(
        IPlcExportableObject exportable,
        ExportCategory category,
        ExportRecordStatus status,
        string? exportedFilePath,
        string? message)
    {
        _records.Add(new ExportMetadataRecord(
            CreateStableId(category, exportable.ObjectPath),
            exportable.Name,
            exportable.ObjectPath,
            ExportCategories.GetDisplayName(category),
            ExportCategories.GetFolderName(category),
            exportable.SiemensTypeName,
            GetStatusName(status),
            ToRelativePath(exportedFilePath),
            message,
            exportable.Metadata));
    }

    public string Write(DateTimeOffset exportFinishedUtc)
    {
        var document = new ExportMetadataDocument(
            "1.0",
            _exportStartedUtc,
            exportFinishedUtc,
            _exportRoot,
            _records);

        var filePath = Path.Combine(_exportRoot, MetadataFileName);
        File.WriteAllText(filePath, ExportMetadataJsonSerializer.Serialize(document));
        return filePath;
    }

    private string? ToRelativePath(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return null;
        }

        var root = Path.GetFullPath(_exportRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var fullPath = Path.GetFullPath(filePath);
        return fullPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            ? fullPath.Substring(root.Length + 1)
            : filePath;
    }

    private static string GetStatusName(ExportRecordStatus status)
    {
        return status == ExportRecordStatus.Success ? "Exported" : status.ToString();
    }

    private static string CreateStableId(ExportCategory category, string sourcePath)
    {
        using var sha256 = SHA256.Create();
        var input = $"{ExportCategories.GetDisplayName(category)}|{sourcePath}";
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}

internal static class ExportMetadataJsonSerializer
{
    public static string Serialize(ExportMetadataDocument document)
    {
        var builder = new StringBuilder();
        builder.AppendLine("{");
        WriteProperty(builder, 1, "schemaVersion", document.SchemaVersion, appendComma: true);
        WriteProperty(builder, 1, "exportStartedUtc", document.ExportStartedUtc.ToString("O"), appendComma: true);
        WriteProperty(builder, 1, "exportFinishedUtc", document.ExportFinishedUtc.ToString("O"), appendComma: true);
        WriteProperty(builder, 1, "exportRoot", document.ExportRoot, appendComma: true);
        Indent(builder, 1).AppendLine("\"components\": [");

        for (var index = 0; index < document.Components.Count; index++)
        {
            WriteRecord(builder, document.Components[index], index < document.Components.Count - 1);
        }

        Indent(builder, 1).AppendLine("]");
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static void WriteRecord(StringBuilder builder, ExportMetadataRecord record, bool appendComma)
    {
        Indent(builder, 2).AppendLine("{");
        WriteProperty(builder, 3, "id", record.Id, appendComma: true);
        WriteProperty(builder, 3, "name", record.Name, appendComma: true);
        WriteProperty(builder, 3, "sourcePath", record.SourcePath, appendComma: true);
        WriteProperty(builder, 3, "category", record.Category, appendComma: true);
        WriteProperty(builder, 3, "folder", record.Folder, appendComma: true);
        WriteProperty(builder, 3, "siemensTypeName", record.SiemensTypeName, appendComma: true);
        WriteProperty(builder, 3, "status", record.Status, appendComma: true);
        WriteProperty(builder, 3, "exportedFile", record.ExportedFile, appendComma: true);
        WriteProperty(builder, 3, "message", record.Message, appendComma: true);
        WriteProperty(builder, 3, "programmingLanguage", record.ProgrammingLanguage, appendComma: true);
        WriteProperty(builder, 3, "tiaIdentifier", record.TiaIdentifier, appendComma: true);
        WriteProperty(builder, 3, "number", record.Number, appendComma: true);
        WriteProperty(builder, 3, "isKnowHowProtected", record.IsKnowHowProtected, appendComma: true);
        WriteProperty(builder, 3, "creationDate", record.CreationDate?.ToString("O"), appendComma: true);
        WriteProperty(builder, 3, "modifiedDate", record.ModifiedDate?.ToString("O"), appendComma: true);
        WriteProperty(builder, 3, "codeModifiedDate", record.CodeModifiedDate?.ToString("O"), appendComma: true);
        WriteProperty(builder, 3, "interfaceModifiedDate", record.InterfaceModifiedDate?.ToString("O"), appendComma: false);
        Indent(builder, 2).Append('}');
        if (appendComma)
        {
            builder.Append(',');
        }

        builder.AppendLine();
    }

    private static void WriteProperty(StringBuilder builder, int indentLevel, string name, string? value, bool appendComma)
    {
        Indent(builder, indentLevel).Append('"').Append(Escape(name)).Append("\": ");
        if (value == null)
        {
            builder.Append("null");
        }
        else
        {
            builder.Append('"').Append(Escape(value)).Append('"');
        }

        AppendCommaAndNewLine(builder, appendComma);
    }

    private static void WriteProperty(StringBuilder builder, int indentLevel, string name, int? value, bool appendComma)
    {
        Indent(builder, indentLevel).Append('"').Append(Escape(name)).Append("\": ");
        builder.Append(value.HasValue ? value.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : "null");
        AppendCommaAndNewLine(builder, appendComma);
    }

    private static void WriteProperty(StringBuilder builder, int indentLevel, string name, bool? value, bool appendComma)
    {
        Indent(builder, indentLevel).Append('"').Append(Escape(name)).Append("\": ");
        builder.Append(value.HasValue ? value.Value.ToString().ToLowerInvariant() : "null");
        AppendCommaAndNewLine(builder, appendComma);
    }

    private static void AppendCommaAndNewLine(StringBuilder builder, bool appendComma)
    {
        if (appendComma)
        {
            builder.Append(',');
        }

        builder.AppendLine();
    }

    private static StringBuilder Indent(StringBuilder builder, int indentLevel)
    {
        return builder.Append(' ', indentLevel * 2);
    }

    private static string Escape(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            switch (character)
            {
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                default:
                    if (char.IsControl(character))
                    {
                        builder.Append("\\u").Append(((int)character).ToString("x4", System.Globalization.CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(character);
                    }

                    break;
            }
        }

        return builder.ToString();
    }
}
