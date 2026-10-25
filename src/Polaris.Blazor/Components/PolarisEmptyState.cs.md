<!-- summary-of: PolarisEmptyState.cs -->
<!-- source-sha256: f46a7a4a2aca -->

# PolarisEmptyState.cs

Explains why a list or table has nothing in it, and what to do next: an `<s-empty-state>` with a
plain-text heading, a supporting subheading and room for actions in its default slot.

`Polaris.Blazor.Components` · renders `<s-empty-state>` · base `PolarisSlottedComponentBase`

## Parameters

| Parameter | Type | Default | What it does |
|---|---|---|---|
| `Heading` | `string?` | `null` | The headline, written as the `heading` attribute. Plain text only: markup passed here is escaped and displayed literally. A `null` drops the attribute, leaving the browser to draw the subheading and actions alone. |
| `Subheading` | `RenderFragment?` | `null` | Supporting sentence under the heading. Rendered as `<s-text slot="subheading">` inside the element, so it lands in the element's own `subheading` slot rather than in the body. |
| `ChildContent` | `RenderFragment?` | `null` | The default slot: the action that fixes the emptiness — usually a `PolarisButton`. Rendered after the subheading. |

Inherited: `SupplementalStart` (the `supplemental-start` slot) and `Aside` (the `aside` column) from
[PolarisSlottedComponentBase.cs.md](../PolarisSlottedComponentBase.cs.md); `Id`,
`AccessibilityLabel`, `ItemTag`, `Slot` and `AdditionalAttributes` from
[PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md).

## Behaviour

- **Children are drawn in a fixed order:** the subheading slot wrapper first, then `ChildContent`. Both
  are optional, and an empty state with neither still renders — an element with a heading and nothing
  else.
- **The `s-text` wrapper around the subheading is load-bearing, not styling.** A `RenderFragment`
  cannot carry an attribute of its own, so a fragment can only reach a named slot if the component
  wraps it in an element that does. This is the markup Shopify's own examples use for this element, and
  it is why `Subheading` is a fragment here while every other named slot in the library is set with
  the `Slot` parameter on a child component.
- **The action belongs in `ChildContent`, not in `Subheading`.** The subheading slot is text and is
  wrapped in `s-text`; a button put there is inside a text element rather than in the state's own
  action area.

## Gotchas

- **It cannot detect emptiness.** Outside a table nothing tells this component that the list is
  populated, so leaving it in the markup unconditionally shows "no products" underneath a full table.
  Render it conditionally, on the same condition the list itself uses.
- **Markup in `Heading` is not markup.** It is an attribute value, so `<b>` arrives as the characters
  `<b>` on screen; use `Subheading` or `ChildContent` when formatting is wanted.
- A parameter that is set but never read is invisible: earlier versions of this component declared
  `Subheading` and rendered nothing for it, so the text vanished with no warning — an empty state that
  said nothing about what to do next. The slot wrapper is what fixed it.

## See also

- [PolarisTable.cs.md](PolarisTable.cs.md) — the table whose emptiness this usually describes.
- [PolarisText.cs.md](PolarisText.cs.md) — the element the subheading is wrapped in.
- [PolarisButton.cs.md](PolarisButton.cs.md) — the usual child, the "add your first product" action.
- [PolarisBox.cs.md](PolarisBox.cs.md) — a bordered container to draw the state inside.
