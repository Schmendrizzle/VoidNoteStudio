using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace VoidNote.Infrastructure.Logging;

/// <summary>Writes one local structured JSON object per log entry.</summary>
public sealed class JsonFileLoggerProvider : ILoggerProvider
{
    private readonly string _filePath;
    private readonly object _writeLock = new();

    /// <summary>Creates a provider for a local log file.</summary>
    public JsonFileLoggerProvider(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)
            ?? throw new ArgumentException("The log path has no parent directory.", nameof(filePath)));
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
                File.AppendAllText(_filePath, line);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"VoidNote file logging failed: {exception.Message}");
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
