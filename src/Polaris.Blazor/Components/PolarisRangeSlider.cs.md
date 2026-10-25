<!-- summary-of: PolarisRangeSlider.cs -->
<!-- source-sha256: 5eadb6cec4e4 -->

# PolarisRangeSlider.cs

A dual-handle numeric range control: two native range inputs over one track, reporting the chosen
interval as a `PolarisNumberRange`. Polaris has no slider element at all, so it is composed rather than
wrapped, and it is used for a filter's range facet or a price band.

`Polaris.Blazor.Components` · composed from two native `<input type="range">` elements, not a Polaris
element · base `ComponentBase`

## Parameters

| Parameter | Type | Default | What it does |
|---|---|---|---|
| `Minimum` | `decimal` | `0` | The lower end of the domain. If it is above `Maximum` the two are swapped when the domain is read, so the control never receives an inverted range. |
| `Maximum` | `decimal` | `100` | The upper end of the domain. |
| `Step` | `decimal` | `1` | Increment a handle moves by, and the grid values are snapped to. `0` means "any value": the inputs get `step="any"`, no snapping is applied, and ticks are suppressed. |
| `Value` | `PolarisNumberRange?` | `null` | The selected interval. **`null` means the whole domain**, not "nothing selected", and it follows a domain that arrives or grows. |
| `ValueChanged` | `EventCallback<PolarisNumberRange>` | — | Raised with the new interval when a drag is released, a key is pressed, or a number is typed. Not raised for mid-drag movement. |
| `Disabled` | `bool` | `false` | Blocks every interaction, including both numeric fields, which are rendered with `disabled`. |
| `ShowInputs` | `bool` | `true` | Draws a numeric field for each end — for values a thumb cannot land on precisely. |
| `ShowTicks` | `bool` | `false` | Draws tick marks along the track at whole steps. Silently draws none when `Step <= 0` or the span holds more than 24 steps. |
| `Label` | `string?` | `null` | The control's title: a visible run above the track **and** the accessible name of the group. Without it the wrapper has neither `role="group"` nor `aria-label`, and the two handles are announced as two unnamed sliders. |
| `LowerLabel` | `string` | `"Minimum"` | Accessible name of the lower handle, and the label of the lower numeric field. |
| `UpperLabel` | `string` | `"Maximum"` | Accessible name of the upper handle, and the label of the upper numeric field. |
| `Unit` | `string?` | `null` | Appended after each numeric field and to the handles' announced value (with a space). It never becomes part of the reported number. |
| `Format` | `string?` | `null` | Display format for the **announced** handle value only, e.g. `N2` or `C2`. Falls back to `InputFormat` when null. |
| `InputFormat` | `string` | `F2` | Numeric format of the two typed Min/Max fields, invariant culture. Must stay a plain number format; `""` turns rounding off and shows the raw value. The literal default is exposed as the public const `DefaultInputFormat`, so a caller showing the same bound elsewhere can match it. |

## Events

| Callback | Args | Raised when |
|---|---|---|
| `ValueChanged` | `PolarisNumberRange` | A drag ends (`change`), a handle is moved by keyboard, or a typed Min/Max value commits. Fires once per finished interaction, and only when the interval actually differs from the last one reported. |

`Current` is a public read-only `PolarisNumberRange` — the interval as currently shown, already clamped
to the domain. Read it when a caller needs the displayed value without waiting for a callback.

## Behaviour

- **Markup.** A plain `<div data-polaris-range-slider>` carries `data-collapsed` and writes the interval
  inline as `--polaris-range-start` / `--polaris-range-end` percentages. A label block, the track div
  with its two `<input type="range">` (`data-polaris-range-handle="lower"` / `"upper"`), an optional
  inert `<datalist>` of ticks, and an optional two-column input row all live inside it. Everything
  decorative is in `polaris-range-slider.css`; without that stylesheet the control is still a working
  slider that merely looks unpolished.
- **Only the layout is inline.** The interval percentages, the label's 8px gap and the input row's
  `display:grid;grid-template-columns:minmax(0,1fr) minmax(0,1fr)` are written in the markup so a
  missing stylesheet costs looks, not layout. The grid is inline because an `s-stack direction="inline"`
  around the two fields stacked them full width — a Polaris host computes to `display: contents`, so its
  contents are not the flex items the stack lays out.
- **Handles cannot cross.** The moving end is clamped against the other with `Math.Min` / `Math.Max`, so
  a handle dragged past its neighbour stops there and the merchant keeps hold of the one they grabbed.
- **The collapsed handle is raised.** When both ends sit on the same value, the handle that can still
  move is given the higher `z-index` (3 vs 2): at the top of the domain that is the lower handle,
  anywhere else the upper one. Otherwise the thumb on top would be the one that cannot move in the
  needed direction.
- **`input` previews, `change` commits.** A drag produces a stream of `input` events (four were measured
  across one drag), which move the interval and re-render this component only; the caller is told on
  `change`, which fires once on release (alongside `pointerup` and `mouseup`), so a page re-queries
  exactly once. A keyboard press produces both together, which is correct — a key press is a finished
  change.
- **Commits compare against the last reported interval**, not against the displayed one, so a release
  after a drag still reports instead of looking like "nothing changed".
- **Adopting the caller's value.** `OnParametersSet` adopts `Value` only when it is *different from the
  value last seen*; an unchanged parameter leaves the handles alone. A null value additionally follows a
  domain that changes, which is what lets a filter panel that renders before its data re-open to the full
  range once real bounds arrive. Anything adopted is clamped and, when `Step > 0`, snapped to the step
  grid measured from the domain minimum.
- **Rounding is display-only.** `InputFormat` decides what the Min/Max fields show and `Format` what a
  screen reader hears; neither changes the decimals the control reports. Typed fields go through
  `PolarisNumberText.Format`, so a bound reads identically here and on the filter's chip.
- **The numeric fields** receive the domain ends as `Min`/`Max`, `Step` when positive (`null` when not),
  `Unit` as their suffix, the shared `Disabled`, and a hidden-but-announced label
  (`LabelAccessibilityVisibility.Exclusive`).
- **Invalid input is swallowed.** A value that cannot be parsed as a number leaves the interval where it
  was; nothing is reported and nothing is logged. A domain of zero width makes every percentage 0.

## Gotchas

- **`null` `Value` is "the whole domain", not "no selection".** A cleared filter therefore shows both
  handles at the ends, and a domain that later grows widens the interval with it. Setting `Value` to the
  value the component already mirrors is not an instruction: a page that re-renders with its previous
  value (a debounced filter mid-flight) will not pull the handles back — which is deliberate, since the
  opposite behaviour made a drag need two releases and undid every second key press.
- **`Format` does not format the fields.** It only decorates the announced value; `InputFormat` is what
  the typed fields parse and display. A currency format belongs in `Format` and never in `InputFormat`
  (`s-number-field` parses what it is handed, so `£1,234.57` there is a broken value with no warning).
- **`ShowTicks` can render nothing at all.** A step of `0`, a zero-width span, or a span holding more
  than 24 steps returns an empty list and the `datalist` is simply absent — no warning. Ticks are laid
  out by the browser against the input's own geometry, and they are inert.
- **A collapsed interval keeps a `z-index` inline style** on one handle and drops it as soon as the
  interval opens; `data-collapsed` mirrors the same fact for CSS.
- **`Minimum` above `Maximum` is not an error.** The domain is read through `Math.Min`/`Math.Max`, so
  an inverted pair is quietly ordered and the handles still range over the real span.
- **`ValueChanged` is silent when nothing changed**, including a drag released on the value it started
  from and any interaction while `Disabled`. Do not use it as a "the merchant touched this" signal.
- **The slider knows nothing about grids, filters or data.** Turning its interval into a criterion is
  the caller's job — `PolarisDataFilter` does it — and a range that covers the whole domain means
  "no filter", not "matches everything".

## See also

- [PolarisDataFilter.cs.md](PolarisDataFilter.cs.md) — builds its range control from this component and turns the reported interval into a filter descriptor.
- [PolarisDataGrid.cs.md](PolarisDataGrid.cs.md) — the table the filter bar narrows.
- [PolarisGridState.cs.md](../DataGrid/PolarisGridState.cs.md) — where the applied range ends up.
