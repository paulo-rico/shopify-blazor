using Shopify.Blazor.Cli.Commands;
using Xunit;

namespace Shopify.Blazor.Cli.Tests;

/// <summary>
/// Tests for the documentation surfaces the CLI exposes: help text, exit codes and the name
/// conversions everything else depends on.
/// </summary>
/// <remarks>
/// These are the tests that keep the parts a person reads true. A help text that omits
/// <c>--force</c>, or a route spelled differently from the name, are both invisible failures.
/// </remarks>
public sealed class CliDocumentationTests
{
    public static TheoryData<string> AllOptionNames()
    {
        var data = new TheoryData<string>();

        // Options shared by several commands (--force, --dry-run, --path) must appear once:
        // xUnit skips a theory case whose id is a duplicate of an earlier one.
        var names = NewCommand.AcceptedOptions
            .Concat(AddPageCommand.AcceptedOptions)
            .Concat(AddComponentCommand.AcceptedOptions)
            .Concat(ConfigureCommand.AcceptedOptions)
            .Concat(DoctorCommand.AcceptedOptions)
            .Select(spec => spec.Name)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal);

        foreach (var name in names)
        {
            data.Add("--" + name);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AllOptionNames))]
    public void Help_documents_every_accepted_option(string option)
    {
        // A documented surface that drifts from the real one is worse than none: the developer
        // trusts it. This fails the moment an option is added without being documented.
        Assert.Contains(option, HelpText.Full);
    }

    [Theory]
    [InlineData("new")]
    [InlineData("add page")]
    [InlineData("add component")]
    [InlineData("configure")]
    [InlineData("doctor")]
    [InlineData("--help")]
    [InlineData("--version")]
    public void Help_documents_every_command(string command)
    {
        Assert.Contains(command, HelpText.Full);
    }

    [Fact]
    public void Help_states_the_exit_codes()
    {
        // A script gating on doctor depends on the numbers, not the wording.
        Assert.Contains("Exit codes", HelpText.Full);
        Assert.Contains("3  a file already exists", HelpText.Full);
    }

    [Fact]
    public void Help_says_the_cli_is_not_the_only_way()
    {
        // The plan is explicit: the CLI is a convenience wrapper. If the help ever contradicts that,
        // the promise has been broken somewhere else too.
        Assert.Contains("never the only way", HelpText.Full);
        Assert.Contains("dotnet new shopify-blazor", HelpText.Full);
    }

    [Fact]
    public void Exit_codes_are_distinct()
    {
        // Two codes with the same number makes a script's branching ambiguous.
        var codes = new[]
        {
            ExitCodes.Success, ExitCodes.Failure, ExitCodes.Usage,
            ExitCodes.AlreadyExists, ExitCodes.ProcessFailed, ExitCodes.InternalError,
        };

        Assert.Equal(codes.Length, codes.Distinct().Count());
        Assert.Equal(0, ExitCodes.Success);
    }

    [Theory]
    [InlineData("Products", "products")]
    [InlineData("ProductEditor", "product-editor")]
    [InlineData("APIKeys", "api-keys")]
    [InlineData("orderHistory", "order-history")]
    [InlineData("Product 2", "product-2")]
    [InlineData("URL", "url")]
    public void Kebab_case_matches_the_route_convention(string input, string expected)
    {
        // A route that does not match the convention is either a 404 or a page that quietly
        // shadows another page's route.
        Assert.Equal(expected, NameHelper.ToKebabCase(input));
    }

    [Theory]
    [InlineData("product-editor", "ProductEditor")]
    [InlineData("Product Editor", "ProductEditor")]
    [InlineData("products", "Products")]
    public void Pascal_case_produces_a_valid_type_name(string input, string expected)
    {
        Assert.Equal(expected, NameHelper.ToPascalCase(input));
    }

    [Fact]
    public void A_page_already_containing_the_render_mode_passes_the_directive_check()
    {
        // The directive has to be recognised where a person would put it, including after
        // indentation.
        Assert.True(RenderModeScanner.HasDirective("    @rendermode InteractiveServer\n"));
        Assert.False(RenderModeScanner.HasDirective("@rendermode InteractiveWebAssembly\n"));
        Assert.False(RenderModeScanner.HasDirective("text @rendermode InteractiveServer text\n"));
    }
}
