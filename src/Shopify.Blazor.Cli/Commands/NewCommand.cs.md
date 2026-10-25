<!-- summary-of: NewCommand.cs -->
<!-- source-sha256: 4d35ecd4e343 -->

# NewCommand.cs

`shopify-blazor new <Name>` — scaffolds a project by running `dotnet new shopify-blazor`, then prints
what has to happen before the app will run. It is a wrapper over the template and nothing more, so
there is no second scaffold to keep in step with the template.

`Shopify.Blazor.Cli.Commands` · the `new` command · no base class

## Members

| Member | Signature | What it does |
|---|---|---|
| `TemplateName` | `const string` = `"shopify-blazor"` | The template instantiated; also what the not-installed hint tells you to install. |
| `EnvironmentVariables` | `static readonly IReadOnlyList<string>` | `SHOPIFY_API_KEY`, `SHOPIFY_API_SECRET`, `SHOPIFY_APP_URL`, in the order the README lists them. |
| `AcceptedOptions` | `static readonly IReadOnlyList<OptionSpec>` | `include-sample-pages` (value), `include-admin-api-example` (value), `output` (value), `dry-run` (flag). |
| `.ctor` | `NewCommand(IFileSystem, IProcessRunner, IEnvironment, IConsole)` | The filesystem is used read-only, only to inspect the target directory; the process runner does the work. |
| `Run` | `int Run(ParsedArguments arguments)` | The whole command. Returns `Success` (0), `Usage` (2), `AlreadyExists` (3) or `ProcessFailed` (4). |
| `PrintNextSteps` | `private void PrintNextSteps(string projectDirectory)` | The five steps: `cd`, create the app in the Dashboard, credentials (environment variables or user secrets), read the generated README, then `doctor` and `dotnet run`. |
| `TryReadBoolean` | `private bool TryReadBoolean(ParsedArguments, string name, bool defaultValue, out bool value)` | Parses an option with `bool.TryParse` only; an unparseable value prints an error and fails the command. |
| `Quote` | `private static string Quote(string argument)` | Wraps an argument in double quotes for the *display* copy of the command line when it contains a space. |

## Behaviour that is not in the member list

The order of decisions in `Run`:

1. No positional → a usage error naming the expected spelling; more than one → an error that quotes the
   count and every name given. Both exit 2.
2. `NameHelper.IsValidComponentName` — a name that cannot be a C# type or a file name is a usage error.
3. `--include-sample-pages` and `--include-admin-api-example` are read as booleans, **defaulting to
   true**; the first bad value stops the command (exit 2) before anything else happens.
4. `--output` (resolved against the environment's current directory) or a directory named after the
   argument is the output directory; with `--output`, the target is `<output>/<name>`.
5. An existing **non-empty** target directory is refused (exit 3): "the difference between a scaffold
   and a merge". An existing empty directory is allowed.
6. The template arguments are built and shown as `dotnet new shopify-blazor --name <PascalCase> --output
   <dir> --include-sample-pages <true|false> --include-admin-api-example <true|false>`.
7. `--dry-run` prints the command, the target directory and the next steps, and writes nothing (exit 0).
8. Otherwise the process runs. On failure the exit code is printed and **4** is returned; stderr is
   searched for "No templates found" or "not found", and a match adds the fix:
   `dotnet new install Shopify.Blazor.Templates`.
9. On success the next steps are printed, including both credential routes and a pointer to
   `shopify-blazor doctor`.

## Gotchas

- **The project name and the directory name differ.** The template gets `--name NameHelper.ToPascalCase(name)`
  while the default directory is the argument as typed, so `new my-app` creates directory `my-app`
  holding project `MyApp`.
- **`--output` is a parent directory, not the project directory.** The target becomes
  `<output>/<name>`, and the name is the raw argument, not the PascalCase form.
- **A dry run can still refuse.** The non-empty-directory check comes before the dry-run branch, so you
  cannot preview into an occupied directory; the run exits 3 instead.
- **The template-install hint depends on English stderr.** It is a substring match against
  `StandardError`, so a localised toolchain or a differently worded failure loses the hint and leaves
  only the exit code.
- **Nothing here writes a project file.** The command touches the filesystem read-only; every file comes
  from the template engine, which is why a template defect cannot be fixed from this class.
- **The generated project is not built by the solution**, and this command does not build it either —
  the next steps end at `doctor` and `dotnet run`.

## See also

- [../Abstractions/IProcessRunner.cs.md](../Abstractions/IProcessRunner.cs.md) — how the process is
  started and why a missing `dotnet` is a result rather than an exception.
- [ConfigureCommand.cs.md](ConfigureCommand.cs.md) — the full version of the credential steps.
- [DoctorCommand.cs.md](DoctorCommand.cs.md) — what step 5 actually runs.
- [../NameHelper.cs.md](../NameHelper.cs.md) — `ToPascalCase` and the name validation.
