<!-- summary-of: PolarisPopover.cs -->
<!-- source-sha256: 8f2ae6cab946 -->

# PolarisPopover.cs

A non-modal floating panel anchored to a trigger, rendered as `<s-popover>`: give it an `Id` and open it
from an `s-button` whose `CommandFor` names that id. For a confirmation or a form, use a modal instead —
a popover does not block the page.

`Polaris.Blazor.Components` · renders `<s-popover>` · base `PolarisContentComponentBase`

## Parameters

| Parameter | Type | Default | What it does |
|---|---|---|---|
| `MaxBlockSize` | `string?` | `null` | Maximum block size before the content is cut off, for example `"300px"`. Null drops the attribute and leaves the element's own limit. |
| `BlockSize` | `string?` | `null` | Fixed block size, as a CSS length. Null means "size to the content, within the maximum". |
| `MaxInlineSize` | `string?` | `null` | Maximum inline size, as a CSS length. |
| `InlineSize` | `string?` | `null` | Fixed inline size. **Set this in practice** — a popover with no inline size lays its slotted content out at zero width. |

Inherited: `ChildContent` — the panel's content — from `PolarisContentComponentBase`, plus `Id`,
`AccessibilityLabel`, `ItemTag`, `Slot`, `AdditionalAttributes` (see
[PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md)).

## Behaviour

- **Opening is declarative and lives on the trigger, not here.** The popover has no `Open`, `Active` or
  `Trigger` parameter: the trigger button carries `CommandFor="<popover id>"` and
  `Command="PolarisCommand.Show"` (or `Toggle`), and the id is the only link between the two. Without an
  `Id` the panel can never be opened.
- **The size parameters are CSS strings, not numbers.** `"300px"`, `"20rem"` and the like are passed
  through verbatim; a bare number would be an invalid length with nothing logged.
- **It is not modal.** The page underneath stays interactive: the merchant can click, type and navigate
  away while the panel is open, so a confirmation or anything that must be finished before continuing
  belongs in `PolarisModal`.
- **`s-popover` exposes no methods at all.** Measured: `show`, `hide`, `toggle`, `showOverlay`,
  `hideOverlay` and `toggleOverlay` are all `undefined` on the element, so a popover cannot be opened
  from code, by focus, or by typing — the declarative `commandFor` on a button is the one way in.
- **It has no trigger of its own and no arrow or placement parameter.** Position is the element's
  business; the wrapper models only the four size attributes.

## Gotchas

- **With no inline size the content is laid out at zero width.** Measured: the panel opens, the trigger
  reports `aria-expanded=true`, the content even has a height — and every control inside is 0px wide, so
  a slider cannot be dragged and a field is a sliver. State `InlineSize` (the data filter panel uses
  `"320px"`); nothing in the markup says it is missing.
- **It paints no padding.** The `div.popover` inside the shadow root computes to `0px` on every side and
  has the same rect as the slotted panel, so content placed straight into a popover puts its own border
  against the popover's border. Give the panel its own inset (the data filter uses `16px`, read from
  `var(--p-space-400, 16px)` — the custom property is not defined on `:root`, so the fallback is the
  value in force).
- **It clips its content; it does not scroll it.** With enough content the panel is capped and the
  controls beyond the cap are painted outside its box, in space it does not occupy — nothing there
  receives a pointer. Keep the content shorter than the cap, or put your own scroll box
  (`max-block-size` + `overflow-y: auto` on a plain `div`) inside the popover's frame.
- **The host computes to `display: contents`.** Its own box is 0×0 at the page origin, so a click probe
  aimed at the host's centre clicks the top-left of the window, and "is the panel open?" measured from
  the host always answers 0. Measure the `<button>` in the trigger's shadow root, and open-ness from a
  control *inside* the panel.
- **An unset `Id` fails silently.** The trigger's `CommandFor` then addresses nothing and the button
  looks inert; two popovers sharing an id make the address ambiguous instead.
- **Nothing reconciles the trigger's state.** `aria-expanded` belongs to the button and follows the
  target's open state; a hand-written `AccessibilityExpanded` on the trigger is ignored when
  `CommandFor` is set.

## See also

- [PolarisButton.cs.md](PolarisButton.cs.md) — `CommandFor` and `Command`, the only way to open this
  panel.
- [PolarisModal.cs.md](PolarisModal.cs.md) — the blocking alternative for a confirmation or a form.
- [PolarisTooltip.cs.md](PolarisTooltip.cs.md) — a text-only overlay pointed at with `InterestFor`.
- [PolarisDataFilter.cs.md](PolarisDataFilter.cs.md) — a real panel that states `InlineSize` and its own
  inset because of the traps above.
