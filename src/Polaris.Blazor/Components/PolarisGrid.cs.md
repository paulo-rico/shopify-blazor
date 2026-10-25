<!-- summary-of: PolarisGrid.cs -->
<!-- source-sha256: a87f4e1c92c7 -->

# PolarisGrid.cs

A two-dimensional layout — an `<s-grid>` of rows and columns — whose tracks, gaps, alignment and box
styling are all set from CSS-capable strings rather than enums, so shorthands and `@container`
responsive values pass straight through.

`Polaris.Blazor.Components` · renders `<s-grid>` · base `PolarisContentComponentBase`

## Parameters

A `null` drops the attribute, so the element's own default applies. Each parameter is written to the
camelCase attribute of the same name.

| Parameter | Type | Default | What it does |
|---|---|---|---|
| `GridTemplateColumns` | `string?` | `null` | The column tracks and their sizes, for example `repeat(2, 1fr)` or `1fr 2fr 1fr`. Unset means a single implicit column. |
| `GridTemplateRows` | `string?` | `null` | The row tracks and their sizes, for example `1fr auto`. |
| `Gap` | `string?` | `null` | Gap on both axes: one value, or a two-value shorthand. See the ambiguity note below. |
| `RowGap` | `string?` | `null` | Gap on the block axis, overriding `Gap` there. |
| `ColumnGap` | `string?` | `null` | Gap on the inline axis, overriding `Gap` there. |
| `JustifyContent` | `string?` | `null` | Inline-axis alignment of the tracks themselves within the grid. |
| `AlignContent` | `string?` | `null` | Block-axis alignment of the tracks themselves within the grid. |
| `JustifyItems` | `string?` | `null` | Inline-axis alignment of items within their own tracks. |
| `AlignItems` | `string?` | `null` | Block-axis alignment of items within their own tracks. |
| `PlaceContent` | `string?` | `null` | Shorthand setting both alignment axes for the tracks, instead of `AlignContent` + `JustifyContent`. |
| `PlaceItems` | `string?` | `null` | Shorthand setting both alignment axes for the items, instead of `AlignItems` + `JustifyItems`. |
| `AccessibilityRole` | `string?` | `null` | ARIA role of the grid, as a bare string (`list`, `presentation`, …). |
| `AccessibilityVisibility` | `PolarisAccessibilityVisibility?` | `null` | `Visible` for everyone, `Exclusive` for screen readers only, `Hidden` for neither. `null` drops the attribute. |
| `Background` | `string?` | `null` | Background treatment, for example `base` or `subdued`. |
| `Padding` | `string?` | `null` | Padding on all edges, in the 1-to-4-value CSS shorthand. |
| `PaddingBlock` | `string?` | `null` | Block-axis padding, overriding `Padding`. |
| `PaddingBlockStart` | `string?` | `null` | Block-start padding, overriding `PaddingBlock`. |
| `PaddingBlockEnd` | `string?` | `null` | Block-end padding, overriding `PaddingBlock`. |
| `PaddingInline` | `string?` | `null` | Inline-axis padding, overriding `Padding`. |
| `PaddingInlineStart` | `string?` | `null` | Inline-start padding, overriding `PaddingInline`. |
| `PaddingInlineEnd` | `string?` | `null` | Inline-end padding, overriding `PaddingInline`. |
| `Border` | `string?` | `null` | Border shorthand: **width, then colour, then style** — not the CSS `width style colour` order. |
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
| `Display` | `string?` | `null` | `auto` or `none` — `none` takes the whole grid out of the layout. |

Inherited: `ChildContent` from [PolarisContentComponentBase.cs.md](../PolarisContentComponentBase.cs.md),
and `Id`, `AccessibilityLabel`, `ItemTag`, `Slot`, `AdditionalAttributes` from
[PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md). `ChildContent` is where the
`PolarisGridItem` cells go.

## Behaviour

- **Sizes and gaps are strings because one enum cannot express them.** Polaris accepts CSS units,
  multi-value shorthands and responsive `@container` values; the value is written into the attribute
  verbatim and nothing here validates it, so an invalid length is dropped by the element's own CSS with
  no exception and no console message from this library.
- **A grid only earns its complexity when content spans tracks or must reflow by column count.** For a
  single axis of spacing, `PolarisStack` is the simpler element and the right default.
- No parameter is required. A grid with no template columns is a single-column grid, which is normally
  a sign that a stack was wanted.

## Gotchas

- **`Gap`'s two-value pair is ambiguous.** Shopify's prose and its type definition disagree about which
  axis the pair sets, so treat `RowGap` and `ColumnGap` as the unambiguous spelling whenever the value
  has two parts.
- **There is no subgrid support.** A nested grid cannot align to its parent's tracks, so a nested grid
  is always an independent grid — reach for `PolarisGridItem`'s `GridColumn` / `GridRow` spans instead
  of nesting when the alignment matters.
- **`Border` is width-first.** Writing `Border="1px solid base"` follows the CSS habit but puts the
  colour where the style belongs; the documented order here is width, colour, style.
- **Shorthand and longhand are separate attributes, and both are written.** The per-axis parameters
  are documented as overriding the shorthand (`PaddingBlockStart` over `PaddingBlock` over `Padding`),
  so combining them is legal and intended — but nothing warns when the shorthand already says what the
  longhand repeats.

## See also

- [PolarisGridItem.cs.md](PolarisGridItem.cs.md) — a cell, including the track spans a grid cannot express.
- [PolarisStack.cs.md](PolarisStack.cs.md) — the one-axis alternative, usually the better choice.
- [PolarisBox.cs.md](PolarisBox.cs.md) — the same shared box styling on a single element.
- [PolarisGridColumn.cs.md](PolarisGridColumn.cs.md) — unrelated despite the name: a data-grid column, not a grid track.
