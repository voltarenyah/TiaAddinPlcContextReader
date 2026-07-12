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
    public void TranslatesSclStructuredTextFromOrderedXmlTokens()
    {
        var root = Path.Combine(Path.GetTempPath(), "PlcSourceExporterTests", Guid.NewGuid().ToString("N"));
        var service = new PlcExportService(() => new DateTimeOffset(2026, 7, 12, 1, 2, 3, TimeSpan.Zero));
        var plc = FakePlcSoftware.Create(
            blocks:
            [
                new FakeExportableObject(
                    "Blocks/SclLogic",
                    "FunctionBlock",
                    exportContent: """
                        <?xml version="1.0" encoding="utf-8"?>
                        <Document>
                          <SW.Blocks.FB ID="0">
                            <AttributeList><Name>SclLogic</Name><ProgrammingLanguage>SCL</ProgrammingLanguage></AttributeList>
                            <ObjectList>
                              <SW.Blocks.CompileUnit ID="cu-scl">
                                <AttributeList>
                                  <NetworkSource>
                                    <StructuredText xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/StructuredText/v3">
                                      <Access Scope="GlobalVariable" UId="21">
                                        <Symbol UId="22">
                                          <Component Name="HMI_Cav_A" UId="23" />
                                          <Token Text="." UId="24" />
                                          <Component Name="Full_Cycle_Diag" UId="25">
                                            <Token Text="[" UId="26" />
                                            <Access Scope="LiteralConstant" UId="27"><Constant UId="28"><ConstantValue UId="30">0</ConstantValue></Constant></Access>
                                            <Token Text="]" UId="31" />
                                          </Component>
                                        </Symbol>
                                      </Access>
                                      <Blank UId="32" />
                                      <Token Text=":=" UId="33" />
                                      <Blank UId="34" />
                                      <Token Text="NOT" UId="35" />
                                      <Blank UId="36" />
                                      <Access Scope="GlobalVariable" UId="37">
                                        <Symbol UId="38">
                                          <Component Name="Cav_A" UId="39" />
                                          <Token Text="." UId="40" />
                                          <Component Name="RunMode" UId="41" />
                                          <Token Text="." UId="42" />
                                          <Component Name="Current_Cycle" UId="43" />
                                          <Token Text="." UId="44" />
                                          <Component Name="EoC_Request" UId="45" />
                                        </Symbol>
                                      </Access>
                                      <Token Text=";" UId="46" />
                                      <NewLine UId="47" />
                                      <Access Scope="GlobalVariable" UId="48">
                                        <Symbol UId="49">
                                          <Component Name="HMI_Cav_A" UId="50" />
                                          <Token Text="." UId="51" />
                                          <Component Name="Full_Cycle_Diag" UId="52">
                                            <Token Text="[" UId="53" />
                                            <Access Scope="LiteralConstant" UId="54"><Constant UId="55"><ConstantValue UId="57">1</ConstantValue></Constant></Access>
                                            <Token Text="]" UId="58" />
                                          </Component>
                                        </Symbol>
                                      </Access>
                                      <Blank UId="59" />
                                      <Token Text=":=" UId="60" />
                                      <Blank UId="61" />
                                      <Access Scope="GlobalVariable" UId="62"><Symbol UId="63"><Component Name="Ready" UId="64" /></Symbol></Access>
                                      <Token Text=";" UId="65" />
                                      <NewLine UId="66" />
                                      <Access Scope="LocalVariable" UId="67">
                                        <Symbol UId="68">
                                          <Component Name="S_HMI_Control" UId="69" />
                                          <Token Text="." UId="70" />
                                          <Component Name="DeviceStatus" UId="71" />
                                          <Token Text="." UId="72" />
                                          <Component Name="DeviceReadRetVal" UId="73" />
                                        </Symbol>
                                      </Access>
                                      <Blank UId="74" />
                                      <Token Text=":=" UId="75" />
                                      <Blank UId="76" />
                                      <Access Scope="Call" UId="77">
                                        <Instruction Name="ModuleStates" UId="78">
                                          <Token Text="(" UId="79" />
                                          <Parameter Name="LADDR" UId="80">
                                            <Blank UId="81" />
                                            <Token Text=":=" UId="82" />
                                            <Blank UId="83" />
                                            <Access Scope="LocalVariable" UId="84"><Symbol UId="85"><Component Name="I_HWIDDevice" UId="86" /></Symbol></Access>
                                          </Parameter>
                                          <Token Text="," UId="87" />
                                          <Blank UId="88" />
                                          <Parameter Name="MODE" UId="89">
                                            <Blank UId="90" />
                                            <Token Text=":=" UId="91" />
                                            <Blank UId="92" />
                                            <Access Scope="LocalVariable" UId="93"><Symbol UId="94"><Component Name="DeviceStatusDiag" UId="95" /><Token Text="." UId="96" /><Component Name="StatusReadMode" UId="97" /></Symbol></Access>
                                          </Parameter>
                                          <Token Text="," UId="98" />
                                          <Blank UId="99" />
                                          <Parameter Name="STATE" UId="100">
                                            <Blank UId="101" />
                                            <Token Text=":=" UId="102" />
                                            <Blank UId="103" />
                                            <Access Scope="LocalVariable" UId="104"><Symbol UId="105"><Component Name="DeviceStatusDiag" UId="106" /><Token Text="." UId="107" /><Component Name="StatusReadState" UId="108" /></Symbol></Access>
                                          </Parameter>
                                          <Token Text=")" UId="109" />
                                        </Instruction>
                                      </Access>
                                      <Token Text=";" UId="110" />
                                      <NewLine UId="111" />
                                      <Access Scope="LocalVariable" UId="112">
                                        <Symbol UId="113">
                                          <Component Name="G" SliceAccessModifier="x1" UId="114" />
                                        </Symbol>
                                      </Access>
                                      <Blank UId="115" />
                                      <Token Text=":=" UId="116" />
                                      <Blank UId="117" />
                                      <Access Scope="LocalVariable" UId="118">
                                        <Symbol UId="119">
                                          <Component Name="GT" SliceAccessModifier="x0" UId="120" />
                                        </Symbol>
                                      </Access>
                                      <Token Text=";" UId="121" />
                                    </StructuredText>
                                  </NetworkSource>
                                  <ProgrammingLanguage>SCL</ProgrammingLanguage>
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

        Assert.Contains("compileUnitId: \"cu-scl\"", yaml);
        Assert.Contains("language: \"SCL\"", yaml);
        Assert.Contains("translation:", yaml);
        Assert.Contains("language: \"scl\"", yaml);
        Assert.Contains("confidence: \"exact\"", yaml);
        Assert.Contains("HMI_Cav_A.Full_Cycle_Diag[0] := NOT Cav_A.RunMode.Current_Cycle.EoC_Request;", yaml);
        Assert.Contains("HMI_Cav_A.Full_Cycle_Diag[1] := Ready;", yaml);
        Assert.Contains("S_HMI_Control.DeviceStatus.DeviceReadRetVal := ModuleStates(LADDR := I_HWIDDevice, MODE := DeviceStatusDiag.StatusReadMode, STATE := DeviceStatusDiag.StatusReadState);", yaml);
        Assert.Contains("G.%X1 := GT.%X0;", yaml);
        Assert.DoesNotContain("- \"01\"", yaml);
    }

    [Fact]
    public void TranslatesLadSliceAccessModifiersAsBitAccesses()
    {
        var yaml = ExportSingleLadBlockYaml(
            "SliceLogic",
            "cu-slice",
            """
            <FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
              <Parts>
                <Access Scope="LocalVariable" UId="a-gt"><Symbol><Component Name="GT" SliceAccessModifier="x0" /></Symbol></Access>
                <Access Scope="LocalVariable" UId="a-g"><Symbol><Component Name="G" SliceAccessModifier="x1" /></Symbol></Access>
                <Part Name="Contact" UId="p-gt" />
                <Part Name="SCoil" UId="p-g" />
              </Parts>
              <Wires>
                <Wire UId="w-gt-in"><Powerrail /><NameCon UId="p-gt" Name="in" /></Wire>
                <Wire UId="w-gt-op"><IdentCon UId="a-gt" /><NameCon UId="p-gt" Name="operand" /></Wire>
                <Wire UId="w-gt-out"><NameCon UId="p-gt" Name="out" /><NameCon UId="p-g" Name="in" /></Wire>
                <Wire UId="w-g-op"><IdentCon UId="a-g" /><NameCon UId="p-g" Name="operand" /></Wire>
              </Wires>
            </FlgNet>
            """);

        Assert.Contains("confidence: \"exact\"", yaml);
        Assert.Contains("- \"IF GT.%X0 THEN G.%X1 := TRUE; END_IF;\"", yaml);
    }

    [Fact]
    public void TranslatesProgramAlarmSigFromSetCoilOutput()
    {
        var yaml = ExportSingleLadBlockYaml(
            "AlarmLogic",
            "cu-program-alarm",
            """
            <FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
              <Parts>
                <Access Scope="LocalVariable" UId="a-condition"><Symbol><Component Name="FaultCondition" /></Symbol></Access>
                <Access Scope="LocalVariable" UId="a-latched"><Symbol><Component Name="FaultLatched" /></Symbol></Access>
                <Access Scope="LocalVariable" UId="a-name"><Symbol><Component Name="Name_DF" /></Symbol></Access>
                <Part Name="Contact" UId="p-condition" />
                <Part Name="SCoil" UId="p-set" />
                <Part Name="Program_Alarm" Version="1.0" UId="p-alarm">
                  <Instance Scope="LocalVariable"><Component Name="Program_Alarm_1" /></Instance>
                </Part>
              </Parts>
              <Wires>
                <Wire UId="w-condition-in"><Powerrail /><NameCon UId="p-condition" Name="in" /><NameCon UId="p-alarm" Name="en" /></Wire>
                <Wire UId="w-condition-op"><IdentCon UId="a-condition" /><NameCon UId="p-condition" Name="operand" /></Wire>
                <Wire UId="w-condition-set"><NameCon UId="p-condition" Name="out" /><NameCon UId="p-set" Name="in" /></Wire>
                <Wire UId="w-set-op"><IdentCon UId="a-latched" /><NameCon UId="p-set" Name="operand" /></Wire>
                <Wire UId="w-set-sig"><NameCon UId="p-set" Name="out" /><NameCon UId="p-alarm" Name="SIG" /></Wire>
                <Wire UId="w-name"><IdentCon UId="a-name" /><NameCon UId="p-alarm" Name="SD_1" /></Wire>
              </Wires>
            </FlgNet>
            """);

        Assert.Contains("confidence: \"exact\"", yaml);
        Assert.Contains("- \"Program_Alarm_1(SIG := FaultCondition, SD_1 := Name_DF);\"", yaml);
        Assert.Contains("- \"IF FaultCondition THEN FaultLatched := TRUE; END_IF;\"", yaml);
        Assert.DoesNotContain("Skipped Program_Alarm call Program_Alarm_1 because no input pins could be resolved.", yaml);
    }

    [Fact]
    public void TranslatesMoveEnoAsEnablePathForDownstreamLatch()
    {
        var yaml = ExportSingleLadBlockYaml(
            "MoveEnoLogic",
            "cu-move-eno",
            """
            <FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
              <Parts>
                <Access Scope="LocalVariable" UId="a-enable"><Symbol><Component Name="EnableCopy" /></Symbol></Access>
                <Access Scope="LocalVariable" UId="a-source"><Symbol><Component Name="SourceData" /></Symbol></Access>
                <Access Scope="LocalVariable" UId="a-target"><Symbol><Component Name="TargetData" /></Symbol></Access>
                <Access Scope="LocalVariable" UId="a-done"><Symbol><Component Name="CopyDone" /></Symbol></Access>
                <Part Name="Contact" UId="p-enable" />
                <Part Name="Move" UId="p-move" DisabledENO="true" />
                <Part Name="SCoil" UId="p-done" />
              </Parts>
              <Wires>
                <Wire UId="w-enable-in"><Powerrail /><NameCon UId="p-enable" Name="in" /></Wire>
                <Wire UId="w-enable-op"><IdentCon UId="a-enable" /><NameCon UId="p-enable" Name="operand" /></Wire>
                <Wire UId="w-move-en"><NameCon UId="p-enable" Name="out" /><NameCon UId="p-move" Name="en" /></Wire>
                <Wire UId="w-move-in"><IdentCon UId="a-source" /><NameCon UId="p-move" Name="in" /></Wire>
                <Wire UId="w-move-out"><NameCon UId="p-move" Name="out1" /><IdentCon UId="a-target" /></Wire>
                <Wire UId="w-move-eno"><NameCon UId="p-move" Name="eno" /><NameCon UId="p-done" Name="in" /></Wire>
                <Wire UId="w-done-op"><IdentCon UId="a-done" /><NameCon UId="p-done" Name="operand" /></Wire>
              </Wires>
            </FlgNet>
            """);

        Assert.Contains("confidence: \"exact\"", yaml);
        Assert.Contains("- \"IF EnableCopy THEN TargetData := SourceData; END_IF;\"", yaml);
        Assert.Contains("- \"IF EnableCopy THEN CopyDone := TRUE; END_IF;\"", yaml);
        Assert.DoesNotContain("Skipped Move output pin 'eno'", yaml);
    }

    [Fact]
    public void TranslatesNamedCallInputsFromCallInfoParameters()
    {
        var yaml = ExportSingleLadBlockYaml(
            "NamedCallLogic",
            "cu-named-call",
            """
            <FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
              <Parts>
                <Access Scope="LocalVariable" UId="a-start-a"><Symbol><Component Name="StartA" /></Symbol></Access>
                <Access Scope="LocalVariable" UId="a-start-b"><Symbol><Component Name="StartB" /></Symbol></Access>
                <Part Name="Contact" UId="p-start-a" />
                <Part Name="Contact" UId="p-start-b" />
                <Part Name="O" UId="p-or" />
                <Call UId="c-read">
                  <CallInfo Name="ReadProgramAlarm" BlockType="FB">
                    <Instance Scope="GlobalVariable"><Component Name="ReadProgramAlarm_DB" /></Instance>
                    <Parameter Name="startAlarmWatcher" Section="Input" Type="Bool" />
                    <Parameter Name="OperateMode" Section="Output" Type="DInt" />
                  </CallInfo>
                </Call>
              </Parts>
              <Wires>
                <Wire UId="w-power"><Powerrail /><NameCon UId="p-start-a" Name="in" /><NameCon UId="p-start-b" Name="in" /><NameCon UId="c-read" Name="en" /></Wire>
                <Wire UId="w-start-a"><IdentCon UId="a-start-a" /><NameCon UId="p-start-a" Name="operand" /></Wire>
                <Wire UId="w-start-b"><IdentCon UId="a-start-b" /><NameCon UId="p-start-b" Name="operand" /></Wire>
                <Wire UId="w-or-a"><NameCon UId="p-start-a" Name="out" /><NameCon UId="p-or" Name="in1" /></Wire>
                <Wire UId="w-or-b"><NameCon UId="p-start-b" Name="out" /><NameCon UId="p-or" Name="in2" /></Wire>
                <Wire UId="w-call"><NameCon UId="p-or" Name="out" /><NameCon UId="c-read" Name="startAlarmWatcher" /></Wire>
              </Wires>
            </FlgNet>
            """);

        Assert.Contains("confidence: \"exact\"", yaml);
        Assert.Contains("- \"ReadProgramAlarm(startAlarmWatcher := (StartA OR StartB));\"", yaml);
        Assert.DoesNotContain("Skipped call ReadProgramAlarm", yaml);
    }

    [Fact]
    public void TranslatesEnabledCallWithOnlyOpenParameters()
    {
        var yaml = ExportSingleLadBlockYaml(
            "OpenParameterCallLogic",
            "cu-open-parameter-call",
            """
            <FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
              <Parts>
                <Call UId="c-time-base">
                  <CallInfo Name="Time_Base" BlockType="FB">
                    <Instance Scope="LocalVariable"><Component Name="Time_Base_Instance" /></Instance>
                    <Parameter Name="Time_Base_ms" Section="Input" Type="DInt" />
                    <Parameter Name="Out_OS" Section="Output" Type="Bool" />
                    <Parameter Name="Out_Blink" Section="Output" Type="Bool" />
                  </CallInfo>
                </Call>
              </Parts>
              <Wires>
                <Wire UId="w-enable"><Powerrail /><NameCon UId="c-time-base" Name="en" /></Wire>
                <Wire UId="w-input"><OpenCon UId="o-input" /><NameCon UId="c-time-base" Name="Time_Base_ms" /></Wire>
                <Wire UId="w-out-os"><NameCon UId="c-time-base" Name="Out_OS" /><OpenCon UId="o-os" /></Wire>
                <Wire UId="w-out-blink"><NameCon UId="c-time-base" Name="Out_Blink" /><OpenCon UId="o-blink" /></Wire>
              </Wires>
            </FlgNet>
            """);

        Assert.Contains("confidence: \"exact\"", yaml);
        Assert.Contains("- \"Time_Base();\"", yaml);
        Assert.DoesNotContain("Skipped call Time_Base because no parameters could be resolved by symbol name.", yaml);
    }

    [Fact]
    public void TranslatesZeroInputTimeReadFunctionsAsOutputBindingCalls()
    {
        var yaml = ExportSingleLadBlockYaml(
            "TimeReadLogic",
            "cu-time-read",
            """
            <FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
              <Parts>
                <Access Scope="LocalVariable" UId="a-ret"><Symbol><Component Name="RET_VAL_RD_LOC_T" /></Symbol></Access>
                <Access Scope="LocalVariable" UId="a-clock"><Symbol><Component Name="Clock_Local" /></Symbol></Access>
                <Part Name="RD_LOC_T" Version="1.0" UId="p-read" />
              </Parts>
              <Wires>
                <Wire UId="w-enable"><Powerrail /><NameCon UId="p-read" Name="en" /></Wire>
                <Wire UId="w-ret"><NameCon UId="p-read" Name="RET_VAL" /><IdentCon UId="a-ret" /></Wire>
                <Wire UId="w-out"><NameCon UId="p-read" Name="OUT" /><IdentCon UId="a-clock" /></Wire>
              </Wires>
            </FlgNet>
            """);

        Assert.Contains("confidence: \"exact\"", yaml);
        Assert.Contains("- \"RD_LOC_T(OUT => Clock_Local, RET_VAL => RET_VAL_RD_LOC_T);\"", yaml);
        Assert.DoesNotContain("Skipped RD_LOC_T output", yaml);
    }

    [Fact]
    public void TranslatesNamedLocalConstantsInLadExpressions()
    {
        var yaml = ExportSingleLadBlockYaml(
            "LocalConstantLogic",
            "cu-local-constant",
            """
            <FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
              <Parts>
                <Access Scope="LocalVariable" UId="a-speed"><Symbol><Component Name="SpeedFeedback" /></Symbol></Access>
                <Access Scope="LocalConstant" UId="a-max-rpm"><Constant Name="Max_Speed_RPM" /></Access>
                <Access Scope="LocalConstant" UId="a-max-hz"><Constant Name="Max_Speed_Hertz" /></Access>
                <Access Scope="LiteralConstant" UId="a-scale"><Constant><ConstantValue>100</ConstantValue></Constant></Access>
                <Access Scope="LocalVariable" UId="a-current"><Symbol><Component Name="Current_Speed" /></Symbol></Access>
                <Access Scope="LocalVariable" UId="a-temp"><Symbol><Component Name="Simu_temp" /></Symbol></Access>
                <Access Scope="LocalConstant" UId="a-gain"><Constant Name="Gain_Temp_Heat" /></Access>
                <Access Scope="LocalVariable" UId="a-next-temp"><Symbol><Component Name="Next_Simu_temp" /></Symbol></Access>
                <Part Name="Calc" UId="p-calc"><Equation>IN1*IN3/IN2</Equation></Part>
                <Part Name="Add" UId="p-add" />
              </Parts>
              <Wires>
                <Wire UId="w-calc-en"><Powerrail /><NameCon UId="p-calc" Name="en" /><NameCon UId="p-add" Name="en" /></Wire>
                <Wire UId="w-calc-in1"><IdentCon UId="a-speed" /><NameCon UId="p-calc" Name="IN1" /></Wire>
                <Wire UId="w-calc-in2"><IdentCon UId="a-max-hz" /><NameCon UId="p-calc" Name="IN2" /></Wire>
                <Wire UId="w-calc-in3"><IdentCon UId="a-max-rpm" /><NameCon UId="p-calc" Name="IN3" /></Wire>
                <Wire UId="w-calc-out"><NameCon UId="p-calc" Name="out" /><IdentCon UId="a-current" /></Wire>
                <Wire UId="w-add-in1"><IdentCon UId="a-temp" /><NameCon UId="p-add" Name="in1" /></Wire>
                <Wire UId="w-add-in2"><IdentCon UId="a-gain" /><NameCon UId="p-add" Name="in2" /></Wire>
                <Wire UId="w-add-out"><NameCon UId="p-add" Name="out" /><IdentCon UId="a-next-temp" /></Wire>
              </Wires>
            </FlgNet>
            """);

        Assert.Contains("confidence: \"exact\"", yaml);
        Assert.Contains("- \"Current_Speed := SpeedFeedback*Max_Speed_RPM/Max_Speed_Hertz;\"", yaml);
        Assert.Contains("- \"Next_Simu_temp := (Simu_temp + Gain_Temp_Heat);\"", yaml);
        Assert.DoesNotContain("Skipped Calc output because IN3 could not be resolved.", yaml);
        Assert.DoesNotContain("Skipped Add output because fewer than two input pins could be resolved.", yaml);
    }

    [Fact]
    public void TranslatesMoveCardOutputsAndIn1Source()
    {
        var yaml = ExportSingleLadBlockYaml(
            "MoveCardLogic",
            "cu-move-card",
            """
            <FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
              <Parts>
                <Access Scope="LocalVariable" UId="a-enable"><Symbol><Component Name="EnableMove" /></Symbol></Access>
                <Access Scope="LocalVariable" UId="a-source"><Symbol><Component Name="SourceValue" /></Symbol></Access>
                <Access Scope="LocalVariable" UId="a-target"><Symbol><Component Name="SecondTarget" /></Symbol></Access>
                <Part Name="Contact" UId="p-enable" />
                <Part Name="Move" UId="p-move"><TemplateValue Name="Card" Type="Cardinality">2</TemplateValue></Part>
              </Parts>
              <Wires>
                <Wire UId="w-enable-in"><Powerrail /><NameCon UId="p-enable" Name="in" /></Wire>
                <Wire UId="w-enable-op"><IdentCon UId="a-enable" /><NameCon UId="p-enable" Name="operand" /></Wire>
                <Wire UId="w-enable-out"><NameCon UId="p-enable" Name="out" /><NameCon UId="p-move" Name="en" /></Wire>
                <Wire UId="w-source"><IdentCon UId="a-source" /><NameCon UId="p-move" Name="in1" /></Wire>
                <Wire UId="w-target"><NameCon UId="p-move" Name="out2" /><IdentCon UId="a-target" /></Wire>
              </Wires>
            </FlgNet>
            """);

        Assert.Contains("confidence: \"exact\"", yaml);
        Assert.Contains("- \"IF EnableMove THEN SecondTarget := SourceValue; END_IF;\"", yaml);
        Assert.DoesNotContain("Skipped Move output", yaml);
    }

    [Fact]
    public void TranslatesBuiltInSrPartAndQOutput()
    {
        var yaml = ExportSingleLadBlockYaml(
            "SrLogic",
            "cu-sr",
            """
            <FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
              <Parts>
                <Access Scope="LocalVariable" UId="a-set"><Symbol><Component Name="SetRequest" /></Symbol></Access>
                <Access Scope="LocalVariable" UId="a-reset"><Symbol><Component Name="ResetRequest" /></Symbol></Access>
                <Access Scope="LocalVariable" UId="a-memory"><Symbol><Component Name="LatchedMemory" /></Symbol></Access>
                <Access Scope="LocalVariable" UId="a-alarm"><Symbol><Component Name="Alarm" /></Symbol></Access>
                <Part Name="Contact" UId="p-set" />
                <Part Name="Contact" UId="p-reset" />
                <Part Name="Sr" UId="p-sr" />
                <Part Name="Coil" UId="p-alarm" />
              </Parts>
              <Wires>
                <Wire UId="w-power"><Powerrail /><NameCon UId="p-set" Name="in" /><NameCon UId="p-reset" Name="in" /></Wire>
                <Wire UId="w-set-op"><IdentCon UId="a-set" /><NameCon UId="p-set" Name="operand" /></Wire>
                <Wire UId="w-reset-op"><IdentCon UId="a-reset" /><NameCon UId="p-reset" Name="operand" /></Wire>
                <Wire UId="w-set"><NameCon UId="p-set" Name="out" /><NameCon UId="p-sr" Name="s" /></Wire>
                <Wire UId="w-reset"><NameCon UId="p-reset" Name="out" /><NameCon UId="p-sr" Name="r1" /></Wire>
                <Wire UId="w-memory"><IdentCon UId="a-memory" /><NameCon UId="p-sr" Name="operand" /></Wire>
                <Wire UId="w-q"><NameCon UId="p-sr" Name="q" /><NameCon UId="p-alarm" Name="in" /></Wire>
                <Wire UId="w-alarm"><IdentCon UId="a-alarm" /><NameCon UId="p-alarm" Name="operand" /></Wire>
              </Wires>
            </FlgNet>
            """);

        Assert.Contains("confidence: \"exact\"", yaml);
        Assert.Contains("- \"IF ResetRequest THEN LatchedMemory := FALSE; END_IF;\"", yaml);
        Assert.Contains("- \"IF SetRequest THEN LatchedMemory := TRUE; END_IF;\"", yaml);
        Assert.Contains("- \"Alarm := LatchedMemory;\"", yaml);
        Assert.DoesNotContain("Skipped Sr", yaml);
    }

    [Fact]
    public void TreatsEmptyStructuredTextNetworkAsExactEmpty()
    {
        var root = Path.Combine(Path.GetTempPath(), "PlcSourceExporterTests", Guid.NewGuid().ToString("N"));
        var service = new PlcExportService(() => new DateTimeOffset(2026, 7, 7, 4, 5, 6, TimeSpan.Zero));
        var plc = FakePlcSoftware.Create(
            blocks:
            [
                new FakeExportableObject(
                    "Blocks/EmptyScl",
                    "Function",
                    exportContent: """
                        <?xml version="1.0" encoding="utf-8"?>
                        <Document>
                          <SW.Blocks.FC ID="0">
                            <AttributeList><Name>EmptyScl</Name><ProgrammingLanguage>SCL</ProgrammingLanguage></AttributeList>
                            <ObjectList>
                              <SW.Blocks.CompileUnit ID="cu-empty-scl">
                                <AttributeList>
                                  <NetworkSource><StructuredText xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/StructuredText/v3"><NewLine Num="1" UId="21" /></StructuredText></NetworkSource>
                                  <ProgrammingLanguage>SCL</ProgrammingLanguage>
                                </AttributeList>
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

        Assert.Contains("compileUnitId: \"cu-empty-scl\"", yaml);
        Assert.Contains("language: \"scl\"", yaml);
        Assert.Contains("confidence: \"exact\"", yaml);
        Assert.DoesNotContain("Unsupported network language or missing FlgNet source.", yaml);
    }

    [Fact]
    public void TranslatesReturnValueAndArrayOperands()
    {
        var yaml = ExportSingleLadBlockYaml(
            "ReturnLogic",
            "cu-return-value",
            """
            <FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
              <Parts>
                <Access Scope="GlobalVariable" UId="a-test">
                  <Symbol>
                    <Component Name="PC" />
                    <Component Name="Test" AccessModifier="Array">
                      <Access Scope="LiteralConstant"><Constant><ConstantValue>1</ConstantValue></Constant></Access>
                    </Component>
                  </Symbol>
                </Access>
                <Access Scope="LiteralConstant" UId="a-true"><Constant><ConstantValue>true</ConstantValue></Constant></Access>
                <Part Name="Contact" UId="p-test" />
                <Part Name="ReturnValue" UId="p-return" />
              </Parts>
              <Wires>
                <Wire UId="w-power"><Powerrail /><NameCon UId="p-test" Name="in" /></Wire>
                <Wire UId="w-test"><IdentCon UId="a-test" /><NameCon UId="p-test" Name="operand" /></Wire>
                <Wire UId="w-return-in"><NameCon UId="p-test" Name="out" /><NameCon UId="p-return" Name="in" /></Wire>
                <Wire UId="w-return-value"><IdentCon UId="a-true" /><NameCon UId="p-return" Name="operand" /></Wire>
              </Wires>
            </FlgNet>
            """);

        Assert.Contains("confidence: \"exact\"", yaml);
        Assert.Contains("- \"IF PC.Test[1] THEN RETURN true; END_IF;\"", yaml);
        Assert.DoesNotContain("No supported coils or resolvable call statements were found.", yaml);
    }

    [Fact]
    public void TranslatesJumpToLabel()
    {
        var yaml = ExportSingleLadBlockYaml(
            "JumpLogic",
            "cu-jump",
            """
            <FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
              <Parts>
                <Access Scope="LocalVariable" UId="a-sample"><Symbol><Component Name="Sample_ms" /></Symbol></Access>
                <Access Scope="LiteralConstant" UId="a-zero"><Constant><ConstantValue>0</ConstantValue></Constant></Access>
                <Access Scope="Label" UId="a-label"><Label Name="OUTP" /></Access>
                <Part Name="Ne" UId="p-ne" />
                <Part Name="Jump" UId="p-jump" />
              </Parts>
              <Wires>
                <Wire UId="w-power"><Powerrail /><NameCon UId="p-ne" Name="pre" /></Wire>
                <Wire UId="w-left"><IdentCon UId="a-sample" /><NameCon UId="p-ne" Name="in1" /></Wire>
                <Wire UId="w-right"><IdentCon UId="a-zero" /><NameCon UId="p-ne" Name="in2" /></Wire>
                <Wire UId="w-jump-in"><NameCon UId="p-ne" Name="out" /><NameCon UId="p-jump" Name="in" /></Wire>
                <Wire UId="w-label"><IdentCon UId="a-label" /><NameCon UId="p-jump" Name="label" /></Wire>
              </Wires>
            </FlgNet>
            """);

        Assert.Contains("confidence: \"exact\"", yaml);
        Assert.Contains("- \"IF (Sample_ms <> 0) THEN GOTO OUTP; END_IF;\"", yaml);
        Assert.DoesNotContain("Skipped Jump", yaml);
    }

    [Fact]
    public void TranslatesPulseCoilAndIncrement()
    {
        var yaml = ExportSingleLadBlockYaml(
            "PulseAndIncrementLogic",
            "cu-pulse-inc",
            """
            <FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
              <Parts>
                <Access Scope="LocalVariable" UId="a-direction"><Symbol><Component Name="O_Direction_1" /></Symbol></Access>
                <Access Scope="LocalVariable" UId="a-pulse"><Symbol><Component Name="OS_Speed_Front_OFF" /></Symbol></Access>
                <Access Scope="LocalVariable" UId="a-bit"><Symbol><Component Name="BO" SliceAccessModifier="x1" /></Symbol></Access>
                <Access Scope="LocalVariable" UId="a-count"><Symbol><Component Name="Nb_cures" /></Symbol></Access>
                <Part Name="Contact" UId="p-direction" />
                <Part Name="PCoil" UId="p-pcoil" />
                <Part Name="Inc" UId="p-inc" />
              </Parts>
              <Wires>
                <Wire UId="w-power"><Powerrail /><NameCon UId="p-direction" Name="in" /></Wire>
                <Wire UId="w-direction"><IdentCon UId="a-direction" /><NameCon UId="p-direction" Name="operand" /></Wire>
                <Wire UId="w-pcoil-in"><NameCon UId="p-direction" Name="out" /><NameCon UId="p-pcoil" Name="in" /><NameCon UId="p-inc" Name="en" /></Wire>
                <Wire UId="w-pcoil-bit"><IdentCon UId="a-bit" /><NameCon UId="p-pcoil" Name="bit" /></Wire>
                <Wire UId="w-pcoil-op"><IdentCon UId="a-pulse" /><NameCon UId="p-pcoil" Name="operand" /></Wire>
                <Wire UId="w-inc-op"><IdentCon UId="a-count" /><NameCon UId="p-inc" Name="operand" /></Wire>
              </Wires>
            </FlgNet>
            """);

        Assert.Contains("confidence: \"exact\"", yaml);
        Assert.Contains("- \"OS_Speed_Front_OFF := PULSE(O_Direction_1, BO.%X1);\"", yaml);
        Assert.Contains("- \"IF O_Direction_1 THEN Nb_cures := Nb_cures + 1; END_IF;\"", yaml);
        Assert.DoesNotContain("No supported coils or resolvable call statements were found.", yaml);
    }

    [Fact]
    public void TranslatesInstanceInstructionParts()
    {
        var yaml = ExportSingleLadBlockYaml(
            "InstanceInstructionLogic",
            "cu-instance-instruction",
            """
            <FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
              <Parts>
                <Access Scope="LocalVariable" UId="a-hwid"><Symbol><Component Name="I_HWIDSTWZSW" /></Symbol></Access>
                <Part Name="SinaPos" UId="p-sina">
                  <Instance Scope="LocalVariable"><Component Name="SinaPos_Instance" /></Instance>
                </Part>
              </Parts>
              <Wires>
                <Wire UId="w-enable"><Powerrail /><NameCon UId="p-sina" Name="en" /></Wire>
                <Wire UId="w-stw"><IdentCon UId="a-hwid" /><NameCon UId="p-sina" Name="HWIDSTW" /></Wire>
                <Wire UId="w-zsw"><IdentCon UId="a-hwid" /><NameCon UId="p-sina" Name="HWIDZSW" /></Wire>
              </Wires>
            </FlgNet>
            """);

        Assert.Contains("confidence: \"exact\"", yaml);
        Assert.Contains("- \"SinaPos_Instance(HWIDSTW := I_HWIDSTWZSW, HWIDZSW := I_HWIDSTWZSW);\"", yaml);
        Assert.DoesNotContain("No supported coils or resolvable call statements were found.", yaml);
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
    public void TranslatesParameterlessCallGuardedByNegatedContact()
    {
        var root = Path.Combine(Path.GetTempPath(), "PlcSourceExporterTests", Guid.NewGuid().ToString("N"));
        var service = new PlcExportService(() => new DateTimeOffset(2026, 7, 7, 4, 5, 6, TimeSpan.Zero));
        var plc = FakePlcSoftware.Create(
            blocks:
            [
                new FakeExportableObject(
                    "Blocks/GuardedCall",
                    "OrganizationBlock",
                    exportContent: """
                        <?xml version="1.0" encoding="utf-8"?>
                        <Document>
                          <SW.Blocks.OB ID="0">
                            <AttributeList><Name>GuardedCall</Name><ProgrammingLanguage>LAD</ProgrammingLanguage></AttributeList>
                            <ObjectList>
                              <SW.Blocks.CompileUnit ID="cu-guarded-call">
                                <AttributeList>
                                  <NetworkSource>
                                    <FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
                                      <Parts>
                                        <Access Scope="GlobalVariable" UId="a-start"><Symbol><Component Name="Starting_Value" /><Component Name="Tests_IO" /></Symbol></Access>
                                        <Part Name="Contact" UId="p-start"><Negated Name="operand" /></Part>
                                        <Call UId="c-press">
                                          <CallInfo Name="210_Press_Frame_Cav_A" BlockType="FC" />
                                        </Call>
                                      </Parts>
                                      <Wires>
                                        <Wire UId="w-start-in"><Powerrail /><NameCon UId="p-start" Name="in" /></Wire>
                                        <Wire UId="w-start-op"><IdentCon UId="a-start" /><NameCon UId="p-start" Name="operand" /></Wire>
                                        <Wire UId="w-call-en"><NameCon UId="p-start" Name="out" /><NameCon UId="c-press" Name="en" /></Wire>
                                      </Wires>
                                    </FlgNet>
                                  </NetworkSource>
                                  <ProgrammingLanguage>LAD</ProgrammingLanguage>
                                </AttributeList>
                              </SW.Blocks.CompileUnit>
                            </ObjectList>
                          </SW.Blocks.OB>
                        </Document>
                        """)
            ],
            types: [],
            tagTables: []);

        service.Export(plc, root);
        var yaml = File.ReadAllText(Path.Combine(root, "translate", "program-blocks.yaml"));

        Assert.Contains("name: \"GuardedCall\"", yaml);
        Assert.Contains("compileUnitId: \"cu-guarded-call\"", yaml);
        Assert.Contains("confidence: \"exact\"", yaml);
        Assert.Contains("- \"IF NOT Starting_Value.Tests_IO THEN 210_Press_Frame_Cav_A(); END_IF;\"", yaml);
        Assert.DoesNotContain("Skipped call 210_Press_Frame_Cav_A because no parameters could be resolved by symbol name.", yaml);
    }

    [Fact]
    public void TranslatesSetAndResetCoils()
    {
        var yaml = ExportSingleLadBlockYaml(
            "LatchLogic",
            "cu-latches",
            """
            <FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
              <Parts>
                <Access Scope="LocalVariable" UId="a-start"><Symbol><Component Name="Start" /></Symbol></Access>
                <Access Scope="LocalVariable" UId="a-latch"><Symbol><Component Name="Latched" /></Symbol></Access>
                <Access Scope="LocalVariable" UId="a-reset"><Symbol><Component Name="ResetRequest" /></Symbol></Access>
                <Part Name="Contact" UId="p-start" />
                <Part Name="Contact" UId="p-reset" />
                <Part Name="SCoil" UId="p-set" />
                <Part Name="RCoil" UId="p-reset-coil" />
              </Parts>
              <Wires>
                <Wire UId="w-start-in"><Powerrail /><NameCon UId="p-start" Name="in" /></Wire>
                <Wire UId="w-start-op"><IdentCon UId="a-start" /><NameCon UId="p-start" Name="operand" /></Wire>
                <Wire UId="w-start-out"><NameCon UId="p-start" Name="out" /><NameCon UId="p-set" Name="in" /></Wire>
                <Wire UId="w-set-op"><IdentCon UId="a-latch" /><NameCon UId="p-set" Name="operand" /></Wire>
                <Wire UId="w-reset-in"><Powerrail /><NameCon UId="p-reset" Name="in" /></Wire>
                <Wire UId="w-reset-op"><IdentCon UId="a-reset" /><NameCon UId="p-reset" Name="operand" /></Wire>
                <Wire UId="w-reset-out"><NameCon UId="p-reset" Name="out" /><NameCon UId="p-reset-coil" Name="in" /></Wire>
                <Wire UId="w-reset-coil-op"><IdentCon UId="a-latch" /><NameCon UId="p-reset-coil" Name="operand" /></Wire>
              </Wires>
            </FlgNet>
            """);

        Assert.Contains("compileUnitId: \"cu-latches\"", yaml);
        Assert.Contains("confidence: \"exact\"", yaml);
        Assert.Contains("- \"IF Start THEN Latched := TRUE; END_IF;\"", yaml);
        Assert.Contains("- \"IF ResetRequest THEN Latched := FALSE; END_IF;\"", yaml);
    }

    [Fact]
    public void TranslatesLatchCoilOutputAsInputCondition()
    {
        var yaml = ExportSingleLadBlockYaml(
            "ChainedLatchLogic",
            "cu-chained-latches",
            """
            <FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
              <Parts>
                <Access Scope="LocalVariable" UId="a-start"><Symbol><Component Name="Start" /></Symbol></Access>
                <Access Scope="LocalVariable" UId="a-latch"><Symbol><Component Name="Latched" /></Symbol></Access>
                <Access Scope="LocalVariable" UId="a-after"><Symbol><Component Name="AfterLatch" /></Symbol></Access>
                <Part Name="Contact" UId="p-start" />
                <Part Name="SCoil" UId="p-set" />
                <Part Name="Coil" UId="p-after" />
              </Parts>
              <Wires>
                <Wire UId="w-start-in"><Powerrail /><NameCon UId="p-start" Name="in" /></Wire>
                <Wire UId="w-start-op"><IdentCon UId="a-start" /><NameCon UId="p-start" Name="operand" /></Wire>
                <Wire UId="w-start-out"><NameCon UId="p-start" Name="out" /><NameCon UId="p-set" Name="in" /></Wire>
                <Wire UId="w-set-op"><IdentCon UId="a-latch" /><NameCon UId="p-set" Name="operand" /></Wire>
                <Wire UId="w-set-out"><NameCon UId="p-set" Name="out" /><NameCon UId="p-after" Name="in" /></Wire>
                <Wire UId="w-after-op"><IdentCon UId="a-after" /><NameCon UId="p-after" Name="operand" /></Wire>
              </Wires>
            </FlgNet>
            """);

        Assert.Contains("confidence: \"exact\"", yaml);
        Assert.Contains("- \"IF Start THEN Latched := TRUE; END_IF;\"", yaml);
        Assert.Contains("- \"AfterLatch := Start;\"", yaml);
        Assert.DoesNotContain("Unsupported LAD/FBD part 'SCoil'", yaml);
    }

    [Fact]
    public void TranslatesMoveDirectAssignment()
    {
        var yaml = ExportSingleLadBlockYaml(
            "MoveLogic",
            "cu-move",
            """
            <FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
              <Parts>
                <Access Scope="LocalVariable" UId="a-enable"><Symbol><Component Name="EnableMove" /></Symbol></Access>
                <Access Scope="LocalVariable" UId="a-source"><Symbol><Component Name="SourceValue" /></Symbol></Access>
                <Access Scope="LocalVariable" UId="a-target"><Symbol><Component Name="TargetValue" /></Symbol></Access>
                <Part Name="Contact" UId="p-enable" />
                <Part Name="Move" UId="p-move" />
              </Parts>
              <Wires>
                <Wire UId="w-enable-in"><Powerrail /><NameCon UId="p-enable" Name="in" /></Wire>
                <Wire UId="w-enable-op"><IdentCon UId="a-enable" /><NameCon UId="p-enable" Name="operand" /></Wire>
                <Wire UId="w-enable-out"><NameCon UId="p-enable" Name="out" /><NameCon UId="p-move" Name="en" /></Wire>
                <Wire UId="w-source"><IdentCon UId="a-source" /><NameCon UId="p-move" Name="in" /></Wire>
                <Wire UId="w-target"><NameCon UId="p-move" Name="out1" /><IdentCon UId="a-target" /></Wire>
              </Wires>
            </FlgNet>
            """);

        Assert.Contains("confidence: \"exact\"", yaml);
        Assert.Contains("- \"IF EnableMove THEN TargetValue := SourceValue; END_IF;\"", yaml);
        Assert.DoesNotContain("Unsupported LAD/FBD part 'Move'", yaml);
    }

    [Fact]
    public void TranslatesTimerOnDelayOutput()
    {
        var yaml = ExportSingleLadBlockYaml(
            "TimerLogic",
            "cu-ton",
            """
            <FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
              <Parts>
                <Access Scope="LocalVariable" UId="a-over"><Symbol><Component Name="OverPressure" /></Symbol></Access>
                <Access Scope="LiteralConstant" UId="a-time"><Constant><ConstantValue>T#2s</ConstantValue></Constant></Access>
                <Access Scope="LocalVariable" UId="a-alarm"><Symbol><Component Name="Alarm" /></Symbol></Access>
                <Part Name="Contact" UId="p-over" />
                <Part Name="TON" UId="p-ton">
                  <Instance Scope="GlobalVariable"><Component Name="T_OverPressure" /></Instance>
                </Part>
                <Part Name="Coil" UId="p-coil" />
              </Parts>
              <Wires>
                <Wire UId="w-over-in"><Powerrail /><NameCon UId="p-over" Name="in" /></Wire>
                <Wire UId="w-over-op"><IdentCon UId="a-over" /><NameCon UId="p-over" Name="operand" /></Wire>
                <Wire UId="w-over-out"><NameCon UId="p-over" Name="out" /><NameCon UId="p-ton" Name="IN" /></Wire>
                <Wire UId="w-pt"><IdentCon UId="a-time" /><NameCon UId="p-ton" Name="PT" /></Wire>
                <Wire UId="w-q"><NameCon UId="p-ton" Name="Q" /><NameCon UId="p-coil" Name="in" /></Wire>
                <Wire UId="w-coil-op"><IdentCon UId="a-alarm" /><NameCon UId="p-coil" Name="operand" /></Wire>
              </Wires>
            </FlgNet>
            """);

        Assert.Contains("confidence: \"exact\"", yaml);
        Assert.Contains("- \"Alarm := T_OverPressure.Q;\"", yaml);
        Assert.Contains("- \"T_OverPressure(IN := OverPressure, PT := T#2s);\"", yaml);
    }

    [Fact]
    public void TranslatesPositiveContact()
    {
        var yaml = ExportSingleLadBlockYaml(
            "PulseLogic",
            "cu-pulse",
            """
            <FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
              <Parts>
                <Access Scope="LocalVariable" UId="a-enable"><Symbol><Component Name="EnablePulse" /></Symbol></Access>
                <Access Scope="LocalVariable" UId="a-signal"><Symbol><Component Name="Sensor" /></Symbol></Access>
                <Access Scope="LocalVariable" UId="a-bit"><Symbol><Component Name="SensorEdgeMemory" /></Symbol></Access>
                <Access Scope="LocalVariable" UId="a-target"><Symbol><Component Name="PulseSeen" /></Symbol></Access>
                <Part Name="Contact" UId="p-enable" />
                <Part Name="PContact" UId="p-pulse" />
                <Part Name="Coil" UId="p-coil" />
              </Parts>
              <Wires>
                <Wire UId="w-enable-in"><Powerrail /><NameCon UId="p-enable" Name="in" /></Wire>
                <Wire UId="w-enable-op"><IdentCon UId="a-enable" /><NameCon UId="p-enable" Name="operand" /></Wire>
                <Wire UId="w-enable-out"><NameCon UId="p-enable" Name="out" /><NameCon UId="p-pulse" Name="pre" /></Wire>
                <Wire UId="w-signal"><IdentCon UId="a-signal" /><NameCon UId="p-pulse" Name="operand" /></Wire>
                <Wire UId="w-bit"><IdentCon UId="a-bit" /><NameCon UId="p-pulse" Name="bit" /></Wire>
                <Wire UId="w-pulse-out"><NameCon UId="p-pulse" Name="out" /><NameCon UId="p-coil" Name="in" /></Wire>
                <Wire UId="w-target"><IdentCon UId="a-target" /><NameCon UId="p-coil" Name="operand" /></Wire>
              </Wires>
            </FlgNet>
            """);

        Assert.Contains("confidence: \"exact\"", yaml);
        Assert.Contains("- \"PulseSeen := (EnablePulse AND PULSE(Sensor, SensorEdgeMemory));\"", yaml);
    }

    [Fact]
    public void TranslatesCalcDirectAssignment()
    {
        var yaml = ExportSingleLadBlockYaml(
            "CalcLogic",
            "cu-calc",
            """
            <FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
              <Parts>
                <Access Scope="LocalVariable" UId="a-in1"><Symbol><Component Name="Flow" /></Symbol></Access>
                <Access Scope="LocalVariable" UId="a-in2"><Symbol><Component Name="Scale" /></Symbol></Access>
                <Access Scope="LocalVariable" UId="a-in3"><Symbol><Component Name="Offset" /></Symbol></Access>
                <Access Scope="LocalVariable" UId="a-result"><Symbol><Component Name="PressureFlow" /></Symbol></Access>
                <Part Name="Calc" UId="p-calc"><Equation>IN1*IN2+IN3</Equation></Part>
              </Parts>
              <Wires>
                <Wire UId="w-in1"><IdentCon UId="a-in1" /><NameCon UId="p-calc" Name="IN1" /></Wire>
                <Wire UId="w-in2"><IdentCon UId="a-in2" /><NameCon UId="p-calc" Name="IN2" /></Wire>
                <Wire UId="w-in3"><IdentCon UId="a-in3" /><NameCon UId="p-calc" Name="IN3" /></Wire>
                <Wire UId="w-out"><NameCon UId="p-calc" Name="OUT" /><IdentCon UId="a-result" /></Wire>
              </Wires>
            </FlgNet>
            """);

        Assert.Contains("confidence: \"exact\"", yaml);
        Assert.Contains("- \"PressureFlow := Flow*Scale+Offset;\"", yaml);
    }

    [Fact]
    public void TranslatesInRangeExpression()
    {
        var yaml = ExportSingleLadBlockYaml(
            "RangeLogic",
            "cu-range",
            """
            <FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
              <Parts>
                <Access Scope="LocalVariable" UId="a-enable"><Symbol><Component Name="CheckEnabled" /></Symbol></Access>
                <Access Scope="LiteralConstant" UId="a-min"><Constant><ConstantValue>10</ConstantValue></Constant></Access>
                <Access Scope="LocalVariable" UId="a-value"><Symbol><Component Name="ActualPressure" /></Symbol></Access>
                <Access Scope="LiteralConstant" UId="a-max"><Constant><ConstantValue>20</ConstantValue></Constant></Access>
                <Access Scope="LocalVariable" UId="a-ok"><Symbol><Component Name="PressureOk" /></Symbol></Access>
                <Part Name="Contact" UId="p-enable" />
                <Part Name="InRange" UId="p-range" />
                <Part Name="Coil" UId="p-coil" />
              </Parts>
              <Wires>
                <Wire UId="w-enable-in"><Powerrail /><NameCon UId="p-enable" Name="in" /></Wire>
                <Wire UId="w-enable-op"><IdentCon UId="a-enable" /><NameCon UId="p-enable" Name="operand" /></Wire>
                <Wire UId="w-enable-out"><NameCon UId="p-enable" Name="out" /><NameCon UId="p-range" Name="pre" /></Wire>
                <Wire UId="w-min"><IdentCon UId="a-min" /><NameCon UId="p-range" Name="min" /></Wire>
                <Wire UId="w-in"><IdentCon UId="a-value" /><NameCon UId="p-range" Name="in" /></Wire>
                <Wire UId="w-max"><IdentCon UId="a-max" /><NameCon UId="p-range" Name="max" /></Wire>
                <Wire UId="w-out"><NameCon UId="p-range" Name="out" /><NameCon UId="p-coil" Name="in" /></Wire>
                <Wire UId="w-coil-op"><IdentCon UId="a-ok" /><NameCon UId="p-coil" Name="operand" /></Wire>
              </Wires>
            </FlgNet>
            """);

        Assert.Contains("confidence: \"exact\"", yaml);
        Assert.Contains("- \"PressureOk := (CheckEnabled AND (10 <= ActualPressure AND ActualPressure <= 20));\"", yaml);
    }

    [Fact]
    public void TranslatesArithmeticPartsAsExpressions()
    {
        var yaml = ExportSingleLadBlockYaml(
            "ArithmeticLogic",
            "cu-arithmetic",
            """
            <FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
              <Parts>
                <Access Scope="LocalVariable" UId="a-enable"><Symbol><Component Name="EnableMath" /></Symbol></Access>
                <Access Scope="LocalVariable" UId="a-left"><Symbol><Component Name="Left" /></Symbol></Access>
                <Access Scope="LocalVariable" UId="a-right"><Symbol><Component Name="Right" /></Symbol></Access>
                <Access Scope="LocalVariable" UId="a-sum"><Symbol><Component Name="Sum" /></Symbol></Access>
                <Access Scope="LocalVariable" UId="a-delta"><Symbol><Component Name="Delta" /></Symbol></Access>
                <Access Scope="LocalVariable" UId="a-product"><Symbol><Component Name="Product" /></Symbol></Access>
                <Access Scope="LocalVariable" UId="a-ratio"><Symbol><Component Name="Ratio" /></Symbol></Access>
                <Access Scope="LocalVariable" UId="a-mod"><Symbol><Component Name="Remainder" /></Symbol></Access>
                <Access Scope="LocalVariable" UId="a-neg"><Symbol><Component Name="NegativeLeft" /></Symbol></Access>
                <Part Name="Contact" UId="p-enable" />
                <Part Name="Add" UId="p-add" />
                <Part Name="Sub" UId="p-sub" />
                <Part Name="Mul" UId="p-mul" />
                <Part Name="Div" UId="p-div" />
                <Part Name="Mod" UId="p-mod" />
                <Part Name="Neg" UId="p-neg" />
              </Parts>
              <Wires>
                <Wire UId="w-enable-in"><Powerrail /><NameCon UId="p-enable" Name="in" /></Wire>
                <Wire UId="w-enable-op"><IdentCon UId="a-enable" /><NameCon UId="p-enable" Name="operand" /></Wire>
                <Wire UId="w-add-en"><NameCon UId="p-enable" Name="out" /><NameCon UId="p-add" Name="en" /></Wire>
                <Wire UId="w-left-add"><IdentCon UId="a-left" /><NameCon UId="p-add" Name="in1" /></Wire>
                <Wire UId="w-right-add"><IdentCon UId="a-right" /><NameCon UId="p-add" Name="in2" /></Wire>
                <Wire UId="w-add-out"><NameCon UId="p-add" Name="out" /><IdentCon UId="a-sum" /></Wire>
                <Wire UId="w-left-sub"><IdentCon UId="a-left" /><NameCon UId="p-sub" Name="in1" /></Wire>
                <Wire UId="w-right-sub"><IdentCon UId="a-right" /><NameCon UId="p-sub" Name="in2" /></Wire>
                <Wire UId="w-sub-out"><NameCon UId="p-sub" Name="out" /><IdentCon UId="a-delta" /></Wire>
                <Wire UId="w-left-mul"><IdentCon UId="a-left" /><NameCon UId="p-mul" Name="in1" /></Wire>
                <Wire UId="w-right-mul"><IdentCon UId="a-right" /><NameCon UId="p-mul" Name="in2" /></Wire>
                <Wire UId="w-mul-out"><NameCon UId="p-mul" Name="out" /><IdentCon UId="a-product" /></Wire>
                <Wire UId="w-left-div"><IdentCon UId="a-left" /><NameCon UId="p-div" Name="in1" /></Wire>
                <Wire UId="w-right-div"><IdentCon UId="a-right" /><NameCon UId="p-div" Name="in2" /></Wire>
                <Wire UId="w-div-out"><NameCon UId="p-div" Name="out" /><IdentCon UId="a-ratio" /></Wire>
                <Wire UId="w-left-mod"><IdentCon UId="a-left" /><NameCon UId="p-mod" Name="in1" /></Wire>
                <Wire UId="w-right-mod"><IdentCon UId="a-right" /><NameCon UId="p-mod" Name="in2" /></Wire>
                <Wire UId="w-mod-out"><NameCon UId="p-mod" Name="out" /><IdentCon UId="a-mod" /></Wire>
                <Wire UId="w-left-neg"><IdentCon UId="a-left" /><NameCon UId="p-neg" Name="in" /></Wire>
                <Wire UId="w-neg-out"><NameCon UId="p-neg" Name="out" /><IdentCon UId="a-neg" /></Wire>
              </Wires>
            </FlgNet>
            """);

        Assert.Contains("confidence: \"exact\"", yaml);
        Assert.Contains("- \"IF EnableMath THEN Sum := (Left + Right); END_IF;\"", yaml);
        Assert.Contains("- \"Delta := (Left - Right);\"", yaml);
        Assert.Contains("- \"Product := (Left * Right);\"", yaml);
        Assert.Contains("- \"Ratio := (Left / Right);\"", yaml);
        Assert.Contains("- \"Remainder := (Left MOD Right);\"", yaml);
        Assert.Contains("- \"NegativeLeft := (-Left);\"", yaml);
    }

    [Fact]
    public void TranslatesNegativeEdgeContactsAndBoxes()
    {
        var yaml = ExportSingleLadBlockYaml(
            "EdgeLogic",
            "cu-edges",
            """
            <FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
              <Parts>
                <Access Scope="LocalVariable" UId="a-enable"><Symbol><Component Name="EnableEdge" /></Symbol></Access>
                <Access Scope="LocalVariable" UId="a-signal"><Symbol><Component Name="Signal" /></Symbol></Access>
                <Access Scope="LocalVariable" UId="a-bit"><Symbol><Component Name="SignalMemory" /></Symbol></Access>
                <Access Scope="LocalVariable" UId="a-falling"><Symbol><Component Name="FallingSeen" /></Symbol></Access>
                <Access Scope="LocalVariable" UId="a-positive"><Symbol><Component Name="PositiveBoxSeen" /></Symbol></Access>
                <Access Scope="LocalVariable" UId="a-negative"><Symbol><Component Name="NegativeBoxSeen" /></Symbol></Access>
                <Part Name="Contact" UId="p-enable" />
                <Part Name="NContact" UId="p-ncontact" />
                <Part Name="PBox" UId="p-pbox" />
                <Part Name="NBox" UId="p-nbox" />
                <Part Name="Coil" UId="p-falling" />
                <Part Name="Coil" UId="p-positive" />
                <Part Name="Coil" UId="p-negative" />
              </Parts>
              <Wires>
                <Wire UId="w-enable-in"><Powerrail /><NameCon UId="p-enable" Name="in" /></Wire>
                <Wire UId="w-enable-op"><IdentCon UId="a-enable" /><NameCon UId="p-enable" Name="operand" /></Wire>
                <Wire UId="w-pre"><NameCon UId="p-enable" Name="out" /><NameCon UId="p-ncontact" Name="pre" /></Wire>
                <Wire UId="w-ncontact-signal"><IdentCon UId="a-signal" /><NameCon UId="p-ncontact" Name="operand" /></Wire>
                <Wire UId="w-ncontact-bit"><IdentCon UId="a-bit" /><NameCon UId="p-ncontact" Name="bit" /></Wire>
                <Wire UId="w-ncontact-out"><NameCon UId="p-ncontact" Name="out" /><NameCon UId="p-falling" Name="in" /></Wire>
                <Wire UId="w-falling"><IdentCon UId="a-falling" /><NameCon UId="p-falling" Name="operand" /></Wire>
                <Wire UId="w-pbox-in"><IdentCon UId="a-signal" /><NameCon UId="p-pbox" Name="in" /></Wire>
                <Wire UId="w-pbox-bit"><IdentCon UId="a-bit" /><NameCon UId="p-pbox" Name="bit" /></Wire>
                <Wire UId="w-pbox-out"><NameCon UId="p-pbox" Name="out" /><NameCon UId="p-positive" Name="in" /></Wire>
                <Wire UId="w-positive"><IdentCon UId="a-positive" /><NameCon UId="p-positive" Name="operand" /></Wire>
                <Wire UId="w-nbox-in"><IdentCon UId="a-signal" /><NameCon UId="p-nbox" Name="in" /></Wire>
                <Wire UId="w-nbox-bit"><IdentCon UId="a-bit" /><NameCon UId="p-nbox" Name="bit" /></Wire>
                <Wire UId="w-nbox-out"><NameCon UId="p-nbox" Name="out" /><NameCon UId="p-negative" Name="in" /></Wire>
                <Wire UId="w-negative"><IdentCon UId="a-negative" /><NameCon UId="p-negative" Name="operand" /></Wire>
              </Wires>
            </FlgNet>
            """);

        Assert.Contains("confidence: \"exact\"", yaml);
        Assert.Contains("- \"FallingSeen := (EnableEdge AND NPULSE(Signal, SignalMemory));\"", yaml);
        Assert.Contains("- \"PositiveBoxSeen := PULSE(Signal, SignalMemory);\"", yaml);
        Assert.Contains("- \"NegativeBoxSeen := NPULSE(Signal, SignalMemory);\"", yaml);
    }

    [Fact]
    public void TranslatesTimerCounterAndTriggerParts()
    {
        var yaml = ExportSingleLadBlockYaml(
            "TimerCounterLogic",
            "cu-fb-parts",
            """
            <FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
              <Parts>
                <Access Scope="LocalVariable" UId="a-start"><Symbol><Component Name="Start" /></Symbol></Access>
                <Access Scope="TypedConstant" UId="a-time"><Constant><ConstantValue>T#5s</ConstantValue></Constant></Access>
                <Access Scope="LocalVariable" UId="a-done"><Symbol><Component Name="Done" /></Symbol></Access>
                <Access Scope="LocalVariable" UId="a-edge"><Symbol><Component Name="EdgeSeen" /></Symbol></Access>
                <Access Scope="LocalVariable" UId="a-count"><Symbol><Component Name="CounterValue" /></Symbol></Access>
                <Part Name="Contact" UId="p-start" />
                <Part Name="TOF" UId="p-tof"><Instance Scope="GlobalVariable"><Component Name="OffDelay" /></Instance></Part>
                <Part Name="R_TRIG" UId="p-trigger"><Instance Scope="GlobalVariable"><Component Name="RiseEdge" /></Instance></Part>
                <Part Name="CTU" UId="p-counter"><Instance Scope="GlobalVariable"><Component Name="Counter" /></Instance></Part>
                <Part Name="Coil" UId="p-done" />
                <Part Name="Coil" UId="p-edge" />
              </Parts>
              <Wires>
                <Wire UId="w-start-in"><Powerrail /><NameCon UId="p-start" Name="in" /></Wire>
                <Wire UId="w-start-op"><IdentCon UId="a-start" /><NameCon UId="p-start" Name="operand" /></Wire>
                <Wire UId="w-tof-in"><NameCon UId="p-start" Name="out" /><NameCon UId="p-tof" Name="IN" /></Wire>
                <Wire UId="w-tof-pt"><IdentCon UId="a-time" /><NameCon UId="p-tof" Name="PT" /></Wire>
                <Wire UId="w-tof-q"><NameCon UId="p-tof" Name="Q" /><NameCon UId="p-done" Name="in" /></Wire>
                <Wire UId="w-done"><IdentCon UId="a-done" /><NameCon UId="p-done" Name="operand" /></Wire>
                <Wire UId="w-trigger-clk"><NameCon UId="p-start" Name="out" /><NameCon UId="p-trigger" Name="CLK" /></Wire>
                <Wire UId="w-trigger-q"><NameCon UId="p-trigger" Name="Q" /><NameCon UId="p-edge" Name="in" /></Wire>
                <Wire UId="w-edge"><IdentCon UId="a-edge" /><NameCon UId="p-edge" Name="operand" /></Wire>
                <Wire UId="w-counter-cu"><NameCon UId="p-trigger" Name="Q" /><NameCon UId="p-counter" Name="CU" /></Wire>
                <Wire UId="w-counter-cv"><NameCon UId="p-counter" Name="CV" /><IdentCon UId="a-count" /></Wire>
              </Wires>
            </FlgNet>
            """);

        Assert.Contains("confidence: \"exact\"", yaml);
        Assert.Contains("- \"OffDelay(IN := Start, PT := T#5s);\"", yaml);
        Assert.Contains("- \"RiseEdge(CLK := Start);\"", yaml);
        Assert.Contains("- \"Counter(CU := RiseEdge.Q);\"", yaml);
        Assert.Contains("- \"Done := OffDelay.Q;\"", yaml);
        Assert.Contains("- \"EdgeSeen := RiseEdge.Q;\"", yaml);
        Assert.Contains("- \"CounterValue := Counter.CV;\"", yaml);
    }

    [Fact]
    public void TranslatesGenericFunctionPartsAsExpressions()
    {
        var yaml = ExportSingleLadBlockYaml(
            "FunctionLogic",
            "cu-functions",
            """
            <FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
              <Parts>
                <Access Scope="LocalVariable" UId="a-value"><Symbol><Component Name="Value" /></Symbol></Access>
                <Access Scope="LiteralConstant" UId="a-min"><Constant><ConstantValue>0</ConstantValue></Constant></Access>
                <Access Scope="LiteralConstant" UId="a-max"><Constant><ConstantValue>100</ConstantValue></Constant></Access>
                <Access Scope="LocalVariable" UId="a-limited"><Symbol><Component Name="Limited" /></Symbol></Access>
                <Access Scope="LocalVariable" UId="a-converted"><Symbol><Component Name="Converted" /></Symbol></Access>
                <Access Scope="LocalVariable" UId="a-length"><Symbol><Component Name="Length" /></Symbol></Access>
                <Part Name="LIMIT" UId="p-limit" />
                <Part Name="Convert" UId="p-convert" />
                <Part Name="LEN" UId="p-len" />
              </Parts>
              <Wires>
                <Wire UId="w-limit-mn"><IdentCon UId="a-min" /><NameCon UId="p-limit" Name="mn" /></Wire>
                <Wire UId="w-limit-in"><IdentCon UId="a-value" /><NameCon UId="p-limit" Name="in" /></Wire>
                <Wire UId="w-limit-mx"><IdentCon UId="a-max" /><NameCon UId="p-limit" Name="mx" /></Wire>
                <Wire UId="w-limit-out"><NameCon UId="p-limit" Name="out" /><IdentCon UId="a-limited" /></Wire>
                <Wire UId="w-convert-in"><IdentCon UId="a-value" /><NameCon UId="p-convert" Name="in" /></Wire>
                <Wire UId="w-convert-out"><NameCon UId="p-convert" Name="out" /><IdentCon UId="a-converted" /></Wire>
                <Wire UId="w-len-in"><IdentCon UId="a-value" /><NameCon UId="p-len" Name="in" /></Wire>
                <Wire UId="w-len-out"><NameCon UId="p-len" Name="out" /><IdentCon UId="a-length" /></Wire>
              </Wires>
            </FlgNet>
            """);

        Assert.Contains("confidence: \"exact\"", yaml);
        Assert.Contains("- \"Limited := LIMIT(mn := 0, in := Value, mx := 100);\"", yaml);
        Assert.Contains("- \"Converted := Convert(in := Value);\"", yaml);
        Assert.Contains("- \"Length := LEN(in := Value);\"", yaml);
    }

    [Fact]
    public void TranslatesRemainingMoveCoilAndRuntimeParts()
    {
        var yaml = ExportSingleLadBlockYaml(
            "RemainingPartLogic",
            "cu-remaining-parts",
            """
            <FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
              <Parts>
                <Access Scope="LocalVariable" UId="a-enable"><Symbol><Component Name="Enable" /></Symbol></Access>
                <Access Scope="LocalVariable" UId="a-source"><Symbol><Component Name="SourceValue" /></Symbol></Access>
                <Access Scope="LocalVariable" UId="a-target"><Symbol><Component Name="TargetValue" /></Symbol></Access>
                <Access Scope="LocalVariable" UId="a-chain"><Symbol><Component Name="ChainedCoil" /></Symbol></Access>
                <Access Scope="LocalVariable" UId="a-mem"><Symbol><Component Name="RuntimeMemory" /></Symbol></Access>
                <Access Scope="LocalVariable" UId="a-runtime"><Symbol><Component Name="RuntimeRetVal" /></Symbol></Access>
                <Access Scope="LiteralConstant" UId="a-mode"><Constant><ConstantValue>1</ConstantValue></Constant></Access>
                <Access Scope="LocalVariable" UId="a-ob"><Symbol><Component Name="CurrentOb" /></Symbol></Access>
                <Access Scope="LocalVariable" UId="a-info"><Symbol><Component Name="RuntimeInfo" /></Symbol></Access>
                <Access Scope="LocalVariable" UId="a-rt"><Symbol><Component Name="RuntimeInfoRetVal" /></Symbol></Access>
                <Access Scope="LocalVariable" UId="a-time"><Symbol><Component Name="SystemTime" /></Symbol></Access>
                <Access Scope="LocalVariable" UId="a-write"><Symbol><Component Name="WriteTimeRetVal" /></Symbol></Access>
                <Part Name="Contact" UId="p-enable" />
                <Part Name="S_Move" UId="p-smove" />
                <Part Name="Coil" UId="p-first-coil" />
                <Part Name="Coil" UId="p-chain-coil" />
                <Part Name="Runtime" UId="p-runtime" />
                <Part Name="RT_INFO" UId="p-rt-info" />
                <Part Name="WR_SYS_T" UId="p-wr-sys-t" />
              </Parts>
              <Wires>
                <Wire UId="w-enable-in"><Powerrail /><NameCon UId="p-enable" Name="in" /></Wire>
                <Wire UId="w-enable-op"><IdentCon UId="a-enable" /><NameCon UId="p-enable" Name="operand" /></Wire>
                <Wire UId="w-enable-out"><NameCon UId="p-enable" Name="out" /><NameCon UId="p-smove" Name="en" /><NameCon UId="p-first-coil" Name="in" /><NameCon UId="p-runtime" Name="en" /><NameCon UId="p-rt-info" Name="en" /><NameCon UId="p-wr-sys-t" Name="en" /></Wire>
                <Wire UId="w-smove-source"><IdentCon UId="a-source" /><NameCon UId="p-smove" Name="in" /></Wire>
                <Wire UId="w-smove-target"><NameCon UId="p-smove" Name="out" /><IdentCon UId="a-target" /></Wire>
                <Wire UId="w-first-coil-op"><IdentCon UId="a-enable" /><NameCon UId="p-first-coil" Name="operand" /></Wire>
                <Wire UId="w-first-coil-out"><NameCon UId="p-first-coil" Name="out" /><NameCon UId="p-chain-coil" Name="in" /></Wire>
                <Wire UId="w-chain-op"><IdentCon UId="a-chain" /><NameCon UId="p-chain-coil" Name="operand" /></Wire>
                <Wire UId="w-runtime-mem"><IdentCon UId="a-mem" /><NameCon UId="p-runtime" Name="mem" /></Wire>
                <Wire UId="w-runtime-out"><NameCon UId="p-runtime" Name="ret_val" /><IdentCon UId="a-runtime" /></Wire>
                <Wire UId="w-rt-mode"><IdentCon UId="a-mode" /><NameCon UId="p-rt-info" Name="MODE" /></Wire>
                <Wire UId="w-rt-ob"><IdentCon UId="a-ob" /><NameCon UId="p-rt-info" Name="OB" /></Wire>
                <Wire UId="w-rt-info"><IdentCon UId="a-info" /><NameCon UId="p-rt-info" Name="INFO" /></Wire>
                <Wire UId="w-rt-out"><NameCon UId="p-rt-info" Name="Ret_Val" /><IdentCon UId="a-rt" /></Wire>
                <Wire UId="w-time"><IdentCon UId="a-time" /><NameCon UId="p-wr-sys-t" Name="IN" /></Wire>
                <Wire UId="w-write"><NameCon UId="p-wr-sys-t" Name="RET_VAL" /><IdentCon UId="a-write" /></Wire>
              </Wires>
            </FlgNet>
            """);

        Assert.Contains("confidence: \"exact\"", yaml);
        Assert.Contains("- \"IF Enable THEN TargetValue := SourceValue; END_IF;\"", yaml);
        Assert.Contains("- \"Enable := Enable;\"", yaml);
        Assert.Contains("- \"ChainedCoil := Enable;\"", yaml);
        Assert.Contains("- \"IF Enable THEN RuntimeRetVal := Runtime(mem := RuntimeMemory); END_IF;\"", yaml);
        Assert.Contains("- \"IF Enable THEN RuntimeInfoRetVal := RT_INFO(MODE := 1, OB := CurrentOb, INFO := RuntimeInfo); END_IF;\"", yaml);
        Assert.Contains("- \"IF Enable THEN WriteTimeRetVal := WR_SYS_T(IN := SystemTime); END_IF;\"", yaml);
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
    public void EmitsEmptyLadNetworksAsEmptyNetworkTrace()
    {
        var root = Path.Combine(Path.GetTempPath(), "PlcSourceExporterTests", Guid.NewGuid().ToString("N"));
        var service = new PlcExportService(() => new DateTimeOffset(2026, 7, 7, 7, 8, 9, TimeSpan.Zero));
        var plc = FakePlcSoftware.Create(
            blocks:
            [
                new FakeExportableObject(
                    "Blocks/EmptyLad",
                    "OrganizationBlock",
                    exportContent: """
                        <?xml version="1.0" encoding="utf-8"?>
                        <Document>
                          <SW.Blocks.OB ID="0">
                            <AttributeList><Name>EmptyLad</Name><ProgrammingLanguage>LAD</ProgrammingLanguage></AttributeList>
                            <ObjectList>
                              <SW.Blocks.CompileUnit ID="cu-empty-lad">
                                <AttributeList>
                                  <NetworkSource />
                                  <ProgrammingLanguage>LAD</ProgrammingLanguage>
                                </AttributeList>
                              </SW.Blocks.CompileUnit>
                            </ObjectList>
                          </SW.Blocks.OB>
                        </Document>
                        """)
            ],
            types: [],
            tagTables: []);

        service.Export(plc, root);
        var yaml = File.ReadAllText(Path.Combine(root, "translate", "program-blocks.yaml"));

        Assert.Contains("name: \"EmptyLad\"", yaml);
        Assert.Contains("compileUnitId: \"cu-empty-lad\"", yaml);
        Assert.Contains("language: \"LAD\"", yaml);
        Assert.Contains("confidence: \"exact\"", yaml);
        Assert.Contains("statements:\r\n            []", yaml);
        Assert.Contains("notes:\r\n            []", yaml);
        Assert.DoesNotContain("- \"Empty network without any content.\"", yaml);
        Assert.DoesNotContain("- \"Unsupported network language or missing FlgNet source.\"", yaml);
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

    private static string ExportSingleLadBlockYaml(string blockName, string compileUnitId, string flgNet)
    {
        var root = Path.Combine(Path.GetTempPath(), "PlcSourceExporterTests", Guid.NewGuid().ToString("N"));
        var service = new PlcExportService(() => new DateTimeOffset(2026, 7, 11, 1, 2, 3, TimeSpan.Zero));
        var plc = FakePlcSoftware.Create(
            blocks:
            [
                new FakeExportableObject(
                    $"Blocks/{blockName}",
                    "FunctionBlock",
                    exportContent: $$"""
                        <?xml version="1.0" encoding="utf-8"?>
                        <Document>
                          <SW.Blocks.FB ID="0">
                            <AttributeList><Name>{{blockName}}</Name><ProgrammingLanguage>LAD</ProgrammingLanguage></AttributeList>
                            <ObjectList>
                              <SW.Blocks.CompileUnit ID="{{compileUnitId}}">
                                <AttributeList>
                                  <NetworkSource>{{flgNet}}</NetworkSource>
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
        return File.ReadAllText(Path.Combine(root, "translate", "program-blocks.yaml"));
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
