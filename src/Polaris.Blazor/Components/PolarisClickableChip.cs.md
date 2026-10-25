<!-- summary-of: PolarisClickableChip.cs -->
<!-- source-sha256: 9f21f32e62bb -->

# PolarisClickableChip.cs

Wraps `<s-clickable-chip>`, the interactive chip: one the merchant can click, remove, or that opens a
link. It has the events a plain chip lacks — click, remove and after-hide — which is exactly what
separates a filter or a tag the merchant can act on from a label that only looks like one.

`Polaris.Blazor.Components` · renders `<s-clickable-chip>` · base `PolarisContentComponentBase`

## Parameters

| Parameter | Type | Default | What it does |
|---|---|---|---|
| `Color` | `PolarisColor` | `PolarisColor.Base` | Colour intensity: `Base`, `Subdued` or `Strong`. Written as `color="base"`, `"subdued"` or `"strong"`. |
| `Removable` | `bool` | `false` | Shows the control that removes the chip. Bare `removable` attribute when true. |
| `Hidden` | `bool` | `false` | Renders the chip hidden. Presence means hidden: `true` emits a bare `hidden` attribute, `false` emits nothing, and the interop layer also clears the attribute when it should not be set. |
| `Disabled` | `bool` | `false` | Prevents interaction — click, remove control and link all stop responding. |
| `Href` | `string?` | `null` | Renders the chip as a link. |
| `CommandFor` | `string?` | `null` | Id of the element this chip controls, for a declarative command. |
| `Command` | `PolarisCommand` | `PolarisCommand.Auto` | Command sent to `CommandFor`: `Auto`, `Show`, `Hide`, `Toggle`, `Copy`. Dropped when `CommandFor` is null. |
| `InterestFor` | `string?` | `null` | Id of a tooltip or popover shown when the chip is hovered or focused. |

Inherited: `Id`, `AccessibilityLabel`, `ItemTag`, `Slot`, `AdditionalAttributes` and `ChildContent` —
the chip's visible label is its child content; see
[PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md).

## Events

| Callback | Args | Raised when |
|---|---|---|
| `OnClick` | `EventCallback<PolarisEventArgs>` | The chip is clicked, as a whole. |
| `OnRemove` | `EventCallback<PolarisEventArgs>` | The merchant uses the chip's own remove control. This is the event to drop the value from your collection on. |
| `OnAfterHide` | `EventCallback<PolarisEventArgs>` | The chip has finished hiding — after the remove transition, not when the remove control was pressed. |

## Behaviour

- Removal is a two-step interaction: `OnRemove` reports the intent immediately, the chip hides itself,
  and `OnAfterHide` arrives when the hide has completed. A parent that drops the chip from its data on
  `OnRemove` gets the collapse for free; one that needs the row to stay until the animation ends
  listens to `OnAfterHide` instead.
- `Hidden` behaves as a presence attribute like the rest of Polaris's booleans — `true` emits a bare
  `hidden` attribute and `false` emits nothing — but the XML notes one extra step: the interop layer
  also clears the attribute, so hiding is not a one-way trip in the DOM.
- `Href` turns the whole chip into a link; without it the chip is a click target. There is no `Target`
  parameter here, unlike [PolarisClickable.cs.md](PolarisClickable.cs.md), so the browsing context
  cannot be chosen from this component.

## Gotchas

- Use this rather than [PolarisChip.cs.md](PolarisChip.cs.md) whenever the merchant can act on it. A
  plain chip's `Removable` draws a remove control and gives you no event to handle, so the click goes
  nowhere with nothing logged.
- The XML warns that the graphic slot takes an icon component only. Any other element placed there is
  rejected by Polaris's slot validation and disappears from the chip.
- `Command` is dropped unless `CommandFor` is set, so a chip written as a command trigger without a
  target is a plain clickable chip with nothing to open.
- `Disabled` blocks interaction with the chip as a whole, which includes its remove control: a disabled
  removable chip cannot be dismissed. Hide it instead if it must be unavailable but still visible.

## See also

- [PolarisChip.cs.md](PolarisChip.cs.md) — the non-interactive chip, and why it is not enough here.
- [PolarisClickable.cs.md](PolarisClickable.cs.md) — the same interaction model on a full surface.
- [PolarisButton.cs.md](PolarisButton.cs.md) — the declarative `CommandFor`/`Command` pattern in full.
