# TiaAddinPlcContextReader

TIA Portal add-in and export tooling for reading PLC project context into source-faithful and AI-friendly files.

The implementation project is named `PlcSourceExporter`. It provides TIA Portal V17 and V20 add-ins, console harnesses, and shared export logic for PLC source context extraction.

## What It Does

- Adds an `Export PLC Source Data` context-menu command inside TIA Portal.
- Exports PLC blocks, DBs, UDTs, and tag tables from a TIA project.
- Preserves raw TIA XML as the source-of-truth export.
- Generates compact analysis artifacts for AI-assisted PLC review.
- Builds a semantic SQLite graph for blocks, variables, tags, DBs, UDTs, calls, reads, writes, and relationships.
- Writes SCL-like translated network summaries to `translate\program-blocks.yaml`.
- Shows a progress window during long exports and writes `PlcSourceExporter.log`.

This tool reads and exports PLC context. It does not write modified PLC logic back into TIA Portal.

## Projects

- `PlcSourceExporter.Core`: version-neutral export planner, flat category folders, filename sanitizing, duplicate naming, summary, logging, and AI-readable derived artifacts.
- `PlcSourceExporter.TiaV17`: normal TIA Openness V17 adapter for the V17 console harness.
- `PlcSourceExporter.TiaV20`: normal TIA Openness V20 adapter for the V20 console harness.
- `PlcSourceExporter.TestHarness.V17`: `net48` console runner that opens or attaches to a V17 project and exports one PLC.
- `PlcSourceExporter.TestHarness`: `net48` console runner that opens or attaches to a V20 project and exports one PLC.
- `PlcSourceExporter.AddIn.V17`: `net48` TIA Portal V17 project-tree context-menu add-in.
- `PlcSourceExporter.AddIn`: `net48` TIA Portal V20 project-tree context-menu add-in.
- `PlcSourceExporter.Core.Tests`: xUnit tests for the shared export behavior.

## Export Layout

The exporter writes to:

```text
<TIA project folder>\UserFiles\export
```

It recreates only the category folders below that export root:

```text
Blocks
DB
UDT
Tags
```

OB, FC, and FB are exported together into `Blocks`. DB, UDT, and tag tables remain separate. Nested PLC block/type/tag folders are read recursively, but output files are always flat in the category folder. Duplicate object names receive deterministic suffixes such as `_2` and `_3`.

Both V17 and V20 emit the same AI-readable artifact set in the export root:

```text
translate\program-blocks.yaml
model\plc-graph.sqlite
model\schema.sql
model\AGENT_SQLITE_GUIDE.md
metadata.json
block-profiles.jsonl
optimization-hints.jsonl
AI_EXPORT_GUIDE.md
```

## Build and Test

The V17 projects target these installed TIA Portal assemblies:

```text
C:\Program Files\Siemens\Automation\Portal V17\PublicAPI\V17\Siemens.Engineering.dll
C:\Program Files\Siemens\Automation\Portal V17\PublicAPI\V17.AddIn\Siemens.Engineering.AddIn.dll
C:\Program Files\Siemens\Automation\Portal V17\Bin\PublicAPI\AddIn\Feature\V17.AddIn\Siemens.Engineering.AddIn.CntxtMn.dll
```

The V20 projects target these installed TIA Portal assemblies:

```text
C:\Program Files\Siemens\Automation\Portal V20\PublicAPI\V20\Siemens.Engineering.dll
C:\Program Files\Siemens\Automation\Portal V20\PublicAPI\V20.AddIn\Siemens.Engineering.AddIn.dll
C:\Program Files\Siemens\Automation\Portal V20\Bin\PublicAPI\AddIn\Feature\V20.AddIn\Siemens.Engineering.AddIn.CntxtMn.dll
```

Recommended V17 build order:

```powershell
dotnet build .\src\PlcSourceExporter.Core\PlcSourceExporter.Core.csproj
dotnet build .\src\PlcSourceExporter.TiaV17\PlcSourceExporter.TiaV17.csproj --no-restore
dotnet build .\src\PlcSourceExporter.TestHarness.V17\PlcSourceExporter.TestHarness.V17.csproj --no-restore
dotnet build .\src\PlcSourceExporter.AddIn.V17\PlcSourceExporter.AddIn.V17.csproj --no-restore
dotnet test .\tests\PlcSourceExporter.Core.Tests\PlcSourceExporter.Core.Tests.csproj --no-restore
```

Recommended V20 build order:

```powershell
dotnet build .\src\PlcSourceExporter.Core\PlcSourceExporter.Core.csproj
dotnet build .\src\PlcSourceExporter.TiaV20\PlcSourceExporter.TiaV20.csproj --no-restore
dotnet build .\src\PlcSourceExporter.TestHarness\PlcSourceExporter.TestHarness.csproj --no-restore
dotnet build .\src\PlcSourceExporter.AddIn\PlcSourceExporter.AddIn.csproj --no-restore
dotnet test .\tests\PlcSourceExporter.Core.Tests\PlcSourceExporter.Core.Tests.csproj --no-restore
```

If default solution restore fails silently in this environment, use static graph restore:

```powershell
dotnet restore .\PlcSourceExporter.sln /p:RestoreUseStaticGraphEvaluation=true
```

The harness projects reference the already-built version adapter DLLs. Build `PlcSourceExporter.TiaV17` or `PlcSourceExporter.TiaV20` before building the matching harness.

## Console Harness

V17 usage:

```powershell
.\src\PlcSourceExporter.TestHarness.V17\bin\Debug\net48\PlcSourceExporter.TestHarness.V17.exe `
  --tia-version V17 `
  --project "C:\Path\To\Project.ap17" `
  --plc-name "PLC_1" `
  --output "UserFiles\export"
```

V20 usage:

```powershell
.\src\PlcSourceExporter.TestHarness\bin\Debug\net48\PlcSourceExporter.TestHarness.exe `
  --tia-version V20 `
  --project "C:\Path\To\Project.ap20" `
  --plc-name "PLC_1" `
  --output "UserFiles\export"
```

`--plc-name` is optional. If omitted, the first PLC software found in the project is exported. The harness starts visible TIA if no suitable running instance is already attached to the requested project.

Live attach/open/export can require normal desktop permissions. If it fails from a sandboxed terminal with IPC access errors, run the same harness from a normal user PowerShell session.

## Add-In Packaging and Deployment

The V17 add-in entry point is:

```text
PlcSourceExporter.AddIn.V17.PlcSourceExporterProjectTreeProvider
```

The V20 add-in entry point is:

```text
PlcSourceExporter.AddIn.PlcSourceExporterProjectTreeProvider
```

Package V17 with the V17 Add-In Publisher and install the produced package separately from V20:

```powershell
& "C:\Program Files\Siemens\Automation\Portal V17\PublicAPI\V17.AddIn\Siemens.Engineering.AddIn.Publisher.exe" `
  --configuration ".\package\PlcSourceExporter.V17.publisher.xml" `
  --logfile ".\package\PlcSourceExporter.V17.publisher.log" `
  --outfile ".\package\PlcSourceExporter.V17.addin" `
  --verbose `
  --console

New-Item -ItemType Directory -Force "C:\Program Files\Siemens\Automation\Portal V17\AddIns\PlcSourceExporter"
Copy-Item ".\package\PlcSourceExporter.V17.addin" "C:\Program Files\Siemens\Automation\Portal V17\AddIns\PlcSourceExporter\PlcSourceExporter.V17.addin" -Force
```

Package V20 with the V20 Add-In Publisher and install it only under the V20 AddIns folder:

```powershell
& "C:\Program Files\Siemens\Automation\Portal V20\PublicAPI\V20.AddIn\Siemens.Engineering.AddIn.Publisher.exe" `
  --configuration ".\package\PlcSourceExporter.V20.publisher.xml" `
  --logfile ".\package\PlcSourceExporter.V20.publisher.log" `
  --outfile ".\package\PlcSourceExporter.V20.addin" `
  --verbose `
  --console

New-Item -ItemType Directory -Force "C:\Program Files\Siemens\Automation\Portal V20\AddIns\PlcSourceExporter"
Copy-Item ".\package\PlcSourceExporter.V20.addin" "C:\Program Files\Siemens\Automation\Portal V20\AddIns\PlcSourceExporter\PlcSourceExporter.V20.addin" -Force
```

Both package configurations include the built add-in assembly plus the current `PlcSourceExporter.Core.dll`. Required permissions:

```text
TIA.ReadWrite
System.Security.Permissions.FileIOPermission
System.Security.Permissions.UIPermission
```

After install, restart the matching TIA Portal version, enable the add-in if prompted, open a project, then right-click a PLC/device item in the project tree. The menu item is:

```text
Export PLC Source Data
```

The menu item is hidden unless the selected `DeviceItem` resolves to `PlcSoftware`. A log is written to:

```text
<TIA project folder>\UserFiles\export\PlcSourceExporter.log
```
