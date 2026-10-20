using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Polaris.Blazor.DataGrid;
using Polaris.Blazor.Events;

namespace Polaris.Blazor.Components;

/// <summary>
/// Renders one page of a local collection as a Polaris table, with sorting, selection and empty,
/// loading and error states.
/// </summary>
/// <remarks>
/// <para>
/// Composed rather than wrapped, because <c>s-table</c> is a table and nothing more: it has no
/// sorting, no selection and no notion of a page. What this component adds is the behaviour, drawn
/// entirely with Polaris elements — a column heading is an <c>s-button</c>, a selectable row is an
/// <c>s-checkbox</c> in an <c>s-table-cell</c> — so the table still looks like every other table in
/// the admin.
/// </para>
/// <para>
/// The grid owns no state. It is handed a <see cref="PolarisGridState{TItem}"/> that the page
/// created, and a <see cref="PolarisDataFilter"/> and <see cref="PolarisDataPager"/> elsewhere on
/// the page are handed the same instance. That is what lets the three be laid out separately while
/// still describing one grid.
/// </para>
/// <para>
/// The columns are declared as <see cref="PolarisGridColumn{TItem}"/> children and rendered where
/// they cannot be seen. They must stay rendered even while the table is not: a filter's controls come
/// from the columns that are registered, so a column that is disposed with the table would take the
/// merchant's way back to their data with it.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;PolarisDataFilter State="@State" /&gt;
///
/// &lt;PolarisDataGrid TItem="Alloy" State="@State" AllowSelection="true"&gt;
///     &lt;Columns&gt;
///         &lt;PolarisGridColumn TItem="Alloy" Field="alloy =&gt; alloy.Name"
///                            Title="Alloy" FilterType="PolarisGridFilterType.Text" /&gt;
///         &lt;PolarisGridColumn TItem="Alloy" Field="alloy =&gt; alloy.Silver"
///                            Title="Silver %" Format="P0"
///                            Alignment="PolarisTableHeaderFormat.Numeric"
///                            FilterType="PolarisGridFilterType.Range" /&gt;
///     &lt;/Columns&gt;
/// &lt;/PolarisDataGrid&gt;
///
/// &lt;PolarisDataPager State="@State" /&gt;
/// </code>
/// </example>
/// <typeparam name="TItem">The row type.</typeparam>
public sealed class PolarisDataGrid<TItem> : PolarisGridComponentBase
{
    private PolarisGridState<TItem>? _selectionSource;

    /// <summary>The shared state. Required, and owned by the page that created it.</summary>
    [Parameter, EditorRequired]
    public PolarisGridState<TItem> State { get; set; } = default!;

    /// <summary>The column declarations, one <see cref="PolarisGridColumn{TItem}"/> each.</summary>
    [Parameter] public RenderFragment? Columns { get; set; }

    /// <summary>Shows a checkbox column, and a header checkbox that selects the visible page.</summary>
    [Parameter] public bool AllowSelection { get; set; }

    /// <summary>
    /// Raised with the keys of the selected records after a selection change, including one caused
    /// by a chip or a keyboard.
    /// </summary>
    [Parameter] public EventCallback<IReadOnlyCollection<object>> SelectionChanged { get; set; }

    /// <summary>
    /// A heading for the empty state. Shown when nothing matched the filters, which is not the same
    /// thing as having no records — the caller usually knows which.
    /// </summary>
    [Parameter] public string? EmptyHeading { get; set; }

    /// <summary>Supporting text under the empty heading.</summary>
    [Parameter] public string? EmptySubheading { get; set; }

    /// <summary>
    /// Replaces the whole empty state. The action slots accept exactly one button each, so a caller
    /// that wants a "clear the filters" action supplies it here rather than through a parameter.
    /// </summary>
    [Parameter] public RenderFragment? EmptyContent { get; set; }

    /// <summary>Replaces the spinner shown while <see cref="PolarisGridState.IsLoading"/> is set.</summary>
    [Parameter] public RenderFragment? LoadingContent { get; set; }

    /// <inheritdoc />
    protected override PolarisGridState? BoundState => State;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        base.OnParametersSet();

        if (ReferenceEquals(_selectionSource, State))
        {
            return;
        }

        if (_selectionSource is not null)
        {
            _selectionSource.SelectionChanged -= OnSelectionChanged;
        }

        _selectionSource = State;

        if (_selectionSource is not null)
        {
            _selectionSource.SelectionChanged += OnSelectionChanged;
        }
    }

    /// <summary>Redraws the grid when its columns changed without the query changing.</summary>
    internal void NotifyColumnChanged()
    {
        if (State is not null)
        {
            _ = InvokeAsync(StateHasChanged);
        }
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        if (_selectionSource is not null)
        {
            _selectionSource.SelectionChanged -= OnSelectionChanged;
            _selectionSource = null;
        }

        base.Dispose();
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        // s-section padding="none" is Shopify's own guidance for wrapping a table: a section pads its
        // content, and a padded table has a gutter between its rules and the page.
        builder.OpenComponent<PolarisSection>(0);
        builder.AddAttribute(1, nameof(PolarisSection.Padding), PolarisSectionPadding.None);

        // A marker for tests and for a stylesheet, on the one element the grid owns outright. Nothing in
        // the library's CSS needs it, but a browser check has to be able to tell this table from the
        // plain `PolarisTable` in the component gallery — one has sorting and one does not, and asking
        // "does the first heading contain a button" is the measurement, not the way to find the grid.
        builder.AddAttribute(2, nameof(PolarisSection.AdditionalAttributes), new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["data-polaris-data-grid"] = true,
        });

        builder.AddAttribute(3, nameof(PolarisSection.ChildContent), (RenderFragment)BuildSectionContent);
        builder.CloseComponent();
    }

    private void BuildSectionContent(RenderTreeBuilder builder)
    {
        // Fixed, because the value is this component: the columns only need to find the grid, and
        // re-cascading on every render would re-render all of them for nothing.
        builder.OpenComponent<CascadingValue<PolarisDataGrid<TItem>>>(0);
        builder.AddAttribute(1, nameof(CascadingValue<PolarisDataGrid<TItem>>.Value), this);
        builder.AddAttribute(2, nameof(CascadingValue<PolarisDataGrid<TItem>>.IsFixed), true);
        builder.AddAttribute(3, nameof(CascadingValue<PolarisDataGrid<TItem>>.ChildContent), (RenderFragment)BuildContent);
        builder.CloseComponent();
    }

    private void BuildContent(RenderTreeBuilder builder)
    {
        // Rendered before anything that depends on it, and never removed: this fragment is what keeps
        // the column components alive, and a registered column is what the filter draws its controls
        // from. Inside the table it would be disposed the moment the result went empty.
        if (Columns is not null)
        {
            builder.AddContent(0, Columns);
        }

        if (State is null)
        {
            return;
        }

        if (State.IsLoading)
        {
            BuildLoadingContent(builder);
            return;
        }

        if (!string.IsNullOrEmpty(State.ErrorMessage))
        {
            BuildErrorContent(builder);
            return;
        }

        if (State.Result.IsEmpty)
        {
            BuildEmptyContent(builder);
            return;
        }

        builder.OpenComponent<PolarisTable>(2);
        builder.AddAttribute(3, nameof(PolarisTable.ChildContent), (RenderFragment)BuildTable);
        builder.CloseComponent();
    }

    private void BuildTable(RenderTreeBuilder builder)
    {
        builder.OpenComponent<PolarisTableHeaderRow>(0);
        builder.AddAttribute(1, nameof(PolarisTableHeaderRow.ChildContent), (RenderFragment)BuildHeaderRow);
        builder.CloseComponent();

        builder.OpenComponent<PolarisTableBody>(2);
        builder.AddAttribute(3, nameof(PolarisTableBody.ChildContent), (RenderFragment)BuildRows);
        builder.CloseComponent();
    }

    private void BuildHeaderRow(RenderTreeBuilder builder)
    {
        if (AllowSelection)
        {
            builder.OpenComponent<PolarisTableHeader>(0);
            builder.AddAttribute(1, nameof(PolarisTableHeader.ChildContent), (RenderFragment)BuildSelectAllCell);
            builder.CloseComponent();
        }

        // One literal sequence per attribute: the headers are siblings, so the same numbers are
        // correct for each of them and the diff stays positional.
        foreach (var column in VisibleColumns())
        {
            builder.OpenComponent<PolarisTableHeader>(10);
            builder.AddAttribute(11, nameof(PolarisTableHeader.Format), column.Alignment);
            builder.AddAttribute(12, nameof(PolarisTableHeader.ListSlot), column.ListSlot);
            builder.AddAttribute(13, nameof(PolarisTableHeader.ChildContent), (RenderFragment)(cell => BuildHeaderCell(cell, column)));
            builder.CloseComponent();
        }
    }

    private void BuildSelectAllCell(RenderTreeBuilder builder)
    {
        var pageRows = State.Result.Items;
        var selected = 0;

        foreach (var item in pageRows)
        {
            if (State.IsSelected(item))
            {
                selected++;
            }
        }

        builder.OpenComponent<PolarisCheckbox>(0);
        builder.AddAttribute(1, nameof(PolarisCheckbox.Label), "Select every row on this page");
        builder.AddAttribute(2, nameof(PolarisCheckbox.LabelAccessibilityVisibility), PolarisAccessibilityVisibility.Exclusive);
        builder.AddAttribute(3, nameof(PolarisCheckbox.Checked), pageRows.Count > 0 && selected == pageRows.Count);
        builder.AddAttribute(4, nameof(PolarisCheckbox.Indeterminate), selected > 0 && selected < pageRows.Count);
        builder.AddAttribute(5, nameof(PolarisCheckbox.OnChange), EventCallback.Factory.Create<PolarisEventArgs>(this, SelectPageAsync));
        builder.CloseComponent();
    }

    private void BuildHeaderCell(RenderTreeBuilder builder, IPolarisGridColumn column)
    {
        if (!column.Sortable)
        {
            builder.AddContent(0, column.Title);
            return;
        }

        // A heading that sorts is a button, because a button is what a keyboard can reach and what a
        // screen reader announces as activatable. Polaris has no sortable header of its own — s-table
        // documents no sorting at all — so the affordance has to be made of something.
        builder.OpenComponent<PolarisButton>(0);
        builder.AddAttribute(1, nameof(PolarisButton.Variant), PolarisButtonVariant.Tertiary);
        builder.AddAttribute(2, nameof(PolarisButton.Icon), SortIcon(column.Id));
        builder.AddAttribute(3, nameof(PolarisButton.AccessibilityLabel), SortLabel(column));
        builder.AddAttribute(4, nameof(PolarisButton.OnClick), EventCallback.Factory.Create<PolarisEventArgs>(this, _ => ToggleSortAsync(column.Id)));
        builder.AddAttribute(5, nameof(PolarisButton.ChildContent), (RenderFragment)(text => text.AddContent(0, column.Title)));
        builder.CloseComponent();
    }

    private void BuildRows(RenderTreeBuilder builder)
    {
        foreach (var item in State.Result.Items)
        {
            builder.OpenComponent<PolarisTableRow>(0);

            // Keyed by the record, so a checkbox and its row stay together when the order changes.
            // Without a key Blazor reuses a row position, and a tick appears to jump to another row.
            builder.SetKey(State.KeyOf(item));

            builder.AddAttribute(1, nameof(PolarisTableRow.ChildContent), (RenderFragment)(row => BuildRowCells(row, item)));
            builder.CloseComponent();
        }
    }

    private void BuildRowCells(RenderTreeBuilder builder, TItem item)
    {
        if (AllowSelection)
        {
            builder.OpenComponent<PolarisTableCell>(0);
            builder.AddAttribute(1, nameof(PolarisTableCell.ChildContent), (RenderFragment)(cell => BuildRowCheckbox(cell, item)));
            builder.CloseComponent();
        }

        foreach (var column in VisibleColumns())
        {
            builder.OpenComponent<PolarisTableCell>(10);
            builder.AddAttribute(11, nameof(PolarisTableCell.ChildContent), (RenderFragment)(cell => BuildCellContent(cell, column, item)));
            builder.CloseComponent();
        }
    }

    private void BuildRowCheckbox(RenderTreeBuilder builder, TItem item)
    {
        builder.OpenComponent<PolarisCheckbox>(0);
        builder.AddAttribute(1, nameof(PolarisCheckbox.Label), "Select this row");
        builder.AddAttribute(2, nameof(PolarisCheckbox.LabelAccessibilityVisibility), PolarisAccessibilityVisibility.Exclusive);
        builder.AddAttribute(3, nameof(PolarisCheckbox.Checked), State.IsSelected(item));
        builder.AddAttribute(4, nameof(PolarisCheckbox.OnChange), EventCallback.Factory.Create<PolarisEventArgs>(this, args => ToggleRowAsync(item, args)));
        builder.CloseComponent();
    }

    private void BuildCellContent(RenderTreeBuilder builder, IPolarisGridColumn column, TItem item)
    {
        var template = column is PolarisGridColumnDefinition<TItem> typed ? typed.CellTemplate : null;

        if (template is not null)
        {
            builder.AddContent(0, template(item));
            return;
        }

        builder.AddContent(1, column.FormatValue(column.ReadValue(item!)));
    }

    private void BuildEmptyContent(RenderTreeBuilder builder)
    {
        if (EmptyContent is not null)
        {
            builder.AddContent(0, EmptyContent);
            return;
        }

        builder.OpenComponent<PolarisEmptyState>(0);
        builder.AddAttribute(1, nameof(PolarisEmptyState.Heading), EmptyHeading ?? "No records to show");
        builder.AddAttribute(2, nameof(PolarisEmptyState.Subheading), (RenderFragment?)DefaultEmptySubheading());
        builder.CloseComponent();
    }

    private RenderFragment? DefaultEmptySubheading()
        => EmptySubheading is null
            ? null
            : subheading => subheading.AddContent(0, EmptySubheading);

    private void BuildLoadingContent(RenderTreeBuilder builder)
    {
        if (LoadingContent is not null)
        {
            builder.AddContent(0, LoadingContent);
            return;
        }

        builder.OpenComponent<PolarisStack>(0);
        builder.AddAttribute(1, nameof(PolarisStack.Direction), PolarisStackDirection.Inline);
        builder.AddAttribute(2, nameof(PolarisStack.Gap), PolarisGap.Base);
        builder.AddAttribute(3, nameof(PolarisStack.AlignItems), "center");
        builder.AddAttribute(4, nameof(PolarisStack.ChildContent), (RenderFragment)(content =>
        {
            content.OpenComponent<PolarisSpinner>(0);
            content.AddAttribute(1, nameof(PolarisSpinner.AccessibilityLabel), "Loading records");
            content.CloseComponent();

            content.OpenComponent<PolarisText>(2);
            content.AddAttribute(3, nameof(PolarisText.Color), PolarisColor.Subdued);
            content.AddAttribute(4, nameof(PolarisText.ChildContent), (RenderFragment)(text => text.AddContent(0, "Loading records…")));
            content.CloseComponent();
        }));
        builder.CloseComponent();
    }

    private void BuildErrorContent(RenderTreeBuilder builder)
    {
        builder.OpenComponent<PolarisBanner>(0);
        builder.AddAttribute(1, nameof(PolarisBanner.Heading), "The grid could not be loaded");
        builder.AddAttribute(2, nameof(PolarisBanner.Tone), PolarisTone.Critical);
        builder.AddAttribute(3, nameof(PolarisBanner.ChildContent), (RenderFragment)(content => content.AddContent(0, State.ErrorMessage)));
        builder.CloseComponent();
    }

    private IEnumerable<IPolarisGridColumn> VisibleColumns()
        => State.Columns.Where(column => column.Visible);

    private string? SortIcon(string columnId) => State.SortDirectionFor(columnId) switch
    {
        PolarisGridSortDirection.Ascending => "chevron-up",
        PolarisGridSortDirection.Descending => "chevron-down",
        _ => null,
    };

    private string? SortLabel(IPolarisGridColumn column) => State.SortDirectionFor(column.Id) switch
    {
        // Only stated when there is something to state: an unsorted column is announced by its own
        // text, and repeating "not sorted" on every heading is noise.
        PolarisGridSortDirection.Ascending => string.Concat(column.Title, ", sorted ascending"),
        PolarisGridSortDirection.Descending => string.Concat(column.Title, ", sorted descending"),
        _ => null,
    };

    private Task ToggleSortAsync(string columnId)
    {
        State.ToggleSort(columnId);
        return Task.CompletedTask;
    }

    private async Task ToggleRowAsync(TItem item, PolarisEventArgs args)
    {
        State.SetSelected(item, args.Checked ?? !State.IsSelected(item));
        await RaiseSelectionChangedAsync();
    }

    private async Task SelectPageAsync(PolarisEventArgs args)
    {
        State.SetPageSelected(args.Checked ?? false);
        await RaiseSelectionChangedAsync();
    }

    private Task RaiseSelectionChangedAsync()
        => SelectionChanged.HasDelegate
            ? SelectionChanged.InvokeAsync([.. State.SelectedKeys])
            : Task.CompletedTask;

    private void OnSelectionChanged() => _ = InvokeAsync(StateHasChanged);
}
