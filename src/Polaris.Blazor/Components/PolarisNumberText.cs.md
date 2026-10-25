<!-- summary-of: PolarisNumberText.cs -->
<!-- source-sha256: 7c3dbe7dd629 -->

# PolarisNumberText.cs

The one place a numeric bound becomes display text, so the range slider's Min/Max fields and the
applied-filter chip cannot disagree about the same number. It rounds a halfway value away from zero at
the format's own scale, instead of letting `decimal.ToString` round it to even.

`Polaris.Blazor.Components` · `internal static class`, not a component · used by the range slider and the data filter

## Members

| Member | Signature | What it does |
|---|---|---|
| `Format` | `public static string Format(decimal value, string? format, IFormatProvider? provider)` | Renders the value under `format`, pre-rounded to the number of decimals the format can show. A null or empty format returns a plain rendering with no rounding. A null provider means `CultureInfo.InvariantCulture`. |
| `ScaleOf` | `public static int? ScaleOf(string format)` | How many decimal places a numeric format can show, or `null` when it fixes no count — a general or round-trip style format, where pre-rounding would throw away precision the caller asked to keep. |

## Behaviour

- **Two callers, one spelling.** `PolarisRangeSlider` formats the two typed fields and the announced
  handle value through it, and `PolarisDataFilter` formats each end of a range filter's chip through
  it. A chip reading `4.1666667` beside fields reading `4.17` is exactly the disagreement this exists
  to prevent.
- **The rounding is stated, not inherited.** `decimal.ToString` rounds a halfway value to even, so
  `4.165` under `F2` would read `4.16` — a penny under the number a merchant would write down.
  `Format` therefore rounds explicitly with `MidpointRounding.AwayFromZero` before formatting, and
  `4.165` reads `4.17`.
- **Only the text is rounded.** The `decimal` is passed by value and never written back, so the
  interval a control reports keeps its full precision; a display format never moves a bound nobody
  touched.
- **The scale comes from the format string.** `ScaleOf` recognises the two families Polaris uses:
  a standard format (one letter and an optional precision, `F2`, `N0`, `C2`, `E3`), where a letter
  alone takes that specifier's default — two decimals for `F`, `N`, `C` and `P`, `null` for anything
  else; and a custom format, where the count is the run of `0` / `#` placeholders after the first `.`,
  or `0` when the format has placeholders but no decimal point at all.
- **A `null` scale means no rounding.** For `G`, `R`, or a format this cannot parse, the value goes
  straight to `ToString`, so precision survives.

## Gotchas

- **`ScaleOf` throws on an empty string.** It reads `format[0]` without a guard, so `ScaleOf("")`
  raises `IndexOutOfRangeException`. `Format` is safe because it checks for null-or-empty first, but
  a new caller of `ScaleOf` must do the same.
- **`ScaleOf` is a format-string reader, not a format engine.** It looks only at the first `.` in the
  whole string, so a quoted literal period in a custom format is mistaken for the decimal point and
  yields the wrong scale. It also reads the first section only, and ignores the `,` group-scaling
  rules entirely.
- **A letter with a precision is taken literally for every specifier.** `E3` reports a scale of 3 and
  the value is rounded to three *decimal places* before being written in scientific notation, where 3
  counts places inside the mantissa — `0.0004999` becomes `0.000E+000` instead of `4.999E-004`. Pass a
  plain-number format if the value must reach a control.
- **`#` placeholders are counted as if they were obligatory.** `0.##` reports 2, so the value is
  rounded to two decimals even though the format prints at most two and drops a trailing zero. Rounding
  is the point of the class, but it means a `#` format is treated as a fixed maximum, not an upper
  bound that may be unused.
- **A custom formatter must bypass this class.** The data filter checks for an `ICustomFormatter` on
  the column's provider and formats the value through the column instead, because rounding before
  handing the value to someone else's formatter is not this class's decision to make.
- **The default culture is invariant on purpose.** The number has to be a plain number for
  `s-number-field`, so a caller that wants a merchant's separators must pass the provider explicitly
  and must not feed the result back into a numeric field.

## See also

- [PolarisRangeSlider.cs.md](PolarisRangeSlider.cs.md) — the fields and the announced handle value
  that share this formatting.
- [PolarisDataFilter.cs.md](PolarisDataFilter.cs.md) — the applied-filter chip whose bound has to match
  those fields.
