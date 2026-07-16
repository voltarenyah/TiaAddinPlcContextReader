# PlcSourceExporter AI Agent Handoff Guide

This file is the primary orientation document for another AI coding agent that needs to understand, test, modify, package, or continue `PlcSourceExporter`.

It is intentionally direct and operational. Start here, then confirm details in the current source files before changing behavior.

## 1. Scope And Repo Boundary

Repository root:

```text
C:\Users\Ansel\Documents\Siemens TIA Add-in Dev\PlcSourceExporter
```

Treat this folder as the project boundary. Do not use the parent folder as the git repo for this project. The parent `Siemens TIA Add-in Dev` workspace can contain unrelated exports, drafts, and Siemens investigation material.

The implementation name is `PlcSourceExporter`; the public-facing README title is `TiaAddinPlcContextReader`.

The tool is a Siemens TIA Portal V17/V20 add-in plus console harnesses and shared export logic. It reads PLC project context and exports source-faithful files plus AI-friendly derived artifacts. It does not write modified PLC logic back into TIA Portal in the current shipped workflow.

## 2. Current Working Tree Notice

At the time this guide was written, the repo already had uncommitted changes before this file was added:

```text
 M src/PlcSourceExporter.Core/AiExportGuide.cs
 M src/PlcSourceExporter.Core/ProgramBlockLogicYamlWriter.cs
 M src/PlcSourceExporter.Core/SemanticPlcGraph.cs
 M tests/PlcSourceExporter.Core.Tests/SemanticPlcGraphTests.cs
?? agentknowledge-draft/
?? docs/
```

Do not revert those changes unless the user explicitly asks. If you need to edit any of those files, inspect the current contents first and preserve unrelated work.

## 3. Fast Orientation For A New AI Agent

Read these files first:

1. `README.md` for user-facing build, install, and usage instructions.
2. `AI_EXPORT_GUIDE.md` for how an AI should read generated exports.
3. `src/PlcSourceExporter.Core/PlcExportService.cs` for the export pipeline.
4. `src/PlcSourceExporter.Core/ProgramBlockLogicYamlWriter.cs` for LAD/FBD/SCL translation into `translate\program-blocks.yaml`.
5. `src/PlcSourceExporter.Core/SemanticPlcGraph.cs` for the SQLite semantic graph and embedded `AGENT_SQLITE_GUIDE.md`.
6. `src/PlcSourceExporter.AddInShared/ExportAddInWorkflow.cs` and `src/PlcSourceExporter.AddInShared/AddInSemanticPlcModelWriter.cs` for add-in orchestration and the helper process.
7. `package/PlcSourceExporter.V17.publisher.xml` and `package/install-v17-addin.ps1` for V17 packaging/install.
8. `tests/PlcSourceExporter.Core.Tests/PlcExportServiceTests.cs` for the broadest behavioral test coverage.

Useful mental model:

```text
TIA project tree selection
  -> V17/V20 add-in or harness resolves PlcSoftware
  -> SiemensShared enumerates blocks, UDTs, and tag tables recursively
  -> Core exports XML into flat category folders
  -> Core writes metadata
  -> Core writes translated logic YAML
  -> Core/add-in helper writes SQLite graph and model guide
  -> Core writes block profiles and optimization hints
  -> Core writes AI export guide
```

When answering PLC logic questions from an export, the semantic source of truth is `model\plc-graph.sqlite`; raw XML remains the final proof source for exact TIA source shape.

## 4. What This Project Can Do

`PlcSourceExporter` can:

- Add an `Export PLC Source Data` context-menu command inside TIA Portal V17 and V20.
- Export PLC blocks, data blocks, UDTs, and tag tables from a selected PLC software object.
- Recursively read nested Siemens block/type/tag groups while writing a flat output layout.
- Preserve raw Siemens Openness XML under `Blocks`, `DB`, `UDT`, and `Tags`.
- Generate `metadata.json` with exported/skipped/failed inventory records.
- Translate program block networks into `translate\program-blocks.yaml`.
- Generate `model\plc-graph.sqlite`, `model\schema.sql`, and `model\AGENT_SQLITE_GUIDE.md`.
- Add translated network statements into SQLite `Network` node property `logicStatements`.
- Generate `block-profiles.jsonl` and `optimization-hints.jsonl`.
- Write `AI_EXPORT_GUIDE.md` into each export root.
- Log export progress and errors to `<project>\UserFiles\export\PlcSourceExporter.log`.
- Run as a TIA add-in or from a version-specific console harness.
- Package and install a V17 `.addin` with an external `ExportAnalyzer` helper folder.

The codebase contains older builders for `tags.json`, `udt.json`, `callgraph.json`, `calltree.md`, `networks.jsonl`, and `references.jsonl`. The current `PlcExportService` pipeline does not call all of those writers directly. Some are still used internally by current artifacts, and some are retained for tests or previous export-generation flows. Check `PlcExportService.Export(...)` before claiming an artifact is emitted by the current run.

## 5. What This Project Cannot Do

Current limits:

- It does not write modified PLC logic back into TIA Portal.
- It does not import changed XML back into the project in the normal export command.
- It does not guarantee full Boolean decompilation for every LAD/FBD network shape.
- It does not guarantee read/write classification for every standalone LAD/FBD access.
- It does not reconstruct full state machines.
- It does not provide a safety-certified semantic layer; safety languages such as `F_LAD` and `F_FBD` are preserved as language markers, not interpreted as safety proof.
- It cannot make TIA Openness permissions, Siemens installed assemblies, or Program Files write permissions appear if they are missing.
- It cannot safely infer machine intent from variable names alone.

If a network translation is `partial` or `untranslated`, inspect the raw XML before making any engineering claim that would affect PLC behavior.

## 6. Project Tree Map

Top-level source and support files:

```text
README.md
AI_EXPORT_GUIDE.md
AI_AGENT_HANDOFF.md
PlcSourceExporter.sln
Siemens.Engineering.AddIn.res
package\
src\
tests\
agentknowledge-draft\
docs\superpowers\plans\
```

Important top-level responsibilities:

- `README.md`: public usage, install, build, packaging, and harness instructions.
- `AI_EXPORT_GUIDE.md`: repo copy of the guide emitted into export roots by `AiExportGuideBuilder`.
- `PlcSourceExporter.sln`: solution containing core, V17/V20 adapters, add-ins, harnesses, helper, and tests.
- `Siemens.Engineering.AddIn.res`: resource used by the add-in publisher process.
- `package\`: publisher XMLs, generated `.addin` packages, installer script, and release staging.
- `src\`: implementation projects.
- `tests\PlcSourceExporter.Core.Tests`: xUnit tests for version-neutral behavior.
- `agentknowledge-draft\`: draft generic PLC reasoning package; not wired into current export output.
- `docs\superpowers\plans\`: implementation plans. Current notable plan is for future TIA inline translation comments/import.

## 7. Solution Projects

### `src\PlcSourceExporter.Core`

Target framework: `netstandard2.0`.

This is the version-neutral engine. It must not reference Siemens assemblies. It owns:

- export contracts
- category/folder rules
- file path planning
- metadata serialization
- export summary/progress/logging types
- translated logic YAML
- semantic graph import and SQLite persistence
- block profile and optimization hint generation
- AI guide generation

Key dependency:

```xml
<PackageReference Include="Microsoft.Data.Sqlite" Version="8.0.0" />
```

The core project embeds `e_sqlite3.dll` as a resource so native SQLite can be extracted/loaded in controlled contexts.

### `src\PlcSourceExporter.SiemensShared`

This is shared Siemens-facing source linked into version-specific projects. It is not a standalone project in the solution.

`SiemensPlcSoftwareSource.cs` adapts a Siemens `PlcSoftware` object to the core `IPlcSoftwareSource` contract. It recursively enumerates:

- `PlcSoftware.BlockGroup`
- `PlcSoftware.TypeGroup`
- `PlcSoftware.TagTableGroup`

It creates `IPlcExportableObject` wrappers, reads metadata defensively, logs unreadable groups, and calls a caller-provided export delegate. That delegate is different for normal Openness/harness use versus add-in reflection use.

### `src\PlcSourceExporter.TiaV17`

Target framework: `net48`.

This is the normal Siemens Openness adapter for TIA Portal V17 console usage. It references:

```text
C:\Program Files\Siemens\Automation\Portal V17\PublicAPI\V17\Siemens.Engineering.dll
```

Main files:

- `TiaPlcResolver.cs`: finds PLC software by walking devices and device items; selects first PLC or a named PLC.
- `TiaPlcSoftwareSource.cs`: wraps `SiemensPlcSoftwareSource` and exports blocks/types/tag tables using direct typed Openness calls.
- `TiaPortalProjectSession.cs`: attaches to an already-open TIA project when possible, otherwise opens the project visibly.
- `TiaProjectPaths.cs`: resolves default or user-specified export roots.

### `src\PlcSourceExporter.TiaV20`

Target framework: `net48`.

This mirrors `TiaV17`, but references V20 Siemens assemblies:

```text
C:\Program Files\Siemens\Automation\Portal V20\PublicAPI\V20\Siemens.Engineering.dll
```

Keep V17 and V20 changes synchronized only when the API behavior is truly identical. Do not deploy a V20 `.addin` to V17 or assume version compatibility from file names.

### `src\PlcSourceExporter.AddInShared`

Shared code linked into both add-in projects.

Files:

- `ExportAddInWorkflow.cs`: orchestrates the add-in export, progress window, exclusive access, cancellation, logging, and final UI state.
- `ExportProgressWindow.cs`: hosts the WinForms progress form on an STA UI thread and waits for completion/close.
- `ExportProgressForm.cs`: WinForms UI for current phase, counts, cancel button, success/failure/cancel states, and summary display.
- `AddInSemanticPlcModelWriter.cs`: starts `PlcSourceExporter.ExportAnalyzer.exe` through `Siemens.Engineering.AddIn.Utilities.Process`, waits up to 20 minutes, and returns expected model paths.

Important design point: normal `System.Diagnostics.Process` is not safe in the TIA add-in partial-trust path. The add-in helper uses Siemens AddIn Utilities process APIs and requires `Siemens.Engineering.AddIn.Permissions.ProcessStartPermission`.

### `src\PlcSourceExporter.AddIn.V17`

Target framework: `net48`.

TIA Portal V17 project-tree context-menu add-in.

Entry point:

```text
PlcSourceExporter.AddIn.V17.PlcSourceExporterProjectTreeProvider
```

Main files:

- `PlcSourceExporterProjectTreeProvider.cs`: returns the context menu add-in.
- `ExportPlcSourceDataContextMenu.cs`: defines `Export PLC Source Data`, hides it unless the selection resolves to `PlcSoftware`, resolves project/export paths, creates the logger, and runs the shared workflow.
- `AddInPlcSoftwareSource.cs`: resolves `PlcSoftware` from selected `DeviceItem` and exports via reflection against `Export(FileInfo, ExportOptions)`.
- `PlcSourceExporter.AddIn.V17.csproj`: links shared add-in and Siemens source files, references V17 AddIn assemblies, and copies SQLite runtime dependencies to output.

Hardcoded helper path:

```text
C:\Program Files\Siemens\Automation\Portal V17\AddIns\PlcSourceExporter\ExportAnalyzer\PlcSourceExporter.ExportAnalyzer.exe
```

### `src\PlcSourceExporter.AddIn`

Target framework: `net48`.

TIA Portal V20 project-tree context-menu add-in. It mirrors the V17 add-in but references V20 AddIn assemblies and uses the V20 helper path:

```text
C:\Program Files\Siemens\Automation\Portal V20\AddIns\PlcSourceExporter\ExportAnalyzer\PlcSourceExporter.ExportAnalyzer.exe
```

Current V20 publisher XML only includes the add-in and core DLL, while the V17 publisher XML includes SQLite runtime dependency DLLs and process-start permission. Check the publisher XML before assuming parity.

### `src\PlcSourceExporter.ExportAnalyzer`

Target framework: `net48`; `PlatformTarget` is `x64`.

This is the helper executable invoked from add-ins to generate the semantic model outside the TIA add-in sandbox.

Usage:

```powershell
.\src\PlcSourceExporter.ExportAnalyzer\bin\Debug\net48\PlcSourceExporter.ExportAnalyzer.exe --export-root "C:\Path\To\UserFiles\export" --model-only
```

It accepts `--model-only` for compatibility but always calls `SemanticPlcModelWriter.Write(exportRoot)`.

### `src\PlcSourceExporter.TestHarness.V17`

Target framework: `net48`.

Console runner for TIA V17. It opens or attaches to a `.ap17` project, selects a PLC, exports, prints summary, and writes the same log file as the add-in path.

Usage:

```powershell
.\src\PlcSourceExporter.TestHarness.V17\bin\Debug\net48\PlcSourceExporter.TestHarness.V17.exe `
  --tia-version V17 `
  --project "C:\Path\To\Project.ap17" `
  --plc-name "PLC_1" `
  --output "UserFiles\export"
```

### `src\PlcSourceExporter.TestHarness`

Target framework: `net48`.

Console runner for TIA V20. Same behavior as V17, but requires `--tia-version V20` and `.ap20` projects.

Usage:

```powershell
.\src\PlcSourceExporter.TestHarness\bin\Debug\net48\PlcSourceExporter.TestHarness.exe `
  --tia-version V20 `
  --project "C:\Path\To\Project.ap20" `
  --plc-name "PLC_1" `
  --output "UserFiles\export"
```

### `tests\PlcSourceExporter.Core.Tests`

Target framework: `net8.0`.

The only test project. It tests core behavior without launching TIA Portal.

Run this first for nearly every code change:

```powershell
dotnet test .\tests\PlcSourceExporter.Core.Tests\PlcSourceExporter.Core.Tests.csproj --no-restore
```

## 8. Core Export Pipeline

`PlcExportService.Export(...)` is the main pipeline:

1. Validate `IPlcSoftwareSource` and `exportRoot`.
2. Prepare the export root with `ExportDirectoryPreparer.Prepare(exportRoot)`.
3. Create `ExportSummary`, `ExportPathPlanner`, and `ExportMetadataWriter`.
4. Enumerate blocks, resolve block category, and export each object.
5. Enumerate UDTs and export to `UDT`.
6. Enumerate tag tables and export to `Tags`.
7. Write `metadata.json`.
8. Write `translate\program-blocks.yaml`.
9. Write semantic model files:
   - `model\plc-graph.sqlite`
   - `model\schema.sql`
   - `model\AGENT_SQLITE_GUIDE.md`
10. Write `block-profiles.jsonl` and `optimization-hints.jsonl`.
11. Write export-local `AI_EXPORT_GUIDE.md`.
12. Log final exported/skipped/failed counts.

The service continues after individual object export failures. It records successes, skips, and failures in both `ExportSummary` and `metadata.json`.

Skipped behavior:

- `ExportEligibility.GetUnsupportedBlockLanguageReason(...)` skips known unsupported languages such as `ProDiag`, `ProDiag_OB`, and `F_CALL`.
- Siemens "export of block ... not permitted" errors are treated as skipped, not failed.
- Generic exceptions remain failures.

Current progress behavior:

- `Preparing`: percent `0`.
- `ExportingObjects`: percent `15`, completed object count shown without pre-counting all objects.
- `WritingDerivedArtifacts`: percent roughly `75` to `95`.
- `Completed`: percent `100`.

Tests confirm that export streams objects without pre-enumerating all objects and honors cancellation between exported objects.

## 9. Export Layout And Artifacts

Default export root:

```text
<TIA project folder>\UserFiles\export
```

Category folders created by the current export service:

```text
Blocks\
DB\
UDT\
Tags\
```

Program OB, FC, and FB XML files all go into `Blocks`. Data blocks go into `DB`. User data types go into `UDT`. Tag tables go into `Tags`.

Nested Siemens groups are flattened. Duplicate object names get deterministic suffixes such as `_2` and `_3`. Invalid Windows filename characters are replaced with `_`.

Current directly emitted derived artifacts:

```text
metadata.json
translate\program-blocks.yaml
model\plc-graph.sqlite
model\schema.sql
model\AGENT_SQLITE_GUIDE.md
block-profiles.jsonl
optimization-hints.jsonl
AI_EXPORT_GUIDE.md
PlcSourceExporter.log
```

Do not assume older artifact names are current output. Older docs/memories may mention:

```text
components.metadata.json
program-block-callgraph.json
program-block-calling-structure.md
program-networks.jsonl
program-references.jsonl
udt.type-table.json
callgraph.json
calltree.md
networks.jsonl
references.jsonl
tags.json
udt.json
```

Some builders for these still exist in code. Check the pipeline before claiming they are emitted in a live export.

## 10. Key Core Files

### `PlcExportContracts.cs`

Defines the core abstractions:

- `IPlcSoftwareSource`: enumerates blocks, types, and tag tables.
- `IPlcExportableObject`: name/path/type/metadata/skip reason plus `ExportTo`.
- `PlcExportableMetadata`: language, TIA identifier, number, know-how protection flag, timestamps.
- `IExportLogger`: info/warning/error logging.
- `ISemanticPlcModelWriter`: semantic model writer abstraction.
- `NullExportLogger`: no-op logger.

If you need to add import/writeback behavior, do not overload these export-only interfaces casually. Add a separate importer abstraction and tests.

### `ExportCategory.cs`

Defines the fixed categories and folders:

```text
OB -> Blocks
FC -> Blocks
FB -> Blocks
DB -> DB
UDT -> UDT
Tags -> Tags
```

### `BlockCategoryResolver.cs`

Maps Siemens type names and object-name prefixes to categories. Fallback is `Function`.

Be careful with the ordering. `FunctionBlock` must resolve to FB, not FC.

### `ExportDirectoryPreparer.cs`

Cleans the export root, deletes files/directories, preserves an existing `PlcSourceExporter.log`, then creates category folders.

This code is intentionally scoped to the export root. Do not broaden deletion behavior.

### `ExportPathPlanner.cs`

Sanitizes filenames and adds deterministic numeric suffixes for duplicate names.

### `ExportMetadata.cs`

Writes `metadata.json` with schema version `1.0`, start/finish timestamps, export root, and one component record per exported/skipped/failed object.

Records include stable IDs based on category and source path.

### `ProgramBlockComponentCatalog.cs`

Reads `metadata.json`, filters to status `Exported`, and returns only program block categories `OB`, `FC`, and `FB`. It orders OBs first, FCs second, FBs third.

This catalog is used by translation, profiles, and semantic graph import.

### `ProgramBlockLogicYamlWriter.cs`

Writes:

```text
translate\program-blocks.yaml
```

Schema version is `1.0`.

For each program block it records block name, kind, source file, programming language, network index, compile unit ID, title, network language, translation language, confidence, statements, and notes.

Translation behavior:

- SCL networks are compacted from structured text XML tokens when possible.
- LAD/FBD/F_LAD/F_FBD networks are read from `FlgNet`.
- Empty networks can be emitted as exact empty translations.
- Unsupported or unresolved networks become `confidence: "untranslated"` with notes.
- Resolvable partial networks become `confidence: "partial"` with statements plus notes.
- Fully resolved networks become `confidence: "exact"`.

Important supported LAD/FBD shapes include contacts, negated contacts, coils, set/reset coils, pulse contacts/coils, branches, comparisons, timers/counters/triggers, calls, `Move`, `S_Move`, `Sr`, `Calc`, `InRange`, `Jump`, `Return`, `ReturnTrue`, `ReturnValue`, increment parts, generic function-expression parts, and some runtime-specific parts such as `ACK_GL`, `RDREC`, and `SinaPos`.

When improving translation, add or update tests in `PlcExportServiceTests.cs` near the existing translation cases.

### `ProgramSemanticReference.cs`

Parses program block XML into network records and reference records. It can write `networks.jsonl` and `references.jsonl`, but the current export pipeline does not directly emit these files.

Current uses still matter:

- `ProgramBlockProfileBuilder` uses `ProgramSemanticReferenceBuilder.Parse(...)`.
- `TiaXmlSemanticGraphImporter.ImportBlockXml(...)` uses parsed references to add graph relationships.

Reference access values:

- `read`
- `write`
- `inout`
- `call`
- `unknown`

Do not treat `unknown` as read or write without XML confirmation.

### `SemanticPlcGraph.cs`

The largest and most important semantic model file.

It defines:

- node kinds in `SemanticNodeKind`
- relationship types in `SemanticRelationshipType`
- `SemanticGraphNode`
- `SemanticGraphEdge`
- in-memory `SemanticPlcGraph`
- `TiaXmlSemanticGraphImporter`
- query helpers in `PlcSemanticGraphQueries`
- SQLite schema in `PlcSemanticGraphSqliteSchema`
- SQLite persistence in `SqliteSemanticGraphStore`
- native SQLite runtime extraction/loading
- semantic model file writer
- embedded `AGENT_SQLITE_GUIDE.md` content

Node kinds include project, PLC device, hardware device, OB, FB, FC, network, instruction, variable, global DB, instance DB, DB member, UDT, UDT member, data type, PLC tag, and IO address.

Relationship types include:

```text
CONTAINS
CALLS
READS
WRITES
DECLARES
HAS_TYPE
INSTANCE_OF
CONNECTED_TO
EXECUTES_BEFORE
EXECUTES_AFTER
```

SQLite tables:

```text
graph_nodes
graph_node_properties
graph_edges
graph_edge_properties
```

The graph importer builds from `metadata.json` plus raw XML. It imports program blocks, DBs, UDTs, and tags. It adds `logicStatements` to network nodes by calling `ProgramBlockLogicYamlWriter.GetNetworkStatementTextByCompileUnitId(...)`.

### `SemanticPlcModelWriters.cs`

Provides model-writer strategies:

- `InProcessSemanticPlcModelWriter`: calls `SemanticPlcModelWriter.Write(...)` in the current process.
- `ExternalProcessSemanticPlcModelWriter`: starts an external executable with `--export-root ... --model-only`.

The add-ins use `AddInSemanticPlcModelWriter` instead of this standard external writer because the Siemens add-in sandbox needs Siemens AddIn Utilities process APIs.

### `ProgramBlockProfile.cs`

Writes:

```text
block-profiles.jsonl
optimization-hints.jsonl
```

Profiles include block kind, language, source file, network count, call count, interface summary, key reads, key writes, key calls, stateful elements, and instance DBs.

Optimization hint kinds include:

- `multi_writer`
- `never_read_symbol`
- `never_written_output`
- `repeated_call_target`
- `scan_order_dependency`

Hints are review leads, not proof of defects.

### `ProgramBlockCallGraph.cs`

Can build `callgraph.json` and `calltree.md` from exported program blocks. The current main pipeline does not directly call it, but tests still cover it and it remains useful as a prior/export-adjacent artifact generator.

### `TagTable.cs`

Can parse tag table XML and write `tags.json`. The current main pipeline does not directly call `TagTableBuilder.Write(...)`; the semantic graph importer parses tag XML directly.

### `UdtTypeTable.cs`

Can parse UDT XML and write `udt.json`. The current main pipeline does not directly call `UdtTypeTableBuilder.Write(...)`; the semantic graph importer parses UDT XML directly.

### `AiExportGuide.cs`

Writes export-local `AI_EXPORT_GUIDE.md`. Keep it synchronized with any change to emitted artifact names, reading order, or graph/YAML semantics. The repo-root `AI_EXPORT_GUIDE.md` should match the intended export-local content.

## 11. Packaging And Install

### V17 Build Order

Run from repo root:

```powershell
dotnet build .\src\PlcSourceExporter.Core\PlcSourceExporter.Core.csproj
dotnet build .\src\PlcSourceExporter.TiaV17\PlcSourceExporter.TiaV17.csproj --no-restore
dotnet build .\src\PlcSourceExporter.TestHarness.V17\PlcSourceExporter.TestHarness.V17.csproj --no-restore
dotnet build .\src\PlcSourceExporter.ExportAnalyzer\PlcSourceExporter.ExportAnalyzer.csproj --no-restore
dotnet build .\src\PlcSourceExporter.AddIn.V17\PlcSourceExporter.AddIn.V17.csproj --no-restore
dotnet test .\tests\PlcSourceExporter.Core.Tests\PlcSourceExporter.Core.Tests.csproj --no-restore
```

### V20 Build Order

```powershell
dotnet build .\src\PlcSourceExporter.Core\PlcSourceExporter.Core.csproj
dotnet build .\src\PlcSourceExporter.TiaV20\PlcSourceExporter.TiaV20.csproj --no-restore
dotnet build .\src\PlcSourceExporter.TestHarness\PlcSourceExporter.TestHarness.csproj --no-restore
dotnet build .\src\PlcSourceExporter.ExportAnalyzer\PlcSourceExporter.ExportAnalyzer.csproj --no-restore
dotnet build .\src\PlcSourceExporter.AddIn\PlcSourceExporter.AddIn.csproj --no-restore
dotnet test .\tests\PlcSourceExporter.Core.Tests\PlcSourceExporter.Core.Tests.csproj --no-restore
```

If solution restore behaves strangely:

```powershell
dotnet restore .\PlcSourceExporter.sln /p:RestoreUseStaticGraphEvaluation=true
```

### V17 Publisher Command

```powershell
& "C:\Program Files\Siemens\Automation\Portal V17\PublicAPI\V17.AddIn\Siemens.Engineering.AddIn.Publisher.exe" `
  --configuration ".\package\PlcSourceExporter.V17.publisher.xml" `
  --logfile ".\package\PlcSourceExporter.V17.publisher.log" `
  --outfile ".\package\PlcSourceExporter.V17.addin" `
  --verbose `
  --console
```

V17 install target:

```text
C:\Program Files\Siemens\Automation\Portal V17\AddIns\PlcSourceExporter\PlcSourceExporter.V17.addin
```

V17 helper target:

```text
C:\Program Files\Siemens\Automation\Portal V17\AddIns\PlcSourceExporter\ExportAnalyzer\
```

Recommended install from a release folder, as Administrator:

```powershell
.\install-v17-addin.ps1
```

The installer copies the `.addin`, copies the full `ExportAnalyzer` folder, writes `PlcSourceExporter.V17.install-status.txt`, and returns the installed add-in hash.

### V20 Publisher Command

```powershell
& "C:\Program Files\Siemens\Automation\Portal V20\PublicAPI\V20.AddIn\Siemens.Engineering.AddIn.Publisher.exe" `
  --configuration ".\package\PlcSourceExporter.V20.publisher.xml" `
  --logfile ".\package\PlcSourceExporter.V20.publisher.log" `
  --outfile ".\package\PlcSourceExporter.V20.addin" `
  --verbose `
  --console
```

V20 install target:

```text
C:\Program Files\Siemens\Automation\Portal V20\AddIns\PlcSourceExporter\PlcSourceExporter.V20.addin
```

V20 helper target expected by code:

```text
C:\Program Files\Siemens\Automation\Portal V20\AddIns\PlcSourceExporter\ExportAnalyzer\
```

Before claiming V20 install readiness, verify the publisher XML and helper folder behavior. The current V20 publisher XML is smaller than the V17 publisher XML.

### Public Release Zip Best Practice

A release zip for V17 should contain a top-level folder such as:

```text
v0.1.0-TIA-17\
  PlcSourceExporter.V17.addin
  install-v17-addin.ps1
  ExportAnalyzer\
    PlcSourceExporter.ExportAnalyzer.exe
    PlcSourceExporter.Core.dll
    Microsoft.Data.Sqlite.dll
    SQLitePCLRaw.*
    e_sqlite3.dll
    other runtime DLLs
```

A plain `.addin` file is not enough because the add-in starts `ExportAnalyzer`.

## 12. Testing Strategy

Run the core test project after most modifications:

```powershell
dotnet test .\tests\PlcSourceExporter.Core.Tests\PlcSourceExporter.Core.Tests.csproj --no-restore
```

If dependencies are not restored:

```powershell
dotnet restore .\PlcSourceExporter.sln /p:RestoreUseStaticGraphEvaluation=true
dotnet test .\tests\PlcSourceExporter.Core.Tests\PlcSourceExporter.Core.Tests.csproj
```

Targeted test areas:

- Export flow: `PlcExportServiceTests.cs`
- LAD/FBD/SCL translation: `PlcExportServiceTests.cs`
- Semantic graph: `SemanticPlcGraphTests.cs`
- Package constraints: `PublisherPackageTests.cs`
- Program references: `ProgramSemanticReferenceTests.cs`
- Profiles and hints: `BlockProfilesAndHintsTests.cs`
- Path planning: `ExportPathPlannerTests.cs`
- Export folder cleaning: `ExportDirectoryPreparerTests.cs`
- Tags: `TagTableTests.cs`
- UDTs: `UdtTypeTableTests.cs`
- Legacy/optional callgraph builder: `ProgramBlockCallGraphTests.cs`

For translation work, a practical loop is:

1. Add a focused failing test in `PlcExportServiceTests.cs`.
2. Implement the smallest translator change in `ProgramBlockLogicYamlWriter.cs`.
3. Run the core test project.
4. Regenerate a real export with the harness or add-in.
5. Count weak translation markers:

```powershell
Select-String -Path "C:\Path\To\UserFiles\export\translate\program-blocks.yaml" -Pattern "partial|untranslated|Skipped|Unsupported|No supported" -AllMatches
```

Do not treat zero weak markers as proof of correct machine semantics. It only means the translator produced statements without self-reported gaps.

## 13. Live TIA Verification

Use live TIA verification when changes touch:

- Siemens adapter behavior
- add-in context menus
- project/PLC resolution
- export source enumeration
- package/publisher XML
- Program Files install layout
- helper process launch
- permissions
- actual generated export contents

Harness verification example for V17:

```powershell
.\src\PlcSourceExporter.TestHarness.V17\bin\Debug\net48\PlcSourceExporter.TestHarness.V17.exe `
  --tia-version V17 `
  --project "C:\Path\To\Project.ap17" `
  --output "UserFiles\export"
```

Harness verification example for V20:

```powershell
.\src\PlcSourceExporter.TestHarness\bin\Debug\net48\PlcSourceExporter.TestHarness.exe `
  --tia-version V20 `
  --project "C:\Path\To\Project.ap20" `
  --output "UserFiles\export"
```

Live verification checklist:

1. Confirm TIA project opened or attached successfully.
2. Confirm `PlcSourceExporter.log` ends with `Export complete: X exported, Y skipped, Z failed`.
3. Confirm expected folders exist: `Blocks`, `DB`, `UDT`, `Tags`.
4. Confirm current artifacts exist: `metadata.json`, `translate\program-blocks.yaml`, `model\plc-graph.sqlite`, `model\schema.sql`, `model\AGENT_SQLITE_GUIDE.md`, `block-profiles.jsonl`, `optimization-hints.jsonl`, `AI_EXPORT_GUIDE.md`.
5. Check SQLite integrity:

```powershell
dotnet run --project .\SomeLocalSqliteCheckProject
```

Or use any available SQLite client:

```sql
PRAGMA integrity_check;
SELECT kind, COUNT(*) FROM graph_nodes GROUP BY kind ORDER BY kind;
SELECT type, COUNT(*) FROM graph_edges GROUP BY type ORDER BY type;
```

6. Inspect `translate\program-blocks.yaml` for `partial`, `untranslated`, `Skipped`, `Unsupported`, and `No supported`.
7. For installed add-ins, compare package and installed hashes:

```powershell
Get-FileHash ".\package\PlcSourceExporter.V17.addin" -Algorithm SHA256
Get-FileHash "C:\Program Files\Siemens\Automation\Portal V17\AddIns\PlcSourceExporter\PlcSourceExporter.V17.addin" -Algorithm SHA256
```

Program Files writes usually require Administrator PowerShell. Do not hide or work around permission failures; ask for elevation when install verification is required.

## 14. Common Failure Modes

### Missing Siemens Assemblies

Builds for V17/V20 require installed TIA Portal PublicAPI assemblies at the exact paths in the `.csproj` files. If those files are absent, version-specific builds cannot succeed.

### Stale PATH Or Restore Problems

If a tool was installed during the same shell session, PATH may be stale. Use the full executable path or restart the shell.

If solution restore fails or hangs, try static graph restore:

```powershell
dotnet restore .\PlcSourceExporter.sln /p:RestoreUseStaticGraphEvaluation=true
```

### TIA IPC Or Attach Failures

Harnesses attach to running TIA processes where possible. Sandboxed terminals or mismatched TIA versions can produce access or IPC failures. Re-run from a normal user PowerShell session if TIA access fails for environment reasons.

### Add-In Process Launch Failures

The add-in path uses `Siemens.Engineering.AddIn.Utilities.Process`, not `System.Diagnostics.Process`. If the helper fails:

- verify `ExportAnalyzer` exists under the expected Program Files add-in folder
- verify publisher XML grants `Siemens.Engineering.AddIn.Permissions.ProcessStartPermission`
- inspect `PlcSourceExporter.log`
- run `PlcSourceExporter.ExportAnalyzer.exe --export-root ... --model-only` manually

### Native SQLite Issues

V17 tests assert that the package includes managed SQLite DLLs, core embeds native SQLite, and the package avoids unmanaged-code permission. If SQLite fails:

- inspect `PublisherPackageTests.cs`
- inspect `NativeSqliteRuntime` in `SemanticPlcGraph.cs`
- verify helper output folder contains runtime DLLs
- prefer helper process generation in add-ins

### Stale Package Artifacts

The repo may contain generated `.addin`, publisher logs, status files, and release zips. Rebuild before publishing. Do not assume an existing package is current just because it exists.

### Legacy Artifact Confusion

Older generated exports and older notes may mention JSON/JSONL artifacts no longer emitted by the current main pipeline. Always check current `PlcExportService.Export(...)` and current export folder contents.

## 15. Modification Playbooks

### Add Or Change An Exported Artifact

1. Add/update a focused test in `PlcExportServiceTests.cs`.
2. Implement the writer in `PlcSourceExporter.Core`.
3. Wire it into `PlcExportService.Export(...)` after metadata is available if it depends on exported files.
4. Add summary fields to `ExportSummary` if callers need paths.
5. Update `AiExportGuide.cs`, repo-root `AI_EXPORT_GUIDE.md`, and `README.md`.
6. Run core tests.
7. Regenerate a live export if behavior depends on real Siemens XML.

### Improve LAD/FBD Translation

1. Add a test case that contains the exact Siemens XML shape.
2. Keep parsing in `ProgramBlockLogicYamlWriter.cs`.
3. Prefer structured XML traversal over string matching.
4. Emit notes for unresolved or partially supported shapes.
5. Preserve deterministic statement ordering.
6. Run tests and inspect YAML weak markers on a real export.

Known tricky XML shapes:

- constants may appear as `<Constant Name="..." />`, not only `<ConstantValue>...`
- array operands can be nested under `<Component AccessModifier="Array">`
- `Move` and similar parts need output-pin-to-`IdentCon` direct assignment extraction
- part output wires can drive tags without a coil
- pin names and part names must be compared case-insensitively

### Change The Semantic Graph

1. Add or update tests in `SemanticPlcGraphTests.cs`.
2. Change `SemanticNodeKind`, `SemanticRelationshipType`, import logic, or edge properties as needed.
3. If schema changes, update `PlcSemanticGraphSqliteSchema.CreateScript`.
4. Update embedded `SemanticPlcGraphAgentGuide.Content`.
5. Update export-local and repo-root guide text if the reasoning workflow changes.
6. Verify SQLite save/load roundtrip tests.
7. Verify a live export with `PRAGMA integrity_check`.

### Change Add-In UI Or Progress

1. Inspect `ExportAddInWorkflow.cs`, `ExportProgressWindow.cs`, and `ExportProgressForm.cs`.
2. Keep UI-thread interactions inside `ExportProgressWindow`.
3. Preserve cancellation behavior.
4. Preserve final wait-for-close behavior unless the user explicitly wants control returned immediately.
5. Build the matching add-in project.
6. Publish and install only the targeted TIA version.
7. Test inside TIA Portal.

### Change V17 Or V20 Adapter Behavior

1. Decide whether the change applies to one version or both.
2. Change version-specific files only where needed.
3. Keep shared enumeration in `SiemensShared` when behavior is version-neutral.
4. Build the affected adapter, harness, and add-in.
5. Verify with the matching TIA version.

### Change Packaging Or Release Contents

1. Update the matching `package\PlcSourceExporter.V*.publisher.xml`.
2. Add tests in `PublisherPackageTests.cs` for permission and dependency constraints.
3. Build the affected add-in and `ExportAnalyzer`.
4. Run the publisher command.
5. Install to Program Files with Administrator PowerShell.
6. Verify installed package hash and helper folder contents.
7. Restart TIA and test the menu.

## 16. Best Practices For Future AI Work

- Read current files before editing. This repo has evolved quickly and older notes can be stale.
- Keep source-faithfulness above clever decompilation.
- Treat raw TIA XML as final proof for exact source shape.
- Treat SQLite as the primary AI reasoning surface.
- Treat YAML translation as a readable mirror with confidence and notes.
- Treat optimization hints as leads, not bugs.
- Use tests first for parser and translator changes.
- Keep version-specific Siemens API references out of `PlcSourceExporter.Core`.
- Do not mutate parent workspace files unless the user explicitly asks.
- Do not broaden export-root deletion behavior.
- Do not claim an installed add-in is updated until you verify the installed file or the user confirms installation.
- Do not claim a live export is healthy until the log and artifact set are checked.
- Keep `README.md`, `AI_EXPORT_GUIDE.md`, and `AiExportGuide.cs` aligned when artifact names or workflows change.
- Prefer explicit, deterministic serialization. Current JSON/YAML writers are hand-written for stable output and minimal dependencies.
- Keep PowerShell commands Windows-native and path-literal where possible.

## 17. AI Agent Capabilities Useful In This Repo

A capable follow-on agent should be able to:

- inspect C# source and tests with `rg` and `Get-Content`
- edit files with small scoped patches
- run `dotnet test` and targeted builds
- read Siemens-generated XML without guessing semantics
- inspect generated export folders and logs
- query or validate SQLite graph files
- run V17/V20 harnesses when TIA is installed and accessible
- publish `.addin` packages with Siemens Add-In Publisher
- request elevation before Program Files install operations
- compare package and installed hashes
- keep user changes in the working tree intact

The agent should not:

- use V20 packages in V17
- infer safety behavior from names
- silently delete export roots outside the intended project export folder
- collapse `unknown` access into read/write
- overwrite uncommitted user work
- publish release zips without checking helper runtime contents

## 18. Future Work Already Planned

There is an implementation plan at:

```text
docs\superpowers\plans\2026-07-13-tia-inline-translation-comments.md
```

Goal of that plan: add an explicit TIA workflow that reads `translate\program-blocks.yaml` and writes translated network statements back into matching program block network comments, with backups and staged XML.

Important: that plan describes a future mutating workflow. The current normal export command remains read/export oriented.

Before implementing that plan, validate the exact Siemens Openness import API for V17 and V20. Do not guess the import method or override options.

## 19. Command Cheat Sheet

Repo root:

```powershell
cd "C:\Users\Ansel\Documents\Siemens TIA Add-in Dev\PlcSourceExporter"
```

Status:

```powershell
git status --short
```

Core tests:

```powershell
dotnet test .\tests\PlcSourceExporter.Core.Tests\PlcSourceExporter.Core.Tests.csproj --no-restore
```

Static graph restore:

```powershell
dotnet restore .\PlcSourceExporter.sln /p:RestoreUseStaticGraphEvaluation=true
```

Build V17 add-in:

```powershell
dotnet build .\src\PlcSourceExporter.AddIn.V17\PlcSourceExporter.AddIn.V17.csproj --no-restore
```

Build V20 add-in:

```powershell
dotnet build .\src\PlcSourceExporter.AddIn\PlcSourceExporter.AddIn.csproj --no-restore
```

Build helper:

```powershell
dotnet build .\src\PlcSourceExporter.ExportAnalyzer\PlcSourceExporter.ExportAnalyzer.csproj --no-restore
```

Publish V17 add-in:

```powershell
& "C:\Program Files\Siemens\Automation\Portal V17\PublicAPI\V17.AddIn\Siemens.Engineering.AddIn.Publisher.exe" `
  --configuration ".\package\PlcSourceExporter.V17.publisher.xml" `
  --logfile ".\package\PlcSourceExporter.V17.publisher.log" `
  --outfile ".\package\PlcSourceExporter.V17.addin" `
  --verbose `
  --console
```

Publish V20 add-in:

```powershell
& "C:\Program Files\Siemens\Automation\Portal V20\PublicAPI\V20.AddIn\Siemens.Engineering.AddIn.Publisher.exe" `
  --configuration ".\package\PlcSourceExporter.V20.publisher.xml" `
  --logfile ".\package\PlcSourceExporter.V20.publisher.log" `
  --outfile ".\package\PlcSourceExporter.V20.addin" `
  --verbose `
  --console
```

Install V17 release folder as Administrator:

```powershell
.\package\install-v17-addin.ps1
```

Manual helper run:

```powershell
.\src\PlcSourceExporter.ExportAnalyzer\bin\Debug\net48\PlcSourceExporter.ExportAnalyzer.exe --export-root "C:\Path\To\UserFiles\export" --model-only
```

## 20. Final Checklist Before Handing Off Again

Before claiming a change is complete:

1. Check `git status --short`.
2. Confirm which files you changed and which were pre-existing changes.
3. Run the narrowest relevant tests.
4. For core changes, run the whole core test project if feasible.
5. For packaging changes, rebuild, publish, and verify package contents.
6. For installed add-in changes, verify installed hashes or installed file timestamps.
7. For live export behavior, check `PlcSourceExporter.log` and artifact existence.
8. Update `README.md`, `AI_EXPORT_GUIDE.md`, and `AiExportGuide.cs` if user-facing/export-facing behavior changed.
9. State clearly what was not verified.

