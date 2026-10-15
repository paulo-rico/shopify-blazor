using System.Text.Json;
using System.Text.Json.Serialization;
using Shopify.Blazor.Cli.Abstractions;

namespace Shopify.Blazor.Cli.Commands;

/// <summary>One check as it appears in <c>doctor --json</c>.</summary>
/// <param name="Name">The check name.</param>
/// <param name="Status">PASS, WARN or FAIL.</param>
/// <param name="Reason">One line saying what was found.</param>
/// <param name="Hint">What to do about it, or <c>null</c> when it passed.</param>
public sealed record DoctorCheckJson(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("reason")] string Reason,
    [property: JsonPropertyName("hint")] string? Hint);

/// <summary>The whole <c>doctor --json</c> document.</summary>
/// <param name="Project">The project that was checked.</param>
/// <param name="Healthy">Whether anything FAILed.</param>
/// <param name="Checks">Every check, in the order it ran.</param>
public sealed record DoctorReportJson(
    [property: JsonPropertyName("project")] string Project,
    [property: JsonPropertyName("healthy")] bool Healthy,
    [property: JsonPropertyName("checks")] IReadOnlyList<DoctorCheckJson> Checks);

/// <summary>Source-generated JSON for the doctor report.</summary>
/// <remarks>
/// The naming policy is declared, not assumed. Without it a source-generated context ignores the
/// policy entirely and the property names come out in whatever case the record declares — the trap
/// AGENTS.md #9 records for Shopify's own payloads, and the same one here: CI would parse
/// <c>Name</c> and quietly find nothing.
/// </remarks>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(DoctorReportJson))]
internal sealed partial class DoctorJsonContext : JsonSerializerContext;

/// <summary>
/// Runs the checks from docs/templates-plan.md and reports them.
/// </summary>
/// <remarks>
/// Exit code 0 when nothing FAILs, so <c>doctor</c> can gate CI. WARN never changes the exit code:
/// a warning means "cannot decide" or "worth a look", and a check that cannot decide must not fail
/// a build.
/// </remarks>
public sealed class DoctorCommand
{
    /// <summary>The options <c>doctor</c> accepts.</summary>
    public static readonly IReadOnlyList<OptionSpec> AcceptedOptions =
    [
        new("project", OptionKind.Value),
        new("json", OptionKind.Flag),
    ];

    private readonly IFileSystem _fileSystem;
    private readonly IEnvironment _environment;
    private readonly IConsole _console;

    /// <summary>Creates the command.</summary>
    public DoctorCommand(IFileSystem fileSystem, IEnvironment environment, IConsole console)
    {
        _fileSystem = fileSystem;
        _environment = environment;
        _console = console;
    }

    /// <summary>Runs the checks and prints the report. Returns the process exit code.</summary>
    public int Run(ParsedArguments arguments)
    {
        var search = new ProjectLocator(_fileSystem)
            .Find(arguments.Value("project"), _environment.CurrentDirectory);

        if (!search.Found)
        {
            _console.WriteLine("FAIL  project      " + search.Error);

            if (arguments.Has("json"))
            {
                EmitJson("<none>", [DoctorCheck.Fail("project", search.Error ?? "No project found.", null)]);
            }

            return ExitCodes.Failure;
        }

        var project = search.Project!;
        var checks = RunChecks(project);

        if (arguments.Has("json"))
        {
            EmitJson(project.FilePath, checks);
        }
        else
        {
            EmitHuman(project, checks);
        }

        return checks.Any(check => check.Status == CheckStatus.Fail) ? ExitCodes.Failure : ExitCodes.Success;
    }

    /// <summary>Runs every check against <paramref name="project"/>, in a fixed order.</summary>
    public IReadOnlyList<DoctorCheck> RunChecks(ProjectFile project) =>
    [
        CheckSdkVersion(project),
        CheckReferences(project),
        CheckConfiguration(project),
        CheckAdminApiVersion(project),
        CheckRenderMode(project),
        CheckStaticAsset(project),
    ];

    private DoctorCheck CheckSdkVersion(ProjectFile project)
    {
        var globalJsonPath = FindGlobalJson(project.Directory);
        var globalJsonText = globalJsonPath is not null ? _fileSystem.ReadAllText(globalJsonPath) : null;

        var result = SdkVersionCheck.Evaluate(globalJsonPath, globalJsonText, _environment.FrameworkDescription);

        return result.Outcome switch
        {
            SdkCheckOutcome.Satisfied => DoctorCheck.Pass("dotnet-sdk", result.Reason),
            SdkCheckOutcome.NotSatisfied => DoctorCheck.Fail("dotnet-sdk", result.Reason, result.Hint),
            _ => DoctorCheck.Warn("dotnet-sdk", result.Reason, result.Hint),
        };
    }

    private DoctorCheck CheckReferences(ProjectFile project)
    {
        var references = ProjectFileReader.ReadReferences(_fileSystem, project.FilePath);
        var missing = new List<string>();

        if (!references.References("Shopify.Blazor"))
        {
            missing.Add("Shopify.Blazor");
        }

        if (!references.References("Polaris.Blazor"))
        {
            missing.Add("Polaris.Blazor");
        }

        return missing.Count == 0
            ? DoctorCheck.Pass("references", "the project references Shopify.Blazor and Polaris.Blazor.")
            : DoctorCheck.Fail(
                "references",
                $"{project.FileName} does not reference {string.Join(" and ", missing)}.",
                "Without Polaris.Blazor none of the <Polaris…> components resolve; without Shopify.Blazor there " +
                "is no session-token validation, App Bridge interop or Admin API client. Add the missing " +
                "references (in this repository the template does it via ProjectReference).");
    }

    private DoctorCheck CheckConfiguration(ProjectFile project)
    {
        var configuration = ConfigurationReader.Resolve(_fileSystem, _environment, project.Directory);
        var missing = new List<string>();

        if (!configuration.ApiKey.IsPresent)
        {
            missing.Add("Shopify:ApiKey (SHOPIFY_API_KEY)");
        }

        if (!configuration.ApiSecret.IsPresent)
        {
            missing.Add("Shopify:ApiSecret (SHOPIFY_API_SECRET)");
        }

        if (!configuration.AppUrl.IsPresent)
        {
            missing.Add("Shopify:AppUrl (SHOPIFY_APP_URL)");
        }

        // The committed-secret check comes first when it fires: a credential in git is worse than
        // a missing one, and it needs a different action.
        var committed = ConfigurationReader.FindCommittedSecrets(_fileSystem, project.Directory);

        if (committed.Count > 0)
        {
            var files = string.Join(", ", committed.Select(entry => entry.File));

            return DoctorCheck.Fail(
                "configuration",
                $"a non-empty Shopify:ApiSecret is committed in {files}.",
                "An API secret in a committed file is a leaked credential: rotate it in the Partner Dashboard. " +
                "Keep appsettings.json's value empty and set the real one with " +
                "'dotnet user-secrets set \"Shopify:ApiSecret\" <value>' or the SHOPIFY_API_SECRET environment " +
                "variable. The value is deliberately not printed here.");
        }

        if (missing.Count > 0)
        {
            return DoctorCheck.Fail(
                "configuration",
                $"not set: {string.Join(", ", missing)}.",
                "Run 'shopify-blazor configure' to see the exact dotnet user-secrets commands and the Partner " +
                "Dashboard settings. An environment variable overrides user secrets, so check both.");
        }

        return DoctorCheck.Pass(
            "configuration",
            $"ApiKey, ApiSecret and AppUrl all resolve (ApiKey from {configuration.ApiKey.Source}).");
    }

    private DoctorCheck CheckAdminApiVersion(ProjectFile project)
    {
        var version = ReadAdminApiVersion(project.Directory);
        var result = AdminApiVersionCheck.Evaluate(version);

        return result.Outcome switch
        {
            ApiVersionOutcome.Current => DoctorCheck.Pass("admin-api-version", result.Reason),
            ApiVersionOutcome.Outdated => DoctorCheck.Warn("admin-api-version", result.Reason, result.Hint),
            ApiVersionOutcome.Missing => DoctorCheck.Warn("admin-api-version", result.Reason, result.Hint),
            _ => DoctorCheck.Warn("admin-api-version", result.Reason, result.Hint),
        };
    }

    private DoctorCheck CheckRenderMode(ProjectFile project)
    {
        var scan = RenderModeScanner.Scan(_fileSystem, project.Directory);

        if (scan.Found)
        {
            return DoctorCheck.Pass(
                "rendermode",
                $"{RenderModeScanner.Directive} is applied in {Relative(project.Directory, scan.FilesWithDirective[0])}.");
        }

        return DoctorCheck.Fail(
            "rendermode",
            $"no '{RenderModeScanner.Directive}' directive was found in {scan.FilesScanned} .razor file(s).",
            "A Blazor Web App is static without a render mode: the page renders perfectly and no handler is " +
            "ever attached, so every button is inert and nothing reports an error. Put the directive on the " +
            "page, or on Components/Routes.razor to apply it to every page (docs/adr/0001-hosting-model.md).");
    }

    private DoctorCheck CheckStaticAsset(ProjectFile project)
    {
        var references = ProjectFileReader.ReadReferences(_fileSystem, project.FilePath);
        var result = StaticAssetCheck.Evaluate(_fileSystem, _environment, references, project.Directory);

        return result.Outcome switch
        {
            StaticAssetOutcome.Found => DoctorCheck.Pass("polaris-interop", result.Reason),
            StaticAssetOutcome.Missing => DoctorCheck.Fail("polaris-interop", result.Reason, result.Hint),
            _ => DoctorCheck.Warn("polaris-interop", result.Reason, result.Hint),
        };
    }

    private void EmitHuman(ProjectFile project, IReadOnlyList<DoctorCheck> checks)
    {
        // Naming the project first matters when --project was used from elsewhere: otherwise the
        // report does not say what it just checked.
        _console.WriteLine($"Checking {project.FileName} in {project.Directory}");
        _console.WriteLine();

        foreach (var check in checks)
        {
            _console.WriteLine($"{check.StatusText,-4}  {check.Name,-17} {check.Reason}");

            if (check.Status != CheckStatus.Pass && check.Hint is { Length: > 0 })
            {
                _console.WriteLine($"      {' ', -17} -> {check.Hint}");
            }
        }

        var failed = checks.Count(check => check.Status == CheckStatus.Fail);
        var warned = checks.Count(check => check.Status == CheckStatus.Warn);

        _console.WriteLine();
        _console.WriteLine(failed == 0
            ? $"{checks.Count - failed - warned} passed, {warned} warned, 0 failed."
            : $"{checks.Count - failed - warned} passed, {warned} warned, {failed} failed.");

        if (failed > 0)
        {
            _console.WriteLine($"Exit code 1: fix the {failed} failure(s) above. See docs/troubleshooting.md.");
        }
    }

    private void EmitJson(string projectPath, IReadOnlyList<DoctorCheck> checks)
    {
        var report = new DoctorReportJson(
            projectPath,
            !checks.Any(check => check.Status == CheckStatus.Fail),
            checks.Select(check => new DoctorCheckJson(check.Name, check.StatusText, check.Reason, check.Hint)).ToList());

        _console.WriteLine(JsonSerializer.Serialize(report, DoctorJsonContext.Default.DoctorReportJson));
    }

    /// <summary>The nearest <c>global.json</c> at or above <paramref name="directory"/>, or <c>null</c>.</summary>
    private string? FindGlobalJson(string directory)
    {
        var current = directory;

        while (!string.IsNullOrEmpty(current))
        {
            var candidate = Path.Combine(current, "global.json");

            if (_fileSystem.FileExists(candidate))
            {
                return candidate;
            }

            current = Path.GetDirectoryName(current);
        }

        return null;
    }

    /// <summary>Reads <c>Shopify:AdminApiVersion</c> from the project's settings files.</summary>
    private string? ReadAdminApiVersion(string projectDirectory)
    {
        string? version = null;

        foreach (var path in ConfigurationReader.SettingsFiles(_fileSystem, projectDirectory))
        {
            var found = ReadAdminApiVersionFrom(path);

            if (found is not null)
            {
                version = found;
            }
        }

        return version;
    }

    private string? ReadAdminApiVersionFrom(string path)
    {
        try
        {
            using var document = JsonDocument.Parse(_fileSystem.ReadAllText(path), new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });

            if (document.RootElement.TryGetProperty("Shopify", out var section)
                && section.ValueKind == JsonValueKind.Object
                && section.TryGetProperty("AdminApiVersion", out var value)
                && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }
        }
        catch (JsonException)
        {
            // Treated as "no version declared"; the warn branch explains the default applies.
        }

        return null;
    }

    private static string Relative(string directory, string path) =>
        Path.GetRelativePath(directory, path).Replace('\\', '/');
}
