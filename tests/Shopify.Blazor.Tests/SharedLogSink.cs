using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Shopify.Blazor.Tests;

/// <summary>Captures log output so a test can assert on what the toolkit reported.</summary>
public sealed class SharedLogSink : ILoggerProvider
{
    /// <summary>Every message written.</summary>
    public ConcurrentQueue<string> Messages { get; } = new();

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => new Sink(Messages);

    /// <inheritdoc />
    public void Dispose()
    {
    }

    private sealed class Sink(ConcurrentQueue<string> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => messages.Enqueue(formatter(state, exception));
    }
}
