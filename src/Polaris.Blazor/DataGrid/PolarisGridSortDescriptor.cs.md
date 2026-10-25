<!-- summary-of: PolarisGridSortDescriptor.cs -->
<!-- source-sha256: aee352e3273a -->

# PolarisGridSortDescriptor.cs

One column of a sort order, with the priority that decides which column is sorted first — so a grid
sorts by several columns with two descriptors instead of a composite key the caller has to build.

`Polaris.Blazor.DataGrid` · one positional `record`, not a component · a value the grid state holds and the pager/popover code builds

## Members

| Member | Signature | What it does |
|---|---|---|
| Constructor | `PolarisGridSortDescriptor(string Field, PolarisGridSortDirection Direction, int Priority = 0)` | Positional record: the three properties below are constructor parameters and public properties at once. |
| `Field` | `string Field { get; init; }` | The `Id` of the `PolarisGridColumnDefinition<TItem>` being sorted — a column id, not a property name. |
| `Direction` | `PolarisGridSortDirection Direction { get; init; }` | Which way the values are ordered (ascending or descending). |
| `Priority` | `int Priority { get; init; }` | Lower numbers sort first. The default is 0. |

## Behaviour

- **More than one descriptor sorts by several columns.** "Carat, then name" is two descriptors, not a
  composite key; the state applies them in `Priority` order.
- **Ties at every level keep their source order**, because the underlying ordering is stable. Two
  descriptors that compare equal never shuffle the rows.
- Being a record, equality and `ToString` are generated: two descriptors with the same field and
  direction but different priorities are *not* equal.

## Gotchas

- `Priority` is only consulted when several descriptors exist at once. The single-column entry points on
  the state (`SetSort`, and therefore every header click through `ToggleSort`) clear the list and add
  one descriptor at the default priority 0, so an order restored through `SetSorts` can disagree with
  the order a later header click produces.
- `Field` is not validated here. An empty or misspelt id is accepted by the record and only becomes
  visible as a sort that does nothing.

## See also

- [PolarisGridState.cs.md](PolarisGridState.cs.md) — `SetSorts`, `AddSort` and `ToggleSort`, which build
  and replace these.
- [PolarisGridEnums.cs.md](PolarisGridEnums.cs.md) — `PolarisGridSortDirection`.
- [PolarisGridColumnDefinition.cs.md](PolarisGridColumnDefinition.cs.md) — the `Id` that `Field` names.
