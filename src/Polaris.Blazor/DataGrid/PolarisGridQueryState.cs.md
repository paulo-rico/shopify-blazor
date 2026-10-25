<!-- summary-of: PolarisGridQueryState.cs -->
<!-- source-sha256: c4b1e4738835 -->

# PolarisGridQueryState.cs

The query behind a grid: which column filters apply, how the result is ordered, which page is visible,
how big a page is, and the global search term. It is a mutable value object, owned by
`PolarisGridState<TItem>` — components never change it directly, so every mutation recomputes once and
notifies once.

`Polaris.Blazor.DataGrid` · `public sealed class` · a value the grid state holds, not a component

## Members

| Member | Signature | What it does |
|---|---|---|
| `Filters` | `List<PolarisGridFilterDescriptor> Filters { get; }` | The column filters that apply, AND-ed together. The list is mutable and is created empty. |
| `Sorts` | `List<PolarisGridSortDescriptor> Sorts { get; }` | The sort order, applied in `Priority` order. Created empty. |
| `PageIndex` | `int PageIndex { get; set; }` | The **zero-based** page currently visible. Not clamped here — the engine clamps it when the query runs. |
| `PageSize` | `int PageSize { get; set; }` | How many rows a page holds. Defaults to **25**. |
| `GlobalSearch` | `string? GlobalSearch { get; set; }` | Text matched against every searchable, visible column, AND-ed with the column filters. Null or whitespace means no search. |
| `FilterFor` | `PolarisGridFilterDescriptor? FilterFor(string field)` | The filter whose `Field` matches, compared with `StringComparison.Ordinal`; the **first** match wins, or null when there is none. |
| `HasAnyFilter` | `bool HasAnyFilter { get; }` | True when at least one filter exists **or** the global search is non-whitespace. It says nothing about sorting. |
| `Clone` | `PolarisGridQueryState Clone()` | A copy of the query — page index, page size, search term, and copies of both lists. |
| `ToString` | `override string ToString()` | The state as one invariant line: `filters=N, sorts=N, page=N, size=N, search="…"`. Built with `string.Create` and the invariant culture, so a log line reads the same on every machine. |

## Behaviour

- **One filter per column, AND-ed.** The engine builds one predicate per descriptor and keeps a row
  only if all of them pass, so two descriptors for the same column intersect rather than replace.
- **`Sorts` is a list, not a single sort**, which is what makes "carat, then name" possible; the
  priority and the insertion order between them are resolved by the engine.
- **`Clone` exists for comparison**, so a caller can tell whether a change actually altered the query
  before recomputing — the returned snapshot cannot be mutated underneath it.
- `Clone` copies the lists but shares the descriptors inside them. That is safe because
  `PolarisGridFilterDescriptor` and `PolarisGridSortDescriptor` are records; note that a descriptor's
  `Values` collection is the same instance in both copies.
- `GlobalSearch` is trimmed by the engine before use, so surrounding whitespace is harmless; a
  whitespace-only term is treated as no search by both `HasAnyFilter` and the engine.

## Gotchas

- **Changing `Filters` or `Sorts` directly bypasses the state's recompute-and-notify.** The lists are
  exposed as mutable `List<T>` for the state to build with, not for a component to edit; a component
  that adds a descriptor here gets a changed query and a stale result, with no notification and
  nothing logged.
- **Adding a second filter for the same column does not replace the first.** `FilterFor` returns the
  first match, while the engine ANDs every descriptor — so a caller that adds a descriptor instead of
  replacing the existing one narrows the result, while `FilterFor` still reports the old range.
- **`PageIndex` is not clamped on assignment.** After a filter shrinks the dataset, the state can hold
  an index that no longer exists; `Execute` clamps it for the result but does not write the clamped
  value back, so the state and the result disagree until something assigns the clamped index.
- **`HasAnyFilter` ignores `Sorts`** — a grid sorted but not filtered answers false — and it counts a
  filter that `IsEmpty` would drop, so the two tests answer different questions.
- The page index is zero-based while `PolarisGridQueryResult` reports one-based record numbers; a pager
  that prints `PageIndex` directly is off by one.

## See also

- [PolarisGridQueryEngine.cs.md](PolarisGridQueryEngine.cs.md) — interprets this state and clamps its
  page index.
- [PolarisGridFilterDescriptor.cs.md](PolarisGridFilterDescriptor.cs.md) — what goes in `Filters`.
- [PolarisGridSortDescriptor.cs.md](PolarisGridSortDescriptor.cs.md) — what goes in `Sorts`.
- [PolarisGridStateOfT.cs.md](PolarisGridStateOfT.cs.md) — the owner that mutates this and notifies.
