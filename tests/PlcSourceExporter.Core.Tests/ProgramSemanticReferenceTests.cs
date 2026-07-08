using PlcSourceExporter.Core;

namespace PlcSourceExporter.Core.Tests;

public sealed class ProgramSemanticReferenceTests
{
    [Fact]
    public void ParsesLadCallNetworkReferencesByParameterSection()
    {
        var xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <Document>
              <SW.Blocks.OB ID="0">
                <AttributeList>
                  <Name>Main</Name>
                  <ProgrammingLanguage>LAD</ProgrammingLanguage>
                </AttributeList>
                <ObjectList>
                  <SW.Blocks.CompileUnit ID="4" CompositionName="CompileUnits">
                    <AttributeList>
                      <NetworkSource><FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
                        <Parts>
                          <Access Scope="GlobalVariable" UId="25"><Symbol><Component Name="Cell_IO_Main" /><Component Name="Input_Status" /></Symbol></Access>
                          <Access Scope="GlobalVariable" UId="26"><Symbol><Component Name="Cell_IO_Main" /><Component Name="Output_Status" /></Symbol></Access>
                          <Access Scope="GlobalVariable" UId="27"><Symbol><Component Name="Cell_IO_Main" /><Component Name="Input_Simulate" /></Symbol></Access>
                          <Call UId="28">
                            <CallInfo Name="FC_IO_Mapping" BlockType="FC">
                              <Parameter Name="I_Status" Section="Input" Type="Bool" />
                              <Parameter Name="O_Status" Section="Output" Type="Bool" />
                              <Parameter Name="IO_Simulate" Section="InOut" Type="Bool" />
                            </CallInfo>
                          </Call>
                        </Parts>
                        <Wires>
                          <Wire UId="30"><IdentCon UId="25" /><NameCon UId="28" Name="I_Status" /></Wire>
                          <Wire UId="31"><NameCon UId="28" Name="O_Status" /><IdentCon UId="26" /></Wire>
                          <Wire UId="32"><IdentCon UId="27" /><NameCon UId="28" Name="IO_Simulate" /></Wire>
                        </Wires>
                      </FlgNet></NetworkSource>
                      <ProgrammingLanguage>LAD</ProgrammingLanguage>
                    </AttributeList>
                    <ObjectList>
                      <MultilingualText ID="T1" CompositionName="Title">
                        <ObjectList>
                          <MultilingualTextItem ID="I1" CompositionName="Items">
                            <AttributeList><Culture>en-US</Culture><Text>Physical input mapping</Text></AttributeList>
                          </MultilingualTextItem>
                        </ObjectList>
                      </MultilingualText>
                    </ObjectList>
                  </SW.Blocks.CompileUnit>
                </ObjectList>
              </SW.Blocks.OB>
            </Document>
            """;

        var result = ProgramSemanticReferenceBuilder.Parse(
            xml,
            new ProgramBlockComponent("Main", "OB", "Blocks/Main", "Blocks\\Main.xml"));

        var network = Assert.Single(result.Networks);
        Assert.Equal("network:Main:1", network.Id);
        Assert.Equal("Main", network.Block);
        Assert.Equal("OB", network.BlockKind);
        Assert.Equal("LAD", network.Language);
        Assert.Equal(1, network.NetworkIndex);
        Assert.Equal("4", network.CompileUnitId);
        Assert.Equal("Physical input mapping", network.Title);
        Assert.Equal(3, network.AccessCount);
        Assert.Equal(1, network.CallCount);
        Assert.Contains("Cell_IO_Main.Input_Status", network.Reads);
        Assert.Contains("Cell_IO_Main.Output_Status", network.Writes);
        Assert.Contains("FC_IO_Mapping", network.Calls);

        Assert.Contains(result.References, item =>
            item.TargetKind == "block" &&
            item.Access == "call" &&
            item.To == "FC_IO_Mapping");
        Assert.Contains(result.References, item =>
            item.TargetKind == "symbol" &&
            item.Access == "read" &&
            item.To == "Cell_IO_Main.Input_Status" &&
            item.Parameter == "I_Status");
        Assert.Contains(result.References, item =>
            item.TargetKind == "symbol" &&
            item.Access == "write" &&
            item.To == "Cell_IO_Main.Output_Status" &&
            item.Parameter == "O_Status");
        Assert.Contains(result.References, item =>
            item.TargetKind == "symbol" &&
            item.Access == "inout" &&
            item.To == "Cell_IO_Main.Input_Simulate" &&
            item.Parameter == "IO_Simulate");
    }

    [Fact]
    public void ParsesSclAccessCallInfoWithoutNameAttribute()
    {
        var xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <Document>
              <SW.Blocks.FB ID="0">
                <AttributeList>
                  <Name>Cell_Sequence</Name>
                  <ProgrammingLanguage>SCL</ProgrammingLanguage>
                </AttributeList>
                <ObjectList>
                  <SW.Blocks.CompileUnit ID="10" CompositionName="CompileUnits">
                    <AttributeList>
                      <NetworkSource><StructuredText xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/StructuredText/v4">
                        <Access Scope="GlobalVariable" UId="50">
                          <CallInfo UId="57" BlockType="FC">
                            <Instance Scope="GlobalVariable" UId="59">
                              <Component Name="FC_ArrayUintHandling" UId="58" />
                            </Instance>
                            <Parameter Name="I_InNumber" Section="Input" UId="61">
                              <Access Scope="GlobalVariable" UId="65"><Symbol><Component Name="Cell_DB" /><Component Name="Count" /></Symbol></Access>
                            </Parameter>
                            <Parameter Name="O_Result" Section="Output" UId="72">
                              <Access Scope="GlobalVariable" UId="76"><Symbol><Component Name="Cell_DB" /><Component Name="Result" /></Symbol></Access>
                            </Parameter>
                          </CallInfo>
                        </Access>
                      </StructuredText></NetworkSource>
                      <ProgrammingLanguage>SCL</ProgrammingLanguage>
                    </AttributeList>
                  </SW.Blocks.CompileUnit>
                </ObjectList>
              </SW.Blocks.FB>
            </Document>
            """;

        var result = ProgramSemanticReferenceBuilder.Parse(
            xml,
            new ProgramBlockComponent("Cell_Sequence", "FB", "Blocks/Cell_Sequence", "Blocks\\Cell_Sequence.xml"));

        var network = Assert.Single(result.Networks);
        Assert.Equal("SCL", network.Language);
        Assert.Contains("FC_ArrayUintHandling", network.Calls);
        Assert.Contains("Cell_DB.Count", network.Reads);
        Assert.Contains("Cell_DB.Result", network.Writes);
        Assert.Contains(result.References, item => item.Access == "call" && item.To == "FC_ArrayUintHandling");
        Assert.Contains(result.References, item => item.Access == "read" && item.To == "Cell_DB.Count" && item.Parameter == "I_InNumber");
        Assert.Contains(result.References, item => item.Access == "write" && item.To == "Cell_DB.Result" && item.Parameter == "O_Result");
    }

    [Fact]
    public void FallsBackNetworkTitlesDeterministically()
    {
        var xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <Document>
              <SW.Blocks.FC ID="0">
                <AttributeList><Name>Fallbacks</Name><ProgrammingLanguage>LAD</ProgrammingLanguage></AttributeList>
                <ObjectList>
                  <SW.Blocks.CompileUnit ID="1"><AttributeList><ProgrammingLanguage>LAD</ProgrammingLanguage></AttributeList><ObjectList>
                    <MultilingualText CompositionName="Title"><ObjectList>
                      <MultilingualTextItem><AttributeList><Culture>en-US</Culture><Text>US title</Text></AttributeList></MultilingualTextItem>
                      <MultilingualTextItem><AttributeList><Culture>en-GB</Culture><Text>GB title</Text></AttributeList></MultilingualTextItem>
                    </ObjectList></MultilingualText>
                  </ObjectList></SW.Blocks.CompileUnit>
                  <SW.Blocks.CompileUnit ID="2"><AttributeList><ProgrammingLanguage>LAD</ProgrammingLanguage></AttributeList><ObjectList>
                    <MultilingualText CompositionName="Title"><ObjectList>
                      <MultilingualTextItem><AttributeList><Culture>zh-CN</Culture><Text>First non-empty</Text></AttributeList></MultilingualTextItem>
                    </ObjectList></MultilingualText>
                  </ObjectList></SW.Blocks.CompileUnit>
                  <SW.Blocks.CompileUnit ID="3"><AttributeList><ProgrammingLanguage>LAD</ProgrammingLanguage></AttributeList></SW.Blocks.CompileUnit>
                </ObjectList>
              </SW.Blocks.FC>
            </Document>
            """;

        var result = ProgramSemanticReferenceBuilder.Parse(
            xml,
            new ProgramBlockComponent("Fallbacks", "FC", "Blocks/Fallbacks", "Blocks\\Fallbacks.xml"));

        Assert.Collection(
            result.Networks,
            item => Assert.Equal("GB title", item.Title),
            item => Assert.Equal("First non-empty", item.Title),
            item => Assert.Equal("Network 3", item.Title));
    }

    [Fact]
    public void ClassifiesStandaloneLadContactAndCoilsByWireRole()
    {
        var xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <Document>
              <SW.Blocks.FC ID="0">
                <AttributeList><Name>Manual_Control</Name><ProgrammingLanguage>LAD</ProgrammingLanguage></AttributeList>
                <ObjectList>
                  <SW.Blocks.CompileUnit ID="1">
                    <AttributeList>
                      <NetworkSource><FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
                        <Parts>
                          <Access Scope="GlobalVariable" UId="25"><Symbol><Component Name="Manual_DB" /><Component Name="Enable" /></Symbol></Access>
                          <Access Scope="GlobalVariable" UId="26"><Symbol><Component Name="Manual_DB" /><Component Name="Run" /></Symbol></Access>
                          <Access Scope="GlobalVariable" UId="27"><Symbol><Component Name="Manual_DB" /><Component Name="Reset" /></Symbol></Access>
                          <Access Scope="GlobalVariable" UId="28"><Symbol><Component Name="Manual_DB" /><Component Name="Set" /></Symbol></Access>
                          <Part Name="Contact" UId="31" />
                          <Part Name="Coil" UId="32" />
                          <Part Name="RCoil" UId="33" />
                          <Part Name="SCoil" UId="34" />
                        </Parts>
                        <Wires>
                          <Wire UId="41"><Powerrail /><NameCon UId="31" Name="in" /></Wire>
                          <Wire UId="42"><IdentCon UId="25" /><NameCon UId="31" Name="operand" /></Wire>
                          <Wire UId="43"><NameCon UId="31" Name="out" /><NameCon UId="32" Name="in" /></Wire>
                          <Wire UId="44"><IdentCon UId="26" /><NameCon UId="32" Name="operand" /></Wire>
                          <Wire UId="45"><NameCon UId="32" Name="out" /><NameCon UId="33" Name="in" /></Wire>
                          <Wire UId="46"><IdentCon UId="27" /><NameCon UId="33" Name="operand" /></Wire>
                          <Wire UId="47"><NameCon UId="33" Name="out" /><NameCon UId="34" Name="in" /></Wire>
                          <Wire UId="48"><IdentCon UId="28" /><NameCon UId="34" Name="operand" /></Wire>
                        </Wires>
                      </FlgNet></NetworkSource>
                      <ProgrammingLanguage>LAD</ProgrammingLanguage>
                    </AttributeList>
                  </SW.Blocks.CompileUnit>
                </ObjectList>
              </SW.Blocks.FC>
            </Document>
            """;

        var result = ProgramSemanticReferenceBuilder.Parse(
            xml,
            new ProgramBlockComponent("Manual_Control", "FC", "Blocks/Manual_Control", "Blocks\\Manual_Control.xml"));

        var network = Assert.Single(result.Networks);
        Assert.Equal(new[] { "Manual_DB.Enable" }, network.Reads);
        Assert.Equal(
            new[] { "Manual_DB.Reset", "Manual_DB.Run", "Manual_DB.Set" },
            network.Writes.OrderBy(item => item, StringComparer.Ordinal).ToArray());

        Assert.Contains(result.References, item => item.To == "Manual_DB.Enable" && item.Access == "read");
        Assert.Contains(result.References, item => item.To == "Manual_DB.Run" && item.Access == "write");
        Assert.Contains(result.References, item => item.To == "Manual_DB.Reset" && item.Access == "write");
        Assert.Contains(result.References, item => item.To == "Manual_DB.Set" && item.Access == "write");
        Assert.DoesNotContain(result.References, item => item.Access == "unknown");
    }

    [Fact]
    public void ClassifiesStandaloneLadTimerAndLatchPinsByWireRole()
    {
        var xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <Document>
              <SW.Blocks.FC ID="0">
                <AttributeList><Name>Cylinder_Sim</Name><ProgrammingLanguage>LAD</ProgrammingLanguage></AttributeList>
                <ObjectList>
                  <SW.Blocks.CompileUnit ID="1">
                    <AttributeList>
                      <NetworkSource><FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
                        <Parts>
                          <Access Scope="GlobalVariable" UId="21"><Symbol><Component Name="CylinderGoForwardPos" /></Symbol></Access>
                          <Access Scope="GlobalVariable" UId="22"><Symbol><Component Name="CylinderGoBackwardPos" /></Symbol></Access>
                          <Access Scope="GlobalVariable" UId="23"><Symbol><Component Name="CylinderMovementSimulate" /></Symbol></Access>
                          <Access Scope="GlobalVariable" UId="25"><Symbol><Component Name="Cylinder.ForwardPos" /></Symbol></Access>
                          <Part Name="Contact" UId="26" />
                          <Part Name="Contact" UId="27"><Negated Name="operand" /></Part>
                          <Part Name="TON" Version="1.0" UId="28">
                            <Instance Scope="GlobalVariable" UId="29">
                              <Component Name="IEC_CylForwardMovement" />
                            </Instance>
                            <TemplateValue Name="time_type" Type="Type">Time</TemplateValue>
                          </Part>
                          <Part Name="Contact" UId="30" />
                          <Part Name="Sr" UId="31" />
                        </Parts>
                        <Wires>
                          <Wire UId="33">
                            <Powerrail />
                            <NameCon UId="26" Name="in" />
                            <NameCon UId="30" Name="in" />
                          </Wire>
                          <Wire UId="34"><IdentCon UId="21" /><NameCon UId="26" Name="operand" /></Wire>
                          <Wire UId="35"><NameCon UId="26" Name="out" /><NameCon UId="27" Name="in" /></Wire>
                          <Wire UId="36"><IdentCon UId="22" /><NameCon UId="27" Name="operand" /></Wire>
                          <Wire UId="37"><NameCon UId="27" Name="out" /><NameCon UId="28" Name="IN" /></Wire>
                          <Wire UId="38"><IdentCon UId="23" /><NameCon UId="28" Name="PT" /></Wire>
                          <Wire UId="39"><NameCon UId="28" Name="Q" /><NameCon UId="31" Name="s" /></Wire>
                          <Wire UId="41"><IdentCon UId="22" /><NameCon UId="30" Name="operand" /></Wire>
                          <Wire UId="42"><NameCon UId="30" Name="out" /><NameCon UId="31" Name="r1" /></Wire>
                          <Wire UId="43"><IdentCon UId="25" /><NameCon UId="31" Name="operand" /></Wire>
                        </Wires>
                      </FlgNet></NetworkSource>
                      <ProgrammingLanguage>LAD</ProgrammingLanguage>
                    </AttributeList>
                  </SW.Blocks.CompileUnit>
                </ObjectList>
              </SW.Blocks.FC>
            </Document>
            """;

        var result = ProgramSemanticReferenceBuilder.Parse(
            xml,
            new ProgramBlockComponent("Cylinder_Sim", "FC", "Blocks/Cylinder_Sim", "Blocks\\Cylinder_Sim.xml"));

        var network = Assert.Single(result.Networks);
        Assert.Equal(
            new[] { "CylinderGoBackwardPos", "CylinderGoForwardPos", "CylinderMovementSimulate" },
            network.Reads.OrderBy(item => item, StringComparer.Ordinal).ToArray());
        Assert.Equal(new[] { "Cylinder.ForwardPos" }, network.Writes);

        Assert.Contains(result.References, item => item.To == "CylinderGoForwardPos" && item.Access == "read");
        Assert.Contains(result.References, item => item.To == "CylinderGoBackwardPos" && item.Access == "read");
        Assert.Contains(result.References, item => item.To == "CylinderMovementSimulate" && item.Access == "read");
        Assert.Contains(result.References, item => item.To == "Cylinder.ForwardPos" && item.Access == "write");
        Assert.DoesNotContain(result.References, item => item.Access == "unknown");
    }

    [Fact]
    public void WritesJsonlFilesFromExportFolder()
    {
        var root = Path.Combine(Path.GetTempPath(), "PlcSourceExporterTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "Blocks"));
        File.WriteAllText(
            Path.Combine(root, "metadata.json"),
            """
            {
              "schemaVersion": "1.0",
              "components": [
                { "name": "Main", "sourcePath": "Blocks/Main", "category": "OB", "status": "Exported", "exportedFile": "Blocks\\Main.xml" }
              ]
            }
            """);
        File.WriteAllText(
            Path.Combine(root, "Blocks", "Main.xml"),
            """
            <?xml version="1.0" encoding="utf-8"?>
            <Document>
              <SW.Blocks.OB ID="0">
                <AttributeList><Name>Main</Name><ProgrammingLanguage>LAD</ProgrammingLanguage></AttributeList>
                <ObjectList>
                  <SW.Blocks.CompileUnit ID="1"><AttributeList><ProgrammingLanguage>LAD</ProgrammingLanguage></AttributeList></SW.Blocks.CompileUnit>
                </ObjectList>
              </SW.Blocks.OB>
            </Document>
            """);

        var result = ProgramSemanticReferenceBuilder.Write(root, new DateTimeOffset(2026, 6, 22, 1, 2, 3, TimeSpan.Zero));

        Assert.True(File.Exists(result.NetworksFilePath));
        Assert.True(File.Exists(result.ReferencesFilePath));
        Assert.Equal(1, result.NetworkCount);
        Assert.Contains("\"schemaVersion\":\"1.0\"", File.ReadAllText(result.NetworksFilePath));
        Assert.Contains("\"id\":\"network:Main:1\"", File.ReadAllText(result.NetworksFilePath));
    }
}
