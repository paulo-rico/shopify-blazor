<!-- summary-of: PolarisGridQueryEngine.cs -->
<!-- source-sha256: 1b24bd00f5b1 -->

# PolarisGridQueryEngine.cs

The local query: it takes an in-memory snapshot, applies the filters and the global search, counts what
matched, sorts it and cuts one page. Everything about a grid's result is decided here, in a fixed
order, which is why it is a plain static class with no reference to a component, a lifecycle method or
the renderer — and why a wrong page count is a failing test rather than a visibly odd page. `Match`
exposes the same filtering without the ordering or the page, which is what the filter panel's narrowing
is built on.

`Polaris.Blazor.DataGrid` · `public static class` · `Execute<TItem>`, `Match<TItem>` plus the page-size constants and clamps

## Members

| Member | Signature | What it does |
|---|---|---|
| `MinimumPageSize` | `const int` = `1` | The smallest page size a pager may use. |
| `MaximumPageSize` | `const int` = `10_000` | The largest page size a pager may use. A page is rendered into the DOM, so an unbounded size is a request for the whole dataset in markup. |
| `Execute<TItem>` | `static PolarisGridQueryResult<TItem> Execute<TItem>(IReadOnlyList<TItem> items, IReadOnlyList<PolarisGridColumnDefinition<TItem>> columns, PolarisGridQueryState state)` | Runs the query and returns the visible page, after normalising the page size and clamping the page index. Throws `ArgumentNullException` when `items`, `columns` or `state` is null. |
| `Match<TItem>` | `static IReadOnlyList<TItem> Match<TItem>(IReadOnlyList<TItem> items, IReadOnlyList<PolarisGridColumnDefinition<TItem>> columns, PolarisGridQueryState state)` | Every row the filters keep, in the snapshot's own order, before sorting and paging. `Execute` is this plus the ordering and one page. |
| `NormalizePageSize` | `static int NormalizePageSize(int pageSize)` | Clamps into `[1, 10_000]`. Zero and negatives become **1** — not "all rows". |
| `NormalizePageIndex` | `static int NormalizePageIndex(int pageIndex, int totalCount, int pageSize)` | Clamps an index against the pages a count fills; an empty result has exactly one valid index, **0**, so a pager never shows a page number that cannot exist. |

## Behaviour

- **The order is the whole contract:** source snapshot → filters and global search, AND-ed → total
  count of what matched → sort → page. Counting before paging is what makes "26–50 of 148" possible;
  sorting before paging is what makes the order stable *across* pages rather than within one. In
  detail:
  1. The caller's `IReadOnlyList<TItem>` is enumerated **once** into a working list, so the rest of the
     query is decided against a stable set.
  2. One predicate per applicable descriptor, plus one for the search term; a row survives only if all
     of them pass.
  3. `TotalCount` is the size of that matched list, taken before any paging.
  4. The matched list is sorted, and only then cut from `PageIndex × PageSize` for at most `PageSize`
     rows.
- **Columns are indexed by `Id` with an `Ordinal` comparer.** Every filter and sort resolves its column
  through that dictionary, so a filter names a column id and never a property path.
- **A filter is dropped, not applied, when it cannot be evaluated.** Four cases: an empty `Field`, a
  descriptor whose `IsEmpty` is true, a field that matches no registered column, and a value that
  `PolarisGridValues.TryCoerce` cannot convert to the column's `ValueType`. A filter that silently
  matched nothing would look exactly like an empty dataset, and the merchant has no way to tell the
  two apart.
- **Global search** trims the term, then matches it as an `OrdinalIgnoreCase` substring against the
  invariant `ToString` of **searchable and visible** columns only. A row is kept if any one of them
  contains it. A null cell value never matches.
- **`Between` accepts either bound missing** (the absent end simply does not constrain), and swaps the
  bounds when they arrive the wrong way round — "a range whose ends arrive the wrong way round is still
  a range", and it spares every caller from validating what a slider already swaps.
- **A null cell value is tested on the value itself, not on its text**, because `Convert.ToString(null)`
  is the empty string — so `IncludeNulls` would otherwise have no effect on an empty cell.
- **All three text operators are `OrdinalIgnoreCase`** — `Contains`, `StartsWith` and `EndsWith` differ
  only in which `string` method they call, not in their comparison.
- **`Equals`/`NotEquals` with a null expected value become null tests**, not filters that match no row:
  "equals nothing" means "has no value", and "not equals nothing" means "has a value".
- **Sorting is stable and multi-key.** Descriptors are ordered by `Priority` and, for equal priorities,
  by the order they were added; unknown fields are skipped. Values compare through
  `PolarisGridValueComparer.Instance`, which puts empty values after everything when ascending.
- **Sorting and filtering never copy the rows** — the result's `Items` holds references into the source
  list, which is what makes in-place record editing visible to the grid.
- **`Match` is the same filter pass without the page.** It exists for the filter panel: a control's
  choices are read from the rows that pass every *other* applied filter, and a facet that only saw the
  visible page would describe a different dataset on page 2.
- The result always reports the page size and page index **actually used**, normalised and clamped,
  rather than the ones the state asked for.

## Gotchas

- **A page size of 0 means one row per page.** `NormalizePageSize` clamps to `MinimumPageSize`, so a
  page size bound from an empty configuration field silently paginates one row at a time.
- **Duplicate column ids are resolved last-wins.** The dictionary is filled with
  `byId[column.Id] = column`, so when two columns share an id, filters and sorts read the **last** one —
  a filter attached to the visible column can therefore be reading a different column entirely.
- **A `Between` filter with both bounds null is dropped**, and an `In` filter is dropped entirely if
  **any** one of its candidates fails to coerce — not just the failing candidate. A multi-select with a
  single stale value therefore filters nothing at all.
- **A comparison operator with a null operand becomes a null test.** `GreaterThan` with an
  uncoercible, null value does not mean "greater than nothing"; it means "has no value", because the
  expected-null branch is taken before the operator is inspected.
- **Global search ignores hidden columns.** Hiding a column removes it from search, so the same term
  can find fewer rows after a column is hidden — an effect nothing in the UI explains.
- **The engine does not write the clamped page index back.** `state.PageIndex` keeps whatever the
  caller set; the result reports the clamped value. After a filter shrinks the dataset the two
  disagree until the state updates itself.
- **`Execute` is synchronous and in-memory.** It is not a LINQ query over a provider: there is no
  deferred execution and no translation, so a caller that wants a database query implements its own
  path rather than expecting this one to push predicates down.
- A sort is skipped entirely when there are fewer than two matched rows, so a one-row page keeps source
  order — harmless, but it means a sort cannot be tested from a single-row fixture.

## See also

- [PolarisGridQueryState.cs.md](PolarisGridQueryState.cs.md) — the filters, sorts, page and search
  term this reads.
- [PolarisGridQueryResult.cs.md](PolarisGridQueryResult.cs.md) — the shape it returns, including the
  one-based record numbers.
- [PolarisGridValues.cs.md](PolarisGridValues.cs.md) — the coercion and comparison rules every
  predicate here relies on.
- [PolarisGridFilterDescriptor.cs.md](PolarisGridFilterDescriptor.cs.md) — `IsEmpty`, which decides
  which filters are skipped before the engine ever sees them.
