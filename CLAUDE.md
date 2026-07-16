# CLAUDE.md

## Build

```powershell
# V17
dotnet build .\src\PlcSourceExporter.Core\PlcSourceExporter.Core.csproj
dotnet build .\src\PlcSourceExporter.TiaV17\PlcSourceExporter.TiaV17.csproj --no-restore
dotnet build .\src\PlcSourceExporter.TestHarness.V17\PlcSourceExporter.TestHarness.V17.csproj --no-restore
dotnet build .\src\PlcSourceExporter.ExportAnalyzer\PlcSourceExporter.ExportAnalyzer.csproj --no-restore
dotnet build .\src\PlcSourceExporter.AddIn.V17\PlcSourceExporter.AddIn.V17.csproj --no-restore

# V20
dotnet build .\src\PlcSourceExporter.Core\PlcSourceExporter.Core.csproj
dotnet build .\src\PlcSourceExporter.TiaV20\PlcSourceExporter.TiaV20.csproj --no-restore
dotnet build .\src\PlcSourceExporter.TestHarness\PlcSourceExporter.TestHarness.csproj --no-restore
dotnet build .\src\PlcSourceExporter.ExportAnalyzer\PlcSourceExporter.ExportAnalyzer.csproj --no-restore
dotnet build .\src\PlcSourceExporter.AddIn\PlcSourceExporter.AddIn.csproj --no-restore
```

If restore fails: `dotnet restore .\PlcSourceExporter.sln /p:RestoreUseStaticGraphEvaluation=true`

## Test

All core tests (no TIA required):
```powershell
dotnet test .\tests\PlcSourceExporter.Core.Tests\PlcSourceExporter.Core.Tests.csproj --no-restore
```

Key test areas:
- `PlcExportServiceTests.cs` — export flow and LAD/FBD/SCL translation
- `SemanticPlcGraphTests.cs` — SQLite graph import, save/load roundtrip
- `PublisherPackageTests.cs` — add-in package dependency and permission assertions
- `ProgramSemanticReferenceTests.cs` — XML reference parsing
- `BlockProfilesAndHintsTests.cs` — block profiles and optimization hints
- `ExportPathPlannerTests.cs` — filename sanitization and duplicate naming
- `ExportDirectoryPreparerTests.cs` — export folder cleaning
- `TagTableTests.cs` / `UdtTypeTableTests.cs` — tag/UDT XML parsing

## Architecture

Siemens TIA Portal V17/V20 add-in that exports PLC source code to raw XML + translated YAML + SQLite semantic graph. Read-only — does not write PLC logic back.

```
TIA project tree selection
  -> V17/V20 add-in or harness resolves PlcSoftware
  -> SiemensShared enumerates blocks, UDTs, tag tables recursively
  -> Core exports XML into flat category folders (Blocks/, DB/, UDT/, Tags/)
  -> Core writes metadata.json
  -> Core writes translate/program-blocks.yaml (LAD/FBD/SCL -> SCL-like statements)
  -> Core/add-in helper writes model/plc-graph.sqlite + schema.sql + AGENT_SQLITE_GUIDE.md
  -> Core writes block-profiles.jsonl, optimization-hints.jsonl, AI_EXPORT_GUIDE.md
```

### Projects

- `PlcSourceExporter.Core` (netstandard2.0) — version-neutral engine. **Must not reference Siemens assemblies.**
- `PlcSourceExporter.TiaV17` / `PlcSourceExporter.TiaV20` (net48) — version-specific Openness adapters using standard typed Siemens API calls.
- `PlcSourceExporter.SiemensShared` — linked source (not standalone project) shared by both adapters for recursive block/type/tag enumeration via `SiemensPlcSoftwareSource.cs`.
- `PlcSourceExporter.AddIn.V17` / `PlcSourceExporter.AddIn` (net48) — TIA context-menu add-ins using reflection-based export.
- `PlcSourceExporter.AddInShared` — linked source for progress UI (`ExportProgressWindow`, `ExportProgressForm`) and workflow orchestration (`ExportAddInWorkflow.cs`, `AddInSemanticPlcModelWriter.cs`).
- `PlcSourceExporter.ExportAnalyzer` (net48, x64) — helper EXE launched from add-ins to generate the SQLite model outside the TIA sandbox.
- `PlcSourceExporter.TestHarness.V17` / `PlcSourceExporter.TestHarness` (net48) — console runners for live TIA testing.

### Key Constraints

1. **Core must not reference Siemens assemblies.** Keep it netstandard2.0. All Siemens API boundaries go through `IPlcSoftwareSource`, `IPlcExportableObject`, etc.
2. **Add-in process launch must use `Siemens.Engineering.AddIn.Utilities.Process`**, not `System.Diagnostics.Process`. The TIA add-in sandbox blocks normal process start.
3. **Never mix V17 and V20.** Deploy each `.addin` only to its matching TIA installation. V17 publisher XML includes SQLite runtime DLLs; V20 publisher XML is **incomplete** — missing those DLLs and `ProcessStartPermission`.
4. **Do not mutate parent workspace** (`Siemens TIA Add-in Dev/`). Repo boundary is this folder.
5. **Export directory cleaner is scoped to the export root only** — do not broaden deletion.
6. **Raw TIA XML is ground truth.** YAML translation confidence levels: `exact` / `partial` / `untranslated`. SQLite `Network` nodes may also carry `logicStatements` property.
7. **Prefer SQLite graph for agent queries** (`model/plc-graph.sqlite`). Use `translate/program-blocks.yaml` for human-readable logic with confidence/notes. Use raw XML as final proof for `partial`/`untranslated` networks.

### Key Files

- `src/PlcSourceExporter.Core/PlcExportService.cs` — main export pipeline (12-step process)
- `src/PlcSourceExporter.Core/ProgramBlockLogicYamlWriter.cs` — LAD/FBD/SCL → YAML translation
- `src/PlcSourceExporter.Core/SemanticPlcGraph.cs` — SQLite graph schema, importer, queries, native SQLite runtime (largest file)
- `src/PlcSourceExporter.Core/PlcExportContracts.cs` — core abstractions (`IPlcSoftwareSource`, `IExportLogger`, `ISemanticPlcModelWriter`)
- `src/PlcSourceExporter.Core/ExportCategory.cs` — fixed category/folder mapping (OB/FC/FB→Blocks, DB→DB, UDT→UDT, Tags→Tags)
- `src/PlcSourceExporter.Core/ProgramBlockComponentCatalog.cs` — reads metadata.json for program block listing (OB→FC→FB ordering)
- `src/PlcSourceExporter.Core/ProgramBlockProfile.cs` — writes block-profiles.jsonl and optimization-hints.jsonl
- `src/PlcSourceExporter.Core/ProgramSemanticReference.cs` — parses XML to network/reference records
- `src/PlcSourceExporter.Core/ExportMetadata.cs` — writes metadata.json (schema v1.0)
- `src/PlcSourceExporter.Core/AiExportGuide.cs` — writes export-local AI_EXPORT_GUIDE.md
- `src/PlcSourceExporter.SiemensShared/SiemensPlcSoftwareSource.cs` — recursive block/type/tag enumeration
- `src/PlcSourceExporter.AddInShared/ExportAddInWorkflow.cs` — add-in orchestration
- `src/PlcSourceExporter.AddInShared/AddInSemanticPlcModelWriter.cs` — spawns ExportAnalyzer via Siemens process API
- `AI_AGENT_HANDOFF.md` — comprehensive orientation for AI agents (read for deeper context)

### Testing Strategy

- Run core tests after nearly every code change: `dotnet test .\tests\PlcSourceExporter.Core.Tests\PlcSourceExporter.Core.Tests.csproj --no-restore`
- For translation work: add failing test in `PlcExportServiceTests.cs`, implement change, test, then verify on live export.
- For semantic graph changes: add test in `SemanticPlcGraphTests.cs`, verify SQLite save/load roundtrip.
- Live TIA verification required for: Siemens adapter changes, add-in menus, project/PLC resolution, package XML, install layout, permission changes.

### Export Layout

```
<TIA project folder>\UserFiles\export\
  Blocks\         — OB, FC, FB XML files (flat, nested groups flattened)
  DB\             — data block XML files
  UDT\            — user data type XML files
  Tags\           — tag table XML files
  metadata.json   — schema v1.0, export inventory
  translate\program-blocks.yaml — LAD/FBD/SCL translation (v1.0)
  model\plc-graph.sqlite        — semantic graph database
  model\schema.sql               — SQLite schema reference
  model\AGENT_SQLITE_GUIDE.md   — SQLite query guide for AI agents
  block-profiles.jsonl           — compact block summaries
  optimization-hints.jsonl       — review leads (multi_writer, never_read, etc.)
  AI_EXPORT_GUIDE.md            — per-export AI reading guide
  PlcSourceExporter.log          — export progress log
```

### Add-In Packaging

V17 publisher:
```powershell
& "C:\Program Files\Siemens\Automation\Portal V17\PublicAPI\V17.AddIn\Siemens.Engineering.AddIn.Publisher.exe" `
  --configuration ".\package\PlcSourceExporter.V17.publisher.xml" `
  --logfile ".\package\PlcSourceExporter.V17.publisher.log" `
  --outfile ".\package\PlcSourceExporter.V17.addin" `
  --verbose --console
```
Install: copy the `PlcSourceExporter` folder (addin + `ExportAnalyzer\`) into `C:\Program Files\Siemens\Automation\Portal V17\AddIns\`. See `package/release/v1.0.0-tia17/` for the reference release layout.

V20 publisher: same pattern with V20 paths. **V20 add-in is not ready for release** — publisher XML is incomplete.

### Useful Patterns

- All JSON/YAML writers are hand-written for deterministic output — no serialization library dependency in Core.
- Duplicate object names get deterministic `_2`, `_3` suffixes via `ExportPathPlanner`.
- Invalid filename chars are replaced with `_`.
- Export continues after individual failures — records successes, skips, failures in both `ExportSummary` and `metadata.json`.
- Siemens "export of block ... not permitted" errors = skipped, not failed.
- Obsolete artifact names (not emitted by current pipeline): `tags.json`, `udt.json`, `callgraph.json`, `calltree.md`, `networks.jsonl`, `references.jsonl`. Some builders still exist for tests/back-compat.
