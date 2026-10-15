using Shopify.Blazor.Cli.Abstractions;

namespace Shopify.Blazor.Cli.Commands;

/// <summary>
/// <c>shopify-blazor new &lt;Name&gt;</c> — scaffolds a project from the <c>shopify-blazor</c>
/// template and prints the setup steps.
/// </summary>
/// <remarks>
/// The command is a wrapper over <c>dotnet new shopify-blazor</c> and nothing more. It never writes
/// the project itself, so there is no second scaffold to keep in step with the template, and the
/// output is an ordinary project the developer can edit freely.
/// </remarks>
public sealed class NewCommand
{
    /// <summary>Name of the template this command instantiates.</summary>
    public const string TemplateName = "shopify-blazor";

    /// <summary>The environment variables the generated app reads, in the order the README lists them.</summary>
    public static readonly IReadOnlyList<string> EnvironmentVariables =
    [
        "SHOPIFY_API_KEY",
        "SHOPIFY_API_SECRET",
        "SHOPIFY_APP_URL",
    ];

    /// <summary>The options <c>new</c> accepts.</summary>
    public static readonly IReadOnlyList<OptionSpec> AcceptedOptions =
    [
        new("include-sample-pages", OptionKind.Value),
        new("include-admin-api-example", OptionKind.Value),
        new("output", OptionKind.Value),
        new("dry-run", OptionKind.Flag),
    ];

    private readonly IFileSystem _fileSystem;
    private readonly IProcessRunner _processRunner;
    private readonly IEnvironment _environment;
    private readonly IConsole _console;

    /// <summary>Creates the command.</summary>
    public NewCommand(
        IFileSystem fileSystem,
        IProcessRunner processRunner,
        IEnvironment environment,
        IConsole console)
    {
        _fileSystem = fileSystem;
        _processRunner = processRunner;
        _environment = environment;
        _console = console;
    }

    /// <summary>Runs <c>new</c>. Returns the process exit code.</summary>
    public int Run(ParsedArguments arguments)
    {
        if (arguments.Positional.Count == 0)
        {
            _console.WriteLine("error: 'new' needs a project name, for example 'shopify-blazor new MyShopifyApp'.");
            return ExitCodes.Usage;
        }

        if (arguments.Positional.Count > 1)
        {
            _console.WriteLine(
                $"error: 'new' takes one project name; got {arguments.Positional.Count} " +
                $"({string.Join(", ", arguments.Positional)}).");
            return ExitCodes.Usage;
        }

        var name = arguments.Positional[0];

        if (!NameHelper.IsValidComponentName(name, out var nameProblem))
        {
            _console.WriteLine($"error: {nameProblem}");
            return ExitCodes.Usage;
        }

        if (!TryReadBoolean(arguments, "include-sample-pages", defaultValue: true, out var includeSamplePages)
            || !TryReadBoolean(arguments, "include-admin-api-example", defaultValue: true, out var includeAdminApiExample))
        {
            return ExitCodes.Usage;
        }

        var currentDirectory = _environment.CurrentDirectory;
        var outputDirectory = string.IsNullOrWhiteSpace(arguments.Value("output"))
            ? Path.GetFullPath(name, currentDirectory)
            : Path.GetFullPath(arguments.Value("output")!, currentDirectory);

        var targetDirectory = arguments.Value("output") is null
            ? outputDirectory
            : Path.Combine(outputDirectory, name);

        // Refusing a non-empty directory is the difference between a scaffold and a merge: the
        // template engine would otherwise write into whatever is there and overwrite edited files.
        if (_fileSystem.DirectoryExists(targetDirectory)
            && _fileSystem.EnumerateFileSystemEntries(targetDirectory).Any())
        {
            _console.WriteLine($"error: '{targetDirectory}' already exists and is not empty.");
            _console.WriteLine("       Choose another name or --output, or empty the directory first. " +
                "This command never overwrites an existing project.");
            return ExitCodes.AlreadyExists;
        }

        var templateArguments = new List<string>
        {
            "new",
            TemplateName,
            "--name",
            NameHelper.ToPascalCase(name),
            "--output",
            outputDirectory,
            "--include-sample-pages",
            includeSamplePages ? "true" : "false",
            "--include-admin-api-example",
            includeAdminApiExample ? "true" : "false",
        };

        var commandLine = "dotnet " + string.Join(" ", templateArguments.Select(Quote));

        if (arguments.Has("dry-run"))
        {
            _console.WriteLine("dry run: nothing was written.");
            _console.WriteLine($"  would run, from {currentDirectory}:");
            _console.WriteLine($"    {commandLine}");
            _console.WriteLine($"  which would create the project in {targetDirectory}");
            PrintNextSteps(targetDirectory);
            return ExitCodes.Success;
        }

        _console.WriteLine($"Running: {commandLine}");
        var result = _processRunner.Run("dotnet", templateArguments, currentDirectory);

        if (!result.Succeeded)
        {
            _console.WriteLine();
            _console.WriteLine($"error: 'dotnet new {TemplateName}' failed with exit code {result.ExitCode}.");

            if (result.StandardError.Contains("No templates found", StringComparison.OrdinalIgnoreCase)
                || result.StandardError.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                _console.WriteLine("       The template is not installed. Install the toolkit's template package first:");
                _console.WriteLine("         dotnet new install Shopify.Blazor.Templates");
            }

            return ExitCodes.ProcessFailed;
        }

        PrintNextSteps(targetDirectory);
        return ExitCodes.Success;
    }

    /// <summary>Prints what has to happen before the app will run.</summary>
    private void PrintNextSteps(string projectDirectory)
    {
        _console.WriteLine();
        _console.WriteLine("Next steps:");
        _console.WriteLine($"  1. cd {projectDirectory}");
        _console.WriteLine("  2. Create a Shopify app in the Partner Dashboard and copy its client ID and secret.");
        _console.WriteLine("  3. Give the credentials to the app — either environment variables:");
        _console.WriteLine($"       {EnvironmentVariables[0]}=<client id>");
        _console.WriteLine($"       {EnvironmentVariables[1]}=<client secret>");
        _console.WriteLine($"       {EnvironmentVariables[2]}=https://<your-server-hostname>");
        _console.WriteLine("     or user secrets (see 'shopify-blazor configure'):");
        _console.WriteLine("       dotnet user-secrets set \"Shopify:ApiKey\" <client id>");
        _console.WriteLine("  4. Read the README in the generated project for the deployment and install steps.");
        _console.WriteLine("  5. Run 'shopify-blazor doctor' to check the project, then 'dotnet run'.");
    }

    /// <summary>Reads a <c>bool</c> option, accepting only the spellings .NET's template engine accepts.</summary>
    private bool TryReadBoolean(ParsedArguments arguments, string name, bool defaultValue, out bool value)
    {
        value = defaultValue;

        if (arguments.Value(name) is not { } raw)
        {
            return true;
        }

        if (bool.TryParse(raw, out value))
        {
            return true;
        }

        _console.WriteLine($"error: --{name} takes true or false; got '{raw}'.");
        return false;
    }

    /// <summary>Quotes an argument for display when it contains a space.</summary>
    private static string Quote(string argument) =>
        argument.Contains(' ') ? $"\"{argument}\"" : argument;
}
