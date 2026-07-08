using System.Text;

namespace PlcSourceExporter.Core;

public sealed class ExportPathPlanner
{
    private readonly string _exportRoot;
    private readonly Dictionary<string, int> _pathCounts = new(StringComparer.OrdinalIgnoreCase);

    public ExportPathPlanner(string exportRoot)
    {
        _exportRoot = exportRoot ?? throw new ArgumentNullException(nameof(exportRoot));
    }

    public string NextFilePath(ExportCategory category, string objectName)
    {
        var folder = Path.Combine(_exportRoot, ExportCategories.GetFolderName(category));
        var safeName = FileNameSanitizer.Sanitize(objectName);
        var key = Path.Combine(folder, safeName);
        _pathCounts.TryGetValue(key, out var count);
        count++;
        _pathCounts[key] = count;

        var fileName = count == 1 ? $"{safeName}.xml" : $"{safeName}_{count}.xml";
        return Path.Combine(folder, fileName);
    }
}

public static class FileNameSanitizer
{
    private static readonly HashSet<char> InvalidCharacters = new(
        Path.GetInvalidFileNameChars().Concat(new[] { '<', '>', ':', '"', '/', '\\', '|', '?', '*' }));

    public static string Sanitize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Unnamed";
        }

        var builder = new StringBuilder(value.Length);
        foreach (var character in value.Trim())
        {
            builder.Append(InvalidCharacters.Contains(character) || char.IsControl(character) ? '_' : character);
        }

        var sanitized = builder.ToString().Trim('.', ' ');
        return sanitized.Length == 0 ? "Unnamed" : sanitized;
    }
}
