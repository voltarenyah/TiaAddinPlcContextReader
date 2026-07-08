using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace PlcSourceExporter.Core;

public sealed class ProgramBlockProfileResult
{
    public ProgramBlockProfileResult(string blockProfilesFilePath, string optimizationHintsFilePath, int blockProfileCount, int optimizationHintCount)
    {
        BlockProfilesFilePath = blockProfilesFilePath;
        OptimizationHintsFilePath = optimizationHintsFilePath;
        BlockProfileCount = blockProfileCount;
        OptimizationHintCount = optimizationHintCount;
    }

    public string BlockProfilesFilePath { get; }

    public string OptimizationHintsFilePath { get; }

    public int BlockProfileCount { get; }

    public int OptimizationHintCount { get; }
}

public sealed class BlockInterfaceSummary
{
    public BlockInterfaceSummary(int inputCount, int outputCount, int inOutCount, int staticCount, int tempCount, int constantCount, int retValCount)
    {
        InputCount = inputCount;
        OutputCount = outputCount;
        InOutCount = inOutCount;
        StaticCount = staticCount;
        TempCount = tempCount;
        ConstantCount = constantCount;
        RetValCount = retValCount;
    }

    public int InputCount { get; }

    public int OutputCount { get; }

    public int InOutCount { get; }

    public int StaticCount { get; }

    public int TempCount { get; }

    public int ConstantCount { get; }

    public int RetValCount { get; }
}

public sealed class ProgramBlockProfileRecord
{
    public ProgramBlockProfileRecord(
        string block,
        string blockKind,
        string language,
        string sourceFile,
        int networkCount,
        int callCount,
        BlockInterfaceSummary interfaceSummary,
        IReadOnlyList<string> keyReads,
        IReadOnlyList<string> keyWrites,
        IReadOnlyList<string> keyCalls,
        IReadOnlyList<string> statefulElements,
        IReadOnlyList<string> instanceDbs)
    {
        Block = block;
        BlockKind = blockKind;
        Language = language;
        SourceFile = sourceFile;
        NetworkCount = networkCount;
        CallCount = callCount;
        InterfaceSummary = interfaceSummary;
        KeyReads = keyReads;
        KeyWrites = keyWrites;
        KeyCalls = keyCalls;
        StatefulElements = statefulElements;
        InstanceDbs = instanceDbs;
    }

    public string Block { get; }

    public string BlockKind { get; }

    public string Language { get; }

    public string SourceFile { get; }

    public int NetworkCount { get; }

    public int CallCount { get; }

    public BlockInterfaceSummary InterfaceSummary { get; }

    public IReadOnlyList<string> KeyReads { get; }

    public IReadOnlyList<string> KeyWrites { get; }

    public IReadOnlyList<string> KeyCalls { get; }

    public IReadOnlyList<string> StatefulElements { get; }

    public IReadOnlyList<string> InstanceDbs { get; }
}

public sealed class ProgramOptimizationHintRecord
{
    public ProgramOptimizationHintRecord(
        string kind,
        string block,
        string target,
        string severity,
        string evidence,
        string sourceFile,
        int? networkIndex = null)
    {
        Kind = kind;
        Block = block;
        Target = target;
        Severity = severity;
        Evidence = evidence;
        SourceFile = sourceFile;
        NetworkIndex = networkIndex;
    }

    public string Kind { get; }

    public string Block { get; }

    public string Target { get; }

    public string Severity { get; }

    public string Evidence { get; }

    public string SourceFile { get; }

    public int? NetworkIndex { get; }
}

public static class ProgramBlockProfileBuilder
{
    public const string BlockProfilesFileName = "block-profiles.jsonl";
    public const string OptimizationHintsFileName = "optimization-hints.jsonl";
    private const string SchemaVersion = "1.0";

    public static ProgramBlockProfileResult Write(string exportRoot, DateTimeOffset generatedUtc)
    {
        if (string.IsNullOrWhiteSpace(exportRoot))
        {
            throw new ArgumentException("Export root is required.", nameof(exportRoot));
        }

        var components = ProgramBlockComponentCatalog.LoadExportedProgramBlocks(exportRoot);
        var profileRecords = new List<ProgramBlockProfileRecord>();
        var optimizationRecords = new List<ProgramOptimizationHintRecord>();
        var blockContexts = new List<BlockContext>();

        foreach (var component in components)
        {
            var filePath = Path.Combine(exportRoot, component.ExportedFile);
            if (!File.Exists(filePath))
            {
                continue;
            }

            var xml = File.ReadAllText(filePath);
            var parsed = ProgramSemanticReferenceBuilder.Parse(xml, component);
            var interfaceSummary = ParseInterfaceSummary(xml);
            var interfaceMembers = ParseInterfaceMembers(xml);
            var statefulElements = DetectStatefulElements(parsed.References);
            var instanceDbs = parsed.References
                .Where(reference => string.Equals(reference.TargetKind, "block", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(reference.Access, "call", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(reference.InstanceDb))
                .Select(reference => reference.InstanceDb)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            profileRecords.Add(new ProgramBlockProfileRecord(
                component.Name,
                component.Category,
                parsed.Networks.FirstOrDefault()?.Language ?? GetBlockLanguage(xml),
                component.ExportedFile,
                parsed.Networks.Count,
                parsed.References.Count(reference => string.Equals(reference.TargetKind, "block", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(reference.Access, "call", StringComparison.OrdinalIgnoreCase)),
                interfaceSummary,
                DistinctTargets(parsed.References, "read"),
                DistinctTargets(parsed.References, "write"),
                DistinctCallTargets(parsed.References),
                statefulElements,
                instanceDbs));

            blockContexts.Add(new BlockContext(component, parsed, interfaceMembers));
        }

        optimizationRecords.AddRange(BuildMultiWriterHints(blockContexts));
        optimizationRecords.AddRange(BuildNeverReadHints(blockContexts));
        optimizationRecords.AddRange(BuildNeverWrittenOutputHints(blockContexts));
        optimizationRecords.AddRange(BuildRepeatedCallHints(blockContexts));
        optimizationRecords.AddRange(BuildScanOrderDependencyHints(blockContexts));

        var blockProfilesFilePath = Path.Combine(exportRoot, BlockProfilesFileName);
        File.WriteAllText(blockProfilesFilePath, SerializeProfiles(profileRecords, generatedUtc, exportRoot));

        var optimizationHintsFilePath = Path.Combine(exportRoot, OptimizationHintsFileName);
        File.WriteAllText(optimizationHintsFilePath, SerializeHints(optimizationRecords, generatedUtc, exportRoot));

        return new ProgramBlockProfileResult(blockProfilesFilePath, optimizationHintsFilePath, profileRecords.Count, optimizationRecords.Count);
    }

    private static IEnumerable<ProgramOptimizationHintRecord> BuildMultiWriterHints(IReadOnlyList<BlockContext> blockContexts)
    {
        var writers = blockContexts
            .SelectMany(context => context.Parsed.References.Where(reference =>
                string.Equals(reference.TargetKind, "symbol", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(reference.Access, "write", StringComparison.OrdinalIgnoreCase)))
            .GroupBy(reference => reference.To, StringComparer.OrdinalIgnoreCase);

        foreach (var group in writers)
        {
            var distinctBlocks = group.Select(reference => reference.Block).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (distinctBlocks.Length < 2)
            {
                continue;
            }

            var evidence = string.Join(", ", distinctBlocks.OrderBy(value => value, StringComparer.OrdinalIgnoreCase));
            yield return new ProgramOptimizationHintRecord(
                "multi_writer",
                distinctBlocks[0],
                group.Key,
                "warning",
                $"Written by multiple blocks: {evidence}",
                blockContexts.First(context => string.Equals(context.Component.Name, distinctBlocks[0], StringComparison.OrdinalIgnoreCase)).Component.ExportedFile);
        }
    }

    private static IEnumerable<ProgramOptimizationHintRecord> BuildNeverReadHints(IReadOnlyList<BlockContext> blockContexts)
    {
        var contextsByBlock = blockContexts.ToDictionary(context => context.Component.Name, StringComparer.OrdinalIgnoreCase);
        var reads = blockContexts
            .SelectMany(context => context.Parsed.References.Where(reference =>
                string.Equals(reference.TargetKind, "symbol", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(reference.Access, "read", StringComparison.OrdinalIgnoreCase)))
            .Select(reference => reference.To)
            .ToArray();
        var readSet = new HashSet<string>(reads, StringComparer.OrdinalIgnoreCase);

        var writes = blockContexts
            .SelectMany(context => context.Parsed.References.Where(reference =>
                string.Equals(reference.TargetKind, "symbol", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(reference.Access, "write", StringComparison.OrdinalIgnoreCase)));

        foreach (var group in writes.GroupBy(reference => reference.To, StringComparer.OrdinalIgnoreCase))
        {
            if (readSet.Contains(group.Key))
            {
                continue;
            }

            var writer = group.First();
            if (string.Equals(writer.Scope, "LocalVariable", StringComparison.OrdinalIgnoreCase) &&
                contextsByBlock.TryGetValue(writer.Block, out var blockContext) &&
                blockContext.InterfaceSummaryMembers.Outputs.Contains(group.Key, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            yield return new ProgramOptimizationHintRecord(
                "never_read_symbol",
                writer.Block,
                group.Key,
                "info",
                "Symbol is written but no read reference was exported.",
                writer.SourceFile,
                writer.NetworkIndex);
        }
    }

    private static IEnumerable<ProgramOptimizationHintRecord> BuildNeverWrittenOutputHints(IReadOnlyList<BlockContext> blockContexts)
    {
        foreach (var context in blockContexts)
        {
            var writtenSymbols = context.Parsed.References
                .Where(reference =>
                    string.Equals(reference.TargetKind, "symbol", StringComparison.OrdinalIgnoreCase) &&
                    (string.Equals(reference.Access, "write", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(reference.Access, "inout", StringComparison.OrdinalIgnoreCase)))
                .Select(reference => reference.To)
                .ToArray();
            var writtenSet = new HashSet<string>(writtenSymbols, StringComparer.OrdinalIgnoreCase);

            foreach (var outputMember in context.InterfaceSummaryMembers.Outputs)
            {
                if (writtenSet.Contains(outputMember))
                {
                    continue;
                }

                yield return new ProgramOptimizationHintRecord(
                    "never_written_output",
                    context.Component.Name,
                    outputMember,
                    "info",
                    "Output member was not written by any exported network.",
                    context.Component.ExportedFile);
            }
        }
    }

    private static IEnumerable<ProgramOptimizationHintRecord> BuildRepeatedCallHints(IReadOnlyList<BlockContext> blockContexts)
    {
        foreach (var context in blockContexts)
        {
            foreach (var group in context.Parsed.References
                         .Where(reference =>
                             string.Equals(reference.TargetKind, "block", StringComparison.OrdinalIgnoreCase) &&
                             string.Equals(reference.Access, "call", StringComparison.OrdinalIgnoreCase))
                         .GroupBy(reference => reference.To, StringComparer.OrdinalIgnoreCase))
            {
                var networkIndexes = group.Select(reference => reference.NetworkIndex).Distinct().OrderBy(value => value).ToArray();
                if (networkIndexes.Length < 2)
                {
                    continue;
                }

                yield return new ProgramOptimizationHintRecord(
                    "repeated_call_target",
                    context.Component.Name,
                    group.Key,
                    "info",
                    $"Called from networks {string.Join(", ", networkIndexes)}.",
                    context.Component.ExportedFile);
            }
        }
    }

    private static IEnumerable<ProgramOptimizationHintRecord> BuildScanOrderDependencyHints(IReadOnlyList<BlockContext> blockContexts)
    {
        foreach (var context in blockContexts)
        {
            var readsBySymbol = context.Parsed.References
                .Where(reference =>
                    string.Equals(reference.TargetKind, "symbol", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(reference.Access, "read", StringComparison.OrdinalIgnoreCase))
                .GroupBy(reference => reference.To, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Select(reference => reference.NetworkIndex).Distinct().OrderBy(value => value).ToArray(), StringComparer.OrdinalIgnoreCase);

            foreach (var writeGroup in context.Parsed.References
                         .Where(reference =>
                             string.Equals(reference.TargetKind, "symbol", StringComparison.OrdinalIgnoreCase) &&
                             string.Equals(reference.Access, "write", StringComparison.OrdinalIgnoreCase))
                         .GroupBy(reference => reference.To, StringComparer.OrdinalIgnoreCase))
            {
                if (!readsBySymbol.TryGetValue(writeGroup.Key, out var readNetworks))
                {
                    continue;
                }

                var writeNetworks = writeGroup.Select(reference => reference.NetworkIndex).Distinct().OrderBy(value => value).ToArray();
                if (writeNetworks.Length == 0 || readNetworks.Length == 0)
                {
                    continue;
                }

                if (writeNetworks.Min() < readNetworks.Max())
                {
                    yield return new ProgramOptimizationHintRecord(
                        "scan_order_dependency",
                        context.Component.Name,
                        writeGroup.Key,
                        "warning",
                        $"Written in network {writeNetworks.Min()} and read later in network {readNetworks.Max()}.",
                        context.Component.ExportedFile);
                }
            }
        }
    }

    private static BlockInterfaceSummary ParseInterfaceSummary(string xml)
    {
        try
        {
            var document = XDocument.Parse(xml);
            var block = document.Descendants().FirstOrDefault(IsProgramBlockElement);
            if (block == null)
            {
                return new BlockInterfaceSummary(0, 0, 0, 0, 0, 0, 0);
            }

            var sectionCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var sections = block
                .Descendants()
                .FirstOrDefault(element => element.Name.LocalName == "Interface")?
                .Descendants()
                .Where(element => element.Name.LocalName == "Section")
                .ToArray() ?? Array.Empty<XElement>();

            foreach (var section in sections)
            {
                var sectionName = ((string?)section.Attribute("Name"))?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(sectionName))
                {
                    continue;
                }

                var memberCount = section.Elements().Count(element => element.Name.LocalName == "Member");
                if (!sectionCounts.ContainsKey(sectionName))
                {
                    sectionCounts[sectionName] = 0;
                }

                sectionCounts[sectionName] += memberCount;
            }

            return new BlockInterfaceSummary(
                GetCount(sectionCounts, "Input"),
                GetCount(sectionCounts, "Output"),
                GetCount(sectionCounts, "InOut"),
                GetCount(sectionCounts, "Static"),
                GetCount(sectionCounts, "Temp"),
                GetCount(sectionCounts, "Constant"),
                GetCount(sectionCounts, "RetVal"));
        }
        catch (XmlException)
        {
            return new BlockInterfaceSummary(0, 0, 0, 0, 0, 0, 0);
        }
    }

    private static InterfaceMembers ParseInterfaceMembers(string xml)
    {
        try
        {
            var document = XDocument.Parse(xml);
            var block = document.Descendants().FirstOrDefault(IsProgramBlockElement);
            if (block == null)
            {
                return new InterfaceMembers(Array.Empty<string>(), Array.Empty<string>());
            }

            var sections = block
                .Descendants()
                .FirstOrDefault(element => element.Name.LocalName == "Interface")?
                .Descendants()
                .Where(element => element.Name.LocalName == "Section")
                .ToArray() ?? Array.Empty<XElement>();

            var inputs = GetMemberNames(sections, "Input");
            var outputs = GetMemberNames(sections, "Output");
            return new InterfaceMembers(inputs, outputs);
        }
        catch (XmlException)
        {
            return new InterfaceMembers(Array.Empty<string>(), Array.Empty<string>());
        }
    }

    private static IReadOnlyList<string> DetectStatefulElements(IReadOnlyList<ProgramReferenceRecord> references)
    {
        var elements = new List<string>();

        foreach (var reference in references.Where(item =>
                     string.Equals(item.TargetKind, "block", StringComparison.OrdinalIgnoreCase) &&
                     string.Equals(item.Access, "call", StringComparison.OrdinalIgnoreCase)))
        {
            var blockType = reference.CalleeBlockType;
            if (IsTimerCall(reference.To, blockType))
            {
                elements.Add($"timer:{reference.To}");
            }

            if (IsCounterCall(reference.To, blockType))
            {
                elements.Add($"counter:{reference.To}");
            }

            if (!string.IsNullOrWhiteSpace(reference.InstanceDb))
            {
                elements.Add($"instanceDb:{reference.InstanceDb}");
            }
        }

        var symbolAccesses = references
            .Where(reference => string.Equals(reference.TargetKind, "symbol", StringComparison.OrdinalIgnoreCase))
            .GroupBy(reference => reference.To, StringComparer.OrdinalIgnoreCase);
        foreach (var group in symbolAccesses)
        {
            var hasRead = group.Any(reference => string.Equals(reference.Access, "read", StringComparison.OrdinalIgnoreCase));
            var hasWrite = group.Any(reference => string.Equals(reference.Access, "write", StringComparison.OrdinalIgnoreCase));
            if (hasRead && hasWrite)
            {
                elements.Add($"latch:{group.Key}");
            }
        }

        return elements.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static bool IsTimerCall(string calleeName, string blockType)
    {
        return MatchesAny(calleeName, "TON", "TOF", "TP") || MatchesAny(blockType, "TON", "TOF", "TP");
    }

    private static bool IsCounterCall(string calleeName, string blockType)
    {
        return MatchesAny(calleeName, "CTU", "CTD", "CTUD") || MatchesAny(blockType, "CTU", "CTD", "CTUD");
    }

    private static bool MatchesAny(string value, params string[] candidates)
    {
        return candidates.Any(candidate => string.Equals(value, candidate, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetBlockLanguage(string xml)
    {
        try
        {
            var document = XDocument.Parse(xml);
            var block = document.Descendants().FirstOrDefault(IsProgramBlockElement);
            if (block == null)
            {
                return string.Empty;
            }

            return block
                .Elements()
                .FirstOrDefault(child => child.Name.LocalName == "AttributeList")
                ?.Elements()
                .FirstOrDefault(child => child.Name.LocalName == "ProgrammingLanguage")
                ?.Value
                .Trim() ?? string.Empty;
        }
        catch (XmlException)
        {
            return string.Empty;
        }
    }

    private static bool IsProgramBlockElement(XElement element)
    {
        return element.Name.LocalName == "SW.Blocks.OB" ||
            element.Name.LocalName == "SW.Blocks.FC" ||
            element.Name.LocalName == "SW.Blocks.FB";
    }

    private static IReadOnlyList<string> DistinctTargets(IReadOnlyList<ProgramReferenceRecord> references, string access)
    {
        return references
            .Where(reference =>
                string.Equals(reference.TargetKind, "symbol", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(reference.Access, access, StringComparison.OrdinalIgnoreCase))
            .Select(reference => reference.To)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> DistinctCallTargets(IReadOnlyList<ProgramReferenceRecord> references)
    {
        return references
            .Where(reference =>
                string.Equals(reference.TargetKind, "block", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(reference.Access, "call", StringComparison.OrdinalIgnoreCase))
            .Select(reference => reference.To)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> GetMemberNames(IEnumerable<XElement> sections, string sectionName)
    {
        return sections
            .Where(section => string.Equals(((string?)section.Attribute("Name"))?.Trim(), sectionName, StringComparison.OrdinalIgnoreCase))
            .SelectMany(section => section.Elements().Where(element => element.Name.LocalName == "Member"))
            .Select(member => ((string?)member.Attribute("Name"))?.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static int GetCount(IReadOnlyDictionary<string, int> counts, string key)
    {
        return counts.TryGetValue(key, out var value) ? value : 0;
    }

    private static string SerializeProfiles(IReadOnlyList<ProgramBlockProfileRecord> profiles, DateTimeOffset generatedUtc, string exportRoot)
    {
        var builder = new StringBuilder();
        foreach (var profile in profiles)
        {
            builder.Append('{');
            WriteProperty(builder, "schemaVersion", SchemaVersion, appendComma: true);
            WriteProperty(builder, "generatedUtc", generatedUtc.ToString("O"), appendComma: true);
            WriteProperty(builder, "exportRoot", exportRoot, appendComma: true);
            WriteProperty(builder, "block", profile.Block, appendComma: true);
            WriteProperty(builder, "blockKind", profile.BlockKind, appendComma: true);
            WriteProperty(builder, "language", profile.Language, appendComma: true);
            WriteProperty(builder, "sourceFile", profile.SourceFile, appendComma: true);
            WriteProperty(builder, "networkCount", profile.NetworkCount, appendComma: true);
            WriteProperty(builder, "callCount", profile.CallCount, appendComma: true);
            builder.Append('"').Append("interfaceSummary").Append("\":{");
            WriteProperty(builder, "inputCount", profile.InterfaceSummary.InputCount, appendComma: true);
            WriteProperty(builder, "outputCount", profile.InterfaceSummary.OutputCount, appendComma: true);
            WriteProperty(builder, "inOutCount", profile.InterfaceSummary.InOutCount, appendComma: true);
            WriteProperty(builder, "staticCount", profile.InterfaceSummary.StaticCount, appendComma: true);
            WriteProperty(builder, "tempCount", profile.InterfaceSummary.TempCount, appendComma: true);
            WriteProperty(builder, "constantCount", profile.InterfaceSummary.ConstantCount, appendComma: true);
            WriteProperty(builder, "retValCount", profile.InterfaceSummary.RetValCount, appendComma: false);
            builder.Append("},");
            WriteArrayProperty(builder, "keyReads", profile.KeyReads, appendComma: true);
            WriteArrayProperty(builder, "keyWrites", profile.KeyWrites, appendComma: true);
            WriteArrayProperty(builder, "keyCalls", profile.KeyCalls, appendComma: true);
            WriteArrayProperty(builder, "statefulElements", profile.StatefulElements, appendComma: true);
            WriteArrayProperty(builder, "instanceDbs", profile.InstanceDbs, appendComma: false);
            builder.AppendLine("}");
        }

        return builder.ToString();
    }

    private static string SerializeHints(IReadOnlyList<ProgramOptimizationHintRecord> hints, DateTimeOffset generatedUtc, string exportRoot)
    {
        var builder = new StringBuilder();
        foreach (var hint in hints)
        {
            builder.Append('{');
            WriteProperty(builder, "schemaVersion", SchemaVersion, appendComma: true);
            WriteProperty(builder, "generatedUtc", generatedUtc.ToString("O"), appendComma: true);
            WriteProperty(builder, "exportRoot", exportRoot, appendComma: true);
            WriteProperty(builder, "kind", hint.Kind, appendComma: true);
            WriteProperty(builder, "block", hint.Block, appendComma: true);
            WriteProperty(builder, "target", hint.Target, appendComma: true);
            WriteProperty(builder, "severity", hint.Severity, appendComma: true);
            WriteProperty(builder, "evidence", hint.Evidence, appendComma: true);
            WriteProperty(builder, "sourceFile", hint.SourceFile, appendComma: true);
            WriteProperty(builder, "networkIndex", hint.NetworkIndex ?? 0, appendComma: false);
            builder.AppendLine("}");
        }

        return builder.ToString();
    }

    private static void WriteProperty(StringBuilder builder, string name, string value, bool appendComma)
    {
        builder.Append('"').Append(Escape(name)).Append("\":\"").Append(Escape(value)).Append('"');
        if (appendComma)
        {
            builder.Append(',');
        }
    }

    private static void WriteProperty(StringBuilder builder, string name, int value, bool appendComma)
    {
        builder.Append('"')
            .Append(Escape(name))
            .Append("\":")
            .Append(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (appendComma)
        {
            builder.Append(',');
        }
    }

    private static void WriteArrayProperty(StringBuilder builder, string name, IReadOnlyList<string> values, bool appendComma)
    {
        builder.Append('"').Append(Escape(name)).Append("\":[");
        for (var index = 0; index < values.Count; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            builder.Append('"').Append(Escape(values[index])).Append('"');
        }

        builder.Append(']');
        if (appendComma)
        {
            builder.Append(',');
        }
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

    private sealed class BlockContext
    {
        public BlockContext(ProgramBlockComponent component, ProgramSemanticParseResult parsed, InterfaceMembers interfaceSummaryMembers)
        {
            Component = component;
            Parsed = parsed;
            InterfaceSummaryMembers = interfaceSummaryMembers;
        }

        public ProgramBlockComponent Component { get; }

        public ProgramSemanticParseResult Parsed { get; }

        public InterfaceMembers InterfaceSummaryMembers { get; }
    }

    private sealed class InterfaceMembers
    {
        public InterfaceMembers(IReadOnlyList<string> inputs, IReadOnlyList<string> outputs)
        {
            Inputs = inputs;
            Outputs = outputs;
        }

        public IReadOnlyList<string> Inputs { get; }

        public IReadOnlyList<string> Outputs { get; }
    }
}
