using Siemens.Engineering;

namespace PlcSourceExporter.TiaV20;

public static class TiaProjectPaths
{
    public static string GetDefaultExportRoot(Project project)
    {
        var projectDirectory = GetProjectDirectory(project);
        return Path.Combine(projectDirectory, "UserFiles", "export");
    }

    public static string ResolveExportRoot(Project project, string? output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return GetDefaultExportRoot(project);
        }

        if (Path.IsPathRooted(output))
        {
            return output!;
        }

        return Path.Combine(GetProjectDirectory(project), output!);
    }

    public static string GetProjectDirectory(Project project)
    {
        if (project == null)
        {
            throw new ArgumentNullException(nameof(project));
        }

        var projectPath = project.Path;
        if (projectPath == null)
        {
            throw new InvalidOperationException("The open TIA project does not expose a project path.");
        }

        return projectPath.DirectoryName ?? throw new InvalidOperationException($"Unable to resolve project directory from '{projectPath.FullName}'.");
    }
}
