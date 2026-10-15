using Shopify.Blazor.Cli.Abstractions;
using Shopify.Blazor.Cli.Catalogue;

namespace Shopify.Blazor.Cli.Commands;

/// <summary>
/// <c>shopify-blazor add component &lt;Name&gt;</c> — writes a usage snippet for a supported wrapper.
/// </summary>
/// <remarks>
/// The catalogue decides what is supported, and it is generated from the Polaris.Blazor sources.
/// This command therefore never invents a wrapper: an unknown name fails with the closest known
/// names, and a name that exists only in the Shopify element spec is reported as having no wrapper
/// yet, with the script that generates one.
/// </remarks>
public sealed class AddComponentCommand
{
    /// <summary>The options <c>add component</c> accepts.</summary>
    public static readonly IReadOnlyList<OptionSpec> AcceptedOptions =
    [
        new("path", OptionKind.Value),
        new("project", OptionKind.Value),
        new("force", OptionKind.Flag),
        new("dry-run", OptionKind.Flag),
    ];

    private readonly IFileSystem _fileSystem;
    private readonly IEnvironment _environment;
    private readonly IConsole _console;
    private readonly Func<PolarisCatalogue> _catalogueFactory;

    /// <summary>Creates the command.</summary>
    /// <param name="fileSystem">The filesystem the snippet is written through.</param>
    /// <param name="environment">Supplies the current directory used to resolve <c>--path</c>.</param>
    /// <param name="console">Where the report and the snippet go.</param>
    /// <param name="catalogueFactory">
    /// Loads the catalogue. Injected so a test can supply a fixture catalogue instead of depending
    /// on the generated file being present.
    /// </param>
    public AddComponentCommand(
        IFileSystem fileSystem,
        IEnvironment environment,
        IConsole console,
        Func<PolarisCatalogue> catalogueFactory)
    {
        _fileSystem = fileSystem;
        _environment = environment;
        _console = console;
        _catalogueFactory = catalogueFactory;
    }

    /// <summary>Runs <c>add component</c>. Returns the process exit code.</summary>
    public int Run(ParsedArguments arguments, string currentDirectory)
    {
        if (arguments.Positional.Count != 1)
        {
            _console.WriteLine(
                "error: 'add component' takes one component name, for example 'shopify-blazor add component Button'.");
            return ExitCodes.Usage;
        }

        var name = arguments.Positional[0];

        PolarisCatalogue catalogue;

        try
        {
            catalogue = _catalogueFactory();
        }
        catch (CatalogueException ex)
        {
            _console.WriteLine("error: " + ex.Message);
            return ExitCodes.InternalError;
        }

        // --project names where to write; without it the current directory is the root, and
        // --path is resolved against it. Relative paths must not depend on which directory the
        // process happens to have been started in.
        var root = arguments.Value("project") is { Length: > 0 } explicitProject
            ? Path.GetFullPath(explicitProject, currentDirectory)
            : currentDirectory;

        var component = catalogue.Find(name);

        if (component is null)
        {
            return ReportUnknown(name, catalogue, root);
        }

        var snippet = ComponentUsageTemplate.Render(component);
        var destination = ResolveDestination(arguments, root, component);
        var force = arguments.Has("force");

        if (destination is not null && _fileSystem.FileExists(destination) && !force)
        {
            _console.WriteLine($"error: '{destination}' already exists. Nothing was written.");
            _console.WriteLine("       Pass --force to replace it, or choose another --path.");
            return ExitCodes.AlreadyExists;
        }

        if (arguments.Has("dry-run"))
        {
            _console.WriteLine($"dry run: nothing was written. {component.Wrapper} renders <{component.Element}>.");
            _console.WriteLine(destination is null
                ? "  no --path was given, so the snippet below would only be printed."
                : $"  would write {destination}");
            _console.WriteLine();
            _console.WriteLine(snippet);
            return ExitCodes.Success;
        }

        if (destination is not null)
        {
            _fileSystem.WriteAllText(destination, snippet);
            _console.WriteLine($"Created {destination} ({component.Wrapper} -> <{component.Element}>).");
        }
        else
        {
            _console.WriteLine($"{component.Wrapper} renders <{component.Element}>. Paste this into a page:");
            _console.WriteLine();
        }

        if (destination is null || arguments.Has("dry-run"))
        {
            _console.WriteLine(snippet);
        }

        if (component.Parameters.Count > 0)
        {
            _console.WriteLine("Parameters: " + string.Join(", ",
                component.Parameters.Select(parameter => $"{parameter.Name}:{parameter.Type ?? "?"}")));
        }

        return ExitCodes.Success;
    }

    /// <summary>Fails with the closest known names, and says when the element exists but has no wrapper.</summary>
    private int ReportUnknown(string name, PolarisCatalogue catalogue, string currentDirectory)
    {
        var suggestions = catalogue.Suggest(name);
        var elements = PolarisElementsReader.Read(_fileSystem, currentDirectory);

        _console.WriteLine($"error: '{name}' is not in the component catalogue, so no usage can be written for it.");

        if (suggestions.Count > 0)
        {
            _console.WriteLine("       Closest known components: " + string.Join(", ", suggestions));
        }

        if (elements.Contains(name))
        {
            _console.WriteLine();
            _console.WriteLine(
                $"       '{name}' is in {PolarisElementsReader.RelativePath}, so Shopify ships the element — " +
                "but the toolkit has no wrapper for it yet.");
            _console.WriteLine(
                "       Write the wrapper, or generate a first draft with 'python3 tools/generate-component.py' " +
                "(a parallel workstream owns that script).");
            return ExitCodes.Success;
        }

        _console.WriteLine();

        var known = catalogue.Components
            .Select(component => component.Wrapper)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(wrapper => wrapper, StringComparer.Ordinal)
            .ToList();

        _console.WriteLine($"       The catalogue has {known.Count} wrappers:");
        _console.WriteLine("       " + string.Join(", ", known));

        return ExitCodes.Usage;
    }

    /// <summary>
    /// Where the snippet goes: an explicit <c>--path</c> (a <c>.razor</c> file, or a directory), or
    /// <c>null</c> when there is nowhere sensible to put it and printing is enough.
    /// </summary>
    private string? ResolveDestination(ParsedArguments arguments, string currentDirectory, CatalogueComponent component)
    {
        if (arguments.Value("path") is not { Length: > 0 } explicitPath)
        {
            return null;
        }

        var resolved = Path.GetFullPath(explicitPath, currentDirectory);

        if (resolved.EndsWith(".razor", StringComparison.OrdinalIgnoreCase))
        {
            return resolved;
        }

        return Path.Combine(resolved, component.Wrapper + "Usage.razor");
    }
}
