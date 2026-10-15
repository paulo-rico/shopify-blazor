namespace Shopify.Blazor.Cli;

/// <summary>
/// The CLI's console surface. Wrapping it means a command can be asserted on without capturing
/// process stdout, and — more importantly — a test can assert that a secret was <em>not</em>
/// printed.
/// </summary>
public interface IConsole
{
    /// <summary>Writes a line.</summary>
    void WriteLine(string text);

    /// <summary>Writes an empty line.</summary>
    void WriteLine();

    /// <summary>Writes text without a trailing newline.</summary>
    void Write(string text);
}

/// <summary>Writes to the real standard output.</summary>
public sealed class SystemConsole : IConsole
{
    /// <inheritdoc />
    public void WriteLine(string text) => Console.Out.WriteLine(text);

    /// <inheritdoc />
    public void WriteLine() => Console.Out.WriteLine();

    /// <inheritdoc />
    public void Write(string text) => Console.Out.Write(text);
}
