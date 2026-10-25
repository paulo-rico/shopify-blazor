<!-- summary-of: PolarisNumber.cs -->
<!-- source-sha256: f2d77069c103 -->

# PolarisNumber.cs

An inline numeric run, rendered as `<s-number>`. Its point is tabular numerals: figures line up down a
column, so it belongs in every table cell or totalling layout where a plain `PolarisText` would let the
digits drift.

`Polaris.Blazor.Components` · renders `<s-number>` · base `PolarisContentComponentBase`

## Parameters

| Parameter | Type | Default | What it does |
|---|---|---|---|
| `Tone` | `PolarisTone` | `Auto` | Semantic colour: `Neutral`, `Info`, `Success`, `Warning`, `Caution`, `Critical`; `Auto` lets the element choose. Use `Critical` for a negative or overdue figure. |
| `Color` | `PolarisColor` | `Base` | Contrast: `Base`, `Subdued` (de-emphasised, e.g. a secondary figure) or `Strong`. |
| `FontSize` | `PolarisFontSize` | `Auto` | Size of the digits, from `small-200` up to `large-100`; `Auto` is the element's default. |
| `FontWeight` | `PolarisFontWeight` | `Auto` | Weight, `Base` through `Bold`; `Auto` leaves it to the element. |
| `Dir` | `PolarisTextDirection?` | `null` | Text direction. Null drops the attribute and leaves the element's own default in force. |
| `AccessibilityVisibility` | `PolarisAccessibilityVisibility?` | `null` | How assistive technology sees this run: `Visible`, `Exclusive` (announced, not drawn) or `Hidden` (removed from accessibility entirely). Null drops the attribute. |

Inherited: `ChildContent` (the digits, from `PolarisContentComponentBase`) plus `Id`,
`AccessibilityLabel`, `ItemTag`, `Slot`, `AdditionalAttributes` (see
[PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md)).

## Behaviour

- **It does not format the value.** The number is written by the page as content — the component only
  applies Polaris's numeric styling — so decimals, separators and rounding are the caller's decision.
  Where a figure must read exactly as a field shows it, format it with `PolarisNumberText` first.
- **It is the replacement for `FontVariantNumeric` on the text components**, which Polaris deprecates
  in its favour. Reaching for `PolarisText` with a numeric variant is the old spelling.
- **It is inline with no spacing of its own.** Two numbers placed side by side run together, and a
  number used as a standalone statistic needs a `PolarisStack` or a paragraph around it to stand
  apart.
- **Tabular figures are fixed-width per digit.** Every digit occupies the same advance, so a column of
  values lines up digit for digit; the column's own left/right alignment is still a decision made on
  the table header, not here.

## Gotchas

- **`Auto` and `Base` are real values, not "leave it out".** `Tone`, `Color`, `FontSize` and
  `FontWeight` are always emitted — `auto`, `base`, `auto`, `auto` — even when the caller sets
  nothing, so the wrapper states them rather than letting the element fall back.
- **`AccessibilityVisibility.Hidden` is not "visually hidden".** It removes the run from the
  accessibility tree as well; use `Exclusive` for a value only a screen reader should hear, and
  `Hidden` only for a decorative figure already announced elsewhere.
- **`Dir` is nullable for a reason.** Null omits `dir` entirely and the element inherits its
  direction from the document; a value set here overrides that inheritance for this run only, so
  direction belongs on a container when the whole block is right-to-left.
- **Nothing to show is an empty element, not a dash.** The number is `ChildContent` and there is no
  fallback, so a nullable decimal that is null leaves the cell blank — which reads as a missing value
  rather than as a zero. Render the placeholder yourself.
- **It raises no events.** It is display only; a figure that must be clickable needs a
  `PolarisClickable` or a button around it.

## See also

- [PolarisTable.cs.md](PolarisTable.cs.md) — the table whose numeric columns this belongs in.
- [PolarisStack.cs.md](PolarisStack.cs.md) — gives a standalone figure the spacing an inline element
  lacks.
- [PolarisRangeSlider.cs.md](PolarisRangeSlider.cs.md) — where the same plain-number formatting rule
  is enforced for bounds.
- [PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md) — attributes, properties
  and the inherited parameters.
