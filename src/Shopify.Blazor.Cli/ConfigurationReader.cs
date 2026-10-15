using System.Text.Json;
using Shopify.Blazor.Cli.Abstractions;

namespace Shopify.Blazor.Cli;

/// <summary>One setting, resolved value and the layer it came from.</summary>
/// <param name="Value">The resolved value, or <c>null</c>.</param>
/// <param name="Source">Where it came from, for messages, or <c>null</c> when unset.</param>
public sealed record ResolvedSetting(string? Value, string? Source)
{
    /// <summary>Whether a non-empty value was resolved.</summary>
    public bool IsPresent => !string.IsNullOrWhiteSpace(Value);
}

/// <summary>The resolved configuration of one project.</summary>
/// <param name="ApiKey">The resolved <c>Shopify:ApiKey</c>.</param>
/// <param name="ApiSecret">The resolved <c>Shopify:ApiSecret</c>.</param>
/// <param name="AppUrl">The resolved <c>Shopify:AppUrl</c>.</param>
public sealed record ResolvedConfiguration(ResolvedSetting ApiKey, ResolvedSetting ApiSecret, ResolvedSetting AppUrl)
{
    /// <summary>Whether all three settings resolved to non-empty values.</summary>
    public bool IsComplete => ApiKey.IsPresent && ApiSecret.IsPresent && AppUrl.IsPresent;
}

/// <summary>
/// Reads <c>Shopify:*</c> configuration the same way the application does: environment variable
/// first, then <c>appsettings.json</c>.
/// </summary>
/// <remarks>
/// The precedence is not cosmetic. An environment variable silently overrides user secrets and
/// appsettings, so a key belonging to a different Shopify app makes every session token fail as
/// "another app" while the appsettings file looks correct (AGENTS.md #15). Reporting which layer
/// won is the whole point of this type.
/// </remarks>
public static class ConfigurationReader
{
    /// <summary>
    /// The three settings, with the environment-variable name each one maps to. Declared once so
    /// the reader, the messages and the tests cannot disagree about a name.
    /// </summary>
    public static readonly IReadOnlyList<(string Key, string EnvironmentVariable)> Settings =
    [
        ("ApiKey", "SHOPIFY_API_KEY"),
        ("ApiSecret", "SHOPIFY_API_SECRET"),
        ("AppUrl", "SHOPIFY_APP_URL"),
    ];

    /// <summary>
    /// Resolves the three settings for the project in <paramref name="projectDirectory"/>.
    /// </summary>
    public static ResolvedConfiguration Resolve(
        IFileSystem fileSystem,
        IEnvironment environment,
        string projectDirectory)
    {
        var fromFiles = ReadAppSettings(fileSystem, projectDirectory);

        ResolvedSetting Resolve(string key, string environmentVariable)
        {
            var fromEnvironment = environment.GetEnvironmentVariable(environmentVariable);

            if (!string.IsNullOrWhiteSpace(fromEnvironment))
            {
                return new ResolvedSetting(fromEnvironment, $"the environment variable {environmentVariable}");
            }

            return fromFiles.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
                ? new ResolvedSetting(value, "appsettings.json")
                : new ResolvedSetting(null, null);
        }

        return new ResolvedConfiguration(
            Resolve("ApiKey", "SHOPIFY_API_KEY"),
            Resolve("ApiSecret", "SHOPIFY_API_SECRET"),
            Resolve("AppUrl", "SHOPIFY_APP_URL"));
    }

    /// <summary>
    /// Every non-empty <c>Shopify:ApiSecret</c> in a committed settings file, with its file name.
    /// </summary>
    /// <remarks>
    /// This is the check that catches a credential in git. An empty string is a placeholder and is
    /// fine; anything else must live in user secrets or an environment variable. Callers must not
    /// print the value — it is returned so it can be compared, not displayed.
    /// </remarks>
    public static IReadOnlyList<(string File, string Value)> FindCommittedSecrets(
        IFileSystem fileSystem,
        string projectDirectory)
    {
        var found = new List<(string File, string Value)>();

        foreach (var path in SettingsFiles(fileSystem, projectDirectory))
        {
            var secret = ReadSecret(fileSystem.ReadAllText(path));

            if (!string.IsNullOrWhiteSpace(secret))
            {
                found.Add((Path.GetFileName(path), secret));
            }
        }

        return found;
    }

    /// <summary>The <c>appsettings*.json</c> files in the project directory, ordered by name.</summary>
    public static IReadOnlyList<string> SettingsFiles(IFileSystem fileSystem, string projectDirectory) =>
        fileSystem
            .EnumerateFiles(projectDirectory, "appsettings*.json")
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

    private static Dictionary<string, string?> ReadAppSettings(IFileSystem fileSystem, string projectDirectory)
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        // Base file first, so a more specific one read later wins — the same order the host uses.
        foreach (var path in SettingsFiles(fileSystem, projectDirectory))
        {
            foreach (var (key, value) in ReadShopifySection(fileSystem.ReadAllText(path)))
            {
                values[key] = value;
            }
        }

        return values;
    }

    /// <summary>Reads the <c>Shopify</c> section of one appsettings file.</summary>
    private static IEnumerable<(string Key, string? Value)> ReadShopifySection(string json)
    {
        JsonDocument document;

        try
        {
            document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });
        }
        catch (JsonException)
        {
            // A malformed settings file is the application's problem to report, not this reader's:
            // failing a doctor run over a stray comment in JSON would hide the real problems.
            yield break;
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("Shopify", out var section)
                || section.ValueKind != JsonValueKind.Object)
            {
                yield break;
            }

            foreach (var property in section.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.String)
                {
                    yield return (property.Name, property.Value.GetString());
                }
            }
        }
    }

    /// <summary>Reads just the secret, which is the one value that must never be committed.</summary>
    private static string? ReadSecret(string json)
    {
        foreach (var (key, value) in ReadShopifySection(json))
        {
            if (string.Equals(key, "ApiSecret", StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
        }

        return null;
    }
}
