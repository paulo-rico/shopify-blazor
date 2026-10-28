<!-- summary-of: PolarisGridStateOfT.cs -->
<!-- source-sha256: 6a9b8e74cf81 -->

# PolarisGridStateOfT.cs

The state of one grid over a local collection: it holds a materialised snapshot of the records, the
registered columns and the selection, and recomputes the visible page whenever any of the three changes.

`Polaris.Blazor.DataGrid` · sealed, renders nothing · derives from `PolarisGridState`

## Members

| Member | Signature | What it does |
|---|---|---|
| Constructor | `PolarisGridState(IEnumerable<TItem>? items = null, Func<TItem, object>? keySelector = null)` | Defaults the key selector to the item itself, starts `Result` as the empty page at `Query.PageSize`, and calls `SetItems` when `items` is supplied — which means the constructor itself runs a query. |
| `Items` | `IReadOnlyList<TItem> Items { get; }` | The records, materialised. Replaced as a whole, never edited in place. |
| `ColumnDefinitions` | `IReadOnlyList<PolarisGridColumnDefinition<TItem>> ColumnDefinitions { get; }` | The typed columns the grid registered, in registration order. |
| `Columns` | `override IReadOnlyList<IPolarisGridColumn> Columns { get; }` | The same list as the non-generic components see it. |
| `Result` | `PolarisGridQueryResult<TItem> Result { get; private set; }` | The visible page and what it took to get there. |
| `KeySelector` | `Func<TItem, object> KeySelector { get; }` | Identifies one record, so selection survives a reload. |
| `SelectedKeys` | `IReadOnlyCollection<object> SelectedKeys { get; }` | The keys of the selected records. The live set, not a copy. |
| `TotalCount` / `FirstRecordNumber` / `LastRecordNumber` / `IsEmpty` | `override int` / `bool` | Pass-throughs to `Result`. |
| `SelectionChanged` | `event Action? SelectionChanged` | Raised when the selection changes. **Not** raised for a change of page, filter or sort. |
| `SetItems` | `void SetItems(IEnumerable<TItem> items)` | Replaces the snapshot, invalidates the derived caches, drops selected keys that no longer exist, refreshes. |
| `FindItem` | `TItem? FindItem(object key)` | Finds a record by key across the **whole snapshot**, not just the visible page. Returns `default` when absent. |
| `UpdateItems` | `int UpdateItems(IReadOnlyDictionary<object, TItem> updates)` | Replaces records by row key in one pass, invalidates caches, refreshes **once**; returns how many were found and replaced. Zero when nothing matched, with no refresh. |
| `RegisterColumn` | `void RegisterColumn(PolarisGridColumnDefinition<TItem> column)` | Adds a column, invalidates caches, refreshes. Throws `ArgumentException` on a null column, an empty `Id`, or a **duplicate id**. |
| `RegisterColumn` (untyped) | `override void RegisterColumn(IPolarisGridColumn column)` | The door a column component comes in by, because its host knows the state but not the row type. Forwards to the typed overload, and refuses a declaration built for another row type with an `ArgumentException` naming the column and the row type. |
| `UnregisterColumn` | `override bool UnregisterColumn(string id)` | Forgets a column and the filters and sorts naming it; returns false when it was not there. |
| `Refresh` | `override void Refresh()` | Runs `PolarisGridQueryEngine.Execute`, **writes the engine's clamped `PageIndex` and `PageSize` back into `Query`**, then notifies. |
| `GetNumericBounds` | `override (decimal? Minimum, decimal? Maximum) GetNumericBounds(string columnId)` | Cached min/max of a column across the unfiltered snapshot. |
| `GetDateBounds` | `override (DateOnly? Minimum, DateOnly? Maximum) GetDateBounds(string columnId)` | The same for dates, using `TryToDateOnly`. |
| `GetDistinctValues` | `override IReadOnlyList<object?> GetDistinctValues(string columnId)` | Non-null distinct values from the whole snapshot, sorted with `PolarisGridValueComparer`. |
| `KeyOf` | `object KeyOf(TItem item)` | The key that identifies one record. |
| `IsSelected` | `bool IsSelected(TItem item)` | True when the record is selected. |
| `SetSelected` | `void SetSelected(TItem item, bool selected)` | Selects or deselects one record; raises `SelectionChanged` only when the set actually changed. |
| `ToggleSelection` | `void ToggleSelection(TItem item)` | Turns one record's selection on or off. |
| `ClearSelection` | `void ClearSelection()` | Clears the selection; no-op when it was already empty. |
| `SetPageSelected` | `void SetPageSelected(bool selected)` | Selects or deselects **every record on the visible page**, which is what a header checkbox does. One notification for the whole page. |

## Behaviour

- **The snapshot is copied into a list on `SetItems`.** A lazy sequence re-enumerated per query is a
  dataset that can differ between the count and the page.
- **Selection is kept by key, not by index**, so it survives sorting, filtering and paging.
- **Bounds come from the unfiltered snapshot on purpose.** A slider bounded by the *filtered* data cannot be
  widened again once narrowed, because its own ends move inwards with every drag.
- **Distinct values come from the whole snapshot too.** An option that vanished because the merchant
  filtered on it could never be cleared again.
- **One save, one refresh.** `UpdateItems` applies a whole batch and notifies once, so a page saving twelve
  edited rows does not redraw the table twelve times over a half-saved collection.
- **`Refresh` writes the engine's verdict back into `Query`**, which is what keeps a pager from showing page
  3 of a result that has one page.

## Gotchas

- **The default key is the item itself, compared by `Equals`.** Correct for a record or a struct with value
  equality; **reference identity for a class**, so a class with a meaningful identity must pass
  `keySelector` or the selection will not survive a reload.
- **`SetItems` silently drops selected keys that no longer exist, and does not raise `SelectionChanged`.**
  A page that keeps the last `SelectionChanged` payload goes on saying "1 selected" with a live Delete
  button after the row it named was deleted. Call `ClearSelection` and clear the page's own copy after a
  programmatic reload.
- **A duplicate column id throws** rather than shadowing: ids identify filters and sort orders, so two
  columns sharing one would make both ambiguous. The throw happens at registration, i.e. during the grid's
  first render.
- **`RegisterColumn` refreshes per column**, so a grid with eight columns runs eight queries while it builds
  itself. Cheap on a few hundred records, not free on a large page.
- **The three derived caches are invalidated only by this class's own mutations.** A `ValueGetter` whose
  behaviour changes behind the state's back leaves stale bounds and stale filter options until the next
  `SetItems`, `UpdateItems`, `RegisterColumn` or `UnregisterColumn`.
- **`SelectedKeys` is the live set**, not a snapshot: holding it and reading it later reads the current
  selection. Copy it if you need the payload at the moment the event fired.

## See also

- [PolarisGridState.cs.md](PolarisGridState.cs.md) — the base class and the paging/filter/sort rules.
- [PolarisGridQueryResult.cs.md](PolarisGridQueryResult.cs.md) — what `Result` carries.
- [PolarisGridColumnDefinition.cs.md](PolarisGridColumnDefinition.cs.md) — the definition `RegisterColumn`
  takes, including `Id` and `ValueGetter`.
- [PolarisGridValues.cs.md](PolarisGridValues.cs.md) — the comparison rules behind `GetDistinctValues`,
  `GetNumericBounds` and the query engine.
