namespace PlcSourceExporter.Core;

public sealed class FileExportLogger : IExportLogger
{
    private readonly string _logFilePath;

    public FileExportLogger(string logFilePath)
    {
        _logFilePath = logFilePath ?? throw new ArgumentNullException(nameof(logFilePath));
        Directory.CreateDirectory(Path.GetDirectoryName(_logFilePath)!);
    }

    public void Info(string message)
    {
        Write("INFO", message);
    }

    public void Warning(string message)
    {
        Write("WARN", message);
    }

    public void Error(string message)
    {
        Write("ERROR", message);
    }

    private void Write(string level, string message)
    {
        File.AppendAllText(_logFilePath, $"{DateTimeOffset.Now:u} [{level}] {message}{Environment.NewLine}");
    }
}
