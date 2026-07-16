# TIA Inline Translation Comments Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a TIA add-in workflow that reads `translate\program-blocks.yaml` and writes each translated network statement back into the matching program block network comments so translation quality can be inspected directly inside TIA Portal.

**Architecture:** Keep all YAML parsing and XML comment patching in `PlcSourceExporter.Core` so it is testable without TIA. Add a small TIA-facing import abstraction in the shared Siemens layer, then expose one explicit context-menu command that backs up original XML, patches exported block XML, and imports only changed blocks back into TIA under exclusive access.

**Tech Stack:** C# `netstandard2.0` core library, xUnit tests, Siemens TIA Openness V17/V20 add-ins, XML via `System.Xml.Linq`, YAML parsing via a small purpose-built reader for the existing generated schema or `YamlDotNet` if already accepted for the project.

---

## File Structure

- Modify: `src/PlcSourceExporter.Core/ProgramBlockLogicYamlWriter.cs`
  - Reuse the existing schema names and, if needed, expose a shared constant for the generated translation comment prefix.
- Create: `src/PlcSourceExporter.Core/ProgramBlockTranslationAnnotations.cs`
  - Parse `translate\program-blocks.yaml` into block/network annotations keyed by `sourceFile` plus `compileUnitId`.
  - Format translated statements as deterministic network comment text.
- Create: `src/PlcSourceExporter.Core/ProgramBlockNetworkCommentPatcher.cs`
  - Patch exported block XML by adding or replacing a managed translation block inside each `SW.Blocks.CompileUnit` `Comment` multilingual text.
  - Preserve existing human network comments outside the managed marker block.
- Create: `src/PlcSourceExporter.Core/ProgramBlockTranslationImportPlanner.cs`
  - Compare annotation data with exported XML files, write patched XML files to a staging folder, and return a list of changed block files.
- Create: `src/PlcSourceExporter.Core/ProgramBlockTranslationImportResult.cs`
  - Hold counts for matched networks, changed networks, skipped networks, changed blocks, and staged files.
- Modify: `src/PlcSourceExporter.Core/PlcExportContracts.cs`
  - Add `IPlcBlockSourceImporter` with a method that imports a staged XML block source into the selected PLC software.
- Modify: `src/PlcSourceExporter.SiemensShared/SiemensPlcSoftwareSource.cs`
  - Add a Siemens implementation for locating block groups and importing staged XML with override semantics after a validation spike confirms the exact Openness API.
- Modify: `src/PlcSourceExporter.AddInShared/ExportAddInWorkflow.cs`
  - Add a sibling workflow for `ImportTranslationComments`.
- Modify: `src/PlcSourceExporter.AddIn.V17/ExportPlcSourceDataContextMenu.cs`
- Modify: `src/PlcSourceExporter.AddIn/ExportPlcSourceDataContextMenu.cs`
  - Add a second menu item such as `Import Translation Comments`.
- Add tests under: `tests/PlcSourceExporter.Core.Tests/ProgramBlockTranslationAnnotationsTests.cs`
- Add tests under: `tests/PlcSourceExporter.Core.Tests/ProgramBlockNetworkCommentPatcherTests.cs`
- Add tests under: `tests/PlcSourceExporter.Core.Tests/ProgramBlockTranslationImportPlannerTests.cs`
- Modify docs: `README.md` and `AI_EXPORT_GUIDE.md`
  - Document that this command mutates TIA block comments and should be run on a backed-up project first.

## Design Decisions

- Treat this as an explicit import command, not part of normal export. Exporting should remain read-mostly and predictable.
- Write translation text into network `Comment`, not `Title`, because the user wants it displayed together with network comments and titles are often used for original engineering labels.
- Preserve the original network comment by wrapping generated text in markers:

```text
[PlcSourceExporter translation begin]
<translated statement 1>
<translated statement 2>
confidence: partial
notes:
- Skipped assignment because operand could not be resolved.
[PlcSourceExporter translation end]
```

- If a marker block already exists, replace only that block. If no marker exists, append after the human comment with a blank line separator.
- Match annotations by `sourceFile` plus `compileUnitId` first. Fall back to `networkIndex` only when the compile unit ID is missing in both YAML and XML.
- Stage modified XML under `UserFiles\export\_tia-comment-import\yyyyMMdd-HHmmss\` and keep a copy of the original XML there before importing.
- Require the generated `translate\program-blocks.yaml` and matching exported XML files to exist. Do not retranslate from XML during import.

### Task 1: Validate TIA Import API

**Files:**
- Inspect only: installed Siemens Openness assemblies and local sample material.
- Document result in: `docs/superpowers/plans/2026-07-13-tia-inline-translation-comments.md`

- [ ] **Step 1: Search installed/sample API usage**

Run:

```powershell
rg -n "\.Import\(|ImportOptions|PlcBlockGroup" "C:\Users\Ansel\Documents\Siemens TIA Add-in Dev" -S
```

Expected: either a local sample showing block import or no hits requiring reflection inspection.

- [ ] **Step 2: Confirm method signature from assemblies**

Run a short reflection check against the V17 and V20 Siemens assemblies available on this PC. The target methods to confirm are on the block composition behind `PlcBlockGroup.Blocks`, typically an `Import(FileInfo, ImportOptions)` overload or equivalent.

Expected: exact import method name, argument types, and override option recorded before implementation.

- [ ] **Step 3: Update this plan with the confirmed call**

Replace this sentence after validation: `Siemens implementation uses the confirmed block import method and override option from Step 2.`

### Task 2: Parse `program-blocks.yaml`

**Files:**
- Create: `src/PlcSourceExporter.Core/ProgramBlockTranslationAnnotations.cs`
- Test: `tests/PlcSourceExporter.Core.Tests/ProgramBlockTranslationAnnotationsTests.cs`

- [ ] **Step 1: Write failing parser tests**

Test cases:

```csharp
[Fact]
public void LoadsAnnotationsBySourceFileAndCompileUnitId()
{
    var yaml = """
        schemaVersion: "1.0"
        generatedUtc: "2026-07-13T00:00:00.0000000Z"
        exportRoot: "D:\\Project\\UserFiles\\export"
        blocks:
          - name: "Main"
            kind: "OB"
            sourceFile: "Blocks\\Main.xml"
            programmingLanguage: "LAD"
            networks:
              - networkIndex: 1
                compileUnitId: "4"
                title: "Physical input mapping"
                language: "LAD"
                translation:
                  language: "scl-like"
                  confidence: "partial"
                  statements:
                    - "Out1 := In1;"
                  notes:
                    - "One note"
        """;

    var result = ProgramBlockTranslationAnnotations.Parse(yaml);

    var annotation = Assert.Single(result.Blocks).Networks.Single();
    Assert.Equal("Blocks\\Main.xml", result.Blocks.Single().SourceFile);
    Assert.Equal("4", annotation.CompileUnitId);
    Assert.Equal("Out1 := In1;", Assert.Single(annotation.Statements));
    Assert.Equal("partial", annotation.Confidence);
}
```

- [ ] **Step 2: Implement the minimal parser**

Use a deterministic parser for this known YAML shape. If adding `YamlDotNet`, add it only to `PlcSourceExporter.Core.csproj` and keep the parsed DTO internal to Core.

- [ ] **Step 3: Run parser tests**

Run:

```powershell
dotnet test .\tests\PlcSourceExporter.Core.Tests\PlcSourceExporter.Core.Tests.csproj --no-restore --filter ProgramBlockTranslationAnnotationsTests
```

Expected: new parser tests pass.

### Task 3: Patch Network Comments in XML

**Files:**
- Create: `src/PlcSourceExporter.Core/ProgramBlockNetworkCommentPatcher.cs`
- Test: `tests/PlcSourceExporter.Core.Tests/ProgramBlockNetworkCommentPatcherTests.cs`

- [ ] **Step 1: Write failing XML patch tests**

Test cases:

```csharp
[Fact]
public void AppendsManagedTranslationToExistingNetworkComment()
{
    var xml = """
        <Document>
          <SW.Blocks.FC ID="0">
            <ObjectList>
              <SW.Blocks.CompileUnit ID="4">
                <ObjectList>
                  <MultilingualText CompositionName="Comment">
                    <ObjectList>
                      <MultilingualTextItem>
                        <AttributeList><Culture>en-US</Culture><Text>Original operator note</Text></AttributeList>
                      </MultilingualTextItem>
                    </ObjectList>
                  </MultilingualText>
                </ObjectList>
              </SW.Blocks.CompileUnit>
            </ObjectList>
          </SW.Blocks.FC>
        </Document>
        """;

    var annotation = new ProgramBlockNetworkTranslation(
        networkIndex: 1,
        compileUnitId: "4",
        title: "Network",
        confidence: "exact",
        statements: new[] { "Pump.Run := Pump.Enable;" },
        notes: Array.Empty<string>());

    var patched = ProgramBlockNetworkCommentPatcher.Patch(xml, new[] { annotation });

    Assert.Contains("Original operator note", patched.Xml);
    Assert.Contains("[PlcSourceExporter translation begin]", patched.Xml);
    Assert.Contains("Pump.Run := Pump.Enable;", patched.Xml);
    Assert.True(patched.Changed);
}
```

- [ ] **Step 2: Implement comment insertion**

Implement:

```csharp
public static ProgramBlockNetworkCommentPatchResult Patch(
    string xml,
    IReadOnlyList<ProgramBlockNetworkTranslation> annotations)
```

Behavior:
- Find `SW.Blocks.CompileUnit`.
- Match `ID` to `compileUnitId`.
- Locate or create `ObjectList/MultilingualText[@CompositionName='Comment']/ObjectList/MultilingualTextItem/AttributeList`.
- Prefer existing culture `en-US`, then `en-GB`, then first item; create `en-US` if no comment item exists.
- Replace existing managed marker block or append a new one.
- Return unchanged XML when there are no matching networks.

- [ ] **Step 3: Add idempotency and preservation tests**

Add tests for:
- running patch twice does not duplicate generated text;
- comments without marker are preserved byte-for-byte inside the text value;
- missing compile unit is counted as skipped;
- empty `statements` with notes writes notes and confidence, not a fake statement.

- [ ] **Step 4: Run patch tests**

Run:

```powershell
dotnet test .\tests\PlcSourceExporter.Core.Tests\PlcSourceExporter.Core.Tests.csproj --no-restore --filter ProgramBlockNetworkCommentPatcherTests
```

Expected: patch tests pass.

### Task 4: Plan Changed Block Imports

**Files:**
- Create: `src/PlcSourceExporter.Core/ProgramBlockTranslationImportPlanner.cs`
- Create: `src/PlcSourceExporter.Core/ProgramBlockTranslationImportResult.cs`
- Test: `tests/PlcSourceExporter.Core.Tests/ProgramBlockTranslationImportPlannerTests.cs`

- [ ] **Step 1: Write failing planner test**

Arrange a temp export root with:
- `translate\program-blocks.yaml`
- `Blocks\Main.xml`
- metadata only if needed for source-path diagnostics.

Assert:
- staged patched XML exists under `_tia-comment-import`;
- original XML copy exists under the same staging run;
- result counts one changed block and one changed network.

- [ ] **Step 2: Implement planner**

Implement:

```csharp
public static ProgramBlockTranslationImportResult Plan(
    string exportRoot,
    DateTimeOffset generatedUtc)
```

Behavior:
- Read `translate\program-blocks.yaml`.
- For each block annotation, resolve `sourceFile` under `exportRoot`.
- Patch XML with `ProgramBlockNetworkCommentPatcher`.
- If changed, write original and patched XML into the staging folder.
- Return `ProgramBlockTranslationImportResult` with exact changed files.

- [ ] **Step 3: Run planner tests**

Run:

```powershell
dotnet test .\tests\PlcSourceExporter.Core.Tests\PlcSourceExporter.Core.Tests.csproj --no-restore --filter ProgramBlockTranslationImportPlannerTests
```

Expected: planner tests pass.

### Task 5: Add Shared Import Contract

**Files:**
- Modify: `src/PlcSourceExporter.Core/PlcExportContracts.cs`
- Modify: `src/PlcSourceExporter.SiemensShared/SiemensPlcSoftwareSource.cs`

- [ ] **Step 1: Add Core import contract**

Add:

```csharp
public interface IPlcBlockSourceImporter
{
    void ImportBlockSource(string stagedXmlFilePath, IExportLogger logger);
}
```

- [ ] **Step 2: Add Siemens importer implementation**

Create a Siemens shared importer that takes `PlcSoftware` and imports staged XML files into the selected PLC block group using the confirmed API from Task 1.

Expected behavior:
- import with override/update semantics;
- log every imported staged XML file;
- throw a meaningful exception for unsupported import failures.

- [ ] **Step 3: Build V17 and V20 adapters**

Run:

```powershell
dotnet build .\src\PlcSourceExporter.TiaV17\PlcSourceExporter.TiaV17.csproj --no-restore
dotnet build .\src\PlcSourceExporter.TiaV20\PlcSourceExporter.TiaV20.csproj --no-restore
```

Expected: both adapter projects compile.

### Task 6: Add Add-In Command

**Files:**
- Modify: `src/PlcSourceExporter.AddInShared/ExportAddInWorkflow.cs`
- Modify: `src/PlcSourceExporter.AddIn.V17/ExportPlcSourceDataContextMenu.cs`
- Modify: `src/PlcSourceExporter.AddIn/ExportPlcSourceDataContextMenu.cs`

- [ ] **Step 1: Add shared workflow**

Add:

```csharp
public static void ImportTranslationComments(
    string deviceName,
    string exportRoot,
    string logFile,
    IExportLogger logger,
    IPlcBlockSourceImporter importer,
    Func<IDisposable?> acquireExclusiveAccess)
```

Behavior:
- open progress window titled `Import Translation Comments`;
- acquire exclusive access;
- call `ProgramBlockTranslationImportPlanner.Plan(exportRoot, DateTimeOffset.UtcNow)`;
- import every staged patched XML file;
- log changed/skipped counts;
- show completion text with changed block count.

- [ ] **Step 2: Add V17/V20 menu item**

Add a second action item:

```csharp
addInRoot.Items.AddActionItem<DeviceItem>(
    "Import Translation Comments",
    OnImportTranslationCommentsClick,
    OnUpdateStatus);
```

The click handler resolves the same PLC software and export root as `Export PLC Source Data`, then calls the new shared workflow.

- [ ] **Step 3: Build add-ins**

Run:

```powershell
dotnet build .\src\PlcSourceExporter.AddIn.V17\PlcSourceExporter.AddIn.V17.csproj --no-restore
dotnet build .\src\PlcSourceExporter.AddIn\PlcSourceExporter.AddIn.csproj --no-restore
```

Expected: both add-in projects compile.

### Task 7: Documentation and Safety Notes

**Files:**
- Modify: `README.md`
- Modify: `AI_EXPORT_GUIDE.md`

- [ ] **Step 1: Update README**

Document the workflow:

```text
1. Run Export PLC Source Data.
2. Inspect or regenerate translate\program-blocks.yaml.
3. Run Import Translation Comments.
4. Open the affected blocks in TIA and inspect network comments.
```

Include warning: this command writes comments into the TIA project and should be used on a copied project until the output is trusted.

- [ ] **Step 2: Update AI guide**

Mention that `program-blocks.yaml` can now be projected back into TIA network comments via the import command, but `model\plc-graph.sqlite` remains the durable analysis source.

### Task 8: Verification

**Files:**
- No new files unless packaging is updated.

- [ ] **Step 1: Run Core tests**

Run:

```powershell
dotnet test .\tests\PlcSourceExporter.Core.Tests\PlcSourceExporter.Core.Tests.csproj --no-restore
```

Expected: all Core tests pass.

- [ ] **Step 2: Run solution build**

Run:

```powershell
dotnet build .\PlcSourceExporter.sln --no-restore
```

Expected: solution builds with `0 Error(s)`.

- [ ] **Step 3: Live V17 validation**

On a copied test project:
- install the rebuilt V17 add-in package;
- run `Export PLC Source Data`;
- run `Import Translation Comments`;
- open at least one patched LAD/FBD block in TIA;
- verify the network comment contains original comment plus the managed translation block;
- rerun `Import Translation Comments` and verify the comment does not duplicate.

- [ ] **Step 4: Rollback check**

Use the staged original XML backup from `_tia-comment-import\...` to restore one block through the same import path.

Expected: the managed generated translation block is removed and the original network comment is restored.

## Execution Order

1. Validate import API.
2. Implement Core parser.
3. Implement Core XML patcher.
4. Implement import planner and tests.
5. Add Siemens importer.
6. Add add-in menu workflow.
7. Update docs.
8. Run offline tests/build.
9. Perform live V17 test on a copied project.

## Risks

- TIA may not accept imported XML when only comments changed if the XML is not exported with exactly compatible options. Mitigation: use the same `ExportOptions.WithDefaults` source files and validate import API before implementation.
- Network comment XML shape may vary by TIA version or block language. Mitigation: create missing comment elements in the same `MultilingualText` shape used by Openness exports and keep tests for both existing-comment and no-comment cases.
- Importing comments mutates the project. Mitigation: make it an explicit command, stage backups, log every changed block, and validate first on a copied project.
- `program-blocks.yaml` may be stale relative to exported XML. Mitigation: match by source file and compile unit ID; skip missing/mismatched networks with clear log warnings.

## Self-Review

- Spec coverage: The plan displays translated statements directly in TIA by importing them into network comments and preserving existing comments.
- Test coverage: Parser, XML patching, idempotency, planner output, build, and live TIA validation are covered.
- Boundary check: Core stays TIA-independent; TIA import behavior stays in Siemens/add-in layers.
- Placeholder scan: No implementation task depends on unspecified behavior except the explicit Task 1 API validation spike.
