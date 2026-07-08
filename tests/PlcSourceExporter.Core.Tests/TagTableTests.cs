using PlcSourceExporter.Core;

namespace PlcSourceExporter.Core.Tests;

public sealed class TagTableTests
{
    [Fact]
    public void ParsesTagTableXmlIntoFlatTagRows()
    {
        var xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <Document>
              <SW.Tags.PlcTagTable ID="0">
                <AttributeList>
                  <Name>Robot IO</Name>
                </AttributeList>
                <ObjectList>
                  <SW.Tags.PlcTag ID="1" CompositionName="Tags">
                    <AttributeList>
                      <DataTypeName>&quot;UDT_Robot_In&quot;</DataTypeName>
                      <ExternalAccessible>true</ExternalAccessible>
                      <ExternalVisible>false</ExternalVisible>
                      <ExternalWritable>true</ExternalWritable>
                      <LogicalAddress>%I7000.0</LogicalAddress>
                      <Name>Robot_IN</Name>
                    </AttributeList>
                    <ObjectList>
                      <MultilingualText ID="2" CompositionName="Comment">
                        <ObjectList>
                          <MultilingualTextItem ID="3" CompositionName="Items">
                            <AttributeList>
                              <Culture>en-US</Culture>
                              <Text>Robot input area</Text>
                            </AttributeList>
                          </MultilingualTextItem>
                        </ObjectList>
                      </MultilingualText>
                    </ObjectList>
                  </SW.Tags.PlcTag>
                  <SW.Tags.PlcTag ID="4" CompositionName="Tags">
                    <AttributeList>
                      <DataTypeName>Bool</DataTypeName>
                      <LogicalAddress>%M1.0</LogicalAddress>
                      <Name>Ready</Name>
                    </AttributeList>
                  </SW.Tags.PlcTag>
                </ObjectList>
              </SW.Tags.PlcTagTable>
            </Document>
            """;

        var rows = TagTableBuilder.ParseRows(xml, "Tags\\Robot IO.xml", "Tags/Robot IO");

        Assert.Collection(
            rows,
            row =>
            {
                Assert.Equal("tag:Robot IO:Robot_IN:%I7000.0", row.Id);
                Assert.Equal("Robot IO", row.TagTable);
                Assert.Equal("Tags/Robot IO", row.TagTableSourcePath);
                Assert.Equal("Robot_IN", row.Name);
                Assert.Equal("UDT_Robot_In", row.DataType);
                Assert.Equal("\"UDT_Robot_In\"", row.RawDataType);
                Assert.Equal("%I7000.0", row.LogicalAddress);
                Assert.True(row.ExternalAccessible);
                Assert.False(row.ExternalVisible);
                Assert.True(row.ExternalWritable);
                Assert.Equal("Robot input area", row.Comment);
                Assert.Equal("Tags\\Robot IO.xml", row.SourceFile);
            },
            row =>
            {
                Assert.Equal("Ready", row.Name);
                Assert.Equal("Bool", row.DataType);
                Assert.Null(row.ExternalAccessible);
                Assert.Null(row.ExternalVisible);
                Assert.Null(row.ExternalWritable);
            });
    }

    [Fact]
    public void WritesTagsJsonDocument()
    {
        var document = new TagTableDocument(
            "1.0",
            new DateTimeOffset(2026, 6, 21, 1, 2, 3, TimeSpan.Zero),
            "C:\\Export\\Tags",
            [
                new TagTableRow(
                    "tag:Robot IO:Robot_IN:%I7000.0",
                    "Robot IO",
                    "Tags/Robot IO",
                    "Robot_IN",
                    "UDT_Robot_In",
                    "\"UDT_Robot_In\"",
                    "%I7000.0",
                    true,
                    false,
                    true,
                    "Robot input area",
                    "Tags\\Robot IO.xml")
            ]);

        var json = TagTableJsonSerializer.Serialize(document);

        Assert.Contains("\"schemaVersion\": \"1.0\"", json);
        Assert.Contains("\"generatedUtc\": \"2026-06-21T01:02:03.0000000+00:00\"", json);
        Assert.Contains("\"tagCount\": 1", json);
        Assert.Contains("\"tagTable\": \"Robot IO\"", json);
        Assert.Contains("\"dataType\": \"UDT_Robot_In\"", json);
        Assert.Contains("\"rawDataType\": \"\\\"UDT_Robot_In\\\"\"", json);
        Assert.Contains("\"externalVisible\": false", json);
    }
}
