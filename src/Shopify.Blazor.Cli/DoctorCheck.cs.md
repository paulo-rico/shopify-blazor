<!-- summary-of: DoctorCheck.cs -->
<!-- source-sha256: ff2526eb30c6 -->

# DoctorCheck.cs

One line of a `doctor` report: a named check with a status, a one-line reason, and an optional hint
saying what to do about it. A report is just a list of these, which is what lets `doctor` print
PASS / WARN / FAIL for a person and the same facts as JSON for CI without two code paths.

`Shopify.Blazor.Cli` · `CheckStatus` enum + `sealed record` · the unit a `doctor` report is built from

## Members

| Member | Kind | What it does |
|---|---|---|
| `CheckStatus.Pass` | enum value | The check ran and the project is fine. |
| `CheckStatus.Warn` | enum value | Something looks wrong but could not be decided, or is worth a look. |
| `CheckStatus.Fail` | enum value | A definite problem that will cost time if it is not fixed. |
| `Name` | positional property, `string` | The check's name, as printed. |
| `Status` | positional property, `CheckStatus` | Which of the three outcomes. |
| `Reason` | positional property, `string` | One line saying what was found. |
| `Hint` | positional property, `string?` | What to do about it, printed for anything that is not `PASS`. `null` means no advice line. |
| `StatusText` | computed `string` | `"PASS"`, `"WARN"` or `"FAIL"` — the spelling the human report and the JSON both use. |
| `Pass(string name, string reason)` | `static DoctorCheck` | A passing check. The hint is always `null`. |
| `Warn(string name, string reason, string? hint = null)` | `static DoctorCheck` | A warning, with optional advice. |
| `Fail(string name, string reason, string? hint = null)` | `static DoctorCheck` | A failure, with optional advice. |

## Behaviour

- `StatusText` is the only spelling decision in the CLI. Both the printed report and the `--json`
  document use it, so a CI script that matches `"FAIL"` is not reading two vocabularies.
- Being a positional `record`, the type has structural equality and a generated `Deconstruct`. Two
  checks built from the same four values compare equal, so a test can assert a whole report as a list
  of expected records rather than as formatted text.
- `Hint` is genuinely optional on both `Warn` and `Fail`; the difference between "nothing to say" and
  "advice omitted" is not recorded, so the printer cannot distinguish them.

## Gotchas

- **`Pass` takes no hint.** There is no way to attach advice to a passing check; if a passing line
  needs a note, the outcome is a `Warn`. Constructing one by hand through the primary constructor
  bypasses that, and the hint then flows straight into the report.
- **`StatusText` maps anything that is not `Pass` or `Warn` to `FAIL`.** The default switch arm hides
  an out-of-range enum value (a bad cast, a future member) as a failure rather than throwing, so the
  report stays printable but the status is a lie.
- **`Reason` is documented as one line but nothing enforces it.** A reason carrying a newline breaks
  the alignment of the printed table and nothing warns.
- **Nothing here raises or catches exceptions.** A check that throws has to be converted by the caller
  into `DoctorCheck.Fail`; this type deliberately has no failure path of its own.

## See also

- [Commands/DoctorCommand.cs.md](Commands/DoctorCommand.cs.md) — builds the list and decides the exit code.
- [ExitCodes.cs.md](ExitCodes.cs.md) — how a `FAIL` becomes a non-zero process exit.
- [AdminApiVersionCheck.cs.md](AdminApiVersionCheck.cs.md) — one of the checks that produces these.
