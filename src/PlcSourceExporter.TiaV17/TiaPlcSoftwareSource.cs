using PlcSourceExporter.Core;
using Siemens.Engineering;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.SW.Tags;
using Siemens.Engineering.SW.Types;

namespace PlcSourceExporter.TiaV17;

public sealed class TiaPlcSoftwareSource : IPlcSoftwareSource
{
    private readonly IPlcSoftwareSource _source;

    public TiaPlcSoftwareSource(PlcSoftware plcSoftware, IExportLogger? logger = null)
    {
        _source = SiemensPlcSoftwareSource.Create(plcSoftware, ExportDirect, logger);
    }

    public IEnumerable<IPlcExportableObject> EnumerateBlocks() => _source.EnumerateBlocks();

    public IEnumerable<IPlcExportableObject> EnumerateTypes() => _source.EnumerateTypes();

    public IEnumerable<IPlcExportableObject> EnumerateTagTables() => _source.EnumerateTagTables();

    private static void ExportDirect(object exportable, string filePath)
    {
        var file = new FileInfo(filePath);
        switch (exportable)
        {
            case PlcBlock block:
                block.Export(file, ExportOptions.WithDefaults);
                break;
            case PlcType type:
                type.Export(file, ExportOptions.WithDefaults);
                break;
            case PlcTagTable tagTable:
                tagTable.Export(file, ExportOptions.WithDefaults);
                break;
            default:
                throw new NotSupportedException($"Unsupported Siemens export object type '{exportable.GetType().FullName}'.");
        }
    }
}
