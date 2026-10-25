<!-- summary-of: PolarisTableHeaderRow.cs -->
<!-- source-sha256: f98a57ba721b -->

# PolarisTableHeaderRow.cs

The row of column headings in a `PolarisTable`. It holds `PolarisTableHeader` cells, and its cells are
the table's column definitions — Polaris pairs each body cell with the heading at the same index. A
table without it logs a missing-header-row warning and has no headings.

`Polaris.Blazor.Components` · renders `<s-table-header-row>` · base `PolarisContentComponentBase`

## Parameters

None of its own. `ChildContent` — the heading cells — is inherited from
[PolarisContentComponentBase.cs.md](../PolarisContentComponentBase.cs.md), along with `Id`,
`AccessibilityLabel`, `ItemTag`, `Slot` and `AdditionalAttributes` from
[PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md).

## Behaviour

- **It is structural.** Only `TagName` is overridden: no attributes, no DOM properties, no events.
  Everything a heading decides — alignment, list slot, content — lives on the header cells.
- **One instance is one row of headings.** The normal table has a single header row; a second one
  renders a second line of headings rather than replacing the first.
- **It is the column count.** Each heading here pairs with the cell at the same index in every body
  row, so this row is what makes a column exist. Alignment and narrow-screen behaviour come from
  `PolarisTableHeader`.
- The headings are light-DOM children in the order written; nothing here sorts, groups or validates
  them.

## Gotchas

- **A table with no header row still draws its body, but has no column definitions.** Polaris logs
  `Table component is missing TableHeaderRow` at upgrade time and the body is laid out without
  headings; the data is there, so it is easy to read as a styling fault rather than a missing
  component.
- **Under Blazor's renderer the table element arrives before its header row.** Polaris's own
  upgrade-time check therefore warns on an interactive render even though this component is present
  and correct. The warning is expected noise; the rendered table is right.
- Header cells go here and body cells go in a `PolarisTableRow` inside `PolarisTableBody`. The
  wrapper does not check which component it is given, so a body cell in this row is not a column
  definition — it has no `Format` and no `ListSlot`.
- A header row placed inside `PolarisTableBody` puts the headings in the data area, where nothing
  reads them as columns.

## See also

- [PolarisTableHeader.cs.md](PolarisTableHeader.cs.md) — one heading, its alignment and list slot.
- [PolarisTableBody.cs.md](PolarisTableBody.cs.md) — the rows these headings describe.
- [PolarisTableCell.cs.md](PolarisTableCell.cs.md) — the body cells paired by index.
- [PolarisTable.cs.md](PolarisTable.cs.md) — the table shell that contains both.
