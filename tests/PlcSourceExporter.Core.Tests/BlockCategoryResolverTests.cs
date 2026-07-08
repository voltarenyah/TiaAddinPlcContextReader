using PlcSourceExporter.Core;

namespace PlcSourceExporter.Core.Tests;

public sealed class BlockCategoryResolverTests
{
    [Theory]
    [InlineData("OrganizationBlock", "Main", ExportCategory.OrganizationBlock)]
    [InlineData("OB", "OB1", ExportCategory.OrganizationBlock)]
    [InlineData("OB", "ProDiagOB", ExportCategory.OrganizationBlock)]
    [InlineData("Siemens.Engineering.SW.Blocks.OB", "ProDiagOB", ExportCategory.OrganizationBlock)]
    [InlineData("Function", "Scale", ExportCategory.Function)]
    [InlineData("FC", "FC10", ExportCategory.Function)]
    [InlineData("FC", "Scale", ExportCategory.Function)]
    [InlineData("Siemens.Engineering.SW.Blocks.FC", "Scale", ExportCategory.Function)]
    [InlineData("FunctionBlock", "Motor", ExportCategory.FunctionBlock)]
    [InlineData("FB", "FB_Motor", ExportCategory.FunctionBlock)]
    [InlineData("FB", "Default_SupervisionFB", ExportCategory.FunctionBlock)]
    [InlineData("Siemens.Engineering.SW.Blocks.FB", "Default_SupervisionFB", ExportCategory.FunctionBlock)]
    [InlineData("DataBlock", "Config", ExportCategory.DataBlock)]
    [InlineData("DB", "Config", ExportCategory.DataBlock)]
    [InlineData("Siemens.Engineering.SW.Blocks.DB", "Config", ExportCategory.DataBlock)]
    [InlineData("GlobalDB", "DB_Config", ExportCategory.DataBlock)]
    [InlineData("InstanceDB", "FB_Motor_DB", ExportCategory.DataBlock)]
    [InlineData("Siemens.Engineering.SW.Blocks.InstanceDB", "FB_Motor_DB", ExportCategory.DataBlock)]
    public void ResolvesBlockCategoryFromSiemensTypeNameAndObjectName(string typeName, string objectName, ExportCategory expected)
    {
        Assert.Equal(expected, BlockCategoryResolver.ResolveBlockCategory(typeName, objectName));
    }
}
