using Shopify.Blazor.Cli.Commands;
using Xunit;

namespace Shopify.Blazor.Cli.Tests;

/// <summary>
/// Tests for <c>configure</c> and <c>configure --check</c>.
/// </summary>
/// <remarks>
/// The command's output goes into terminals, CI logs and pasted issue reports, so its most
/// important behaviour is a negative one: it must never print a secret value. A secret that appears
/// once in a log has to be rotated.
/// </remarks>
public sealed class ConfigureCommandTests
{
    private const string SecretValue = "shpss_do_not_print_this_value";

    private static int Run(TempProject project, params string[] args)
    {
        var parsed = ArgumentParser.Parse(args, ConfigureCommand.AcceptedOptions);
        Assert.True(parsed.Succeeded, parsed.Error);

        return new ConfigureCommand(project.FileSystem, project.Environment, project.Console)
            .Run(parsed.Arguments!, project.Root);
    }

    [Fact]
    public void Check_passes_when_every_setting_resolves()
    {
        // The baseline: a complete configuration must exit 0, or the check is unusable as a gate.
        using var project = new TempProject().WithHealthyProjectFile();

        project.Environment
            .With("SHOPIFY_API_KEY", "key")
            .With("SHOPIFY_API_SECRET", SecretValue)
            .With("SHOPIFY_APP_URL", "https://shop.example.com");

        var exitCode = Run(project, "--check");

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("complete", project.Console.Text);
    }

    [Fact]
    public void Check_never_prints_a_secret_value()
    {
        // The one assertion that matters most here: the resolved secret is reported as set, with
        // its source, and never echoed.
        using var project = new TempProject().WithHealthyProjectFile();

        project.Environment
            .With("SHOPIFY_API_KEY", "key-value-visible")
            .With("SHOPIFY_API_SECRET", SecretValue)
            .With("SHOPIFY_APP_URL", "https://shop.example.com");

        Run(project, "--check");

        Assert.DoesNotContain(SecretValue, project.Console.Text);
        Assert.Contains("SHOPIFY_API_SECRET", project.Console.Text);
    }

    [Fact]
    public void Check_fails_and_lists_what_is_missing()
    {
        // A check that exits 0 with settings missing would let a broken environment through CI.
        using var project = new TempProject().WithHealthyProjectFile()
            .WithAppSettings(apiKey: string.Empty, apiSecret: string.Empty, appUrl: string.Empty);

        var exitCode = Run(project, "--check");

        Assert.Equal(ExitCodes.Failure, exitCode);
        Assert.Contains("Shopify:ApiKey", project.Console.Text);
        Assert.Contains("Shopify:ApiSecret", project.Console.Text);
        Assert.Contains("Shopify:AppUrl", project.Console.Text);
    }

    [Fact]
    public void Check_fails_when_the_secret_is_committed_in_appsettings()
    {
        // configure --check and doctor agree on this: a committed secret is a failure, not a pass
        // with a note.
        using var project = new TempProject().WithHealthyProjectFile()
            .WithAppSettings(apiKey: "key", apiSecret: SecretValue, appUrl: "https://shop.example.com");

        var exitCode = Run(project, "--check");

        Assert.Equal(ExitCodes.Failure, exitCode);
        Assert.DoesNotContain(SecretValue, project.Console.Text);
        Assert.Contains("committed file", project.Console.Text);
    }

    [Fact]
    public void The_guide_prints_the_user_secrets_commands_with_the_projects_own_id()
    {
        // A user-secrets command with an id the project does not use writes to a store the app
        // never reads, and the developer sees "successfully set" the whole time.
        using var project = new TempProject().WithHealthyProjectFile();

        var exitCode = Run(project);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("dotnet user-secrets set \"Shopify:ApiKey\"", project.Console.Text);
        Assert.Contains("--id fixture-secrets-id", project.Console.Text);
    }

    [Fact]
    public void The_guide_names_the_three_environment_variables_and_the_dashboard_settings()
    {
        // The environment variable names are the contract between the README, the template and this
        // command; a typo in one of them is a silently unconfigured app.
        using var project = new TempProject().WithHealthyProjectFile();

        Run(project);

        Assert.Contains("SHOPIFY_API_KEY", project.Console.Text);
        Assert.Contains("SHOPIFY_API_SECRET", project.Console.Text);
        Assert.Contains("SHOPIFY_APP_URL", project.Console.Text);
        Assert.Contains("Partner Dashboard", project.Console.Text);
        Assert.Contains("App URL", project.Console.Text);
    }

    [Fact]
    public void Check_fails_clearly_outside_a_project()
    {
        using var project = new TempProject();

        var exitCode = Run(project, "--check");

        Assert.Equal(ExitCodes.Failure, exitCode);
        Assert.Contains("No .csproj file", project.Console.Text);
    }

    [Fact]
    public void The_guide_mentions_that_an_environment_variable_overrides_user_secrets()
    {
        // AGENTS.md #15: a key from a different Shopify app makes every session token fail as
        // "another app", and the only clue is which layer won.
        using var project = new TempProject().WithHealthyProjectFile();

        Run(project);

        Assert.Contains("override user secrets", project.Console.Text);
    }
}
