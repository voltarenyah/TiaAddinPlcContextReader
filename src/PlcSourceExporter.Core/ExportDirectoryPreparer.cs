namespace PlcSourceExporter.Core;

public static class ExportDirectoryPreparer
{
    public static void Prepare(string exportRoot)
    {
        if (Directory.Exists(exportRoot))
        {
            foreach (var file in Directory.EnumerateFiles(exportRoot))
            {
                if (string.Equals(Path.GetFileName(file), "PlcSourceExporter.log", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                File.Delete(file);
            }

            foreach (var directory in Directory.EnumerateDirectories(exportRoot))
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        Directory.CreateDirectory(exportRoot);
        foreach (var folderName in ExportCategories.All.Select(category => category.FolderName).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            Directory.CreateDirectory(Path.Combine(exportRoot, folderName));
        }
    }
}
