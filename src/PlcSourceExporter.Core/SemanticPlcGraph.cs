using Microsoft.Data.Sqlite;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;
using System.Runtime.Serialization.Json;
using System.Xml;
using System.Xml.Linq;

namespace PlcSourceExporter.Core;

public static class SemanticNodeKind
{
    public const string Project = "Project";
    public const string PlcDevice = "PLC Device";
    public const string OrganizationBlock = "OB";
    public const string FunctionBlock = "FB";
    public const string Function = "FC";
    public const string Network = "Network";
    public const string Instruction = "Instruction";
    public const string Variable = "Variable";
    public const string GlobalDataBlock = "Global DB";
    public const string InstanceDataBlock = "Instance DB";
    public const string DataBlockMember = "DB Member";
    public const string UserDataType = "UDT";
    public const string UserDataTypeMember = "UDT Member";
    public const string DataType = "Data Type";
    public const string PlcTag = "PLC Tag";
    public const string IoAddress = "IO Address";
    public const string HardwareDevice = "Hardware Device";
}

public static class SemanticRelationshipType
{
    public const string Contains = "CONTAINS";
    public const string Calls = "CALLS";
    public const string Reads = "READS";
    public const string Writes = "WRITES";
    public const string Declares = "DECLARES";
    public const string HasType = "HAS_TYPE";
    public const string InstanceOf = "INSTANCE_OF";
    public const string ConnectedTo = "CONNECTED_TO";
    public const string ExecutesBefore = "EXECUTES_BEFORE";
    public const string ExecutesAfter = "EXECUTES_AFTER";
}

public sealed class SemanticGraphNode
{
    public SemanticGraphNode(string id, string kind, string name, IReadOnlyDictionary<string, string>? properties = null)
    {
        Id = RequireValue(id, nameof(id));
        Kind = RequireValue(kind, nameof(kind));
        Name = RequireValue(name, nameof(name));
        Properties = CopyProperties(properties);
    }

    public string Id { get; }

    public string Kind { get; }

    public string Name { get; }

    public IReadOnlyDictionary<string, string> Properties { get; }

    private static string RequireValue(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", parameterName);
        }

        return value;
    }

    private static IReadOnlyDictionary<string, string> CopyProperties(IReadOnlyDictionary<string, string>? properties)
    {
        return properties == null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : properties.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
    }
}

public sealed class SemanticGraphEdge
{
    public SemanticGraphEdge(
        string id,
        string fromNodeId,
        string toNodeId,
        string type,
        IReadOnlyDictionary<string, string>? properties = null)
    {
        Id = RequireValue(id, nameof(id));
        FromNodeId = RequireValue(fromNodeId, nameof(fromNodeId));
        ToNodeId = RequireValue(toNodeId, nameof(toNodeId));
        Type = RequireValue(type, nameof(type));
        Properties = CopyProperties(properties);
    }

    public string Id { get; }

    public string FromNodeId { get; }

    public string ToNodeId { get; }

    public string Type { get; }

    public IReadOnlyDictionary<string, string> Properties { get; }

    private static string RequireValue(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", parameterName);
        }

        return value;
    }

    private static IReadOnlyDictionary<string, string> CopyProperties(IReadOnlyDictionary<string, string>? properties)
    {
        return properties == null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : properties.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
    }
}

public sealed class SemanticPlcGraph
{
    private readonly Dictionary<string, SemanticGraphNode> nodes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SemanticGraphEdge> edges = new(StringComparer.Ordinal);

    public IReadOnlyList<SemanticGraphNode> Nodes => nodes.Values.OrderBy(node => node.Id, StringComparer.Ordinal).ToArray();

    public IReadOnlyList<SemanticGraphEdge> Edges => edges.Values.OrderBy(edge => edge.Id, StringComparer.Ordinal).ToArray();

    public void UpsertNode(SemanticGraphNode node)
    {
        if (node == null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        nodes[node.Id] = node;
    }

    public void UpsertEdge(SemanticGraphEdge edge)
    {
        if (edge == null)
        {
            throw new ArgumentNullException(nameof(edge));
        }

        edges[edge.Id] = edge;
    }

    public SemanticGraphNode GetNode(string id)
    {
        if (!nodes.TryGetValue(id, out var node))
        {
            throw new KeyNotFoundException($"Semantic graph node '{id}' was not found.");
        }

        return node;
    }

    public bool TryGetNode(string id, out SemanticGraphNode node)
    {
        return nodes.TryGetValue(id, out node!);
    }

    public IReadOnlyList<SemanticGraphNode> FindNodesByKind(string kind)
    {
        return nodes.Values
            .Where(node => string.Equals(node.Kind, kind, StringComparison.OrdinalIgnoreCase))
            .OrderBy(node => node.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}

public static class TiaXmlSemanticGraphImporter
{
    public static SemanticPlcGraph ImportExportRoot(string exportRoot)
    {
        if (string.IsNullOrWhiteSpace(exportRoot))
        {
            throw new ArgumentException("Export root is required.", nameof(exportRoot));
        }

        var graph = new SemanticPlcGraph();
        var projectName = Path.GetFileName(Path.GetFullPath(exportRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrWhiteSpace(projectName))
        {
            projectName = exportRoot;
        }

        graph.UpsertNode(new SemanticGraphNode(
            $"project:{projectName}",
            SemanticNodeKind.Project,
            projectName,
            new Dictionary<string, string> { ["exportRoot"] = exportRoot }));

        foreach (var component in LoadExportedComponents(exportRoot))
        {
            var filePath = Path.Combine(exportRoot, component.ExportedFile ?? string.Empty);
            if (string.IsNullOrWhiteSpace(component.ExportedFile) || !File.Exists(filePath))
            {
                continue;
            }

            var xml = File.ReadAllText(filePath);
            if (IsProgramBlockCategory(component.Category))
            {
                var blockId = BlockId(component.Name ?? string.Empty);
                ImportBlockXml(
                    xml,
                    new ProgramBlockComponent(
                        component.Name ?? string.Empty,
                        component.Category ?? string.Empty,
                        component.SourcePath ?? string.Empty,
                        component.ExportedFile ?? string.Empty),
                    graph);
                AddEdgeIfTargetExists(graph, $"project:{projectName}", blockId, SemanticRelationshipType.Contains);
                continue;
            }

            if (string.Equals(component.Category, "DB", StringComparison.OrdinalIgnoreCase))
            {
                var dbId = DbId(component.Name ?? string.Empty);
                ImportDbXml(xml, component.ExportedFile ?? string.Empty, component.SourcePath ?? string.Empty, graph);
                AddEdgeIfTargetExists(graph, $"project:{projectName}", dbId, SemanticRelationshipType.Contains);
                continue;
            }

            if (string.Equals(component.Category, "UDT", StringComparison.OrdinalIgnoreCase))
            {
                var udtId = UdtId(component.Name ?? string.Empty);
                ImportUdtXml(xml, component.ExportedFile ?? string.Empty, component.SourcePath ?? string.Empty, graph);
                AddEdgeIfTargetExists(graph, $"project:{projectName}", udtId, SemanticRelationshipType.Contains);
                continue;
            }

            if (string.Equals(component.Category, "Tags", StringComparison.OrdinalIgnoreCase))
            {
                ImportTagTableXml(xml, component.ExportedFile ?? string.Empty, component.SourcePath ?? string.Empty, graph);
            }
        }

        return graph;
    }

    public static void WriteSqlite(string exportRoot, string dbPath)
    {
        SqliteSemanticGraphStore.Save(dbPath, ImportExportRoot(exportRoot));
    }

    public static void ImportBlockXml(string xml, ProgramBlockComponent component, SemanticPlcGraph graph)
    {
        if (component == null)
        {
            throw new ArgumentNullException(nameof(component));
        }

        if (graph == null)
        {
            throw new ArgumentNullException(nameof(graph));
        }

        var blockId = BlockId(component.Name);
        graph.UpsertNode(new SemanticGraphNode(
            blockId,
            GetBlockNodeKind(component.Category),
            component.Name,
            new Dictionary<string, string>
            {
                ["folderPath"] = component.SourcePath,
                ["sourceFile"] = component.ExportedFile
            }));

        var parsed = ProgramSemanticReferenceBuilder.Parse(xml, component);
        ProgramNetworkRecord? previousNetwork = null;
        foreach (var network in parsed.Networks)
        {
            graph.UpsertNode(new SemanticGraphNode(
                network.Id,
                SemanticNodeKind.Network,
                $"{component.Name} Network {network.NetworkIndex}",
                new Dictionary<string, string>
                {
                    ["block"] = network.Block,
                    ["language"] = network.Language,
                    ["title"] = network.Title,
                    ["networkIndex"] = network.NetworkIndex.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["compileUnitId"] = network.CompileUnitId,
                    ["sourceFile"] = network.SourceFile
                }));
            AddEdge(graph, blockId, network.Id, SemanticRelationshipType.Contains, new Dictionary<string, string>
            {
                ["sourceFile"] = network.SourceFile
            });

            if (previousNetwork != null)
            {
                AddEdge(graph, previousNetwork.Id, network.Id, SemanticRelationshipType.ExecutesBefore);
                AddEdge(graph, network.Id, previousNetwork.Id, SemanticRelationshipType.ExecutesAfter);
            }

            previousNetwork = network;
        }

        var instructionSequence = 0;
        foreach (var reference in parsed.References)
        {
            if (string.Equals(reference.Access, "call", StringComparison.OrdinalIgnoreCase))
            {
                var calleeId = BlockId(reference.To);
                graph.UpsertNode(new SemanticGraphNode(
                    calleeId,
                    GetBlockNodeKind(reference.CalleeBlockType),
                    reference.To,
                    new Dictionary<string, string>
                    {
                        ["declaredByReference"] = "true"
                    }));

                instructionSequence++;
                var instructionId = $"instruction:{component.Name}:{reference.NetworkIndex}:call:{instructionSequence}";
                graph.UpsertNode(new SemanticGraphNode(
                    instructionId,
                    SemanticNodeKind.Instruction,
                    reference.To,
                    new Dictionary<string, string>
                    {
                        ["instructionKind"] = "CALL",
                        ["calleeBlockType"] = reference.CalleeBlockType,
                        ["networkIndex"] = reference.NetworkIndex.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ["networkTitle"] = reference.Title
                    }));
                AddEdge(graph, reference.From, instructionId, SemanticRelationshipType.Contains);
                AddEdge(graph, instructionId, calleeId, SemanticRelationshipType.Calls);
                AddEdge(graph, blockId, calleeId, SemanticRelationshipType.Calls, BuildReferenceProperties(reference));
                continue;
            }

            if (string.Equals(reference.TargetKind, "symbol", StringComparison.OrdinalIgnoreCase))
            {
                var symbolId = SymbolId(reference.To);
                graph.UpsertNode(new SemanticGraphNode(
                    symbolId,
                    SemanticNodeKind.Variable,
                    reference.To,
                    new Dictionary<string, string>
                    {
                        ["scope"] = reference.Scope
                    }));

                if (string.Equals(reference.Access, "read", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(reference.Access, "inout", StringComparison.OrdinalIgnoreCase))
                {
                    AddEdge(graph, blockId, symbolId, SemanticRelationshipType.Reads, BuildReferenceProperties(reference));
                    AddEdge(graph, reference.From, symbolId, SemanticRelationshipType.Reads, BuildReferenceProperties(reference));
                }

                if (string.Equals(reference.Access, "write", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(reference.Access, "inout", StringComparison.OrdinalIgnoreCase))
                {
                    AddEdge(graph, blockId, symbolId, SemanticRelationshipType.Writes, BuildReferenceProperties(reference));
                    AddEdge(graph, reference.From, symbolId, SemanticRelationshipType.Writes, BuildReferenceProperties(reference));
                }
            }
        }
    }

    public static void ImportUdtXml(string xml, string sourceFile, string sourcePath, SemanticPlcGraph graph)
    {
        if (graph == null)
        {
            throw new ArgumentNullException(nameof(graph));
        }

        var rows = UdtTypeTableBuilder.ParseRows(xml, sourceFile, sourcePath);
        foreach (var row in rows)
        {
            if (string.Equals(row.Kind, "Type", StringComparison.OrdinalIgnoreCase))
            {
                graph.UpsertNode(new SemanticGraphNode(
                    UdtId(row.Name),
                    SemanticNodeKind.UserDataType,
                    row.Name,
                    new Dictionary<string, string>
                    {
                        ["folderPath"] = row.SourcePath,
                        ["sourceFile"] = row.SourceFile
                    }));
                continue;
            }

            if (string.IsNullOrWhiteSpace(row.DataType))
            {
                continue;
            }

            var memberId = UdtMemberId(row.ParentType, row.Path);
            graph.UpsertNode(new SemanticGraphNode(
                memberId,
                SemanticNodeKind.UserDataTypeMember,
                row.Name,
                new Dictionary<string, string>
                {
                    ["path"] = row.Path,
                    ["folderPath"] = row.SourcePath,
                    ["sourceFile"] = row.SourceFile
                }));
            graph.UpsertNode(new SemanticGraphNode(TypeId(row.DataType), SemanticNodeKind.DataType, row.DataType));
            AddEdge(graph, UdtId(row.ParentType), memberId, SemanticRelationshipType.Contains);
            AddEdge(graph, memberId, TypeId(row.DataType), SemanticRelationshipType.HasType);
        }
    }

    public static void ImportTagTableXml(string xml, string sourceFile, string sourcePath, SemanticPlcGraph graph)
    {
        if (graph == null)
        {
            throw new ArgumentNullException(nameof(graph));
        }

        foreach (var row in TagTableBuilder.ParseRows(xml, sourceFile, sourcePath))
        {
            graph.UpsertNode(new SemanticGraphNode(
                row.Id,
                SemanticNodeKind.PlcTag,
                row.Name,
                new Dictionary<string, string>
                {
                    ["tagTable"] = row.TagTable,
                    ["logicalAddress"] = row.LogicalAddress,
                    ["folderPath"] = row.TagTableSourcePath,
                    ["sourceFile"] = row.SourceFile
                }));
            if (!string.IsNullOrWhiteSpace(row.DataType))
            {
                graph.UpsertNode(new SemanticGraphNode(TypeId(row.DataType), SemanticNodeKind.DataType, row.DataType));
                AddEdge(graph, row.Id, TypeId(row.DataType), SemanticRelationshipType.HasType);
            }

            if (!string.IsNullOrWhiteSpace(row.LogicalAddress))
            {
                var addressId = IoAddressId(row.LogicalAddress);
                graph.UpsertNode(new SemanticGraphNode(addressId, SemanticNodeKind.IoAddress, row.LogicalAddress));
                AddEdge(graph, row.Id, addressId, SemanticRelationshipType.ConnectedTo);
            }
        }
    }

    public static void ImportDbXml(string xml, string sourceFile, string sourcePath, SemanticPlcGraph graph)
    {
        if (xml == null)
        {
            throw new ArgumentNullException(nameof(xml));
        }

        if (graph == null)
        {
            throw new ArgumentNullException(nameof(graph));
        }

        XDocument document;
        try
        {
            document = XDocument.Parse(xml);
        }
        catch (XmlException)
        {
            return;
        }

        var dbRoot = document.Descendants().FirstOrDefault(IsDbElement);
        if (dbRoot == null)
        {
            return;
        }

        var name = GetDirectAttributeValue(dbRoot, "Name");
        if (string.IsNullOrWhiteSpace(name))
        {
            name = Path.GetFileNameWithoutExtension(sourceFile) ?? string.Empty;
        }

        var kind = dbRoot.Name.LocalName == "SW.Blocks.InstanceDB"
            ? SemanticNodeKind.InstanceDataBlock
            : SemanticNodeKind.GlobalDataBlock;
        var dbId = DbId(name);
        graph.UpsertNode(new SemanticGraphNode(
            dbId,
            kind,
            name,
            new Dictionary<string, string>
            {
                ["folderPath"] = sourcePath,
                ["sourceFile"] = sourceFile,
                ["dbType"] = dbRoot.Name.LocalName.Replace("SW.Blocks.", string.Empty)
            }));

        var instanceOfName = GetDirectAttributeValue(dbRoot, "InstanceOfName");
        if (!string.IsNullOrWhiteSpace(instanceOfName))
        {
            graph.UpsertNode(new SemanticGraphNode(BlockId(instanceOfName), SemanticNodeKind.FunctionBlock, instanceOfName));
            AddEdge(graph, dbId, BlockId(instanceOfName), SemanticRelationshipType.InstanceOf);
        }

        foreach (var section in GetSections(dbRoot))
        {
            foreach (var member in section.Elements().Where(element => element.Name.LocalName == "Member"))
            {
                ImportDbMember(member, name, dbId, string.Empty, graph);
            }
        }
    }

    private static void ImportDbMember(XElement member, string dbName, string parentNodeId, string parentPath, SemanticPlcGraph graph)
    {
        var name = ((string?)member.Attribute("Name"))?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var path = string.IsNullOrWhiteSpace(parentPath) ? name : $"{parentPath}.{name}";
        var dataType = NormalizeDataType(((string?)member.Attribute("Datatype"))?.Trim() ?? string.Empty);
        var memberId = DbMemberId(dbName, path);
        graph.UpsertNode(new SemanticGraphNode(
            memberId,
            SemanticNodeKind.DataBlockMember,
            name,
            new Dictionary<string, string>
            {
                ["path"] = path
            }));
        AddEdge(graph, parentNodeId, memberId, SemanticRelationshipType.Contains);

        if (!string.IsNullOrWhiteSpace(dataType))
        {
            graph.UpsertNode(new SemanticGraphNode(TypeId(dataType), SemanticNodeKind.DataType, dataType));
            AddEdge(graph, memberId, TypeId(dataType), SemanticRelationshipType.HasType);
        }

        var directSections = member
            .Elements()
            .FirstOrDefault(element => element.Name.LocalName == "Sections")
            ?.Elements()
            .Where(element => element.Name.LocalName == "Section")
            ?? Array.Empty<XElement>();

        foreach (var section in directSections)
        {
            foreach (var child in section.Elements().Where(element => element.Name.LocalName == "Member"))
            {
                ImportDbMember(child, dbName, memberId, path, graph);
            }
        }
    }

    private static IEnumerable<XElement> GetSections(XElement owner)
    {
        return owner
            .Elements()
            .FirstOrDefault(element => element.Name.LocalName == "AttributeList")
            ?.Elements()
            .FirstOrDefault(element => element.Name.LocalName == "Interface")
            ?.Elements()
            .FirstOrDefault(element => element.Name.LocalName == "Sections")
            ?.Elements()
            .Where(element => element.Name.LocalName == "Section")
            ?? Array.Empty<XElement>();
    }

    private static bool IsDbElement(XElement element)
    {
        return element.Name.LocalName == "SW.Blocks.GlobalDB" ||
            element.Name.LocalName == "SW.Blocks.InstanceDB" ||
            element.Name.LocalName == "SW.Blocks.DB" ||
            element.Name.LocalName == "SW.Blocks.ArrayDB";
    }

    private static IReadOnlyList<ComponentMetadataRecordDto> LoadExportedComponents(string exportRoot)
    {
        var metadataPath = Path.Combine(exportRoot, ExportMetadataWriter.MetadataFileName);
        if (!File.Exists(metadataPath))
        {
            throw new FileNotFoundException($"{ExportMetadataWriter.MetadataFileName} was not found.", metadataPath);
        }

        using var stream = File.OpenRead(metadataPath);
        var serializer = new DataContractJsonSerializer(typeof(ComponentMetadataDocumentDto));
        var document = (ComponentMetadataDocumentDto?)serializer.ReadObject(stream);
        return (document?.Components ?? new List<ComponentMetadataRecordDto>())
            .Where(component =>
                string.Equals(component.Status, "Exported", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(component.ExportedFile))
            .OrderBy(component => component.SourcePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool IsProgramBlockCategory(string? category)
    {
        return string.Equals(category, "OB", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(category, "FC", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(category, "FB", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetDirectAttributeValue(XElement element, string name)
    {
        return element
            .Elements()
            .FirstOrDefault(child => child.Name.LocalName == "AttributeList")
            ?.Elements()
            .FirstOrDefault(child => child.Name.LocalName == name)
            ?.Value
            .Trim() ?? string.Empty;
    }

    private static string NormalizeDataType(string value)
    {
        return value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"'
            ? value.Substring(1, value.Length - 2)
            : value;
    }

    private static IReadOnlyDictionary<string, string> BuildReferenceProperties(ProgramReferenceRecord reference)
    {
        var properties = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["networkId"] = reference.From,
            ["networkIndex"] = reference.NetworkIndex.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["sourceFile"] = reference.SourceFile
        };

        if (!string.IsNullOrWhiteSpace(reference.Parameter))
        {
            properties["parameter"] = reference.Parameter;
        }

        if (!string.IsNullOrWhiteSpace(reference.InstanceDb))
        {
            properties["instanceDb"] = reference.InstanceDb;
        }

        return properties;
    }

    private static void AddEdge(
        SemanticPlcGraph graph,
        string fromNodeId,
        string toNodeId,
        string type,
        IReadOnlyDictionary<string, string>? properties = null)
    {
        graph.UpsertEdge(new SemanticGraphEdge(EdgeId(fromNodeId, toNodeId, type, properties), fromNodeId, toNodeId, type, properties));
    }

    private static void AddEdgeIfTargetExists(
        SemanticPlcGraph graph,
        string fromNodeId,
        string toNodeId,
        string type,
        IReadOnlyDictionary<string, string>? properties = null)
    {
        if (graph.TryGetNode(toNodeId, out _))
        {
            AddEdge(graph, fromNodeId, toNodeId, type, properties);
        }
    }

    private static string EdgeId(string fromNodeId, string toNodeId, string type, IReadOnlyDictionary<string, string>? properties)
    {
        var qualifier = properties == null || properties.Count == 0
            ? string.Empty
            : ":" + string.Join(":", properties.OrderBy(item => item.Key, StringComparer.Ordinal).Select(item => $"{item.Key}={item.Value}"));
        return $"edge:{type}:{fromNodeId}->{toNodeId}{qualifier}";
    }

    private static string BlockId(string name)
    {
        return $"block:{name}";
    }

    private static string SymbolId(string name)
    {
        return $"symbol:{name}";
    }

    private static string UdtId(string name)
    {
        return $"udt:{name}";
    }

    private static string UdtMemberId(string udtName, string path)
    {
        return $"udt-member:{udtName}:{path}";
    }

    private static string DbId(string name)
    {
        return $"db:{name}";
    }

    private static string DbMemberId(string dbName, string path)
    {
        return $"db-member:{dbName}:{path}";
    }

    private static string TypeId(string name)
    {
        return $"type:{name}";
    }

    private static string IoAddressId(string address)
    {
        return $"io:{address}";
    }

    private static string GetBlockNodeKind(string blockKind)
    {
        if (string.Equals(blockKind, "OB", StringComparison.OrdinalIgnoreCase))
        {
            return SemanticNodeKind.OrganizationBlock;
        }

        if (string.Equals(blockKind, "FB", StringComparison.OrdinalIgnoreCase))
        {
            return SemanticNodeKind.FunctionBlock;
        }

        if (string.Equals(blockKind, "FC", StringComparison.OrdinalIgnoreCase))
        {
            return SemanticNodeKind.Function;
        }

        return SemanticNodeKind.Instruction;
    }
}

public sealed class PlcSemanticGraphQueries
{
    private readonly SemanticPlcGraph graph;

    public PlcSemanticGraphQueries(SemanticPlcGraph graph)
    {
        this.graph = graph ?? throw new ArgumentNullException(nameof(graph));
    }

    public IReadOnlyList<SemanticGraphNode> FindBlocksCalling(string blockName)
    {
        var targetId = $"block:{blockName}";
        return graph.Edges
            .Where(edge => edge.Type == SemanticRelationshipType.Calls && edge.ToNodeId == targetId)
            .Select(edge => graph.GetNode(edge.FromNodeId))
            .Where(IsBlockNode)
            .DistinctById()
            .OrderBy(node => node.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public IReadOnlyList<SemanticGraphNode> FindBlocksReading(string variableName)
    {
        return FindBlocksUsing($"symbol:{variableName}", SemanticRelationshipType.Reads);
    }

    public IReadOnlyList<SemanticGraphNode> FindBlocksWriting(string variableName)
    {
        return FindBlocksUsing($"symbol:{variableName}", SemanticRelationshipType.Writes);
    }

    public IReadOnlyList<SemanticGraphNode> FindDbMembersUsed()
    {
        var memberIds = new HashSet<string>(
            graph.Edges
                .Where(edge => edge.Type == SemanticRelationshipType.Reads || edge.Type == SemanticRelationshipType.Writes)
                .Select(edge => edge.ToNodeId),
            StringComparer.Ordinal);

        return graph.Nodes
            .Where(node => node.Kind == SemanticNodeKind.DataBlockMember && memberIds.Contains(node.Id))
            .OrderBy(node => node.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public IReadOnlyList<PlcCallGraphEdge> BuildCallGraph()
    {
        return graph.Edges
            .Where(edge => edge.Type == SemanticRelationshipType.Calls)
            .Select(edge => new PlcCallGraphEdge(graph.GetNode(edge.FromNodeId), graph.GetNode(edge.ToNodeId), edge))
            .Where(edge => IsBlockNode(edge.Caller))
            .OrderBy(edge => edge.Caller.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(edge => edge.Callee.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public IReadOnlyList<PlcDependencyGraphEdge> BuildDependencyGraph()
    {
        return graph.Edges
            .Where(edge =>
                edge.Type == SemanticRelationshipType.Calls ||
                edge.Type == SemanticRelationshipType.Reads ||
                edge.Type == SemanticRelationshipType.Writes ||
                edge.Type == SemanticRelationshipType.HasType ||
                edge.Type == SemanticRelationshipType.InstanceOf ||
                edge.Type == SemanticRelationshipType.ConnectedTo)
            .Select(edge => new PlcDependencyGraphEdge(graph.GetNode(edge.FromNodeId), graph.GetNode(edge.ToNodeId), edge.Type, edge))
            .OrderBy(edge => edge.Source.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(edge => edge.Relationship, StringComparer.OrdinalIgnoreCase)
            .ThenBy(edge => edge.Target.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private IReadOnlyList<SemanticGraphNode> FindBlocksUsing(string targetId, string relationship)
    {
        return graph.Edges
            .Where(edge => edge.Type == relationship && edge.ToNodeId == targetId)
            .Select(edge => graph.GetNode(edge.FromNodeId))
            .Where(IsBlockNode)
            .DistinctById()
            .OrderBy(node => node.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool IsBlockNode(SemanticGraphNode node)
    {
        return node.Kind == SemanticNodeKind.OrganizationBlock ||
            node.Kind == SemanticNodeKind.FunctionBlock ||
            node.Kind == SemanticNodeKind.Function;
    }
}

public sealed class PlcCallGraphEdge
{
    public PlcCallGraphEdge(SemanticGraphNode caller, SemanticGraphNode callee, SemanticGraphEdge edge)
    {
        Caller = caller;
        Callee = callee;
        Edge = edge;
    }

    public SemanticGraphNode Caller { get; }

    public SemanticGraphNode Callee { get; }

    public SemanticGraphEdge Edge { get; }
}

public sealed class PlcDependencyGraphEdge
{
    public PlcDependencyGraphEdge(SemanticGraphNode source, SemanticGraphNode target, string relationship, SemanticGraphEdge edge)
    {
        Source = source;
        Target = target;
        Relationship = relationship;
        Edge = edge;
    }

    public SemanticGraphNode Source { get; }

    public SemanticGraphNode Target { get; }

    public string Relationship { get; }

    public SemanticGraphEdge Edge { get; }
}

public static class PlcSemanticGraphSqliteSchema
{
    public const string CreateScript = """
        CREATE TABLE IF NOT EXISTS graph_nodes (
            id TEXT NOT NULL PRIMARY KEY,
            kind TEXT NOT NULL,
            name TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS graph_node_properties (
            node_id TEXT NOT NULL,
            name TEXT NOT NULL,
            value TEXT NOT NULL,
            PRIMARY KEY (node_id, name),
            FOREIGN KEY (node_id) REFERENCES graph_nodes(id) ON DELETE CASCADE
        );

        CREATE TABLE IF NOT EXISTS graph_edges (
            id TEXT NOT NULL PRIMARY KEY,
            from_node_id TEXT NOT NULL,
            to_node_id TEXT NOT NULL,
            type TEXT NOT NULL,
            FOREIGN KEY (from_node_id) REFERENCES graph_nodes(id) ON DELETE CASCADE,
            FOREIGN KEY (to_node_id) REFERENCES graph_nodes(id) ON DELETE CASCADE
        );

        CREATE TABLE IF NOT EXISTS graph_edge_properties (
            edge_id TEXT NOT NULL,
            name TEXT NOT NULL,
            value TEXT NOT NULL,
            PRIMARY KEY (edge_id, name),
            FOREIGN KEY (edge_id) REFERENCES graph_edges(id) ON DELETE CASCADE
        );

        CREATE INDEX IF NOT EXISTS ix_graph_nodes_kind ON graph_nodes(kind);
        CREATE INDEX IF NOT EXISTS ix_graph_nodes_name ON graph_nodes(name);
        CREATE INDEX IF NOT EXISTS ix_graph_edges_type ON graph_edges(type);
        CREATE INDEX IF NOT EXISTS ix_graph_edges_from ON graph_edges(from_node_id);
        CREATE INDEX IF NOT EXISTS ix_graph_edges_to ON graph_edges(to_node_id);
        """;
}

public static class SqliteSemanticGraphStore
{
    private static readonly object SQLiteInitializationGate = new();
    private static bool sqliteInitialized;

    public static void Save(string dbPath, SemanticPlcGraph graph)
    {
        if (string.IsNullOrWhiteSpace(dbPath))
        {
            throw new ArgumentException("SQLite path is required.", nameof(dbPath));
        }

        if (graph == null)
        {
            throw new ArgumentNullException(nameof(graph));
        }

        var directory = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        EnsureSqliteInitialized(dbPath);
        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();
        ExecuteNonQuery(connection, "PRAGMA foreign_keys = ON;");
        ExecuteNonQuery(connection, PlcSemanticGraphSqliteSchema.CreateScript);
        ExecuteNonQuery(connection, "DELETE FROM graph_edge_properties; DELETE FROM graph_edges; DELETE FROM graph_node_properties; DELETE FROM graph_nodes;");

        using var transaction = connection.BeginTransaction();
        foreach (var node in graph.Nodes)
        {
            ExecuteNonQuery(
                connection,
                "INSERT INTO graph_nodes (id, kind, name) VALUES ($id, $kind, $name);",
                transaction,
                ("$id", node.Id),
                ("$kind", node.Kind),
                ("$name", node.Name));
            foreach (var property in node.Properties.OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                ExecuteNonQuery(
                    connection,
                    "INSERT INTO graph_node_properties (node_id, name, value) VALUES ($node_id, $name, $value);",
                    transaction,
                    ("$node_id", node.Id),
                    ("$name", property.Key),
                    ("$value", property.Value));
            }
        }

        foreach (var edge in graph.Edges)
        {
            ExecuteNonQuery(
                connection,
                "INSERT INTO graph_edges (id, from_node_id, to_node_id, type) VALUES ($id, $from_node_id, $to_node_id, $type);",
                transaction,
                ("$id", edge.Id),
                ("$from_node_id", edge.FromNodeId),
                ("$to_node_id", edge.ToNodeId),
                ("$type", edge.Type));
            foreach (var property in edge.Properties.OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                ExecuteNonQuery(
                    connection,
                    "INSERT INTO graph_edge_properties (edge_id, name, value) VALUES ($edge_id, $name, $value);",
                    transaction,
                    ("$edge_id", edge.Id),
                    ("$name", property.Key),
                    ("$value", property.Value));
            }
        }

        transaction.Commit();
    }

    public static SemanticPlcGraph Load(string dbPath)
    {
        if (string.IsNullOrWhiteSpace(dbPath))
        {
            throw new ArgumentException("SQLite path is required.", nameof(dbPath));
        }

        var graph = new SemanticPlcGraph();
        EnsureSqliteInitialized(dbPath);
        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();

        var nodeProperties = LoadProperties(connection, "SELECT node_id, name, value FROM graph_node_properties ORDER BY node_id, name;");
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT id, kind, name FROM graph_nodes ORDER BY id;";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var id = reader.GetString(0);
                graph.UpsertNode(new SemanticGraphNode(
                    id,
                    reader.GetString(1),
                    reader.GetString(2),
                    nodeProperties.TryGetValue(id, out var properties) ? properties : null));
            }
        }

        var edgeProperties = LoadProperties(connection, "SELECT edge_id, name, value FROM graph_edge_properties ORDER BY edge_id, name;");
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT id, from_node_id, to_node_id, type FROM graph_edges ORDER BY id;";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var id = reader.GetString(0);
                graph.UpsertEdge(new SemanticGraphEdge(
                    id,
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    edgeProperties.TryGetValue(id, out var properties) ? properties : null));
            }
        }

        return graph;
    }

    private static Dictionary<string, Dictionary<string, string>> LoadProperties(SqliteConnection connection, string sql)
    {
        var results = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var ownerId = reader.GetString(0);
            if (!results.TryGetValue(ownerId, out var properties))
            {
                properties = new Dictionary<string, string>(StringComparer.Ordinal);
                results.Add(ownerId, properties);
            }

            properties[reader.GetString(1)] = reader.GetString(2);
        }

        return results;
    }

    private static void EnsureSqliteInitialized(string dbPath)
    {
        if (sqliteInitialized)
        {
            return;
        }

        lock (SQLiteInitializationGate)
        {
            if (sqliteInitialized)
            {
                return;
            }

            var dbDirectory = Path.GetDirectoryName(Path.GetFullPath(dbPath));
            if (string.IsNullOrWhiteSpace(dbDirectory))
            {
                throw new InvalidOperationException("SQLite database path must resolve to a directory.");
            }

            NativeSqliteRuntime.EnsureAvailable(Path.Combine(dbDirectory, "runtime", "sqlite", "win-x64"));
            SQLitePCL.Batteries_V2.Init();
            sqliteInitialized = true;
        }
    }

    private static void ExecuteNonQuery(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static void ExecuteNonQuery(
        SqliteConnection connection,
        string sql,
        SqliteTransaction transaction,
        params (string Name, string Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var parameter in parameters)
        {
            command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        }

        command.ExecuteNonQuery();
    }
}

internal static class NativeSqliteRuntime
{
    private const string NativeDllName = "e_sqlite3.dll";
    private const string NativeResourceSuffix = ".e_sqlite3.dll";

    public static void EnsureAvailable(string directory)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        Directory.CreateDirectory(directory);
        var destination = Path.Combine(directory, NativeDllName);
        ExtractNativeRuntime(destination);
        LoadNativeRuntime(destination);
    }

    private static void ExtractNativeRuntime(string destination)
    {
        var assembly = typeof(NativeSqliteRuntime).GetTypeInfo().Assembly;
        var resourceName = assembly
            .GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith(NativeResourceSuffix, StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(resourceName))
        {
            throw new FileNotFoundException("Embedded native SQLite runtime was not found.", NativeDllName);
        }

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            throw new FileNotFoundException("Embedded native SQLite runtime could not be opened.", NativeDllName);
        }

        var destinationInfo = new FileInfo(destination);
        if (destinationInfo.Exists && destinationInfo.Length == stream.Length)
        {
            return;
        }

        using var file = File.Create(destination);
        stream.CopyTo(file);
    }

    [SecuritySafeCritical]
    private static void LoadNativeRuntime(string path)
    {
        if (LoadLibrary(path) == IntPtr.Zero)
        {
            throw new InvalidOperationException($"Unable to load native SQLite runtime from '{path}'. Win32 error {Marshal.GetLastWin32Error()}.");
        }
    }

    [SecurityCritical]
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadLibrary(string lpFileName);
}

public sealed class SemanticPlcModelWriteResult
{
    public SemanticPlcModelWriteResult(string sqliteFilePath, string schemaFilePath, string agentGuideFilePath)
    {
        SqliteFilePath = sqliteFilePath;
        SchemaFilePath = schemaFilePath;
        AgentGuideFilePath = agentGuideFilePath;
    }

    public string SqliteFilePath { get; }

    public string SchemaFilePath { get; }

    public string AgentGuideFilePath { get; }
}

public static class SemanticPlcModelWriter
{
    public const string ModelFolderName = "model";
    public const string SqliteFileName = "plc-graph.sqlite";
    public const string SchemaFileName = "schema.sql";
    public const string AgentGuideFileName = "AGENT_SQLITE_GUIDE.md";

    public static SemanticPlcModelWriteResult Write(string exportRoot)
    {
        if (string.IsNullOrWhiteSpace(exportRoot))
        {
            throw new ArgumentException("Export root is required.", nameof(exportRoot));
        }

        var modelFolder = Path.Combine(exportRoot, ModelFolderName);
        Directory.CreateDirectory(modelFolder);

        var result = GetExpectedResult(exportRoot);
        File.WriteAllText(result.SchemaFilePath, PlcSemanticGraphSqliteSchema.CreateScript);

        File.WriteAllText(result.AgentGuideFilePath, SemanticPlcGraphAgentGuide.Content);

        TiaXmlSemanticGraphImporter.WriteSqlite(exportRoot, result.SqliteFilePath);

        return result;
    }

    public static SemanticPlcModelWriteResult GetExpectedResult(string exportRoot)
    {
        if (string.IsNullOrWhiteSpace(exportRoot))
        {
            throw new ArgumentException("Export root is required.", nameof(exportRoot));
        }

        var modelFolder = Path.Combine(exportRoot, ModelFolderName);
        return new SemanticPlcModelWriteResult(
            Path.Combine(modelFolder, SqliteFileName),
            Path.Combine(modelFolder, SchemaFileName),
            Path.Combine(modelFolder, AgentGuideFileName));
    }
}

public static class SemanticPlcGraphAgentGuide
{
    public const string Content = """
        # Agent Guide For `plc-graph.sqlite`

        Use `plc-graph.sqlite` as the semantic source of truth for PLC analysis. Treat the original TIA XML as an import/export format. If this SQLite file is the only artifact available, answer questions by querying the graph tables and citing node names, relationship types, `sourceFile`, and `networkIndex` values when available.

        ## Mental Model

        The database is a property graph stored in generic SQLite tables:

        - `graph_nodes`: semantic engineering objects.
        - `graph_node_properties`: metadata for nodes.
        - `graph_edges`: semantic relationships between nodes.
        - `graph_edge_properties`: metadata for relationships.

        Important node kinds include `Project`, `PLC Device`, `Hardware Device`, `OB`, `FB`, `FC`, `Network`, `Instruction`, `Variable`, `Global DB`, `Instance DB`, `DB Member`, `UDT`, `UDT Member`, `Data Type`, `PLC Tag`, and `IO Address`.

        Important relationship types include `CONTAINS`, `CALLS`, `READS`, `WRITES`, `DECLARES`, `HAS_TYPE`, `INSTANCE_OF`, `CONNECTED_TO`, `EXECUTES_BEFORE`, and `EXECUTES_AFTER`.

        Folder paths are metadata only. Do not infer engineering semantics from folder structure. Prefer relationships in `graph_edges`.

        ## Schema

        ```sql
        SELECT id, kind, name
        FROM graph_nodes;

        SELECT node_id, name, value
        FROM graph_node_properties;

        SELECT id, from_node_id, to_node_id, type
        FROM graph_edges;

        SELECT edge_id, name, value
        FROM graph_edge_properties;
        ```

        ## First Inspection Queries

        Count node kinds:

        ```sql
        SELECT kind, COUNT(*) AS count
        FROM graph_nodes
        GROUP BY kind
        ORDER BY kind;
        ```

        Count relationship types:

        ```sql
        SELECT type, COUNT(*) AS count
        FROM graph_edges
        GROUP BY type
        ORDER BY type;
        ```

        List blocks:

        ```sql
        SELECT id, kind, name
        FROM graph_nodes
        WHERE kind IN ('OB', 'FB', 'FC')
        ORDER BY kind, name;
        ```

        List networks by block:

        ```sql
        SELECT
          block.kind AS block_kind,
          block.name AS block_name,
          network.name AS network_name,
          idx.value AS network_index,
          src.value AS source_file
        FROM graph_edges e
        JOIN graph_nodes block ON block.id = e.from_node_id
        JOIN graph_nodes network ON network.id = e.to_node_id
        LEFT JOIN graph_node_properties idx
          ON idx.node_id = network.id AND idx.name = 'networkIndex'
        LEFT JOIN graph_node_properties src
          ON src.node_id = network.id AND src.name = 'sourceFile'
        WHERE e.type = 'CONTAINS'
          AND block.kind IN ('OB', 'FB', 'FC')
          AND network.kind = 'Network'
        ORDER BY block.name, CAST(idx.value AS INTEGER);
        ```

        ## Call Graph

        Which blocks call which blocks:

        ```sql
        SELECT
          caller.kind AS caller_kind,
          caller.name AS caller,
          callee.kind AS callee_kind,
          callee.name AS callee,
          sf.value AS source_file,
          ni.value AS network_index
        FROM graph_edges e
        JOIN graph_nodes caller ON caller.id = e.from_node_id
        JOIN graph_nodes callee ON callee.id = e.to_node_id
        LEFT JOIN graph_edge_properties sf
          ON sf.edge_id = e.id AND sf.name = 'sourceFile'
        LEFT JOIN graph_edge_properties ni
          ON ni.edge_id = e.id AND ni.name = 'networkIndex'
        WHERE e.type = 'CALLS'
          AND caller.kind IN ('OB', 'FB', 'FC')
          AND callee.kind IN ('OB', 'FB', 'FC')
        ORDER BY caller.name, CAST(ni.value AS INTEGER), callee.name;
        ```

        Who calls a specific block:

        ```sql
        SELECT caller.kind, caller.name, sf.value AS source_file, ni.value AS network_index
        FROM graph_edges e
        JOIN graph_nodes caller ON caller.id = e.from_node_id
        JOIN graph_nodes callee ON callee.id = e.to_node_id
        LEFT JOIN graph_edge_properties sf
          ON sf.edge_id = e.id AND sf.name = 'sourceFile'
        LEFT JOIN graph_edge_properties ni
          ON ni.edge_id = e.id AND ni.name = 'networkIndex'
        WHERE e.type = 'CALLS'
          AND callee.name = 'REPLACE_WITH_BLOCK_NAME'
        ORDER BY caller.name, CAST(ni.value AS INTEGER);
        ```

        ## Variable Usage

        Which blocks read or write variables:

        ```sql
        SELECT
          e.type AS access,
          block.kind AS block_kind,
          block.name AS block_name,
          variable.kind AS variable_kind,
          variable.name AS variable_name,
          sf.value AS source_file,
          ni.value AS network_index
        FROM graph_edges e
        JOIN graph_nodes block ON block.id = e.from_node_id
        JOIN graph_nodes variable ON variable.id = e.to_node_id
        LEFT JOIN graph_edge_properties sf
          ON sf.edge_id = e.id AND sf.name = 'sourceFile'
        LEFT JOIN graph_edge_properties ni
          ON ni.edge_id = e.id AND ni.name = 'networkIndex'
        WHERE e.type IN ('READS', 'WRITES')
          AND block.kind IN ('OB', 'FB', 'FC', 'Network')
        ORDER BY variable.name, access, block.name, CAST(ni.value AS INTEGER);
        ```

        Reads and writes for one symbol:

        ```sql
        SELECT
          e.type AS access,
          block.kind AS block_kind,
          block.name AS block_name,
          target.name AS symbol,
          sf.value AS source_file,
          ni.value AS network_index
        FROM graph_edges e
        JOIN graph_nodes block ON block.id = e.from_node_id
        JOIN graph_nodes target ON target.id = e.to_node_id
        LEFT JOIN graph_edge_properties sf
          ON sf.edge_id = e.id AND sf.name = 'sourceFile'
        LEFT JOIN graph_edge_properties ni
          ON ni.edge_id = e.id AND ni.name = 'networkIndex'
        WHERE e.type IN ('READS', 'WRITES')
          AND target.name = 'REPLACE_WITH_SYMBOL_NAME'
        ORDER BY access, block.name, CAST(ni.value AS INTEGER);
        ```

        ## Tags And IO

        PLC tags connected to IO addresses:

        ```sql
        SELECT
          tag.name AS tag_name,
          address.name AS io_address,
          dtype.name AS data_type
        FROM graph_edges connected
        JOIN graph_nodes tag ON tag.id = connected.from_node_id
        JOIN graph_nodes address ON address.id = connected.to_node_id
        LEFT JOIN graph_edges typed
          ON typed.from_node_id = tag.id AND typed.type = 'HAS_TYPE'
        LEFT JOIN graph_nodes dtype ON dtype.id = typed.to_node_id
        WHERE connected.type = 'CONNECTED_TO'
          AND tag.kind = 'PLC Tag'
          AND address.kind = 'IO Address'
        ORDER BY address.name, tag.name;
        ```

        ## Data Blocks, UDTs, And Types

        DB members and data types:

        ```sql
        SELECT
          db.kind AS db_kind,
          db.name AS db_name,
          member.name AS member_name,
          dtype.name AS data_type
        FROM graph_edges contains
        JOIN graph_nodes db ON db.id = contains.from_node_id
        JOIN graph_nodes member ON member.id = contains.to_node_id
        LEFT JOIN graph_edges typed
          ON typed.from_node_id = member.id AND typed.type = 'HAS_TYPE'
        LEFT JOIN graph_nodes dtype ON dtype.id = typed.to_node_id
        WHERE contains.type = 'CONTAINS'
          AND db.kind IN ('Global DB', 'Instance DB')
          AND member.kind = 'DB Member'
        ORDER BY db.name, member.name;
        ```

        Instance DB to FB relationship:

        ```sql
        SELECT db.name AS instance_db, fb.name AS function_block
        FROM graph_edges e
        JOIN graph_nodes db ON db.id = e.from_node_id
        JOIN graph_nodes fb ON fb.id = e.to_node_id
        WHERE e.type = 'INSTANCE_OF'
        ORDER BY db.name;
        ```

        UDT members and data types:

        ```sql
        SELECT
          udt.name AS udt_name,
          member.name AS member_name,
          dtype.name AS data_type
        FROM graph_edges contains
        JOIN graph_nodes udt ON udt.id = contains.from_node_id
        JOIN graph_nodes member ON member.id = contains.to_node_id
        LEFT JOIN graph_edges typed
          ON typed.from_node_id = member.id AND typed.type = 'HAS_TYPE'
        LEFT JOIN graph_nodes dtype ON dtype.id = typed.to_node_id
        WHERE contains.type = 'CONTAINS'
          AND udt.kind = 'UDT'
          AND member.kind = 'UDT Member'
        ORDER BY udt.name, member.name;
        ```

        ## Execution Order

        Network execution order:

        ```sql
        SELECT
          before.name AS executes_before,
          after.name AS executes_after,
          before_idx.value AS before_index,
          after_idx.value AS after_index
        FROM graph_edges e
        JOIN graph_nodes before ON before.id = e.from_node_id
        JOIN graph_nodes after ON after.id = e.to_node_id
        LEFT JOIN graph_node_properties before_idx
          ON before_idx.node_id = before.id AND before_idx.name = 'networkIndex'
        LEFT JOIN graph_node_properties after_idx
          ON after_idx.node_id = after.id AND after_idx.name = 'networkIndex'
        WHERE e.type = 'EXECUTES_BEFORE'
        ORDER BY before.name, CAST(before_idx.value AS INTEGER);
        ```

        ## How To Validate Against Real Code

        When the SQLite file is your only source, validate internally:

        1. Start from counts by node kind and relationship type.
        2. Use call graph queries to find execution entry points and called blocks.
        3. Use `READS` and `WRITES` edges to identify variable dependencies.
        4. Use `sourceFile` and `networkIndex` edge properties to locate where the semantic fact came from in the original export.
        5. Prefer block-level `CALLS`, `READS`, and `WRITES` for high-level reasoning.
        6. Prefer network-level relationships when diagnosing a specific rung/network.

        If a relationship is absent, do not assume the underlying PLC code never uses that object. Report it as "not represented in the current semantic graph" and ask for a refreshed model if precision matters.
        """;
}

internal static class SemanticGraphEnumerableExtensions
{
    public static IEnumerable<SemanticGraphNode> DistinctById(this IEnumerable<SemanticGraphNode> nodes)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in nodes)
        {
            if (seen.Add(node.Id))
            {
                yield return node;
            }
        }
    }
}
