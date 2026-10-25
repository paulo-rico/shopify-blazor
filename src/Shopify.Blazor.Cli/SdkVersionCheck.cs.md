<!-- summary-of: SdkVersionCheck.cs -->
<!-- source-sha256: b75834daad88 -->

# SdkVersionCheck.cs

Compares the running .NET framework against the `global.json` that governs a project and answers
Satisfied, NotSatisfied or Indeterminate. The comparison is deliberately conservative: it decides only
what is unambiguous — the running version is past the pinned line, or at least at its feature band — and
reports "cannot decide" for the rest, because a false FAIL sends someone to reinstall an SDK they
already have.

`Shopify.Blazor.Cli` · `static partial class`, one enum and one record · no base class

## Members

| Member | Signature | What it does |
|---|---|---|
| `SdkCheckOutcome` | `enum` | `Satisfied`, `NotSatisfied`, `Indeterminate`. The last means "not decidable from what is available", and is what makes `doctor` WARN instead of FAIL. |
| `SdkCheckResult` | `sealed record (SdkCheckOutcome Outcome, string Reason, string? Hint)` | The verdict, a one-line reason, and what to do about it (null when nothing needs doing). |
| `SdkVersionCheck.Evaluate` | `static SdkCheckResult Evaluate(string? globalJsonPath, string? globalJsonText, string frameworkDescription)` | The whole check, from already-read inputs — no file access, no process, so it is pure and testable. |
| `SdkVersionCheck.ReadVersion` | `static string? ReadVersion(string globalJsonText)` | The `sdk.version` string, or null when there is no readable one. |
| `SdkVersionCheck.ReadRollForward` | `static string? ReadRollForward(string globalJsonText)` | The `sdk.rollForward` value, or null when unset. Available and documented, but `Evaluate` never consults it. |
| `SdkVersionCheck.ParseFrameworkVersion` | `static Version? ParseFrameworkVersion(string frameworkDescription)` | Extracts the first `major.minor[.build]` from a description such as `.NET 10.0.1`. |

## How `Evaluate` decides

| Input state | Outcome | Reason it carries |
|---|---|---|
| `globalJsonText` is null | Indeterminate | no `global.json` was found, so nothing is pinned; the hint calls that normal but suggests adding one |
| no `sdk.version` readable | Indeterminate | names the file and shows the `sdk` block to add |
| framework description unparseable | Indeterminate | states the pin and the raw description, and tells the reader to compare with `dotnet --version` |
| running major.minor **below** the pin | NotSatisfied | names both and hints at installing the pinned SDK or relaxing `global.json` |
| running major.minor **above** the pin | Satisfied | "a later release line than the pin"; no hint |
| same major.minor, feature band sufficient | Satisfied | the running version is at least the feature band the pin names |
| same major.minor, feature band unclear | Indeterminate | the runtime version and the SDK feature band are different numbers, so it cannot be decided here |

## Behaviour

- **The feature-band test is the only patch-level judgement.** An SDK version's third component is the
  feature band times one hundred (`10.0.100` is band 1), so the running build is multiplied by 100 before
  comparing. `10.0.1` therefore clears a `10.0.100` pin. It can only upgrade an Indeterminate into a
  Satisfied, never produce a NotSatisfied.
- The major/minor comparison degrades to "same line" when the pin has fewer than two numeric parts, so an
  unreadable pin falls through to the feature-band test rather than failing.
- All number parsing uses `InvariantCulture`, so a comma decimal separator in a locale cannot change the
  verdict.
- `Reason` and `Hint` are the user-facing text in the `doctor` report; the hint is null only on a clean
  pass.

## Gotchas

- **`ReadRollForward` is public and unused.** `Evaluate` deliberately ignores it: `rollForward` has seven
  values with subtly different rules, and reimplementing MSBuild's resolution would guess. A project
  pinned `10.0.100` with a permissive rollForward running an older major still reports NotSatisfied.
- **`ParseFrameworkVersion` takes the first version-shaped number anywhere in the string.** Its pattern is
  unanchored, so despite the doc comment saying .NET Framework returns null, `.NET Framework 4.8.1` parses
  as `4.8.1` — and the major/minor comparison then reports NotSatisfied with advice to install an SDK.
- **`ReadVersion` is anchored on the literal lowercase `"sdk"` and needs the version inside that object.**
  A `version` key outside the `sdk` block is not found, a `}` may not intervene, and a file written with
  `"SDK"` is invisible.
- **`ReadRollForward` slices from the first `"sdk"` to the end of the text, not to the end of the block**,
  so a `rollForward` belonging to a later object still matches.
- **Indeterminate is not a pass.** It is the correct verdict when the inputs cannot support a decision, and
  a caller that treats "not NotSatisfied" as healthy loses the distinction the enum exists to preserve.

## See also

- [Commands/DoctorCommand.cs.md](Commands/DoctorCommand.cs.md) — the `dotnet-sdk` check, which maps these
  outcomes onto PASS, WARN and FAIL.
- [Abstractions/IEnvironment.cs.md](Abstractions/IEnvironment.cs.md) — supplies `FrameworkDescription`,
  the string this class parses.
- [ProjectLocator.cs.md](ProjectLocator.cs.md) — finds the project whose `global.json` is being honoured.
