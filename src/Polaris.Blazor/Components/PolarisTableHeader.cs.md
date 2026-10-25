<!-- summary-of: PolarisTableHeader.cs -->
<!-- source-sha256: e79a03068f7e -->

# PolarisTableHeader.cs

One column heading in a `PolarisTableHeaderRow`: it sets the column's alignment and how the column
appears when the table collapses to a list. Its text is `ChildContent`, and it accepts light-DOM
content, so the heading itself can be a button — which is how sortable columns are built.

`Polaris.Blazor.Components` · renders `<s-table-header>` · base `PolarisContentComponentBase`

## Parameters

| Parameter | Type | Default | What it does |
|---|---|---|---|
| `Format` | `PolarisTableHeaderFormat` | `Base` | Alignment of the column **and of the cells beneath it**: `Base` left, `Numeric` right for counts, `Currency` right for money. Always emitted, including the default. |
| `ListSlot` | `PolarisTableListSlot` | `Labeled` | How this column appears when the table collapses to a list on a narrow screen: `Labeled` (header text beside the value), `Primary` (the row's headline), `Secondary` (supporting detail), `Kicker` (leading metadata such as a status), `Inline` (shares the line with the primary value). Always emitted. |

Inherited: `ChildContent` (the heading text, from
[PolarisContentComponentBase.cs.md](../PolarisContentComponentBase.cs.md)), plus `Id`,
`AccessibilityLabel`, `ItemTag`, `Slot` and `AdditionalAttributes` from
[PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md).

## Behaviour

- **`Format` is the only alignment control in the table.** Body cells have no equivalent, so the
  right-aligned money or count column is declared here once and every cell under it follows.
- **`ChildContent` is not restricted to text.** A sortable column is an `s-button` inside the
  header, which keeps sorting keyboard-reachable and gives the control a real accessible name;
  `PolarisDataGrid` builds its headings exactly that way.
- **`ListSlot` is about the narrow-screen presentation, not the table's normal layout.** In the
  list-style presentation the columns become labelled key/value pairs, and `ListSlot` decides which
  value becomes the headline and which sit beside it.
- Both attributes are written unconditionally, so a header with no parameters still states
  `format="base"` and `listSlot="labeled"` — this component has no "omit it and let the element
  decide" state.

## Gotchas

- **Without one `Primary` column, the collapsed row has no headline.** Every value then renders under
  its own label, which reads as a label/value list rather than a row. Mark the most identifying
  column — usually the name — `ListSlot="Primary"`.
- **There is no way to leave `Format` or `ListSlot` unset.** Both properties are non-nullable enums
  with defaults, so the element's own default never applies and an inherited value cannot shine
  through. `Base` is the element's left-aligned default, so this is usually invisible; it becomes
  visible if a future Polaris default changes.
- **The heading is the column's identity, not just its label.** It is the reference that body cells
  are counted against, so adding or removing a header without matching every body row is a silent
  column shift — see [PolarisTableCell.cs.md](PolarisTableCell.cs.md).
- A header cell outside a `PolarisTableHeaderRow` gets no column row and no index.
- Under Blazor's renderer the table element arrives before its header row, so Polaris may log a
  missing-child warning while the table still renders correctly.

## See also

- [PolarisTableHeaderRow.cs.md](PolarisTableHeaderRow.cs.md) — the row a heading must sit in.
- [PolarisTableCell.cs.md](PolarisTableCell.cs.md) — the body cells paired with these headings.
- [PolarisTable.cs.md](PolarisTable.cs.md) — the table shell and its list presentation.
- [PolarisDataGrid.cs.md](PolarisDataGrid.cs.md) — sortable headings built from this component.
