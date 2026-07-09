using PlcSourceExporter.Core;
using Siemens.Engineering.AddIn.Utilities;

namespace PlcSourceExporter.AddInShared;

internal sealed class AddInSemanticPlcModelWriter : ISemanticPlcModelWriter
{
    private readonly string executablePath;
    private readonly TimeSpan timeout;

    public AddInSemanticPlcModelWriter(string executablePath)
        : this(executablePath, TimeSpan.FromMinutes(20))
    {
    }

    public AddInSemanticPlcModelWriter(string executablePath, TimeSpan timeout)
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

        var process = Process.Start(executablePath, $"--export-root {Quote(exportRoot)} --model-only");
        if (process == null)
        {
            throw new InvalidOperationException($"Unable to start semantic model helper: {executablePath}");
        }

        try
        {
            if (!process.WaitForExit((int)timeout.TotalMilliseconds))
            {
                TryKill(process);
                throw new TimeoutException($"Semantic model helper did not finish within {timeout}.");
            }

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"Semantic model helper failed with exit code {process.ExitCode}.");
            }

            return SemanticPlcModelWriter.GetExpectedResult(exportRoot);
        }
        finally
        {
            process.Close();
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill();
        }
        catch
        {
        }
    }

    private static string Quote(string value)
    {
        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }
}
