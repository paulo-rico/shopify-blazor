<!-- summary-of: DoctorCommand.cs -->
<!-- source-sha256: 5b92a9546189 -->

# DoctorCommand.cs

`shopify-blazor doctor` runs six checks over a generated project and prints a PASS/WARN/FAIL report, or
the same report as JSON for CI. Exit code 0 unless something FAILs, so it can gate a build; a WARN never
changes the exit code, because a check that cannot decide must not fail one.

`Shopify.Blazor.Cli.Commands` · the `doctor` command, with the JSON report shapes · no base class

## Members

| Member | Signature | What it does |
|---|---|---|
| `AcceptedOptions` | `static readonly IReadOnlyList<OptionSpec>` | `project` (value), `json` (flag). |
| `.ctor` | `DoctorCommand(IFileSystem, IEnvironment, IConsole)` | `IEnvironment` supplies both the current directory and the framework description the SDK check compares against. |
| `Run` | `int Run(ParsedArguments arguments)` | Locates the project, runs the checks, emits human or JSON, returns 0 or 1. |
| `RunChecks` | `IReadOnlyList<DoctorCheck> RunChecks(ProjectFile project)` | The six checks, in a fixed order, as data — so a test asserts results without capturing the console. |
| `DoctorCheckJson` | `sealed record (string Name, string Status, string Reason, string? Hint)` | One check as `--json` spells it: `name`, `status` (`PASS`/`WARN`/`FAIL`), `reason`, `hint` (null when it passed). |
| `DoctorReportJson` | `sealed record (string Project, bool Healthy, IReadOnlyList<DoctorCheckJson> Checks)` | The whole document: the checked project, whether anything failed, and every check in the order it ran. |
| `DoctorJsonContext` | `internal sealed partial class : JsonSerializerContext` | Source-generated serialiser for the report, with the camel-case policy declared on the type. |

## The six checks

| Name | Passes when | Fails or warns when |
|---|---|---|
| `dotnet-sdk` | `SdkVersionCheck.Evaluate` says the nearest `global.json` is satisfied by the running framework | fails when it is definitively not; warns when it cannot decide |
| `references` | the project references both `Shopify.Blazor` and `Polaris.Blazor` | fails, naming the missing one(s) and what breaks without each |
| `configuration` | `ApiKey`, `ApiSecret` and `AppUrl` all resolve | fails on a committed `ApiSecret`, or on any of the three missing |
| `admin-api-version` | `AdminApiVersionCheck` reports the version current | **warns only** — outdated or missing, never fails |
| `rendermode` | `RenderModeScanner` found `@rendermode InteractiveServer` | fails, naming the first file that has it as evidence when it passes, and the file count scanned when it fails |
| `polaris-interop` | `StaticAssetCheck` found the interop asset reachable | fails when missing, warns when it cannot tell |

## Behaviour that is not in the member list

- **Human output** starts with `Checking <file> in <directory>` — naming the project matters when
  `--project` pointed somewhere else — then one aligned line per check, and an indented `-> hint` line
  for anything that is not PASS. It ends with `N passed, N warned, N failed.` and, when anything
  failed, `Exit code 1: fix the N failure(s) above. See docs/troubleshooting.md.`
- **JSON output** is the serialised `DoctorReportJson`. `healthy` is false as soon as one check is a
  FAIL; WARN does not affect it.
- **When no project is found** the human `FAIL  project  …` line is printed, and with `--json` a
  one-check document is emitted too, with `project` set to the literal `<none>`. Both come out, and the
  exit code is 1.
- **The committed-secret check precedes the missing-setting check** inside `configuration`: a
  credential in git needs rotation, not a value to be filled in, so it wins the single FAIL slot.
- **`admin-api-version` can only warn.** A retired Admin API version does not error — Shopify falls
  forward to an older stable release and answers normally — so the check states the version and lets a
  human compare it against Shopify's version table.
- **Reading the Admin API version tolerates hand-edited settings.** `Shopify:AdminApiVersion` is read
  from every settings file in `ConfigurationReader`'s order with comments and trailing commas allowed,
  the last file that declares one wins, and unparseable JSON means "no version declared" rather than a
  crash.

## Gotchas

- **`Run` takes no `currentDirectory`.** Unlike the `add` commands it reads `IEnvironment.CurrentDirectory`,
  so a test that wants a different starting point must supply the environment, not an argument.
- **`DoctorJsonContext` declares the naming policy on purpose.** A source-generated `JsonSerializerContext`
  otherwise ignores the policy entirely and emits `Name` where a consumer parses `name` — the same trap
  the toolkit records for Shopify's payloads, and here it would make CI silently find nothing.
- **Check names are the JSON contract.** `dotnet-sdk`, `references`, `configuration`,
  `admin-api-version`, `rendermode` and `polaris-interop` are what a CI job greps for; renaming one is a
  breaking change to the report even though nothing fails to compile.
- **`FindGlobalJson` walks up without a boundary** — it can leave the project and find a parent
  checkout's `global.json`, which is why the SDK check's reason names the file it used.
- A WARN-only run exits 0 but still prints hint lines, so "exit 0" does not mean the report was clean.

## See also

- [../DoctorCheck.cs.md](../DoctorCheck.cs.md) — the result record, `CheckStatus` and `StatusText`.
- [../SdkVersionCheck.cs.md](../SdkVersionCheck.cs.md), [../AdminApiVersionCheck.cs.md](../AdminApiVersionCheck.cs.md),
  [../RenderModeScanner.cs.md](../RenderModeScanner.cs.md) and [../StaticAssetCheck.cs.md](../StaticAssetCheck.cs.md)
  — the four helpers the checks delegate to.
- [ConfigureCommand.cs.md](ConfigureCommand.cs.md) — the same configuration rules, with the exact
  commands to fix them.
- [../ProjectLocator.cs.md](../ProjectLocator.cs.md) — how `--project` is resolved.
