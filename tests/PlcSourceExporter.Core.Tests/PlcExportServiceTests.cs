using PlcSourceExporter.Core;

namespace PlcSourceExporter.Core.Tests;

public sealed class PlcExportServiceTests
{
    [Fact]
    public void ExportsNestedObjectsToFlatCategoryFolders()
    {
        var root = Path.Combine(Path.GetTempPath(), "PlcSourceExporterTests", Guid.NewGuid().ToString("N"));
        var service = new PlcExportService();
        var plc = FakePlcSoftware.Create(
            blocks:
            [
                new FakeExportableObject("Program/OB1", "OrganizationBlock"),
                new FakeExportableObject("Program/Nested/Motor", "FunctionBlock"),
                new FakeExportableObject("Program/Nested/Scale", "Function"),
                new FakeExportableObject("Program/Config", "DataBlock")
            ],
            types: [new FakeExportableObject("Types/ValveType", "PlcType")],
            tagTables: [new FakeExportableObject("Tags/Default tag table", "PlcTagTable")]);

        var summary = service.Export(plc, root);

        Assert.Equal(6, summary.SuccessCount);
        Assert.True(File.Exists(Path.Combine(root, "Blocks", "OB1.xml")));
        Assert.True(File.Exists(Path.Combine(root, "Blocks", "Motor.xml")));
        Assert.True(File.Exists(Path.Combine(root, "Blocks", "Scale.xml")));
        Assert.True(File.Exists(Path.Combine(root, "DB", "Config.xml")));
        Assert.True(File.Exists(Path.Combine(root, "UDT", "ValveType.xml")));
        Assert.True(File.Exists(Path.Combine(root, "Tags", "Default tag table.xml")));
        Assert.Empty(Directory.GetDirectories(Path.Combine(root, "Blocks")));
    }

    [Fact]
    public void ContinuesAfterAnObjectExportFails()
    {
        var root = Path.Combine(Path.GetTempPath(), "PlcSourceExporterTests", Guid.NewGuid().ToString("N"));
        var service = new PlcExportService();
        var plc = FakePlcSoftware.Create(
            blocks:
            [
                new FakeExportableObject("OB1", "OrganizationBlock"),
                new FakeExportableObject("Secret", "FunctionBlock", shouldThrow: true)
            ],
            types: [],
            tagTables: []);

        var summary = service.Export(plc, root);

        Assert.Equal(1, summary.SuccessCount);
        Assert.Equal(1, summary.FailureCount);
        Assert.True(File.Exists(Path.Combine(root, "Blocks", "OB1.xml")));
    }

    [Fact]
    public void SkipsObjectsThatDeclareSkipReason()
    {
        var root = Path.Combine(Path.GetTempPath(), "PlcSourceExporterTests", Guid.NewGuid().ToString("N"));
        var service = new PlcExportService();
        var plc = FakePlcSoftware.Create(
            blocks:
            [
                new FakeExportableObject("OB1", "OrganizationBlock"),
                new FakeExportableObject(
                    "ProDiagOB",
                    "OrganizationBlock",
                    skipReason: "Programming language 'ProDiag_OB' is not supported by Siemens Openness XML export.")
            ],
            types: [],
            tagTables: []);

        var summary = service.Export(plc, root);

        Assert.Equal(1, summary.SuccessCount);
        Assert.Equal(1, summary.SkippedCount);
        Assert.Equal(0, summary.FailureCount);
        Assert.True(File.Exists(Path.Combine(root, "Blocks", "OB1.xml")));
        Assert.False(File.Exists(Path.Combine(root, "Blocks", "ProDiagOB.xml")));
    }

    [Fact]
    public void TreatsSiemensNotPermittedBlockExportAsSkipped()
    {
        var root = Path.Combine(Path.GetTempPath(), "PlcSourceExporterTests", Guid.NewGuid().ToString("N"));
        var service = new PlcExportService();
        var plc = FakePlcSoftware.Create(
            blocks:
            [
                new FakeExportableObject(
                    "FOB_RTG1",
                    "OrganizationBlock",
                    exceptionMessage: "Error when calling method 'Export' of type 'Siemens.Engineering.SW.Blocks.OB'.\r\n\r\nThe export of block 'FOB_RTG1' is not permitted.")
            ],
            types: [],
            tagTables: []);

        var summary = service.Export(plc, root);

        Assert.Equal(0, summary.SuccessCount);
        Assert.Equal(1, summary.SkippedCount);
        Assert.Equal(0, summary.FailureCount);
    }

    [Fact]
    public void WritesComponentMetadataForExportedSkippedAndFailedObjects()
    {
        var root = Path.Combine(Path.GetTempPath(), "PlcSourceExporterTests", Guid.NewGuid().ToString("N"));
        var service = new PlcExportService(() => new DateTimeOffset(2026, 6, 19, 1, 2, 3, TimeSpan.Zero));
        var plc = FakePlcSoftware.Create(
            blocks:
            [
                new FakeExportableObject(
                    "Program/Main",
                    "OrganizationBlock",
                    programmingLanguage: "LAD",
                    tiaIdentifier: "tia-ob-main",
                    number: 1,
                    isKnowHowProtected: false),
                new FakeExportableObject(
                    "Program/ProDiagOB",
                    "OrganizationBlock",
                    programmingLanguage: "ProDiag_OB",
                    skipReason: "Programming language 'ProDiag_OB' is not supported by Siemens Openness XML export."),
                new FakeExportableObject(
                    "Program/Secret",
                    "FunctionBlock",
                    programmingLanguage: "SCL",
                    shouldThrow: true)
            ],
            types: [new FakeExportableObject("Types/ValveType", "PlcType", tiaIdentifier: "tia-type-valve")],
            tagTables: [new FakeExportableObject("Tags/Default tag table", "PlcTagTable")]);

        var summary = service.Export(plc, root);
        var metadataFile = Path.Combine(root, "metadata.json");
        var json = File.ReadAllText(metadataFile);

        Assert.Equal(metadataFile, summary.MetadataFilePath);
        Assert.Contains("\"schemaVersion\": \"1.0\"", json);
        Assert.Contains("\"exportStartedUtc\": \"2026-06-19T01:02:03.0000000+00:00\"", json);
        Assert.Contains("\"exportFinishedUtc\": \"2026-06-19T01:02:03.0000000+00:00\"", json);
        Assert.Contains("\"sourcePath\": \"Program/Main\"", json);
        Assert.Contains("\"category\": \"OB\"", json);
        Assert.Contains("\"folder\": \"Blocks\"", json);
        Assert.Contains("\"programmingLanguage\": \"LAD\"", json);
        Assert.Contains("\"tiaIdentifier\": \"tia-ob-main\"", json);
        Assert.Contains("\"number\": 1", json);
        Assert.Contains("\"isKnowHowProtected\": false", json);
        Assert.Contains("\"status\": \"Exported\"", json);
        Assert.Contains("\"status\": \"Skipped\"", json);
        Assert.Contains("\"status\": \"Failed\"", json);
        Assert.Contains("\"exportedFile\": \"Blocks\\\\Main.xml\"", json);
    }

    [Fact]
    public void DoesNotWriteRetiredDerivedArtifactsAfterExport()
    {
        var root = Path.Combine(Path.GetTempPath(), "PlcSourceExporterTests", Guid.NewGuid().ToString("N"));
        var service = new PlcExportService(() => new DateTimeOffset(2026, 6, 19, 4, 5, 6, TimeSpan.Zero));
        var plc = FakePlcSoftware.Create(
            blocks:
            [
                new FakeExportableObject(
                    "Blocks/Main",
                    "OrganizationBlock",
                    exportContent: """
                        <?xml version="1.0" encoding="utf-8"?>
                        <Document>
                          <SW.Blocks.OB ID="0">
                            <AttributeList>
                              <Name>Main</Name>
                              <ProgrammingLanguage>LAD</ProgrammingLanguage>
                            </AttributeList>
                            <ObjectList>
                              <SW.Blocks.CompileUnit ID="1">
                                <AttributeList><ProgrammingLanguage>LAD</ProgrammingLanguage></AttributeList>
                              </SW.Blocks.CompileUnit>
                            </ObjectList>
                          </SW.Blocks.OB>
                        </Document>
                        """)
            ],
            types:
            [
                new FakeExportableObject(
                    "Types/UDT_Cell",
                    "PlcType",
                    exportContent: """
                        <?xml version="1.0" encoding="utf-8"?>
                        <Document>
                          <SW.Types.PlcStruct ID="0">
                            <AttributeList>
                              <Interface><Sections xmlns="http://www.siemens.com/automation/Openness/SW/Interface/v5">
                                <Section Name="None">
                                  <Member Name="Ready" Datatype="Bool" />
                                </Section>
                              </Sections></Interface>
                              <Name>UDT_Cell</Name>
                            </AttributeList>
                          </SW.Types.PlcStruct>
                        </Document>
                        """)
            ],
            tagTables:
            [
                new FakeExportableObject(
                    "Tags/Robot IO",
                    "PlcTagTable",
                    exportContent: """
                        <?xml version="1.0" encoding="utf-8"?>
                        <Document>
                          <SW.Tags.PlcTagTable ID="0">
                            <AttributeList>
                              <Name>Robot IO</Name>
                            </AttributeList>
                            <ObjectList>
                              <SW.Tags.PlcTag ID="1" CompositionName="Tags">
                                <AttributeList>
                                  <DataTypeName>Bool</DataTypeName>
                                  <LogicalAddress>%M1.0</LogicalAddress>
                                  <Name>Ready</Name>
                                </AttributeList>
                              </SW.Tags.PlcTag>
                            </ObjectList>
                          </SW.Tags.PlcTagTable>
                        </Document>
                        """)
            ]);

        var summary = service.Export(plc, root);

        Assert.NotNull(summary.MetadataFilePath);
        Assert.NotNull(summary.SemanticModelSqliteFilePath);
        Assert.NotNull(summary.BlockProfilesFilePath);
        Assert.NotNull(summary.OptimizationHintsFilePath);
        Assert.False(File.Exists(Path.Combine(root, "udt.json")));
        Assert.False(File.Exists(Path.Combine(root, "tags.json")));
        Assert.False(File.Exists(Path.Combine(root, "callgraph.json")));
        Assert.False(File.Exists(Path.Combine(root, "calltree.md")));
        Assert.False(File.Exists(Path.Combine(root, "networks.jsonl")));
        Assert.False(File.Exists(Path.Combine(root, "references.jsonl")));
    }

    [Fact]
    public void WritesBlockProfilesAndOptimizationHintsAfterSemanticExports()
    {
        var root = Path.Combine(Path.GetTempPath(), "PlcSourceExporterTests", Guid.NewGuid().ToString("N"));
        var service = new PlcExportService(() => new DateTimeOffset(2026, 6, 26, 8, 9, 10, TimeSpan.Zero));
        var plc = FakePlcSoftware.Create(
            blocks:
            [
                new FakeExportableObject(
                    "Blocks/SequenceFb",
                    "FunctionBlock",
                    exportContent: """
                        <?xml version="1.0" encoding="utf-8"?>
                        <Document>
                          <SW.Blocks.FB ID="0">
                            <AttributeList>
                              <Name>SequenceFb</Name>
                              <ProgrammingLanguage>LAD</ProgrammingLanguage>
                              <Interface>
                                <Sections xmlns="http://www.siemens.com/automation/Openness/SW/Interface/v5">
                                  <Section Name="Input"><Member Name="Start" Datatype="Bool" /></Section>
                                  <Section Name="Output"><Member Name="Done" Datatype="Bool" /></Section>
                                </Sections>
                              </Interface>
                            </AttributeList>
                            <ObjectList>
                              <SW.Blocks.CompileUnit ID="1">
                                <AttributeList>
                                  <NetworkSource><FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
                                    <Parts>
                                      <Access Scope="LocalVariable" UId="1"><Symbol><Component Name="Start" /></Symbol></Access>
                                      <Access Scope="LocalVariable" UId="2"><Symbol><Component Name="Done" /></Symbol></Access>
                                      <Call UId="3">
                                        <CallInfo Name="TON" BlockType="FB">
                                          <Instance Scope="LocalVariable" UId="4"><Component Name="Timer_1" /></Instance>
                                          <Parameter Name="IN" Section="Input" Type="Bool" />
                                          <Parameter Name="Q" Section="Output" Type="Bool" />
                                        </CallInfo>
                                      </Call>
                                    </Parts>
                                    <Wires>
                                      <Wire UId="5"><IdentCon UId="1" /><NameCon UId="3" Name="IN" /></Wire>
                                      <Wire UId="6"><NameCon UId="3" Name="Q" /><IdentCon UId="2" /></Wire>
                                    </Wires>
                                  </FlgNet></NetworkSource>
                                  <ProgrammingLanguage>LAD</ProgrammingLanguage>
                                </AttributeList>
                              </SW.Blocks.CompileUnit>
                            </ObjectList>
                          </SW.Blocks.FB>
                        </Document>
                        """)
            ],
            types: [],
            tagTables: []);

        var summary = service.Export(plc, root);

        Assert.Equal(Path.Combine(root, "block-profiles.jsonl"), summary.BlockProfilesFilePath);
        Assert.Equal(Path.Combine(root, "optimization-hints.jsonl"), summary.OptimizationHintsFilePath);
        Assert.True(File.Exists(summary.BlockProfilesFilePath));
        Assert.True(File.Exists(summary.OptimizationHintsFilePath));
    }

    [Fact]
    public void WritesCompactProgramBlockLogicYamlAfterMetadata()
    {
        var root = Path.Combine(Path.GetTempPath(), "PlcSourceExporterTests", Guid.NewGuid().ToString("N"));
        var service = new PlcExportService(() => new DateTimeOffset(2026, 7, 7, 1, 2, 3, TimeSpan.Zero));
        var plc = FakePlcSoftware.Create(
            blocks:
            [
                new FakeExportableObject(
                    "Blocks/Main",
                    "OrganizationBlock",
                    exportContent: """
                        <?xml version="1.0" encoding="utf-8"?>
                        <Document>
                          <SW.Blocks.OB ID="0">
                            <AttributeList>
                              <Name>Main</Name>
                              <ProgrammingLanguage>LAD</ProgrammingLanguage>
                            </AttributeList>
                            <ObjectList>
                              <SW.Blocks.CompileUnit ID="cu-simple">
                                <AttributeList>
                                  <NetworkSource>
                                    <FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
                                      <Parts>
                                        <Access Scope="LocalVariable" UId="access-source"><Symbol><Component Name="Start" /></Symbol></Access>
                                        <Access Scope="LocalVariable" UId="access-target"><Symbol><Component Name="Run" /></Symbol></Access>
                                        <Part Name="Contact" UId="part-contact" />
                                        <Part Name="Coil" UId="part-coil" />
                                      </Parts>
                                      <Wires>
                                        <Wire UId="wire-power"><Powerrail /><NameCon UId="part-contact" Name="in" /></Wire>
                                        <Wire UId="wire-contact-out"><NameCon UId="part-contact" Name="out" /><NameCon UId="part-coil" Name="in" /></Wire>
                                        <Wire UId="wire-contact-operand"><IdentCon UId="access-source" /><NameCon UId="part-contact" Name="operand" /></Wire>
                                        <Wire UId="wire-coil-operand"><IdentCon UId="access-target" /><NameCon UId="part-coil" Name="operand" /></Wire>
                                      </Wires>
                                    </FlgNet>
                                  </NetworkSource>
                                  <ProgrammingLanguage>LAD</ProgrammingLanguage>
                                </AttributeList>
                                <ObjectList>
                                  <MultilingualText CompositionName="Title" ID="title-simple">
                                    <ObjectList>
                                      <MultilingualTextItem ID="title-simple-en">
                                        <AttributeList><Culture>en-US</Culture><Text>Start run</Text></AttributeList>
                                      </MultilingualTextItem>
                                    </ObjectList>
                                  </MultilingualText>
                                </ObjectList>
                              </SW.Blocks.CompileUnit>
                            </ObjectList>
                          </SW.Blocks.OB>
                        </Document>
                        """),
                new FakeExportableObject(
                    "Blocks/DataOnly",
                    "DataBlock",
                    exportContent: """
                        <?xml version="1.0" encoding="utf-8"?>
                        <Document><SW.Blocks.GlobalDB ID="0"><AttributeList><Name>DataOnly</Name></AttributeList></SW.Blocks.GlobalDB></Document>
                        """)
            ],
            types: [new FakeExportableObject("Types/UDT_Cell", "PlcType")],
            tagTables: [new FakeExportableObject("Tags/Default tag table", "PlcTagTable")]);

        var summary = service.Export(plc, root);
        var yamlPath = Path.Combine(root, "translate", "program-blocks.yaml");
        var yaml = File.ReadAllText(yamlPath);

        Assert.Equal(yamlPath, summary.ProgramBlockTranslationFilePath);
        Assert.Contains("schemaVersion: \"1.0\"", yaml);
        Assert.Contains("generatedUtc: \"2026-07-07T01:02:03.0000000Z\"", yaml);
        Assert.Contains("sourceFile: \"Blocks\\\\Main.xml\"", yaml);
        Assert.Contains("kind: \"OB\"", yaml);
        Assert.Contains("title: \"Start run\"", yaml);
        Assert.Contains("confidence: \"exact\"", yaml);
        Assert.Contains("- \"Run := Start;\"", yaml);
        Assert.DoesNotContain("DataOnly", yaml);
        Assert.DoesNotContain("UDT_Cell", yaml);
        Assert.DoesNotContain("Default tag table", yaml);
        Assert.DoesNotContain("UId", yaml);
        Assert.DoesNotContain("part-contact", yaml);
        Assert.DoesNotContain("access-source", yaml);
    }

    [Fact]
    public void TranslatesLadNegatedBranchAndCompareExpressionsToSclLikeYaml()
    {
        var root = Path.Combine(Path.GetTempPath(), "PlcSourceExporterTests", Guid.NewGuid().ToString("N"));
        var service = new PlcExportService(() => new DateTimeOffset(2026, 7, 7, 4, 5, 6, TimeSpan.Zero));
        var plc = FakePlcSoftware.Create(
            blocks:
            [
                new FakeExportableObject(
                    "Blocks/Logic",
                    "FunctionBlock",
                    exportContent: """
                        <?xml version="1.0" encoding="utf-8"?>
                        <Document>
                          <SW.Blocks.FB ID="0">
                            <AttributeList><Name>Logic</Name><ProgrammingLanguage>LAD</ProgrammingLanguage></AttributeList>
                            <ObjectList>
                              <SW.Blocks.CompileUnit ID="cu-complex">
                                <AttributeList>
                                  <NetworkSource>
                                    <FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
                                      <Parts>
                                        <Access Scope="LocalVariable" UId="a-auto"><Symbol><Component Name="AutoMode" /></Symbol></Access>
                                        <Access Scope="LocalVariable" UId="a-fault"><Symbol><Component Name="Fault" /></Symbol></Access>
                                        <Access Scope="LocalVariable" UId="a-temp"><Symbol><Component Name="Temperature" /></Symbol></Access>
                                        <Access Scope="LiteralConstant" UId="a-limit"><Constant><ConstantValue>50</ConstantValue></Constant></Access>
                                        <Access Scope="LocalVariable" UId="a-ready"><Symbol><Component Name="Ready" /></Symbol></Access>
                                        <Part Name="Contact" UId="p-auto" />
                                        <Part Name="Contact" UId="p-fault"><Negated Name="operand" /></Part>
                                        <Part Name="Le" UId="p-compare" />
                                        <Part Name="O" UId="p-or" />
                                        <Part Name="Coil" UId="p-coil" />
                                      </Parts>
                                      <Wires>
                                        <Wire UId="w-auto-in"><Powerrail /><NameCon UId="p-auto" Name="in" /></Wire>
                                        <Wire UId="w-auto-out"><NameCon UId="p-auto" Name="out" /><NameCon UId="p-or" Name="in1" /></Wire>
                                        <Wire UId="w-auto-op"><IdentCon UId="a-auto" /><NameCon UId="p-auto" Name="operand" /></Wire>
                                        <Wire UId="w-fault-in"><Powerrail /><NameCon UId="p-fault" Name="in" /></Wire>
                                        <Wire UId="w-fault-out"><NameCon UId="p-fault" Name="out" /><NameCon UId="p-or" Name="in2" /></Wire>
                                        <Wire UId="w-fault-op"><IdentCon UId="a-fault" /><NameCon UId="p-fault" Name="operand" /></Wire>
                                        <Wire UId="w-cmp-in"><Powerrail /><NameCon UId="p-compare" Name="pre" /></Wire>
                                        <Wire UId="w-cmp-out"><NameCon UId="p-compare" Name="out" /><NameCon UId="p-or" Name="in3" /></Wire>
                                        <Wire UId="w-cmp-left"><IdentCon UId="a-temp" /><NameCon UId="p-compare" Name="in1" /></Wire>
                                        <Wire UId="w-cmp-right"><IdentCon UId="a-limit" /><NameCon UId="p-compare" Name="in2" /></Wire>
                                        <Wire UId="w-or-out"><NameCon UId="p-or" Name="out" /><NameCon UId="p-coil" Name="in" /></Wire>
                                        <Wire UId="w-coil-op"><IdentCon UId="a-ready" /><NameCon UId="p-coil" Name="operand" /></Wire>
                                      </Wires>
                                    </FlgNet>
                                  </NetworkSource>
                                  <ProgrammingLanguage>LAD</ProgrammingLanguage>
                                </AttributeList>
                              </SW.Blocks.CompileUnit>
                            </ObjectList>
                          </SW.Blocks.FB>
                        </Document>
                        """)
            ],
            types: [],
            tagTables: []);

        service.Export(plc, root);
        var yaml = File.ReadAllText(Path.Combine(root, "translate", "program-blocks.yaml"));

        Assert.Contains("- \"Ready := (AutoMode OR NOT Fault OR (Temperature <= 50));\"", yaml);
        Assert.DoesNotContain("UId", yaml);
        Assert.DoesNotContain("p-compare", yaml);
        Assert.DoesNotContain("a-limit", yaml);
    }

    [Fact]
    public void EmitsUnsupportedNetworksAsUntranslatedYamlTrace()
    {
        var root = Path.Combine(Path.GetTempPath(), "PlcSourceExporterTests", Guid.NewGuid().ToString("N"));
        var service = new PlcExportService(() => new DateTimeOffset(2026, 7, 7, 7, 8, 9, TimeSpan.Zero));
        var plc = FakePlcSoftware.Create(
            blocks:
            [
                new FakeExportableObject(
                    "Blocks/Legacy",
                    "Function",
                    exportContent: """
                        <?xml version="1.0" encoding="utf-8"?>
                        <Document>
                          <SW.Blocks.FC ID="0">
                            <AttributeList><Name>Legacy</Name><ProgrammingLanguage>STL</ProgrammingLanguage></AttributeList>
                            <ObjectList>
                              <SW.Blocks.CompileUnit ID="cu-stl">
                                <AttributeList><ProgrammingLanguage>STL</ProgrammingLanguage></AttributeList>
                              </SW.Blocks.CompileUnit>
                            </ObjectList>
                          </SW.Blocks.FC>
                        </Document>
                        """)
            ],
            types: [],
            tagTables: []);

        service.Export(plc, root);
        var yaml = File.ReadAllText(Path.Combine(root, "translate", "program-blocks.yaml"));

        Assert.Contains("name: \"Legacy\"", yaml);
        Assert.Contains("compileUnitId: \"cu-stl\"", yaml);
        Assert.Contains("language: \"STL\"", yaml);
        Assert.Contains("confidence: \"untranslated\"", yaml);
        Assert.Contains("- \"Unsupported network language or missing FlgNet source.\"", yaml);
    }

    [Fact]
    public void WritesAiExportGuideIntoExportRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "PlcSourceExporterTests", Guid.NewGuid().ToString("N"));
        var service = new PlcExportService(() => new DateTimeOffset(2026, 6, 27, 1, 2, 3, TimeSpan.Zero));
        var plc = FakePlcSoftware.Create(
            blocks:
            [
                new FakeExportableObject(
                    "Blocks/Main",
                    "OrganizationBlock",
                    exportContent: """
                        <?xml version="1.0" encoding="utf-8"?>
                        <Document>
                          <SW.Blocks.OB ID="0" />
                        </Document>
                        """)
            ],
            types: [],
            tagTables: []);

        var summary = service.Export(plc, root);
        var guidePath = Path.Combine(root, "AI_EXPORT_GUIDE.md");
        var guide = File.ReadAllText(guidePath);

        Assert.Equal(guidePath, summary.AiExportGuideFilePath);
        Assert.Contains("AI Guide For Using PlcSourceExporter Outputs", guide);
        Assert.Contains("model\\plc-graph.sqlite", guide);
        Assert.Contains("model\\schema.sql", guide);
        Assert.Contains("model\\AGENT_SQLITE_GUIDE.md", guide);
        Assert.Contains("translate\\program-blocks.yaml", guide);
        Assert.Contains("contact polarity", guide);
        Assert.Contains("block-profiles.jsonl", guide);
        Assert.Contains("optimization-hints.jsonl", guide);
        Assert.DoesNotContain("tags.json", guide);
        Assert.DoesNotContain("udt.json", guide);
        Assert.DoesNotContain("callgraph.json", guide);
        Assert.DoesNotContain("calltree.md", guide);
        Assert.DoesNotContain("networks.jsonl", guide);
        Assert.DoesNotContain("references.jsonl", guide);
    }

    [Fact]
    public void WritesSemanticModelFilesUnderModelFolder()
    {
        var root = Path.Combine(Path.GetTempPath(), "PlcSourceExporterTests", Guid.NewGuid().ToString("N"));
        var service = new PlcExportService(() => new DateTimeOffset(2026, 7, 5, 1, 2, 3, TimeSpan.Zero));
        var plc = FakePlcSoftware.Create(
            blocks:
            [
                new FakeExportableObject(
                    "Blocks/Main",
                    "OrganizationBlock",
                    exportContent: """
                        <?xml version="1.0" encoding="utf-8"?>
                        <Document>
                          <SW.Blocks.OB ID="0">
                            <AttributeList>
                              <Name>Main</Name>
                              <ProgrammingLanguage>LAD</ProgrammingLanguage>
                            </AttributeList>
                            <ObjectList>
                              <SW.Blocks.CompileUnit ID="1">
                                <AttributeList><ProgrammingLanguage>LAD</ProgrammingLanguage></AttributeList>
                              </SW.Blocks.CompileUnit>
                            </ObjectList>
                          </SW.Blocks.OB>
                        </Document>
                        """)
            ],
            types: [],
            tagTables: []);

        var summary = service.Export(plc, root);
        var modelFolder = Path.Combine(root, "model");
        var sqlitePath = Path.Combine(modelFolder, "plc-graph.sqlite");
        var schemaPath = Path.Combine(modelFolder, "schema.sql");
        var guidePath = Path.Combine(modelFolder, "AGENT_SQLITE_GUIDE.md");
        var graph = SqliteSemanticGraphStore.Load(sqlitePath);

        Assert.Equal(sqlitePath, summary.SemanticModelSqliteFilePath);
        Assert.Equal(schemaPath, summary.SemanticModelSchemaFilePath);
        Assert.Equal(guidePath, summary.SemanticModelAgentGuideFilePath);
        Assert.True(File.Exists(sqlitePath));
        Assert.True(File.Exists(schemaPath));
        Assert.True(File.Exists(guidePath));
        Assert.Contains("CREATE TABLE IF NOT EXISTS graph_nodes", File.ReadAllText(schemaPath));
        var guide = File.ReadAllText(guidePath);
        Assert.Contains("Agent Guide For `plc-graph.sqlite`", guide);
        Assert.Contains("graph_nodes", guide);
        Assert.Contains("graph_edges", guide);
        Assert.Contains("CALLS", guide);
        Assert.Contains("READS", guide);
        Assert.Contains("WRITES", guide);
        Assert.Contains("sourceFile", guide);
        Assert.Contains("networkIndex", guide);
        Assert.Equal(SemanticNodeKind.OrganizationBlock, graph.GetNode("block:Main").Kind);
        Assert.Equal(SemanticNodeKind.Network, graph.GetNode("network:Main:1").Kind);
    }

    [Fact]
    public void UsesInjectedSemanticModelWriterForAddInSafeOutOfProcessGeneration()
    {
        var root = Path.Combine(Path.GetTempPath(), "PlcSourceExporterTests", Guid.NewGuid().ToString("N"));
        var semanticWriter = new FakeSemanticModelWriter();
        var service = new PlcExportService(
            () => new DateTimeOffset(2026, 7, 9, 1, 2, 3, TimeSpan.Zero),
            semanticWriter);
        var plc = FakePlcSoftware.Create(
            blocks: [new FakeExportableObject("Blocks/Main", "OrganizationBlock")],
            types: [],
            tagTables: []);

        var summary = service.Export(plc, root);

        Assert.Equal(root, semanticWriter.ExportRoot);
        Assert.Equal(Path.Combine(root, "model", "plc-graph.sqlite"), summary.SemanticModelSqliteFilePath);
        Assert.Equal(Path.Combine(root, "model", "schema.sql"), summary.SemanticModelSchemaFilePath);
        Assert.Equal(Path.Combine(root, "model", "AGENT_SQLITE_GUIDE.md"), summary.SemanticModelAgentGuideFilePath);
    }

    [Fact]
    public void ReportsProgressThroughObjectExportAndPostProcessing()
    {
        var root = Path.Combine(Path.GetTempPath(), "PlcSourceExporterTests", Guid.NewGuid().ToString("N"));
        var service = new PlcExportService(() => new DateTimeOffset(2026, 7, 3, 1, 2, 3, TimeSpan.Zero));
        var plc = FakePlcSoftware.Create(
            blocks:
            [
                new FakeExportableObject("Blocks/Main", "OrganizationBlock"),
                new FakeExportableObject("Blocks/Logic", "FunctionBlock")
            ],
            types: [new FakeExportableObject("Types/ValveType", "PlcType")],
            tagTables: [new FakeExportableObject("Tags/Default tag table", "PlcTagTable")]);
        var updates = new List<ExportProgress>();

        service.Export(plc, root, progress: new RecordingProgress(updates));

        Assert.Contains(updates, update => update.Phase == ExportPhase.Preparing && update.PercentComplete == 0);
        Assert.Contains(updates, update => update.Phase == ExportPhase.ExportingObjects && update.CurrentItem == "Blocks/Main");
        Assert.Contains(updates, update => update.Phase == ExportPhase.ExportingObjects && update.CompletedItems == 4 && update.TotalItems == 0);
        Assert.Contains(updates, update => update.Phase == ExportPhase.WritingDerivedArtifacts && update.Message.Contains("component metadata", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(100, updates[^1].PercentComplete);
        Assert.Equal(ExportPhase.Completed, updates[^1].Phase);
    }

    [Fact]
    public void ReportsExportedCountWithoutCountingObjectsFirst()
    {
        var root = Path.Combine(Path.GetTempPath(), "PlcSourceExporterTests", Guid.NewGuid().ToString("N"));
        var service = new PlcExportService(() => new DateTimeOffset(2026, 7, 4, 1, 2, 3, TimeSpan.Zero));
        var plc = FakePlcSoftware.Create(
            blocks:
            [
                new FakeExportableObject("Blocks/Main", "OrganizationBlock"),
                new FakeExportableObject("Blocks/Logic", "FunctionBlock")
            ],
            types: [new FakeExportableObject("Types/ValveType", "PlcType")],
            tagTables: [new FakeExportableObject("Tags/Default tag table", "PlcTagTable")]);
        var updates = new List<ExportProgress>();

        service.Export(plc, root, progress: new RecordingProgress(updates));

        Assert.DoesNotContain(updates, update => update.Message.Contains("Counting", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(updates, update => update.Phase == ExportPhase.EnumeratingObjects);
        Assert.DoesNotContain(updates, update => update.TotalItems > 0 && update.Phase == ExportPhase.ExportingObjects);
        Assert.Contains(updates, update =>
            update.Phase == ExportPhase.ExportingObjects &&
            update.CurrentItem == "Blocks/Main" &&
            update.CompletedItems == 1 &&
            update.TotalItems == 0);
    }

    [Fact]
    public void StreamsExportWithoutPreEnumeratingAllObjects()
    {
        var root = Path.Combine(Path.GetTempPath(), "PlcSourceExporterTests", Guid.NewGuid().ToString("N"));
        var service = new PlcExportService(() => new DateTimeOffset(2026, 7, 4, 1, 2, 3, TimeSpan.Zero));
        var events = new List<string>();
        var plc = StreamingFakePlcSoftware.Create(
            blocks:
            [
                new FakeExportableObject("Blocks/Main", "OrganizationBlock", afterExport: () => events.Add("export Main")),
                new FakeExportableObject("Blocks/Logic", "FunctionBlock", afterExport: () => events.Add("export Logic"))
            ],
            types: [],
            tagTables: [],
            events);

        service.Export(plc, root);

        Assert.True(
            events.IndexOf("export Main") < events.IndexOf("enumerate Blocks/Logic"),
            string.Join(", ", events));
    }

    [Fact]
    public void HonorsCancellationBetweenExportedObjects()
    {
        var root = Path.Combine(Path.GetTempPath(), "PlcSourceExporterTests", Guid.NewGuid().ToString("N"));
        var service = new PlcExportService();
        var cts = new CancellationTokenSource();
        var plc = FakePlcSoftware.Create(
            blocks:
            [
                new FakeExportableObject("Blocks/Main", "OrganizationBlock", afterExport: cts.Cancel),
                new FakeExportableObject("Blocks/Second", "FunctionBlock")
            ],
            types: [],
            tagTables: []);

        Assert.Throws<OperationCanceledException>(
            () => service.Export(plc, root, cancellationToken: cts.Token));
        Assert.True(File.Exists(Path.Combine(root, "Blocks", "Main.xml")));
        Assert.False(File.Exists(Path.Combine(root, "Blocks", "Second.xml")));
    }

    private sealed class FakePlcSoftware : IPlcSoftwareSource
    {
        private readonly IReadOnlyList<IPlcExportableObject> _blocks;
        private readonly IReadOnlyList<IPlcExportableObject> _types;
        private readonly IReadOnlyList<IPlcExportableObject> _tagTables;

        private FakePlcSoftware(
            IReadOnlyList<IPlcExportableObject> blocks,
            IReadOnlyList<IPlcExportableObject> types,
            IReadOnlyList<IPlcExportableObject> tagTables)
        {
            _blocks = blocks;
            _types = types;
            _tagTables = tagTables;
        }

        public static FakePlcSoftware Create(
            IReadOnlyList<IPlcExportableObject> blocks,
            IReadOnlyList<IPlcExportableObject> types,
            IReadOnlyList<IPlcExportableObject> tagTables)
        {
            return new FakePlcSoftware(blocks, types, tagTables);
        }

        public IEnumerable<IPlcExportableObject> EnumerateBlocks() => _blocks;

        public IEnumerable<IPlcExportableObject> EnumerateTypes() => _types;

        public IEnumerable<IPlcExportableObject> EnumerateTagTables() => _tagTables;
    }

    private sealed class StreamingFakePlcSoftware : IPlcSoftwareSource
    {
        private readonly IReadOnlyList<IPlcExportableObject> _blocks;
        private readonly IReadOnlyList<IPlcExportableObject> _types;
        private readonly IReadOnlyList<IPlcExportableObject> _tagTables;
        private readonly List<string> _events;

        private StreamingFakePlcSoftware(
            IReadOnlyList<IPlcExportableObject> blocks,
            IReadOnlyList<IPlcExportableObject> types,
            IReadOnlyList<IPlcExportableObject> tagTables,
            List<string> events)
        {
            _blocks = blocks;
            _types = types;
            _tagTables = tagTables;
            _events = events;
        }

        public static StreamingFakePlcSoftware Create(
            IReadOnlyList<IPlcExportableObject> blocks,
            IReadOnlyList<IPlcExportableObject> types,
            IReadOnlyList<IPlcExportableObject> tagTables,
            List<string> events)
        {
            return new StreamingFakePlcSoftware(blocks, types, tagTables, events);
        }

        public IEnumerable<IPlcExportableObject> EnumerateBlocks() => Enumerate(_blocks);

        public IEnumerable<IPlcExportableObject> EnumerateTypes() => Enumerate(_types);

        public IEnumerable<IPlcExportableObject> EnumerateTagTables() => Enumerate(_tagTables);

        private IEnumerable<IPlcExportableObject> Enumerate(IEnumerable<IPlcExportableObject> objects)
        {
            foreach (var item in objects)
            {
                _events.Add($"enumerate {item.ObjectPath}");
                yield return item;
            }
        }
    }

    private sealed class RecordingProgress : IProgress<ExportProgress>
    {
        private readonly List<ExportProgress> _updates;

        public RecordingProgress(List<ExportProgress> updates)
        {
            _updates = updates;
        }

        public void Report(ExportProgress value)
        {
            _updates.Add(value);
        }
    }

    private sealed class FakeSemanticModelWriter : ISemanticPlcModelWriter
    {
        public string? ExportRoot { get; private set; }

        public SemanticPlcModelWriteResult Write(string exportRoot)
        {
            ExportRoot = exportRoot;
            var result = SemanticPlcModelWriter.GetExpectedResult(exportRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(result.SqliteFilePath)!);
            File.WriteAllText(result.SqliteFilePath, string.Empty);
            File.WriteAllText(result.SchemaFilePath, string.Empty);
            File.WriteAllText(result.AgentGuideFilePath, string.Empty);
            return result;
        }
    }

    private sealed class FakeExportableObject : IPlcExportableObject
    {
        private readonly bool _shouldThrow;
        private readonly string? _exceptionMessage;

        public FakeExportableObject(
            string objectPath,
            string siemensTypeName,
            bool shouldThrow = false,
            string? skipReason = null,
            string? exceptionMessage = null,
            string? programmingLanguage = null,
            string? tiaIdentifier = null,
            int? number = null,
            bool? isKnowHowProtected = null,
            string? exportContent = null,
            Action? afterExport = null)
        {
            ObjectPath = objectPath;
            SiemensTypeName = siemensTypeName;
            _shouldThrow = shouldThrow;
            _exceptionMessage = exceptionMessage;
            ExportContent = exportContent;
            AfterExport = afterExport;
            SkipReason = skipReason;
            Metadata = new PlcExportableMetadata(
                programmingLanguage,
                tiaIdentifier,
                number,
                isKnowHowProtected,
                null,
                null,
                null,
                null);
        }

        public string Name => ObjectPath.Split('/').Last();

        public string ObjectPath { get; }

        public string SiemensTypeName { get; }

        public string? SkipReason { get; }

        public PlcExportableMetadata Metadata { get; }

        private string? ExportContent { get; }

        private Action? AfterExport { get; }

        public void ExportTo(string filePath)
        {
            if (_exceptionMessage != null)
            {
                throw new InvalidOperationException(_exceptionMessage);
            }

            if (_shouldThrow)
            {
                throw new InvalidOperationException("Simulated export failure");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            File.WriteAllText(filePath, ExportContent ?? ObjectPath);
            AfterExport?.Invoke();
        }
    }
}
