<!-- summary-of: ConfigureCommand.cs -->
<!-- source-sha256: 22b078c9c3fa -->

# ConfigureCommand.cs

`shopify-blazor configure` prints the Partner Dashboard and credential steps; `--check` verifies that
the three settings resolve without printing any of them. The check names which configuration layer
supplied each value, because an environment variable silently overrides user secrets.

`Shopify.Blazor.Cli.Commands` · the `configure` command · no base class

## Members

| Member | Signature | What it does |
|---|---|---|
| `AcceptedOptions` | `static readonly IReadOnlyList<OptionSpec>` | `check` (flag), `project` (value). |
| `ExampleScopes` | `const string` = `"read_products,write_products"` | The scopes the documentation assumes; the text says the Dashboard is authoritative. |
| `.ctor` | `ConfigureCommand(IFileSystem, IEnvironment, IConsole)` | `IEnvironment` supplies environment variables to the check. |
| `Run` | `int Run(ParsedArguments arguments, string currentDirectory)` | Without `--check`, prints the guide (exit 0). With `--check`, validates the project and returns 0 or 1. |
| `PrintGuide` | `void PrintGuide(ProjectFile? project)` | The numbered guide, public so the caller can print it without a project. |
| `RunCheck` | `int RunCheck(ProjectFile project)` | Resolves the three settings and the committed-secret scan; 0 only when nothing is missing and nothing is committed. |
| `UserSecretsCommands` | `IReadOnlyList<string> UserSecretsCommands(ProjectFile? project)` | The four `dotnet user-secrets` commands, each suffixed `--id <UserSecretsId>` when the project file declares one. |
| `ReadUserSecretsId` | `private string?` | Reads `<UserSecretsId>` out of the `.csproj` with a generated regex; `null` when the file or the element is absent. |

## Behaviour that is not in the member list

* **The guide** covers, in order: creating the app in the Partner Dashboard; the App URL and the
  `/signin-oidc` redirect URL; the minimum scopes; where the client ID and secret are; that the app must
  be served over public HTTPS because the Admin will not frame a localhost URL; user secrets for local
  work versus the three `SHOPIFY_*` environment variables; the warning that a key from a different
  Shopify app rejects every session token as "another app"; and the verify step
  (`configure --check`, then `doctor`). It closes by noting the same values can come from
  `appsettings.json` (except the secret), `launchSettings.json` or the shell.
* **A check line** is either `ok    Shopify:ApiKey   set (source)` or `FAIL  Shopify:ApiSecret not set
  (checked the SHOPIFY_API_SECRET variable and appsettings*.json)`, and the failing keys are collected
  for a single `Missing: …` line that points back at the guide.
- **A committed secret is its own failure**, checked after the three settings: the message names the
  file, says to rotate the key in the Partner Dashboard, and says to keep `appsettings.json` empty. It
  fails the command even when every setting resolves, because a credential in git is worse than a
  missing one and needs a different action.
- **`--check` outside a project** prints `FAIL  project  <locator message>` and returns 1 rather than
  falling back to the guide; run without `--check` for the generic steps.

## Gotchas

- **No secret is ever printed — only presence and provenance.** The output ends up in terminals, CI
  logs and pasted issue reports, and a secret that appears once in a log has to be rotated. Any change
  that prints `setting.Value` breaks the point of the command.
- **A missing `--id` makes `dotnet user-secrets` write into a store the app never reads, silently.**
  That is why `UserSecretsCommands` appends the project's own id when it can find one, and why the
  guide prints those commands rather than a generic form.
- **The check only proves a value is non-empty.** It cannot tell a key belonging to another app from
  the right one; that failure surfaces later as every session token being rejected, which is why the
  guide warns about it instead.
- The environment layer wins over user secrets, so a stale `SHOPIFY_API_KEY` in the shell makes a
  correct user secret look wrong. The `(source)` on each `ok` line is the only way to see which layer
  answered.
- The guide is printed even when the project was not found (`PrintGuide(search.Project)` accepts
  `null`); only `--check` insists on a project.

## See also

- [../ConfigurationReader.cs.md](../ConfigurationReader.cs.md) — resolution order and the
  committed-secret scan.
- [DoctorCommand.cs.md](DoctorCommand.cs.md) — the same settings as one of six checks.
- [../ExitCodes.cs.md](../ExitCodes.cs.md) — `0` success, `1` failure.
- [NewCommand.cs.md](NewCommand.cs.md) — prints the short form of these steps after scaffolding.
