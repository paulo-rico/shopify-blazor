<!-- summary-of: PolarisNumberRange.cs -->
<!-- source-sha256: f5a6134170b5 -->

# PolarisNumberRange.cs

A `decimal` interval whose two ends are ordered, used as the single value of a range control and as a
filter's bounds. The constructor swaps a reversed pair, so a caller can hand over whatever the merchant
typed without checking it first.

`Polaris.Blazor` · `readonly record struct` · the value type of the range slider and of range filters

## Members

| Member | Signature | What it does |
|---|---|---|
| constructor | `PolarisNumberRange(decimal first, decimal second)` | Orders the two ends with `Min`/`Max`, so `Lower` is never above `Upper` whatever order they arrive in. |
| `Lower` | `decimal { get; init; }` | The lower end, inclusive. |
| `Upper` | `decimal { get; init; }` | The upper end, inclusive. |
| `Span` | `decimal` (computed, no backing field) | `Upper - Lower`; the width of the interval. Never negative for a range the constructor built. |
| `Create` | `static PolarisNumberRange Create(decimal first, decimal second)` | Identical to the constructor, and reads as intent at a call site: `Create(min, max)` says the two ends belong together. |
| `Contains` | `bool Contains(decimal value)` | True when the value lies inside, with both ends counting as inside. |
| `Snap` | `PolarisNumberRange Snap(decimal step)` | Floors `Lower` and ceils `Upper` onto a grid of `step`, so the result contains the original. A `step` of zero or less returns the range untouched. |
| `ToString` | `string ToString(string? format, IFormatProvider? provider = null)` | Both ends formatted and joined by `" – "`; a `null` provider means the invariant culture. |
| `ToString` | `override string ToString()` | Same as `ToString(null)`: both ends plainly, invariant culture. |

Equality is the record struct's own, by value: two ranges with the same `Lower` and `Upper` are equal.
`Span` is computed, so it takes no part in it.

## Behaviour

- **One value, not two bindable numbers.** A range control has one state, and two parameters can be
  observed half-updated — a lower bound above the upper one is a state the next keystroke silently
  corrects. The type exists so that state cannot be represented in the first place.
- **`decimal`, because the values are money and percentages.** A binary floating point type cannot
  represent a tenth of a percent without a rounding story, and a `double` bound that has to equal a
  stored price can miss it by an epsilon.
- **`Snap` widens, never narrows.** `Math.Floor` on the lower end and `Math.Ceiling` on the upper make
  the snapped interval a superset of the original; a range already on the grid comes back unchanged, and
  a single point that lands exactly on a step stays a single point.
- **`ToString` is for display.** The separator is an en dash with a space either side, and the default
  invariant culture matches the rest of the library, whose numbers have to be invariant because they are
  handed to `s-number-field`, which parses whatever it is given.
- Used as `PolarisRangeSlider.Value` (a `PolarisNumberRange?`, where `null` means "the whole domain"),
  as the domain `PolarisDataFilter` builds from a column's cheapest and dearest value, and as the
  argument to `PolarisGridFilterDescriptor.InRange`.

## Gotchas

- **`Lower` and `Upper` are `init`-settable, so the ordering invariant can be bypassed.**
  `new PolarisNumberRange { Lower = 10, Upper = 0 }` compiles, as does `range with { Lower = 20 }`, and
  both produce a range with a **negative `Span`** — which contradicts what this type says about itself.
  Only the constructor and `Create` normalise; nothing re-checks afterwards. Treat the two `init`
  accessors as constructor plumbing rather than as a second way to build a range.
- **The format string is silently dropped by interpolation and `string.Format`.** The type declares
  `ToString(string?, IFormatProvider?)` but does **not** implement `IFormattable`, so `$"{range:F2}"`
  and `string.Format("{0:F2}", range)` call the parameterless overload and print the unformatted form.
  Call `range.ToString("F2")` explicitly, and note it still rounds the way `decimal.ToString` does —
  halfway values to even, which
  [PolarisNumberText.cs.md](Components/PolarisNumberText.cs.md) exists to correct for money.
- **`Contains` and `Snap` have no call site inside this library.** `Span` is what `PolarisDataFilter`
  reads to choose a step size, and `Create` is what the slider uses to keep a clamp from inverting an
  interval. The other two are public surface for callers building their own filters.
- **`Snap` does not throw on a meaningless step.** A `step` of zero or less is a silent no-op, the same
  convention as the slider's `Step="0"`, which means "any value at all" — so a caller who expected
  snapping, or an exception, gets neither.
- **The default is a real range, not "no range".** `default(PolarisNumberRange)` is `0 – 0`, which is a
  valid single point. Absence needs `PolarisNumberRange?`, and the slider uses `null` for "the whole
  domain" — a distinction that matters because a filter that is not applied yet and a filter pinned to
  zero are different states.
- **A reversed range is corrected silently.** `new PolarisNumberRange(min, max)` with the arguments
  swapped is indistinguishable from the correct call — convenient at the edge, but it also hides a
  genuine bug in the caller, such as a domain whose endpoints came from two different sorts.

## See also

- [PolarisRangeSlider.cs.md](Components/PolarisRangeSlider.cs.md) — the dual-handle control that emits
  this type, and where `null` means the whole domain.
- [PolarisDataFilter.cs.md](Components/PolarisDataFilter.cs.md) — turns a range into a filter and shows
  its bounds in a chip.
- [PolarisGridFilterDescriptor.cs.md](DataGrid/PolarisGridFilterDescriptor.cs.md) — `InRange`, the
  bridge from a range to a grid query.
- [PolarisNumberText.cs.md](Components/PolarisNumberText.cs.md) — the one formatter both places a bound
  is shown go through, and why halfway values round away from zero.
