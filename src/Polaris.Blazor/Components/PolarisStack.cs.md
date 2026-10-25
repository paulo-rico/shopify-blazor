<!-- summary-of: PolarisStack.cs -->
<!-- source-sha256: faac6a296764 -->

# PolarisStack.cs

The layout primitive for a single axis of spacing: children are laid out in a block (vertical) or
inline (horizontal) line with one gap between them. It is the default container for form fields and
toolbar rows.

`Polaris.Blazor.Components` · renders `<s-stack>` · base `PolarisContentComponentBase`

## Parameters

| Parameter | Type | Default | What it does |
|---|---|---|---|
| `Direction` | `PolarisStackDirection` | `PolarisStackDirection.Block` | `Block` lays children out vertically, top to bottom; `Inline` horizontally. Rendered as `direction="block"` / `"inline"`. |
| `Gap` | `PolarisGap` | `PolarisGap.None` | Space between children on both axes: `None`, `Small`, `Base`, `Large`. **Default is no gap.** |
| `RowGap` | `PolarisGap?` | `null` | Overrides `Gap` between rows only. Null leaves `Gap` in charge; the attribute is omitted rather than defaulted. |
| `ColumnGap` | `PolarisGap?` | `null` | Overrides `Gap` between columns only, same rule. |
| `JustifyContent` | `string?` | `null` | Main-axis alignment, passed through verbatim as `justifyContent`. |
| `AlignItems` | `string?` | `null` | Cross-axis alignment for a single line — the one to use almost always. Passed through verbatim. |
| `AlignContent` | `string?` | `null` | Cross-axis alignment for *wrapping* content, i.e. more than one line. Passed through verbatim. |

Inherited: `ChildContent`, `Id`, `AccessibilityLabel`, `ItemTag`, `Slot`, `AdditionalAttributes`
(see [PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md)).

## Behaviour

- The three gap parameters compose in one direction only: `RowGap` and `ColumnGap` each replace `Gap`
  on their own axis when set, and are simply absent when null. Setting only `RowGap` still applies
  `Gap` across columns.
- `Direction` and `Gap` are enums and are always written (`none` for the default gap), so a stack is
  explicitly gapless rather than inheriting spacing from a stylesheet.
- The three alignment values are **plain strings**, not enums: nothing in C# validates them. They are
  the element's own keyword strings and are forwarded untouched.

## Gotchas

- **`Gap` defaults to `None`, not `Base`.** A stack with no `Gap` renders its children flush against
  each other; every other component here that exposes a gap defaults to `Base`. Forgetting it is the
  usual reason a stack "does nothing".
- **`AlignItems` versus `AlignContent`.** For a single line — which is almost every stack —
  `AlignItems` is the working knob; `AlignContent` only redistributes wrapped lines and silently
  changes nothing otherwise.
- **A misspelled alignment value fails silently.** Because the property is a `string?`, `"centre"`,
  `"Center"` or `"end "` renders an attribute Polaris does not recognise, the layout keeps its old
  alignment, and neither the compiler nor the browser complains. Check the spelling against the
  element's documentation rather than trial and error.
- **`Direction="Inline"` does not wrap.** A row of chips or buttons longer than the container runs off
  the edge with no way to reach the end; an auto-filling
  [PolarisGrid.cs.md](PolarisGrid.cs.md) is the wrapping alternative, which is why
  [PolarisSuggestField.cs.md](PolarisSuggestField.cs.md) draws its chips in a grid and not in an
  inline stack.
- A Polaris host computes to `display: contents`, so a child element may not be the flex item the
  stack is laying out. When two fields that should share a line stack full width instead, use a grid
  of real cells rather than an inline stack.

## See also

- [PolarisGrid.cs.md](PolarisGrid.cs.md) — two-dimensional layout, and the wrapping option.
- [PolarisGridItem.cs.md](PolarisGridItem.cs.md), [PolarisSection.cs.md](PolarisSection.cs.md)
- [PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md)
