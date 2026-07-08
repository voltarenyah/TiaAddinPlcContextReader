using PlcSourceExporter.Core;

namespace PlcSourceExporter.Core.Tests;

public sealed class UdtTypeTableTests
{
    [Fact]
    public void ParsesOnlyFirstLevelUdtMembersIntoTheTypeTable()
    {
        var xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <Document>
              <Engineering version="V20" />
              <SW.Types.PlcStruct ID="0">
                <AttributeList>
                  <Interface><Sections xmlns="http://www.siemens.com/automation/Openness/SW/Interface/v5">
                    <Section Name="None">
                      <Member Name="Ready" Datatype="Bool" />
                      <Member Name="Motion" Datatype="UDT_Motion">
                        <Sections>
                          <Section Name="None">
                            <Member Name="Command" Datatype="UDT_MotionCommand" />
                            <Member Name="Speed" Datatype="Real" />
                          </Section>
                        </Sections>
                      </Member>
                    </Section>
                  </Sections></Interface>
                  <Name>UDT_Cell</Name>
                </AttributeList>
              </SW.Types.PlcStruct>
            </Document>
            """;

        var rows = UdtTypeTableBuilder.ParseRows(xml, "UDT\\UDT_Cell.xml", "Types/UDT_Cell");

        Assert.Collection(
            rows,
            row =>
            {
                Assert.Equal("Type", row.Kind);
                Assert.Equal("", row.ParentType);
                Assert.Equal("UDT_Cell", row.Name);
                Assert.Equal("UDT_Cell", row.DataType);
                Assert.Equal("", row.Path);
            },
            row =>
            {
                Assert.Equal("Member", row.Kind);
                Assert.Equal("UDT_Cell", row.ParentType);
                Assert.Equal("", row.ParentPath);
                Assert.Equal("Ready", row.Name);
                Assert.Equal("Ready", row.Path);
                Assert.Equal("Bool", row.DataType);
            },
            row =>
            {
                Assert.Equal("Member", row.Kind);
                Assert.Equal("UDT_Cell", row.ParentType);
                Assert.Equal("", row.ParentPath);
                Assert.Equal("Motion", row.Name);
                Assert.Equal("Motion", row.Path);
                Assert.Equal("UDT_Motion", row.DataType);
            });
    }

    [Fact]
    public void WritesJsonWithStableRowsAndEscapedValues()
    {
        var document = new UdtTypeTableDocument(
            "1.0",
            new DateTimeOffset(2026, 6, 19, 2, 3, 4, TimeSpan.Zero),
            "C:\\Export\\UDT",
            [
                new UdtTypeTableRow(
                    "type:UDT_Cell",
                    "Type",
                    "",
                    "",
                    "UDT_Cell",
                    "",
                    "UDT_Cell",
                    "Types/UDT_Cell",
                    "UDT\\UDT_Cell.xml"),
                new UdtTypeTableRow(
                    "member:UDT_Cell:Motion.Command",
                    "Member",
                    "UDT_Cell",
                    "Motion",
                    "Command",
                    "Motion.Command",
                    "UDT_MotionCommand",
                    "Types/UDT_Cell",
                    "UDT\\UDT_Cell.xml")
            ]);

        var json = UdtTypeTableJsonSerializer.Serialize(document);

        Assert.Contains("\"schemaVersion\": \"1.0\"", json);
        Assert.Contains("\"generatedUtc\": \"2026-06-19T02:03:04.0000000+00:00\"", json);
        Assert.Contains("\"sourceFolder\": \"C:\\\\Export\\\\UDT\"", json);
        Assert.Contains("\"parentType\": \"\"", json);
        Assert.Contains("\"parentType\": \"UDT_Cell\"", json);
        Assert.Contains("\"dataType\": \"UDT_MotionCommand\"", json);
    }
}
