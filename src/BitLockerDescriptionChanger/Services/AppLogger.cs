namespace BitLockerDescriptionChanger.Services;

public sealed class AppLogger
{
    private readonly object _sync = new();
    private readonly string _logFile;

    public AppLogger() : this(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "BitLockerDescriptionChanger",
        "Logs",
        $"{DateTime.UtcNow:yyyyMMdd}.log"))
    {
    }

    public AppLogger(string logFile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logFile);
        _logFile = Path.GetFullPath(logFile);
        Directory.CreateDirectory(Path.GetDirectoryName(_logFile)
            ?? throw new ArgumentException("The log file must have a parent directory.", nameof(logFile)));
    }

    public string LogFile => _logFile;

    public void Information(string message) => Write("INFO", message);

    public void Warning(string message) => Write("WARN", message);

    public void Error(string message) => Write("ERROR", message);

    private void Write(string level, string message)
    {
        var sanitized = message.Replace('\r', ' ').Replace('\n', ' ');
        try
        {
            lock (_sync)
            {
                File.AppendAllText(_logFile, $"{DateTime.UtcNow:O} [{level}] {sanitized}{Environment.NewLine}");
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Logging is deliberately non-fatal so it cannot alter a metadata transaction outcome.
        }
    }
}
