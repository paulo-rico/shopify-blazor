<!-- summary-of: Program.cs -->
<!-- source-sha256: 66c0dfc790f1 -->

# Program.cs

The CLI's entry point: a top-level-statements program that reads the first argument, sends the rest to
one command class, and turns the result into an exit code. It owns dispatch, help and version handling
only — every decision lives in a command object a test can drive without a process, a disk or a network.

`Shopify.Blazor.Cli` · executable entry point, top-level statements · no class, no base type

## Members

These are local functions inside the top-level statement file, not public API: they exist so the last
statement can be `return Run(args);`.

| Member | Signature | What it does |
|---|---|---|
| `Run` | `int Run(string[] argv)` | The dispatcher. Empty argv prints `HelpText.Full` and succeeds; `-h`/`--help`/`help` and `--version`/`-v` are handled inline; a token beginning `-` is a usage error; otherwise `new`, `add`, `configure` and `doctor` are matched, and anything else is an unknown command. |
| `DispatchAdd` | `int DispatchAdd(string[] rest)` | `add` has one level more: no subject prints `HelpText.AddUsage` and returns the usage code, `-h`/`--help` prints the same usage and succeeds, and the subject is `page` or `component`. |
| `Dispatch` | `int Dispatch(IReadOnlyList<OptionSpec> acceptedOptions, string[] commandArguments, Func<ParsedArguments,int> run)` | Parses arguments against the command's accepted options, reports a parse error, handles `--help`, then calls the command. |
| `UnknownCommand` | `int UnknownCommand(string command)` | Prints `error: '<x>' is not a command. Run 'shopify-blazor --help' for the list.` and returns the usage code. |
| `UnknownAddSubject` | `int UnknownAddSubject(string subject)` | The same for a bad `add` subject, naming `page` and `component` as the supported ones, then reprinting `HelpText.AddUsage`. |
| `VersionText` | `string VersionText()` | The assembly's `InformationalVersion` with any `+<commit>` suffix stripped, falling back to the assembly version and then `0.0.0`. |

## Behaviour

- The three services are built once at the top: `SystemConsole`, `PhysicalFileSystem` and
  `SystemEnvironment`. Every command receives them by interface, which is what makes the CLI testable.
- `new` is the only command also given a `ProcessRunner`, constructed around `Console.Out` — the single
  place in the file that touches the real console instead of the injected `IConsole`.
- Command objects are constructed inside the lambda passed to `Dispatch`, so construction happens only
  after parsing succeeds. A mistyped flag cannot reach a command, and no command needs its own option
  validation.
- `Dispatch` writes `"error: " + parsed.Error` on a parse failure; the `error:` prefix is added there,
  not by `ArgumentParser`.
- The only exit codes this file produces are `ExitCodes.Success` and `ExitCodes.Usage`; every other code
  comes back from the command. The process exit code is whatever `Run` returns.

## Gotchas

- **Only `argv[0]` is the command.** `shopify-blazor --json doctor` fails with `'--json' is not a
  command.` because the first token begins with `-`; flags must follow the command.
- **`--help` after a command prints the whole help, not that command's usage.** `Dispatch` writes
  `HelpText.Full` whenever the parsed arguments carry the help option, so `shopify-blazor doctor --help`
  lists every command. `add --help` is the exception only because `DispatchAdd` handles it before parsing.
- **Command names are matched exactly and case-sensitively** by a string switch, so `Doctor` and `doc`
  are unknown commands.
- **Bare `shopify-blazor` exits 0** after printing help. A CI script that checks the exit code reads that
  as success even though no command ran.
- **`VersionText` only strips a suffix when the `+` is not the first character**, so a version string
  beginning with `+` is returned whole.
- **`-v` is the version flag only as the first token.** After a command it belongs to that command's
  option parser.

## See also

- [Commands/NewCommand.cs.md](Commands/NewCommand.cs.md),
  [Commands/AddPageCommand.cs.md](Commands/AddPageCommand.cs.md),
  [Commands/AddComponentCommand.cs.md](Commands/AddComponentCommand.cs.md) — the `new` and `add` branches.
- [Commands/DoctorCommand.cs.md](Commands/DoctorCommand.cs.md),
  [Commands/ConfigureCommand.cs.md](Commands/ConfigureCommand.cs.md) — the `doctor` and `configure`
  branches and the report shapes.
- [Abstractions/IFileSystem.cs.md](Abstractions/IFileSystem.cs.md),
  [Abstractions/IEnvironment.cs.md](Abstractions/IEnvironment.cs.md) — the two seams built at the top.
