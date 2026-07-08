using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace PlcSourceExporter.Core;

public sealed class ProgramBlockLogicYamlResult
{
    public ProgramBlockLogicYamlResult(string filePath, int blockCount, int networkCount)
    {
        FilePath = filePath;
        BlockCount = blockCount;
        NetworkCount = networkCount;
    }

    public string FilePath { get; }

    public int BlockCount { get; }

    public int NetworkCount { get; }
}

public static class ProgramBlockLogicYamlWriter
{
    public const string FolderName = "translate";
    public const string FileName = "program-blocks.yaml";
    private const string SchemaVersion = "1.0";

    public static ProgramBlockLogicYamlResult Write(string exportRoot, DateTimeOffset generatedUtc)
    {
        if (string.IsNullOrWhiteSpace(exportRoot))
        {
            throw new ArgumentException("Export root is required.", nameof(exportRoot));
        }

        var components = ProgramBlockComponentCatalog.LoadExportedProgramBlocks(exportRoot);
        var blocks = new List<LogicBlock>();
        foreach (var component in components)
        {
            var filePath = Path.Combine(exportRoot, component.ExportedFile);
            if (!File.Exists(filePath))
            {
                continue;
            }

            blocks.Add(ParseBlock(File.ReadAllText(filePath), component));
        }

        var translateFolder = Path.Combine(exportRoot, FolderName);
        Directory.CreateDirectory(translateFolder);
        var yamlPath = Path.Combine(translateFolder, FileName);
        File.WriteAllText(yamlPath, Serialize(exportRoot, generatedUtc, blocks));
        return new ProgramBlockLogicYamlResult(
            yamlPath,
            blocks.Count,
            blocks.Sum(block => block.Networks.Count));
    }

    private static LogicBlock ParseBlock(string xml, ProgramBlockComponent component)
    {
        XDocument document;
        try
        {
            document = XDocument.Parse(xml);
        }
        catch (XmlException)
        {
            return new LogicBlock(
                component.Name,
                component.Category,
                component.ExportedFile,
                string.Empty,
                new[]
                {
                    LogicNetwork.Untranslated(1, string.Empty, "Network 1", string.Empty, "Malformed XML source.")
                });
        }

        var blockLanguage = GetBlockLanguage(document);
        var networks = document
            .Descendants()
            .Where(element => element.Name.LocalName == "SW.Blocks.CompileUnit")
            .Select((element, index) => ParseNetwork(element, index + 1, blockLanguage))
            .ToArray();

        return new LogicBlock(
            component.Name,
            component.Category,
            component.ExportedFile,
            blockLanguage,
            networks);
    }

    private static LogicNetwork ParseNetwork(XElement compileUnit, int networkIndex, string blockLanguage)
    {
        var compileUnitId = ((string?)compileUnit.Attribute("ID"))?.Trim() ?? string.Empty;
        var title = GetPreferredMultilingualText(compileUnit, "Title");
        if (string.IsNullOrWhiteSpace(title))
        {
            title = $"Network {networkIndex}";
        }

        var language = GetDirectAttributeValue(compileUnit, "ProgrammingLanguage");
        if (string.IsNullOrWhiteSpace(language))
        {
            language = blockLanguage;
        }

        if (IsLadOrFbd(language))
        {
            var flgNet = compileUnit.Descendants().FirstOrDefault(element => element.Name.LocalName == "FlgNet");
            if (flgNet == null)
            {
                return LogicNetwork.Untranslated(
                    networkIndex,
                    compileUnitId,
                    title,
                    language,
                    "Unsupported network language or missing FlgNet source.");
            }

            return TranslateFlgNet(flgNet, networkIndex, compileUnitId, title, language);
        }

        if (string.Equals(language, "SCL", StringComparison.OrdinalIgnoreCase))
        {
            var source = GetCompactSclSource(compileUnit);
            if (!string.IsNullOrWhiteSpace(source))
            {
                return new LogicNetwork(
                    networkIndex,
                    compileUnitId,
                    title,
                    language,
                    "scl",
                    "exact",
                    SplitSclStatements(source),
                    Array.Empty<string>());
            }
        }

        return LogicNetwork.Untranslated(
            networkIndex,
            compileUnitId,
            title,
            language,
            "Unsupported network language or missing FlgNet source.");
    }

    private static LogicNetwork TranslateFlgNet(
        XElement flgNet,
        int networkIndex,
        string compileUnitId,
        string title,
        string language)
    {
        var context = FlgNetContext.Create(flgNet);
        var statements = new List<string>();
        var notes = new List<string>();

        foreach (var coil in context.Parts.Values.Where(part => IsCoil(part.Name)).OrderBy(part => part.Order))
        {
            var target = context.GetPinAccess(coil.Uid, "operand");
            if (string.IsNullOrWhiteSpace(target))
            {
                notes.Add("Skipped a coil because its operand could not be resolved by symbol name.");
                continue;
            }

            var input = context.EvaluateInput(coil.Uid, "in", notes);
            if (string.IsNullOrWhiteSpace(input))
            {
                notes.Add($"Skipped assignment to {target} because the coil input could not be resolved.");
                continue;
            }

            statements.Add($"{target} := {input};");
        }

        foreach (var call in context.Calls.OrderBy(call => call.Order))
        {
            var callStatement = context.TryBuildCallStatement(call, notes);
            if (!string.IsNullOrWhiteSpace(callStatement))
            {
                statements.Add(callStatement);
            }
        }

        if (statements.Count == 0)
        {
            if (notes.Count == 0)
            {
                notes.Add("No supported coils or resolvable call statements were found.");
            }

            return new LogicNetwork(
                networkIndex,
                compileUnitId,
                title,
                language,
                "scl-like",
                "untranslated",
                Array.Empty<string>(),
                notes);
        }

        return new LogicNetwork(
            networkIndex,
            compileUnitId,
            title,
            language,
            "scl-like",
            notes.Count == 0 ? "exact" : "partial",
            statements,
            notes);
    }

    private static bool IsLadOrFbd(string language)
    {
        return string.Equals(language, "LAD", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(language, "FBD", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(language, "F_LAD", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(language, "F_FBD", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCoil(string partName)
    {
        return MatchesAny(partName, "Coil", "SCoil", "RCoil");
    }

    private static bool MatchesAny(string value, params string[] candidates)
    {
        return candidates.Any(candidate => string.Equals(value, candidate, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetCompactSclSource(XElement compileUnit)
    {
        var networkSource = compileUnit.Descendants().FirstOrDefault(element => element.Name.LocalName == "NetworkSource");
        if (networkSource == null)
        {
            return string.Empty;
        }

        var text = networkSource
            .Descendants()
            .Where(element => element.Name.LocalName == "Text" || element.Name.LocalName == "Source")
            .Select(element => element.Value)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        if (string.IsNullOrWhiteSpace(text))
        {
            text = networkSource.Value;
        }

        return NormalizeWhitespace(text);
    }

    private static IReadOnlyList<string> SplitSclStatements(string source)
    {
        return string.IsNullOrWhiteSpace(source)
            ? Array.Empty<string>()
            : new[] { source.Trim() };
    }

    private static string NormalizeWhitespace(string value)
    {
        var builder = new StringBuilder();
        var pendingSpace = false;
        foreach (var character in value)
        {
            if (char.IsWhiteSpace(character))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(character);
        }

        return builder.ToString().Trim();
    }

    private static string GetBlockLanguage(XDocument document)
    {
        var block = document.Descendants().FirstOrDefault(IsProgramBlockElement);
        return block == null ? string.Empty : GetDirectAttributeValue(block, "ProgrammingLanguage");
    }

    private static bool IsProgramBlockElement(XElement element)
    {
        return element.Name.LocalName == "SW.Blocks.OB" ||
            element.Name.LocalName == "SW.Blocks.FC" ||
            element.Name.LocalName == "SW.Blocks.FB";
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
                Culture = GetDirectAttributeValue(item, "Culture"),
                Text = GetDirectAttributeValue(item, "Text")
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Text))
            .ToArray();

        return items.FirstOrDefault(item => string.Equals(item.Culture, "en-GB", StringComparison.OrdinalIgnoreCase))?.Text
            ?? items.FirstOrDefault(item => string.Equals(item.Culture, "en-US", StringComparison.OrdinalIgnoreCase))?.Text
            ?? items.FirstOrDefault()?.Text
            ?? string.Empty;
    }

    private static string Serialize(string exportRoot, DateTimeOffset generatedUtc, IReadOnlyList<LogicBlock> blocks)
    {
        var builder = new StringBuilder();
        builder.Append("schemaVersion: ").AppendLine(Quote(SchemaVersion));
        builder.Append("generatedUtc: ").AppendLine(Quote(generatedUtc.UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture)));
        builder.Append("exportRoot: ").AppendLine(Quote(exportRoot));
        builder.AppendLine("blocks:");
        foreach (var block in blocks)
        {
            builder.Append("  - name: ").AppendLine(Quote(block.Name));
            builder.Append("    kind: ").AppendLine(Quote(block.Kind));
            builder.Append("    sourceFile: ").AppendLine(Quote(block.SourceFile));
            builder.Append("    programmingLanguage: ").AppendLine(Quote(block.ProgrammingLanguage));
            builder.AppendLine("    networks:");
            foreach (var network in block.Networks)
            {
                builder.Append("      - networkIndex: ").AppendLine(network.NetworkIndex.ToString(System.Globalization.CultureInfo.InvariantCulture));
                builder.Append("        compileUnitId: ").AppendLine(Quote(network.CompileUnitId));
                builder.Append("        title: ").AppendLine(Quote(network.Title));
                builder.Append("        language: ").AppendLine(Quote(network.Language));
                builder.AppendLine("        translation:");
                builder.Append("          language: ").AppendLine(Quote(network.TranslationLanguage));
                builder.Append("          confidence: ").AppendLine(Quote(network.Confidence));
                builder.AppendLine("          statements:");
                WriteStringArray(builder, network.Statements, "            ");
                builder.AppendLine("          notes:");
                WriteStringArray(builder, network.Notes, "            ");
            }
        }

        return builder.ToString();
    }

    private static void WriteStringArray(StringBuilder builder, IReadOnlyList<string> values, string indent)
    {
        if (values.Count == 0)
        {
            builder.Append(indent).AppendLine("[]");
            return;
        }

        foreach (var value in values)
        {
            builder.Append(indent).Append("- ").AppendLine(Quote(value));
        }
    }

    private static string Quote(string value)
    {
        var builder = new StringBuilder(value.Length + 2);
        builder.Append('"');
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

        builder.Append('"');
        return builder.ToString();
    }

    private sealed class FlgNetContext
    {
        private readonly Dictionary<PartPin, PartPin> _inputSources;
        private readonly Dictionary<PartPin, string> _pinAccesses;
        private readonly Dictionary<PartPin, string> _powerInputs;
        private readonly Dictionary<PartPin, string> _expressionCache = new();
        private readonly HashSet<PartPin> _visiting = new();

        private FlgNetContext(
            IReadOnlyDictionary<string, PartNode> parts,
            IReadOnlyDictionary<string, AccessNode> accesses,
            IReadOnlyList<CallNode> calls,
            Dictionary<PartPin, PartPin> inputSources,
            Dictionary<PartPin, string> pinAccesses,
            Dictionary<PartPin, string> powerInputs)
        {
            Parts = parts;
            Accesses = accesses;
            Calls = calls;
            _inputSources = inputSources;
            _pinAccesses = pinAccesses;
            _powerInputs = powerInputs;
        }

        public IReadOnlyDictionary<string, PartNode> Parts { get; }

        public IReadOnlyDictionary<string, AccessNode> Accesses { get; }

        public IReadOnlyList<CallNode> Calls { get; }

        public static FlgNetContext Create(XElement flgNet)
        {
            var orderedElements = flgNet
                .Descendants()
                .Where(element => element.Name.LocalName == "Access" || element.Name.LocalName == "Part" || element.Name.LocalName == "Call")
                .Select((element, index) => new { Element = element, Order = index })
                .ToArray();

            var accesses = orderedElements
                .Where(item => item.Element.Name.LocalName == "Access")
                .Select(item => new AccessNode(
                    GetUid(item.Element),
                    GetAccessValue(item.Element),
                    ((string?)item.Element.Attribute("Scope"))?.Trim() ?? string.Empty))
                .Where(access => !string.IsNullOrWhiteSpace(access.Uid))
                .GroupBy(access => access.Uid, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            var parts = orderedElements
                .Where(item => item.Element.Name.LocalName == "Part")
                .Select(item => new PartNode(
                    GetUid(item.Element),
                    ((string?)item.Element.Attribute("Name"))?.Trim() ?? string.Empty,
                    item.Order,
                    item.Element.Descendants()
                        .Where(element => element.Name.LocalName == "Negated")
                        .Select(element => ((string?)element.Attribute("Name"))?.Trim() ?? string.Empty)
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .ToArray()))
                .Where(part => !string.IsNullOrWhiteSpace(part.Uid))
                .GroupBy(part => part.Uid, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            var calls = orderedElements
                .Where(item => item.Element.Name.LocalName == "Call")
                .Select(item => CreateCallNode(item.Element, item.Order))
                .Where(call => !string.IsNullOrWhiteSpace(call.Uid))
                .ToArray();

            var inputSources = new Dictionary<PartPin, PartPin>();
            var pinAccesses = new Dictionary<PartPin, string>();
            var powerInputs = new Dictionary<PartPin, string>();

            foreach (var wire in flgNet.Descendants().Where(element => element.Name.LocalName == "Wire"))
            {
                var nameCons = wire
                    .Descendants()
                    .Where(element => element.Name.LocalName == "NameCon")
                    .Select(element => new PartPin(
                        ((string?)element.Attribute("UId"))?.Trim() ?? string.Empty,
                        ((string?)element.Attribute("Name"))?.Trim() ?? string.Empty))
                    .Where(pin => !string.IsNullOrWhiteSpace(pin.PartUid) && !string.IsNullOrWhiteSpace(pin.PinName))
                    .ToArray();

                var accessValues = wire
                    .Descendants()
                    .Where(element => element.Name.LocalName == "IdentCon")
                    .Select(element => ((string?)element.Attribute("UId"))?.Trim() ?? string.Empty)
                    .Where(uid => !string.IsNullOrWhiteSpace(uid) && accesses.ContainsKey(uid))
                    .Select(uid => accesses[uid].Value)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .ToArray();

                if (accessValues.Length == 1)
                {
                    foreach (var pin in nameCons)
                    {
                        pinAccesses[pin] = accessValues[0];
                    }
                }

                if (wire.Descendants().Any(element => element.Name.LocalName == "Powerrail"))
                {
                    foreach (var pin in nameCons.Where(pin => IsInputPin(pin.PinName)))
                    {
                        powerInputs[pin] = "TRUE";
                    }
                }

                var sourcePins = nameCons.Where(pin => IsOutputPin(pin.PinName)).ToArray();
                var targetPins = nameCons.Where(pin => IsInputPin(pin.PinName)).ToArray();
                foreach (var target in targetPins)
                {
                    foreach (var source in sourcePins)
                    {
                        inputSources[target] = source;
                    }
                }
            }

            return new FlgNetContext(parts, accesses, calls, inputSources, pinAccesses, powerInputs);
        }

        public string GetPinAccess(string partUid, string pinName)
        {
            return _pinAccesses.TryGetValue(new PartPin(partUid, pinName), out var access) ? access : string.Empty;
        }

        public string EvaluateInput(string partUid, string pinName, List<string> notes)
        {
            var inputPin = new PartPin(partUid, pinName);
            if (_powerInputs.TryGetValue(inputPin, out var powerExpression))
            {
                return powerExpression;
            }

            if (!_inputSources.TryGetValue(inputPin, out var source))
            {
                return string.Empty;
            }

            return EvaluateOutput(source.PartUid, source.PinName, notes);
        }

        public string TryBuildCallStatement(CallNode call, List<string> notes)
        {
            if (string.IsNullOrWhiteSpace(call.Name))
            {
                notes.Add("Skipped a call because its block or instance name could not be resolved.");
                return string.Empty;
            }

            var bindings = new List<string>();
            foreach (var parameter in call.Parameters)
            {
                var value = GetPinAccess(call.Uid, parameter.Name);
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                var separator = string.Equals(parameter.Section, "Output", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(parameter.Section, "InOut", StringComparison.OrdinalIgnoreCase)
                    ? " => "
                    : " := ";
                bindings.Add($"{parameter.Name}{separator}{value}");
            }

            if (bindings.Count == 0)
            {
                notes.Add($"Skipped call {call.Name} because no parameters could be resolved by symbol name.");
                return string.Empty;
            }

            var enable = EvaluateInput(call.Uid, "en", notes);
            var prefix = string.IsNullOrWhiteSpace(enable) || string.Equals(enable, "TRUE", StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : $"IF {enable} THEN ";
            var suffix = string.IsNullOrWhiteSpace(prefix) ? string.Empty : " END_IF;";
            return $"{prefix}{call.Name}({string.Join(", ", bindings)});{suffix}";
        }

        private string EvaluateOutput(string partUid, string pinName, List<string> notes)
        {
            var outputPin = new PartPin(partUid, pinName);
            if (_expressionCache.TryGetValue(outputPin, out var cached))
            {
                return cached;
            }

            if (_visiting.Contains(outputPin))
            {
                notes.Add("Skipped cyclic LAD/FBD wiring while translating a network expression.");
                return string.Empty;
            }

            _visiting.Add(outputPin);
            try
            {
                if (!Parts.TryGetValue(partUid, out var part))
                {
                    notes.Add("Skipped a wire endpoint because its part was not found.");
                    return string.Empty;
                }

                var expression = EvaluatePartOutput(part, notes);
                _expressionCache[outputPin] = expression;
                return expression;
            }
            finally
            {
                _visiting.Remove(outputPin);
            }
        }

        private string EvaluatePartOutput(PartNode part, List<string> notes)
        {
            if (string.Equals(part.Name, "Contact", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(part.Name, "ContactF", StringComparison.OrdinalIgnoreCase))
            {
                var operand = GetPinAccess(part.Uid, "operand");
                if (string.IsNullOrWhiteSpace(operand))
                {
                    notes.Add("Skipped a contact because its operand could not be resolved by symbol name.");
                    return string.Empty;
                }

                var condition = part.IsNegated("operand") ? $"NOT {operand}" : operand;
                var upstream = EvaluateInput(part.Uid, "in", notes);
                return And(upstream, condition);
            }

            if (IsCompare(part.Name))
            {
                var left = GetPinAccess(part.Uid, "in1");
                var right = GetPinAccess(part.Uid, "in2");
                if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
                {
                    notes.Add($"Skipped compare part {part.Name} because one side could not be resolved.");
                    return string.Empty;
                }

                var compare = $"({left} {GetCompareOperator(part.Name)} {right})";
                var upstream = EvaluateInput(part.Uid, "pre", notes);
                if (string.IsNullOrWhiteSpace(upstream))
                {
                    upstream = EvaluateInput(part.Uid, "in", notes);
                }

                return And(upstream, compare);
            }

            if (string.Equals(part.Name, "Not", StringComparison.OrdinalIgnoreCase))
            {
                var input = EvaluateInput(part.Uid, "in", notes);
                return string.IsNullOrWhiteSpace(input) ? string.Empty : $"NOT ({input})";
            }

            if (string.Equals(part.Name, "O", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(part.Name, "Or", StringComparison.OrdinalIgnoreCase))
            {
                var inputs = _inputSources.Keys
                    .Where(pin => string.Equals(pin.PartUid, part.Uid, StringComparison.OrdinalIgnoreCase) && IsInputPin(pin.PinName))
                    .Concat(_powerInputs.Keys.Where(pin => string.Equals(pin.PartUid, part.Uid, StringComparison.OrdinalIgnoreCase) && IsInputPin(pin.PinName)))
                    .Select(pin => pin.PinName)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(GetPinSortKey)
                    .Select(pinName => EvaluateInput(part.Uid, pinName, notes))
                    .Where(value => !string.IsNullOrWhiteSpace(value) && !string.Equals(value, "TRUE", StringComparison.OrdinalIgnoreCase))
                    .ToArray();

                if (inputs.Length == 0)
                {
                    return string.Empty;
                }

                return inputs.Length == 1 ? inputs[0] : $"({string.Join(" OR ", inputs)})";
            }

            if (string.Equals(part.Name, "A", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(part.Name, "And", StringComparison.OrdinalIgnoreCase))
            {
                var inputs = _inputSources.Keys
                    .Where(pin => string.Equals(pin.PartUid, part.Uid, StringComparison.OrdinalIgnoreCase) && IsInputPin(pin.PinName))
                    .Select(pin => pin.PinName)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(GetPinSortKey)
                    .Select(pinName => EvaluateInput(part.Uid, pinName, notes))
                    .Where(value => !string.IsNullOrWhiteSpace(value) && !string.Equals(value, "TRUE", StringComparison.OrdinalIgnoreCase))
                    .ToArray();

                if (inputs.Length == 0)
                {
                    return string.Empty;
                }

                return inputs.Length == 1 ? inputs[0] : $"({string.Join(" AND ", inputs)})";
            }

            notes.Add($"Unsupported LAD/FBD part '{part.Name}'.");
            return string.Empty;
        }

        private static bool IsInputPin(string pinName)
        {
            if (pinName.StartsWith("in", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return MatchesAny(pinName, "pre", "en", "s", "s1", "r", "r1");
        }

        private static bool IsOutputPin(string pinName)
        {
            return MatchesAny(pinName, "out", "eno", "q");
        }

        private static int GetPinSortKey(string pinName)
        {
            if (pinName.Length > 2 &&
                pinName.StartsWith("in", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(pinName.Substring(2), out var index))
            {
                return index;
            }

            return int.MaxValue;
        }

        private static string And(string upstream, string condition)
        {
            if (string.IsNullOrWhiteSpace(condition))
            {
                return string.Empty;
            }

            if (string.IsNullOrWhiteSpace(upstream) || string.Equals(upstream, "TRUE", StringComparison.OrdinalIgnoreCase))
            {
                return condition;
            }

            return $"({upstream} AND {condition})";
        }

        private static bool IsCompare(string partName)
        {
            return MatchesAny(partName, "Eq", "Ne", "Lt", "Le", "Gt", "Ge");
        }

        private static string GetCompareOperator(string partName)
        {
            if (string.Equals(partName, "Eq", StringComparison.OrdinalIgnoreCase))
            {
                return "=";
            }

            if (string.Equals(partName, "Ne", StringComparison.OrdinalIgnoreCase))
            {
                return "<>";
            }

            if (string.Equals(partName, "Lt", StringComparison.OrdinalIgnoreCase))
            {
                return "<";
            }

            if (string.Equals(partName, "Le", StringComparison.OrdinalIgnoreCase))
            {
                return "<=";
            }

            if (string.Equals(partName, "Gt", StringComparison.OrdinalIgnoreCase))
            {
                return ">";
            }

            return ">=";
        }

        private static CallNode CreateCallNode(XElement call, int order)
        {
            var callInfo = call.Elements().FirstOrDefault(element => element.Name.LocalName == "CallInfo");
            var name = ((string?)callInfo?.Attribute("Name"))?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name) && callInfo != null)
            {
                name = GetInstanceDb(callInfo);
            }

            var parameters = callInfo?
                .Elements()
                .Where(element => element.Name.LocalName == "Parameter")
                .Select(parameter => new CallParameter(
                    ((string?)parameter.Attribute("Name"))?.Trim() ?? string.Empty,
                    ((string?)parameter.Attribute("Section"))?.Trim() ?? string.Empty))
                .Where(parameter => !string.IsNullOrWhiteSpace(parameter.Name))
                .ToArray() ?? Array.Empty<CallParameter>();

            return new CallNode(GetUid(call), name, order, parameters);
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

        private static string GetAccessValue(XElement access)
        {
            var symbol = access
                .Descendants()
                .Where(element => element.Name.LocalName == "Symbol")
                .Take(1)
                .Descendants()
                .Where(element => element.Name.LocalName == "Component")
                .Select(element => ((string?)element.Attribute("Name"))?.Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToArray();
            if (symbol.Length > 0)
            {
                return string.Join(".", symbol);
            }

            return access
                .Descendants()
                .FirstOrDefault(element => element.Name.LocalName == "ConstantValue")
                ?.Value
                .Trim() ?? string.Empty;
        }

        private static string GetUid(XElement element)
        {
            return ((string?)element.Attribute("UId"))?.Trim() ?? string.Empty;
        }
    }

    private sealed class LogicBlock
    {
        public LogicBlock(string name, string kind, string sourceFile, string programmingLanguage, IReadOnlyList<LogicNetwork> networks)
        {
            Name = name;
            Kind = kind;
            SourceFile = sourceFile;
            ProgrammingLanguage = programmingLanguage;
            Networks = networks;
        }

        public string Name { get; }

        public string Kind { get; }

        public string SourceFile { get; }

        public string ProgrammingLanguage { get; }

        public IReadOnlyList<LogicNetwork> Networks { get; }
    }

    private sealed class LogicNetwork
    {
        public LogicNetwork(
            int networkIndex,
            string compileUnitId,
            string title,
            string language,
            string translationLanguage,
            string confidence,
            IReadOnlyList<string> statements,
            IReadOnlyList<string> notes)
        {
            NetworkIndex = networkIndex;
            CompileUnitId = compileUnitId;
            Title = title;
            Language = language;
            TranslationLanguage = translationLanguage;
            Confidence = confidence;
            Statements = statements;
            Notes = notes;
        }

        public int NetworkIndex { get; }

        public string CompileUnitId { get; }

        public string Title { get; }

        public string Language { get; }

        public string TranslationLanguage { get; }

        public string Confidence { get; }

        public IReadOnlyList<string> Statements { get; }

        public IReadOnlyList<string> Notes { get; }

        public static LogicNetwork Untranslated(int networkIndex, string compileUnitId, string title, string language, string note)
        {
            return new LogicNetwork(
                networkIndex,
                compileUnitId,
                title,
                language,
                "scl-like",
                "untranslated",
                Array.Empty<string>(),
                new[] { note });
        }
    }

    private sealed class PartNode
    {
        private readonly HashSet<string> _negatedPins;

        public PartNode(string uid, string name, int order, IReadOnlyList<string> negatedPins)
        {
            Uid = uid;
            Name = name;
            Order = order;
            _negatedPins = new HashSet<string>(negatedPins, StringComparer.OrdinalIgnoreCase);
        }

        public string Uid { get; }

        public string Name { get; }

        public int Order { get; }

        public bool IsNegated(string pinName)
        {
            return _negatedPins.Contains(pinName);
        }
    }

    private sealed class AccessNode
    {
        public AccessNode(string uid, string value, string scope)
        {
            Uid = uid;
            Value = value;
            Scope = scope;
        }

        public string Uid { get; }

        public string Value { get; }

        public string Scope { get; }
    }

    private sealed class CallNode
    {
        public CallNode(string uid, string name, int order, IReadOnlyList<CallParameter> parameters)
        {
            Uid = uid;
            Name = name;
            Order = order;
            Parameters = parameters;
        }

        public string Uid { get; }

        public string Name { get; }

        public int Order { get; }

        public IReadOnlyList<CallParameter> Parameters { get; }
    }

    private sealed class CallParameter
    {
        public CallParameter(string name, string section)
        {
            Name = name;
            Section = section;
        }

        public string Name { get; }

        public string Section { get; }
    }

    private readonly struct PartPin : IEquatable<PartPin>
    {
        public PartPin(string partUid, string pinName)
        {
            PartUid = partUid;
            PinName = pinName;
        }

        public string PartUid { get; }

        public string PinName { get; }

        public bool Equals(PartPin other)
        {
            return string.Equals(PartUid, other.PartUid, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(PinName, other.PinName, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object? obj)
        {
            return obj is PartPin other && Equals(other);
        }

        public override int GetHashCode()
        {
            return StringComparer.OrdinalIgnoreCase.GetHashCode(PartUid ?? string.Empty) ^
                StringComparer.OrdinalIgnoreCase.GetHashCode(PinName ?? string.Empty);
        }
    }
}
