using Shopify.Blazor.Cli.Abstractions;
using Shopify.Blazor.Cli.Commands;
using Xunit;

namespace Shopify.Blazor.Cli.Tests;

/// <summary>A process runner that records the command and returns a scripted result.</summary>
/// <remarks>
/// Tests must never run a template engine: a real <c>dotnet new</c> would need the template
/// installed, take seconds, and write into the repository. This seam is why <c>new</c> is testable
/// at all.
/// </remarks>
public sealed class FakeProcessRunner : IProcessRunner
{
    private readonly ProcessResult _result;

    /// <summary>Creates a runner returning <paramref name="result"/>.</summary>
    public FakeProcessRunner(ProcessResult? result = null) =>
        _result = result ?? new ProcessResult(0, string.Empty, string.Empty);

    /// <summary>Every invocation, in order.</summary>
    public List<(string FileName, IReadOnlyList<string> Arguments, string WorkingDirectory)> Invocations { get; } = [];

    /// <inheritdoc />
    public ProcessResult Run(string fileName, IReadOnlyList<string> arguments, string workingDirectory)
    {
        Invocations.Add((fileName, arguments, workingDirectory));
        return _result;
    }
}

/// <summary>
/// Tests for <c>new</c>.
/// </summary>
/// <remarks>
/// <c>new</c> is the one command that shells out, so the tests pin the command line it builds and
/// the directory checks it makes. Refusing a non-empty directory is what keeps a scaffold from
/// becoming a merge.
/// </remarks>
public sealed class NewCommandTests
{
    private static int Run(
        TempProject project,
        FakeProcessRunner runner,
        params string[] args)
    {
        var parsed = ArgumentParser.Parse(args, NewCommand.AcceptedOptions);
        Assert.True(parsed.Succeeded, parsed.Error);

        return new NewCommand(project.FileSystem, runner, project.Environment, project.Console)
            .Run(parsed.Arguments!);
    }

    [Fact]
    public void Runs_dotnet_new_with_the_template_and_the_project_name()
    {
        // The CLI must not implement its own scaffolding: 'dotnet new shopify-blazor' is the single
        // source of the project shape, and the argument list is the contract with it.
        using var project = new TempProject();
        var runner = new FakeProcessRunner();

        var exitCode = Run(project, runner, "MyShopifyApp");

        Assert.Equal(ExitCodes.Success, exitCode);

        var invocation = Assert.Single(runner.Invocations);

        Assert.Equal("dotnet", invocation.FileName);
        Assert.Equal("new", invocation.Arguments[0]);
        Assert.Equal("shopify-blazor", invocation.Arguments[1]);
        Assert.Contains("--name", invocation.Arguments);
        Assert.Contains("MyShopifyApp", invocation.Arguments);
    }

    [Fact]
    public void Passes_both_template_parameters_explicitly()
    {
        // Relying on the template's defaults makes the generated project depend on which template
        // version happens to be installed, which is exactly the kind of drift that is invisible.
        using var project = new TempProject();
        var runner = new FakeProcessRunner();

        Run(project, runner, "MyShopifyApp", "--include-sample-pages", "false", "--include-admin-api-example", "false");

        var invocation = Assert.Single(runner.Invocations);
        var arguments = invocation.Arguments.ToList();

        Assert.Equal("false", arguments[arguments.IndexOf("--include-sample-pages") + 1]);
        Assert.Equal("false", arguments[arguments.IndexOf("--include-admin-api-example") + 1]);
    }

    [Fact]
    public void Defaults_both_template_parameters_to_true()
    {
        using var project = new TempProject();
        var runner = new FakeProcessRunner();

        Run(project, runner, "MyShopifyApp");

        var arguments = Assert.Single(runner.Invocations).Arguments.ToList();

        Assert.Equal("true", arguments[arguments.IndexOf("--include-sample-pages") + 1]);
        Assert.Equal("true", arguments[arguments.IndexOf("--include-admin-api-example") + 1]);
    }

    [Fact]
    public void Refuses_a_target_directory_that_exists_and_is_not_empty()
    {
        // The template engine would write into whatever is there. A scaffold that merges into an
        // existing project overwrites edited files, and the developer has no reason to expect it.
        using var project = new TempProject();
        var target = project.At("MyShopifyApp");

        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "Program.cs"), "// existing work");
        var runner = new FakeProcessRunner();

        var exitCode = Run(project, runner, "MyShopifyApp");

        Assert.Equal(ExitCodes.AlreadyExists, exitCode);
        Assert.Empty(runner.Invocations);
        Assert.Contains("already exists and is not empty", project.Console.Text);
        Assert.Equal("// existing work", File.ReadAllText(Path.Combine(target, "Program.cs")));
    }

    [Fact]
    public void Allows_an_existing_but_empty_target_directory()
    {
        // An empty directory is the common case after 'mkdir MyApp && cd MyApp'.
        using var project = new TempProject();

        Directory.CreateDirectory(project.At("MyShopifyApp"));
        var runner = new FakeProcessRunner();

        var exitCode = Run(project, runner, "MyShopifyApp");

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Single(runner.Invocations);
    }

    [Fact]
    public void Dry_run_prints_the_command_and_runs_nothing()
    {
        // 'new' writes a whole project. A dry run has to be a genuine preview for it to be worth
        // having.
        using var project = new TempProject();
        var runner = new FakeProcessRunner();

        var exitCode = Run(project, runner, "MyShopifyApp", "--dry-run");

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Empty(runner.Invocations);
        Assert.Contains("dotnet new shopify-blazor", project.Console.Text);
        Assert.Contains("nothing was written", project.Console.Text);
    }

    [Fact]
    public void Prints_the_setup_steps_with_the_environment_variable_names()
    {
        // The environment variable names are the contract between the README, the template and the
        // app's configuration. A wrong name here leaves the app unconfigured with no error.
        using var project = new TempProject();
        var runner = new FakeProcessRunner();

        Run(project, runner, "MyShopifyApp");

        Assert.Contains("SHOPIFY_API_KEY", project.Console.Text);
        Assert.Contains("SHOPIFY_API_SECRET", project.Console.Text);
        Assert.Contains("SHOPIFY_APP_URL", project.Console.Text);
        Assert.Contains("dotnet user-secrets set", project.Console.Text);
        Assert.Contains("README", project.Console.Text);
    }

    [Fact]
    public void Points_at_the_template_install_when_the_template_is_missing()
    {
        // The most likely failure of 'new' on a fresh machine is that the template was never
        // installed, and the engine's own message does not say what to install.
        using var project = new TempProject();
        var runner = new FakeProcessRunner(new ProcessResult(103, string.Empty, "No templates found matching: 'shopify-blazor'."));

        var exitCode = Run(project, runner, "MyShopifyApp");

        Assert.Equal(ExitCodes.ProcessFailed, exitCode);
        Assert.Contains("dotnet new install", project.Console.Text);
    }

    [Fact]
    public void Needs_a_project_name()
    {
        using var project = new TempProject();
        var runner = new FakeProcessRunner();

        var exitCode = Run(project, runner);

        Assert.Equal(ExitCodes.Usage, exitCode);
        Assert.Empty(runner.Invocations);
    }

    [Fact]
    public void Rejects_a_boolean_that_is_not_a_boolean()
    {
        // '--include-sample-pages maybe' silently becoming the default is the kind of quiet
        // difference a developer would never find.
        using var project = new TempProject();
        var runner = new FakeProcessRunner();

        var exitCode = Run(project, runner, "MyShopifyApp", "--include-sample-pages", "maybe");

        Assert.Equal(ExitCodes.Usage, exitCode);
        Assert.Empty(runner.Invocations);
        Assert.Contains("takes true or false", project.Console.Text);
    }

    [Fact]
    public void Honours_an_explicit_output_directory()
    {
        // --output is how a project is scaffolded outside the current directory; the project still
        // goes into a subdirectory named after the project.
        using var project = new TempProject();
        var runner = new FakeProcessRunner();
        var output = project.At("apps");

        Run(project, runner, "MyShopifyApp", "--output", output);

        var arguments = Assert.Single(runner.Invocations).Arguments.ToList();

        Assert.Equal(output, arguments[arguments.IndexOf("--output") + 1]);
        Assert.Contains(output, project.Console.Text);
    }
}
