using PlcSourceExporter.Core;

namespace PlcSourceExporter.Core.Tests;

public sealed class ExportEligibilityTests
{
    [Theory]
    [InlineData("ProDiag")]
    [InlineData("ProDiag_OB")]
    [InlineData("F_CALL")]
    public void ReturnsSkipReasonForUnsupportedProDiagLanguages(string programmingLanguage)
    {
        var reason = ExportEligibility.GetUnsupportedBlockLanguageReason(programmingLanguage);

        Assert.Contains(programmingLanguage, reason);
    }

    [Theory]
    [InlineData("LAD")]
    [InlineData("FBD")]
    [InlineData("SCL")]
    [InlineData("F_LAD")]
    [InlineData("DB")]
    public void AllowsNormalProgrammingLanguages(string programmingLanguage)
    {
        Assert.Null(ExportEligibility.GetUnsupportedBlockLanguageReason(programmingLanguage));
    }

    [Fact]
    public void ReturnsSkipReasonForSiemensNotPermittedBlockExportMessage()
    {
        var reason = ExportEligibility.GetNonExportableFailureReason(
            "Error when calling method 'Export' of type 'Siemens.Engineering.SW.Blocks.OB'.\r\n\r\nThe export of block 'FOB_RTG1' is not permitted.");

        Assert.Contains("FOB_RTG1", reason);
    }

    [Fact]
    public void DoesNotTreatGenericExportFailureAsSkip()
    {
        Assert.Null(ExportEligibility.GetNonExportableFailureReason("Disk is full."));
    }
}
