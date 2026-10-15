using System.Text.Json;

namespace Shopify.Blazor.Cli;

/// <summary>The Shopify-side element spec, when a parallel workstream has written it.</summary>
/// <param name="Exists">Whether the file was present.</param>
/// <param name="ElementNames">Every string that looks like an element or component name in it.</param>
public sealed record PolarisElements(bool Exists, IReadOnlyList<string> ElementNames)
{
    /// <summary>Whether <paramref name="name"/> appears in the spec.</summary>
    /// <remarks>
    /// Names are compared after stripping the <c>Polaris</c>/<c>s-</c> decoration and the separators,
    /// so <c>FancyChart</c>, <c>fancy-chart</c> and <c>s-fancy-chart</c> all match. A literal
    /// comparison would miss the very case this exists for: a developer typing the wrapper name they
    /// wish existed.
    /// </remarks>
    public bool Contains(string name) =>
        ElementNames.Any(element => Normalise(element) == Normalise(name));

    private static string Normalise(string name) =>
        name
            .Replace("Polaris", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("s-", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();
}

/// <summary>
/// Reads <c>tools/polaris-elements.json</c>, the Shopify-side element spec.
/// </summary>
/// <remarks>
/// Optional by design: a parallel workstream owns this file and its exact schema is not fixed yet,
/// so this reader collects the strings under the keys a component spec plausibly uses rather than
/// binding to a shape that may change. When the schema settles, this is the one place to tighten.
/// A wrong or missing file can only cost a better error message — the catalogue is what decides
/// whether a component can be added.
/// </remarks>
public static class PolarisElementsReader
{
    /// <summary>The repository-relative path of the file, also used in messages.</summary>
    public const string RelativePath = "tools/polaris-elements.json";

    /// <summary>Keys whose string values are taken to be element or component names.</summary>
    private static readonly string[] NameKeys =
        ["tag", "tagName", "tag_name", "element", "name", "title", "component", "className"];

    /// <summary>
    /// Reads the spec from <paramref name="startDirectory"/> and up to eight parent directories.
    /// </summary>
    public static PolarisElements Read(Abstractions.IFileSystem fileSystem, string startDirectory)
    {
        var path = Find(fileSystem, startDirectory);

        if (path is null)
        {
            return new PolarisElements(false, []);
        }

        try
        {
            using var document = JsonDocument.Parse(fileSystem.ReadAllText(path), new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });

            var names = new List<string>();
            Collect(document.RootElement, names);

            return new PolarisElements(true, names);
        }
        catch (JsonException)
        {
            return new PolarisElements(true, []);
        }
    }

    /// <summary>The nearest <c>tools/polaris-elements.json</c>, or <c>null</c>.</summary>
    public static string? Find(Abstractions.IFileSystem fileSystem, string startDirectory)
    {
        var current = startDirectory;

        for (var depth = 0; depth < 8 && !string.IsNullOrEmpty(current); depth++)
        {
            var candidate = Path.Combine(current, "tools", "polaris-elements.json");

            if (fileSystem.FileExists(candidate))
            {
                return candidate;
            }

            current = Path.GetDirectoryName(current);
        }

        return null;
    }

    private static void Collect(JsonElement element, List<string> names)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (property.Value.ValueKind == JsonValueKind.String
                        && NameKeys.Contains(property.Name, StringComparer.OrdinalIgnoreCase)
                        && property.Value.GetString() is { Length: > 0 } value)
                    {
                        names.Add(value);
                    }
                    else
                    {
                        Collect(property.Value, names);
                    }
                }

                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    Collect(item, names);
                }

                break;
        }
    }
}
