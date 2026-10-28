<!-- summary-of: PolarisPopup.cs -->
<!-- source-sha256: 020537a1271c -->

# PolarisPopup.cs

A panel drawn over the page and anchored to the element it belongs to, for content Polaris has no element
to show — a suggestion list, a menu, a small picker. It is a native popover, so the browser paints it in
the top layer where no ancestor's clipping can reach it, and the library positions it from the anchor's
rectangle while the page scrolls.

`Polaris.Blazor.Components` · composed component, base `ComponentBase` (there is no `s-popup`) · renders an anchor wrapper holding the caller's markup and a `<div popover="manual">` panel

## Parameters

| Parameter | Type | Default | What it does |
|---|---|---|---|
| `Open` | `bool` | `false` | Whether the panel is shown. The caller owns it; the popup never closes itself. |
| `Anchor` | `RenderFragment?` | null | The element the panel is anchored to — usually the trigger, or the field it belongs to. |
| `ChildContent` | `RenderFragment?` | null | The panel's content. Null means no panel element at all. |
| `Placement` | `PolarisPopupPlacement` | `Below` | `Below` or `Above`. It is declared, never guessed: this layer has no flip logic. |
| `MaxBlockSize` | `string` | `"320px"` | The preferred height cap. The module lowers it to the room on the chosen side; content longer than the cap scrolls inside the panel. |
| `ZIndex` | `int` | `1000` | Stacking order outside the top layer — the fallback path, and the closed-state style. |

No inherited parameters: this class derives from `ComponentBase`, so there is no `Id`, `Slot`,
`ItemTag`, `AccessibilityLabel` or `AdditionalAttributes`. The panel has no role of its own; what is
inside it carries the semantics.

## Members

| Member | Signature | What it does |
|---|---|---|
| `DisposeAsync` | `ValueTask DisposeAsync()` | Hides the panel and releases its positioning listeners when the component goes. |

## Behaviour

**Why not `s-popover`.** Polaris's popover cannot be opened from code — measured in Chrome, `show`,
`hide`, `toggle` and `showOverlay` are all `undefined` — and it is opened by a *button*'s `commandFor`,
which is a different control from a text field with suggestions. The behaviour cannot be borrowed; the
appearance can, and is: everything inside the panel is a Polaris element.

**The top layer is the anti-clipping mechanism, and it was measured.** An absolutely positioned panel
inside an `s-section` was cut off exactly at the section's painted edge, so its second suggestion could
not be clicked. Clipping is not stacking, so no `z-index` fixes it. The `popover` attribute puts the
panel in the browser's top layer, which is painted outside every ancestor's clip; the element stays where
Blazor rendered it, so nothing has to be reparented.

**The panel is in the markup from the first render, and hidden in two ways.** `hidden` covers every
browser, and a closed popover is `display: none` in the browsers that support the API. Keeping the
element stable is what lets the first show call happen in `OnAfterRenderAsync` without waiting for
another render. The caller draws nothing into it while closed (the autocomplete returns early from its
panel fragment), so a closed popup has no rows to find, count or tab into.

**Positioning is the module's job.** `showPopup` removes `hidden`, calls `showPopover()` when it exists,
measures the anchor's viewport rect and sets `position: fixed`, `left`, `width` and `top` (or `bottom`
for `Above`), then caps the height to the room on that side. It re-applies the position while the page
scrolls and while the window resizes, and `hidePopup` removes those listeners. `box-sizing: border-box`
is in the panel's own style so the measured width is the painted width — without it the panel is 10px
wider than its anchor because of its padding and border.

**The surface is system colours.** `Canvas` and `CanvasText` are what the browser resolves for the
document's `color-scheme`, so the panel follows a light or dark Admin. A Polaris custom property would
resolve to nothing here: those tokens live inside Polaris's shadow roots and do not inherit out.

## Gotchas

- **No dismissal logic, by design.** The popup does not listen for Escape, outside clicks or blur. Only
  the caller knows whether a blur means "the merchant is leaving" or "the merchant is moving into the
  panel" — `PolarisAutocomplete` is the worked example, and it delays its close for exactly that reason.
- **`Placement="Above"` is not a flip.** The caller decides. There is no measurement of the viewport
  beyond the height cap, so a control at the bottom of a long page has to say `Above` itself.
- **The top layer is ordered by entry.** A Polaris dialog opened after the panel is painted above it;
  one opened before is below it. That is the browser's rule, not something this component can override.
- **Nothing appears without JavaScript.** The panel is `hidden` until the module shows it, so a static
  prerender is correct — and a page whose interop module never loads shows no panel at all. The markup
  is still there, which is why the component tests can see it.
- **The panel is fixed to the viewport while it is up.** Scrolling repositions it (the module listens),
  but an anchor scrolled past the edge takes the panel with it, clamped by its height cap rather than
  sliding off screen.
- **`Open` is a parameter, not state.** The component has no toggle of its own; passing `false` asks the
  module to hide, and that is what releases the scroll and resize listeners.

## See also

- [PolarisAutocomplete.cs.md](PolarisAutocomplete.cs.md) — the caller that shows and hides it.
- [../Interop/PolarisInterop.cs.md](../Interop/PolarisInterop.cs.md) — `ShowPopupAsync` and `HidePopupAsync`.
- [../wwwroot/polaris-interop.js.md](../wwwroot/polaris-interop.js.md) — `showPopup`, `hidePopup` and the
  measurements behind them.
