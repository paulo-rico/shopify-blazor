<!-- summary-of: AdminApiVersionCheck.cs -->
<!-- source-sha256: 7cc8dd837172 -->

# AdminApiVersionCheck.cs

Classifies a project's configured Admin API version against a small hand-maintained list of stable
Shopify releases, so `doctor` can warn before Shopify silently answers from an older release. It reports
four outcomes and never throws, because neither a retired version nor a future one produces an error at
request time.

`Shopify.Blazor.Cli` · `static class` + `ApiVersionOutcome` enum + `ApiVersionCheckResult` record

## Members

| Member | Signature | What it does |
|---|---|---|
| `VersioningUrl` | `public const string` = `"https://shopify.dev/docs/api/usage/versioning"` | Where the tracked table is published; quoted in every hint. |
| `KnownStableVersions` | `public static readonly IReadOnlyList<string>` | The stable `YYYY-MM` releases this build knows, oldest first — twelve entries from `2024-01` to `2026-10`. |
| `NewestKnownVersion` | `public static string` (computed) | `KnownStableVersions[^1]`; the target every "move to" hint names. |
| `Evaluate` | `static ApiVersionCheckResult Evaluate(string? version)` | Trims, parses and classifies the version. Never throws; a null or blank value is `Missing`. |
| `Parse` | `static (int Year, int Month)? Parse(string version)` | Shape-only parse of `YYYY-MM`; returns `null` unless it is exactly four digits, a dash and exactly two digits forming a month in 1–12. |
| `ApiVersionTupleComparer.Compare` | `internal static int Compare((int Year, int Month) left, (int Year, int Month) right)` | Orders two parsed values chronologically; used only to decide "newer than the newest known". |

### `ApiVersionOutcome`

| Value | Meaning |
|---|---|
| `Current` | A known stable release **and** the newest in the table. |
| `Outdated` | A known stable release but not the newest. |
| `Unknown` | Not a stable release at all: wrong shape, a month Shopify does not release in, or a version this table has not learned yet. |
| `Missing` | No version configured, so the toolkit's default is assumed. |

### `ApiVersionCheckResult`

| Position | Type | What it carries |
|---|---|---|
| `Outcome` | `ApiVersionOutcome` | What was decided. |
| `Version` | `string?` | The trimmed version that was checked; `null` only for `Missing`. |
| `Reason` | `string` | One line naming the version and what is wrong or right about it. |
| `Hint` | `string?` | What to do; `null` for `Current` and for `Missing`. |

## Behaviour

`Evaluate` classifies each input as follows:

| Input | Outcome | Reason it produces |
|---|---|---|
| `null`, `""`, whitespace | `Missing` | "no AdminApiVersion is set in appsettings.json, so the toolkit default (…2026-10) applies." No hint. |
| Not `YYYY-MM` by shape, e.g. `2026-1`, `v2026-10`, `2026-13` | `Unknown` | "'…' is not a Shopify version name; stable versions look like 2026-10." |
| Equal to the newest table entry | `Current` | "… is the newest stable version this CLI knows about." |
| In the table but earlier than the newest | `Outdated` | "… is stable but older than …", plus a hint about Shopify's silent fall-forward. |
| Shapes correctly and later than the newest | `Unknown` | "… is newer than the newest stable version this CLI knows about (…)." The hint asks you to confirm it exists and update the CLI. |
| Shapes correctly, earlier than the newest, not in the table | `Unknown` | "… is not one of the stable versions this CLI knows about (2024-01 to …)." |

## Gotchas

- **The table is a hint, not an oracle.** It is maintained by hand (four releases a year), so an
  out-of-date CLI calls a *newly released* version `Unknown` rather than `Outdated`. That is deliberate:
  a stale tool must never tell anyone a working configuration is broken. The cost is that `Unknown` needs
  a human to check the URL before it means anything.
- **A future-dated version is `Unknown`, not wrong**, and the hint says so. `Parse` only checks shape, so
  `9999-01` parses and is judged "newer than newest" rather than rejected as nonsense.
- **`Missing` is the one outcome with no hint and a null `Version`.** The consumer prints a WARN with no
  remedy line, which is correct — the toolkit default is valid — but it looks different from the others.
- **An older version that is not a release month is `Unknown`, not `Outdated`.** `2026-09` shapes
  correctly but was never released, so it takes the generic "not one of the stable versions" branch.
- **A retired version still in the table reports `Outdated`.** Once the table drops it, the same
  configuration becomes `Unknown`, which reads as "maybe a typo" — a less useful diagnosis for a version
  that genuinely worked until it aged out.
- Comparisons are ordinal and against the **trimmed** value, so surrounding whitespace is harmless; the
  result's `Version` is the trimmed string, not the original.
- This class only classifies. It does not read configuration, print anything or choose an exit code —
  its caller decides which outcomes are warnable.

## See also

- [Commands/DoctorCommand.cs.md](Commands/DoctorCommand.cs.md) — the consumer, which turns the outcome into a PASS or WARN.
- [ConfigurationReader.cs.md](ConfigurationReader.cs.md) — how the version's settings file is read.
- [../Shopify.Blazor/ShopifyOptions.cs.md](../Shopify.Blazor/ShopifyOptions.cs.md) — the runtime default and the property being checked.
