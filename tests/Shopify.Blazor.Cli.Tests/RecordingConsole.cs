using System.Text;

namespace Shopify.Blazor.Cli.Tests;

/// <summary>
/// An <see cref="IConsole"/> that records everything written.
/// </summary>
/// <remarks>
/// The recorded text is what several tests assert on, and one of them asserts on what is
/// <em>absent</em>: <c>configure --check</c> must never print a secret, and a buffer is the only
/// way to prove it did not.
/// </remarks>
public sealed class RecordingConsole : IConsole
{
    private readonly StringBuilder _buffer = new();

    /// <summary>Everything written so far, lines separated by <c>\n</c>.</summary>
    public string Text => _buffer.ToString();

    /// <summary>Everything written so far, as separate lines.</summary>
    public IReadOnlyList<string> Lines => _buffer.ToString()
        .Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .Select(line => line.TrimEnd('\r'))
        .ToList();

    /// <inheritdoc />
    public void WriteLine(string text) => _buffer.Append(text).Append('\n');

    /// <inheritdoc />
    public void WriteLine() => _buffer.Append('\n');

    /// <inheritdoc />
    public void Write(string text) => _buffer.Append(text);
}
