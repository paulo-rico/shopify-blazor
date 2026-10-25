<!-- summary-of: PolarisGridColumn.cs -->
<!-- source-sha256: 57c68227949d -->

# PolarisGridColumn.cs

Declares one column of a `PolarisDataGrid<TItem>` — how to read it, how to render it, what the merchant
can filter it by and whether its cells are editable. It is a declaration, not a region of the page: it
renders nothing at all.

`Polaris.Blazor.Components` · a declaration that draws nothing · base `ComponentBase`

## Parameters

| Parameter | Type | Default | What it does |
|---|---|---|---|
| `Field` | `Expression<Func<TItem, object?>>` | required (`EditorRequired`) | The property this column shows, `x => x.Name`. Typed `object?` so the column needs one type parameter rather than two; the value's real type and the member's name are both read from the expression. |
| `Title` | `string?` | `null` | The heading. `null` falls back to the property name; an **empty string** is a deliberate column with no heading at all (a thumbnail column), and is not treated as absent. |
| `Id` | `string?` | `null` | The column's stable identifier, which is what a filter or a saved sort order refers to. `null` or whitespace falls back to the member name from `Field`. |
| `Sortable` | `bool` | `true` | Whether activating the header sorts by this column. |
| `Searchable` | `bool` | `true` | Whether the global search box looks inside this column. |
| `FilterType` | `PolarisGridFilterType` | `None` | Which filter control the column offers: `None`, `Text`, `MultiSelect`, `Boolean`, `Range`, `DateRange` or `Custom`. |
| `FilterOptions` | `IReadOnlyList<object?>?` | `null` | Explicit choices for a multi-select filter. `null` derives them from the dataset, which is what a categorical column usually wants. |
| `FilterTemplate` | `RenderFragment?` | `null` | Custom filter markup, rendered inside the filter panel; the column's own control is then not drawn. Required when `FilterType` is `Custom`. |
| `CellTemplate` | `RenderFragment<TItem>?` | `null` | Custom cell markup. `null` renders the formatted value as text. |
| `Format` | `string?` | `null` | A standard or custom format string, such as `N2` or `P0`. |
| `FormatProvider` | `IFormatProvider?` | `null` | How values are formatted. `null` means the invariant culture, so a value renders identically in a test, an export and a browser. |
| `Alignment` | `PolarisTableHeaderFormat` | `Base` | How the header aligns the column — and the cells beneath it: `Base` (left), `Numeric` or `Currency` (right, for counts and money). |
| `ListSlot` | `PolarisTableListSlot` | `Labeled` | What the column becomes when the table collapses to a list on a narrow screen — `Labeled`, `Primary`, `Secondary` and so on. |
| `Visible` | `bool` | `true` | Whether the column is drawn. A hidden column keeps its filter and its sort. |
| `Editable` | `bool` | `false` | Whether the merchant can edit this column's cells, when the grid is editable too. Needs a writable member or `ValueSetter`, and is refused on mount otherwise. |
| `EditorType` | `PolarisGridEditorType` | `Auto` | Which editor a cell draws: `Auto` (chosen from the value's type), `Text`, `Number`, `Date`, `Boolean` or `Select`. |
| `EditorOptions` | `IReadOnlyList<object?>?` | `null` | Choices for a `Select` editor. `null` takes them from an enum's values, else from the values the dataset contains. |
| `ValueSetter` | `Func<TItem, object?, TItem>?` | `null` | Writes a value onto a record and returns the record to keep. The value has already been converted to the property's type when it arrives. |

## Members

| Member | Signature | What it does |
|---|---|---|
| `Definition` | `PolarisGridColumnDefinition<TItem>?` (get) | The definition this column registered — the grid draws from it, and a test can assert on it. `null` until `OnInitialized` has run. |
| `Dispose` | `void Dispose()` | Unregisters the column from the grid's state by id. Idempotent: a second call does nothing, and a column that never registered unregisters nothing. |

## Behaviour

- **Nothing is rendered here.** What appears on screen is drawn by the grid from the
  `PolarisGridColumnDefinition<TItem>` this component builds once and registers with the grid's state.
  That is why a column can be filtered by a `PolarisDataFilter` that never sees this component, and why
  hiding a column does not lose its filter.
- **The expression is read once, for two facts at a time:** the property's type, which filter values
  are coerced against, and the property's name, which is the column's default id. Both become facts
  rather than something the caller can mistype by hand.
- **`FilterType` and `Visible` are independent on purpose.** A column with a filter type and
  `Visible="false"` is the way to filter on something the table has no room for — a facet behind a
  badge, a flag the cell already conveys in words — and it still gets a control and a chip like any
  other column.
- **Most parameters are read once, at mount.** `Title`, `Visible`, both templates, `Editable`,
  `EditorType`, `EditorOptions` and `ValueSetter` are adopted in place on later parameter passes, and
  the grid is notified only when one actually changed. `Field`, `Id`, `Sortable`, `Searchable`,
  `FilterType`, `FilterOptions`, `Format`, `FormatProvider`, `Alignment` and `ListSlot` are captured
  when the column mounts and are never re-read.
- Editability is read from the definition on every cell render, so a page that turns a column editable,
  or a column picker that swaps an editor, is honoured without re-registering the column.

## Gotchas

- **Declared outside a grid it throws** `InvalidOperationException` from `OnInitialized` — a column
  nobody reads would silently lose its heading and its filter, with nothing to say why.
- **A computed column needs an `Id`.** `x => x.Price * x.Quantity` names no property, so there is no
  natural id and the mount throws `ArgumentException`; set `Id` explicitly.
- **`FilterType="Custom"` without a `FilterTemplate` throws** `ArgumentException` — otherwise the
  column would offer a filter control that is not there.
- **`Editable` without a writable member and without `ValueSetter` throws** `ArgumentException`. The
  alternative is worse: a cell that accepts typing and discards it without a word.
- **Template comparison is reference equality.** An inline `CellTemplate="@(row => …)"` is a new
  delegate on every render of the declaring page, so it is treated as a change and the grid is notified
  every time. Hoist a template into a field when that notification must not repeat.
- **`Visible="false"` does not remove the filter.** The declared filter still reaches the filter bar and
  its chip is still shown, because a filter narrowing the result with nothing on screen to explain it
  reads as missing data. To take a column out of the filters as well, declare `FilterType="None"`.
- **A template should be a function of the row it is given.** The declaration is read when the column
  mounts, so a template that closes over page state is one render behind; pass the state through the
  context instead.
- Changing `FilterType`, `Format` or `Alignment` after the column has mounted has no effect and raises
  nothing — rebuild the grid, or key it so the column is re-created.

## See also

- [PolarisDataGrid.cs.md](PolarisDataGrid.cs.md) — the grid that draws these declarations.
- [PolarisDataFilter.cs.md](PolarisDataFilter.cs.md) — the filter panel that consumes their filter types.
- [PolarisGridColumnDefinition.cs.md](../DataGrid/PolarisGridColumnDefinition.cs.md) — what a registered column actually is.
- [PolarisTable.cs.md](PolarisTable.cs.md) — the plain table, with no declaration layer.
