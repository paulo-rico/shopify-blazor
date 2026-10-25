<!-- summary-of: PolarisGridQueryResult.cs -->
<!-- source-sha256: 28a4063e0190 -->

# PolarisGridQueryResult.cs

One page of a query plus what it took to get there: the visible rows, how many matched across every
page, the page index actually used after clamping, and the page size. The derived numbers — page
count, previous/next, first and last record — are what a pager prints, and they are computed here so
that "26–50 of 148" is arithmetic rather than guesswork in the component.

`Polaris.Blazor.DataGrid` · `public sealed record` with derived page metrics · not a component

## Members

| Member | Signature | What it does |
|---|---|---|
| Constructor | `PolarisGridQueryResult(IReadOnlyList<TItem> Items, int TotalCount, int PageIndex, int PageSize)` | Positional record; the four parameters below are also public init-only properties. |
| `Items` | `IReadOnlyList<TItem> Items` | The rows of the visible page, in sorted order. |
| `TotalCount` | `int TotalCount` | How many rows matched the filters, **across every page** — counted before paging. |
| `PageIndex` | `int PageIndex` | The zero-based page these rows belong to, **after clamping** — not necessarily what was asked for. |
| `PageSize` | `int PageSize` | The page size the page was cut with, after normalisation. |
| `PageCount` | `int PageCount { get; }` | How many pages the filtered result fills. **Zero** when nothing matched or the page size is not positive. |
| `IsEmpty` | `bool IsEmpty { get; }` | True when this page holds no rows — which is also true of an empty result. |
| `HasPreviousPage` | `bool HasPreviousPage { get; }` | True when a page comes before this one. |
| `HasNextPage` | `bool HasNextPage { get; }` | True when a page comes after this one. |
| `FirstRecordNumber` | `int FirstRecordNumber { get; }` | One-based number of the first row on this page, or 0 when there are none. |
| `LastRecordNumber` | `int LastRecordNumber { get; }` | One-based number of the last row on this page, or 0 when there are none. |
| `Empty` | `static PolarisGridQueryResult<TItem> Empty(int pageSize = 25)` | An empty result for a page size: no rows, `TotalCount` 0, `PageIndex` 0. |

## Behaviour

- **Counting happens before paging, and the record keeps both.** `TotalCount` is the number that
  matched, not the number on the page, which is what a pager needs for "26–50 of 148" and what
  `PageCount` is derived from.
- **An empty result reports no pages rather than one empty one**, so a pager cannot offer a first page
  that holds nothing. Every navigation property is additionally guarded on `PageCount > 0`, so an
  empty result also has no previous or next page.
- `PageCount` rounds up (`ceil(TotalCount / PageSize)`) and does the arithmetic in `long`, so a dataset
  whose count approaches `int.MaxValue` cannot overflow into a negative page count.
- `FirstRecordNumber` and `LastRecordNumber` are **one-based**, and both answer 0 for a page with no
  rows — a pager can hide its "showing x–y" line by testing for 0 rather than special-casing.
  `LastRecordNumber` is capped at `TotalCount`, so the final partial page reports its true end.
- `Empty(pageSize)` exists so a pager never has to handle a `null` result. It is used before the first
  query runs and again after the dataset is replaced.
- Being a record, two results with the same four values are equal; the derived properties are
  computed from those values, so a result is a snapshot of the page and never re-queries.

## Gotchas

- **`PageIndex` is the clamped index, not the requested one.** A caller that keeps its own page number
  in a field can therefore disagree with the result it just received — after a filter shrinks the
  dataset, the result says page 0 while the field still says 4. Read the index back from the result.
- **`IsEmpty` and `PageCount` are independent.** A result constructed by hand with a page index past
  the end reports pages and holds no rows; the engine never produces one (it clamps first), but a test
  or a cached result can. A pager that branches on `IsEmpty` alone will print "26–50 of 148" over an
  empty table.
- **`Empty()`'s page size defaults to 25**, matching the grid state's default. A grid using another
  page size should pass it, or the pager's arithmetic changes for the one render before the first
  query completes.
- **`Items` holds references to the records, not copies.** An edit to a row after the query is visible
  through a result obtained before it, which is exactly what the grid's in-place editing relies on — and
  a snapshot taken for comparison is not one.
- A `PageSize` of 0 or less yields `PageCount` 0 rather than a divide-by-zero; the engine normalises
  such a size to 1 before a page is cut, so only a hand-built result can carry it.

## See also

- [PolarisGridQueryEngine.cs.md](PolarisGridQueryEngine.cs.md) — the only producer, and where paging
  and clamping happen.
- [PolarisGridQueryState.cs.md](PolarisGridQueryState.cs.md) — the filters, sorts and page this result
  answers.
- [PolarisGridStateOfT.cs.md](PolarisGridStateOfT.cs.md) — holds the latest result and raises change
  notifications for it.
