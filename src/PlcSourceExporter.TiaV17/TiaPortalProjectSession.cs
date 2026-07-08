using Siemens.Engineering;

namespace PlcSourceExporter.TiaV17;

public sealed class TiaPortalProjectSession : IDisposable
{
    private readonly bool _ownsPortal;

    private TiaPortalProjectSession(TiaPortal tiaPortal, Project project, bool ownsPortal)
    {
        TiaPortal = tiaPortal;
        Project = project;
        _ownsPortal = ownsPortal;
    }

    public TiaPortal TiaPortal { get; }

    public Project Project { get; }

    public static TiaPortalProjectSession OpenVisible(FileInfo projectFile)
    {
        if (projectFile == null)
        {
            throw new ArgumentNullException(nameof(projectFile));
        }

        if (!projectFile.Exists)
        {
            throw new FileNotFoundException("TIA project file was not found.", projectFile.FullName);
        }

        var attached = TryAttachToProject(projectFile);
        if (attached != null)
        {
            return attached;
        }

        var tiaPortal = new TiaPortal(TiaPortalMode.WithUserInterface);
        var project = tiaPortal.Projects.Open(projectFile);
        return new TiaPortalProjectSession(tiaPortal, project, ownsPortal: true);
    }

    public void Dispose()
    {
        if (_ownsPortal)
        {
            TiaPortal.Dispose();
        }
    }

    private static TiaPortalProjectSession? TryAttachToProject(FileInfo projectFile)
    {
        foreach (var process in TiaPortal.GetProcesses())
        {
            try
            {
                var tiaPortal = process.Attach();
                var project = tiaPortal.Projects
                    .Cast<Project>()
                    .FirstOrDefault(openProject => PathsEqual(openProject.Path, projectFile));

                if (project != null)
                {
                    return new TiaPortalProjectSession(tiaPortal, project, ownsPortal: false);
                }

                tiaPortal.Dispose();
            }
            catch
            {
                // Some running TIA instances may deny attach or be a different major version.
            }
        }

        return null;
    }

    private static bool PathsEqual(FileInfo left, FileInfo right)
    {
        return string.Equals(left.FullName.TrimEnd('\\'), right.FullName.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
    }
}
