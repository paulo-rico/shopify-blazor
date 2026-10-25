<!-- summary-of: PolarisTableRow.cs -->
<!-- source-sha256: 685649a8ee7a -->

# PolarisTableRow.cs

One row of data cells in a `PolarisTableBody`. Beyond holding `PolarisTableCell` children it can
nominate an existing control inside the row as the target of the row's click, so the whole row acts
like that control. The wrapper raises no event of its own.

`Polaris.Blazor.Components` · renders `<s-table-row>` · base `PolarisContentComponentBase`

## Parameters

| Parameter | Type | Default | What it does |
|---|---|---|---|
| `ClickDelegate` | `string?` | `null` | Id of an interactive element **inside this row** to act as the row's click target. Null drops the attribute, leaving the row with no click behaviour. |

Inherited: `ChildContent` (the row's cells, from
[PolarisContentComponentBase.cs.md](../PolarisContentComponentBase.cs.md)), plus `Id`,
`AccessibilityLabel`, `ItemTag`, `Slot` and `AdditionalAttributes` from
[PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md).

## Behaviour

- **`ClickDelegate` names a control; it does not add one.** The value is an element id, so the click
  is delivered to that element's own handler — the row merely extends the clickable area over itself.
  There is no `OnClick` parameter here and no event is registered on the row element.
- **The named element keeps its own identity.** Because it is a real control with its own id, it is
  still focusable, still labelled, and still reachable on its own — which is what the remarks require.
- **The row is a grouping element otherwise.** Only the `clickDelegate` attribute is added; the cells
  are passed through in order, and column alignment comes from the header row.
- **It must live in the body.** Rows belong inside `PolarisTableBody`; a row appended straight to
  `s-table` renders nothing.

## Gotchas

- **The affordance is click-only.** Setting `ClickDelegate` adds no keyboard handler, no role and no
  screen-reader announcement to the row, so a row that is *only* clickable is unusable without a
  mouse. The target must be reachable and labelled in its own right — in practice a
  `PolarisButton` or `PolarisLink` in one of the cells, whose `OnClick` does the work.
- **Nothing validates the id.** A typo, an id that names nothing, or an id on a non-interactive
  element produces no error and no warning: the row is simply inert, and the page looks finished.
- **The delegate points *into* the row.** The id is resolved inside the row element, so an id
  belonging to a control elsewhere on the page cannot make this row clickable.
- Setting `ClickDelegate` back to `null` removes the attribute on the next render; there is no
  "disabled" spelling of a delegate.
- Cells in this row are positional, so a row with fewer cells than the header row shifts its later
  values under the wrong headings — see [PolarisTableCell.cs.md](PolarisTableCell.cs.md).

## See also

- [PolarisTableBody.cs.md](PolarisTableBody.cs.md) — the only valid parent for a row.
- [PolarisTableCell.cs.md](PolarisTableCell.cs.md) — the cells a row holds.
- [PolarisTable.cs.md](PolarisTable.cs.md) — the table shell.
- [PolarisDataGrid.cs.md](PolarisDataGrid.cs.md) — a grid that manages selection itself rather than
  through a row delegate.
