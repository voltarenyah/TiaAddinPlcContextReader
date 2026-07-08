using PlcSourceExporter.Core;

try
{
    var options = AnalyzerOptions.Parse(args);

    var model = SemanticPlcModelWriter.Write(options.ExportRoot);
    Console.WriteLine("Semantic PLC model generated.");
    Console.WriteLine($"SQLite: {model.SqliteFilePath}");
    Console.WriteLine($"Schema: {model.SchemaFilePath}");
    Console.WriteLine($"Agent guide: {model.AgentGuideFilePath}");
}
catch (Exception ex)
{
    Console.Error.WriteLine("Export analysis failed.");
    Console.Error.WriteLine(ex);
    Environment.ExitCode = 1;
}

internal sealed class AnalyzerOptions
{
    private AnalyzerOptions(string exportRoot)
    {
        ExportRoot = exportRoot;
    }

    public string ExportRoot { get; }

    public static AnalyzerOptions Parse(string[] args)
    {
        string? exportRoot = null;
        for (var index = 0; index < args.Length; index++)
        {
            var arg = args[index];
            switch (arg)
            {
                case "--export-root":
                    exportRoot = ReadValue(args, ref index, arg);
                    break;
                case "--model-only":
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

        if (string.IsNullOrWhiteSpace(exportRoot))
        {
            PrintUsage();
            throw new ArgumentException("--export-root is required.");
        }

        return new AnalyzerOptions(Path.GetFullPath(exportRoot!));
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
        Console.WriteLine("  PlcSourceExporter.ExportAnalyzer.exe --export-root <path-to-UserFiles\\export>");
    }
}
