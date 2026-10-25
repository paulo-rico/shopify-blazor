<!-- summary-of: PolarisGridEditSession.cs -->
<!-- source-sha256: cdd93e49846f -->

# PolarisGridEditSession.cs

The committed cell values that have not been saved yet, keyed by row and column: drafts rather than edits
to the records, so a rejected save leaves nothing behind. The grid keeps one of these while the merchant
works, and empties it row by row as `SaveChanges` accepts what it writes.

`Polaris.Blazor.DataGrid` · `internal sealed class` · owned by the editable grid, not by the caller

## Members

| Member | Signature | Behaviour |
|---|---|---|
| `RowCount` | `public int RowCount { get; }` | How many rows have at least one draft. |
| `IsEmpty` | `public bool IsEmpty { get; }` | True when nothing has been edited. |
| `Keys` | `public IEnumerable<object> Keys { get; }` | The keys of the rows with drafts, in the order they were first edited. |
| `IsRowDirty` | `public bool IsRowDirty(object rowKey)` | True when the row has any draft at all. |
| `ChangesFor` | `public IReadOnlyDictionary<string, object?> ChangesFor(object rowKey)` | The row's draft values by column id. A clean row returns a **new empty dictionary**, not a shared view. |
| `TryGetCell` | `public bool TryGetCell(object rowKey, string columnId, out object? value)` | Reads one cell's draft; the return value says whether a draft exists, because a `null` draft is legitimate. |
| `SetCell` | `public void SetCell(object rowKey, string columnId, object? value, object? current)` | Records a draft, replacing any earlier one — **unless** `value` equals `current`, in which case the draft is dropped instead. |
| `ClearCell` | `public bool ClearCell(object rowKey, string columnId)` | Drops one cell's draft, which is what Escape does. Returns false when there was nothing to drop; the row stays dirty if other cells still are. |
| `ClearRow` | `public bool ClearRow(object rowKey)` | Drops every draft for one row, which is what a saved row does. Returns false for a clean row. |
| `Clear` | `public void Clear()` | Drops every draft. |

## Behaviour

- **A draft is not a change to the record.** Until it is saved, the collection still holds what the
  database holds. That is what makes a rejected save leave nothing behind, and what lets a merchant edit a
  column the grid is sorted or filtered by without the row jumping out from under the cursor mid-row.
- **Keyed by row key, not by position.** A page change, a sort or a filter rebuilds the visible rows, and a
  draft indexed by position would silently attach itself to a different record. Drafts for rows that are
  no longer on the visible page are kept, and are saved with the rest.
- **Values are stored already converted to the column's own type.** The text a merchant typed is parsed
  once, on commit, so a save writes `42` and not `"42"`. A value that cannot be parsed never becomes a
  draft at all — it stays in the editor with a message instead.
- **Clearing is what keeps the dirty marks honest.** `SetCell` compares the new value with the record's
  current one, so typing a cell's own text back into it leaves the row clean and cannot produce a save with
  an empty delta. `ClearCell` then removes the row's entry entirely once its last draft is gone, so
  `RowCount` counts edited rows rather than touched ones.

## Gotchas

- **`ChangesFor` on a clean row returns a throwaway empty dictionary.** Adding to it is a silent no-op —
  nothing throws, nothing is stored. Use `SetCell` to record anything.
- **The equality check is `object.Equals`, across types.** A draft of `42` (an `int`) compared with a
  current `42m` (a `decimal`) is *not* equal, so a column whose `ValueType` disagrees with its stored type
  marks every touched cell dirty even when the merchant typed the same number.
- **A `null` draft is meaningful.** "Cleared the field" is a real edit, which is why `TryGetCell` returns a
  bool and why `ChangesFor` can hold null values — a caller that tests the value instead of the flag
  misreads an intentional clear as "no change".
- **Row keys are dictionary keys with the default comparer.** Two rows that compare equal (or a key whose
  hash code changes after it is stored) collapse into one row's drafts, and the second row's edits are
  written onto the first. Key by something immutable and unique.
- **This is an implementation detail.** The type is `internal`: the grid empties it as saves succeed, and
  a caller that wants the drafts receives them as `PolarisGridRowEdit<TItem>` values in `SaveChanges`.
- **Nothing here is synchronised.** It is a plain dictionary with no locking, driven from the grid's own
  event handling rather than shared between threads.

## See also

- [PolarisGridRowEdit.cs.md](PolarisGridRowEdit.cs.md) — the delta handed to `SaveChanges`, and what
  `Original` means.
- [PolarisDataGrid.cs.md](../Components/PolarisDataGrid.cs.md) — the component that opens editors, commits
  drafts and saves them.
- [PolarisGridStateOfT.cs.md](PolarisGridStateOfT.cs.md) — the state that holds the records the drafts are
  compared against.
