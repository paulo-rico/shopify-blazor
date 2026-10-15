using System.Text.Json;
using Shopify.Blazor.Cli.Commands;
using Xunit;

namespace Shopify.Blazor.Cli.Tests;

/// <summary>
/// Tests for <c>doctor</c>.
/// </summary>
/// <remarks>
/// Doctor's value is entirely in its judgement: a check that FAILs on a healthy project trains the
/// reader to ignore the tool, and one that PASSes on a broken project is worse than no check at
/// all. Each test here is a specific broken-or-healthy project shape and the outcome it must get.
/// </remarks>
public sealed class DoctorCommandTests
{
    private static DoctorCommand Command(TempProject project) =>
        new(project.FileSystem, project.Environment, project.Console);

    private static int Run(TempProject project, params string[] args)
    {
        var parsed = ArgumentParser.Parse(args, DoctorCommand.AcceptedOptions);
        Assert.True(parsed.Succeeded, parsed.Error);

        return Command(project).Run(parsed.Arguments!);
    }

    /// <summary>
    /// A fixture that is healthy in every way doctor can see: both references, a complete
    /// configuration, a current API version, the render-mode directive, and a real
    /// <c>polaris-interop.js</c> behind a project reference.
    /// </summary>
    private static TempProject HealthyProject()
    {
        var project = new TempProject()
            .WithHealthyProjectFile()
            .WithGlobalJson()
            .WithAppSettings(apiKey: "key", apiSecret: string.Empty, appUrl: "https://example.ngrok.app", adminApiVersion: "2026-10")
            .WithRenderMode()
            .WithFramework(".NET 10.0.1");

        project.Environment
            .With("SHOPIFY_API_KEY", "env-key")
            .With("SHOPIFY_API_SECRET", "env-secret")
            .With("SHOPIFY_APP_URL", "https://example.ngrok.app");

        // The Polaris project reference has to resolve to something with the interop script in its
        // wwwroot, because that is the strongest offline proof the asset will be published.
        var polarisRoot = Path.Combine(project.RepositoryRoot, "src", "Polaris.Blazor");
        Directory.CreateDirectory(Path.Combine(polarisRoot, "wwwroot"));
        File.WriteAllText(Path.Combine(polarisRoot, "wwwroot", "polaris-interop.js"), "// interop");
        File.WriteAllText(Path.Combine(polarisRoot, "Polaris.Blazor.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk.Razor\"></Project>");

        return project;
    }

    private static DoctorCheck Check(TempProject project, string name)
    {
        var projectFile = new ProjectLocator(project.FileSystem)
            .Find(null, project.Root)
            .Project!;

        return Command(project).RunChecks(projectFile).Single(check => check.Name == name);
    }

    [Fact]
    public void A_healthy_fixture_produces_no_failures()
    {
        // The baseline. If this fails, every other verdict from doctor is suspect.
        using var project = HealthyProject();

        var exitCode = Run(project);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.DoesNotContain("FAIL", project.Console.Text);
        Assert.Contains("PASS", project.Console.Text);
    }

    [Fact]
    public void A_project_missing_both_references_fails_the_references_check()
    {
        // Without Polaris.Blazor no <Polaris…> component resolves and the project is a plain Blazor
        // app; without Shopify.Blazor there is no embedded-app authentication at all.
        using var project = HealthyProject();
        project.WithHealthyProjectFile(referencePolaris: false, referenceShopify: false);

        var check = Check(project, "references");

        Assert.Equal(CheckStatus.Fail, check.Status);
        Assert.Contains("Shopify.Blazor", check.Reason);
        Assert.Contains("Polaris.Blazor", check.Reason);
        Assert.NotNull(check.Hint);
    }

    [Fact]
    public void A_project_missing_only_polaris_fails_and_names_polaris()
    {
        // Naming both when only one is missing sends the developer to edit a correct reference.
        using var project = HealthyProject();
        project.WithHealthyProjectFile(referencePolaris: false);

        var check = Check(project, "references");

        Assert.Equal(CheckStatus.Fail, check.Status);
        Assert.Contains("Polaris.Blazor", check.Reason);
        Assert.DoesNotContain("Shopify.Blazor and ", check.Reason);
    }

    [Fact]
    public void A_committed_non_empty_api_secret_fails()
    {
        // A credential in a committed file has to be rotated, and nothing else in the toolkit says
        // so. This is the check that catches it before it reaches a public repository.
        using var project = HealthyProject();
        project.WithAppSettings(apiKey: "key", apiSecret: "shpss_a_real_looking_secret", appUrl: "https://example.ngrok.app");

        var check = Check(project, "configuration");

        Assert.Equal(CheckStatus.Fail, check.Status);
        Assert.Contains("appsettings.json", check.Reason);
        Assert.NotNull(check.Hint);

        // The value must never be echoed: doctor output ends up in issues and CI logs.
        Assert.DoesNotContain("shpss_a_real_looking_secret", check.Reason);
        Assert.DoesNotContain("shpss_a_real_looking_secret", check.Hint);
    }

    [Fact]
    public void A_committed_secret_in_the_development_settings_file_fails_too()
    {
        // appsettings.Development.json is committed just as often as appsettings.json.
        using var project = HealthyProject();
        project.WithAppSettings(apiKey: "key", apiSecret: "dev-secret", appUrl: "https://x", fileName: "appsettings.Development.json");

        var check = Check(project, "configuration");

        Assert.Equal(CheckStatus.Fail, check.Status);
        Assert.Contains("appsettings.Development.json", check.Reason);
    }

    [Fact]
    public void Missing_settings_fail_and_name_the_environment_variable()
    {
        // "Not configured" has two fixes — user secrets and an environment variable — and the
        // message has to give both, because an environment variable silently wins (AGENTS.md #15).
        using var project = new TempProject()
            .WithHealthyProjectFile()
            .WithAppSettings(apiKey: string.Empty, apiSecret: string.Empty, appUrl: string.Empty);

        var check = Check(project, "configuration");

        Assert.Equal(CheckStatus.Fail, check.Status);
        Assert.Contains("SHOPIFY_API_KEY", check.Reason);
        Assert.Contains("SHOPIFY_API_SECRET", check.Reason);
        Assert.Contains("SHOPIFY_APP_URL", check.Reason);
    }

    [Fact]
    public void A_missing_render_mode_directive_fails()
    {
        // The silent failure this whole toolkit is built around: the page renders perfectly and
        // every handler is never attached. Nothing else reports it.
        using var project = HealthyProject();
        File.Delete(project.At("Components", "Routes.razor"));

        var check = Check(project, "rendermode");

        Assert.Equal(CheckStatus.Fail, check.Status);
        Assert.Contains("@rendermode InteractiveServer", check.Reason);
        Assert.Contains("static", check.Hint);
    }

    [Fact]
    public void A_commented_out_render_mode_directive_does_not_count()
    {
        // The template plan itself quotes the directive inside a comment. Counting that as present
        // is exactly the mistake the check exists to catch.
        using var project = HealthyProject();
        project.Write("Components/Routes.razor", """
            @*
                @rendermode InteractiveServer
            *@
            <Router AppAssembly="@typeof(Program).Assembly"></Router>
            """);

        Assert.Equal(CheckStatus.Fail, Check(project, "rendermode").Status);
    }

    [Fact]
    public void An_older_stable_admin_api_version_warns_and_names_both()
    {
        // A retired version does not error: Shopify falls forward and answers normally, so this
        // warning is the only signal (AGENTS.md #16). It must be a WARN, not a FAIL — the app works.
        using var project = HealthyProject();
        project.WithAppSettings(apiKey: "key", apiSecret: string.Empty, appUrl: "https://x", adminApiVersion: "2024-01");

        var check = Check(project, "admin-api-version");

        Assert.Equal(CheckStatus.Warn, check.Status);
        Assert.Contains("2024-01", check.Reason);
        Assert.Contains(AdminApiVersionCheck.NewestKnownVersion, check.Reason);
    }

    [Fact]
    public void A_version_this_cli_does_not_know_warns_rather_than_fails()
    {
        // A CLI older than the project must never call a working configuration broken.
        using var project = HealthyProject();
        project.WithAppSettings(apiKey: "key", apiSecret: string.Empty, appUrl: "https://x", adminApiVersion: "2032-04");

        var check = Check(project, "admin-api-version");

        Assert.Equal(CheckStatus.Warn, check.Status);
        Assert.Contains("newer than the newest stable version", check.Reason);
    }

    [Fact]
    public void A_missing_admin_api_version_warns_about_the_default()
    {
        using var project = HealthyProject();
        project.WithAppSettings(apiKey: "key", apiSecret: string.Empty, appUrl: "https://x");

        var check = Check(project, "admin-api-version");

        Assert.Equal(CheckStatus.Warn, check.Status);
        Assert.Contains("no AdminApiVersion", check.Reason);
    }

    [Fact]
    public void A_project_reference_with_the_interop_script_passes()
    {
        // The offline proxy for /_content/Polaris.Blazor/polaris-interop.js: a project reference
        // resolves and its source wwwroot holds the file.
        using var project = HealthyProject();

        var check = Check(project, "polaris-interop");

        Assert.Equal(CheckStatus.Pass, check.Status);
        Assert.Contains("polaris-interop.js", check.Reason);
    }

    [Fact]
    public void A_project_reference_without_the_interop_script_fails()
    {
        // This is the 404 that makes every control inert while the markup looks perfect.
        using var project = HealthyProject();
        File.Delete(Path.Combine(project.RepositoryRoot, "src", "Polaris.Blazor", "wwwroot", "polaris-interop.js"));

        var check = Check(project, "polaris-interop");

        Assert.Equal(CheckStatus.Fail, check.Status);
        Assert.Contains("interop", check.Hint);
    }

    [Fact]
    public void A_package_reference_with_a_cache_hit_passes_and_a_miss_warns()
    {
        // A PackageReference can only be checked through the NuGet cache. A hit is proof; a miss is
        // unknown, never a failure — the cache layout is not a contract.
        using var project = HealthyProject();
        project.Write("Fixture.csproj", """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <ItemGroup>
                <PackageReference Include="Shopify.Blazor" Version="0.1.0-preview.1" />
                <PackageReference Include="Polaris.Blazor" Version="0.1.0-preview.1" />
              </ItemGroup>
            </Project>
            """);

        var cache = project.At("nuget-cache");
        Directory.CreateDirectory(Path.Combine(cache, "polaris.blazor", "0.1.0-preview.1", "staticwebassets"));
        File.WriteAllText(
            Path.Combine(cache, "polaris.blazor", "0.1.0-preview.1", "staticwebassets", "polaris-interop.js"),
            "// interop");

        project.Environment.With("NUGET_PACKAGES", cache);

        var hit = Check(project, "polaris-interop");

        Assert.Equal(CheckStatus.Pass, hit.Status);
        Assert.Contains("restored package", hit.Reason);

        // A cache that does not exist is the "cannot decide" case: the package may well be
        // restored somewhere this process cannot see, so it is a warning, never a failure.
        project.Environment.With("NUGET_PACKAGES", project.At("no-such-cache"));

        var miss = Check(project, "polaris-interop");

        Assert.Equal(CheckStatus.Warn, miss.Status);
        Assert.Contains("/_content/Polaris.Blazor/polaris-interop.js", miss.Hint);
    }

    [Fact]
    public void An_unreadable_global_json_warns_rather_than_fails()
    {
        // rollForward has seven values with different rules. Guessing wrongly sends someone to
        // reinstall an SDK they already have.
        using var project = HealthyProject();
        project.Write("global.json", "{ \"sdk\": { \"rollForward\": \"latestFeature\" } }");

        var check = Check(project, "dotnet-sdk");

        Assert.Equal(CheckStatus.Warn, check.Status);
    }

    [Fact]
    public void An_installed_sdk_below_the_pinned_band_fails()
    {
        // This one really is broken: the SDK MSBuild resolves cannot build the project.
        using var project = HealthyProject().WithFramework(".NET 9.0.5");
        project.WithGlobalJson("10.0.100");

        var check = Check(project, "dotnet-sdk");

        Assert.Equal(CheckStatus.Fail, check.Status);
        Assert.Contains("10.0.100", check.Reason);
    }

    [Fact]
    public void Json_output_parses_and_carries_the_same_verdicts()
    {
        // CI reads this. If it does not parse, or the property names come out in the wrong case,
        // the gate silently stops gating.
        using var project = HealthyProject();
        project.WithAppSettings(apiKey: "key", apiSecret: "committed-secret", appUrl: "https://x");

        var exitCode = Run(project, "--json");

        Assert.Equal(ExitCodes.Failure, exitCode);

        using var document = JsonDocument.Parse(project.Console.Text);

        Assert.False(document.RootElement.GetProperty("healthy").GetBoolean());

        var checks = document.RootElement.GetProperty("checks").EnumerateArray().ToList();

        Assert.NotEmpty(checks);
        Assert.All(checks, check =>
        {
            Assert.False(string.IsNullOrWhiteSpace(check.GetProperty("name").GetString()));
            Assert.Contains(check.GetProperty("status").GetString(), new[] { "PASS", "WARN", "FAIL" });
            Assert.False(string.IsNullOrWhiteSpace(check.GetProperty("reason").GetString()));
        });

        // The secret must not have leaked into the machine-readable output either.
        Assert.DoesNotContain("committed-secret", project.Console.Text);
    }

    [Fact]
    public void Json_output_marks_a_healthy_project_healthy()
    {
        using var project = HealthyProject();

        var exitCode = Run(project, "--json");

        Assert.Equal(ExitCodes.Success, exitCode);

        using var document = JsonDocument.Parse(project.Console.Text);

        Assert.True(document.RootElement.GetProperty("healthy").GetBoolean());
    }

    [Fact]
    public void A_missing_project_fails_the_project_check()
    {
        // Nothing else can run, so this has to be a failure with a usable message rather than a
        // report full of "not found" for six checks.
        using var project = new TempProject();

        var exitCode = Run(project);

        Assert.Equal(ExitCodes.Failure, exitCode);
        Assert.Contains("No .csproj file", project.Console.Text);
    }

    [Fact]
    public void Project_option_selects_the_project_to_check()
    {
        // --project is how the command is pointed at a project from anywhere else, which is what CI
        // does. It must win over the working directory.
        using var project = HealthyProject();

        var parsed = ArgumentParser.Parse(["--project", project.Root], DoctorCommand.AcceptedOptions);
        var exitCode = new DoctorCommand(project.FileSystem, project.Environment, project.Console)
            .Run(parsed.Arguments!);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("Fixture.csproj", project.Console.Text);
    }

    [Fact]
    public void Reports_a_project_path_that_does_not_exist()
    {
        // A bad --project has to be reported as such, not silently fall back to the working
        // directory and check the wrong project.
        using var project = HealthyProject();

        var parsed = ArgumentParser.Parse(
            ["--project", Path.Combine(project.RepositoryRoot, "nope")],
            DoctorCommand.AcceptedOptions);

        var exitCode = new DoctorCommand(project.FileSystem, project.Environment, project.Console)
            .Run(parsed.Arguments!);

        Assert.Equal(ExitCodes.Failure, exitCode);
        Assert.Contains("FAIL  project", project.Console.Text);
    }
}
