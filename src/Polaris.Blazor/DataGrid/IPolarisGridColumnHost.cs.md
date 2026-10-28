<!-- summary-of: IPolarisGridColumnHost.cs -->
<!-- source-sha256: b791f8e93d21 -->

# IPolarisGridColumnHost.cs

The contract a component implements when it hosts column declarations: it owns the grid state a
column registers with, and it can be asked to redraw when a declaration changes in place. It is what
lets a column be declared on a table-less screen.

`Polaris.Blazor.DataGrid` · interface · implemented by `PolarisDataGrid<TItem>` and `PolarisDataFilter`

## Members

| Member | Signature | What it does |
|---|---|---|
| `State` | `PolarisGridState State { get; }` | The state a column declared inside this host registers with. Untyped, because the host does not have to know the row type at the point of registration. |
| `NotifyColumnChanged` | `void NotifyColumnChanged()` | Asks the host to redraw after a column's `Title`, `Visible`, filter order, template or editor changed in place. |

## Behaviour

- **Cascaded, not passed.** `PolarisGridColumn<TItem>` reads it as a `[CascadingParameter]`, because a
  column is written in markup where there is nowhere to put a parameter — the host is whichever
  component encloses it.
- **Two implementations, one purpose.** `PolarisDataGrid<TItem>` hosts the columns that describe its
  table. `PolarisDataFilter` hosts the columns of a screen whose rows are not a table — cards, tiles,
  a gallery — through its `Columns` fragment; the filter's controls are generated from the same
  declarations either way.
- **Registration is untrusted input.** The column component hands its definition to
  `State.RegisterColumn(IPolarisGridColumn)`; the concrete state refuses one built for a different row
  type rather than casting blindly.

## Gotchas

- **Columns are still registered with the state, never with the host.** The host is only how a column
  reaches the state; two hosts sharing one state would register into the same list.
- **The interface deliberately has no `Dispose` or lifetime.** The page owns the state; a host that
  disposed it would take every sibling component down with it.

## See also

- [PolarisGridState.cs.md](PolarisGridState.cs.md) — `RegisterColumn` and `UnregisterColumn`, the
  registry the host exposes.
- [../Components/PolarisGridColumn.cs.md](../Components/PolarisGridColumn.cs.md) — the declaration
  that reads this cascade.
- [../Components/PolarisDataFilter.cs.md](../Components/PolarisDataFilter.cs.md) — the host for a
  screen with no table.
