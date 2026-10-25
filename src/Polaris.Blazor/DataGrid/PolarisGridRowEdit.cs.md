<!-- summary-of: PolarisGridRowEdit.cs -->
<!-- source-sha256: 18c1b865316f -->

# PolarisGridRowEdit.cs

One row's pending edits as a save callback receives them, plus the verdict that callback returns: the
delta rather than the record, and a result type that refuses a write without throwing.

`Polaris.Blazor.DataGrid` · two sealed result types, not components · handed to and returned from `PolarisDataGrid<TItem>.SaveChanges`

## Members

**`PolarisGridRowEdit<TItem>`** — the changes for one row, by column id, already typed.

| Member | Signature | What it does |
|---|---|---|
| Constructor | `PolarisGridRowEdit(object key, TItem original, IReadOnlyDictionary<string, object?> changes)` | Throws `ArgumentNullException` when `key` or `changes` is null. `original` is not checked, so it may be null. |
| `Key` | `object Key { get; }` | The row key, from `PolarisGridState<TItem>.KeyOf`, so a caller can recognise a record it sees twice. |
| `Original` | `TItem Original { get; }` | The record the changes apply to, as the query returned it — what identifies the row, which a save callback usually needs more than the values. |
| `Changes` | `IReadOnlyDictionary<string, object?> Changes { get; }` | Only the columns the merchant actually changed, keyed by column id, already converted to the column's own type. |
| `ChangedColumns` | `IEnumerable<string> ChangedColumns { get; }` | `Changes.Keys`, in a stable order — for a caller that wants the list rather than the dictionary. |

**`PolarisGridSaveResult`** — what the save callback reports back.

| Member | Signature | What it does |
|---|---|---|
| `Succeeded` | `bool Succeeded { get; }` | True when the row was written. |
| `Message` | `string? Message { get; }` | What to show the merchant when it was not, or null. |
| `Success` | `static PolarisGridSaveResult Success()` | The row was written; no message. |
| `Failure` | `static PolarisGridSaveResult Failure(string message)` | Not written; the draft stays on screen with this message beside the grid. A null, empty or whitespace message is replaced with "The row could not be saved." |

There is no public constructor: a result is only ever `Success()` or `Failure(...)`.

## Behaviour

- **It is a delta, not a record.** The grid has already converted and validated the merchant's typing,
  so a caller writes an `UPDATE … SET name = @p0` without diffing anything and without parsing text.
- **A rejected edit is a normal outcome, not an exception.** `Failure` covers a duplicate SKU, a price
  below cost, a row someone else has deleted — the answer is a message under the merchant's cursor.
- **Throwing out of the callback is still supported** and is turned into the same message, so a caller
  that never read this type still fails safely.
- The types are plain sealed classes: callable, constructible and assertable from a unit test with no
  grid and no browser.

## Gotchas

- **`Original` is not a copy.** A mutable model is deliberately left alone until the save succeeds, so
  an edit that is rejected leaves the collection exactly as it was.
- **A record edited through a compiled property setter is updated in place** — including a positional
  record's `init` accessor, which reflection calls happily. `Original` is therefore only pristine for a
  column that declares a `ValueSetter` returning a new record (`product with { … }`).
- A null `Key` or null `Changes` fails at construction, not at save time, so a test that passes null
  gets an `ArgumentNullException` rather than a refused save.
- `Failure` cannot be made silent: an empty message still renders the generic sentence.

## See also

- [PolarisGridColumnDefinition.cs.md](PolarisGridColumnDefinition.cs.md) — `ValueSetter`, the column
  option that keeps `Original` pristine.
- [PolarisGridStateOfT.cs.md](PolarisGridStateOfT.cs.md) — `UpdateItems`, the method a successful save
  feeds.
- [PolarisGridEditSession.cs.md](PolarisGridEditSession.cs.md) — where drafts live before a save.
- [PolarisDataGrid.cs.md](../Components/PolarisDataGrid.cs.md) — the component that raises the callback.
