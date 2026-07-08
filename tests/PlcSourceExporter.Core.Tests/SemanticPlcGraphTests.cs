using PlcSourceExporter.Core;

namespace PlcSourceExporter.Core.Tests;

public sealed class SemanticPlcGraphTests
{
    [Fact]
    public void ImportsBlockXmlIntoSemanticNodesAndRelationships()
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
                          <Access Scope="GlobalVariable" UId="25"><Symbol><Component Name="Cell_DB" /><Component Name="Enable" /></Symbol></Access>
                          <Access Scope="GlobalVariable" UId="26"><Symbol><Component Name="Cell_DB" /><Component Name="Command" /></Symbol></Access>
                          <Call UId="28">
                            <CallInfo Name="FC_StartCell" BlockType="FC">
                              <Parameter Name="I_Enable" Section="Input" Type="Bool" />
                              <Parameter Name="O_Command" Section="Output" Type="Bool" />
                            </CallInfo>
                          </Call>
                        </Parts>
                        <Wires>
                          <Wire UId="30"><IdentCon UId="25" /><NameCon UId="28" Name="I_Enable" /></Wire>
                          <Wire UId="31"><NameCon UId="28" Name="O_Command" /><IdentCon UId="26" /></Wire>
                        </Wires>
                      </FlgNet></NetworkSource>
                      <ProgrammingLanguage>LAD</ProgrammingLanguage>
                    </AttributeList>
                    <ObjectList>
                      <MultilingualText ID="T1" CompositionName="Title">
                        <ObjectList>
                          <MultilingualTextItem ID="I1" CompositionName="Items">
                            <AttributeList><Culture>en-US</Culture><Text>Start logic</Text></AttributeList>
                          </MultilingualTextItem>
                        </ObjectList>
                      </MultilingualText>
                    </ObjectList>
                  </SW.Blocks.CompileUnit>
                  <SW.Blocks.CompileUnit ID="5" CompositionName="CompileUnits">
                    <AttributeList><ProgrammingLanguage>LAD</ProgrammingLanguage></AttributeList>
                  </SW.Blocks.CompileUnit>
                </ObjectList>
              </SW.Blocks.OB>
            </Document>
            """;

        var graph = new SemanticPlcGraph();
        TiaXmlSemanticGraphImporter.ImportBlockXml(
            xml,
            new ProgramBlockComponent("Main", "OB", "Program blocks/Main", "Blocks\\Main.xml"),
            graph);

        Assert.Equal(SemanticNodeKind.OrganizationBlock, graph.GetNode("block:Main").Kind);
        Assert.Equal(SemanticNodeKind.Network, graph.GetNode("network:Main:1").Kind);
        Assert.Equal(SemanticNodeKind.Function, graph.GetNode("block:FC_StartCell").Kind);
        Assert.Equal(SemanticNodeKind.Variable, graph.GetNode("symbol:Cell_DB.Enable").Kind);

        Assert.Contains(graph.Edges, edge =>
            edge.Type == SemanticRelationshipType.Contains &&
            edge.FromNodeId == "block:Main" &&
            edge.ToNodeId == "network:Main:1");
        Assert.Contains(graph.Edges, edge =>
            edge.Type == SemanticRelationshipType.Calls &&
            edge.FromNodeId == "block:Main" &&
            edge.ToNodeId == "block:FC_StartCell");
        Assert.Contains(graph.Edges, edge =>
            edge.Type == SemanticRelationshipType.Reads &&
            edge.FromNodeId == "block:Main" &&
            edge.ToNodeId == "symbol:Cell_DB.Enable");
        Assert.Contains(graph.Edges, edge =>
            edge.Type == SemanticRelationshipType.Writes &&
            edge.FromNodeId == "block:Main" &&
            edge.ToNodeId == "symbol:Cell_DB.Command");
        Assert.Contains(graph.Edges, edge =>
            edge.Type == SemanticRelationshipType.ExecutesBefore &&
            edge.FromNodeId == "network:Main:1" &&
            edge.ToNodeId == "network:Main:2");
        Assert.Contains(graph.Edges, edge =>
            edge.Type == SemanticRelationshipType.ExecutesAfter &&
            edge.FromNodeId == "network:Main:2" &&
            edge.ToNodeId == "network:Main:1");
    }

    [Fact]
    public void ImportsUdtTagAndDbXmlIntoTypedEngineeringNodes()
    {
        var graph = new SemanticPlcGraph();

        TiaXmlSemanticGraphImporter.ImportUdtXml(
            """
            <Document>
              <SW.Types.PlcStruct ID="0">
                <AttributeList>
                  <Name>UDT_Cell</Name>
                  <Interface><Sections><Section Name="None">
                    <Member Name="Ready" Datatype="Bool" />
                  </Section></Sections></Interface>
                </AttributeList>
              </SW.Types.PlcStruct>
            </Document>
            """,
            "UDT\\UDT_Cell.xml",
            "PLC data types/UDT_Cell",
            graph);

        TiaXmlSemanticGraphImporter.ImportTagTableXml(
            """
            <Document>
              <SW.Tags.PlcTagTable ID="0">
                <AttributeList><Name>Default tags</Name></AttributeList>
                <ObjectList><SW.Tags.PlcTag ID="1" CompositionName="Tags">
                  <AttributeList>
                    <Name>CellReady</Name>
                    <DataTypeName>Bool</DataTypeName>
                    <LogicalAddress>%I0.0</LogicalAddress>
                  </AttributeList>
                </SW.Tags.PlcTag></ObjectList>
              </SW.Tags.PlcTagTable>
            </Document>
            """,
            "Tags\\Default tags.xml",
            "PLC tags/Default tags",
            graph);

        TiaXmlSemanticGraphImporter.ImportDbXml(
            """
            <Document>
              <SW.Blocks.InstanceDB ID="0">
                <AttributeList>
                  <Name>Inst_StartCell</Name>
                  <InstanceOfName>FB_StartCell</InstanceOfName>
                  <Interface><Sections><Section Name="Static">
                    <Member Name="State" Datatype="Int" />
                  </Section></Sections></Interface>
                </AttributeList>
              </SW.Blocks.InstanceDB>
            </Document>
            """,
            "DB\\Inst_StartCell.xml",
            "Program blocks/Inst_StartCell",
            graph);

        Assert.Equal(SemanticNodeKind.UserDataType, graph.GetNode("udt:UDT_Cell").Kind);
        Assert.Equal(SemanticNodeKind.UserDataTypeMember, graph.GetNode("udt-member:UDT_Cell:Ready").Kind);
        Assert.Equal(SemanticNodeKind.PlcTag, graph.GetNode("tag:Default tags:CellReady:%I0.0").Kind);
        Assert.Equal(SemanticNodeKind.IoAddress, graph.GetNode("io:%I0.0").Kind);
        Assert.Equal(SemanticNodeKind.InstanceDataBlock, graph.GetNode("db:Inst_StartCell").Kind);
        Assert.Equal(SemanticNodeKind.DataBlockMember, graph.GetNode("db-member:Inst_StartCell:State").Kind);

        Assert.Contains(graph.Edges, edge =>
            edge.Type == SemanticRelationshipType.HasType &&
            edge.FromNodeId == "db-member:Inst_StartCell:State" &&
            edge.ToNodeId == "type:Int");
        Assert.Contains(graph.Edges, edge =>
            edge.Type == SemanticRelationshipType.InstanceOf &&
            edge.FromNodeId == "db:Inst_StartCell" &&
            edge.ToNodeId == "block:FB_StartCell");
        Assert.Contains(graph.Edges, edge =>
            edge.Type == SemanticRelationshipType.ConnectedTo &&
            edge.FromNodeId == "tag:Default tags:CellReady:%I0.0" &&
            edge.ToNodeId == "io:%I0.0");
    }

    [Fact]
    public void QueriesReturnCallGraphDependencyGraphAndVariableUsage()
    {
        var graph = new SemanticPlcGraph();
        graph.UpsertNode(new SemanticGraphNode("block:Main", SemanticNodeKind.OrganizationBlock, "Main"));
        graph.UpsertNode(new SemanticGraphNode("block:FC_StartCell", SemanticNodeKind.Function, "FC_StartCell"));
        graph.UpsertNode(new SemanticGraphNode("symbol:Cell_DB.Enable", SemanticNodeKind.Variable, "Cell_DB.Enable"));
        graph.UpsertEdge(new SemanticGraphEdge("edge:call", "block:Main", "block:FC_StartCell", SemanticRelationshipType.Calls));
        graph.UpsertEdge(new SemanticGraphEdge("edge:read", "block:Main", "symbol:Cell_DB.Enable", SemanticRelationshipType.Reads));

        var queries = new PlcSemanticGraphQueries(graph);

        Assert.Equal(new[] { "Main" }, queries.FindBlocksCalling("FC_StartCell").Select(node => node.Name).ToArray());
        Assert.Equal(new[] { "Main" }, queries.FindBlocksReading("Cell_DB.Enable").Select(node => node.Name).ToArray());
        Assert.Equal(
            new[] { ("Main", "FC_StartCell") },
            queries.BuildCallGraph().Select(edge => (edge.Caller.Name, edge.Callee.Name)).ToArray());
        Assert.Contains(queries.BuildDependencyGraph(), edge =>
            edge.Source.Name == "Main" &&
            edge.Target.Name == "Cell_DB.Enable" &&
            edge.Relationship == SemanticRelationshipType.Reads);
    }

    [Fact]
    public void SqliteStoreUsesGenericTablesAndRoundTripsGraph()
    {
        var graph = new SemanticPlcGraph();
        graph.UpsertNode(new SemanticGraphNode(
            "block:Main",
            SemanticNodeKind.OrganizationBlock,
            "Main",
            new Dictionary<string, string> { ["folderPath"] = "Program blocks/Main" }));
        graph.UpsertNode(new SemanticGraphNode("block:FC_StartCell", SemanticNodeKind.Function, "FC_StartCell"));
        graph.UpsertEdge(new SemanticGraphEdge(
            "edge:Main:FC_StartCell",
            "block:Main",
            "block:FC_StartCell",
            SemanticRelationshipType.Calls,
            new Dictionary<string, string> { ["networkId"] = "network:Main:1" }));

        var dbPath = Path.Combine(Path.GetTempPath(), "PlcSourceExporterTests", Guid.NewGuid().ToString("N"), "graph.sqlite");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

        SqliteSemanticGraphStore.Save(dbPath, graph);
        var loaded = SqliteSemanticGraphStore.Load(dbPath);

        Assert.Contains("CREATE TABLE IF NOT EXISTS graph_nodes", PlcSemanticGraphSqliteSchema.CreateScript);
        Assert.Contains("CREATE TABLE IF NOT EXISTS graph_edges", PlcSemanticGraphSqliteSchema.CreateScript);
        Assert.Equal("Program blocks/Main", loaded.GetNode("block:Main").Properties["folderPath"]);
        Assert.Contains(loaded.Edges, edge =>
            edge.Type == SemanticRelationshipType.Calls &&
            edge.Properties["networkId"] == "network:Main:1");
    }

    [Fact]
    public void ImportsExportRootAndWritesSqliteGraph()
    {
        var root = Path.Combine(Path.GetTempPath(), "PlcSourceExporterTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "Blocks"));
        Directory.CreateDirectory(Path.Combine(root, "DB"));
        Directory.CreateDirectory(Path.Combine(root, "UDT"));
        Directory.CreateDirectory(Path.Combine(root, "Tags"));
        File.WriteAllText(
            Path.Combine(root, "metadata.json"),
            """
            {
              "schemaVersion": "1.0",
              "components": [
                { "name": "Main", "sourcePath": "Program blocks/Main", "category": "OB", "status": "Exported", "exportedFile": "Blocks\\Main.xml" },
                { "name": "CellDB", "sourcePath": "Program blocks/CellDB", "category": "DB", "status": "Exported", "exportedFile": "DB\\CellDB.xml" },
                { "name": "UDT_Cell", "sourcePath": "PLC data types/UDT_Cell", "category": "UDT", "status": "Exported", "exportedFile": "UDT\\UDT_Cell.xml" },
                { "name": "Default tags", "sourcePath": "PLC tags/Default tags", "category": "Tags", "status": "Exported", "exportedFile": "Tags\\Default tags.xml" }
              ]
            }
            """);
        File.WriteAllText(
            Path.Combine(root, "Blocks", "Main.xml"),
            """
            <Document><SW.Blocks.OB ID="0"><AttributeList><Name>Main</Name><ProgrammingLanguage>LAD</ProgrammingLanguage></AttributeList><ObjectList>
              <SW.Blocks.CompileUnit ID="1"><AttributeList><ProgrammingLanguage>LAD</ProgrammingLanguage></AttributeList></SW.Blocks.CompileUnit>
            </ObjectList></SW.Blocks.OB></Document>
            """);
        File.WriteAllText(
            Path.Combine(root, "DB", "CellDB.xml"),
            """
            <Document><SW.Blocks.GlobalDB ID="0"><AttributeList><Name>CellDB</Name><Interface><Sections><Section Name="Static">
              <Member Name="Ready" Datatype="Bool" />
            </Section></Sections></Interface></AttributeList></SW.Blocks.GlobalDB></Document>
            """);
        File.WriteAllText(
            Path.Combine(root, "UDT", "UDT_Cell.xml"),
            """
            <Document><SW.Types.PlcStruct ID="0"><AttributeList><Name>UDT_Cell</Name><Interface><Sections><Section Name="None">
              <Member Name="Command" Datatype="Bool" />
            </Section></Sections></Interface></AttributeList></SW.Types.PlcStruct></Document>
            """);
        File.WriteAllText(
            Path.Combine(root, "Tags", "Default tags.xml"),
            """
            <Document><SW.Tags.PlcTagTable ID="0"><AttributeList><Name>Default tags</Name></AttributeList><ObjectList>
              <SW.Tags.PlcTag ID="1"><AttributeList><Name>CellReady</Name><DataTypeName>Bool</DataTypeName><LogicalAddress>%I0.0</LogicalAddress></AttributeList></SW.Tags.PlcTag>
            </ObjectList></SW.Tags.PlcTagTable></Document>
            """);

        var graph = TiaXmlSemanticGraphImporter.ImportExportRoot(root);
        var sqlitePath = Path.Combine(root, "plc-graph.sqlite");

        TiaXmlSemanticGraphImporter.WriteSqlite(root, sqlitePath);
        var loaded = SqliteSemanticGraphStore.Load(sqlitePath);

        Assert.Equal(SemanticNodeKind.OrganizationBlock, graph.GetNode("block:Main").Kind);
        Assert.Equal(SemanticNodeKind.GlobalDataBlock, graph.GetNode("db:CellDB").Kind);
        Assert.Equal(SemanticNodeKind.UserDataType, graph.GetNode("udt:UDT_Cell").Kind);
        Assert.Equal(SemanticNodeKind.PlcTag, graph.GetNode("tag:Default tags:CellReady:%I0.0").Kind);
        Assert.Equal(SemanticNodeKind.GlobalDataBlock, loaded.GetNode("db:CellDB").Kind);
    }
}
