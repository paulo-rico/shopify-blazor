<!-- summary-of: PolarisChip.cs -->
<!-- source-sha256: 80cf2964310d -->

# PolarisChip.cs

Wraps `<s-chip>`, a user-supplied label or tag such as a product type or a filter word. It is a
visual indicator only: it carries a colour and an optional remove affordance, and no events, so a
chip a merchant can act on belongs to PolarisClickableChip instead.

`Polaris.Blazor.Components` · renders `<s-chip>` · base `PolarisContentComponentBase`

## Parameters

| Parameter | Type | Default | What it does |
|---|---|---|---|
| `Color` | `PolarisColor` | `PolarisColor.Base` | Colour intensity: `Base`, `Subdued` (de-emphasised) or `Strong` (high contrast). Rendered as `color="base"`, `"subdued"` or `"strong"`. |
| `Removable` | `bool` | `false` | Shows a control that removes the chip. Rendered as a bare `removable` attribute when true. The chip does not act on it, and this component exposes no remove event. |

Inherited: `Id`, `AccessibilityLabel`, `ItemTag`, `Slot`, `AdditionalAttributes` and `ChildContent` —
the label is the child content, not a parameter; see
[PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md).

## Behaviour

- The text comes from `ChildContent` and nothing else; `AccessibilityLabel` is for the case where that
  text is not descriptive enough for assistive technology (an icon-only or abbreviated chip).
- A chip is single-line and truncates, so the XML says a long label needs a tooltip or a different
  component. There is no wrapping and no way to see the clipped text.

## Gotchas

- `Removable` on this component is a trap in two directions: the remove control appears, but the chip
  does not act on it and this component exposes no `OnRemove` callback to react to instead. A merchant
  clicks the cross and nothing happens, with nothing logged. Use
  [PolarisClickableChip.cs.md](PolarisClickableChip.cs.md), which has `OnClick`, `OnRemove` and
  `OnAfterHide`, whenever the merchant can act on the chip.
- The component has no click, link or command support at all — `Href`, `CommandFor` and `InterestFor`
  live on the clickable chip. A chip wired as a filter that "does nothing" is this component, not a
  wiring bug.
- Colour is an enum with a pinned spelling per member, so an unrecognised value cannot be passed; the
  only options are the three above.

## See also

- [PolarisClickableChip.cs.md](PolarisClickableChip.cs.md) — the interactive and dismissible version.
- [PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md) — inherited parameters.
