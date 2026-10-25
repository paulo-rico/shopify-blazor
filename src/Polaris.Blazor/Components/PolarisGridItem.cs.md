<!-- summary-of: PolarisGridItem.cs -->
<!-- source-sha256: 3de0ea2fac35 -->

# PolarisGridItem.cs

One cell of a `PolarisGrid`: an `<s-grid-item>` that says how many tracks it spans and carries its own
box styling, while gaps and alignment stay on the grid that owns them.

`Polaris.Blazor.Components` · renders `<s-grid-item>` · base `PolarisContentComponentBase`

## Parameters

A `null` drops the attribute, so the element's own default applies. Each parameter is written to the
camelCase attribute of the same name.

| Parameter | Type | Default | What it does |
|---|---|---|---|
| `GridColumn` | `string?` | `null` | How many columns the item spans: `auto`, or for example `span 2` / `1 / 3`. |
| `GridRow` | `string?` | `null` | How many rows the item spans: `auto`, or for example `span 2`. |
| `AccessibilityRole` | `string?` | `null` | ARIA role of the cell, as a bare string. |
| `AccessibilityVisibility` | `PolarisAccessibilityVisibility?` | `null` | `Visible` for everyone, `Exclusive` for screen readers only, `Hidden` for neither. `null` drops the attribute. |
| `Background` | `string?` | `null` | Background treatment, for example `base` or `subdued`. |
| `Padding` | `string?` | `null` | Padding on all edges, in the 1-to-4-value CSS shorthand. |
| `PaddingBlock` | `string?` | `null` | Block-axis padding, overriding `Padding`. |
| `PaddingBlockStart` | `string?` | `null` | Block-start padding, overriding `PaddingBlock`. |
| `PaddingBlockEnd` | `string?` | `null` | Block-end padding, overriding `PaddingBlock`. |
| `PaddingInline` | `string?` | `null` | Inline-axis padding, overriding `Padding`. |
| `PaddingInlineStart` | `string?` | `null` | Inline-start padding, overriding `PaddingInline`. |
| `PaddingInlineEnd` | `string?` | `null` | Inline-end padding, overriding `PaddingInline`. |
| `Border` | `string?` | `null` | Border shorthand: width, then colour, then style. |
| `BorderWidth` | `string?` | `null` | Border width, overriding `Border`. |
| `BorderStyle` | `string?` | `null` | Border style, overriding `Border`. |
| `BorderColor` | `string?` | `null` | Border colour, overriding `Border`. |
| `BorderRadius` | `string?` | `null` | Corner radius, in the 1-to-4-value shorthand. |
| `BlockSize` | `string?` | `null` | Height on the block axis. |
| `MinBlockSize` | `string?` | `null` | Minimum block size. |
| `MaxBlockSize` | `string?` | `null` | Maximum block size. |
| `InlineSize` | `string?` | `null` | Width on the inline axis. |
| `MinInlineSize` | `string?` | `null` | Minimum inline size. |
| `MaxInlineSize` | `string?` | `null` | Maximum inline size. |
| `Overflow` | `string?` | `null` | `visible` or `hidden`. |
| `Display` | `string?` | `null` | `auto` or `none`. |

Inherited: `ChildContent` from [PolarisContentComponentBase.cs.md](../PolarisContentComponentBase.cs.md),
plus `Id`, `AccessibilityLabel`, `ItemTag`, `Slot` and `AdditionalAttributes` from
[PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md).

## Behaviour

- **A cell is a box plus a span, and nothing else.** It carries no gap and no alignment parameters by
  design: `Gap`, `JustifyItems`, `AlignItems` and their siblings belong to the `PolarisGrid` around it,
  so a cell cannot disagree with its grid about the gutter.
- The styling parameters are the same shared set a grid or a box takes, which is why a cell can be
  given a background and a border without a wrapper `div`.

## Gotchas

- **Only valid inside a grid.** An `s-grid-item` outside an `s-grid` has no tracks to span, so the
  spans are ignored and the element renders as a plain block — nothing fails, it just is not a cell.
- **A span larger than the track count is not clamped.** `GridColumn="span 3"` inside a two-column grid
  produces an implicit third track in CSS, widening the whole grid rather than being reduced to two.
- **`Display="none"` hides the cell but keeps its track.** The column still occupies its width,
  because the track is created by the grid's template, not by the item; use the grid's own `Display`
  for hiding the whole thing.

## See also

- [PolarisGrid.cs.md](PolarisGrid.cs.md) — the grid that owns gaps, alignment and the track template.
- [PolarisBox.cs.md](PolarisBox.cs.md) — the same box styling when no grid is involved.
- [PolarisStack.cs.md](PolarisStack.cs.md) — one-axis spacing without tracks.
