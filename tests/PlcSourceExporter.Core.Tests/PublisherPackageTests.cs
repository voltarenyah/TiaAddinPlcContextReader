using System.Xml.Linq;
using System.Reflection;
using System.Security;

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
        Assert.Contains("System.Buffers.dll", assemblyNames);
        Assert.Contains("System.Memory.dll", assemblyNames);
        Assert.Contains("System.Numerics.Vectors.dll", assemblyNames);
        Assert.Contains("System.Runtime.CompilerServices.Unsafe.dll", assemblyNames);
        Assert.DoesNotContain("e_sqlite3.dll", assemblyNames);
        Assert.DoesNotContain("SQLitePCLRaw.provider.dynamic_cdecl.dll", assemblyNames);
    }

    [Fact]
    public void CoreAssemblyEmbedsNativeSqliteRuntimeForAddInPackaging()
    {
        var resourceNames = typeof(SqliteSemanticGraphStore).Assembly.GetManifestResourceNames();

        Assert.Contains(resourceNames, name => name.EndsWith("e_sqlite3.dll", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void V17PublisherPackageDoesNotRequireEnvironmentAccessForNativeSqliteExtraction()
    {
        var packageConfig = Path.Combine(FindRepositoryRoot(), "package", "PlcSourceExporter.V17.publisher.xml");
        var document = XDocument.Load(packageConfig);

        Assert.DoesNotContain(
            document.Descendants(),
            element => element.Name.LocalName == "System.Security.Permissions.EnvironmentPermission");
    }

    [Fact]
    public void V17PublisherPackageStartsSemanticModelHelperInsteadOfCallingNativeSqliteInProcess()
    {
        var packageConfig = Path.Combine(FindRepositoryRoot(), "package", "PlcSourceExporter.V17.publisher.xml");
        var document = XDocument.Load(packageConfig);

        Assert.Contains(
            document.Descendants(),
            element => element.Name.LocalName == "Siemens.Engineering.AddIn.Permissions.ProcessStartPermission");
        Assert.DoesNotContain(
            document.Descendants(),
            element => element.Name.LocalName == "System.Security.Permissions.SecurityPermission.UnmanagedCode");
    }

    [Fact]
    public void NativeSqliteLoaderUsesSafeCriticalWrapperForTiaPartialTrust()
    {
        var nativeRuntimeType = typeof(SqliteSemanticGraphStore).Assembly.GetType("PlcSourceExporter.Core.NativeSqliteRuntime")
            ?? throw new InvalidOperationException("Native SQLite runtime helper was not found.");
        var loadNativeRuntime = nativeRuntimeType.GetMethod("LoadNativeRuntime", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Native SQLite load wrapper was not found.");
        var loadLibrary = nativeRuntimeType.GetMethod("LoadLibrary", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Native SQLite LoadLibrary import was not found.");

        Assert.True(loadNativeRuntime.IsDefined(typeof(SecuritySafeCriticalAttribute), inherit: false));
        Assert.True(loadLibrary.IsDefined(typeof(SecurityCriticalAttribute), inherit: false));
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
