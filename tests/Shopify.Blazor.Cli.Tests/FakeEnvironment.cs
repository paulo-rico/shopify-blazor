using Shopify.Blazor.Cli.Abstractions;

namespace Shopify.Blazor.Cli.Tests;

/// <summary>An environment whose variables and framework description a test controls.</summary>
/// <remarks>
/// A test that sets a real environment variable leaks into every other test in the process, and
/// AGENTS.md #15 is precisely about an environment variable silently winning over configuration —
/// so the resolution order has to be testable without touching the real one.
/// </remarks>
public sealed class FakeEnvironment : IEnvironment
{
    private readonly Dictionary<string, string> _variables = new(StringComparer.Ordinal);

    /// <summary>Creates a fake environment rooted at <paramref name="currentDirectory"/>.</summary>
    public FakeEnvironment(string currentDirectory, string frameworkDescription = ".NET 10.0.1")
    {
        CurrentDirectory = currentDirectory;
        FrameworkDescription = frameworkDescription;
    }

    /// <inheritdoc />
    public string FrameworkDescription { get; set; }

    /// <inheritdoc />
    public string CurrentDirectory { get; set; }

    /// <summary>Sets an environment variable.</summary>
    public FakeEnvironment With(string name, string value)
    {
        _variables[name] = value;
        return this;
    }

    /// <inheritdoc />
    public string? GetEnvironmentVariable(string name) =>
        _variables.TryGetValue(name, out var value) ? value : null;
}
