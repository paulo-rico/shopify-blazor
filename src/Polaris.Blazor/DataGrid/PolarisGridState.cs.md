<!-- summary-of: PolarisGridState.cs -->
<!-- source-sha256: 61e91bfffa40 -->

# PolarisGridState.cs

The state one grid shares with the components composed around it — the filter it applies, the order it
is in and the page it is on — and the only thing that makes a separately placed grid, filter panel and
pager behave as one control.

`Polaris.Blazor.DataGrid` · abstract, `IDisposable`, renders nothing · base of `PolarisGridState<TItem>`

## Members

| Member | Signature | What it does |
|---|---|---|
| `Query` | `PolarisGridQueryState Query { get; }` | The filters, sorts and paging the grid applies. Live and mutable, but the methods below are what keep `PageIndex` and the notification consistent. |
| `Columns` | `abstract IReadOnlyList<IPolarisGridColumn> Columns { get; }` | Columns registered by the grid, in declaration order. |
| `RegisterColumn` | `abstract void RegisterColumn(IPolarisGridColumn column)` | Registers one declaration. Untyped, so a column can register through whichever host encloses it — a table grid or a filter on a card screen — without the host knowing the row type. The concrete state casts it. |
| `UnregisterColumn` | `abstract bool UnregisterColumn(string id)` | Forgets a column, and the filters and sorts naming it; returns false when it was not there. |
| `TotalCount` | `abstract int TotalCount { get; }` | How many rows matched the filters, before paging. |
| `FirstRecordNumber` | `abstract int FirstRecordNumber { get; }` | One-based number of the first row on the visible page, or zero when there are none. |
| `LastRecordNumber` | `abstract int LastRecordNumber { get; }` | One-based number of the last row on the visible page, or zero when there are none. |
| `IsEmpty` | `abstract bool IsEmpty { get; }` | True when the visible page holds no rows. |
| `PageCount` | `int PageCount { get; }` | Pages the filtered result fills; zero when `PageSize <= 0` or nothing matched. The ceiling is done in `long` arithmetic so a huge count cannot overflow. |
| `HasPreviousPage` | `bool HasPreviousPage { get; }` | `PageIndex > 0` **and** `PageCount > 0` — so a stale page index over an empty result is not offered a "previous". |
| `HasNextPage` | `bool HasNextPage { get; }` | `PageCount > 0 && PageIndex + 1 < PageCount`. |
| `IsLoading` | `bool IsLoading { get; private set; }` | True while the caller is fetching or recomputing, so the pager can disable itself. |
| `ErrorMessage` | `string? ErrorMessage { get; private set; }` | Message to show instead of the rows, when a load failed outside the query. |
| `SetLoading` | `void SetLoading(bool isLoading)` | Sets `IsLoading` and notifies. A no-op when the value is unchanged. |
| `SetError` | `void SetError(string? message)` | Sets or clears `ErrorMessage` and notifies. A no-op when the string is unchanged (`Ordinal`). |
| `Changed` | `event Action? Changed` | Raised once per change, after the result has been recomputed — one event, not one per aspect. |
| `SetFilter` | `void SetFilter(PolarisGridFilterDescriptor filter)` | **Replaces** the filter for `filter.Field`; removes it instead when `filter.IsEmpty`. Resets to the first page. Throws on a null descriptor or an empty `Field`. |
| `ClearFilter` | `void ClearFilter(string field)` | Removes that column's filter and resets to the first page; no-op (and no notification) when there was none. |
| `ClearAllFilters` | `void ClearAllFilters()` | Drops every column filter **and** the global search, resets the page; no-op when `Query` held no filter at all. |
| `SetGlobalSearch` | `void SetGlobalSearch(string? term)` | The text every searchable column is matched against. Trimmed; null or whitespace clears it. Resets to the first page; no-op when the normalised term is unchanged. |
| `HasFilter` | `bool HasFilter(string field)` | True when that column has a filter. |
| `SetSort` | `void SetSort(string field, PolarisGridSortDirection direction)` | Sorts by one column, **replacing** any existing order. Does *not* reset the page. Throws on an empty field. |
| `AddSort` | `void AddSort(string field, PolarisGridSortDirection direction)` | Drops that field's existing sort, then appends with `Priority = Query.Sorts.Count` — so added sorts apply after the ones already there. |
| `ToggleSort` | `void ToggleSort(string field)` | Cycles one column: ascending, then descending, then unsorted. The third state is what lets a merchant get back to the dataset's own order. |
| `ClearSort` | `void ClearSort()` | Drops the whole order, restoring the dataset's own; no-op when nothing was sorted. |
| `SetSorts` | `void SetSorts(IEnumerable<PolarisGridSortDescriptor> sorts)` | Replaces the whole order, which is what restoring a saved view does. Throws on null. |
| `SortDirectionFor` | `PolarisGridSortDirection? SortDirectionFor(string field)` | Which way that column is sorted, or null when it is not. |
| `SetPage` | `void SetPage(int pageIndex)` | Moves to a zero-based page, clamped to the pages that exist by `PolarisGridQueryEngine.NormalizePageIndex`. No-op when the clamp lands on the current page. |
| `SetPageSize` | `void SetPageSize(int pageSize)` | Changes the size, clamped by `NormalizePageSize`, and stays on the **record** the merchant was looking at rather than on the page number. |
| `Refresh` | `abstract void Refresh()` | Re-runs the query over the current snapshot and notifies subscribers. |
| `RaiseChanged` | `protected void RaiseChanged()` | Invokes `Changed`, unless the state has been disposed. |
| `GetNumericBounds` | `abstract (decimal? Minimum, decimal? Maximum) GetNumericBounds(string columnId)` | Numeric bounds of a column across the whole dataset, ignoring filters. |
| `GetDateBounds` | `abstract (DateOnly? Minimum, DateOnly? Maximum) GetDateBounds(string columnId)` | Date bounds of a column across the whole dataset, ignoring filters. |
| `GetDistinctValues` | `abstract IReadOnlyList<object?> GetDistinctValues(string columnId)` | Distinct values of a column, sorted, for a multi-select filter's options. |
| `Dispose` | `void Dispose()` | Stops notifying and clears the event. Does not dispose anything the caller owns. |

## Behaviour

- **One instance, three components.** A page creates it, hands the same reference to
  `PolarisDataGrid<TItem>`, `PolarisDataFilter` and `PolarisDataPager`, and each mutation recomputes once
  and notifies once — a filter change reaches the grid, the chips and the pager in the same render.
- **Not a component, deliberately.** No markup, no lifecycle; it can be created, driven and asserted on
  in a plain unit test. Components must not create or dispose it.
- **Not thread-safe, deliberately.** It lives on the circuit's dispatcher and is mutated from event
  handlers there.
- **Paging rules that differ per mutation.** Any *filter* change (`SetFilter`, `ClearFilter`,
  `ClearAllFilters`, `SetGlobalSearch`) resets to page one, because page 4 of the old result may not exist
  in the new one. A *sort* change keeps the page. A *page-size* change keeps the first visible record:
  `PageIndex * PageSize` is computed with the old size, then divided by the new one.
- **`SetPageSize` is the only method that recomputes an index from a record number**, which is why the
  second page of 25 is not the second page of 100.
- Concrete subclasses own the data and do the work: `Refresh`, the three bound lookups and `Columns` are
  all abstract here.

## Gotchas

- **Writing `Query` directly bypasses both the reset and the notification.** Setting `Query.PageIndex` or
  `Query.Filters` by hand leaves the pager on a page that no longer exists, with nothing re-rendered.
- **`SetFilter` replaces rather than appends.** Two filters on one column are an AND the controls cannot
  express, and keeping both would make the chip for each one wrong. A descriptor with `IsEmpty` is how a
  filter is removed through this method.
- **`IsLoading` and `ErrorMessage` are read-only for a reason.** A settable property would be the one part
  of the state that changes without notifying anyone — the grid would keep showing rows while the data
  behind them was replaced, and nothing would say why.
- **A no-op change raises nothing.** `SetLoading(false)` when already false, `SetError` with the same
  string, `ClearSort` with no sort, `SetPage` that clamps to the current page: all return silently, so a
  subscriber waiting for a wake-up will not get one.
- **`Dispose` makes the object inert rather than throwing.** `RaiseChanged` checks a disposed flag and
  `Changed` is set to null, so a late `SetFilter` from a disposed page mutates the query and tells nobody.
- Argument checks throw rather than ignore: a null filter descriptor, an empty `Field`, an empty sort field,
  or a null `SetSorts` argument. They are programmer errors, not merchant input.

## See also

- [PolarisGridStateOfT.cs.md](PolarisGridStateOfT.cs.md) — the concrete state over a local collection.
- [PolarisGridQueryState.cs.md](PolarisGridQueryState.cs.md) — the `Query` object these methods mutate.
- [PolarisGridFilterDescriptor.cs.md](PolarisGridFilterDescriptor.cs.md) — `IsEmpty`, which decides whether
  `SetFilter` applies or removes.
- [PolarisGridQueryEngine.cs.md](PolarisGridQueryEngine.cs.md) — `NormalizePageIndex` and
  `NormalizePageSize`, and the ordering the sort descriptors produce.
