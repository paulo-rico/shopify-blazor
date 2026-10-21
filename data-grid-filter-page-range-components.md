# Technical Specification: Reusable Blazor DataGrid Library

Version 1.0 · Local-data architecture · Spreadsheet editing · Interactive Blazor

This version is tailored to your updated decisions: local in-memory data, spreadsheet-style editing, configurable filtering, separately composed components, and interactive Blazor rendering.

The main difference from a library designed for remote data is that the query engine can work directly with an in-memory collection. That simplifies the first implementation considerably, while keeping the public API reusable across your Shopify app's alloy, ingredient, ring-size and lookup screens.

I recommend a standalone Razor Class Library (RCL) containing four primary components and a shared state/query layer:

- `DataGrid<TItem>` — table rendering, sorting, selection and spreadsheet editing.
- `DataFilter<TItem>` — metadata-driven filter controls.
- `DataPager` — page navigation and page-size selection.
- `RangeSlider` — a reusable dual-handle numeric range control.

The four components will be placed separately in the consuming page and share a single `DataGridState<TItem>` instance. This makes the layout flexible without duplicating filtering or paging logic.

## 1. Scope and architectural decisions

| Decision    | Specification                                                   |
| ----------- | --------------------------------------------------------------- |
| Data source | Local `IEnumerable<TItem>` or `IReadOnlyList<TItem>`            |
| Filtering   | Configurable immediate, debounced or manual Apply               |
| Editing     | Spreadsheet-style cell navigation, with row-level Save/Cancel   |
| Composition | Separate DataGrid, DataFilter and DataPager                     |
| State       | One shared state instance per dataset/grid                      |
| Sorting     | Single- and multi-column sorting                                |
| Paging      | Client-side, after filtering and sorting                        |
| RangeSlider | Two handles on one track, numeric inputs optional               |
| Persistence | Caller-provided callbacks; no database dependency               |
| Rendering   | Interactive Blazor components                                   |
| Testing     | Unit tests, bUnit component tests and browser interaction tests |

### Important local-data assumption

The library will process records already loaded into memory. It will not load pages from a server or execute SQL queries.

For predictable behaviour, I recommend accepting an `IReadOnlyList<TItem>` as the primary local input. You can provide an `IEnumerable<TItem>` convenience overload, but materialize it into a stable snapshot when the dataset is loaded or replaced.

This avoids repeatedly enumerating a lazy sequence and gives the query engine a consistent dataset to work against.

One important distinction: the library can process records locally, but the application still decides how changes are persisted. For example, saving an edited alloy can update the local collection and then invoke an application callback that writes the change to SQL Server or another service.

# 2. Solution and file structure

Use a single reusable library project initially. Keep the query engine and state management in separate folders, so they can later be extracted into a separate core project if needed.

### The following structure shows as a separate solution. Can you actually integrate these components into Polaris.Blazor.
```
ReusableDataGrid.sln
│
├── src/
│   ├── ReusableDataGrid/
│   │   ├── Components/
│   │   │   ├── DataGrid.razor
│   │   │   ├── DataGrid.razor.cs
│   │   │   ├── DataGridHeader.razor
│   │   │   ├── DataGridRow.razor
│   │   │   ├── DataGridCell.razor
│   │   │   ├── DataFilter.razor
│   │   │   ├── DataPager.razor
│   │   │   └── RangeSlider.razor
│   │   ├── Columns/
│   │   │   ├── GridColumn.razor
│   │   │   ├── GridColumnDefinition.cs
│   │   │   └── GridColumnRegistry.cs
│   │   ├── Filtering/
│   │   │   ├── GridFilterDescriptor.cs
│   │   │   ├── GridFilterEvaluator.cs
│   │   │   └── GridFilterOptionProvider.cs
│   │   ├── Query/
│   │   │   ├── GridQueryState.cs
│   │   │   ├── GridQueryResult.cs
│   │   │   └── GridQueryEngine.cs
│   │   ├── Editing/
│   │   │   ├── EditBuffer.cs
│   │   │   ├── GridEditCoordinator.cs
│   │   │   └── GridValueConverter.cs
│   │   ├── State/
│   │   │   ├── DataGridState.cs
│   │   │   └── GridSelectionState.cs
│   │   ├── Data/
│   │   │   └── LocalGridDataSource.cs
│   │   ├── wwwroot/
│   │   │   ├── reusable-data-grid.css
│   │   │   └── range-slider.js
│   │   ├── _Imports.razor
│   │   └── ReusableDataGrid.csproj
│   │
│   └── ReusableDataGrid.Demo/
│       ├── Components/
│       ├── Pages/
│       └── Models/
│
└── tests/
    ├── ReusableDataGrid.UnitTests/
    ├── ReusableDataGrid.ComponentTests/
    └── ReusableDataGrid.BrowserTests/
```

You can consolidate the small classes during early development. The important part is keeping component rendering separate from query evaluation and edit coordination.

## Project responsibilities

- `Components`: Razor markup and UI interaction.
- `Columns`: typed field metadata and rendering configuration.
- `Filtering`: filter descriptors, operators and option generation.
- `Query`: filtering, sorting and paging.
- `Editing`: keyboard navigation, draft values and validation.
- `State`: shared state, notifications and selection.
- `Data`: local collection handling and update callbacks.
- `Demo`: a working example using representative jewellery data.
- `Tests`: correctness, rendering and browser-level interaction.

# 3. Public component API

The following is the proposed API contract. It is a design specification, not a complete drop-in implementation. The exact component markup and registration code should be developed incrementally and compiled as you go.

## 3.1 DataGrid

DataGrid receives the shared state and a typed column collection. It does not own filtering or pagination independently.

```
<DataGrid TItem="Alloy"
          State="@GridState"
          KeySelector="x => x.Id"
          EditMode="GridEditMode.Spreadsheet"
          AllowSorting="true"
          AllowSelection="true"
          AllowEditing="true">

    <Columns>
        <GridColumn TItem="Alloy"
                    Field="x => x.Name"
                    Title="Alloy Name"
                    Editable="true"
                    FilterType="GridFilterType.Text" />

        <GridColumn TItem="Alloy"
                    Field="x => x.Carat"
                    Title="Carat"
                    Editable="true"
                    FilterType="GridFilterType.MultiSelect" />

        <GridColumn TItem="Alloy"
                    Field="x => x.SilverPercentage"
                    Title="Silver %"
                    Editable="true"
                    FilterType="GridFilterType.Range"
                    Format="P2" />
    </Columns>
</DataGrid>
```

The parent owns the state and loads the local records. DataGrid registers its columns with the state and renders the currently processed page.

### Parameters

| Parameter        | Type                   | Purpose                                 |
| ---------------- | ---------------------- | --------------------------------------- |
| `State`          | `DataGridState<TItem>` | Required shared state                   |
| `KeySelector`    | `Func<TItem, object>`  | Stable record identity                  |
| `AllowSorting`   | `bool`                 | Enable column sorting                   |
| `AllowSelection` | `bool`                 | Enable row selection                    |
| `AllowEditing`   | `bool`                 | Enable spreadsheet editing              |
| `EditMode`       | `GridEditMode`         | Editing behaviour                       |
| `Columns`        | Child content          | Typed column definitions                |
| `OnSaveAsync`    | Save callback          | Persist changes through the application |
| `OnDeleteAsync`  | Delete callback        | Optional record deletion                |
| `RowClass`       | Row-class callback     | Conditional styling                     |
| `EmptyContent`   | `RenderFragment?`      | Custom empty state                      |
| `LoadingContent` | `RenderFragment?`      | Custom loading state                    |

I would include `KeySelector` even when records are local. A stable identity prevents selection and edit buffers from becoming associated with the wrong record when the collection is sorted or filtered.

### Column definitions

Each column should expose:

- Stable column ID and display title.
- Typed property accessor.
- Optional setter or edit-value callback.
- Data type and formatting.
- Sortable, filterable and editable flags.
- Filter type and optional filter options.
- Editor type and optional editor template.
- Width, alignment, visibility and CSS class.
- Custom cell and header templates.
- Optional validation and edit permission callbacks.

Use `Expression<Func<TItem, TValue>>` for typed property declarations. Convert those expressions to cached getters and setters during column registration. For a property that cannot be assigned directly, support an explicit setter or custom editor.

Do not use arbitrary reflection for every cell render.

## 3.2 DataFilter

```
<DataFilter TItem="Alloy"
            State="@GridState"
            ApplyMode="FilterApplyMode.Debounced"
            ShowActiveFilters="true"
            ShowClearAll="true" />
```

DataFilter reads the column metadata from the shared state and generates the appropriate controls.

Its responsibilities include:

- Text search.
- Multi-select checkboxes.
- Boolean filtering.
- Dual-handle numeric range filtering.
- Date-range filtering.
- Lookup selectors.
- Optional null/blank filtering.
- Active filter chips.
- Clear-one and clear-all actions.
- Configurable immediate, debounced and manual application.

Do not have DataFilter independently process the collection. It should update the shared filter criteria and ask the state/query layer to recompute the visible result.

## 3.3 DataPager

```
<DataPager State="@GridState"
           PageSizeOptions="@(new[] { 10, 25, 50, 100 })"
           ShowFirstLast="true"
           ShowPageNumbers="true"
           ShowRecordRange="true" />
```

DataPager provides:

- First, previous, next and last navigation.
- Numbered page buttons where appropriate.
- Page-size selection.
- Current page and total page count.
- Current record range, such as `26–50 of 148`.
- Optional direct page-number entry.
- Correct handling of empty results and a deleted final row.

The pager updates `GridQueryState.PageIndex` and triggers the shared state to recompute the visible page.

## 3.4 RangeSlider

```
<RangeSlider Min="0"
             Max="100"
             Step="0.1"
             @bind-Value="@SelectedRange"
             Format="N1"
             Unit="%" />
```

I recommend a single range value rather than two unrelated bindable values.

```
public readonly record struct NumericRange(
    decimal Lower,
    decimal Upper);
```

The component's public parameters should include:

- `Min`, `Max` and `Step`.
- `Value` and `ValueChanged`.
- `Disabled`.
- `ShowInputs` and `ShowTicks`.
- Optional unit and numeric format.
- Optional debounce and commit-on-release behaviour.

The component itself should not know anything about grid filters. It emits a selected range; DataFilter turns that into a `GridFilterDescriptor`.

For v1, implement a single track with two handles and a highlighted interval. Keyboard, pointer and touch interactions must be tested independently.

# 4. Core C# models

These models are the common language used by all four components.

## 4.1 Enums

```
public enum GridFilterType
{
    None,
    Text,
    MultiSelect,
    Boolean,
    Range,
    DateRange,
    Lookup,
    Custom
}

public enum GridOperator
{
    Equals,
    NotEquals,
    Contains,
    StartsWith,
    EndsWith,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual,
    Between,
    In,
    IsNull,
    IsNotNull
}

public enum GridSortDirection
{
    Ascending,
    Descending
}

public enum FilterApplyMode
{
    Immediate,
    Debounced,
    Manual
}

public enum GridEditMode
{
    None,
    Cell,
    Row,
    Spreadsheet
}

public enum GridEditorType
{
    Auto,
    Text,
    Number,
    Boolean,
    Select,
    MultiSelect,
    Date,
    DateTime,
    Custom
}
```

## 4.2 Filter descriptor

Filters should be data objects, not embedded UI behaviour.

```
public sealed record GridFilterDescriptor(
    string Field,
    GridOperator Operator,
    object? Value = null,
    object? SecondValue = null,
    IReadOnlyList<object?>? Values = null,
    bool IncludeNulls = false);
```

Examples:

- `Name Contains "white"`.
- `Carat In [9, 18]`.
- `SilverPercentage Between 0.2 and 0.4`.
- `IsActive Equals true`.

For a range filter, `Value` is the lower bound and `SecondValue` is the upper bound. Both endpoints should be inclusive by default.

Because the data is local, these values can remain typed objects internally. Validate them against column metadata before applying the filter, so an invalid numeric value cannot accidentally be compared as a string.

## 4.3 Sort descriptor

```
public sealed record GridSortDescriptor(
    string Field,
    GridSortDirection Direction,
    int Priority = 0);
```

The priority supports multi-column sorting. For example, sort by `Carat`, then by `Name`.

Sorting should be stable: records with identical sort values should retain a predictable relative order. A stable row key can serve as a final tie-breaker when deterministic ordering is needed.

## 4.4 Query state and results

```
public sealed class GridQueryState
{
    public List<GridFilterDescriptor> Filters { get; } = [];
    public List<GridSortDescriptor> Sorts { get; } = [];

    public int PageIndex { get; set; }
    public int PageSize { get; set; } = 25;

    public string? GlobalSearch { get; set; }
}

public sealed record GridQueryResult<TItem>(
    IReadOnlyList<TItem> Items,
    int TotalCount);
```

The query state is mutable and owned by `DataGridState<TItem>`. The query result is an immutable snapshot of the currently visible page and the total number of matching records.

`TotalCount` must be calculated after filtering but before paging.

## 4.5 Column metadata

A column definition should be created from the typed Razor component and stored in a runtime registry.

```
public sealed class GridColumnDefinition<TItem>
{
    public required string Id { get; init; }
    public required string Title { get; init; }

    public bool Visible { get; set; } = true;
    public bool Sortable { get; init; } = true;
    public bool Filterable { get; init; }
    public bool Editable { get; init; }

    public GridFilterType FilterType { get; init; }
    public GridEditorType EditorType { get; init; }

    public string? Format { get; init; }
    public string? Width { get; init; }

    public required Func<TItem, object?> ValueGetter { get; init; }

    public Func<TItem, object?, TItem>? WithValue { get; init; }
}
```

`WithValue` represents an immutable-style update operation: it returns an updated record rather than mutating the original. For mutable models, use a setter abstraction instead. The implementation should support one consistent internal edit-buffer strategy regardless of the model's mutability.

# 5. Local query engine

Implement the query engine as a plain C# class, without references to Razor components or Blazor lifecycle methods.

Its input is:

- The current local collection.
- The column registry.
- The current query state.

Its output is the current page plus the filtered total count.

## Processing order

1\. Source snapshot

Local records

2\. Apply filters

AND across fields; OR within selected values

3\. Count matching records

4\. Sort

One or more sort descriptors

5\. Page

Skip and Take

### Filtering semantics

Default rules:

- AND between different field filters.
- OR between values selected for the same multi-select filter.
- Global search matches any global-searchable column and is combined with the column filters using AND.
- Text matching is case-insensitive by default.
- Numeric filters compare numeric values, not formatted display strings.
- Null handling is explicit.
- Date ranges use defined date semantics and should not silently mix local dates with UTC timestamps.

The query engine should expose a simple method such as:

```
public GridQueryResult<TItem> Execute<TItem>(
    IReadOnlyList<TItem> items,
    IReadOnlyList<GridColumnDefinition<TItem>> columns,
    GridQueryState state)
{
    // 1. Validate the page size and query descriptors.
    // 2. Apply global search and column filters.
    // 3. Calculate TotalCount.
    // 4. Apply stable sorting.
    // 5. Apply Skip/Take.
    // 6. Return a result snapshot.
    throw new NotImplementedException();
}
```

This is an implementation outline, not a working implementation yet.

### Performance considerations

For typical admin datasets, LINQ over in-memory collections is sufficient. Start with straightforward, readable implementations and profile before adding complexity.

If filtering and sorting become expensive:

- Cache compiled property getters.
- Avoid repeated reflection.
- Materialize the filtered result once per query refresh.
- Avoid rebuilding filter options on every render.
- Debounce text searches and slider interactions.
- Consider a cached normalized search representation for large text-heavy datasets.

Do not cache query results indefinitely: edits and source changes must invalidate affected caches.

# 6. Shared state management

`DataGridState<TItem>` is the central coordination object. Every component in the same grid instance receives the same state reference.

Its responsibilities are:

- Hold the local dataset snapshot and registered columns.
- Maintain filter criteria, sort descriptors and paging settings.
- Recompute and expose the current query result.
- Notify DataGrid, DataFilter and DataPager when relevant state changes.
- Manage selected row keys.
- Track the active editing session.
- Provide methods for replacing or refreshing the dataset.
- Reset the page when a filter changes.
- Ensure that disposed components no longer receive notifications.

## Proposed API

```
public sealed class DataGridState<TItem> : IDisposable
{
    public GridQueryState Query { get; } = new();

    public GridQueryResult<TItem>? Result { get; private set; }

    public bool IsLoading { get; private set; }
    public string? ErrorMessage { get; private set; }

    public event Action? Changed;

    public void SetFilter(GridFilterDescriptor filter)
    {
        // Replace the existing filter for this field.
        // Reset to page zero and recompute.
    }

    public void ClearFilter(string field)
    {
        // Remove this field's filter and recompute.
    }

    public void ClearAllFilters()
    {
        // Remove filters and global search.
    }

    public void SetSort(GridSortDescriptor sort)
    {
        // Update sort state and recompute.
    }

    public void SetPage(int pageIndex)
    {
        // Validate the index and update the visible page.
    }

    public void SetPageSize(int pageSize)
    {
        // Validate the size, reset or preserve the visible
        // record position according to the configured policy.
    }

    public void Refresh()
    {
        // Re-run the query over the current source snapshot.
    }

    public void Dispose()
    {
        // Unsubscribe and release owned resources.
    }
}
```

The code illustrates the public surface; the implementation must ensure that every mutation raises the appropriate change notification and triggers a single consistent query refresh.

### State ownership

Because the components are separately composed, the parent page should create and own the state.

```
@page "/alloys"
@implements IDisposable

<DataFilter TItem="Alloy" State="@GridState" />

<DataGrid TItem="Alloy"
          State="@GridState"
          KeySelector="x => x.Id"
          AllowEditing="true" />

<DataPager State="@GridState" />
```

The parent should initialize `GridState` once, load the records, and dispose it when appropriate. Child components must not independently create or dispose the shared state.

The state should support replacing the source collection when the application reloads or changes the records. Use a stable key selector so selection and edit state can be reconciled after replacement.

# 7. Spreadsheet-style editing

The editing implementation should be separate from the grid's markup. Use a `GridEditCoordinator<TItem>` to manage keyboard navigation, draft values, validation and saving.

## Recommended behaviour

| Action                     | Behaviour                                               |
| -------------------------- | ------------------------------------------------------- |
| Click an editable cell     | Focus its editor                                        |
| Enter                      | Commit the cell draft and move down                     |
| Tab                        | Commit and move to the next editable cell               |
| Shift+Tab                  | Move to the previous editable cell                      |
| Escape                     | Discard the current uncommitted cell change             |
| Save row                   | Validate and persist all dirty cells in the row         |
| Cancel row                 | Discard all unsaved changes in that row                 |
| Validation failure         | Preserve the draft and display the error                |
| Filter or sort while dirty | Prompt, block or preserve the draft according to policy |

I recommend cell-level editing with row-level persistence. This gives spreadsheet-like interaction without sending a save request after every keystroke.

## Edit buffer

```
public sealed class EditBuffer<TItem>
{
    public required object RowKey { get; init; }
    public required TItem Original { get; init; }

    public Dictionary<string, object?> Changes { get; } = [];
    public Dictionary<string, string[]> Errors { get; } = [];

    public bool IsDirty => Changes.Count > 0;
    public bool IsSaving { get; set; }
}
```

The buffer stores only changed fields, so the application can save a minimal update. The original record must be a genuine snapshot when the model is mutable; simply storing a reference is not sufficient to support Cancel.

### Save contract

Define a callback such as:

```
public delegate Task<GridSaveResult<TItem>> GridSaveCallback<TItem>(
    TItem original,
    IReadOnlyDictionary<string, object?> changes,
    CancellationToken cancellationToken);
```

A save should:

1. Convert edited strings into the declared property types.
2. Validate individual fields.
3. Validate the complete row.
4. Invoke the callback.
5. Reconcile the returned record with the local source.
6. Clear the edit buffer only on success.
7. Retain the draft and show errors on failure.

The application decides whether to update SQL Server, an API or another persistence layer. The grid library must not silently assume that changing an in-memory object means it has been saved permanently.

Also define the behaviour for unsaved edits when a user changes filters, pages or sorts. For the first release, a confirmation or block-until-save policy is safer than silently losing the draft.

# 8. Filtering and RangeSlider specification

## Filter controls

Generate controls from column metadata rather than relying solely on CLR property types.

| Filter type  | Control                 | Default behaviour          |
| ------------ | ----------------------- | -------------------------- |
| Text         | Search textbox          | Case-insensitive contains  |
| Multi-select | Checkbox list           | OR between selected values |
| Boolean      | Three-state selector    | All / True / False         |
| Range        | Dual-handle RangeSlider | Inclusive numeric range    |
| DateRange    | Two date inputs         | Inclusive date interval    |
| Lookup       | Searchable selector     | Match selected key(s)      |
| Custom       | Render fragment         | Consumer-defined behaviour |

For enum properties, derive options from the enum. For categorical strings, allow explicit options or derive distinct values from the local dataset. Do not assume every string property is categorical.

### RangeSlider behaviour

The numeric bounds should normally be derived from the source dataset before applying the filter for that same field. Otherwise, selecting a narrow range can make it impossible to widen it again.

Requirements:

- One track with two independently adjustable handles.
- A highlighted interval.
- Configurable `Min`, `Max` and `Step`.
- Decimal support and numeric formatting.
- Optional numeric inputs.
- Keyboard, mouse and touch support.
- Correct handle-crossing behaviour.
- Bounds updates when the dataset changes.
- Debounced or commit-on-release callbacks.
- Accessible labels for both handles.
- Explicit handling of nulls, negative values and single-value datasets.

If the full numeric domain is selected, DataFilter should normally remove the corresponding active filter instead of sending a redundant full-range criterion to the query engine.

## Filter application modes

Support three modes:

- Immediate: update the grid as the value changes.
- Debounced: wait briefly after typing or dragging.
- Manual: edit draft filters and apply them through an Apply button.

The debounce interval should be configurable. Use a single filter coordinator so a text input and RangeSlider do not independently trigger duplicate query refreshes.

# 9. Styling, accessibility and interactivity

Provide a namespaced stylesheet and CSS custom properties for spacing, colours, borders, density and focus indicators. Avoid coupling the library to a particular CSS framework.

Key requirements:

- Semantic table markup.
- `aria-sort` on sortable headers.
- Labels and validation messages for editors.
- Predictable keyboard focus after editing.
- Visible focus indicators.
- Screen-reader labels for RangeSlider handles.
- Responsive horizontal scrolling.
- Empty, loading and error states.
- No reliance on colour alone to convey sorting, validation or selection.

For the RangeSlider, native HTML range inputs with a shared visual track are a reasonable starting point. A small JavaScript module may be used for precise pointer handling if necessary, but Blazor parameters should remain the source of truth.

For interactive rendering, the consuming application must provide the appropriate Blazor interactive render mode. The RCL supplies the components and static assets; it does not configure interactivity in the host application automatically.

# 10. Testing specification

Use three layers of automated tests. The query engine should have the broadest coverage because its correctness affects every component.

## 10.1 Unit tests

| Area            | Tests                                                                      |
| --------------- | -------------------------------------------------------------------------- |
| Filtering       | Each operator, multiple filters, case handling, nulls and empty strings    |
| Multi-select    | OR within a field, AND across fields                                       |
| Numeric ranges  | Inclusive bounds, decimals, negative values, full range and inverted input |
| Sorting         | Ascending, descending, multi-column and stable ordering                    |
| Paging          | Empty results, final partial page, invalid index and page-size changes     |
| Query engine    | Correct filter → count → sort → page order                                 |
| Column metadata | Duplicate IDs, missing getters and invalid filter definitions              |
| Shared state    | Page resets, change notifications and refresh after source replacement     |
| Editing         | Dirty tracking, cancellation, conversion errors and failed saves           |
| RangeSlider     | Handle bounds, step snapping, keyboard input and changing domains          |

Include tests where filtering produces no results, where every record has the same numeric value, and where a field contains nullable values. These cases often expose defects in otherwise straightforward grid implementations.

## 10.2 Component tests

Use bUnit or an equivalent Blazor component testing framework.

Test:

- Sorting updates the visible rows and sort indicator.
- The correct filter control is rendered for each column.
- Checkbox selections update the shared filter state.
- RangeSlider changes update numeric filter criteria.
- Immediate, debounced and manual filter modes behave differently as intended.
- Paging updates the displayed records and total range.
- Editing enters the correct cell editor.
- Enter, Tab, Shift+Tab and Escape follow the documented editing rules.
- Save and Cancel update the local collection correctly.
- Validation errors remain visible until resolved.
- Separate components respond to the same state changes.
- Components unsubscribe from state notifications on disposal.

## 10.3 Browser tests

Use Playwright or a comparable browser automation tool for interactions that component tests cannot fully validate:

- Dragging both RangeSlider handles.
- Handle stacking and crossing behaviour.
- Touch-sized interactions and narrow-screen layouts.
- Spreadsheet navigation across cells.
- Focus restoration after editing.
- Rapid typing and slider movement.
- Large datasets and rendering responsiveness.

### Performance acceptance criteria

Start with a representative dataset of 100,000 records. Set a measurable target for filtering, sorting and rendering, and profile it in a repeatable environment.

The grid should render only the current page rather than placing every record in the DOM. If large collections become too expensive to process, consider query caching or virtualization in a later release.

# 11. Implementation plan

## 1

Foundation and query engine

Create the RCL, models, column registry, filter evaluator, sorting and paging logic.

Deliverable: a tested C# query engine, independent of Blazor rendering.

## 2

Shared state and DataGrid

Implement state ownership, column registration, table rendering, sorting and selection.

Deliverable: a working grid that displays and sorts a local collection.

## 3

DataPager and DataFilter

Add paging controls, automatic filter generation, filter chips, reset behaviour and configurable application modes.

Deliverable: separately composed components that control the same query state.

## 4

Dual-handle RangeSlider

Implement the single-track control, numeric inputs, step normalization, accessibility and interaction tests.

Deliverable: a reusable slider with no direct dependency on the grid.

## 5

Spreadsheet editing

Add edit buffers, field-specific editors, keyboard navigation, validation, Save/Cancel and callbacks.

Deliverable: editable rows with reliable draft and error handling.

## 6

Hardening and packaging

Finish accessibility, responsive styling, browser tests, documentation and NuGet packaging.

Deliverable: a library that can be referenced by another Blazor project.

## Recommended completion criteria

- All query-engine unit tests pass.
- All four components work with the same shared state.
- Filtering and sorting operate on the complete local dataset before paging.
- Changing filters resets the page.
- Spreadsheet edits can be saved or cancelled without corrupting the source.
- RangeSlider supports mouse, keyboard and touch.
- Filter modes work as documented.
- Replacing the source dataset refreshes results and updates range bounds.
- The library builds independently of SQL Server or Entity Framework.
- The demo application and browser tests run in CI.

# 12. Development setup for VS Code and Linux

Create the solution using the .NET SDK installed on your development machine. Choose a framework version compatible with your existing application.

```
mkdir ReusableDataGrid
cd ReusableDataGrid

dotnet new sln -n ReusableDataGrid

dotnet new razorclasslib \
  -n ReusableDataGrid \
  -o src/ReusableDataGrid

dotnet new blazor \
  -n ReusableDataGrid.Demo \
  -o src/ReusableDataGrid.Demo

dotnet new xunit \
  -n ReusableDataGrid.UnitTests \
  -o tests/ReusableDataGrid.UnitTests

dotnet sln add src/ReusableDataGrid/ReusableDataGrid.csproj
dotnet sln add src/ReusableDataGrid.Demo/ReusableDataGrid.Demo.csproj
dotnet sln add tests/ReusableDataGrid.UnitTests/ReusableDataGrid.UnitTests.csproj
```

Add project references:

```
dotnet add src/ReusableDataGrid.Demo/ReusableDataGrid.Demo.csproj \
  reference src/ReusableDataGrid/ReusableDataGrid.csproj

dotnet add tests/ReusableDataGrid.UnitTests/ReusableDataGrid.UnitTests.csproj \
  reference src/ReusableDataGrid/ReusableDataGrid.csproj
```

Build and test:

```
dotnet restore
dotnet build
dotnet test
```

These commands establish the starting solution; the component and model files described in this specification still need to be implemented. If a template name differs in your SDK version, check `dotnet new list`.

## 13. What I would implement first

Start with a working local-data vertical slice rather than building all four components at once.

1. Define `GridColumnDefinition<TItem>`, `GridFilterDescriptor`, `GridSortDescriptor` and `GridQueryState`.
2. Implement and unit-test the local query engine, including null handling, filtering, sorting and paging.
3. Create `DataGridState<TItem>` and connect it to a basic DataGrid.
4. Add DataPager, followed by DataFilter and RangeSlider.
5. Implement spreadsheet editing once the state and query pipeline are stable.

That sequence minimizes rework because each later component builds on a tested foundation.

One final recommendation: keep the initial library focused on local data and make the save operation a callback. That will let you build and test the components independently of your Shopify app's database, while still allowing your application to persist changes when needed. You can then evolve the library without tying its architecture to a specific persistence technology.