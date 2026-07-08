using PlcSourceExporter.Core;

namespace PlcSourceExporter.Core.Tests;

public sealed class ProgramBlockComponentCatalogTests
{
    [Fact]
    public void MetadataDtosArePublicForPartialTrustJsonDeserialization()
    {
        Assert.True(typeof(ComponentMetadataDocumentDto).IsPublic);
        Assert.True(typeof(ComponentMetadataRecordDto).IsPublic);
    }

    [Fact]
    public void LoadsOnlyExportedProgramBlocksFromMetadata()
    {
        var root = Path.Combine(Path.GetTempPath(), "PlcSourceExporterTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(
            Path.Combine(root, ExportMetadataWriter.MetadataFileName),
            """
            {
              "schemaVersion": "1.0",
              "components": [
                { "name": "Main", "sourcePath": "Blocks/Main", "category": "OB", "status": "Exported", "exportedFile": "Blocks\\Main.xml" },
                { "name": "Helper", "sourcePath": "Blocks/Helper", "category": "FC", "status": "Exported", "exportedFile": "Blocks\\Helper.xml" },
                { "name": "Logic", "sourcePath": "Blocks/Logic", "category": "FB", "status": "Exported", "exportedFile": "Blocks\\Logic.xml" },
                { "name": "SkippedBlock", "sourcePath": "Blocks/SkippedBlock", "category": "FC", "status": "Skipped", "exportedFile": "Blocks\\SkippedBlock.xml" },
                { "name": "TagTable", "sourcePath": "Tags/Default tag table", "category": "TagTable", "status": "Exported", "exportedFile": "Tags\\Default.xml" }
              ]
            }
            """);

        var components = ProgramBlockComponentCatalog.LoadExportedProgramBlocks(root);

        Assert.Collection(
            components,
            component =>
            {
                Assert.Equal("Main", component.Name);
                Assert.Equal("OB", component.Category);
            },
            component =>
            {
                Assert.Equal("Helper", component.Name);
                Assert.Equal("FC", component.Category);
            },
            component =>
            {
                Assert.Equal("Logic", component.Name);
                Assert.Equal("FB", component.Category);
            });
    }
}
