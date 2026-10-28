<!-- summary-of: PolarisGridEnums.cs -->
<!-- source-sha256: 62368e82d2fd -->

# PolarisGridEnums.cs

The six enums that spell out the data grid's entire vocabulary. Between them they say what kind of
filter a column offers, how a filter compares, which way a sort runs, which editor a cell draws, when a
committed edit is saved and when a filter change reaches the query — none of which can be inferred from
a column's CLR type.

`Polaris.Blazor.DataGrid` · six public enums, no members of their own · not a component

## Members

### `PolarisGridFilterType` — the control a column's filter offers

| Value | What it selects |
|---|---|
| `None` | Not filterable. The enum's zero, so an unset column is not filterable. |
| `Text` | A case-insensitive contains search box. |
| `MultiSelect` | A list of choices; the selected ones are **OR-ed** together, while separate column filters are AND-ed. |
| `Boolean` | A three-state choice — all, true, false — so "no preference" is distinct from "false". |
| `Range` | A dual-handle numeric range; both ends inclusive. |
| `DateRange` | An inclusive interval between two dates. |
| `Custom` | Caller-supplied markup, taken from the column's `FilterTemplate`. |

### `PolarisGridOperator` — how a descriptor compares a column's value

| Value | What it compares |
|---|---|
| `Equals` | Equal. Text compares case-insensitively. The enum's zero, so it is the default operator. |
| `NotEquals` | Not equal. |
| `Contains` | The value contains the term as a substring, case-insensitively. |
| `StartsWith` / `EndsWith` | Prefix or suffix match, case-insensitively. |
| `GreaterThan` / `GreaterThanOrEqual` | Ordered after the value. |
| `LessThan` / `LessThanOrEqual` | Ordered before the value. |
| `Between` | Inside the inclusive interval from the descriptor's `Value` to its `SecondValue`. |
| `In` | One of the descriptor's `Values`. |
| `IsNull` / `IsNotNull` | The column has no value, or has one. Neither needs a value. |

### `PolarisGridSortDirection` — which way a column's values are ordered

| Value | What it does |
|---|---|
| `Ascending` | Smallest first, with empty values **last**. The zero value. |
| `Descending` | Largest first, with empty values **first**. |

### `PolarisGridEditorType` — the editor a cell draws while being edited

| Value | What it draws |
|---|---|
| `Auto` | Chosen from the column's value type. The default. |
| `Text` | A single-line text field. |
| `Number` | A numeric field; the text is converted back to the column's own type on commit. |
| `Date` | A date field; the value is written as an ISO date, which is what the element reads. |
| `Boolean` | A checkbox, which **commits the moment it is ticked** rather than on Enter — a tick that did nothing until a key was pressed would be indistinguishable from a broken one. |
| `Select` | Options are the column's `EditorOptions` when declared, otherwise an enum's values, otherwise the dataset's distinct values. |

### `PolarisGridSaveMode` — when a committed edit reaches the caller's store

| Value | What it does |
|---|---|
| `Immediate` | After every committed cell, so store and grid agree at all times. A row the change pushes out of the current filter or sort leaves the page as soon as it is saved. The zero value. |
| `Manual` | When the merchant presses the grid's Save button, or the page calls its `SaveAsync`. Edits stay drafts until then, so the table keeps the row where it is while it is being finished. |

### `PolarisGridFilterApplyMode` — when a filter change reaches the query

| Value | What it does |
|---|---|
| `Immediate` | On every change. The zero value. |
| `Debounced` | After typing stops, for a configurable interval. The default for text filters. |
| `Manual` | Only when the merchant presses Apply — or makes a choice, which commits itself. |

### `PolarisDataFilterPresentation` — where a filter draws its controls

| Value | What it does |
|---|---|
| `Bar` | A search box, a Filter button, a chip row, and the controls in a popover. The zero value, and the shape every screen had before this choice existed. |
| `Panel` | The same controls drawn in place, with the applied chips and Clear all above them. No search box, no button and no popover — for a screen that has given the filter a column of its own, such as a sidebar beside a grid of cards. |

The two presentations share one implementation of the controls, the chips, the apply modes and the
state; only what surrounds them differs.

## Behaviour

- **`FilterType` and `EditorType` are declared, never inferred.** A string column may be free text, a
  set of five categories, or a plain enum rendered as text, and only the caller knows which.
  `PolarisGridEditorType.Auto` reads the value type, which is right for most columns and wrong for
  exactly those. Both are declared on `PolarisGridColumnDefinition<TItem>`, beside `EditorOptions` and
  `FilterTemplate`.
- **`SaveMode` decides *when*, never *whether*.** An editable grid always edits the records it was
  given. Without a `SaveChanges` callback a committed edit reaches `PolarisGridState<TItem>` and stops
  there, which is what a screen over local data wants. Neither mode is a database dependency — the
  grid asks the caller to persist a row and does not care what the caller does with it.
- **`Range` and `DateRange` are inclusive at both ends**, matching `Between`, and matching what the
  range slider already reports.
- **`Select`'s option source is a three-step fallback**: declared `EditorOptions` first, then an
  enum's values, then the dataset's distinct values. A select over a string column with no declared
  options therefore offers whatever happens to be in the data — and nothing before the data arrives.

## Gotchas

- **`default(PolarisGridSaveMode)` is `Immediate`**, so a save mode left unset writes after every
  cell rather than on Save. The safe default is to state the mode explicitly on any grid whose store
  is a real service.
- **`default(PolarisGridFilterType)` is `None`**, which means a column with no explicit filter type
  gets no filter control at all — silently, because there is nothing to render.
- **`PolarisGridOperator.Equals` must be written qualified.** A bare `Equals` in the same scope is
  `object.Equals`, and the member name is also easily confused with the descriptor's static `Equals`
  factory and with a record's generated equality.
- `Boolean` is a *three-state* filter, not a nullable checkbox: "all" is its own state, so a page
  binding a filter to a `bool` cannot express "no preference" and will always narrow the result.
- `IsNull` and `IsNotNull` ignore the descriptor's `IncludeNulls` flag, because they are already
  entirely about nulls.
- `MultiSelect` ORs within one column; two filters on the same column are AND-ed by the engine and
  will usually match nothing. One descriptor per column is the intended shape.

## See also

- [PolarisGridFilterDescriptor.cs.md](PolarisGridFilterDescriptor.cs.md) — the filter value these
  enums describe, and its `IsEmpty` rule.
- [PolarisGridQueryEngine.cs.md](PolarisGridQueryEngine.cs.md) — applies the operator and sort
  semantics.
- [PolarisGridSortDescriptor.cs.md](PolarisGridSortDescriptor.cs.md) — `Field`, `Direction` and
  `Priority` together.
- [PolarisGridState.cs.md](PolarisGridState.cs.md) — the columns and filters these values configure.

