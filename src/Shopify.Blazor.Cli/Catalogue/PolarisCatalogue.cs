using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Shopify.Blazor.Cli.Catalogue;

/// <summary>One parameter of a wrapper component.</summary>
/// <param name="Name">The parameter name, as written in Razor (for example <c>Variant</c>).</param>
/// <param name="Type">The parameter's type name (for example <c>PolarisButtonVariant</c>).</param>
public sealed record CatalogueParameter(string Name, string? Type);

/// <summary>One component in the toolkit's catalogue.</summary>
/// <param name="Wrapper">The wrapper class name, for example <c>PolarisButton</c>.</param>
/// <param name="Element">The custom element it renders, for example <c>s-button</c>.</param>
/// <param name="Namespace">The C# namespace of the wrapper class.</param>
/// <param name="Summary">A one-sentence description.</param>
/// <param name="Parameters">The wrapper's parameters.</param>
/// <param name="Events">The events the wrapper raises.</param>
/// <param name="Slots">The named slots the element accepts.</param>
public sealed record CatalogueComponent(
    string Wrapper,
    string? Element,
    string? Namespace,
    string? Summary,
    IReadOnlyList<CatalogueParameter> Parameters,
    IReadOnlyList<string> Events,
    IReadOnlyList<string> Slots);

/// <summary>
/// The embedded component catalogue: what <c>add component</c> can write usage for.
/// </summary>
/// <remarks>
/// The file is generated from the Polaris.Blazor sources by a script that the CLI does not own, so
/// it is read, never written. When it is absent the loader says exactly what is missing and how to
/// produce it, because "the catalogue is empty" and "the build did not embed the catalogue" look
/// identical from the outside and need different fixes.
/// </remarks>
public sealed class PolarisCatalogue
{
    /// <summary>The logical name the catalogue is embedded under.</summary>
    public const string EmbeddedResourceName = "Shopify.Blazor.Cli.Catalogue.polaris-catalogue.json";

    /// <summary>The path, relative to the repository root, of the file a generator should write.</summary>
    public const string SourcePath = "src/Shopify.Blazor.Cli/Catalogue/polaris-catalogue.json";

    private readonly IReadOnlyList<CatalogueComponent> _components;

    private PolarisCatalogue(string? generatedBy, IReadOnlyList<CatalogueComponent> components)
    {
        GeneratedBy = generatedBy;
        _components = components;
    }

    /// <summary>Name of the script that generated the catalogue, when it declares one.</summary>
    public string? GeneratedBy { get; }

    /// <summary>Every component in the catalogue, in file order.</summary>
    public IReadOnlyList<CatalogueComponent> Components => _components;

    /// <summary>
    /// Finds the component whose wrapper matches <paramref name="name"/>, ignoring case and an
    /// optional <c>Polaris</c> prefix — a developer who types <c>Button</c> means <c>PolarisButton</c>.
    /// </summary>
    public CatalogueComponent? Find(string name)
    {
        var wanted = Normalise(name);

        return _components.FirstOrDefault(component =>
            string.Equals(Normalise(component.Wrapper), wanted, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Up to <paramref name="count"/> known wrapper names closest to <paramref name="name"/>, for a
    /// "did you mean" list. Ordering is by edit distance and then alphabetically, so the output is
    /// stable across runs.
    /// </summary>
    public IReadOnlyList<string> Suggest(string name, int count = 5)
    {
        var wanted = Normalise(name);

        return _components
            .Select(component => (component.Wrapper, Distance: Distance(wanted, Normalise(component.Wrapper))))
            // Distinct by wrapper first. The catalogue holds one entry per element, so a wrapper
            // covering more than one element would appear more than once, and a "did you mean" list
            // that repeats a name looks like a bug. No wrapper does today — s-text and s-paragraph have
            // a class each — but the suggestion contract is "up to N names", not "up to N entries".
            .DistinctBy(candidate => candidate.Wrapper, StringComparer.Ordinal)
            .OrderBy(candidate => candidate.Distance)
            .ThenBy(candidate => candidate.Wrapper, StringComparer.Ordinal)
            .Take(count)
            .Select(candidate => candidate.Wrapper)
            .ToList();
    }

    /// <summary>Adds the <c>Polaris</c> prefix when the caller omitted it, and trims whitespace.</summary>
    private static string Normalise(string name)
    {
        var trimmed = name.Trim();

        return trimmed.StartsWith("Polaris", StringComparison.OrdinalIgnoreCase) ? trimmed : "Polaris" + trimmed;
    }

    /// <summary>Levenshtein distance, used only to rank suggestions.</summary>
    private static int Distance(string left, string right)
    {
        var previous = new int[right.Length + 1];
        var current = new int[right.Length + 1];

        for (var column = 0; column <= right.Length; column++)
        {
            previous[column] = column;
        }

        for (var row = 1; row <= left.Length; row++)
        {
            current[0] = row;

            for (var column = 1; column <= right.Length; column++)
            {
                var substitution = char.ToUpperInvariant(left[row - 1]) == char.ToUpperInvariant(right[column - 1])
                    ? previous[column - 1]
                    : previous[column - 1] + 1;

                current[column] = Math.Min(Math.Min(current[column - 1] + 1, previous[column] + 1), substitution);
            }

            (previous, current) = (current, previous);
        }

        return previous[right.Length];
    }

    /// <summary>
    /// The JSON shape of the catalogue file. Kept next to the public records instead of using
    /// generated serialisation, because the file is generated elsewhere and the property names are
    /// the contract — an unnoticed naming-policy change would silently produce zero components.
    /// </summary>
    private sealed record CatalogueFile(
        [property: JsonPropertyName("generatedBy")] string? GeneratedBy,
        [property: JsonPropertyName("components")] List<CatalogueComponentFile>? Components);

    private sealed record CatalogueComponentFile(
        [property: JsonPropertyName("wrapper")] string? Wrapper,
        [property: JsonPropertyName("element")] string? Element,
        [property: JsonPropertyName("namespace")] string? Namespace,
        [property: JsonPropertyName("summary")] string? Summary,
        [property: JsonPropertyName("parameters")] List<CatalogueParameterFile>? Parameters,
        [property: JsonPropertyName("events")] List<string>? Events,
        [property: JsonPropertyName("slots")] List<string>? Slots);

    private sealed record CatalogueParameterFile(
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("type")] string? Type);

    /// <summary>Parses catalogue JSON.</summary>
    /// <exception cref="CatalogueException">The JSON is not a catalogue this CLI can read.</exception>
    public static PolarisCatalogue Parse(string json)
    {
        CatalogueFile? file;

        try
        {
            file = JsonSerializer.Deserialize<CatalogueFile>(json);
        }
        catch (JsonException ex)
        {
            throw new CatalogueException($"The component catalogue is not valid JSON: {ex.Message}", ex);
        }

        if (file is null)
        {
            throw new CatalogueException("The component catalogue is empty or null.");
        }

        var components = (file.Components ?? [])
            .Where(component => !string.IsNullOrWhiteSpace(component.Wrapper))
            .Select(component => new CatalogueComponent(
                component.Wrapper!,
                component.Element,
                component.Namespace,
                component.Summary,
                (component.Parameters ?? [])
                    .Where(parameter => !string.IsNullOrWhiteSpace(parameter.Name))
                    .Select(parameter => new CatalogueParameter(parameter.Name!, parameter.Type))
                    .ToList(),
                component.Events ?? [],
                component.Slots ?? []))
            .ToList();

        return new PolarisCatalogue(file.GeneratedBy, components);
    }

    /// <summary>
    /// Loads the catalogue embedded in this assembly.
    /// </summary>
    /// <exception cref="CatalogueException">
    /// The resource is absent, which is the expected state until the generator has run.
    /// </exception>
    public static PolarisCatalogue LoadEmbedded()
    {
        var assembly = Assembly.GetExecutingAssembly();

        using var stream = assembly.GetManifestResourceStream(EmbeddedResourceName);

        if (stream is null)
        {
            throw new CatalogueException(
                $"The component catalogue '{EmbeddedResourceName}' is not embedded in this build. " +
                $"It is generated, not hand-written: produce '{SourcePath}' (a parallel workstream owns " +
                "the generator) and rebuild, then 'add component' will know the toolkit's wrappers. " +
                "The file is embedded only when it exists at build time, so a missing file cannot break " +
                "the build — only this command.");
        }

        using var reader = new StreamReader(stream);

        return Parse(reader.ReadToEnd());
    }
}

/// <summary>A problem with the component catalogue: missing, unreadable, or the wrong shape.</summary>
public sealed class CatalogueException : Exception
{
    /// <summary>Creates the exception with a message a developer can act on.</summary>
    public CatalogueException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and an inner cause.</summary>
    public CatalogueException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
