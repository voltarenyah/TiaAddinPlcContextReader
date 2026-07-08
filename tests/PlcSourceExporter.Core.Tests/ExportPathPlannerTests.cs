using PlcSourceExporter.Core;

namespace PlcSourceExporter.Core.Tests;

public sealed class ExportPathPlannerTests
{
    [Fact]
    public void CategoryFoldersAreFixedAndFlat()
    {
        Assert.Equal(
            new[] { "Blocks", "Blocks", "Blocks", "DB", "UDT", "Tags" },
            ExportCategories.All.Select(category => category.FolderName));
    }

    [Fact]
    public void SanitizesInvalidWindowsFileNameCharacters()
    {
        var planner = new ExportPathPlanner(@"C:\Project\UserFiles\export");

        var path = planner.NextFilePath(ExportCategory.FunctionBlock, "Pump:FB/1*?");

        Assert.Equal(@"C:\Project\UserFiles\export\Blocks\Pump_FB_1__.xml", path);
    }

    [Fact]
    public void DuplicateNamesUseDeterministicNumericSuffixes()
    {
        var planner = new ExportPathPlanner(@"C:\Project\UserFiles\export");

        var first = planner.NextFilePath(ExportCategory.DataBlock, "Config");
        var second = planner.NextFilePath(ExportCategory.DataBlock, "Config");
        var third = planner.NextFilePath(ExportCategory.DataBlock, "Config");

        Assert.Equal(@"C:\Project\UserFiles\export\DB\Config.xml", first);
        Assert.Equal(@"C:\Project\UserFiles\export\DB\Config_2.xml", second);
        Assert.Equal(@"C:\Project\UserFiles\export\DB\Config_3.xml", third);
    }
}
