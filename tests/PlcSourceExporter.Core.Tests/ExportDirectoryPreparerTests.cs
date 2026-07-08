using PlcSourceExporter.Core;

namespace PlcSourceExporter.Core.Tests;

public sealed class ExportDirectoryPreparerTests
{
    [Fact]
    public void PrepareCreatesOnlyFixedCategoryFolders()
    {
        var root = CreateTempRoot();

        ExportDirectoryPreparer.Prepare(root);

        var folders = Directory.GetDirectories(root).Select(Path.GetFileName).Order().ToArray();
        Assert.Equal(new[] { "Blocks", "DB", "Tags", "UDT" }, folders);
    }

    [Fact]
    public void PrepareClearsExistingExportRootButLeavesSiblingsUntouched()
    {
        var projectRoot = CreateTempRoot();
        var exportRoot = Path.Combine(projectRoot, "UserFiles", "export");
        var sibling = Path.Combine(projectRoot, "UserFiles", "keep.txt");
        Directory.CreateDirectory(exportRoot);
        File.WriteAllText(Path.Combine(exportRoot, "old.xml"), "old");
        Directory.CreateDirectory(Path.GetDirectoryName(sibling)!);
        File.WriteAllText(sibling, "keep");

        ExportDirectoryPreparer.Prepare(exportRoot);

        Assert.False(File.Exists(Path.Combine(exportRoot, "old.xml")));
        Assert.True(File.Exists(sibling));
    }

    [Fact]
    public void PrepareKeepsActiveExporterLog()
    {
        var exportRoot = CreateTempRoot();
        var logFile = Path.Combine(exportRoot, "PlcSourceExporter.log");
        File.WriteAllText(logFile, "active log");
        File.WriteAllText(Path.Combine(exportRoot, "old.json"), "old");

        ExportDirectoryPreparer.Prepare(exportRoot);

        Assert.True(File.Exists(logFile));
        Assert.Equal("active log", File.ReadAllText(logFile));
        Assert.False(File.Exists(Path.Combine(exportRoot, "old.json")));
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "PlcSourceExporterTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
}
