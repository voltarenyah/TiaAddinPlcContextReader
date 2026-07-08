using System.Text.Json;
using PlcSourceExporter.Core;

namespace PlcSourceExporter.Core.Tests;

public sealed class BlockProfilesAndHintsTests
{
    [Fact]
    public void WritesBlockProfilesWithInterfaceStateAndKeyReferences()
    {
        var root = CreateExportRoot();
        WriteMetadata(
            root,
            """
            [
              { "name": "SequenceFb", "sourcePath": "Blocks/SequenceFb", "category": "FB", "status": "Exported", "exportedFile": "Blocks\\SequenceFb.xml" }
            ]
            """);
        File.WriteAllText(Path.Combine(root, "Blocks", "SequenceFb.xml"), SequenceFbXml);

        ProgramSemanticReferenceBuilder.Write(root, new DateTimeOffset(2026, 6, 26, 1, 2, 3, TimeSpan.Zero));
        var result = ProgramBlockProfileBuilder.Write(root, new DateTimeOffset(2026, 6, 26, 1, 2, 3, TimeSpan.Zero));

        Assert.True(File.Exists(result.BlockProfilesFilePath));
        Assert.True(File.Exists(result.OptimizationHintsFilePath));
        Assert.Equal(1, result.BlockProfileCount);

        var profile = ReadJsonLines(result.BlockProfilesFilePath).Single();
        Assert.Equal("SequenceFb", profile.GetProperty("block").GetString());
        Assert.Equal("FB", profile.GetProperty("blockKind").GetString());
        Assert.Equal("LAD", profile.GetProperty("language").GetString());
        Assert.Equal("Blocks\\SequenceFb.xml", profile.GetProperty("sourceFile").GetString());
        Assert.Equal(1, profile.GetProperty("networkCount").GetInt32());
        Assert.Equal(2, profile.GetProperty("callCount").GetInt32());

        var interfaceSummary = profile.GetProperty("interfaceSummary");
        Assert.Equal(1, interfaceSummary.GetProperty("inputCount").GetInt32());
        Assert.Equal(1, interfaceSummary.GetProperty("outputCount").GetInt32());
        Assert.Equal(1, interfaceSummary.GetProperty("staticCount").GetInt32());

        Assert.Contains(profile.GetProperty("keyReads").EnumerateArray().Select(item => item.GetString()), value => value == "Start");
        Assert.Contains(profile.GetProperty("keyWrites").EnumerateArray().Select(item => item.GetString()), value => value == "Done");
        Assert.Contains(profile.GetProperty("keyCalls").EnumerateArray().Select(item => item.GetString()), value => value == "HelperFc");
        Assert.Contains(profile.GetProperty("keyCalls").EnumerateArray().Select(item => item.GetString()), value => value == "TON");
        Assert.Contains(profile.GetProperty("statefulElements").EnumerateArray().Select(item => item.GetString()), value => value == "timer:TON");
        Assert.Contains(profile.GetProperty("instanceDbs").EnumerateArray().Select(item => item.GetString()), value => value == "Timer_1");
    }

    [Fact]
    public void DetectsMultiWriterAndNeverReadSymbols()
    {
        var root = CreateExportRoot();
        WriteMetadata(
            root,
            """
            [
              { "name": "Writer1", "sourcePath": "Blocks/Writer1", "category": "FC", "status": "Exported", "exportedFile": "Blocks\\Writer1.xml" },
              { "name": "Writer2", "sourcePath": "Blocks/Writer2", "category": "FC", "status": "Exported", "exportedFile": "Blocks\\Writer2.xml" }
            ]
            """);
        File.WriteAllText(Path.Combine(root, "Blocks", "Writer1.xml"), BuildWriteOnlyBlockXml("Writer1", "AssignSharedCommand"));
        File.WriteAllText(Path.Combine(root, "Blocks", "Writer2.xml"), BuildWriteOnlyBlockXml("Writer2", "AssignSharedCommand"));

        ProgramSemanticReferenceBuilder.Write(root, new DateTimeOffset(2026, 6, 26, 2, 3, 4, TimeSpan.Zero));
        var result = ProgramBlockProfileBuilder.Write(root, new DateTimeOffset(2026, 6, 26, 2, 3, 4, TimeSpan.Zero));

        var hints = ReadJsonLines(result.OptimizationHintsFilePath).ToArray();
        Assert.Contains(hints, hint =>
            hint.GetProperty("kind").GetString() == "multi_writer" &&
            hint.GetProperty("target").GetString() == "SharedDb.Command");
        Assert.Contains(hints, hint =>
            hint.GetProperty("kind").GetString() == "never_read_symbol" &&
            hint.GetProperty("target").GetString() == "SharedDb.Command");
    }

    [Fact]
    public void DetectsRepeatedCallsAndScanOrderDependencies()
    {
        var root = CreateExportRoot();
        WriteMetadata(
            root,
            """
            [
              { "name": "Main", "sourcePath": "Blocks/Main", "category": "OB", "status": "Exported", "exportedFile": "Blocks\\Main.xml" }
            ]
            """);
        File.WriteAllText(Path.Combine(root, "Blocks", "Main.xml"), MainWithRepeatedCallsXml);

        ProgramSemanticReferenceBuilder.Write(root, new DateTimeOffset(2026, 6, 26, 3, 4, 5, TimeSpan.Zero));
        var result = ProgramBlockProfileBuilder.Write(root, new DateTimeOffset(2026, 6, 26, 3, 4, 5, TimeSpan.Zero));

        var hints = ReadJsonLines(result.OptimizationHintsFilePath).ToArray();
        Assert.Contains(hints, hint =>
            hint.GetProperty("kind").GetString() == "repeated_call_target" &&
            hint.GetProperty("block").GetString() == "Main" &&
            hint.GetProperty("target").GetString() == "FC_Check");
        Assert.Contains(hints, hint =>
            hint.GetProperty("kind").GetString() == "scan_order_dependency" &&
            hint.GetProperty("block").GetString() == "Main" &&
            hint.GetProperty("target").GetString() == "CellDb.StepLatched");
    }

    [Fact]
    public void DetectsNeverWrittenOutputsFromBlockInterface()
    {
        var root = CreateExportRoot();
        WriteMetadata(
            root,
            """
            [
              { "name": "StatusFb", "sourcePath": "Blocks/StatusFb", "category": "FB", "status": "Exported", "exportedFile": "Blocks\\StatusFb.xml" }
            ]
            """);
        File.WriteAllText(Path.Combine(root, "Blocks", "StatusFb.xml"), StatusFbXml);

        ProgramSemanticReferenceBuilder.Write(root, new DateTimeOffset(2026, 6, 26, 4, 5, 6, TimeSpan.Zero));
        var result = ProgramBlockProfileBuilder.Write(root, new DateTimeOffset(2026, 6, 26, 4, 5, 6, TimeSpan.Zero));

        Assert.Contains(
            ReadJsonLines(result.OptimizationHintsFilePath),
            hint => hint.GetProperty("kind").GetString() == "never_written_output" &&
                hint.GetProperty("block").GetString() == "StatusFb" &&
                hint.GetProperty("target").GetString() == "Done");
    }

    [Fact]
    public void DoesNotFlagWrittenFcOutputMembersAsNeverReadWhenConsumedByCallerBinding()
    {
        var root = CreateExportRoot();
        WriteMetadata(
            root,
            """
            [
              { "name": "Main", "sourcePath": "Blocks/Main", "category": "OB", "status": "Exported", "exportedFile": "Blocks\\Main.xml" },
              { "name": "OutputWriter", "sourcePath": "Blocks/OutputWriter", "category": "FC", "status": "Exported", "exportedFile": "Blocks\\OutputWriter.xml" }
            ]
            """);
        File.WriteAllText(Path.Combine(root, "Blocks", "Main.xml"), MainCallingOutputWriterXml);
        File.WriteAllText(Path.Combine(root, "Blocks", "OutputWriter.xml"), OutputWriterXml);

        ProgramSemanticReferenceBuilder.Write(root, new DateTimeOffset(2026, 6, 26, 5, 6, 7, TimeSpan.Zero));
        var result = ProgramBlockProfileBuilder.Write(root, new DateTimeOffset(2026, 6, 26, 5, 6, 7, TimeSpan.Zero));

        Assert.DoesNotContain(
            ReadJsonLines(result.OptimizationHintsFilePath),
            hint => hint.GetProperty("kind").GetString() == "never_read_symbol" &&
                hint.GetProperty("block").GetString() == "OutputWriter" &&
                hint.GetProperty("target").GetString() == "OutputValue");
    }

    private static string CreateExportRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "PlcSourceExporterTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "Blocks"));
        return root;
    }

    private static void WriteMetadata(string root, string componentsJson)
    {
        File.WriteAllText(
            Path.Combine(root, "metadata.json"),
            $$"""
            {
              "schemaVersion": "1.0",
              "components": {{componentsJson}}
            }
            """);
    }

    private static IEnumerable<JsonElement> ReadJsonLines(string path)
    {
        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            using var document = JsonDocument.Parse(line);
            yield return document.RootElement.Clone();
        }
    }

    private static string BuildWriteOnlyBlockXml(string blockName, string calleeName)
    {
        return $$"""
            <?xml version="1.0" encoding="utf-8"?>
            <Document>
              <SW.Blocks.FC ID="0">
                <AttributeList><Name>{{blockName}}</Name><ProgrammingLanguage>LAD</ProgrammingLanguage></AttributeList>
                <ObjectList>
                  <SW.Blocks.CompileUnit ID="1">
                    <AttributeList>
                      <NetworkSource><FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
                        <Parts>
                          <Access Scope="GlobalVariable" UId="1"><Symbol><Component Name="SharedDb" /><Component Name="Command" /></Symbol></Access>
                          <Call UId="2">
                            <CallInfo Name="{{calleeName}}" BlockType="FC">
                              <Parameter Name="OutValue" Section="Output" Type="Bool" />
                            </CallInfo>
                          </Call>
                        </Parts>
                        <Wires>
                          <Wire UId="3"><NameCon UId="2" Name="OutValue" /><IdentCon UId="1" /></Wire>
                        </Wires>
                      </FlgNet></NetworkSource>
                      <ProgrammingLanguage>LAD</ProgrammingLanguage>
                    </AttributeList>
                  </SW.Blocks.CompileUnit>
                </ObjectList>
              </SW.Blocks.FC>
            </Document>
            """;
    }

    private const string SequenceFbXml = """
        <?xml version="1.0" encoding="utf-8"?>
        <Document>
          <SW.Blocks.FB ID="0">
            <AttributeList>
              <Name>SequenceFb</Name>
              <ProgrammingLanguage>LAD</ProgrammingLanguage>
              <Interface>
                <Sections xmlns="http://www.siemens.com/automation/Openness/SW/Interface/v5">
                  <Section Name="Input">
                    <Member Name="Start" Datatype="Bool" />
                  </Section>
                  <Section Name="Output">
                    <Member Name="Done" Datatype="Bool" />
                  </Section>
                  <Section Name="Static">
                    <Member Name="StepIndex" Datatype="Int" />
                  </Section>
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
                      <Access Scope="LocalVariable" UId="3"><Symbol><Component Name="StepIndex" /></Symbol></Access>
                      <Call UId="10">
                        <CallInfo Name="TON" BlockType="FB">
                          <Instance Scope="LocalVariable" UId="11">
                            <Component Name="Timer_1" />
                          </Instance>
                          <Parameter Name="IN" Section="Input" Type="Bool" />
                          <Parameter Name="Q" Section="Output" Type="Bool" />
                        </CallInfo>
                      </Call>
                      <Call UId="12">
                        <CallInfo Name="HelperFc" BlockType="FC">
                          <Parameter Name="Enable" Section="Input" Type="Int" />
                        </CallInfo>
                      </Call>
                    </Parts>
                    <Wires>
                      <Wire UId="20"><IdentCon UId="1" /><NameCon UId="10" Name="IN" /></Wire>
                      <Wire UId="21"><NameCon UId="10" Name="Q" /><IdentCon UId="2" /></Wire>
                      <Wire UId="22"><IdentCon UId="3" /><NameCon UId="12" Name="Enable" /></Wire>
                    </Wires>
                  </FlgNet></NetworkSource>
                  <ProgrammingLanguage>LAD</ProgrammingLanguage>
                </AttributeList>
              </SW.Blocks.CompileUnit>
            </ObjectList>
          </SW.Blocks.FB>
        </Document>
        """;

    private const string MainWithRepeatedCallsXml = """
        <?xml version="1.0" encoding="utf-8"?>
        <Document>
          <SW.Blocks.OB ID="0">
            <AttributeList><Name>Main</Name><ProgrammingLanguage>LAD</ProgrammingLanguage></AttributeList>
            <ObjectList>
              <SW.Blocks.CompileUnit ID="1">
                <AttributeList>
                  <NetworkSource><FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
                    <Parts>
                      <Access Scope="GlobalVariable" UId="1"><Symbol><Component Name="CellDb" /><Component Name="StepLatched" /></Symbol></Access>
                      <Call UId="2">
                        <CallInfo Name="AssignLatch" BlockType="FC">
                          <Parameter Name="OutValue" Section="Output" Type="Bool" />
                        </CallInfo>
                      </Call>
                      <Call UId="3">
                        <CallInfo Name="FC_Check" BlockType="FC">
                          <Parameter Name="Enable" Section="Input" Type="Bool" />
                        </CallInfo>
                      </Call>
                    </Parts>
                    <Wires>
                      <Wire UId="10"><NameCon UId="2" Name="OutValue" /><IdentCon UId="1" /></Wire>
                      <Wire UId="11"><IdentCon UId="1" /><NameCon UId="3" Name="Enable" /></Wire>
                    </Wires>
                  </FlgNet></NetworkSource>
                  <ProgrammingLanguage>LAD</ProgrammingLanguage>
                </AttributeList>
              </SW.Blocks.CompileUnit>
              <SW.Blocks.CompileUnit ID="2">
                <AttributeList>
                  <NetworkSource><FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
                    <Parts>
                      <Access Scope="GlobalVariable" UId="20"><Symbol><Component Name="CellDb" /><Component Name="StepLatched" /></Symbol></Access>
                      <Call UId="21">
                        <CallInfo Name="FC_Check" BlockType="FC">
                          <Parameter Name="Enable" Section="Input" Type="Bool" />
                        </CallInfo>
                      </Call>
                    </Parts>
                    <Wires>
                      <Wire UId="30"><IdentCon UId="20" /><NameCon UId="21" Name="Enable" /></Wire>
                    </Wires>
                  </FlgNet></NetworkSource>
                  <ProgrammingLanguage>LAD</ProgrammingLanguage>
                </AttributeList>
              </SW.Blocks.CompileUnit>
            </ObjectList>
          </SW.Blocks.OB>
        </Document>
        """;

    private const string StatusFbXml = """
        <?xml version="1.0" encoding="utf-8"?>
        <Document>
          <SW.Blocks.FB ID="0">
            <AttributeList>
              <Name>StatusFb</Name>
              <ProgrammingLanguage>LAD</ProgrammingLanguage>
              <Interface>
                <Sections xmlns="http://www.siemens.com/automation/Openness/SW/Interface/v5">
                  <Section Name="Input">
                    <Member Name="Enable" Datatype="Bool" />
                  </Section>
                  <Section Name="Output">
                    <Member Name="Done" Datatype="Bool" />
                  </Section>
                </Sections>
              </Interface>
            </AttributeList>
            <ObjectList>
              <SW.Blocks.CompileUnit ID="1">
                <AttributeList>
                  <NetworkSource><FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
                    <Parts>
                      <Access Scope="LocalVariable" UId="1"><Symbol><Component Name="Enable" /></Symbol></Access>
                    </Parts>
                  </FlgNet></NetworkSource>
                  <ProgrammingLanguage>LAD</ProgrammingLanguage>
                </AttributeList>
              </SW.Blocks.CompileUnit>
            </ObjectList>
          </SW.Blocks.FB>
        </Document>
        """;

    private const string MainCallingOutputWriterXml = """
        <?xml version="1.0" encoding="utf-8"?>
        <Document>
          <SW.Blocks.OB ID="0">
            <AttributeList><Name>Main</Name><ProgrammingLanguage>LAD</ProgrammingLanguage></AttributeList>
            <ObjectList>
              <SW.Blocks.CompileUnit ID="1">
                <AttributeList>
                  <NetworkSource><FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
                    <Parts>
                      <Access Scope="GlobalVariable" UId="1"><Symbol><Component Name="SharedDb" /><Component Name="Command" /></Symbol></Access>
                      <Call UId="2">
                        <CallInfo Name="OutputWriter" BlockType="FC">
                          <Parameter Name="OutputValue" Section="Output" Type="Bool" />
                        </CallInfo>
                      </Call>
                    </Parts>
                    <Wires>
                      <Wire UId="3"><NameCon UId="2" Name="OutputValue" /><IdentCon UId="1" /></Wire>
                    </Wires>
                  </FlgNet></NetworkSource>
                  <ProgrammingLanguage>LAD</ProgrammingLanguage>
                </AttributeList>
              </SW.Blocks.CompileUnit>
            </ObjectList>
          </SW.Blocks.OB>
        </Document>
        """;

    private const string OutputWriterXml = """
        <?xml version="1.0" encoding="utf-8"?>
        <Document>
          <SW.Blocks.FC ID="0">
            <AttributeList>
              <Name>OutputWriter</Name>
              <ProgrammingLanguage>LAD</ProgrammingLanguage>
              <Interface>
                <Sections xmlns="http://www.siemens.com/automation/Openness/SW/Interface/v5">
                  <Section Name="Output">
                    <Member Name="OutputValue" Datatype="Bool" />
                  </Section>
                </Sections>
              </Interface>
            </AttributeList>
            <ObjectList>
              <SW.Blocks.CompileUnit ID="1">
                <AttributeList>
                  <NetworkSource><FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
                    <Parts>
                      <Access Scope="LocalVariable" UId="1"><Symbol><Component Name="OutputValue" /></Symbol></Access>
                      <Part Name="SCoil" UId="2" />
                    </Parts>
                    <Wires>
                      <Wire UId="3"><Powerrail /><NameCon UId="2" Name="in" /></Wire>
                      <Wire UId="4"><IdentCon UId="1" /><NameCon UId="2" Name="operand" /></Wire>
                    </Wires>
                  </FlgNet></NetworkSource>
                  <ProgrammingLanguage>LAD</ProgrammingLanguage>
                </AttributeList>
              </SW.Blocks.CompileUnit>
            </ObjectList>
          </SW.Blocks.FC>
        </Document>
        """;
}
