using System.Xml.Linq;

namespace PlcSourceExporter.Core.Tests;

public sealed class PublisherPackageTests
{
    [Fact]
    public void V17PublisherPackageIncludesSqliteRuntimeDependencies()
    {
        var packageConfig = Path.Combine(FindRepositoryRoot(), "package", "PlcSourceExporter.V17.publisher.xml");
        var document = XDocument.Load(packageConfig);
        var assemblyNames = document
            .Descendants()
            .Where(element => element.Name.LocalName == "Assembly")
            .Select(element => Path.GetFileName(element.Value.Trim()))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains("PlcSourceExporter.Core.dll", assemblyNames);
        Assert.Contains("Microsoft.Data.Sqlite.dll", assemblyNames);
        Assert.Contains("SQLitePCLRaw.batteries_v2.dll", assemblyNames);
        Assert.Contains("SQLitePCLRaw.core.dll", assemblyNames);
        Assert.Contains("SQLitePCLRaw.provider.e_sqlite3.dll", assemblyNames);
        Assert.Contains("e_sqlite3.dll", assemblyNames);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "package")) &&
                Directory.Exists(Path.Combine(directory.FullName, "src")) &&
                Directory.Exists(Path.Combine(directory.FullName, "tests")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find PlcSourceExporter repository root.");
    }
}
