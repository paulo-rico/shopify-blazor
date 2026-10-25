<!-- summary-of: ExitCodes.cs -->
<!-- source-sha256: 78b6bfeb4f1b -->

# ExitCodes.cs

The exit codes every command returns, as named constants, because a script that gates a build on
`shopify-blazor doctor` depends on the numbers and not on the wording. The same six numbers are
documented in `docs/cli-reference.md` and asserted by the tests.

`Shopify.Blazor.Cli` · `static class` of `const int` · the CLI's process-exit contract

## Members

| Member | Value | What it means |
|---|---|---|
| `Success` | `0` | Everything asked for happened. For `doctor`, nothing FAILed — warnings still exit 0. |
| `Failure` | `1` | A check failed, or the command did something that needs attention. Also `configure --check` finding something missing. |
| `Usage` | `2` | The command line itself was wrong: an unknown option, a missing value, a bad name. |
| `AlreadyExists` | `3` | A file already exists and `--force` was not given. |
| `ProcessFailed` | `4` | An external process the command depends on failed — `dotnet new`. |
| `InternalError` | `5` | A required piece of the CLI's own data — the component catalogue, say — is missing. |

## Behaviour

- The codes are a contract, not diagnostics: `help` prints the same table, `docs/cli-reference.md`
  documents it, and the tests assert the values, so a number that moves is a breaking change for
  anything wrapping the CLI.
- Each code names a different *kind* of wrong, which is the point of having six rather than one:
  `2` is the caller's spelling, `3` is a refusal that `--force` resolves, `4` is a tool the CLI shells
  out to, and `5` is a broken installation rather than a broken project.
- `AlreadyExists` is the only code that reports success-of-intent with refusal-of-action: the file is
  fine, it simply was not touched.

## Gotchas

- **`0` does not mean "clean" for `doctor`.** A report full of WARNs exits `0`, so a script that wants
  "no warnings" has to read the output or the `--json` document; the exit code cannot express it.
- **Collapsing the codes loses the one signal a caller can act on.** `if [ $? -ne 0 ]` cannot tell `3`
  (add `--force`) from `1` (fix the project), which is exactly the distinction a wrapper script needs.
- **The numbers are duplicated by hand inside `HelpText.Full`.** Nothing generates the help table from
  these constants, so changing a value here without editing that literal leaves the CLI documenting a
  code it no longer returns.
- **A code outside this set has no meaning to a caller.** Adding a seventh is a change to a published
  contract, not an internal detail.

## See also

- [HelpText.cs.md](HelpText.cs.md) — the printed exit-code table that must stay in step with these.
- [Commands/DoctorCommand.cs.md](Commands/DoctorCommand.cs.md) — the command whose `0` is "nothing FAILed".
- [Commands/ConfigureCommand.cs.md](Commands/ConfigureCommand.cs.md) — where `--check` returns `1`.
