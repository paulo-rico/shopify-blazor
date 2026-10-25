<!-- summary-of: ConfigurationReader.cs -->
<!-- source-sha256: 6583bb14bf02 -->

# ConfigurationReader.cs

Reads the three `Shopify:*` settings the way the running app resolves them — environment variable first,
then `appsettings.json` — and reports which layer supplied each value, because an environment variable
silently overriding a correct-looking settings file is the reason "another app" session-token failures are
so hard to diagnose. It also finds an API secret that has been committed to git.

`Shopify.Blazor.Cli` · `static class` + `ResolvedSetting` and `ResolvedConfiguration` records

## Members

### Records

| Type | Shape | Meaning |
|---|---|---|
| `ResolvedSetting` | `sealed record (string? Value, string? Source)` | One setting, its resolved value and a human-readable source. `IsPresent` is `!string.IsNullOrWhiteSpace(Value)`. |
| `ResolvedConfiguration` | `sealed record (ResolvedSetting ApiKey, ResolvedSetting ApiSecret, ResolvedSetting AppUrl)` | The three settings. `IsComplete` requires all three to be present — including `AppUrl`. |

### `ConfigurationReader`

| Member | Signature | What it does |
|---|---|---|
| `Settings` | `public static readonly IReadOnlyList<(string Key, string EnvironmentVariable)>` | The three key/environment-variable pairs: `ApiKey`/`SHOPIFY_API_KEY`, `ApiSecret`/`SHOPIFY_API_SECRET`, `AppUrl`/`SHOPIFY_APP_URL`. |
| `Resolve` | `static ResolvedConfiguration Resolve(IFileSystem fileSystem, IEnvironment environment, string projectDirectory)` | Resolves each setting: a non-blank environment variable wins, otherwise the merged settings files. |
| `FindCommittedSecrets` | `static IReadOnlyList<(string File, string Value)> FindCommittedSecrets(IFileSystem fileSystem, string projectDirectory)` | Every non-blank `Shopify:ApiSecret` in a settings file, with the file's **name** only. |
| `SettingsFiles` | `static IReadOnlyList<string> SettingsFiles(IFileSystem fileSystem, string projectDirectory)` | The `appsettings*.json` files directly in the project directory, ordered by path with `StringComparer.Ordinal`. |

Private helpers: `ReadAppSettings` accumulates the `Shopify` string properties across files,
`ReadShopifySection` parses one file, and `ReadSecret` looks for `ApiSecret`.

## Behaviour

- **Resolution order per setting:** `environment.GetEnvironmentVariable(name)`, used only when it is not
  null or whitespace; then the merged file value; otherwise `(null, null)`. The source strings are
  `"the environment variable <NAME>"` and the literal `"appsettings.json"`.
- **Files are merged, not chosen.** Every `appsettings*.json` in the directory is parsed and its `Shopify`
  section's string properties are overwritten into one case-insensitive dictionary, in file order, last
  write winning. The intended order is base file first, a more specific file later; the gotcha below shows
  why it does not work out that way.
- **Parsing tolerates JSON comments and trailing commas**, and only string-valued properties are read. A
  nested object such as `Shopify:AppBridge` is skipped, because the section is enumerated one level deep.
- **A malformed settings file contributes nothing.** `JsonException` is swallowed with the comment that a
  doctor run should not be derailed by a stray syntax error in JSON; the caller simply sees the setting as
  missing.
- **The committed-secret check treats an empty string as a placeholder** — only a non-blank `ApiSecret`
  counts as leaked. The value is returned so it can be compared, and must never be printed.

## Gotchas

- **Ordinal ordering puts the base file last, so the base file wins.** `StringComparer.Ordinal` sorts
  `appsettings.Development.json` before `appsettings.json` (`D` < `j`), and `ReadAppSettings` lets the
  *later* file override — so a `Development` override is discarded and the base value stands. The inline
  comment says "a more specific one read later wins — the same order the host uses"; for this pair the
  code does the opposite. The conventional capitalised suffixes (`Development`, `Production`, `Staging`)
  all sort before the base name, so the effect is not limited to `Development`.
- **The reported source is always `appsettings.json`, even when the value came from another file.** A key
  read from `appsettings.Production.json` is attributed to the base file, which sends a reader to the wrong
  place.
- **User secrets are never consulted.** A project configured only with `dotnet user-secrets` runs perfectly
  and reports all three settings as missing — which is why the `doctor` hint names user secrets rather than
  treating a missing value as proof of misconfiguration.
- **Only the immediate project directory is read.** An `appsettings.json` one level up is invisible, so a
  solution-level settings file is not seen.
- **`Settings` is not actually the one source of names.** `Resolve` repeats the three key/environment pairs
  as literals, and `ReadSecret` repeats `"ApiSecret"`; changing `Settings` alone changes neither resolution
  nor the committed-secret check.
- **An empty environment variable does not override a file value here.** The check is
  `IsNullOrWhiteSpace`, unlike `AddShopifyBlazor`, which assigns `GetEnvironmentVariable(…) ?? options.X`
  and therefore lets an empty variable clobber a configured value. The two layers can disagree about what
  "set" means.
- **`FindCommittedSecrets` reports a file name, not a path**, so two projects with the same file name
  produce indistinguishable messages; and it returns the secret itself, so a caller that logs the tuple
  leaks it.

## See also

- [Commands/DoctorCommand.cs.md](Commands/DoctorCommand.cs.md) — the only consumer; turns each missing setting into a FAIL.
- [AdminApiVersionCheck.cs.md](AdminApiVersionCheck.cs.md) — reuses `SettingsFiles` to read the version from the same files.
- [Abstractions/PhysicalFileSystem.cs.md](Abstractions/PhysicalFileSystem.cs.md) — the `IFileSystem` the real CLI passes in.
- [../Shopify.Blazor/ShopifyServiceCollectionExtensions.cs.md](../Shopify.Blazor/ShopifyServiceCollectionExtensions.cs.md) — the runtime layer order this mirrors.

