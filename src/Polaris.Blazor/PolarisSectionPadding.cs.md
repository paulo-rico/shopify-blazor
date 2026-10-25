<!-- summary-of: PolarisSectionPadding.cs -->
<!-- source-sha256: 9aabcd99bf83 -->

# PolarisSectionPadding.cs

The two paddings an `s-section` accepts: the standard inset, or none at all for a section whose child —
a table, a grid, an accordion header — paints its own edges.

`Polaris.Blazor` · enum for `PolarisSection.Padding` · wire spellings pinned with `[PolarisValue]`

## Members

| Member | Wire value | What it does |
|---|---|---|
| `Base` | `base` | The standard inset around a section's content. The default on `PolarisSection`. |
| `None` | `none` | No padding, for a section whose child supplies its own, such as a table. |

## Behaviour

- `PolarisSection.Padding` defaults to `Base` and is written as `padding="base"` on every render, so a
  section always states its inset rather than inheriting one.
- `None` is what the library itself uses, for two different reasons, both recorded in the source:
  `PolarisDataGrid` follows Shopify's own guidance for wrapping a table — a section pads its content, so
  a padded table gets a gutter between its rules and the page edge — and `PolarisAccordionItem` drops it
  because the surface around the heading already pads, and two paddings make a header that is mostly
  space.
- The value changes the inset only. It does not change the heading, the section's border or its
  background, and it does not stop the section rendering its heading when the content is empty.

## Gotchas

- **`None` is not "compact".** It removes the inset entirely, so a section whose child does *not* paint
  its own edges puts text hard against the frame, and nothing warns: the markup is exactly what was
  asked for.
- **Only two values exist.** There is no `Small`, `Large` or size scale; a section's inset is a binary
  choice, and finer control has to come from the child.
- **`Padding` is not a way into the section's other styling.** `s-section` is not one of the elements
  that inherit the box-styling set, so a `border` or a `background` put on it through
  `AdditionalAttributes` is dropped silently — the accordion, which needs a border, puts it on the
  `s-clickable` that wraps the section instead.
- **`Base` means "default", not "medium".** The wire value is `base`, and since the parameter is
  non-nullable and assigned after the caller's attributes, `padding="base"` is always present.
- **`AdditionalAttributes` cannot override it.** `PolarisSection` calls `base.BuildAttributes` first and
  then assigns `padding`, so a same-named entry in the caller's dictionary is overwritten by the
  parameter — the enum really is the whole vocabulary here, and there is no back door to a value it
  does not list.

## See also

- [PolarisSection.cs.md](Components/PolarisSection.cs.md) — the component, with `Heading`, `Subheading`
  and the inherited slot parameters.
- [PolarisAccordionItem.cs.md](Components/PolarisAccordionItem.cs.md) — the in-library `None` call site.
- [PolarisDataGrid.cs.md](Components/PolarisDataGrid.cs.md) — the other one, for a section wrapping a
  table.
- [PolarisModalEnums.cs.md](PolarisModalEnums.cs.md) — the sibling two-value padding enum for `s-modal`.
