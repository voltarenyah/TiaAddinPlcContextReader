using PlcSourceExporter.Core;
using Siemens.Engineering;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.SW.Tags;
using Siemens.Engineering.SW.Types;

internal static class SiemensPlcSoftwareSource
{
    public static IPlcSoftwareSource Create(
        PlcSoftware plcSoftware,
        Action<object, string> exportObject,
        IExportLogger? logger = null,
        IProgress<ExportProgress>? progress = null)
    {
        if (plcSoftware == null)
        {
            throw new ArgumentNullException(nameof(plcSoftware));
        }

        if (exportObject == null)
        {
            throw new ArgumentNullException(nameof(exportObject));
        }

        logger ??= NullExportLogger.Instance;
        return new LivePlcSoftwareSource(plcSoftware, exportObject, logger);
    }

    internal static IEnumerable<IPlcExportableObject> EnumerateBlocks(
        PlcBlockGroup group,
        string path,
        Action<object, string> exportObject,
        IExportLogger logger)
    {
        foreach (var block in Snapshot(() => group.Blocks, "blocks", path, logger))
        {
            var exportable = TryCreate(
                () =>
                {
                    var name = block.Name;
                    return new SiemensExportableObject(
                        name,
                        $"{path}/{name}",
                        block.GetType().Name,
                        ExportEligibility.GetUnsupportedBlockLanguageReason(block.ProgrammingLanguage.ToString()),
                        SiemensMetadataReader.ReadBlock(block),
                        block,
                        exportObject);
                },
                "block",
                path,
                logger);

            if (exportable != null)
            {
                yield return exportable;
            }
        }

        foreach (var child in Snapshot(() => group.Groups, "block groups", path, logger))
        {
            var name = TryReadName(() => child.Name, "block group", path, logger);
            if (name == null)
            {
                continue;
            }

            foreach (var block in EnumerateBlocks(child, $"{path}/{name}", exportObject, logger))
            {
                yield return block;
            }
        }
    }

    internal static IEnumerable<IPlcExportableObject> EnumerateTypes(
        PlcTypeGroup group,
        string path,
        Action<object, string> exportObject,
        IExportLogger logger)
    {
        foreach (var type in Snapshot(() => group.Types, "UDTs", path, logger))
        {
            var exportable = TryCreate(
                () =>
                {
                    var name = type.Name;
                    return new SiemensExportableObject(
                        name,
                        $"{path}/{name}",
                        type.GetType().Name,
                        null,
                        SiemensMetadataReader.ReadObject(type),
                        type,
                        exportObject);
                },
                "UDT",
                path,
                logger);

            if (exportable != null)
            {
                yield return exportable;
            }
        }

        foreach (var child in Snapshot(() => group.Groups, "UDT groups", path, logger))
        {
            var name = TryReadName(() => child.Name, "UDT group", path, logger);
            if (name == null)
            {
                continue;
            }

            foreach (var type in EnumerateTypes(child, $"{path}/{name}", exportObject, logger))
            {
                yield return type;
            }
        }
    }

    internal static IEnumerable<IPlcExportableObject> EnumerateTagTables(
        PlcTagTableGroup group,
        string path,
        Action<object, string> exportObject,
        IExportLogger logger)
    {
        foreach (var tagTable in Snapshot(() => group.TagTables, "tag tables", path, logger))
        {
            var exportable = TryCreate(
                () =>
                {
                    var name = tagTable.Name;
                    return new SiemensExportableObject(
                        name,
                        $"{path}/{name}",
                        tagTable.GetType().Name,
                        null,
                        SiemensMetadataReader.ReadObject(tagTable),
                        tagTable,
                        exportObject);
                },
                "tag table",
                path,
                logger);

            if (exportable != null)
            {
                yield return exportable;
            }
        }

        foreach (var child in Snapshot(() => group.Groups, "tag table groups", path, logger))
        {
            var name = TryReadName(() => child.Name, "tag table group", path, logger);
            if (name == null)
            {
                continue;
            }

            foreach (var tagTable in EnumerateTagTables(child, $"{path}/{name}", exportObject, logger))
            {
                yield return tagTable;
            }
        }
    }

    private static IReadOnlyList<T> Snapshot<T>(
        Func<IEnumerable<T>> enumerate,
        string description,
        string path,
        IExportLogger logger)
    {
        try
        {
            return enumerate().ToList();
        }
        catch (Exception ex)
        {
            logger.Warning($"Skipped {description} under {path}: {ExceptionMessages.GetMeaningfulMessage(ex)}");
            return Array.Empty<T>();
        }
    }

    private static IPlcExportableObject? TryCreate(
        Func<IPlcExportableObject> create,
        string description,
        string path,
        IExportLogger logger)
    {
        try
        {
            return create();
        }
        catch (Exception ex)
        {
            logger.Warning($"Skipped unreadable {description} under {path}: {ExceptionMessages.GetMeaningfulMessage(ex)}");
            return null;
        }
    }

    private static string? TryReadName(Func<string> readName, string description, string path, IExportLogger logger)
    {
        try
        {
            return readName();
        }
        catch (Exception ex)
        {
            logger.Warning($"Skipped unreadable {description} under {path}: {ExceptionMessages.GetMeaningfulMessage(ex)}");
            return null;
        }
    }
}

internal sealed class LivePlcSoftwareSource : IPlcSoftwareSource
{
    private readonly PlcSoftware _plcSoftware;
    private readonly Action<object, string> _exportObject;
    private readonly IExportLogger _logger;

    public LivePlcSoftwareSource(
        PlcSoftware plcSoftware,
        Action<object, string> exportObject,
        IExportLogger logger)
    {
        _plcSoftware = plcSoftware;
        _exportObject = exportObject;
        _logger = logger;
    }

    public IEnumerable<IPlcExportableObject> EnumerateBlocks() =>
        SiemensPlcSoftwareSource.EnumerateBlocks(_plcSoftware.BlockGroup, "Blocks", _exportObject, _logger);

    public IEnumerable<IPlcExportableObject> EnumerateTypes() =>
        SiemensPlcSoftwareSource.EnumerateTypes(_plcSoftware.TypeGroup, "Types", _exportObject, _logger);

    public IEnumerable<IPlcExportableObject> EnumerateTagTables() =>
        SiemensPlcSoftwareSource.EnumerateTagTables(_plcSoftware.TagTableGroup, "Tags", _exportObject, _logger);
}

internal sealed class SiemensExportableObject : IPlcExportableObject
{
    private readonly object _exportable;
    private readonly Action<object, string> _exportObject;

    public SiemensExportableObject(
        string name,
        string objectPath,
        string siemensTypeName,
        string? skipReason,
        PlcExportableMetadata metadata,
        object exportable,
        Action<object, string> exportObject)
    {
        Name = name;
        ObjectPath = objectPath;
        SiemensTypeName = siemensTypeName;
        SkipReason = skipReason;
        Metadata = metadata;
        _exportable = exportable;
        _exportObject = exportObject;
    }

    public string Name { get; }

    public string ObjectPath { get; }

    public string SiemensTypeName { get; }

    public string? SkipReason { get; }

    public PlcExportableMetadata Metadata { get; }

    public void ExportTo(string filePath)
    {
        _exportObject(_exportable, filePath);
    }
}

internal static class SiemensMetadataReader
{
    private static readonly string[] IdentifierPropertyNames = { "Guid", "Id", "ObjectIdentifier", "GlobalId" };
    private static readonly string[] IdentifierAttributeNames = { "Id", "ObjectId", "Guid", "GlobalId", "Name" };

    public static PlcExportableMetadata ReadBlock(PlcBlock block)
    {
        return new PlcExportableMetadata(
            block.ProgrammingLanguage.ToString(),
            TryReadIdentifier(block),
            block.Number,
            block.IsKnowHowProtected,
            ToDateTimeOffset(block.CreationDate),
            ToDateTimeOffset(block.ModifiedDate),
            ToDateTimeOffset(block.CodeModifiedDate),
            ToDateTimeOffset(block.InterfaceModifiedDate));
    }

    public static PlcExportableMetadata ReadObject(object exportable)
    {
        return new PlcExportableMetadata(
            TryReadProperty(exportable, "ProgrammingLanguage")?.ToString(),
            TryReadIdentifier(exportable),
            TryReadInt(exportable, "Number"),
            TryReadBool(exportable, "IsKnowHowProtected"),
            TryReadDate(exportable, "CreationDate"),
            TryReadDate(exportable, "ModifiedDate"),
            TryReadDate(exportable, "CodeModifiedDate"),
            TryReadDate(exportable, "InterfaceModifiedDate"));
    }

    private static string? TryReadIdentifier(object exportable)
    {
        foreach (var propertyName in IdentifierPropertyNames)
        {
            var value = TryReadProperty(exportable, propertyName);
            if (value != null)
            {
                return value.ToString();
            }
        }

        if (exportable is IEngineeringObject engineeringObject)
        {
            foreach (var attributeName in IdentifierAttributeNames)
            {
                try
                {
                    var value = engineeringObject.GetAttribute(attributeName);
                    if (value != null)
                    {
                        return value.ToString();
                    }
                }
                catch
                {
                    // Some TIA object types do not expose these attributes.
                }
            }
        }

        return null;
    }

    private static object? TryReadProperty(object exportable, string propertyName)
    {
        try
        {
            return exportable.GetType().GetProperty(propertyName)?.GetValue(exportable);
        }
        catch
        {
            return null;
        }
    }

    private static int? TryReadInt(object exportable, string propertyName)
    {
        var value = TryReadProperty(exportable, propertyName);
        return value is int number ? number : null;
    }

    private static bool? TryReadBool(object exportable, string propertyName)
    {
        var value = TryReadProperty(exportable, propertyName);
        return value is bool flag ? flag : null;
    }

    private static DateTimeOffset? TryReadDate(object exportable, string propertyName)
    {
        return ToDateTimeOffset(TryReadProperty(exportable, propertyName));
    }

    private static DateTimeOffset? ToDateTimeOffset(object? value)
    {
        return value switch
        {
            DateTimeOffset dateTimeOffset => dateTimeOffset,
            DateTime dateTime => new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Local)).ToUniversalTime(),
            _ => null
        };
    }
}
