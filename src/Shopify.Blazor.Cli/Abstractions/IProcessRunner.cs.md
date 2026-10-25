<!-- summary-of: IProcessRunner.cs -->
<!-- source-sha256: e4479e1274a6 -->

# IProcessRunner.cs

The seam between the CLI and the one external process it runs — `dotnet new` — plus the real
implementation and the result record both sides share. Tests substitute the interface so no template
engine has to be installed, and a missing executable comes back as a result rather than an exception.

`Shopify.Blazor.Cli.Abstractions` · process seam, result record and live runner · no base class

## Members

| Member | Signature | What it does |
|---|---|---|
| `ProcessResult` | `sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError)` | What a run left behind: the exit code and both captured streams. |
| `ProcessResult.Succeeded` | `bool` (computed) | `ExitCode == 0`, nothing more — it does not mean the process produced anything useful. |
| `IProcessRunner.Run` | `ProcessResult Run(string fileName, IReadOnlyList<string> arguments, string workingDirectory)` | Runs the process and waits for it. Never throws on a non-zero exit code, so the caller decides what to print. |
| `ProcessRunner` | `sealed class ProcessRunner(TextWriter output)` | The real runner; `output` receives every line the child writes, as it arrives. |
| `ProcessRunner.Run` | same signature | Starts the process with `UseShellExecute = false`, redirects stdout and stderr, echoes each line to `output` while also capturing it, waits for exit, and returns the three fields. |

## Behaviour that is not in the member list

- **Output is both echoed and captured.** A line goes to the `TextWriter` the runner was constructed
  with **and** into the result, so `NewCommand` can stream `dotnet new`'s progress while still
  inspecting the text afterwards — which it does, to recognise "No templates found".
- **Arguments go through `ArgumentList`, not a command-line string.** Each element is passed as one
  argument, so a path containing a space needs no quoting and cannot be split. (The *display* copy of
  the command line that `NewCommand` prints is a separate, hand-quoted string.)
- **A process that could not start is a result.** `Process.Start` returning `null` and any exception
  thrown while starting both become `ProcessResult(-1, "", message)`; the exception message lands in
  `StandardError`, which is where a caller's error report reads it from.
- **Both streams are read asynchronously** with `BeginOutputReadLine` / `BeginErrorReadLine` before
  `WaitForExit`, which is what stops a chatty child from filling a pipe buffer and deadlocking.

## Gotchas

- **`ExitCode == -1` is ambiguous.** It is the "could not start" sentinel *and* a legitimate exit code;
  the only way to tell them apart is that the sentinel always carries an empty `StandardOutput` and an
  exception message in `StandardError`. `Succeeded` is false either way.
- **There is no timeout and no cancellation.** `Run` blocks for as long as the child lives, so a
  `dotnet new` that waits on input hangs the CLI with no way out but Ctrl-C.
- **Standard input is not redirected.** Only stdout and stderr are; the child inherits the console's
  stdin, so a template that prompts writes its question to a stream nothing captures and waits.
- **A partial line is lost.** Each handler returns early when `e.Data` is `null`, and the line-based
  readers only deliver complete lines, so a child that writes without a trailing newline contributes
  nothing to `StandardOutput` until it exits.
- The returned text keeps the line terminators the reader appends, so callers matching on stderr are
  matching lines, not the exact bytes the process wrote.

## See also

- [../Commands/NewCommand.cs.md](../Commands/NewCommand.cs.md) — the only caller; builds the argument
  list and interprets the failure.
- [IFileSystem.cs.md](IFileSystem.cs.md) — the other seam, for everything that is not a process.
- [IEnvironment.cs.md](IEnvironment.cs.md) — supplies the working directory the process is started in.
