<!-- summary-of: HelpText.cs -->
<!-- source-sha256: 7781de8c6762 -->

# HelpText.cs

The text `--help` prints: every command, every option and the exit-code table, as one hand-written
literal rather than assembled from attributes. It is a contract a person reads, and a test asserts
that each option documented here is a real one, so the help and the parser cannot drift silently.

`Shopify.Blazor.Cli` · `static class` of raw string literals · the CLI's user-facing reference

## Members

| Member | Signature | What it does |
|---|---|---|
| `Full` | `static string Full` | The complete help, returned as one literal. |
| `AddUsage` | `static string AddUsage` | The shorter help for `add` on its own, which is not a command. |

## Behaviour

`Full` is one block, in this order:

1. A one-line description — "a convenience wrapper for the Shopify Blazor Toolkit" — and a usage block
   naming the six invocation forms: `new <Name>`, `add page <Name>`, `add component <Name>`,
   `configure`, `doctor`, and `--help | --version`.
2. A commands block, one paragraph each. `new` creates a project from the `shopify-blazor` template and
   refuses a target directory that exists and is not empty; `add page` writes
   `Components/Pages/<Name>.razor` with a kebab-case route, an interactive render mode and loading,
   empty and error states; `add component` writes a usage snippet for a catalogue component;
   `configure` prints the user-secrets commands and the Partner Dashboard settings; `doctor` reports
   PASS / WARN / FAIL per check.
3. A flat options block. `--include-sample-pages <bool>` and `--include-admin-api-example <bool>` are
   `new`-only and default to `true`; `--output <dir>` is `new`'s parent directory; `--path <dir>` is
   `add page`'s directory (default: the project's `Components/Pages`); `--path <file-or-dir>` is
   `add component`'s target, whose default is to print rather than write; `--project <path>` picks the
   project and defaults to searching upwards; `--nav` adds a navigation entry to a new page;
   `--force` overwrites; `--dry-run` prints exactly what would be written and writes nothing;
   `--json` is `doctor`'s machine-readable report; `--check` validates that `configure`'s settings
   resolve; `-h, --help` and `--version` close the list.
4. "Options may be written as `--option value` or `--option=value`."
5. The exit-code table: `0` success (doctor: nothing FAILed), `1` a check failed or `configure --check`
   found something missing, `2` the command line was wrong, `3` a file exists and `--force` was not
   given, `4` an external process failed (`dotnet new`), `5` a piece of the CLI's own data is missing.
6. A closing paragraph stating that the CLI is never the only way to do anything — every action can be
   taken by hand, and `docs/cli-reference.md` documents the same surface.

`AddUsage` is deliberately much smaller: the `add <page|component> <Name>` form, each subcommand's
options, and a closing line saying that `add` on its own does nothing.

## Gotchas

- **Nothing generates this text.** A new option is invisible in `--help` until someone edits the
  literal, and the test only checks the direction "documented here, present in the parser" — an option
  the parser gained and this file never mentions fails nothing.
- **The exit-code block repeats the numbers from `ExitCodes` by hand.** The two lists have to be edited
  together; the help would otherwise document a code the process never returns.
- **`--path` carries two contracts in one option.** It is a directory for `add page` and a
  file-or-directory for `add component`, with a different default for each. The two usage lines say
  so, but a reader who skims the flat options block will apply the wrong one.
- **`--json` and `--check` sit in the same flat block as the global options**, distinguished only by the
  command named in their description, so the layout reads as though every option applies everywhere.
- **Both members are properties, not constants.** Each access re-evaluates its literal and neither can
  be used in an attribute argument or a `switch` case.
- **`AddUsage` does not repeat the global options.** It points at `--help`; it is not a substitute.

## See also

- [ExitCodes.cs.md](ExitCodes.cs.md) — the constants the exit-code block restates.
- [ArgumentParser.cs.md](ArgumentParser.cs.md) — the options this text has to keep up with.
- [README.md](README.md) — the CLI's own description, alongside this text.
