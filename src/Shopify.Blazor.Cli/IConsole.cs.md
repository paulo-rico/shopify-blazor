<!-- summary-of: IConsole.cs -->
<!-- source-sha256: 5ab26405b08d -->

# IConsole.cs

The CLI's entire output surface, three write methods behind an interface so a command can be asserted
on without capturing process stdout — and, the reason the seam exists, so a test can assert that a
secret was **not** printed. `SystemConsole` is the real implementation and writes to `Console.Out`.

`Shopify.Blazor.Cli` · `interface` + `SystemConsole` implementation · the CLI's stdout seam

## Members

| Member | Signature | What it does |
|---|---|---|
| `WriteLine` | `void WriteLine(string text)` | Writes a line. |
| `WriteLine` | `void WriteLine()` | Writes an empty line. |
| `Write` | `void Write(string text)` | Writes text without a trailing newline. |
| `SystemConsole` | `sealed class SystemConsole : IConsole` | All three members forwarded one-to-one to `Console.Out`. |

## Behaviour

- A command takes `IConsole` and calls nothing else, so a fake that records lines is a complete stand-in
  for the real console: the assertions are about what the developer would have seen, not about internal
  state.
- The interface is narrower than `System.Console` on purpose. There is no colour, no verbosity level, no
  cursor control and no flush, so every command's output is plain text in one stream and two commands
  cannot disagree about whether to colourise.
- `SystemConsole` uses `Console.Out` exclusively — it never touches `Console.Error` and never reads
  `Console.In`.

## Gotchas

- **Everything goes to one stream.** With no `WriteError` member, a `doctor --json` document and the
  messages around it arrive on stdout together. A CI step that pipes stdout into `jq` has to trust that
  nothing else was written, and nothing in this interface lets a command keep them apart.
- **There is no read or prompt member.** No command can ask a question, which is why anything ambiguous
  must be expressed as an option or as a refusal — an interactive "overwrite? y/n" is not expressible
  here.
- **There is no interactivity or redirection signal** (`IsOutputRedirected` and the like), so a command
  cannot vary its output for a pipe; the same lines appear in a terminal and in a log file.
- **`WriteLine()` and `Write("")` are not equivalent.** The first emits a newline and the second emits
  nothing at all, so a blank separator written with `Write` silently disappears.
- **`SystemConsole` inherits the process's output encoding.** The interface has no way to state one, so a
  character the console code page cannot represent is lost in the real console while a fake test still
  sees it.

## See also

- [Program.cs.md](Program.cs.md) — the composition root that supplies `SystemConsole`.
- [Commands/DoctorCommand.cs.md](Commands/DoctorCommand.cs.md) — a command whose output a test asserts line by line.
- [Abstractions/IFileSystem.cs.md](Abstractions/IFileSystem.cs.md) — the sibling test seam, for the disk instead of the screen.
