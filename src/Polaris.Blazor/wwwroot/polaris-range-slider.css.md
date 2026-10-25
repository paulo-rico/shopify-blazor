<!-- summary-of: polaris-range-slider.css -->
<!-- source-sha256: 91173b8e7af2 -->

# polaris-range-slider.css

The range slider's appearance, and only its appearance: two native range inputs over one painted
track, a filled interval between the handles and a 16 px thumb on each. The interval, which handle
is on top and which end is which are all written inline by `PolarisRangeSlider`, so without this file
the control is still a working slider with real keyboard, touch and screen-reader support — it merely
stops looking like the admin.

`Polaris.Blazor/wwwroot` · static stylesheet · the one control in the library that draws itself

## Custom properties

| Property | Default | What it affects |
|---|---|---|
| `--polaris-range-track-color` | `var(--p-color-border-secondary, #d4d4d8)` | The unfilled part of the track, on both sides of the interval. |
| `--polaris-range-fill-color` | `var(--p-color-bg-fill-brand, #2c6ecb)` | The selected interval, the thumb's ring, and the focus outline. |
| `--polaris-range-thumb-color` | `var(--p-color-bg-surface, #ffffff)` | The thumb's fill. |
| `--polaris-range-start` | `0%` (fallback in the gradient) | Where the filled interval begins — written inline by the component. |
| `--polaris-range-end` | `100%` (fallback in the gradient) | Where the filled interval ends — written inline by the component. |

Polaris's own tokens are used when the runtime defines them, with the admin's greys as the fallback; a
consumer can set any of the three colour properties on an instance or on any ancestor.

## Selectors

| Selector | What it affects |
|---|---|
| `[data-polaris-range-slider]` | Hosts the three colour properties, `display: grid`, `inline-size: 100%`. |
| `[data-polaris-range-track]` | `position: relative`, `block-size: 28px`, and a six-stop `linear-gradient` painted as a 4 px-tall stripe centred in the track: track colour to the start, fill colour across the interval, track colour after the end. The line and the selected interval are one background, so there is no second element to keep in sync. |
| `[data-polaris-range-track] input[type="range"]` | Absolutely positioned over the whole track, full width, 28 px tall, `margin`/`padding`/`border` zero, transparent, `appearance: none`, `pointer-events: none`. Both inputs occupy the whole track; the track above is the only thing painted. |
| `::-webkit-slider-runnable-track` / `::-moz-range-track` | 28 px tall, transparent, no border — the native track is removed so only the gradient shows. |
| `::-webkit-slider-thumb` / `::-moz-range-thumb` | `pointer-events: auto` (the only pointer target), 16 px square, 2 px border in the fill colour, circular, thumb-colour fill, a 1 px shadow, `cursor: grab`. The WebKit rule also sets `margin-block-start: 6px` to centre 16 px on the 28 px input. |
| `input[type="range"]:focus-visible::-webkit-slider-thumb` / `::-moz-range-thumb` | A 2 px fill-colour outline, offset 2 px. A native input draws its ring inside the input, which is invisible behind a transparent track, so the ring has to be on the thumb to be seen at all. |
| `input[type="range"]:disabled::-webkit-slider-thumb` / `::-moz-range-thumb` | `cursor: not-allowed`. |
| `[data-polaris-range-slider][data-collapsed="true"]` | Re-declares `--polaris-range-fill-color` with the default. When both handles sit on the same value the filled interval has no width, and the track colour alone would make the control look disabled. |
| `[data-polaris-range-inputs] > [data-polaris-range-input]` | `min-inline-size: 0` — the overflow guard. A grid item defaults to `min-width: auto`, which lets a field's own minimum widen its column and push the second field onto a line of its own. The two-column grid and its gap are written inline by the component. |

## Behaviour

- **No media query.** Unlike the other two stylesheets there is no `prefers-reduced-motion` block,
  because nothing here animates.
- **Loaded automatically** by `polaris-interop.js`, or by hand from
  `_content/Polaris.Blazor/polaris-range-slider.css`.
- The track height, thumb size and WebKit centring margin are one matched set: 16 px thumb + 6 px top
  margin centres in the 28 px input, and the 28 px input is what makes the whole strip draggable rather
  than the 4 px line.

## Gotchas

- **Only the thumbs take pointer events.** `pointer-events` is off on the input and back on for the
  thumb, so a click on the bare track does nothing — the handle does not jump to the pointer. That is a
  deliberate choice, and it surprises anyone used to a single-handle range input.
- **A `[data-collapsed="true"]` slider forces the default fill colour**, overriding a
  `--polaris-range-fill-color` inherited from an ancestor. A themed slider therefore goes back to the
  brand blue at the moment both handles meet.
- **The two inputs are stacked, so the one on top wins a thumb hit** where the handles overlap. Which
  input is on top is decided inline by the component, not here; changing it in CSS alone would make the
  wrong handle move.
- **Change the thumb size and the WebKit margin must change with it**, or the thumb sits off-centre on
  the track. Firefox lays its own thumb out and needs no equivalent margin, so the bug appears in one
  browser only.
- **The 28 px track is the hit area, not decoration.** Shrinking `block-size` to make the control more
  compact makes dragging much harder, and nothing reports it.

## See also

- [PolarisRangeSlider.cs.md](../Components/PolarisRangeSlider.cs.md) — writes the interval, the
  collapsed flag and the two-column field grid these rules style.
- [PolarisNumberField.cs.md](../Components/PolarisNumberField.cs.md) — the fields the bounds are typed
  into, which the `min-inline-size` guard protects.
- [polaris-interop.js.md](polaris-interop.js.md) — the loader that injects this stylesheet.
