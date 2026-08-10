using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace VoidNote.Infrastructure.Logging;

/// <summary>Writes one local structured JSON object per log entry.</summary>
public sealed class JsonFileLoggerProvider : ILoggerProvider
{
    private const long MaximumFileBytes = 5L * 1024 * 1024;
    private static readonly TimeSpan Retention = TimeSpan.FromDays(14);
    private readonly string _filePath;
    private readonly object _writeLock = new();

    /// <summary>Creates a provider for a local log file.</summary>
    public JsonFileLoggerProvider(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
        var directory = Path.GetDirectoryName(filePath)
            ?? throw new ArgumentException("The log path has no parent directory.", nameof(filePath));
        Directory.CreateDirectory(directory);
        RemoveExpiredLogs(directory);
    }

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => new JsonFileLogger(categoryName, Write);

    /// <inheritdoc />
    public void Dispose()
    {
    }

    private void Write(LogRecord record)
    {
        try
        {
            var line = JsonSerializer.Serialize(record) + Environment.NewLine;
            lock (_writeLock)
            {
                RotateIfRequired(line.Length);
                File.AppendAllText(_filePath, line);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"VoidNote file logging failed: {exception.Message}");
        }
    }

    private void RotateIfRequired(int incomingCharacters)
    {
        if (!File.Exists(_filePath) || new FileInfo(_filePath).Length + incomingCharacters * sizeof(char) <= MaximumFileBytes) return;
        var directory = Path.GetDirectoryName(_filePath)!;
        var rotated = Path.Combine(directory, $"{Path.GetFileNameWithoutExtension(_filePath)}-{DateTime.UtcNow:HHmmssfff}{Path.GetExtension(_filePath)}");
        File.Move(_filePath, rotated, false);
    }

    private static void RemoveExpiredLogs(string directory)
    {
        try
        {
            foreach (var path in Directory.EnumerateFiles(directory, "voidnote-*.log"))
                if (DateTime.UtcNow - File.GetLastWriteTimeUtc(path) > Retention) File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"VoidNote log retention cleanup failed: {exception.Message}");
        }
    }

    private sealed class JsonFileLogger(string category, Action<LogRecord> write) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);
            if (!IsEnabled(logLevel)) return;

            write(new LogRecord(
                DateTimeOffset.UtcNow,
                logLevel.ToString(),
                category,
                eventId.Id,
                eventId.Name,
                formatter(state, exception),
                exception?.ToString()));
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();
        public void Dispose() { }
    }

    private sealed record LogRecord(
        DateTimeOffset TimestampUtc,
        string Level,
        string Category,
        int EventId,
        string? EventName,
        string Message,
        string? Exception);
}
