using PlcSourceExporter.Core;

namespace PlcSourceExporter.Core.Tests;

public sealed class ExportSummaryTests
{
    [Fact]
    public void CountsSuccessSkippedAndFailedItemsByCategory()
    {
        var summary = new ExportSummary();
        summary.AddSuccess(ExportCategory.OrganizationBlock, "OB1", @"C:\export\OB\OB1.xml");
        summary.AddSuccess(ExportCategory.OrganizationBlock, "OB2", @"C:\export\OB\OB2.xml");
        summary.AddSkipped(ExportCategory.FunctionBlock, "SecretFB", "Know-how protected");
        summary.AddFailure(ExportCategory.TagTable, "Tags", "Export failed");

        Assert.Equal(2, summary.SuccessCount);
        Assert.Equal(1, summary.SkippedCount);
        Assert.Equal(1, summary.FailureCount);
        Assert.Equal(2, summary.SuccessesByCategory[ExportCategory.OrganizationBlock]);
        Assert.Contains(summary.ToDisplayString(), line => line.Contains("OB: 2 exported"));
        Assert.Contains(summary.ToDisplayString(), line => line.Contains("1 skipped"));
        Assert.Contains(summary.ToDisplayString(), line => line.Contains("1 failed"));
    }
}
