using Shopify.Blazor.Cli.Abstractions;

namespace Shopify.Blazor.Cli.Commands;

/// <summary>
/// <c>shopify-blazor add page &lt;Name&gt;</c> — writes a page with the three states already
/// structured, optionally adding a navigation entry.
/// </summary>
/// <remarks>
/// The command reports every path before it writes it, refuses to replace an existing file without
/// <c>--force</c>, and never deletes anything. <c>--dry-run</c> runs the whole decision path —
/// including the refuses-to-overwrite check — and then writes nothing, so a dry run is a real
/// rehearsal rather than a different code path.
/// </remarks>
public sealed class AddPageCommand
{
    /// <summary>The options <c>add page</c> accepts.</summary>
    public static readonly IReadOnlyList<OptionSpec> AcceptedOptions =
    [
        new("path", OptionKind.Value),
        new("project", OptionKind.Value),
        new("nav", OptionKind.Flag),
        new("force", OptionKind.Flag),
        new("dry-run", OptionKind.Flag),
    ];

    private readonly IFileSystem _fileSystem;
    private readonly IEnvironment _environment;
    private readonly IConsole _console;

    /// <summary>Creates the command.</summary>
    public AddPageCommand(IFileSystem fileSystem, IEnvironment environment, IConsole console)
    {
        _fileSystem = fileSystem;
        _environment = environment;
        _console = console;
    }

    /// <summary>Runs <c>add page</c>. Returns the process exit code.</summary>
    public int Run(ParsedArguments arguments, string currentDirectory)
    {
        if (arguments.Positional.Count != 1)
        {
            _console.WriteLine("error: 'add page' takes one page name, for example 'shopify-blazor add page Products'.");
            return ExitCodes.Usage;
        }

        var name = arguments.Positional[0];

        if (!NameHelper.IsValidComponentName(name, out var nameProblem))
        {
            _console.WriteLine($"error: {nameProblem}");
            return ExitCodes.Usage;
        }

        var search = new ProjectLocator(_fileSystem).Find(arguments.Value("project"), currentDirectory);

        if (!search.Found)
        {
            // A --path that is not on disk cannot be the project either, so fall back to it as the
            // search origin: a developer scaffolding into a brand-new directory has no project yet.
            search = new ProjectLocator(_fileSystem).Find(arguments.Value("path"), currentDirectory);
        }

        if (!search.Found)
        {
            _console.WriteLine("error: " + search.Error);
            return ExitCodes.Usage;
        }

        var project = search.Project!;
        var className = PageTemplate.ClassName(name);
        var route = PageTemplate.Route(name);

        var directory = arguments.Value("path") is { Length: > 0 } explicitPath
            ? Path.GetFullPath(explicitPath, currentDirectory)
            : Path.Combine(project.Directory, "Components", "Pages");

        var filePath = Path.Combine(directory, className + ".razor");
        var force = arguments.Has("force");
        var dryRun = arguments.Has("dry-run");

        // A dry run is a real rehearsal: it makes the same refusal decision, so a run that would be
        // refused is refused here too rather than looking clean and then failing for real.
        if (_fileSystem.FileExists(filePath) && !force)
        {
            _console.WriteLine($"error: '{filePath}' already exists. Nothing was written.");
            _console.WriteLine("       Pass --force to replace it, or choose another name. " +
                "This command never overwrites a file it was not told to replace.");
            return ExitCodes.AlreadyExists;
        }

        if (dryRun)
        {
            _console.WriteLine("dry run: nothing was written.");
            _console.WriteLine($"  would {(force ? "replace" : "create")} {filePath}");
            _console.WriteLine($"    route: {route}");
            _console.WriteLine($"    directive: {PageTemplate.RenderModeDirective}");
            _console.WriteLine("    states: loading (PolarisSpinner), empty (PolarisEmptyState), " +
                "error (PolarisBanner), content");
        }
        else
        {
            _fileSystem.WriteAllText(filePath, PageTemplate.Render(name));
            _console.WriteLine($"Created {filePath}");
            _console.WriteLine($"  route:     {route}");
            _console.WriteLine($"  render:    {PageTemplate.RenderModeDirective}");
        }

        if (arguments.Has("nav"))
        {
            if (dryRun)
            {
                _console.WriteLine("  would also add a navigation entry with --nav.");
            }
            else
            {
                var insertion = NavMenuInserter.Insert(_fileSystem, project.Directory, className, route);
                _console.WriteLine("  nav:       " + insertion.Message);
            }
        }
        else
        {
            _console.WriteLine($"  nav:       not added; pass --nav to insert an entry for {route} automatically.");
        }

        if (dryRun)
        {
            _console.WriteLine();
            _console.WriteLine("Re-run without --dry-run to write it.");
        }

        return ExitCodes.Success;
    }
}
