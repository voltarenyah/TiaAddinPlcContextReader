using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace PlcSourceExporter.Core;

public sealed class TagTableDocument
{
    public TagTableDocument(
        string schemaVersion,
        DateTimeOffset generatedUtc,
        string sourceFolder,
        IReadOnlyList<TagTableRow> tags)
    {
        SchemaVersion = schemaVersion;
        GeneratedUtc = generatedUtc;
        SourceFolder = sourceFolder;
        Tags = tags;
    }

    public string SchemaVersion { get; }

    public DateTimeOffset GeneratedUtc { get; }

    public string SourceFolder { get; }

    public IReadOnlyList<TagTableRow> Tags { get; }
}

public sealed class TagTableRow
{
    public TagTableRow(
        string id,
        string tagTable,
        string tagTableSourcePath,
        string name,
        string dataType,
        string rawDataType,
        string logicalAddress,
        bool? externalAccessible,
        bool? externalVisible,
        bool? externalWritable,
        string comment,
        string sourceFile)
    {
        Id = id;
        TagTable = tagTable;
        TagTableSourcePath = tagTableSourcePath;
        Name = name;
        DataType = dataType;
        RawDataType = rawDataType;
        LogicalAddress = logicalAddress;
        ExternalAccessible = externalAccessible;
        ExternalVisible = externalVisible;
        ExternalWritable = externalWritable;
        Comment = comment;
        SourceFile = sourceFile;
    }

    public string Id { get; }

    public string TagTable { get; }

    public string TagTableSourcePath { get; }

    public string Name { get; }

    public string DataType { get; }

    public string RawDataType { get; }

    public string LogicalAddress { get; }

    public bool? ExternalAccessible { get; }

    public bool? ExternalVisible { get; }

    public bool? ExternalWritable { get; }

    public string Comment { get; }

    public string SourceFile { get; }
}

public sealed class ExportedTagTableFile
{
    public ExportedTagTableFile(string filePath, string sourcePath)
    {
        FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        SourcePath = sourcePath ?? string.Empty;
    }

    public string FilePath { get; }

    public string SourcePath { get; }
}

public static class TagTableBuilder
{
    public const string TagsFileName = "tags.json";

    public static string Write(
        string exportRoot,
        IEnumerable<ExportedTagTableFile> tagTableFiles,
        DateTimeOffset generatedUtc)
    {
        if (string.IsNullOrWhiteSpace(exportRoot))
        {
            throw new ArgumentException("Export root is required.", nameof(exportRoot));
        }

        if (tagTableFiles == null)
        {
            throw new ArgumentNullException(nameof(tagTableFiles));
        }

        var rows = new List<TagTableRow>();
        var tagFolder = Path.Combine(exportRoot, ExportCategories.GetFolderName(ExportCategory.TagTable));

        foreach (var tagTableFile in tagTableFiles.OrderBy(item => item.SourcePath, StringComparer.OrdinalIgnoreCase))
        {
            var relativeFile = ToRelativePath(exportRoot, tagTableFile.FilePath);
            rows.AddRange(ParseRows(File.ReadAllText(tagTableFile.FilePath), relativeFile, tagTableFile.SourcePath));
        }

        var document = new TagTableDocument("1.0", generatedUtc, tagFolder, rows);
        var filePath = Path.Combine(exportRoot, TagsFileName);
        File.WriteAllText(filePath, TagTableJsonSerializer.Serialize(document));
        return filePath;
    }

    public static IReadOnlyList<TagTableRow> ParseRows(string xml, string sourceFile, string tagTableSourcePath)
    {
        if (xml == null)
        {
            throw new ArgumentNullException(nameof(xml));
        }

        XDocument document;
        try
        {
            document = XDocument.Parse(xml);
        }
        catch (XmlException)
        {
            return Array.Empty<TagTableRow>();
        }

        var tagTable = document.Descendants().FirstOrDefault(element => element.Name.LocalName == "SW.Tags.PlcTagTable");
        if (tagTable == null)
        {
            return Array.Empty<TagTableRow>();
        }

        var rawTagTableName = GetDirectAttributeValue(tagTable, "Name");
        var tagTableName = string.IsNullOrWhiteSpace(rawTagTableName) ? null : rawTagTableName;
        if (tagTableName == null)
        {
            tagTableName = Path.GetFileNameWithoutExtension(sourceFile) ?? string.Empty;
        }

        var rows = new List<TagTableRow>();
        foreach (var tag in tagTable.Descendants().Where(element => element.Name.LocalName == "SW.Tags.PlcTag"))
        {
            var rawTagName = GetDirectAttributeValue(tag, "Name");
            if (string.IsNullOrWhiteSpace(rawTagName))
            {
                continue;
            }

            var tagName = rawTagName!;
            var rawDataType = GetDirectAttributeValue(tag, "DataTypeName") ?? string.Empty;
            var logicalAddress = GetDirectAttributeValue(tag, "LogicalAddress") ?? string.Empty;
            rows.Add(new TagTableRow(
                $"tag:{tagTableName}:{tagName}:{logicalAddress}",
                tagTableName,
                tagTableSourcePath,
                tagName,
                NormalizeDataType(rawDataType),
                rawDataType,
                logicalAddress,
                ParseNullableBool(GetDirectAttributeValue(tag, "ExternalAccessible")),
                ParseNullableBool(GetDirectAttributeValue(tag, "ExternalVisible")),
                ParseNullableBool(GetDirectAttributeValue(tag, "ExternalWritable")),
                GetPreferredComment(tag),
                sourceFile));
        }

        return rows;
    }

    private static string? GetDirectAttributeValue(XElement element, string name)
    {
        return element
            .Elements()
            .FirstOrDefault(child => child.Name.LocalName == "AttributeList")
            ?.Elements()
            .FirstOrDefault(child => child.Name.LocalName == name)
            ?.Value
            .Trim();
    }

    private static string NormalizeDataType(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[trimmed.Length - 1] == '"'
            ? trimmed.Substring(1, trimmed.Length - 2)
            : trimmed;
    }

    private static bool? ParseNullableBool(string? value)
    {
        return bool.TryParse(value, out var parsed) ? parsed : null;
    }

    private static string GetPreferredComment(XElement tag)
    {
        var comment = tag
            .Descendants()
            .FirstOrDefault(element =>
                element.Name.LocalName == "MultilingualText" &&
                string.Equals((string?)element.Attribute("CompositionName"), "Comment", StringComparison.OrdinalIgnoreCase));

        if (comment == null)
        {
            return string.Empty;
        }

        var items = comment
            .Descendants()
            .Where(element => element.Name.LocalName == "MultilingualTextItem")
            .Select(item => new
            {
                Culture = GetDirectAttributeValue(item, "Culture") ?? string.Empty,
                Text = GetDirectAttributeValue(item, "Text") ?? string.Empty
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Text))
            .ToArray();

        return items.FirstOrDefault(item => string.Equals(item.Culture, "en-GB", StringComparison.OrdinalIgnoreCase))?.Text
            ?? items.FirstOrDefault(item => string.Equals(item.Culture, "en-US", StringComparison.OrdinalIgnoreCase))?.Text
            ?? items.FirstOrDefault()?.Text
            ?? string.Empty;
    }

    private static string ToRelativePath(string exportRoot, string filePath)
    {
        var root = Path.GetFullPath(exportRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var fullPath = Path.GetFullPath(filePath);
        return fullPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            ? fullPath.Substring(root.Length + 1)
            : filePath;
    }
}

public static class TagTableJsonSerializer
{
    public static string Serialize(TagTableDocument document)
    {
        var builder = new StringBuilder();
        builder.AppendLine("{");
        WriteProperty(builder, 1, "schemaVersion", document.SchemaVersion, appendComma: true);
        WriteProperty(builder, 1, "generatedUtc", document.GeneratedUtc.ToString("O"), appendComma: true);
        WriteProperty(builder, 1, "sourceFolder", document.SourceFolder, appendComma: true);
        WriteProperty(builder, 1, "tagCount", document.Tags.Count, appendComma: true);
        Indent(builder, 1).AppendLine("\"tags\": [");

        for (var index = 0; index < document.Tags.Count; index++)
        {
            WriteTag(builder, document.Tags[index], index < document.Tags.Count - 1);
        }

        Indent(builder, 1).AppendLine("]");
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static void WriteTag(StringBuilder builder, TagTableRow row, bool appendComma)
    {
        Indent(builder, 2).AppendLine("{");
        WriteProperty(builder, 3, "id", row.Id, appendComma: true);
        WriteProperty(builder, 3, "tagTable", row.TagTable, appendComma: true);
        WriteProperty(builder, 3, "tagTableSourcePath", row.TagTableSourcePath, appendComma: true);
        WriteProperty(builder, 3, "name", row.Name, appendComma: true);
        WriteProperty(builder, 3, "dataType", row.DataType, appendComma: true);
        WriteProperty(builder, 3, "rawDataType", row.RawDataType, appendComma: true);
        WriteProperty(builder, 3, "logicalAddress", row.LogicalAddress, appendComma: true);
        WriteProperty(builder, 3, "externalAccessible", row.ExternalAccessible, appendComma: true);
        WriteProperty(builder, 3, "externalVisible", row.ExternalVisible, appendComma: true);
        WriteProperty(builder, 3, "externalWritable", row.ExternalWritable, appendComma: true);
        WriteProperty(builder, 3, "comment", row.Comment, appendComma: true);
        WriteProperty(builder, 3, "sourceFile", row.SourceFile, appendComma: false);
        Indent(builder, 2).Append('}');
        if (appendComma)
        {
            builder.Append(',');
        }

        builder.AppendLine();
    }

    private static void WriteProperty(StringBuilder builder, int indentLevel, string name, string value, bool appendComma)
    {
        Indent(builder, indentLevel)
            .Append('"')
            .Append(Escape(name))
            .Append("\": \"")
            .Append(Escape(value))
            .Append('"');
        AppendCommaAndNewLine(builder, appendComma);
    }

    private static void WriteProperty(StringBuilder builder, int indentLevel, string name, int value, bool appendComma)
    {
        Indent(builder, indentLevel)
            .Append('"')
            .Append(Escape(name))
            .Append("\": ")
            .Append(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
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
