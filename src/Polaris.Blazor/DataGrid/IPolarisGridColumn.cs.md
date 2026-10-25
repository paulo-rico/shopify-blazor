<!-- summary-of: IPolarisGridColumn.cs -->
<!-- source-sha256: b7a6336a4dab -->

# IPolarisGridColumn.cs

A non-generic view of one grid column — id, heading, filter control, editor and formatter — so the filter
panel and pager can work without knowing the row type. `PolarisGridColumnDefinition<TItem>` is the typed
form the query engine uses; this interface is what makes `PolarisDataFilter` and `PolarisDataPager` plain
components instead of generic ones.

`Polaris.Blazor.DataGrid` · `public interface` · implemented by
`PolarisGridColumnDefinition<TItem>`

## Members

| Member | Signature | What it says |
|---|---|---|
| `Id` | `string Id { get; }` | The stable identifier the query state refers to. Unique within one grid; it is the key a filter, a sort and a chip all use. |
| `Title` | `string Title { get; }` | The heading in the table and the label in a filter chip. |
| `ValueType` | `Type ValueType { get; }` | The type of the column's values, used to coerce filter values before comparison. |
| `FilterType` | `PolarisGridFilterType FilterType { get; }` | Which filter control the column offers. `None` means it is not filterable at all. |
| `Sortable` | `bool Sortable { get; }` | Whether the header can be activated to sort. |
| `Searchable` | `bool Searchable { get; }` | Whether the global search box looks inside this column. |
| `Visible` | `bool Visible { get; }` | Whether the column is rendered at all. A hidden column keeps its filter and its sort. |
| `Editable` | `bool Editable { get; }` | Whether the merchant can edit cells in this column — only half the answer, since the grid must be editable too and the column must have somewhere to write. |
| `EditorType` | `PolarisGridEditorType EditorType { get; }` | Which editor a cell draws, or `Auto` to choose one from the value's type. |
| `EditorOptions` | `IReadOnlyList<object?>? EditorOptions { get; }` | Explicit choices for a `Select` editor; null means the value's enum, else the dataset's distinct values. |
| `FilterOptions` | `IReadOnlyList<object?>? FilterOptions { get; }` | Explicit choices for a `MultiSelect` filter; null derives them from the dataset. |
| `Format` | `string? Format { get; }` | A standard or custom format string such as `N2`, or null for a plain rendering. |
| `FilterTemplate` | `RenderFragment? FilterTemplate { get; }` | The column's own filter markup, for a column that declared `Custom`. Part of the metadata because the filter bar builds one control per column from metadata alone. |
| `FormatProvider` | `IFormatProvider? FormatProvider { get; }` | How values are formatted for display. Null means the invariant culture. |
| `Alignment` | `PolarisTableHeaderFormat Alignment { get; }` | How the header aligns the column and the cells beneath it. |
| `ListSlot` | `PolarisTableListSlot ListSlot { get; }` | What the column becomes when the table collapses to a list on a narrow screen. |
| `ReadValue` | `object? ReadValue(object item)` | Reads this column's value from a boxed item. The item must be the grid's row type — the implementation casts it, so a mismatch throws `InvalidCastException`. Used only by metadata helpers. |
| `FormatValue` | `string FormatValue(object? value)` | Renders a value exactly the way the cell does, so a chip and a cell agree on what a value looks like. Null becomes an empty string. |

## Behaviour

- **Filterability is `FilterType`, with no separate flag.** A column that declares no filter type simply
  has none, so "is it filtered?" and "is there a control for it?" cannot disagree. The filter kind is
  declared rather than inferred from `ValueType`, because a string column may be free text or one of five
  categories and only the caller knows which.
- **This interface deliberately never touches an item.** The only two members that could — `ReadValue`
  and `FormatValue` — take and return `object`, which is what keeps the filter and the pager free of a
  type parameter and able to sit anywhere on the page.
- **`FilterTemplate` is metadata, not a callback.** A `Custom` column draws its own control, and the
  filter bar recognises it here rather than casting the column back to its typed definition.

## Gotchas

- **`Id` must be unique per grid and stable across rebuilds.** Two columns sharing an id make a filter,
  a sort and a chip address the wrong column, with no error anywhere; deriving it from the member name is
  what makes a saved sort or a stored view keep pointing at the same column after the page rebuilds its
  columns.
- **A `Custom` column with no `FilterTemplate` never reaches the grid.** The column component refuses it
  when it mounts, with an `ArgumentException` naming `FilterTemplate`, rather than drawing an empty
  control. The interface itself cannot enforce that — the member is null for every other filter type — so
  a definition built by hand gets no check at all.
- **`Editable` alone does not make a column editable.** It is one of three conditions; the other two are
  the grid's own `Editable` and a writable member, and a column that claims editability without one is
  refused when the column component mounts.
- **`ReadValue` boxes and casts.** Calling it from a helper with a different row type throws rather than
  returning null, which is why the query engine works with the generic definition instead.

## See also

- [PolarisGridColumnDefinition.cs.md](PolarisGridColumnDefinition.cs.md) — the typed implementation, with
  the accessor, setter and templates.
- [PolarisGridState.cs.md](PolarisGridState.cs.md) — `Columns` is the ordered list of these.
- [PolarisDataFilter.cs.md](../Components/PolarisDataFilter.cs.md) — the non-generic consumer.
- [PolarisGridEnums.cs.md](PolarisGridEnums.cs.md) — `PolarisGridFilterType` and
  `PolarisGridEditorType`.
