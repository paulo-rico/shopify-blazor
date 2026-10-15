using System.Collections.ObjectModel;

namespace Shopify.Blazor.Cli;

/// <summary>Whether an option is a bare flag or consumes the next argument as its value.</summary>
public enum OptionKind
{
    /// <summary>A flag: present or absent, never followed by a value.</summary>
    Flag,

    /// <summary>An option that requires a value, given as <c>--opt value</c> or <c>--opt=value</c>.</summary>
    Value,
}

/// <summary>One accepted option.</summary>
/// <param name="Name">The long name, without leading dashes (for example <c>dry-run</c>).</param>
/// <param name="Kind">Whether the option takes a value.</param>
public sealed record OptionSpec(string Name, OptionKind Kind);

/// <summary>The arguments a command was invoked with, after parsing.</summary>
public sealed class ParsedArguments
{
    private readonly IReadOnlyDictionary<string, string?> _options;

    private ParsedArguments(IReadOnlyDictionary<string, string?> options, IReadOnlyList<string> positional)
    {
        _options = options;
        Positional = positional;
    }

    /// <summary>Positional arguments, in the order they appeared.</summary>
    public IReadOnlyList<string> Positional { get; }

    /// <summary>Options that were supplied, with their values (<c>null</c> for a flag).</summary>
    public IReadOnlyDictionary<string, string?> Options => _options;

    /// <summary>Whether <paramref name="name"/> was supplied.</summary>
    public bool Has(string name) => _options.ContainsKey(name);

    /// <summary>The value of <paramref name="name"/>, or <c>null</c> when it was not supplied.</summary>
    public string? Value(string name) => _options.TryGetValue(name, out var value) ? value : null;

    internal static ParsedArguments Create(
        IReadOnlyDictionary<string, string?> options,
        IReadOnlyList<string> positional) =>
        new(new ReadOnlyDictionary<string, string?>(new Dictionary<string, string?>(options, StringComparer.Ordinal)),
            positional);
}

/// <summary>The outcome of parsing: either parsed arguments or one message explaining the first problem.</summary>
/// <param name="Arguments">The parsed arguments, or <c>null</c> when parsing failed.</param>
/// <param name="Error">The error message, or <c>null</c> when parsing succeeded.</param>
public sealed record ParseResult(ParsedArguments? Arguments, string? Error)
{
    /// <summary>Whether parsing succeeded.</summary>
    public bool Succeeded => Arguments is not null;

    /// <summary>A successful result.</summary>
    public static ParseResult Ok(ParsedArguments arguments) => new(arguments, null);

    /// <summary>A failed result carrying a message.</summary>
    public static ParseResult Fail(string error) => new(null, error);
}

/// <summary>
/// A deliberately small hand-rolled argument parser.
/// </summary>
/// <remarks>
/// The repository keeps its dependency surface tiny on purpose: a full CLI framework would be the
/// largest dependency in the toolkit for four commands. This handles exactly what is documented —
/// <c>--flag</c>, <c>--option value</c>, <c>--option=value</c> and <c>-h/--help</c> — and reports
/// anything else as an error rather than guessing, because a silently ignored option is how
/// <c>--force</c> gets eaten by a typo and a file gets overwritten.
/// </remarks>
public static class ArgumentParser
{
    /// <summary>The short spelling of <c>--help</c>, which every command accepts.</summary>
    public const string HelpOption = "help";

    /// <summary>Global options accepted by every command.</summary>
    public static readonly IReadOnlyList<OptionSpec> GlobalOptions =
    [
        new(HelpOption, OptionKind.Flag),
        new("version", OptionKind.Flag),
    ];

    /// <summary>
    /// Parses <paramref name="args"/> against <paramref name="accepted"/>. Names are matched
    /// case-insensitively; the first error wins and no later argument is inspected.
    /// </summary>
    public static ParseResult Parse(IReadOnlyList<string> args, IEnumerable<OptionSpec> accepted)
    {
        var specs = new Dictionary<string, OptionSpec>(StringComparer.OrdinalIgnoreCase);

        foreach (var spec in GlobalOptions.Concat(accepted))
        {
            specs[spec.Name] = spec;
        }

        var options = new Dictionary<string, string?>(StringComparer.Ordinal);
        var positional = new List<string>();

        for (var index = 0; index < args.Count; index++)
        {
            var argument = args[index];

            if (!IsOption(argument))
            {
                positional.Add(argument);
                continue;
            }

            var (name, inlineValue) = SplitOption(argument);

            // `--` alone is a bare separator with no name and no meaning here.
            if (name.Length == 0)
            {
                return ParseResult.Fail($"'{argument}' is not a valid option.");
            }

            // -h is the one short option worth supporting: help is what a stuck developer types.
            if (name == "h")
            {
                name = HelpOption;
            }

            if (!specs.TryGetValue(name, out var spec))
            {
                return ParseResult.Fail(
                    $"Unknown option '{argument}'. Run with --help to see the options this command accepts.");
            }

            // The spec's own spelling becomes the key, so a command asked for "--Force" still sees
            // the option it declared as "force".
            name = spec.Name;

            if (spec.Kind == OptionKind.Flag)
            {
                if (inlineValue is not null)
                {
                    return ParseResult.Fail($"Option '--{name}' does not take a value.");
                }

                options[name] = null;
                continue;
            }

            if (inlineValue is not null)
            {
                options[name] = inlineValue;
                continue;
            }

            if (index + 1 >= args.Count)
            {
                return ParseResult.Fail($"Option '--{name}' requires a value, for example '--{name} <value>'.");
            }

            index++;
            options[name] = args[index];
        }

        return ParseResult.Ok(ParsedArguments.Create(options, positional));
    }

    /// <summary>Whether the argument is an option rather than a positional value.</summary>
    private static bool IsOption(string argument) =>
        argument.Length > 1 && argument[0] == '-' && argument != "-";

    /// <summary>Splits <c>--name=value</c> into its two halves, stripping any leading dashes.</summary>
    private static (string Name, string? Value) SplitOption(string argument)
    {
        var trimmed = argument.TrimStart('-');
        var separator = trimmed.IndexOf('=');

        return separator < 0
            ? (trimmed, null)
            : (trimmed[..separator], trimmed[(separator + 1)..]);
    }
}
