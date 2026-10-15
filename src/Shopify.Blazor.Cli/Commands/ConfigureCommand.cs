using System.Text.RegularExpressions;
using Shopify.Blazor.Cli.Abstractions;

namespace Shopify.Blazor.Cli.Commands;

/// <summary>
/// <c>shopify-blazor configure</c> — prints the local-configuration steps, or with <c>--check</c>
/// validates that they have been done.
/// </summary>
/// <remarks>
/// Nothing here prints a secret. The check reports which settings resolved and which layer supplied
/// them, never a value, because its output ends up in terminals, CI logs and pasted issue reports.
/// A secret that appears once in a log is a secret that has to be rotated.
/// </remarks>
public sealed partial class ConfigureCommand
{
    /// <summary>The options <c>configure</c> accepts.</summary>
    public static readonly IReadOnlyList<OptionSpec> AcceptedOptions =
    [
        new("check", OptionKind.Flag),
        new("project", OptionKind.Value),
    ];

    /// <summary>The scopes the toolkit's documentation assumes; the Dashboard is authoritative.</summary>
    public const string ExampleScopes = "read_products,write_products";

    private readonly IFileSystem _fileSystem;
    private readonly IEnvironment _environment;
    private readonly IConsole _console;

    /// <summary>Creates the command.</summary>
    public ConfigureCommand(IFileSystem fileSystem, IEnvironment environment, IConsole console)
    {
        _fileSystem = fileSystem;
        _environment = environment;
        _console = console;
    }

    /// <summary>Runs <c>configure</c>. Returns the process exit code.</summary>
    public int Run(ParsedArguments arguments, string currentDirectory)
    {
        var search = new ProjectLocator(_fileSystem).Find(arguments.Value("project"), currentDirectory);

        if (arguments.Has("check"))
        {
            return search.Found
                ? RunCheck(search.Project!)
                : ReportNotInProject(search.Error);
        }

        PrintGuide(search.Project);

        return ExitCodes.Success;
    }

    /// <summary>Prints the exact commands and Dashboard settings.</summary>
    public void PrintGuide(ProjectFile? project)
    {
        _console.WriteLine("Configuring a Shopify Blazor app");
        _console.WriteLine("===============================");
        _console.WriteLine();
        _console.WriteLine("1. Create the app in the Partner Dashboard (https://partners.shopify.com):");
        _console.WriteLine("     Apps  ->  Create app  ->  Create app manually");
        _console.WriteLine("   Then, on the app's page:");
        _console.WriteLine("     - Configuration -> App URL:      https://<your-server-hostname>");
        _console.WriteLine("     - Configuration -> Redirect URL: https://<your-server-hostname>/signin-oidc");
        _console.WriteLine($"     - Configuration -> Access scopes: at minimum {ExampleScopes}");
        _console.WriteLine("     - Overview      -> Client ID and Client secret: copy both.");
        _console.WriteLine("   The app must be served over public HTTPS; the Admin will not frame a localhost URL.");
        _console.WriteLine();
        _console.WriteLine("2. Give the values to the app. An environment variable wins over user secrets, so pick one:");
        _console.WriteLine();
        _console.WriteLine("   User secrets (recommended for local development):");

        foreach (var line in UserSecretsCommands(project))
        {
            _console.WriteLine("     " + line);
        }

        _console.WriteLine();
        _console.WriteLine("   Environment variables (these override user secrets and appsettings):");
        _console.WriteLine("     export SHOPIFY_API_KEY=<client id>");
        _console.WriteLine("     export SHOPIFY_API_SECRET=<client secret>");
        _console.WriteLine("     export SHOPIFY_APP_URL=https://<your-server-hostname>");
        _console.WriteLine();
        _console.WriteLine("   A key from a different Shopify app rejects every session token as \"another app\",");
        _console.WriteLine("   and startup logs which layer won. Check that before debugging anything else.");
        _console.WriteLine();
        _console.WriteLine("3. Verify:");
        _console.WriteLine("     shopify-blazor configure --check");
        _console.WriteLine("     shopify-blazor doctor");
        _console.WriteLine();
        _console.WriteLine("Nothing here is CLI-only: the same values can be set in appsettings.json (except the");
        _console.WriteLine("secret, which must never be committed), in launchSettings.json, or in your shell.");
    }

    /// <summary>Validates that the three settings resolve. Prints no secret value.</summary>
    public int RunCheck(ProjectFile project)
    {
        var configuration = ConfigurationReader.Resolve(_fileSystem, _environment, project.Directory);
        var missing = new List<string>();

        Report("Shopify:ApiKey", configuration.ApiKey, "SHOPIFY_API_KEY", missing);
        Report("Shopify:ApiSecret", configuration.ApiSecret, "SHOPIFY_API_SECRET", missing);
        Report("Shopify:AppUrl", configuration.AppUrl, "SHOPIFY_APP_URL", missing);

        var committed = ConfigurationReader.FindCommittedSecrets(_fileSystem, project.Directory);

        if (committed.Count > 0)
        {
            _console.WriteLine();
            _console.WriteLine(
                "FAIL  a non-empty Shopify:ApiSecret is in a committed file: " +
                string.Join(", ", committed.Select(entry => entry.File)));
            _console.WriteLine("      Rotate it in the Partner Dashboard and keep appsettings.json empty; " +
                "use user secrets or the SHOPIFY_API_SECRET variable.");
        }

        if (missing.Count == 0 && committed.Count == 0)
        {
            _console.WriteLine();
            _console.WriteLine("Configuration is complete. Values are never printed.");
            return ExitCodes.Success;
        }

        if (missing.Count > 0)
        {
            _console.WriteLine();
            _console.WriteLine("Missing: " + string.Join(", ", missing));
            _console.WriteLine("Run 'shopify-blazor configure' for the exact commands.");
        }

        return ExitCodes.Failure;
    }

    private void Report(string key, ResolvedSetting setting, string environmentVariable, List<string> missing)
    {
        if (setting.IsPresent)
        {
            _console.WriteLine($"ok    {key,-20} set ({setting.Source})");
            return;
        }

        _console.WriteLine($"FAIL  {key,-20} not set (checked the {environmentVariable} variable and appsettings*.json)");
        missing.Add(key);
    }

    private int ReportNotInProject(string? error)
    {
        _console.WriteLine("FAIL  project             " + error);
        _console.WriteLine("      Run this inside a project, or use 'shopify-blazor configure' without --check " +
            "for the generic steps.");
        return ExitCodes.Failure;
    }

    /// <summary>
    /// The <c>dotnet user-secrets</c> commands, using the project's own <c>UserSecretsId</c> when it
    /// declares one — a command with the wrong id silently writes to a store the app never reads.
    /// </summary>
    public IReadOnlyList<string> UserSecretsCommands(ProjectFile? project)
    {
        var secretsId = project is null ? null : ReadUserSecretsId(project.FilePath);
        var idSuffix = secretsId is null ? string.Empty : $" --id {secretsId}";

        return
        [
            $"dotnet user-secrets init{idSuffix}",
            $"dotnet user-secrets set \"Shopify:ApiKey\" <client id>{idSuffix}",
            $"dotnet user-secrets set \"Shopify:ApiSecret\" <client secret>{idSuffix}",
            $"dotnet user-secrets set \"Shopify:AppUrl\" https://<your-server-hostname>{idSuffix}",
        ];
    }

    /// <summary>Reads <c>UserSecretsId</c> from the project file, or <c>null</c>.</summary>
    private string? ReadUserSecretsId(string projectFile)
    {
        if (!_fileSystem.FileExists(projectFile))
        {
            return null;
        }

        var match = UserSecretsIdRegex().Match(_fileSystem.ReadAllText(projectFile));

        return match.Success ? match.Groups["value"].Value.Trim() : null;
    }

    [GeneratedRegex("""<UserSecretsId>\s*(?<value>[^<]+?)\s*</UserSecretsId>""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UserSecretsIdRegex();
}
