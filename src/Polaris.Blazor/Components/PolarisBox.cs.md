<!-- summary-of: PolarisBox.cs -->
<!-- source-sha256: 19384780e36a -->

# PolarisBox.cs

The generic styling container: one `s-box` that paints background, padding, border, radius, size and
overflow from Polaris's own token vocabulary. It carries no behaviour at all — every parameter is a
pass-through string written under its camelCase spelling, for layout that no more specific component
expresses.

`Polaris.Blazor.Components` · renders `<s-box>` · base `PolarisContentComponentBase`

## Parameters

| Parameter | Type | Default | What it does |
|---|---|---|---|
| `Background` | `string?` | `null` | Background treatment, e.g. `subdued` or `transparent`. |
| `Padding` | `string?` | `null` | Padding on all sides. The broadest padding setting; the two axis parameters override it. |
| `PaddingBlock` | `string?` | `null` | Block-axis padding, overriding `Padding`. |
| `PaddingInline` | `string?` | `null` | Inline-axis padding, overriding `Padding`. |
| `Border` | `string?` | `null` | Border treatment, e.g. `base`. |
| `BorderStyle` | `string?` | `null` | Border style, e.g. `dashed`. |
| `BorderRadius` | `string?` | `null` | Corner radius, e.g. `base`. |
| `Overflow` | `string?` | `null` | `visible` or `hidden`. |
| `Display` | `string?` | `null` | `auto` or `none` — `none` removes the box and its children from layout. |
| `BlockSize` | `string?` | `null` | Block size; also accepts the responsive `@container (...)` form. |
| `InlineSize` | `string?` | `null` | Inline size. |
| `AccessibilityRole` | `string?` | `null` | ARIA role of the container. Without one a box is an unnamed generic element. |

Inherited: `Id`, `AccessibilityLabel`, `ItemTag`, `Slot`, `AdditionalAttributes`, `ChildContent` — see
[PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md).

## Behaviour

- Every parameter maps to the same-named attribute; there is no translation and no default supplied.
  A `null` is dropped, so an unset property leaves the attribute absent and Polaris applies its own
  default — it is not the same as passing an empty string, which renders a bare presence attribute
  that none of these properties means.
- `BuildAttributes` calls base first, so `Id`, `AccessibilityLabel` and `AdditionalAttributes` win over
  a modelled parameter of the same name.
- Padding resolves in the order `Padding` < `PaddingBlock`/`PaddingInline`. There are no per-side
  parameters here; [PolarisClickable.cs.md](PolarisClickable.cs.md) has `PaddingBlockStart` and the rest.
- Child content is rendered as-is inside the box; the component adds no wrapper element of its own.

## Gotchas

- The XML remark is a warning, not a description: reach for this only when no other layout component
  expresses the intent. Polaris components cannot be restyled with your own CSS, so a box plus custom
  CSS will not do what it looks like it should.
- The values are free strings, so `Border="dashed"` in the wrong parameter — or a misspelt token — is
  written to the element verbatim and silently does nothing. Nothing in the wrapper validates them.
- `Display="none"` hides the subtree but the children are still rendered and still hold state; this is
  not conditional rendering.

## See also

- [PolarisDivider.cs.md](PolarisDivider.cs.md), [PolarisGridItem.cs.md](PolarisGridItem.cs.md) — the
  specific layout components to prefer.
- [PolarisClickable.cs.md](PolarisClickable.cs.md) — the same styling set plus interaction.
