<!-- summary-of: ArgumentParser.cs -->
<!-- source-sha256: 180e6feeffde -->

# ArgumentParser.cs

The hand-rolled command-line parser behind every `shopify-blazor` command: it understands `--flag`,
`--option value`, `--option=value` and `-h`/`--help`, and rejects anything else rather than guessing. The
repository keeps it deliberately tiny because a full CLI framework would be the largest dependency in the
toolkit for four commands.

`Shopify.Blazor.Cli` · `static class` + three supporting types · no base class

## Members

### Supporting types

| Type | Shape | Meaning |
|---|---|---|
| `OptionKind` | `enum` | `Flag` (present or absent) or `Value` (takes the next argument or an inline `=value`). |
| `OptionSpec` | `sealed record (string Name, OptionKind Kind)` | One accepted option; `Name` is the long name **without** leading dashes. |
| `ParsedArguments` | `sealed class` | `Positional` (in order), `Options` (name → value, `null` for a flag), `Has(name)`, `Value(name)`. Constructed only through the internal `Create`. |
| `ParseResult` | `sealed record (ParsedArguments? Arguments, string? Error)` | `Succeeded` is `Arguments is not null`; also `Ok(arguments)` and `Fail(error)` factories. |

### `ArgumentParser`

| Member | Signature | What it does |
|---|---|---|
| `HelpOption` | `public const string` = `"help"` | The canonical name `-h` is rewritten to. |
| `GlobalOptions` | `public static readonly IReadOnlyList<OptionSpec>` | `help` and `version`, both flags, accepted by every command without being declared by it. |
| `Parse` | `static ParseResult Parse(IReadOnlyList<string> args, IEnumerable<OptionSpec> accepted)` | Parses `args` against `accepted` plus the global options. Matches names case-insensitively; returns the first error and inspects nothing after it. |

## Behaviour

The grammar it accepts:

| Form | Result |
|---|---|
| `positional` | Added to `Positional` in order. |
| `-` | Positional — the one argument starting with a dash that is not treated as an option. |
| `--flag` | `Has("flag")` true, `Value("flag")` null. |
| `--option value` | `Value("option")` is `value`; the next argument is consumed whatever it looks like. |
| `--option=value` | The same, without consuming the next argument. |
| `--option=` | The value is the empty string, not null. |
| `-h`, `--h`, `--help`, `-help` | `Has("help")` — any number of leading dashes is accepted, and `h` is renamed to `help`. |
| `--` | An error: it trims to an empty name, which this parser has no meaning for. |
| `--unknown` | An error naming the option and pointing at `--help`. |
| `--flag=true` | An error: a flag "does not take a value". |

- **The table is the union of the globals and the command's options**, keyed case-insensitively. A command
  that declares an option called `help` or `version` overrides the global spec, including its kind.
- **The spec's own spelling becomes the key.** `--Force` is accepted against a spec named `force`, and the
  result is stored as `force`.
- **The first error wins.** `Parse` returns the moment it meets an unknown option, a flag with a value, or
  a value option at the end of the arguments; later arguments are not inspected, so only one problem is
  ever reported per run.
- **A repeated option keeps the last value**; a repeated flag is a no-op second assignment of null.
- **Nothing is validated beyond shape.** An empty string and a string that looks like another option are
  both perfectly good values here; interpreting them is the command's job.

## Gotchas

- **A value option swallows the next option.** `--path --force` sets `path` to `"--force"` and never sets
  `force`, with no error — the parser has no way to know the next token was meant as a flag. It is the one
  silent failure this design cannot prevent.
- **Lookups are case-sensitive even though matching is not.** The key stored is the declared spelling, so
  after `--Force` the correct call is `Has("force")`; `Has("Force")` returns false.
- **`Value(name)` cannot distinguish "absent" from "flag"**, because both are `null`. Call `Has` first when
  the difference matters.
- **`-v` is not `--version`.** Only `-h` is special-cased; every other short spelling is treated as a long
  name and therefore rejected, so a tool that documents `-v` would be lying.
- **A negative number is an option.** `-1` passes `IsOption`, becomes the name `1`, and fails as unknown.
  It only survives as a *value* consumed by a preceding value option (`--count -1`); there is no
  end-of-options marker such as `--` to make it positional.
- **An empty inline value is a supplied value.** `--opt=` yields `""`, which `Has` reports as present and
  a command must reject explicitly if it needs a non-empty value.

## See also

- [Commands/AddComponentCommand.cs.md](Commands/AddComponentCommand.cs.md) — declares options and reads `ParsedArguments`.
- [Commands/DoctorCommand.cs.md](Commands/DoctorCommand.cs.md) — uses `Has`/`Value` for `--project` and `--json`.
- [Commands/AddPageCommand.cs.md](Commands/AddPageCommand.cs.md) — a command whose option set includes value options.
