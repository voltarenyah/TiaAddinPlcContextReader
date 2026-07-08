using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace PlcSourceExporter.Core;

public static class ProgramBlockComponentCatalog
{
    public static IReadOnlyList<ProgramBlockComponent> LoadExportedProgramBlocks(string exportRoot)
    {
        if (string.IsNullOrWhiteSpace(exportRoot))
        {
            throw new ArgumentException("Export root is required.", nameof(exportRoot));
        }

        var metadataPath = Path.Combine(exportRoot, ExportMetadataWriter.MetadataFileName);
        if (!File.Exists(metadataPath))
        {
            throw new FileNotFoundException($"{ExportMetadataWriter.MetadataFileName} was not found.", metadataPath);
        }

        using var stream = File.OpenRead(metadataPath);
        var serializer = new DataContractJsonSerializer(typeof(ComponentMetadataDocumentDto));
        var document = (ComponentMetadataDocumentDto?)serializer.ReadObject(stream);
        return (document?.Components ?? new List<ComponentMetadataRecordDto>())
            .Where(component =>
                string.Equals(component.Status, "Exported", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(component.ExportedFile) &&
                IsProgramBlockCategory(component.Category))
            .Select(component => new ProgramBlockComponent(
                component.Name ?? string.Empty,
                component.Category ?? string.Empty,
                component.SourcePath ?? string.Empty,
                component.ExportedFile ?? string.Empty))
            .OrderBy(component => GetCategoryOrder(component.Category))
            .ThenBy(component => component.SourcePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool IsProgramBlockCategory(string? category)
    {
        return string.Equals(category, "OB", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(category, "FC", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(category, "FB", StringComparison.OrdinalIgnoreCase);
    }

    private static int GetCategoryOrder(string category)
    {
        if (string.Equals(category, "OB", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (string.Equals(category, "FC", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        return 2;
    }
}

[DataContract]
public sealed class ComponentMetadataDocumentDto
{
    [DataMember(Name = "components")]
    public List<ComponentMetadataRecordDto>? Components { get; set; }
}

[DataContract]
public sealed class ComponentMetadataRecordDto
{
    [DataMember(Name = "name")]
    public string? Name { get; set; }

    [DataMember(Name = "sourcePath")]
    public string? SourcePath { get; set; }

    [DataMember(Name = "category")]
    public string? Category { get; set; }

    [DataMember(Name = "status")]
    public string? Status { get; set; }

    [DataMember(Name = "exportedFile")]
    public string? ExportedFile { get; set; }
}
