<!-- summary-of: PolarisTableBody.cs -->
<!-- source-sha256: d11027f61a9b -->

# PolarisTableBody.cs

The body of a `PolarisTable`: the element that holds the data rows, so an `s-table-row` is rendered
where Polaris expects it. It adds no parameters and no logic of its own — its whole job is to be the
row element's parent.

`Polaris.Blazor.Components` · renders `<s-table-body>` · base `PolarisContentComponentBase`

## Parameters

None of its own. `ChildContent` — the rows — is inherited from
[PolarisContentComponentBase.cs.md](../PolarisContentComponentBase.cs.md), along with `Id`,
`AccessibilityLabel`, `ItemTag`, `Slot` and `AdditionalAttributes` from
[PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md).

## Behaviour

- **It is structural.** The class overrides `TagName` and nothing else: no attributes, no DOM
  properties, no events. Every table it appears in renders the same plain `<s-table-body>` element.
- **The rows are light-DOM children.** A `PolarisTableRow` placed in `ChildContent` becomes a child
  of the body element in the order written; nothing here filters, validates or reorders them.
- A body with no `ChildContent` renders an empty `<s-table-body>` — no placeholder row and no "no
  data" message. An empty-state belongs to the page, typically a `PolarisEmptyState` beside the
  table rather than inside it.
- The body carries no loading state of its own. `Loading` is a parameter of `PolarisTable`, which
  applies its skin to the cells and keeps the column widths during a refresh.

## Gotchas

- **A row placed directly inside `s-table` renders nothing.** `s-table-row` must sit in this element,
  and the failure is silent: the markup is well-formed, the table draws, and the data area is blank.
- Header cells do not belong here. A `PolarisTableHeader` inside the body lands in the data area
  with no column relationship to the rows beneath it.
- Under Blazor's renderer the parent element reaches the DOM before its children, so Polaris can log
  a missing-child warning on an interactive render while the table still draws correctly. The warning
  is the element's upgrade-time check, not a fault in this wrapper.
- Because the body holds no data of its own, "the table is empty" is always an empty child
  collection — there is no parameter here whose default empties it.

## See also

- [PolarisTable.cs.md](PolarisTable.cs.md) — the shell this body belongs in.
- [PolarisTableRow.cs.md](PolarisTableRow.cs.md) — one row, and its row-level click delegate.
- [PolarisTableCell.cs.md](PolarisTableCell.cs.md) — the cells inside a row.
- [PolarisTableHeaderRow.cs.md](PolarisTableHeaderRow.cs.md) — the column headings.
