using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace PlcSourceExporter.Core;

public sealed class ProgramBlockComponent
{
    public ProgramBlockComponent(string name, string category, string sourcePath, string exportedFile)
    {
        Name = name;
        Category = category;
        SourcePath = sourcePath;
        ExportedFile = exportedFile;
    }

    public string Name { get; }

    public string Category { get; }

    public string SourcePath { get; }

    public string ExportedFile { get; }
}

public sealed class ProgramBlockCallEdge
{
    public ProgramBlockCallEdge(
        string callerName,
        string callerCategory,
        string callerSourcePath,
        string callerFile,
        string calleeName,
        string calleeBlockType,
        string instanceDb,
        string compileUnitId,
        string networkTitle,
        int parameterCount)
    {
        CallerName = callerName;
        CallerCategory = callerCategory;
        CallerSourcePath = callerSourcePath;
        CallerFile = callerFile;
        CalleeName = calleeName;
        CalleeBlockType = calleeBlockType;
        InstanceDb = instanceDb;
        CompileUnitId = compileUnitId;
        NetworkTitle = networkTitle;
        ParameterCount = parameterCount;
    }

    public string CallerName { get; }

    public string CallerCategory { get; }

    public string CallerSourcePath { get; }

    public string CallerFile { get; }

    public string CalleeName { get; }

    public string CalleeBlockType { get; }

    public string InstanceDb { get; }

    public string CompileUnitId { get; }

    public string NetworkTitle { get; }

    public int ParameterCount { get; }
}

public sealed class ProgramBlockCallGraphResult
{
    public ProgramBlockCallGraphResult(string jsonFilePath, string markdownFilePath, int componentCount, int edgeCount)
    {
        JsonFilePath = jsonFilePath;
        MarkdownFilePath = markdownFilePath;
        ComponentCount = componentCount;
        EdgeCount = edgeCount;
    }

    public string JsonFilePath { get; }

    public string MarkdownFilePath { get; }

    public int ComponentCount { get; }

    public int EdgeCount { get; }
}

public static class ProgramBlockCallGraphBuilder
{
    public const string JsonFileName = "callgraph.json";
    public const string MarkdownFileName = "calltree.md";

    public static ProgramBlockCallGraphResult Write(string exportRoot, DateTimeOffset generatedUtc)
    {
        if (string.IsNullOrWhiteSpace(exportRoot))
        {
            throw new ArgumentException("Export root is required.", nameof(exportRoot));
        }

        var components = ProgramBlockComponentCatalog.LoadExportedProgramBlocks(exportRoot);
        var edges = new List<ProgramBlockCallEdge>();
        foreach (var component in components)
        {
            var filePath = Path.Combine(exportRoot, component.ExportedFile);
            if (!File.Exists(filePath))
            {
                continue;
            }

            edges.AddRange(ParseCalls(File.ReadAllText(filePath), component));
        }

        var jsonFilePath = Path.Combine(exportRoot, JsonFileName);
        File.WriteAllText(jsonFilePath, SerializeJson(exportRoot, generatedUtc, components, edges));

        var markdownFilePath = Path.Combine(exportRoot, MarkdownFileName);
        File.WriteAllText(markdownFilePath, BuildMarkdown(exportRoot, generatedUtc, components, edges));

        return new ProgramBlockCallGraphResult(jsonFilePath, markdownFilePath, components.Count, edges.Count);
    }

    public static IReadOnlyList<ProgramBlockCallEdge> ParseCalls(string xml, ProgramBlockComponent caller)
    {
        if (xml == null)
        {
            throw new ArgumentNullException(nameof(xml));
        }

        if (caller == null)
        {
            throw new ArgumentNullException(nameof(caller));
        }

        XDocument document;
        try
        {
            document = XDocument.Parse(xml);
        }
        catch (XmlException)
        {
            return Array.Empty<ProgramBlockCallEdge>();
        }

        var compileUnitIndexes = document
            .Descendants()
            .Where(element => element.Name.LocalName == "SW.Blocks.CompileUnit")
            .Select((element, index) => new { Element = element, Index = index + 1 })
            .ToDictionary(item => item.Element, item => item.Index);

        var calls = new List<ProgramBlockCallEdge>();
        foreach (var callInfo in document.Descendants().Where(element => element.Name.LocalName == "CallInfo"))
        {
            var calleeName = GetCalleeName(callInfo);
            if (string.IsNullOrWhiteSpace(calleeName))
            {
                continue;
            }

            var calleeBlockType = ((string?)callInfo.Attribute("BlockType"))?.Trim() ?? string.Empty;
            var compileUnit = callInfo
                .Ancestors()
                .FirstOrDefault(element => element.Name.LocalName == "SW.Blocks.CompileUnit");
            var compileUnitId = ((string?)compileUnit?.Attribute("ID")) ?? string.Empty;
            var networkTitle = compileUnit == null ? string.Empty : GetPreferredMultilingualText(compileUnit, "Title");
            if (string.IsNullOrWhiteSpace(networkTitle) && compileUnit != null && compileUnitIndexes.TryGetValue(compileUnit, out var index))
            {
                networkTitle = $"Network {index}";
            }

            calls.Add(new ProgramBlockCallEdge(
                caller.Name,
                caller.Category,
                caller.SourcePath,
                caller.ExportedFile,
                calleeName!,
                calleeBlockType,
                GetInstanceDb(callInfo),
                compileUnitId,
                networkTitle,
                callInfo.Elements().Count(element => element.Name.LocalName == "Parameter")));
        }

        return calls;
    }

    private static string GetInstanceDb(XElement callInfo)
    {
        var instance = callInfo.Elements().FirstOrDefault(element => element.Name.LocalName == "Instance");
        if (instance == null)
        {
            return string.Empty;
        }

        var components = instance
            .Descendants()
            .Where(element => element.Name.LocalName == "Component")
            .Select(element => ((string?)element.Attribute("Name"))?.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();

        return components.Length == 0 ? string.Empty : string.Join(".", components);
    }

    private static string GetCalleeName(XElement callInfo)
    {
        var name = ((string?)callInfo.Attribute("Name"))?.Trim();
        if (!string.IsNullOrWhiteSpace(name))
        {
            return name!;
        }

        var instance = callInfo.Elements().FirstOrDefault(element => element.Name.LocalName == "Instance");
        if (instance == null)
        {
            return string.Empty;
        }

        var components = instance
            .Descendants()
            .Where(element => element.Name.LocalName == "Component")
            .Select(element => ((string?)element.Attribute("Name"))?.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();

        return components.Length == 0 ? string.Empty : string.Join(".", components);
    }

    private static string GetPreferredMultilingualText(XElement owner, string compositionName)
    {
        var text = owner
            .Descendants()
            .FirstOrDefault(element =>
                element.Name.LocalName == "MultilingualText" &&
                string.Equals((string?)element.Attribute("CompositionName"), compositionName, StringComparison.OrdinalIgnoreCase));

        if (text == null)
        {
            return string.Empty;
        }

        var items = text
            .Descendants()
            .Where(element => element.Name.LocalName == "MultilingualTextItem")
            .Select(item => new
            {
                Culture = GetAttributeListValue(item, "Culture"),
                Text = GetAttributeListValue(item, "Text")
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Text))
            .ToArray();

        return items.FirstOrDefault(item => string.Equals(item.Culture, "en-GB", StringComparison.OrdinalIgnoreCase))?.Text
            ?? items.FirstOrDefault(item => string.Equals(item.Culture, "en-US", StringComparison.OrdinalIgnoreCase))?.Text
            ?? items.FirstOrDefault()?.Text
            ?? string.Empty;
    }

    private static string GetAttributeListValue(XElement element, string name)
    {
        return element
            .Elements()
            .FirstOrDefault(child => child.Name.LocalName == "AttributeList")
            ?.Elements()
            .FirstOrDefault(child => child.Name.LocalName == name)
            ?.Value
            .Trim() ?? string.Empty;
    }

    private static string BuildMarkdown(
        string exportRoot,
        DateTimeOffset generatedUtc,
        IReadOnlyList<ProgramBlockComponent> components,
        IReadOnlyList<ProgramBlockCallEdge> edges)
    {
        var builder = new StringBuilder();
        var componentsByName = components
            .GroupBy(component => component.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var edgesByCaller = edges
            .GroupBy(edge => edge.CallerName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);
        var obs = components
            .Where(component => string.Equals(component.Category, "OB", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        builder.AppendLine("# Program Block Calling Structure");
        builder.AppendLine();
        builder.AppendLine($"Generated: {generatedUtc:O}");
        builder.AppendLine($"Export root: {exportRoot}");
        builder.AppendLine($"Program blocks: {components.Count}");
        builder.AppendLine($"OB roots: {obs.Length}");
        builder.AppendLine($"Call edges: {edges.Count}");
        builder.AppendLine();

        foreach (var ob in obs)
        {
            AppendComponentTree(builder, ob, componentsByName, edgesByCaller, new HashSet<string>(StringComparer.OrdinalIgnoreCase), 0);
            builder.AppendLine();
        }

        builder.AppendLine("## Unresolved Calls");
        builder.AppendLine();
        var unresolved = edges
            .Where(edge => !componentsByName.ContainsKey(edge.CalleeName))
            .OrderBy(edge => edge.CallerName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(edge => edge.CalleeName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (unresolved.Length == 0)
        {
            builder.AppendLine("No unresolved calls.");
        }
        else
        {
            foreach (var edge in unresolved)
            {
                builder.AppendLine($"- {edge.CallerName} -> {edge.CalleeBlockType} {edge.CalleeName}{FormatEdgeDetails(edge)}");
            }
        }

        return builder.ToString();
    }

    private static void AppendComponentTree(
        StringBuilder builder,
        ProgramBlockComponent component,
        IReadOnlyDictionary<string, ProgramBlockComponent> componentsByName,
        IReadOnlyDictionary<string, ProgramBlockCallEdge[]> edgesByCaller,
        HashSet<string> stack,
        int depth)
    {
        builder.Append(' ', depth * 2)
            .Append("- ")
            .Append(component.Category)
            .Append(' ')
            .Append(component.Name);

        if (depth == 0)
        {
            builder.Append(" [").Append(component.SourcePath).Append(']');
        }

        builder.AppendLine();

        if (!stack.Add(component.Name))
        {
            builder.Append(' ', (depth + 1) * 2).AppendLine("- cycle detected");
            return;
        }

        if (edgesByCaller.TryGetValue(component.Name, out var outgoing))
        {
            foreach (var edge in outgoing)
            {
                builder.Append(' ', (depth + 1) * 2)
                    .Append("- ")
                    .Append(edge.CalleeBlockType)
                    .Append(' ')
                    .Append(edge.CalleeName)
                    .Append(FormatEdgeDetails(edge));

                if (!componentsByName.TryGetValue(edge.CalleeName, out var callee))
                {
                    builder.Append(" [not exported]");
                    builder.AppendLine();
                    continue;
                }

                if (stack.Contains(callee.Name))
                {
                    builder.Append(" [cycle]");
                    builder.AppendLine();
                    continue;
                }

                builder.AppendLine();
                AppendComponentChildren(builder, callee, componentsByName, edgesByCaller, stack, depth + 2);
            }
        }

        stack.Remove(component.Name);
    }

    private static void AppendComponentChildren(
        StringBuilder builder,
        ProgramBlockComponent component,
        IReadOnlyDictionary<string, ProgramBlockComponent> componentsByName,
        IReadOnlyDictionary<string, ProgramBlockCallEdge[]> edgesByCaller,
        HashSet<string> stack,
        int depth)
    {
        if (!stack.Add(component.Name))
        {
            builder.Append(' ', depth * 2).AppendLine("- cycle detected");
            return;
        }

        if (edgesByCaller.TryGetValue(component.Name, out var outgoing))
        {
            foreach (var edge in outgoing)
            {
                builder.Append(' ', depth * 2)
                    .Append("- ")
                    .Append(edge.CalleeBlockType)
                    .Append(' ')
                    .Append(edge.CalleeName)
                    .Append(FormatEdgeDetails(edge));

                if (!componentsByName.TryGetValue(edge.CalleeName, out var callee))
                {
                    builder.Append(" [not exported]");
                    builder.AppendLine();
                    continue;
                }

                if (stack.Contains(callee.Name))
                {
                    builder.Append(" [cycle]");
                    builder.AppendLine();
                    continue;
                }

                builder.AppendLine();
                AppendComponentChildren(builder, callee, componentsByName, edgesByCaller, stack, depth + 1);
            }
        }

        stack.Remove(component.Name);
    }

    private static string FormatEdgeDetails(ProgramBlockCallEdge edge)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(edge.InstanceDb))
        {
            parts.Add($"instance DB: {edge.InstanceDb}");
        }

        if (!string.IsNullOrWhiteSpace(edge.NetworkTitle))
        {
            parts.Add($"network: {edge.NetworkTitle}");
        }

        return parts.Count == 0 ? string.Empty : " [" + string.Join("] [", parts) + "]";
    }

    private static string SerializeJson(
        string exportRoot,
        DateTimeOffset generatedUtc,
        IReadOnlyList<ProgramBlockComponent> components,
        IReadOnlyList<ProgramBlockCallEdge> edges)
    {
        var obs = components.Count(component => string.Equals(component.Category, "OB", StringComparison.OrdinalIgnoreCase));
        var builder = new StringBuilder();
        builder.AppendLine("{");
        WriteProperty(builder, 1, "schemaVersion", "1.0", appendComma: true);
        WriteProperty(builder, 1, "generatedUtc", generatedUtc.ToString("O"), appendComma: true);
        WriteProperty(builder, 1, "exportRoot", exportRoot, appendComma: true);
        WriteProperty(builder, 1, "componentCount", components.Count, appendComma: true);
        WriteProperty(builder, 1, "obCount", obs, appendComma: true);
        WriteProperty(builder, 1, "edgeCount", edges.Count, appendComma: true);
        Indent(builder, 1).AppendLine("\"components\": [");
        for (var index = 0; index < components.Count; index++)
        {
            WriteComponent(builder, components[index], index < components.Count - 1);
        }

        Indent(builder, 1).AppendLine("],");
        Indent(builder, 1).AppendLine("\"edges\": [");
        for (var index = 0; index < edges.Count; index++)
        {
            WriteEdge(builder, edges[index], index < edges.Count - 1);
        }

        Indent(builder, 1).AppendLine("]");
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static void WriteComponent(StringBuilder builder, ProgramBlockComponent component, bool appendComma)
    {
        Indent(builder, 2).AppendLine("{");
        WriteProperty(builder, 3, "name", component.Name, appendComma: true);
        WriteProperty(builder, 3, "category", component.Category, appendComma: true);
        WriteProperty(builder, 3, "sourcePath", component.SourcePath, appendComma: true);
        WriteProperty(builder, 3, "exportedFile", component.ExportedFile, appendComma: false);
        Indent(builder, 2).Append('}');
        if (appendComma)
        {
            builder.Append(',');
        }

        builder.AppendLine();
    }

    private static void WriteEdge(StringBuilder builder, ProgramBlockCallEdge edge, bool appendComma)
    {
        Indent(builder, 2).AppendLine("{");
        WriteProperty(builder, 3, "callerName", edge.CallerName, appendComma: true);
        WriteProperty(builder, 3, "callerCategory", edge.CallerCategory, appendComma: true);
        WriteProperty(builder, 3, "callerSourcePath", edge.CallerSourcePath, appendComma: true);
        WriteProperty(builder, 3, "callerFile", edge.CallerFile, appendComma: true);
        WriteProperty(builder, 3, "calleeName", edge.CalleeName, appendComma: true);
        WriteProperty(builder, 3, "calleeBlockType", edge.CalleeBlockType, appendComma: true);
        WriteProperty(builder, 3, "instanceDb", edge.InstanceDb, appendComma: true);
        WriteProperty(builder, 3, "compileUnitId", edge.CompileUnitId, appendComma: true);
        WriteProperty(builder, 3, "networkTitle", edge.NetworkTitle, appendComma: true);
        WriteProperty(builder, 3, "parameterCount", edge.ParameterCount, appendComma: false);
        Indent(builder, 2).Append('}');
        if (appendComma)
        {
            builder.Append(',');
        }

        builder.AppendLine();
    }

    private static void WriteProperty(StringBuilder builder, int indentLevel, string name, string value, bool appendComma)
    {
        Indent(builder, indentLevel).Append('"').Append(Escape(name)).Append("\": \"").Append(Escape(value)).Append('"');
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
