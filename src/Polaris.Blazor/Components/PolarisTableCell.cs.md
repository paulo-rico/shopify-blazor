<!-- summary-of: PolarisTableCell.cs -->
<!-- source-sha256: f5071220b27d -->

# PolarisTableCell.cs

One data cell in a `PolarisTableRow`. It renders `<s-table-cell>` and carries its value as child
content; the column's alignment comes from the header above it, not from the cell. A cell count that
does not match the header count shifts every later column without raising.

`Polaris.Blazor.Components` · renders `<s-table-cell>` · base `PolarisContentComponentBase`

## Parameters

None of its own. `ChildContent` — the cell's value or markup — comes from
[PolarisContentComponentBase.cs.md](../PolarisContentComponentBase.cs.md); `Id`, `AccessibilityLabel`,
`ItemTag`, `Slot` and `AdditionalAttributes` come from
[PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md).

## Behaviour

- **The cell is positional.** Polaris relates a cell to a column by its index in the row, so the
  first cell belongs under the first header. Nothing tags a value with a column name, and this
  wrapper has no `Column`, `Field` or width parameter.
- **Alignment is inherited from the heading.** `PolarisTableHeader.Format` (`Base`, `Numeric`,
  `Currency`) applies to the column *and the cells beneath it*, so a right-aligned count column is
  arranged once, on the header. There is no per-cell alignment to set here.
- **Content is free-form.** A cell holds text, a `PolarisBadge`, a stack or a control; the wrapper
  passes it through untouched.
- Empty content renders an empty cell and keeps its column — the supported way to leave a column
  blank, unlike omitting the cell.

## Gotchas

- **A header/cell count mismatch raises nothing.** The browser lays the row out against the header
  row, so every later value appears under the wrong heading and the last heading is left empty.
  Nothing logs and the result looks deliberate — count the cells when a column's values look shifted
  by one.
- A `PolarisTableCell` outside a `PolarisTableRow` has no row context; the row is what makes it a
  data cell, and the header row is what gives it a column.
- The header and the body use different components for the same element: a heading is a
  `PolarisTableHeader` (alignment and list-slot parameters), a value is this class (neither).
- The `Slot` parameter is inherited but meaningless on a cell: table cells are not slotted content,
  they are positional children.

## See also

- [PolarisTableRow.cs.md](PolarisTableRow.cs.md) — the row a cell must sit in.
- [PolarisTableHeader.cs.md](PolarisTableHeader.cs.md) — the column definition a cell is paired with.
- [PolarisTableBody.cs.md](PolarisTableBody.cs.md) — where the rows live.
- [PolarisTable.cs.md](PolarisTable.cs.md) — the table shell.
