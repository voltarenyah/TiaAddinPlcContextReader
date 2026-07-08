using PlcSourceExporter.Core;

namespace PlcSourceExporter.Core.Tests;

public sealed class ProgramBlockCallGraphTests
{
    [Fact]
    public void ParsesCallInfoWithInstanceDbAndNetworkTitle()
    {
        var xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <Document>
              <SW.Blocks.OB ID="0">
                <ObjectList>
                  <SW.Blocks.CompileUnit ID="4" CompositionName="CompileUnits">
                    <AttributeList>
                      <NetworkSource><FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
                        <Parts>
                          <Call UId="21">
                            <CallInfo Name="MachineGeneralCall" BlockType="FB">
                              <Instance Scope="GlobalVariable" UId="22">
                                <Component Name="MachineGeneralCall_DB" />
                              </Instance>
                              <Parameter Name="I_Start" Section="Input" Type="Bool" />
                            </CallInfo>
                          </Call>
                        </Parts>
                      </FlgNet></NetworkSource>
                    </AttributeList>
                    <ObjectList>
                      <MultilingualText ID="5" CompositionName="Title">
                        <ObjectList>
                          <MultilingualTextItem ID="6" CompositionName="Items">
                            <AttributeList>
                              <Culture>en-US</Culture>
                              <Text>Machine general call</Text>
                            </AttributeList>
                          </MultilingualTextItem>
                        </ObjectList>
                      </MultilingualText>
                    </ObjectList>
                  </SW.Blocks.CompileUnit>
                </ObjectList>
              </SW.Blocks.OB>
            </Document>
            """;

        var calls = ProgramBlockCallGraphBuilder.ParseCalls(
            xml,
            new ProgramBlockComponent("Main", "OB", "Blocks/Main", "Blocks\\Main.xml"));

        var call = Assert.Single(calls);
        Assert.Equal("Main", call.CallerName);
        Assert.Equal("OB", call.CallerCategory);
        Assert.Equal("MachineGeneralCall", call.CalleeName);
        Assert.Equal("FB", call.CalleeBlockType);
        Assert.Equal("MachineGeneralCall_DB", call.InstanceDb);
        Assert.Equal("Machine general call", call.NetworkTitle);
        Assert.Equal("4", call.CompileUnitId);
        Assert.Equal(1, call.ParameterCount);
    }

    [Fact]
    public void ParsesSclAccessCallInfoWithInstanceComponentName()
    {
        var xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <Document>
              <SW.Blocks.FB ID="0">
                <ObjectList>
                  <SW.Blocks.CompileUnit ID="10" CompositionName="CompileUnits">
                    <AttributeList>
                      <NetworkSource><StructuredText xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/StructuredText/v4">
                        <Access Scope="GlobalVariable" UId="50">
                          <CallInfo UId="57" BlockType="FC">
                            <Instance Scope="GlobalVariable" UId="59">
                              <Component Name="FC_ArrayUintHandling" UId="58" />
                            </Instance>
                            <Parameter Name="I_InNumber" Section="Input" UId="61" />
                          </CallInfo>
                        </Access>
                      </StructuredText></NetworkSource>
                    </AttributeList>
                  </SW.Blocks.CompileUnit>
                </ObjectList>
              </SW.Blocks.FB>
            </Document>
            """;

        var calls = ProgramBlockCallGraphBuilder.ParseCalls(
            xml,
            new ProgramBlockComponent("Cell_Sequence", "FB", "Blocks/Cell_Sequence", "Blocks\\Cell_Sequence.xml"));

        var call = Assert.Single(calls);
        Assert.Equal("FC_ArrayUintHandling", call.CalleeName);
        Assert.Equal("FC", call.CalleeBlockType);
        Assert.Equal("10", call.CompileUnitId);
        Assert.Equal(1, call.ParameterCount);
    }

    [Fact]
    public void WritesCallingStructureJsonAndMarkdownFromExportFolder()
    {
        var root = Path.Combine(Path.GetTempPath(), "PlcSourceExporterTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "Blocks"));
        File.WriteAllText(
            Path.Combine(root, "metadata.json"),
            """
            {
              "schemaVersion": "1.0",
              "components": [
                { "name": "Main", "sourcePath": "Blocks/Main", "category": "OB", "status": "Exported", "exportedFile": "Blocks\\Main.xml" },
                { "name": "MachineGeneralCall", "sourcePath": "Blocks/MachineGeneralCall", "category": "FB", "status": "Exported", "exportedFile": "Blocks\\MachineGeneralCall.xml" },
                { "name": "Cabinet Input Mapping", "sourcePath": "Blocks/Cabinet Input Mapping", "category": "FC", "status": "Exported", "exportedFile": "Blocks\\Cabinet Input Mapping.xml" }
              ]
            }
            """);
        File.WriteAllText(
            Path.Combine(root, "Blocks", "Main.xml"),
            BlockXml(
                "SW.Blocks.OB",
                ("Cabinet Input Mapping", "FC", "", "Physical input mapping"),
                ("MachineGeneralCall", "FB", "MachineGeneralCall_DB", "Machine general call")));
        File.WriteAllText(
            Path.Combine(root, "Blocks", "MachineGeneralCall.xml"),
            BlockXml("SW.Blocks.FB", ("Cabinet Input Mapping", "FC", "", "Nested mapping")));
        File.WriteAllText(Path.Combine(root, "Blocks", "Cabinet Input Mapping.xml"), BlockXml("SW.Blocks.FC"));

        var result = ProgramBlockCallGraphBuilder.Write(root, new DateTimeOffset(2026, 6, 21, 9, 10, 11, TimeSpan.Zero));
        var markdown = File.ReadAllText(result.MarkdownFilePath);
        var json = File.ReadAllText(result.JsonFilePath);

        Assert.Contains("Program Block Calling Structure", markdown);
        Assert.Contains("- OB Main", markdown);
        Assert.Contains("- FC Cabinet Input Mapping [network: Physical input mapping]", markdown);
        Assert.Contains("- FB MachineGeneralCall [instance DB: MachineGeneralCall_DB] [network: Machine general call]", markdown);
        Assert.Contains("  - FC Cabinet Input Mapping [network: Nested mapping]", markdown);
        Assert.Contains("\"edgeCount\": 3", json);
        Assert.Contains("\"instanceDb\": \"MachineGeneralCall_DB\"", json);
    }

    private static string BlockXml(string rootElement, params (string Name, string Type, string InstanceDb, string NetworkTitle)[] calls)
    {
        var compileUnits = string.Join(
            Environment.NewLine,
            calls.Select((call, index) =>
            {
                var instance = string.IsNullOrEmpty(call.InstanceDb)
                    ? string.Empty
                    : $"""
                                  <Instance Scope="GlobalVariable" UId="22">
                                    <Component Name="{call.InstanceDb}" />
                                  </Instance>
                      """;

                return $"""
                      <SW.Blocks.CompileUnit ID="{index + 1}" CompositionName="CompileUnits">
                        <AttributeList>
                          <NetworkSource><FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
                            <Parts>
                              <Call UId="21">
                                <CallInfo Name="{call.Name}" BlockType="{call.Type}">
                {instance}
                                </CallInfo>
                              </Call>
                            </Parts>
                          </FlgNet></NetworkSource>
                        </AttributeList>
                        <ObjectList>
                          <MultilingualText ID="T{index}" CompositionName="Title">
                            <ObjectList>
                              <MultilingualTextItem ID="I{index}" CompositionName="Items">
                                <AttributeList><Culture>en-US</Culture><Text>{call.NetworkTitle}</Text></AttributeList>
                              </MultilingualTextItem>
                            </ObjectList>
                          </MultilingualText>
                        </ObjectList>
                      </SW.Blocks.CompileUnit>
                """;
            }));

        return $"""
            <?xml version="1.0" encoding="utf-8"?>
            <Document>
              <{rootElement} ID="0">
                <ObjectList>
            {compileUnits}
                </ObjectList>
              </{rootElement}>
            </Document>
            """;
    }
}
