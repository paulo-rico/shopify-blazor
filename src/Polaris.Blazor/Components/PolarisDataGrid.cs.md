<!-- summary-of: PolarisDataGrid.cs -->
<!-- source-sha256: 96809e5744dd -->

# PolarisDataGrid.cs

Renders one page of a local collection as a Polaris table with sorting, row selection, spreadsheet-style
cell editing, and empty, loading and error states. It owns no state: the page creates one
`PolarisGridState<TItem>` and hands the same instance to this grid, to `PolarisDataFilter` and to
`PolarisDataPager`, which is what lets the three be laid out separately while describing one grid.

`Polaris.Blazor.Components` · composed component, not one `<s-*>` element: an `s-section` around an
`s-table`, with `s-button` sortable headings and `s-checkbox` selection cells · base
`PolarisGridComponentBase`, implements `IPolarisGridColumnHost`

## Parameters

| Parameter | Type | Default | What it does |
|---|---|---|---|
| `State` | `PolarisGridState<TItem>` | required (`EditorRequired`, `default!`) | The shared state. Columns, result page, sort, filters, selection and `IsLoading` / `ErrorMessage` all live there; the grid only reads it and calls its mutators. Null renders nothing but the column declarations. |
| `Columns` | `RenderFragment?` | `null` | The `PolarisGridColumn<TItem>` declarations. Rendered before anything that depends on them and **never removed**, so the filter can still draw controls while the table is loading or empty. |
| `AllowSelection` | `bool` | `false` | Adds a leading checkbox cell to every row and a select-all checkbox to the header, which ticks the visible page only. |
| `Editable` | `bool` | `false` | Turns on cell editing — half the answer, since the column must be editable too. Off means the read-only markup is rendered unchanged: no wrapper `div`, no `tabindex`, no listeners, no save bar. |
| `SaveMode` | `PolarisGridSaveMode` | `Immediate` | When a committed cell reaches `SaveChanges`: after every cell, or only on the Save button / `SaveAsync`. It decides *when*, never *whether* — an editable grid always edits the collection it was given. |
| `SaveChanges` | `Func<PolarisGridRowEdit<TItem>, CancellationToken, Task<PolarisGridSaveResult>>?` | `null` | Writes one edited row, once per row and not once per cell, so a save can be one transaction. Null keeps edits in the in-memory collection. `Failure` — or a thrown exception, which becomes the same failure — keeps the draft on screen, leaves the collection untouched and shows the message in a critical banner. |
| `SelectionChanged` | `EventCallback<IReadOnlyCollection<object>>` | — | The selected row keys after a selection change (see [Events](#events)). |
| `EmptyHeading` | `string?` | `null` | Heading of the built-in empty state; falls back to `"No records to show"`. |
| `EmptySubheading` | `string?` | `null` | Supporting text under that heading. `null` passes no subheading fragment at all rather than an empty one. |
| `EmptyContent` | `RenderFragment?` | `null` | Replaces the whole empty state. The action slots accept exactly one button each, so a "clear the filters" action goes here and not through a parameter. |
| `LoadingContent` | `RenderFragment?` | `null` | Replaces the inline `s-stack` spinner-plus-text shown while `State.IsLoading`. |

Inherited: none. `PolarisGridComponentBase` adds no parameters; it only subscribes to the state's
`Changed` event and redraws — see
[PolarisGridComponentBase.cs.md](../DataGrid/PolarisGridComponentBase.cs.md).

## Events

| Callback | Args | Raised when |
|---|---|---|
| `SelectionChanged` | `IReadOnlyCollection<object>` — the selected row keys | A row checkbox is ticked, or the header checkbox selects/deselects the page. A selection changed elsewhere (a chip, a keyboard handler) redraws the grid through the state's own event but does **not** raise this callback. |

## Members (called from the page)

| Member | Signature | What it does |
|---|---|---|
| `HasUnsavedChanges` | `bool { get; }` | True when at least one row has a draft. |
| `UnsavedRowCount` | `int { get; }` | How many rows have at least one unsaved edit. |
| `IsSaving` | `bool { get; }` | True while `SaveAsync` is running; drives the Save button's spinner. |
| `SaveAsync` | `Task<PolarisGridSaveResult> SaveAsync(CancellationToken = default)` | Saves every edited row. Success when all were written, otherwise a failure carrying the joined messages. Written rows are applied to the collection in **one** `State.UpdateItems` call and lose their drafts; failed rows keep theirs. A no-op success when there are no drafts or `State` is null. |
| `DiscardAsync` | `Task DiscardAsync()` | Drops every draft, the save error, the editor error and the active cell, restoring the cells to what the records hold. |

## Behaviour

- **Rendering branches, in order.** Inside the `s-section`: `State.IsLoading` → loading content;
  otherwise a non-empty `State.ErrorMessage` → a critical `s-banner`; otherwise `State.Result.IsEmpty` →
  the empty state; otherwise the `PolarisTable`. The editing footer sits **outside** those branches, so an
  unsaved edit made on one page is still reachable after the result goes empty.
- **Columns are cascaded an interface, not this grid.** The `Columns` fragment is rendered inside a
  `CascadingValue<IPolarisGridColumnHost>` holding this component, so a `PolarisGridColumn` registers
  with the state through the same contract a `PolarisDataFilter` offers a screen with no table. The
  value is fixed, so re-cascading does not re-render every column on every pass.
- **Sorting.** A sortable column's heading is an `s-button` (`Tertiary` variant) carrying a
  `chevron-up`/`chevron-down` icon and an accessibility label only while sorted
  (`"<Title>, sorted ascending"` / `"…descending"`); an unsorted heading is plain text, and clicking a
  sortable one calls `State.ToggleSort`.
- **Selection.** The header checkbox is `Checked` when every row of the page is selected and
  `Indeterminate` when only some are. Both header and row checkboxes use `LabelAccessibilityVisibility =
  Exclusive`, so "Select every row on this page" / "Select this row" are announced but not painted. Rows
  are keyed by `State.KeyOf`, so a tick follows its record when the order changes.
- **Editing requires three things:** `Editable` on the grid, `Editable` on the column, and a column that
  can write (`CanWrite`, i.e. a settable expression or a `ValueSetter`).
- **Keyboard model.** A printable character on an idle cell opens the editor *seeded with that
  character*; `Enter` or `F2` opens it with the current value. In the editor only `Enter`, `Tab`,
  `Shift+Tab` and `Escape` are intercepted — Enter moves one row down (offset = the number of editable
  columns), Tab moves one cell, and at either edge the editor **closes** instead of wrapping, which is
  also how a keyboard user leaves the table. `Escape` clears the cell's draft entirely.
- **Editor choice.** `PolarisGridEditorType.Auto` reads the column's value type: `DateOnly`/`DateTime` →
  date field, enum → select, `bool` → checkbox, any numeric type → number field, everything else → text
  field. A select's options are the column's `EditorOptions`, else the enum's values, else the dataset's
  distinct values for that column.
- **The editor is seeded with the value as it would be typed back, not as displayed** — invariant and
  unformatted, `yyyy-MM-dd` for dates, `HH:mm` for times — so a `C2` price does not arrive with a
  currency symbol and thousands separators for the merchant to delete.
- **Conversion on commit.** Empty text becomes `string.Empty` for a string column, an error
  (`"<Title> needs a value."`) for a non-nullable value type, and `null` otherwise. Text that will not
  parse gives `"'x' is not a whole number"` (or "a number" / "a date" / "text" / "one of: …") and the
  editor stays open showing it.
- **A ticked checkbox commits on the tick**, immediately and regardless of Enter — a tick that did
  nothing until a key was pressed would look broken.
- **Immediate saving re-queries.** The list of editable cells for Enter/Tab is rebuilt *after* the commit,
  so the move follows the rows the merchant can still see.
- **Drafts apply row by row.** `ApplyChanges` starts from `edit.Original` and runs each changed column
  through its setter, so a column without a copy-returning `ValueSetter` mutates the record the grid was
  given.
- **A row that leaves the page loses its editor.** When a filter, sort or page change drops the active
  cell's row from the result, the active cell, editor text and editor error are cleared — otherwise an
  editor is drawn nowhere with a draft attached.

## Gotchas

- **The editable grid wraps its section in a `div` it owns, and that is load-bearing.** The element
  reference capture lives on that `data-polaris-grid-root` wrapper, not on a cell, because a cell comes
  and goes every time an editor is diffed back to text and a frame the diff must remove throws
  `Unexpected frame type during RemoveOldFrame`. The capture is emitted *after* the section, which is
  also where the Razor compiler puts one.
- **Sequence numbers must not go backwards inside a scope.** A cell's content is numbered at 10, above
  every attribute numbered 0–6; numbering content below the attributes made the renderer's diff fail
  outright rather than draw the cell. The same rule is why the header/body/cell sequences are literals
  per sibling rather than per iteration.
- **`mousedown` opens the editor, not `click`.** Committing the cell the merchant was in re-renders the
  row, and a click whose mousedown and mouseup land on different elements never fires — clicking straight
  from one edited cell into another silently did nothing. The handler is on the cell's own
  `[data-polaris-grid-cell]` div, which is also the element carrying `tabindex="0"`; dispatching at the
  enclosing `s-table-cell` hits nothing.
- **`OnEditorInput` deliberately does not call `StateHasChanged`.** It runs per keystroke, and a render
  would write the value back into the field and move the caret to the end of the text.
- **The focus flag is cleared before the interop await**, so a render caused by anything else while the
  focus call is in flight cannot focus a second time and yank the caret back into the editor.
- **`SaveAsync` sets `_saveError` and returns it.** The button that started the save is re-rendered away
  by the same state change that reports the result, so a page that only reads the return value shows
  nothing; the banner is the message of record.
- **Only `SaveMode.Manual` has a Save button.** The save bar renders only when `SaveMode` is `Manual`
  *and* drafts exist — in `Immediate` mode a refusal appears only as the banner, and `SaveAsync` must be
  reached from the page.
- **A draft whose record disappeared is dropped silently.** If the row was replaced out from under the
  draft (a reload dropped it) there is nothing to write and nothing to show, so the draft is cleared
  rather than left describing no record.
- **The empty state cannot tell "no records" from "nothing matched".** Both land in the same branch;
  `EmptyHeading` / `EmptySubheading` / `EmptyContent` are the caller's only chance to say which.
- **Loading hides a previous error, and an error hides the rows.** `IsLoading` wins over
  `ErrorMessage`, which wins over `IsEmpty`, so a failed reload that left stale items on screen shows the
  banner and no table.
- **`SaveChanges` throwing is handled, not fatal.** The exception's message becomes that row's failure
  and the circuit survives; rows that succeeded before it are still applied.

## See also

- [PolarisGridColumn.cs.md](PolarisGridColumn.cs.md) — the column declaration: field, format, sortability, editor type.
- [PolarisDataFilter.cs.md](PolarisDataFilter.cs.md), [PolarisDataPager.cs.md](PolarisDataPager.cs.md) — the two siblings that share the same state.
- [PolarisGridState.cs.md](../DataGrid/PolarisGridState.cs.md) — the page-owned object everything above reads.
- [PolarisGridComponentBase.cs.md](../DataGrid/PolarisGridComponentBase.cs.md) — the subscription that redraws the grid.
- [IPolarisGridColumnHost.cs.md](../DataGrid/IPolarisGridColumnHost.cs.md) — the cascade the column declarations register through.
