using System.Diagnostics;

namespace PlcSourceExporter.Core;

public sealed class InProcessSemanticPlcModelWriter : ISemanticPlcModelWriter
{
    public static InProcessSemanticPlcModelWriter Instance { get; } = new();

    private InProcessSemanticPlcModelWriter()
    {
    }

    public SemanticPlcModelWriteResult Write(string exportRoot)
    {
        return SemanticPlcModelWriter.Write(exportRoot);
    }
}

public sealed class ExternalProcessSemanticPlcModelWriter : ISemanticPlcModelWriter
{
    private readonly string executablePath;
    private readonly TimeSpan timeout;

    public ExternalProcessSemanticPlcModelWriter(string executablePath)
        : this(executablePath, TimeSpan.FromMinutes(20))
    {
    }

    public ExternalProcessSemanticPlcModelWriter(string executablePath, TimeSpan timeout)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new ArgumentException("Semantic model helper executable path is required.", nameof(executablePath));
        }

        this.executablePath = executablePath;
        this.timeout = timeout;
    }

    public SemanticPlcModelWriteResult Write(string exportRoot)
    {
        if (!File.Exists(executablePath))
        {
            throw new FileNotFoundException("Semantic model helper executable was not found.", executablePath);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = $"--export-root {Quote(exportRoot)} --model-only",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(executablePath) ?? string.Empty
        };

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Unable to start semantic model helper: {executablePath}");
        var standardOutputTask = process.StandardOutput.ReadToEndAsync();
        var standardErrorTask = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit((int)timeout.TotalMilliseconds))
        {
            try
            {
                process.Kill();
            }
            catch
            {
            }

            throw new TimeoutException($"Semantic model helper did not finish within {timeout}.");
        }

        var standardOutput = standardOutputTask.GetAwaiter().GetResult();
        var standardError = standardErrorTask.GetAwaiter().GetResult();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Semantic model helper failed with exit code {process.ExitCode}.{Environment.NewLine}{standardOutput}{standardError}");
        }

        return SemanticPlcModelWriter.GetExpectedResult(exportRoot);
    }

    private static string Quote(string value)
    {
        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }
}
