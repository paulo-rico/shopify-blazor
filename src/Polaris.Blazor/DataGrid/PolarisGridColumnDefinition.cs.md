<!-- summary-of: PolarisGridColumnDefinition.cs -->
<!-- source-sha256: 15713858a047 -->

# PolarisGridColumnDefinition.cs

Everything the grid knows about one column: the compiled accessor that reads it, how it renders, which
filter and editor it offers, and how a value is written back. It is the typed implementation of
`IPolarisGridColumn`, built by the column component and held by the grid state.

`Polaris.Blazor.DataGrid` · `public sealed class PolarisGridColumnDefinition<TItem>` · implements
`IPolarisGridColumn`

## Members

| Member | Signature | Behaviour |
|---|---|---|
| `Id` | `required string Id { get; init; }` | Stable identifier used by filters, sorts and chips. The column component always sets it — from the member name when the caller gives no `Id` — so a rebuilt column keeps its identity. |
| `Title` | `required string Title { get; set; }` | The heading and chip label. The column component passes the id through when no title is given. A **settable** property, unlike the rest — a page can rename a column after construction. |
| `FilterIndex` | `int FilterIndex { get; set; } = 999` | Where the column's filter control appears in the filter bar. Lower values first; the default 999 sorts after every column that states an index, and equal values keep their declaration order. The filter panel reads it on each render. |
| `ValueGetter` | `required Func<TItem, object?> ValueGetter { get; init; }` | Reads the value from one item. Compiled once from the column's expression, so rendering a page of rows never reflects over a property. |
| `ValueType` | `Type ValueType { get; init; } = typeof(object)` | The value's type, used to coerce filter values before comparison; editor input is converted to it before `ValueSetter` sees it. |
| `Sortable` | `bool Sortable { get; init; } = true` | Whether the header sorts when activated. |
| `Searchable` | `bool Searchable { get; init; } = true` | Whether the global search box looks inside this column. |
| `FilterType` | `PolarisGridFilterType FilterType { get; init; }` | The filter control offered. `None` (the default) means not filterable. |
| `FilterOptions` | `IReadOnlyList<object?>? FilterOptions { get; init; }` | Explicit multi-select choices; null derives them from the dataset. |
| `FilterTemplate` | `RenderFragment? FilterTemplate { get; set; }` | Custom filter markup, needed only for `FilterType.Custom`. |
| `CellTemplate` | `RenderFragment<TItem>? CellTemplate { get; set; }` | Custom cell markup; null renders the formatted value as text. |
| `Format` | `string? Format { get; init; }` | A standard or custom format string, e.g. `N2`, `P0`, `yyyy-MM-dd`. |
| `FormatProvider` | `IFormatProvider? FormatProvider { get; init; }` | Display culture; null means invariant, which keeps a value identical in a test, an export and a browser. |
| `Alignment` | `PolarisTableHeaderFormat Alignment { get; init; } = PolarisTableHeaderFormat.Base` | Alignment of the header and the cells beneath it. |
| `ListSlot` | `PolarisTableListSlot ListSlot { get; init; } = PolarisTableListSlot.Labeled` | What the column becomes when the table collapses to a list on a narrow screen. |
| `Visible` | `bool Visible { get; set; } = true` | Whether the column is rendered. A hidden column **keeps** its filter and its sort. |
| `Editable` | `bool Editable { get; set; }` | Whether the merchant can edit cells here; the other half is a writable member (`ValueSetter` or a settable property). |
| `EditorType` | `PolarisGridEditorType EditorType { get; set; }` | Which editor the cell draws. The enum's zero value is `Auto`, so leaving it alone means "choose from the value type". |
| `EditorOptions` | `IReadOnlyList<object?>? EditorOptions { get; set; }` | Choices for a `Select` editor; null takes them from the value's enum, else the dataset's distinct values. |
| `ValueSetter` | `Func<TItem, object?, TItem>? ValueSetter { get; set; }` | Writes a value and returns the record to store; the property is null for a column that cannot be written. Needed for computed columns and for copying types (a record updated with `with`); the value arrives already converted to `ValueType`. |
| `ApplyValue` | `TItem ApplyValue(TItem item, object? value)` | Applies one change: calls `ValueSetter`, else the compiled setter, and returns the record to store. Throws `InvalidOperationException` naming the column when it has neither. |
| `CompiledSetter` | `internal Action<object?, object?>? CompiledSetter { get; init; }` | The writer derived from the column expression; null for a computed value or a get-only property. |
| `CanWrite` | `internal bool CanWrite` | True when either a `ValueSetter` or a compiled setter exists. |
| `ReadValue` | `object? IPolarisGridColumn.ReadValue(object item)` | Explicit implementation: casts the boxed item to `TItem` and calls `ValueGetter`. |
| `FormatValue` | `public string FormatValue(object? value)` | Delegates to `PolarisGridFormatting.Format(value, Format, FormatProvider)`; null renders as an empty string. |

## Behaviour

- **The accessor is compiled, and that is the point.** The remark in the source is explicit: rendering a
  page of rows must not reflect over a property, so the delegate is built once from the expression the
  column declared. Everything downstream — the cell, the sort, the search, the filter chip — goes through
  this one delegate or the query engine's own compiled comparisons.
- **Filterability is `FilterType`, never a second flag.** A column with no filter type offers no filter,
  which removes the possibility of the two disagreeing.
- **Formatting is invariant unless a provider is supplied.** `PolarisGridFormatting` asks a provider for
  an `ICustomFormatter` *first* and otherwise formats with the invariant culture. The order matters for
  `bool` and every other `IFormattable`: their own `ToString(format, provider)` never consults a custom
  formatter, so without that first branch a caller's formatter is silently ignored — which is how a chip
  once read "is True" beside a filter offering Yes and No.
- **`ApplyValue` returns a record, it does not promise a copy.** The compiled setter mutates the instance
  it is given; only a `ValueSetter` that returns a new record (a `with` expression) leaves the original
  untouched.

## Gotchas

- **`Editable` on the definition is not a check.** The column object accepts it happily; the refusal
  happens when the column *component* mounts, where a column that declares editability without a writable
  member is rejected rather than drawn as a control that silently loses typing. Building the definition by
  hand bypasses that guard, and the failure moves to `ApplyValue` at save time.
- **A compiled setter mutates a record in place, including a positional record's `init` accessor.** The
  compiled delegate writes through the member on the instance it is handed, so `PolarisGridRowEdit.Original`
  is only pristine for a column that supplies a `ValueSetter` returning a copy. Two tests sharing one
  static fixture saw each other's edits because of this.
- **`ValueSetter` is trusted completely.** Its return value is stored as the record — there is no null
  check and no verification that it is the same instance — so a setter that returns null makes the row
  null.
- **`ValueType` defaults to `object`.** Filter coercion and editor conversion then have nothing to aim at,
  so a column declared from a computed expression should state its type rather than leave the default.
- **Hiding a column is not the same as removing it.** `Visible = false` stops it being drawn while its
  filter and sort survive, which is deliberate — a saved view that hid a column would otherwise lose the
  filter that view is defined by.
- **`Id` collisions are silent.** The interface documents uniqueness within one grid; nothing enforces it,
  and two columns with one id make a filter, a sort or a chip act on the wrong column.

## See also

- [IPolarisGridColumn.cs.md](IPolarisGridColumn.cs.md) — the non-generic contract this implements.
- [PolarisGridColumn.cs.md](../Components/PolarisGridColumn.cs.md) — the component that builds one of
  these from an expression.
- [PolarisGridExpression.cs.md](PolarisGridExpression.cs.md) — how the getter, type and setter are derived.
- [PolarisGridRowEdit.cs.md](PolarisGridRowEdit.cs.md) — what a save receives, and why `Original` may
  already be mutated.
