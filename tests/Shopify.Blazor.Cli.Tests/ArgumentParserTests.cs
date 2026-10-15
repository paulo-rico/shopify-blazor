using Xunit;

namespace Shopify.Blazor.Cli.Tests;

/// <summary>
/// Tests for the hand-rolled argument parser.
/// </summary>
/// <remarks>
/// The parser is the one place a mistyped flag can be silently dropped, and a dropped
/// <c>--force</c> is the difference between "refused to overwrite" and "overwrote your file". These
/// tests pin the accepted spellings and, just as importantly, that everything else is an error.
/// </remarks>
public sealed class ArgumentParserTests
{
    private static readonly OptionSpec[] Accepted =
    [
        new("path", OptionKind.Value),
        new("force", OptionKind.Flag),
        new("dry-run", OptionKind.Flag),
    ];

    [Fact]
    public void Parses_a_bare_flag()
    {
        // A flag that does not register is a --force that silently does nothing, and the next run
        // refuses to overwrite for no visible reason.
        var result = ArgumentParser.Parse(["--force"], Accepted);

        Assert.True(result.Succeeded);
        Assert.True(result.Arguments!.Has("force"));
        Assert.Null(result.Arguments.Value("force"));
    }

    [Fact]
    public void Parses_separated_and_equals_option_values()
    {
        // The two spellings are documented as equivalent; if they ever diverge, half the documented
        // examples stop working.
        var separated = ArgumentParser.Parse(["--path", "src/Pages"], Accepted);
        var equals = ArgumentParser.Parse(["--path=src/Pages"], Accepted);

        Assert.True(separated.Succeeded);
        Assert.True(equals.Succeeded);
        Assert.Equal("src/Pages", separated.Arguments!.Value("path"));
        Assert.Equal("src/Pages", equals.Arguments!.Value("path"));
    }

    [Fact]
    public void Keeps_positional_arguments_in_order()
    {
        // 'add page Products' depends on the name arriving as the first positional argument.
        var result = ArgumentParser.Parse(["Products", "--force"], Accepted);

        Assert.True(result.Succeeded);
        Assert.Equal(["Products"], result.Arguments!.Positional);
    }

    [Fact]
    public void Reports_an_unknown_option_as_an_error()
    {
        // Silently ignoring --froce would write a file the developer believed was protected.
        var result = ArgumentParser.Parse(["--froce"], Accepted);

        Assert.False(result.Succeeded);
        Assert.Contains("--froce", result.Error);
        Assert.Contains("--help", result.Error);
    }

    [Fact]
    public void Reports_a_missing_value_as_an_error()
    {
        // '--path' with no value must not swallow the next token or default silently.
        var result = ArgumentParser.Parse(["Products", "--path"], Accepted);

        Assert.False(result.Succeeded);
        Assert.Contains("requires a value", result.Error);
    }

    [Fact]
    public void Reports_a_value_given_to_a_flag_as_an_error()
    {
        // '--force=false' looks like a way to turn a flag off; it is not, and pretending it is
        // would mean a page gets overwritten by someone who thought they were disabling it.
        var result = ArgumentParser.Parse(["--force=false"], Accepted);

        Assert.False(result.Succeeded);
        Assert.Contains("does not take a value", result.Error);
    }

    [Theory]
    [InlineData("-h")]
    [InlineData("--help")]
    public void Accepts_help_in_both_spellings(string spelling)
    {
        // Help is what a stuck developer types, and -h is what half of them type.
        var result = ArgumentParser.Parse([spelling], Accepted);

        Assert.True(result.Succeeded);
        Assert.True(result.Arguments!.Has(ArgumentParser.HelpOption));
    }

    [Fact]
    public void Accepts_version_globally()
    {
        var result = ArgumentParser.Parse(["--version"], Accepted);

        Assert.True(result.Succeeded);
        Assert.True(result.Arguments!.Has("version"));
    }

    [Fact]
    public void Matches_option_names_case_insensitively()
    {
        // A developer typing --Force on a case-insensitive-feeling CLI should not lose the flag.
        var result = ArgumentParser.Parse(["--FORCE"], Accepted);

        Assert.True(result.Succeeded);
        Assert.True(result.Arguments!.Has("force"));
    }

    [Fact]
    public void Treats_a_lone_dash_as_a_positional_argument()
    {
        // '-' is conventionally stdin, not an option; rejecting it would be a wrong error.
        var result = ArgumentParser.Parse(["-"], Accepted);

        Assert.True(result.Succeeded);
        Assert.Equal(["-"], result.Arguments!.Positional);
    }

    [Fact]
    public void Returns_the_first_error_and_no_arguments()
    {
        // A partially parsed command line must never be handed to a command: half a --force is
        // not a --force.
        var result = ArgumentParser.Parse(["--force", "--nope", "--path", "x"], Accepted);

        Assert.False(result.Succeeded);
        Assert.Null(result.Arguments);
        Assert.Contains("--nope", result.Error);
    }
}
