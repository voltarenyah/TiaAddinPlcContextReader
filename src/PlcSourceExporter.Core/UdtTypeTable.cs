using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace PlcSourceExporter.Core;

public sealed class UdtTypeTableDocument
{
    public UdtTypeTableDocument(
        string schemaVersion,
        DateTimeOffset generatedUtc,
        string sourceFolder,
        IReadOnlyList<UdtTypeTableRow> rows)
    {
        SchemaVersion = schemaVersion;
        GeneratedUtc = generatedUtc;
        SourceFolder = sourceFolder;
        Rows = rows;
    }

    public string SchemaVersion { get; }

    public DateTimeOffset GeneratedUtc { get; }

    public string SourceFolder { get; }

    public IReadOnlyList<UdtTypeTableRow> Rows { get; }
}

public sealed class UdtTypeTableRow
{
    public UdtTypeTableRow(
        string id,
        string kind,
        string parentType,
        string parentPath,
        string name,
        string path,
        string dataType,
        string sourcePath,
        string sourceFile)
    {
        Id = id;
        Kind = kind;
        ParentType = parentType;
        ParentPath = parentPath;
        Name = name;
        Path = path;
        DataType = dataType;
        SourcePath = sourcePath;
        SourceFile = sourceFile;
    }

    public string Id { get; }

    public string Kind { get; }

    public string ParentType { get; }

    public string ParentPath { get; }

    public string Name { get; }

    public string Path { get; }

    public string DataType { get; }

    public string SourcePath { get; }

    public string SourceFile { get; }
}

public sealed class UdtExportedFile
{
    public UdtExportedFile(string filePath, string sourcePath)
    {
        FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        SourcePath = sourcePath ?? string.Empty;
    }

    public string FilePath { get; }

    public string SourcePath { get; }
}

public static class UdtTypeTableBuilder
{
    public const string TypeTableFileName = "udt.json";

    public static string Write(
        string exportRoot,
        IEnumerable<UdtExportedFile> udtFiles,
        DateTimeOffset generatedUtc)
    {
        if (string.IsNullOrWhiteSpace(exportRoot))
        {
            throw new ArgumentException("Export root is required.", nameof(exportRoot));
        }

        if (udtFiles == null)
        {
            throw new ArgumentNullException(nameof(udtFiles));
        }

        var rows = new List<UdtTypeTableRow>();
        var seenTypeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var udtFolder = Path.Combine(exportRoot, ExportCategories.GetFolderName(ExportCategory.UserDataType));

        foreach (var udtFile in udtFiles.OrderBy(item => item.SourcePath, StringComparer.OrdinalIgnoreCase))
        {
            var relativeFile = ToRelativePath(exportRoot, udtFile.FilePath);
            var parsedRows = ParseRows(File.ReadAllText(udtFile.FilePath), relativeFile, udtFile.SourcePath);
            var typeName = parsedRows.FirstOrDefault(row => row.Kind == "Type")?.DataType;
            if (string.IsNullOrWhiteSpace(typeName))
            {
                continue;
            }

            if (!seenTypeNames.Add(typeName!))
            {
                continue;
            }

            rows.AddRange(parsedRows);
        }

        var document = new UdtTypeTableDocument("1.0", generatedUtc, udtFolder, rows);
        var filePath = Path.Combine(exportRoot, TypeTableFileName);
        File.WriteAllText(filePath, UdtTypeTableJsonSerializer.Serialize(document));
        return filePath;
    }

    public static IReadOnlyList<UdtTypeTableRow> ParseRows(string xml, string sourceFile, string sourcePath)
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
            return Array.Empty<UdtTypeTableRow>();
        }

        var plcStruct = document.Descendants().FirstOrDefault(element => element.Name.LocalName == "SW.Types.PlcStruct");
        if (plcStruct == null)
        {
            return Array.Empty<UdtTypeTableRow>();
        }

        var attributeList = plcStruct.Elements().FirstOrDefault(element => element.Name.LocalName == "AttributeList");
        var rawTypeName = attributeList?
            .Elements()
            .FirstOrDefault(element => element.Name.LocalName == "Name")
            ?.Value
            .Trim();

        var typeName = string.IsNullOrWhiteSpace(rawTypeName)
            ? Path.GetFileNameWithoutExtension(sourceFile) ?? string.Empty
            : rawTypeName!;

        var rows = new List<UdtTypeTableRow>
        {
            new UdtTypeTableRow(
                $"type:{typeName}",
                "Type",
                string.Empty,
                string.Empty,
                typeName,
                string.Empty,
                typeName,
                sourcePath,
                sourceFile)
        };

        var interfaceElement = attributeList?
            .Elements()
            .FirstOrDefault(element => element.Name.LocalName == "Interface");
        var sectionsElement = interfaceElement?
            .Elements()
            .FirstOrDefault(element => element.Name.LocalName == "Sections");
        var sections = sectionsElement?
            .Elements()
            .Where(element => element.Name.LocalName == "Section")
            .ToArray() ?? Array.Empty<XElement>();

        foreach (var section in sections)
        {
            foreach (var member in section.Elements().Where(element => element.Name.LocalName == "Member"))
            {
                AddMemberRow(rows, member, typeName, sourcePath, sourceFile);
            }
        }

        return rows;
    }

    private static void AddMemberRow(
        List<UdtTypeTableRow> rows,
        XElement member,
        string typeName,
        string sourcePath,
        string sourceFile)
    {
        var rawName = member.Attribute("Name")?.Value?.Trim();
        if (string.IsNullOrWhiteSpace(rawName))
        {
            return;
        }

        var name = rawName!;
        var dataType = member.Attribute("Datatype")?.Value?.Trim() ?? string.Empty;
        rows.Add(new UdtTypeTableRow(
            $"member:{typeName}:{name}",
            "Member",
            typeName,
            string.Empty,
            name,
            name,
            dataType,
            sourcePath,
            sourceFile));
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

public static class UdtTypeTableJsonSerializer
{
    public static string Serialize(UdtTypeTableDocument document)
    {
        var builder = new StringBuilder();
        builder.AppendLine("{");
        WriteProperty(builder, 1, "schemaVersion", document.SchemaVersion, appendComma: true);
        WriteProperty(builder, 1, "generatedUtc", document.GeneratedUtc.ToString("O"), appendComma: true);
        WriteProperty(builder, 1, "sourceFolder", document.SourceFolder, appendComma: true);
        WriteProperty(builder, 1, "rowCount", document.Rows.Count, appendComma: true);
        Indent(builder, 1).AppendLine("\"rows\": [");

        for (var index = 0; index < document.Rows.Count; index++)
        {
            WriteRow(builder, document.Rows[index], index < document.Rows.Count - 1);
        }

        Indent(builder, 1).AppendLine("]");
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static void WriteRow(StringBuilder builder, UdtTypeTableRow row, bool appendComma)
    {
        Indent(builder, 2).AppendLine("{");
        WriteProperty(builder, 3, "id", row.Id, appendComma: true);
        WriteProperty(builder, 3, "kind", row.Kind, appendComma: true);
        WriteProperty(builder, 3, "parentType", row.ParentType, appendComma: true);
        WriteProperty(builder, 3, "parentPath", row.ParentPath, appendComma: true);
        WriteProperty(builder, 3, "name", row.Name, appendComma: true);
        WriteProperty(builder, 3, "path", row.Path, appendComma: true);
        WriteProperty(builder, 3, "dataType", row.DataType, appendComma: true);
        WriteProperty(builder, 3, "sourcePath", row.SourcePath, appendComma: true);
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
