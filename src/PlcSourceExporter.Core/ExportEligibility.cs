namespace PlcSourceExporter.Core;

public static class ExportEligibility
{
    private static readonly HashSet<string> UnsupportedBlockLanguages = new(StringComparer.OrdinalIgnoreCase)
    {
        "ProDiag",
        "ProDiag_OB",
        "F_CALL"
    };

    public static string? GetUnsupportedBlockLanguageReason(string? programmingLanguage)
    {
        var language = programmingLanguage?.Trim();
        if (string.IsNullOrWhiteSpace(language))
        {
            return null;
        }

        return UnsupportedBlockLanguages.Contains(language!)
            ? $"Programming language '{language}' is not supported by Siemens Openness XML export."
            : null;
    }

    public static string? GetNonExportableFailureReason(string? errorMessage)
    {
        var message = errorMessage?.Trim();
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        return message!.IndexOf("export of block", StringComparison.OrdinalIgnoreCase) >= 0 &&
            message.IndexOf("not permitted", StringComparison.OrdinalIgnoreCase) >= 0
                ? message
                : null;
    }
}
