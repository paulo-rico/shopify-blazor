namespace Shopify.Blazor.Cli.Abstractions;

/// <summary>
/// Reads process-level facts: environment variables and the running framework. A seam, so
/// <c>configure --check</c> and <c>doctor</c> can be tested without mutating the test runner's own
/// environment — an environment variable silently overrides user secrets, and a test that sets one
/// leaks into every other test in the process.
/// </summary>
public interface IEnvironment
{
    /// <summary>Reads an environment variable, or <c>null</c> when it is unset.</summary>
    string? GetEnvironmentVariable(string name);

    /// <summary>
    /// A human-readable name for the running framework, for example
    /// <c>.NET 10.0.1</c>. Used by the SDK check; it is a description, not a parsed version.
    /// </summary>
    string FrameworkDescription { get; }

    /// <summary>The current working directory.</summary>
    string CurrentDirectory { get; }
}

/// <summary>The real process environment.</summary>
public sealed class SystemEnvironment : IEnvironment
{
    /// <inheritdoc />
    public string? GetEnvironmentVariable(string name) => Environment.GetEnvironmentVariable(name);

    /// <inheritdoc />
    public string FrameworkDescription => System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;

    /// <inheritdoc />
    public string CurrentDirectory => Directory.GetCurrentDirectory();
}
