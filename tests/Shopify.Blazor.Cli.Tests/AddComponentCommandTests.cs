using Shopify.Blazor.Cli.Catalogue;
using Shopify.Blazor.Cli.Commands;
using Xunit;

namespace Shopify.Blazor.Cli.Tests;

/// <summary>
/// Tests for <c>add component</c>.
/// </summary>
/// <remarks>
/// The command must never invent a wrapper. A generated snippet that names a component the library
/// does not have is a compile error in the developer's project, and one that names the wrong
/// element is worse: it builds and renders nothing.
/// </remarks>
public sealed class AddComponentCommandTests
{
    private static AddComponentCommand Command(TempProject project) =>
        new(project.FileSystem, project.Environment, project.Console,
            () => PolarisCatalogue.Parse(PolarisCatalogueTests.FixtureJson));

    private static int Run(TempProject project, params string[] args)
    {
        var parsed = ArgumentParser.Parse(args, AddComponentCommand.AcceptedOptions);
        Assert.True(parsed.Succeeded, parsed.Error);

        return Command(project).Run(parsed.Arguments!, project.Root);
    }

    [Fact]
    public void Prints_usage_that_references_the_right_wrapper_and_element()
    {
        // The snippet is the deliverable. If the wrapper or the element is wrong, the developer
        // copies a page that does not compile or renders nothing.
        using var project = new TempProject().WithHealthyProjectFile();

        var exitCode = Run(project, "Button");

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("PolarisButton", project.Console.Text);
        Assert.Contains("s-button", project.Console.Text);
        Assert.Contains("@using Polaris.Blazor.Components", project.Console.Text);

    }

    [Fact]
    public void The_snippet_is_valid_razor_rather_than_prose()
    {
        // The catalogue records a parameter's type, not a legal value for it. Inventing one
        // produces a file that looks like working code and fails to compile, so every line of the
        // snippet is either Razor or a Razor comment — never prose that happens to parse as markup.
        var component = PolarisCatalogue.Parse(PolarisCatalogueTests.FixtureJson).Find("Button")!;
        var snippet = ComponentUsageTemplate.Render(component);

        foreach (var line in snippet.Split('\n').Select(candidate => candidate.Trim()))
        {
            if (line.Length == 0 || line.StartsWith("@using", StringComparison.Ordinal))
            {
                continue;
            }

            Assert.True(
                line.StartsWith('@') || line.StartsWith('<'),
                $"the snippet must be valid Razor or a comment, but this line is prose: '{line}'");
        }

        Assert.Contains("<PolarisButton />", snippet);
        Assert.Contains("@* Usage of PolarisButton", snippet);
    }

    [Fact]
    public void Writes_the_snippet_to_an_explicit_file()
    {
        // --path with a .razor file is the way to drop the snippet straight into a page.
        using var project = new TempProject().WithHealthyProjectFile();
        var target = project.At("Components", "Pages", "ButtonDemo.razor");

        var exitCode = Run(project, "Button", "--path", target);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.True(project.Exists("Components/Pages/ButtonDemo.razor"));
        Assert.Contains("PolarisButton", project.Read("Components/Pages/ButtonDemo.razor"));
    }

    [Fact]
    public void Writes_a_named_file_when_path_is_a_directory()
    {
        // --path with a directory has to produce a predictable file name, not an overwrite of
        // whatever was in that directory.
        using var project = new TempProject().WithHealthyProjectFile();

        var exitCode = Run(project, "Button", "--path", project.At("Components", "Pages"));

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.True(project.Exists("Components/Pages/PolarisButtonUsage.razor"));
    }

    [Fact]
    public void Refuses_to_overwrite_without_force()
    {
        // Same promise as add page: --force is the only way to replace a file.
        using var project = new TempProject().WithHealthyProjectFile();

        project.Write("Components/Pages/Home.razor", "@* keep me *@");

        var exitCode = Run(project, "Button", "--path", project.At("Components", "Pages", "Home.razor"));

        Assert.Equal(ExitCodes.AlreadyExists, exitCode);
        Assert.Equal("@* keep me *@", project.Read("Components/Pages/Home.razor"));
    }

    [Fact]
    public void Dry_run_writes_nothing()
    {
        using var project = new TempProject().WithHealthyProjectFile();

        var exitCode = Run(project, "Button", "--path", project.At("Components", "Pages", "Demo.razor"), "--dry-run");

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.False(project.Exists("Components/Pages/Demo.razor"));
        Assert.Contains("would write", project.Console.Text);
    }

    [Fact]
    public void An_unknown_component_fails_with_suggestions()
    {
        // The failure mode this prevents: the command writing a snippet for a component the library
        // does not have, which only fails when the developer builds.
        using var project = new TempProject().WithHealthyProjectFile();

        var exitCode = Run(project, "DataTablet");

        Assert.Equal(ExitCodes.Usage, exitCode);
        Assert.Contains("not in the component catalogue", project.Console.Text);
        Assert.Contains("PolarisDataTable", project.Console.Text);
    }

    [Fact]
    public void An_element_with_no_wrapper_says_so_and_points_at_the_generator()
    {
        // 'tools/polaris-elements.json' is optional and owned by another workstream. When it is
        // present and names an element Shopify ships, the useful message is "no wrapper yet", not
        // "unknown component".
        using var project = new TempProject().WithHealthyProjectFile();

        project.Write("tools/polaris-elements.json", """
            {
              "elements": [
                { "tag": "s-fancy-chart", "status": "stable" }
              ]
            }
            """);

        var exitCode = Run(project, "FancyChart");

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("no wrapper for it yet", project.Console.Text);
        Assert.Contains("tools/generate-component.py", project.Console.Text);
    }

    [Fact]
    public void An_unknown_component_fails_when_the_elements_file_is_absent()
    {
        // The elements file is optional, so its absence must not change the exit code of a real
        // failure.
        using var project = new TempProject().WithHealthyProjectFile();

        Assert.False(project.Exists("tools/polaris-elements.json"));

        var exitCode = Run(project, "Nonsense");

        Assert.Equal(ExitCodes.Usage, exitCode);
    }

    [Fact]
    public void A_missing_catalogue_fails_with_an_actionable_message()
    {
        // The catalogue is embedded only when the generated file exists. Until then the command
        // must explain what is missing and how to produce it, rather than reporting that every
        // component is unknown.
        using var project = new TempProject().WithHealthyProjectFile();

        var command = new AddComponentCommand(
            project.FileSystem,
            project.Environment,
            project.Console,
            () => throw new CatalogueException("the catalogue is not embedded; run the generator"));

        var parsed = ArgumentParser.Parse(["Button"], AddComponentCommand.AcceptedOptions);

        var exitCode = command.Run(parsed.Arguments!, project.Root);

        Assert.Equal(ExitCodes.InternalError, exitCode);
        Assert.Contains("not embedded", project.Console.Text);
    }

    [Fact]
    public void Needs_a_component_name()
    {
        using var project = new TempProject().WithHealthyProjectFile();

        var exitCode = Run(project);

        Assert.Equal(ExitCodes.Usage, exitCode);
        Assert.Contains("takes one component name", project.Console.Text);
    }
}
