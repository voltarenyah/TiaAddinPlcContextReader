using PlcSourceExporter.Core;
using PlcSourceExporter.TiaV17;

try
{
    var options = HarnessOptions.Parse(args);
    if (!string.Equals(options.TiaVersion, "V17", StringComparison.OrdinalIgnoreCase))
    {
        throw new ArgumentException("Only --tia-version V17 is supported by this harness build.");
    }

    using var session = TiaPortalProjectSession.OpenVisible(new FileInfo(options.ProjectPath));
    var plc = TiaPlcResolver.SelectPlc(session.Project, options.PlcName);
    var exportRoot = TiaProjectPaths.ResolveExportRoot(session.Project, options.Output);
    var logger = new ConsoleAndFileLogger(Path.Combine(exportRoot, "PlcSourceExporter.log"));

    logger.Info($"Project: {session.Project.Name}");
    logger.Info($"PLC: {plc.DisplayName}");
    logger.Info($"Output: {exportRoot}");

    var summary = new PlcExportService().Export(new TiaPlcSoftwareSource(plc.PlcSoftware, logger), exportRoot, logger);
    PrintSummary(summary, exportRoot);

    Environment.ExitCode = summary.FailureCount == 0 ? 0 : 2;
}
catch (Exception ex)
{
    Console.Error.WriteLine("PLC export failed.");
    Console.Error.WriteLine(ex.Message);
    Environment.ExitCode = 1;
}

static void PrintSummary(ExportSummary summary, string exportRoot)
{
    Console.WriteLine();
    Console.WriteLine("Export summary");
    Console.WriteLine($"Output: {exportRoot}");
    foreach (var line in summary.ToDisplayString())
    {
        Console.WriteLine(line);
    }

    if (summary.FailureCount > 0)
    {
        Console.WriteLine();
        Console.WriteLine("Failures");
        foreach (var record in summary.Records.Where(record => record.Status == ExportRecordStatus.Failed))
        {
            Console.WriteLine($"{ExportCategories.GetDisplayName(record.Category)} {record.ObjectName}: {record.Message}");
        }
    }
}

internal sealed class HarnessOptions
{
    public HarnessOptions(string tiaVersion, string projectPath, string? plcName, string? output)
    {
        TiaVersion = tiaVersion;
        ProjectPath = projectPath;
        PlcName = plcName;
        Output = output;
    }

    public string TiaVersion { get; }

    public string ProjectPath { get; }

    public string? PlcName { get; }

    public string? Output { get; }

    public static HarnessOptions Parse(string[] args)
    {
        string tiaVersion = "V17";
        string? projectPath = null;
        string? plcName = null;
        string? output = Path.Combine("UserFiles", "export");

        for (var index = 0; index < args.Length; index++)
        {
            var arg = args[index];
            switch (arg)
            {
                case "--tia-version":
                    tiaVersion = ReadValue(args, ref index, arg);
                    break;
                case "--project":
                    projectPath = ReadValue(args, ref index, arg);
                    break;
                case "--plc-name":
                    plcName = ReadValue(args, ref index, arg);
                    break;
                case "--output":
                    output = ReadValue(args, ref index, arg);
                    break;
                case "--help":
                case "-h":
                    PrintUsage();
                    Environment.Exit(0);
                    break;
                default:
                    throw new ArgumentException($"Unknown argument '{arg}'.");
            }
        }

        if (string.IsNullOrWhiteSpace(projectPath))
        {
            PrintUsage();
            throw new ArgumentException("--project is required.");
        }

        return new HarnessOptions(tiaVersion, projectPath!, plcName, output);
    }

    private static string ReadValue(string[] args, ref int index, string option)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"{option} requires a value.");
        }

        index++;
        return args[index];
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  PlcSourceExporter.TestHarness.V17.exe --tia-version V17 --project <path-to-.ap17> [--plc-name <name>] [--output UserFiles\\export]");
    }
}

internal sealed class ConsoleAndFileLogger : IExportLogger
{
    private readonly FileExportLogger _fileLogger;

    public ConsoleAndFileLogger(string logFilePath)
    {
        _fileLogger = new FileExportLogger(logFilePath);
    }

    public void Info(string message)
    {
        Console.WriteLine(message);
        _fileLogger.Info(message);
    }

    public void Warning(string message)
    {
        Console.WriteLine(message);
        _fileLogger.Warning(message);
    }

    public void Error(string message)
    {
        Console.Error.WriteLine(message);
        _fileLogger.Error(message);
    }
}
