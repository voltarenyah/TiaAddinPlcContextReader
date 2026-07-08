using Siemens.Engineering.AddIn;
using Siemens.Engineering.AddIn.Menu;

namespace PlcSourceExporter.AddIn.V17;

public sealed class PlcSourceExporterProjectTreeProvider : ProjectTreeAddInProvider
{
    protected override IEnumerable<ContextMenuAddIn> GetContextMenuAddIns()
    {
        yield return new ExportPlcSourceDataContextMenu();
    }
}
