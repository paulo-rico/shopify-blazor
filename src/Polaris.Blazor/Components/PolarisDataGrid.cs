using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Polaris.Blazor.DataGrid;
using Polaris.Blazor.Events;
using Polaris.Blazor.Interop;

namespace Polaris.Blazor.Components;

/// <summary>
/// Renders one page of a local collection as a Polaris table, with sorting, selection, spreadsheet
/// editing and empty, loading and error states.
/// </summary>
/// <remarks>
/// <para>
/// Composed rather than wrapped, because <c>s-table</c> is a table and nothing more: it has no
/// sorting, no selection, no editing and no notion of a page. What this component adds is the
/// behaviour, drawn entirely with Polaris elements — a column heading is an <c>s-button</c>, a
/// selectable row is an <c>s-checkbox</c> in an <c>s-table-cell</c>, an edited cell is an
/// <c>s-text-field</c> — so the table still looks like every other table in the admin.
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
/// &lt;PolarisDataGrid TItem="Alloy" State="@State" AllowSelection="true" Editable="true"&gt;
///     &lt;Columns&gt;
///         &lt;PolarisGridColumn TItem="Alloy" Field="alloy =&gt; alloy.Name" Title="Alloy"
///                            Editable="true" FilterType="PolarisGridFilterType.Text" /&gt;
///         &lt;PolarisGridColumn TItem="Alloy" Field="alloy =&gt; alloy.Silver" Title="Silver %"
///                            Editable="true" Format="P0"
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

    /// <summary>
    /// What identifies the cell being edited, for the interop call that moves the caret into it.
    /// </summary>
    /// <remarks>
    /// An attribute this grid writes, not a Polaris one: the editor is a Polaris field, and the input
    /// that takes typing is inside its shadow root, so the search has to start from an element this
    /// component owns.
    /// </remarks>
    private const string EditingCellSelector = "[data-polaris-grid-editing]";

    private readonly PolarisGridEditSession _session = new();

    private (object RowKey, string ColumnId)? _activeCell;
    private string? _editorText;
    private string? _editorError;
    private string? _saveError;
    private bool _isSaving;
    private bool _focusPending;
    private ElementReference _gridRoot;

    /// <summary>The shared state. Required, and owned by the page that created it.</summary>
    [Parameter, EditorRequired]
    public PolarisGridState<TItem> State { get; set; } = default!;

    /// <summary>The column declarations, one <see cref="PolarisGridColumn{TItem}"/> each.</summary>
    [Parameter] public RenderFragment? Columns { get; set; }

    /// <summary>Shows a checkbox column, and a header checkbox that selects the visible page.</summary>
    [Parameter] public bool AllowSelection { get; set; }

    /// <summary>
    /// Whether cells can be edited. Off by default, and when it is off the table renders exactly as a
    /// read-only grid does — no wrapper, no cursor, no focusable cell.
    /// </summary>
    /// <remarks>
    /// Only half the answer: a cell is editable when the grid is and the column is. That split is what
    /// lets one grid be mostly editable and keep its identifier or its computed column read-only.
    /// </remarks>
    [Parameter] public bool Editable { get; set; }

    /// <summary>When a committed edit reaches <see cref="SaveChanges"/>. Immediate by default.</summary>
    [Parameter] public PolarisGridSaveMode SaveMode { get; set; } = PolarisGridSaveMode.Immediate;

    /// <summary>
    /// Writes one edited row, and reports whether it was written.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The one place the grid and the merchant's data storage meet. Without it an edit is still made —
    /// against the in-memory collection the grid was given — which is what a screen over local data
    /// wants; with it, the edit is written where the caller says.
    /// </para>
    /// <para>
    /// Called once per edited row, not once per cell, so a save can be one transaction or one call.
    /// A returned <see cref="PolarisGridSaveResult.Failure"/> keeps the draft on screen, leaves the
    /// collection untouched and shows the message beside the grid; so does a thrown exception, which
    /// is turned into the same message rather than escaping into the circuit.
    /// </para>
    /// </remarks>
    [Parameter] public Func<PolarisGridRowEdit<TItem>, CancellationToken, Task<PolarisGridSaveResult>>? SaveChanges { get; set; }

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

    /// <summary>The interop service, for moving focus into a cell.</summary>
    [Inject]
    private IPolarisInterop Interop { get; set; } = default!;

    /// <summary>True when an edited cell has not been saved yet.</summary>
    public bool HasUnsavedChanges => !_session.IsEmpty;

    /// <summary>How many rows have at least one unsaved edit.</summary>
    public int UnsavedRowCount => _session.RowCount;

    /// <summary>True while <see cref="SaveAsync"/> is running.</summary>
    public bool IsSaving => _isSaving;

    /// <summary>
    /// Saves every edited row. Called by the grid's own Save button, and by a page that wants the save
    /// somewhere else — a keyboard shortcut, or its own toolbar.
    /// </summary>
    /// <param name="cancellationToken">Passed to <see cref="SaveChanges"/>.</param>
    /// <returns>
    /// Success when every row was written, or a failure carrying what to tell the merchant. Rows that
    /// were written have their drafts cleared and are in the collection; rows that were not keep their
    /// drafts and are unchanged.
    /// </returns>
    public async Task<PolarisGridSaveResult> SaveAsync(CancellationToken cancellationToken = default)
    {
        if (_session.IsEmpty || State is null)
        {
            return PolarisGridSaveResult.Success();
        }

        _isSaving = true;
        _saveError = null;
        StateHasChanged();

        try
        {
            var edits = new List<PolarisGridRowEdit<TItem>>();

            foreach (var key in _session.Keys.ToList())
            {
                if (State.FindItem(key) is { } item)
                {
                    edits.Add(new PolarisGridRowEdit<TItem>(key, item, _session.ChangesFor(key)));
                }
                else
                {
                    // The row was replaced out from under the draft — a reload that dropped it. There is
                    // nothing to write and nothing to show, and keeping the draft would put a value on
                    // screen that belongs to no record.
                    _session.ClearRow(key);
                }
            }

            var written = new List<PolarisGridRowEdit<TItem>>();
            var failures = new List<string>();

            foreach (var edit in edits)
            {
                var result = SaveChanges is null
                    ? PolarisGridSaveResult.Success()
                    : await InvokeSaveAsync(edit, cancellationToken);

                if (result.Succeeded)
                {
                    written.Add(edit);
                }
                else
                {
                    failures.Add(result.Message ?? "The row could not be saved.");
                }
            }

            if (written.Count > 0)
            {
                // One update for the whole save: every mutation of the state notifies the grid, the
                // chips and the pager, and a save of twelve rows would otherwise redraw the table twelve
                // times over a half-written collection.
                var updates = new Dictionary<object, TItem>();

                foreach (var edit in written)
                {
                    updates[edit.Key] = ApplyChanges(edit);
                    _session.ClearRow(edit.Key);
                }

                State.UpdateItems(updates);
            }

            // The message outlives the call: the button that started the save is re-rendered away by the
            // same state change that reports it, so a caller that only read the return value would show
            // nothing at all.
            if (failures.Count > 0)
            {
                _saveError = string.Join(" ", failures);
                return PolarisGridSaveResult.Failure(_saveError);
            }

            _saveError = null;
            return PolarisGridSaveResult.Success();
        }
        finally
        {
            _isSaving = false;
            StateHasChanged();
        }
    }

    /// <summary>Throws away every unsaved edit, restoring the cells to what the records hold.</summary>
    public Task DiscardAsync()
    {
        _session.Clear();
        _saveError = null;
        _editorError = null;
        _activeCell = null;
        _editorText = null;
        StateHasChanged();
        return Task.CompletedTask;
    }

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
    protected override void OnStateChanged()
    {
        // The row being edited can leave the page — the merchant changed the column it is sorted or
        // filtered by, or the page changed under it. An active cell that points at a row nobody can see
        // is an editor drawn nowhere with the draft still attached to it.
        if (_activeCell is { } active && State is not null && !State.Result.Items.Any(item => Equals(State.KeyOf(item), active.RowKey)))
        {
            _activeCell = null;
            _editorText = null;
            _editorError = null;
        }

        base.OnStateChanged();
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        if (!_focusPending)
        {
            return;
        }

        // Cleared before the await: a render caused by anything else while the focus call is in flight
        // must not focus a second time, or the caret jumps back to the editor the merchant just left.
        _focusPending = false;
        await Interop.FocusWithinAsync(_gridRoot, EditingCellSelector).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        // An editable grid renders its section inside a plain div it owns, and captures that div rather
        // than a cell. A cell comes and goes — its content is diffed from an editor back into text every
        // time the merchant leaves it — and a frame that a diff has to remove is the one place this
        // renderer throws ("Unexpected frame type during RemoveOldFrame: ElementReferenceCapture",
        // measured by the tests that press Escape and Tab). The div is the grid's outermost frame, so
        // nothing removes it while the grid is editable.
        if (Editable)
        {
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "data-polaris-grid-root", string.Empty);
            BuildSection(builder, 2);

            // After the content, which is where the Razor compiler puts one: every frame of a scope has
            // to be in non-decreasing sequence order, and the renderer does not check.
            builder.AddElementReferenceCapture(3, element => _gridRoot = element);
            builder.CloseElement();
            return;
        }

        BuildSection(builder, 0);
    }

    // The section's frames are numbered from a parameter because it is a sibling of the wrapper's
    // attribute in one case and the grid's own root in the other, and every frame of a scope has to be
    // in non-decreasing sequence order — the rule the whole editing path was broken by. The analyzer
    // wants literals; this is the one case where a literal is wrong.
#pragma warning disable ASP0006
    private void BuildSection(RenderTreeBuilder builder, int sequence)
    {
        // s-section padding="none" is Shopify's own guidance for wrapping a table: a section pads its
        // content, and a padded table has a gutter between its rules and the page.
        builder.OpenComponent<PolarisSection>(sequence);

        // A marker for tests and for a stylesheet, on the one element the grid owns outright. Nothing in
        // the library's CSS needs it, but a browser check has to be able to tell this table from the
        // plain `PolarisTable` in the component gallery — one has sorting and one does not, and asking
        // "does the first heading contain a button" is the measurement, not the way to find the grid.
        builder.AddAttribute(sequence + 1, nameof(PolarisSection.Padding), PolarisSectionPadding.None);
        builder.AddAttribute(sequence + 2, nameof(PolarisSection.AdditionalAttributes), new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["data-polaris-data-grid"] = true,
            ["data-polaris-grid-editable"] = Editable,
        });

        builder.AddAttribute(sequence + 3, nameof(PolarisSection.ChildContent), (RenderFragment)BuildSectionContent);
        builder.CloseComponent();
    }
#pragma warning restore ASP0006

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
        }
        else if (!string.IsNullOrEmpty(State.ErrorMessage))
        {
            BuildErrorContent(builder);
        }
        else if (State.Result.IsEmpty)
        {
            BuildEmptyContent(builder);
        }
        else
        {
            builder.OpenComponent<PolarisTable>(2);
            builder.AddAttribute(3, nameof(PolarisTable.ChildContent), (RenderFragment)BuildTable);
            builder.CloseComponent();
        }

        // Outside the branches on purpose: an edit made on one page is still unsaved when the merchant
        // moves to another, and the window onto saving it must not disappear with the rows.
        if (Editable)
        {
            BuildEditingFooter(builder);
        }
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
        if (!IsCellEditable(column))
        {
            // The read-only path, unchanged: no wrapper, no tabindex, no listener. A grid that is not
            // editable must render exactly the markup it always did, down to the whitespace.
            BuildValue(builder, column, item, column.ReadValue(item!), 0);
            return;
        }

        var rowKey = State.KeyOf(item);

        if (_activeCell is { } active && Equals(active.RowKey, rowKey) && string.Equals(active.ColumnId, column.Id, StringComparison.Ordinal))
        {
            BuildEditorCell(builder, column, rowKey);
            return;
        }

        BuildIdleCell(builder, column, item, rowKey);
    }

    /// <summary>Renders a cell's value: its template when it has one, otherwise its formatted text.</summary>
    /// <param name="builder">The render tree builder.</param>
    /// <param name="column">The column.</param>
    /// <param name="item">The row's record, which a cell template renders.</param>
    /// <param name="value">The value to show, which may be a draft's rather than the record's.</param>
    /// <param name="sequence">
    /// Where in the element's child frames this content goes. Every frame in one scope has to be in
    /// non-decreasing sequence order, and this content follows the cell's attributes.
    /// </param>
    private void BuildValue(RenderTreeBuilder builder, IPolarisGridColumn column, TItem item, object? value, int sequence)
    {
        var template = column is PolarisGridColumnDefinition<TItem> typed ? typed.CellTemplate : null;

        // Suppressed rather than worked around: a literal is right for a cell that renders its value
        // alone and wrong for the editable one, whose content follows attributes numbered above zero. The
        // two branches cannot be one expression — an object-typed ternary hands the fragment to
        // AddContent as text, and the cell renders "System.Func`2[…]" instead of the template.
#pragma warning disable ASP0006
        if (template is not null)
        {
            builder.AddContent(sequence, template(item));
            return;
        }

        builder.AddContent(sequence, column.FormatValue(value));
#pragma warning restore ASP0006
    }

    /// <summary>
    /// An editable cell that is not being edited: focusable, and a click enters it.
    /// </summary>
    /// <remarks>
    /// The wrapper exists because this is the only element in the grid that is not a Polaris element,
    /// and neither a click nor a keypress from inside a Polaris shadow root can be attributed to a
    /// column without one. It carries the column and the row in data attributes, which is also how the
    /// browser check addresses a cell.
    /// </remarks>
    private void BuildIdleCell(RenderTreeBuilder builder, IPolarisGridColumn column, TItem item, object rowKey)
    {
        builder.OpenElement(0, "div");

        // tabindex 0, not -1: the merchant reaches an editable cell by Tab, so the grid is keyboard
        // navigable without the click that a mouse user needs.
        builder.AddAttribute(1, "tabindex", "0");
        builder.AddAttribute(2, "data-polaris-grid-cell", column.Id);
        builder.AddAttribute(3, "data-polaris-grid-row", Convert.ToString(rowKey, CultureInfo.InvariantCulture));

        if (_session.TryGetCell(rowKey, column.Id, out _))
        {
            builder.AddAttribute(4, "data-polaris-grid-dirty", string.Empty);
        }

        // mousedown, not click: committing the cell the merchant was in re-renders the row, and a click
        // whose mousedown and mouseup landed on different elements never fires. Measured in a browser —
        // clicking straight from one edited cell into another silently did nothing.
        builder.AddAttribute(5, "onmousedown", EventCallback.Factory.Create<MouseEventArgs>(this, async _ => await BeginEditAsync(rowKey, column.Id)));
        builder.AddAttribute(6, "onkeydown", EventCallback.Factory.Create<KeyboardEventArgs>(this, async args => await OnIdleCellKeyDownAsync(rowKey, column.Id, args)));

        var value = ValueFor(column, item, rowKey);

        // After every attribute, and with a number above all of them: the frames of one scope must be in
        // non-decreasing sequence order, and content numbered below the attributes made the renderer's
        // diff fail outright rather than draw the cell.
        BuildValue(builder, column, item, value, 10);
        builder.CloseElement();
    }

    /// <summary>The cell being edited: its editor, and the keys that move out of it.</summary>
    private void BuildEditorCell(RenderTreeBuilder builder, IPolarisGridColumn column, object rowKey)
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "data-polaris-grid-cell", column.Id);
        builder.AddAttribute(2, "data-polaris-grid-row", Convert.ToString(rowKey, CultureInfo.InvariantCulture));
        builder.AddAttribute(3, "data-polaris-grid-editing", string.Empty);
        builder.AddAttribute(4, "onkeydown", EventCallback.Factory.Create<KeyboardEventArgs>(this, async args => await OnEditorKeyDownAsync(rowKey, column.Id, args)));

        // Every frame of one scope in sequence order, which the renderer's diff requires and does not
        // check: the editor is numbered at 10, above the attributes above it. With the editor numbered
        // before them, closing a cell threw "Unexpected frame type during RemoveOldFrame:
        // ElementReferenceCapture" and the grid stopped rendering altogether — measured by the component
        // tests that press Escape and Tab.
        switch (EditorTypeFor(column))
        {
            case PolarisGridEditorType.Boolean:
                BuildBooleanEditor(builder, column, rowKey);
                break;
            case PolarisGridEditorType.Number:
                BuildNumberEditor(builder, column, rowKey);
                break;
            case PolarisGridEditorType.Date:
                BuildDateEditor(builder, column, rowKey);
                break;
            case PolarisGridEditorType.Select:
                BuildSelectEditor(builder, column, rowKey);
                break;
            default:
                BuildTextEditor(builder, column, rowKey);
                break;
        }

        builder.CloseElement();
    }

    private void BuildTextEditor(RenderTreeBuilder builder, IPolarisGridColumn column, object rowKey)
    {
        builder.OpenComponent<PolarisTextField>(10);
        builder.AddAttribute(11, nameof(PolarisTextField.Label), column.Title);
        builder.AddAttribute(12, nameof(PolarisTextField.LabelAccessibilityVisibility), PolarisAccessibilityVisibility.Exclusive);
        builder.AddAttribute(13, nameof(PolarisTextField.Value), _editorText ?? string.Empty);
        builder.AddAttribute(14, nameof(PolarisTextField.Error), _editorError);
        builder.AddAttribute(15, nameof(PolarisTextField.OnInput), EventCallback.Factory.Create<PolarisEventArgs>(this, args => OnEditorInput(rowKey, column.Id, args)));
        builder.AddAttribute(16, nameof(PolarisTextField.OnChange), EventCallback.Factory.Create<PolarisEventArgs>(this, async args => await OnEditorChangedAsync(rowKey, column.Id, args.Value)));
        builder.CloseComponent();
    }

    private void BuildNumberEditor(RenderTreeBuilder builder, IPolarisGridColumn column, object rowKey)
    {
        builder.OpenComponent<PolarisNumberField>(10);
        builder.AddAttribute(11, nameof(PolarisNumberField.Label), column.Title);
        builder.AddAttribute(12, nameof(PolarisNumberField.LabelAccessibilityVisibility), PolarisAccessibilityVisibility.Exclusive);
        builder.AddAttribute(13, nameof(PolarisNumberField.Value), _editorText ?? string.Empty);
        builder.AddAttribute(14, nameof(PolarisNumberField.Error), _editorError);
        builder.AddAttribute(15, nameof(PolarisNumberField.OnInput), EventCallback.Factory.Create<PolarisEventArgs>(this, args => OnEditorInput(rowKey, column.Id, args)));
        builder.AddAttribute(16, nameof(PolarisNumberField.OnChange), EventCallback.Factory.Create<PolarisEventArgs>(this, async args => await OnEditorChangedAsync(rowKey, column.Id, args.Value)));
        builder.CloseComponent();
    }

    private void BuildDateEditor(RenderTreeBuilder builder, IPolarisGridColumn column, object rowKey)
    {
        builder.OpenComponent<PolarisDateField>(10);
        builder.AddAttribute(11, nameof(PolarisDateField.Label), column.Title);
        builder.AddAttribute(12, nameof(PolarisDateField.LabelAccessibilityVisibility), PolarisAccessibilityVisibility.Exclusive);
        builder.AddAttribute(13, nameof(PolarisDateField.Value), _editorText ?? string.Empty);
        builder.AddAttribute(14, nameof(PolarisDateField.Error), _editorError);
        builder.AddAttribute(15, nameof(PolarisDateField.OnInput), EventCallback.Factory.Create<PolarisEventArgs>(this, args => OnEditorInput(rowKey, column.Id, args)));
        builder.AddAttribute(16, nameof(PolarisDateField.OnChange), EventCallback.Factory.Create<PolarisEventArgs>(this, async args => await OnEditorChangedAsync(rowKey, column.Id, args.Value)));
        builder.CloseComponent();
    }

    private void BuildSelectEditor(RenderTreeBuilder builder, IPolarisGridColumn column, object rowKey)
    {
        builder.OpenComponent<PolarisSelect>(10);
        builder.AddAttribute(11, nameof(PolarisSelect.Label), column.Title);
        builder.AddAttribute(12, nameof(PolarisSelect.LabelAccessibilityVisibility), PolarisAccessibilityVisibility.Exclusive);
        builder.AddAttribute(13, nameof(PolarisSelect.Value), _editorText);
        builder.AddAttribute(14, nameof(PolarisSelect.Error), _editorError);
        builder.AddAttribute(15, nameof(PolarisSelect.OnChange), EventCallback.Factory.Create<PolarisEventArgs>(this, async args => await OnEditorChangedAsync(rowKey, column.Id, args.Value)));
        builder.AddAttribute(16, nameof(PolarisSelect.ChildContent), SelectOptions(column));
        builder.CloseComponent();
    }

    /// <summary>The options of a select editor, one <c>s-option</c> each.</summary>
    private RenderFragment SelectOptions(IPolarisGridColumn column) => builder =>
    {
        // The same literal sequence for every option: the list is positional, exactly like the rows
        // below, and a per-iteration sequence would make the diff look for frames that move.
        foreach (var option in EditorOptionsFor(column))
        {
            var value = EditorText(option) ?? string.Empty;
            var text = column.FormatValue(option);

            builder.OpenComponent<PolarisOption>(0);
            builder.AddAttribute(1, nameof(PolarisOption.Value), value);
            builder.AddAttribute(2, nameof(PolarisOption.Selected), string.Equals(value, _editorText, StringComparison.Ordinal));
            builder.AddAttribute(3, nameof(PolarisOption.ChildContent), (RenderFragment)(content => content.AddContent(0, text)));
            builder.CloseComponent();
        }
    };

    private void BuildBooleanEditor(RenderTreeBuilder builder, IPolarisGridColumn column, object rowKey)
    {
        builder.OpenComponent<PolarisCheckbox>(10);
        builder.AddAttribute(11, nameof(PolarisCheckbox.Label), column.Title);
        builder.AddAttribute(12, nameof(PolarisCheckbox.LabelAccessibilityVisibility), PolarisAccessibilityVisibility.Exclusive);
        builder.AddAttribute(13, nameof(PolarisCheckbox.Checked), EditorValueFor(rowKey, column) is true);
        builder.AddAttribute(14, nameof(PolarisCheckbox.Error), _editorError);
        builder.AddAttribute(15, nameof(PolarisCheckbox.OnChange), EventCallback.Factory.Create<PolarisEventArgs>(this, async args => await OnBooleanEditorChangedAsync(rowKey, column.Id, args)));
        builder.CloseComponent();
    }

    private void BuildEditingFooter(RenderTreeBuilder builder)
    {
        if (_saveError is not null)
        {
            builder.OpenComponent<PolarisBanner>(0);
            builder.AddAttribute(1, nameof(PolarisBanner.Heading), "The change could not be saved");
            builder.AddAttribute(2, nameof(PolarisBanner.Tone), PolarisTone.Critical);
            builder.AddAttribute(3, nameof(PolarisBanner.ChildContent), (RenderFragment)(content => content.AddContent(0, _saveError)));
            builder.CloseComponent();
        }

        if (SaveMode != PolarisGridSaveMode.Manual || _session.IsEmpty)
        {
            return;
        }

        builder.OpenElement(10, "div");
        builder.AddAttribute(11, "data-polaris-grid-save-bar", string.Empty);

        // The fallback is the value in force: Polaris defines its --p-space-* custom properties inside
        // its shadow roots, where they do not inherit out, so light-DOM content never sees them.
        builder.AddAttribute(12, "style", "padding:var(--p-space-300,12px) 0");

        builder.OpenComponent<PolarisStack>(13);
        builder.AddAttribute(14, nameof(PolarisStack.Direction), PolarisStackDirection.Inline);
        builder.AddAttribute(15, nameof(PolarisStack.Gap), PolarisGap.Base);
        builder.AddAttribute(16, nameof(PolarisStack.AlignItems), "center");
        builder.AddAttribute(17, nameof(PolarisStack.ChildContent), (RenderFragment)BuildSaveBarContent);
        builder.CloseComponent();

        builder.CloseElement();
    }

    private void BuildSaveBarContent(RenderTreeBuilder builder)
    {
        builder.OpenComponent<PolarisText>(0);
        builder.AddAttribute(1, nameof(PolarisText.Color), PolarisColor.Subdued);
        builder.AddAttribute(2, nameof(PolarisText.ChildContent), (RenderFragment)(text => text.AddContent(0, UnsavedText())));
        builder.CloseComponent();

        builder.OpenComponent<PolarisButton>(10);
        builder.AddAttribute(11, nameof(PolarisButton.Variant), PolarisButtonVariant.Primary);
        builder.AddAttribute(12, nameof(PolarisButton.Loading), _isSaving);
        builder.AddAttribute(13, nameof(PolarisButton.OnClick), EventCallback.Factory.Create<PolarisEventArgs>(this, async _ => await SaveAsync()));
        builder.AddAttribute(14, nameof(PolarisButton.ChildContent), (RenderFragment)(text => text.AddContent(0, "Save changes")));
        builder.CloseComponent();

        builder.OpenComponent<PolarisButton>(20);
        builder.AddAttribute(21, nameof(PolarisButton.OnClick), EventCallback.Factory.Create<PolarisEventArgs>(this, async _ => await DiscardAsync()));
        builder.AddAttribute(22, nameof(PolarisButton.ChildContent), (RenderFragment)(text => text.AddContent(0, "Discard")));
        builder.CloseComponent();
    }

    private string UnsavedText() => _session.RowCount == 1
        ? "1 row changed, not saved yet"
        : string.Create(CultureInfo.InvariantCulture, $"{_session.RowCount} rows changed, not saved yet");

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

    // ── Editing ──────────────────────────────────────────────────────────────────────────────

    /// <summary>True when this cell can be edited at all: the grid, the column, and a place to write.</summary>
    private bool IsCellEditable(IPolarisGridColumn column)
        => Editable && column.Editable && column is PolarisGridColumnDefinition<TItem> { CanWrite: true };

    private List<IPolarisGridColumn> EditableColumns()
        => [.. VisibleColumns().Where(IsCellEditable)];

    /// <summary>The editable cells of the visible page, in the order they are drawn.</summary>
    private List<(object RowKey, string ColumnId)> EditableCells()
    {
        var columns = EditableColumns();
        var cells = new List<(object RowKey, string ColumnId)>(State.Result.Items.Count * Math.Max(columns.Count, 1));

        foreach (var item in State.Result.Items)
        {
            var key = State.KeyOf(item);

            foreach (var column in columns)
            {
                cells.Add((key, column.Id));
            }
        }

        return cells;
    }

    /// <summary>The value a cell shows: its draft when it has one, otherwise the record's own.</summary>
    private object? ValueFor(IPolarisGridColumn column, TItem item, object rowKey)
        => _session.TryGetCell(rowKey, column.Id, out var draft) ? draft : column.ReadValue(item!);

    private object? EditorValueFor(object rowKey, IPolarisGridColumn column)
        => _session.TryGetCell(rowKey, column.Id, out var draft)
            ? draft
            : State.FindItem(rowKey) is { } item ? column.ReadValue(item!) : null;

    private bool IsActive(object rowKey, string columnId)
        => _activeCell is { } active
            && Equals(active.RowKey, rowKey)
            && string.Equals(active.ColumnId, columnId, StringComparison.Ordinal);

    /// <summary>
    /// Enters a cell: commits the cell that was being edited, then draws this cell's editor and takes
    /// the focus into it.
    /// </summary>
    private async Task BeginEditAsync(object rowKey, string columnId)
    {
        if (IsActive(rowKey, columnId))
        {
            return;
        }

        if (_activeCell is { } current && !await CommitCellAsync(current.RowKey, current.ColumnId, _editorText))
        {
            // The value in the cell being left is not valid. Switching away would throw away what the
            // merchant typed and the message explaining why it was refused.
            return;
        }

        Activate(rowKey, columnId);
    }

    private void Activate(object rowKey, string columnId)
    {
        var column = FindColumn(columnId);

        if (column is null || State.FindItem(rowKey) is not { } item)
        {
            CloseEditor();
            return;
        }

        _activeCell = (rowKey, columnId);
        _editorText = EditorText(ValueFor(column, item, rowKey));
        _editorError = null;
        _focusPending = true;
        StateHasChanged();
    }

    private void CloseEditor()
    {
        _activeCell = null;
        _editorText = null;
        _editorError = null;
        StateHasChanged();
    }

    /// <summary>
    /// Commits one cell's text. Returns false when the text is not a value for the column, in which case
    /// the editor stays open with the message and nothing is drafted.
    /// </summary>
    private async Task<bool> CommitCellAsync(object rowKey, string columnId, string? text)
    {
        var column = FindColumn(columnId);

        if (column is null || State.FindItem(rowKey) is not { } item)
        {
            return true;
        }

        if (!TryConvert(column, text, out var value, out var error))
        {
            _editorError = error;
            StateHasChanged();
            return false;
        }

        _editorError = null;
        _session.SetCell(rowKey, columnId, value, column.ValueGetter(item));

        if (SaveMode == PolarisGridSaveMode.Immediate)
        {
            await SaveAsync();
        }

        return true;
    }

    /// <summary>Commits the active cell and moves the editor through the editable cells.</summary>
    /// <param name="offset">
    /// How many cells to move in the flattened list of editable cells: 1 is the next column, the column
    /// count is the next row.
    /// </param>
    private async Task MoveAsync(int offset)
    {
        if (_activeCell is not { } active)
        {
            return;
        }

        if (!await CommitCellAsync(active.RowKey, active.ColumnId, _editorText))
        {
            return;
        }

        // Read after the commit: saving immediately can re-query, and moving through a list rebuilt from
        // the previous result would step to a cell the merchant can no longer see.
        var cells = EditableCells();
        var index = cells.FindIndex(cell => Equals(cell.RowKey, active.RowKey) && string.Equals(cell.ColumnId, active.ColumnId, StringComparison.Ordinal));
        var target = index + offset;

        if (index < 0 || target < 0 || target >= cells.Count)
        {
            // The edge of the grid. Closing rather than wrapping is the spreadsheet rule: Tab out of the
            // last cell leaves the table, which is also the only way a keyboard user gets past it.
            CloseEditor();
            return;
        }

        Activate(cells[target].RowKey, cells[target].ColumnId);
    }

    private async Task OnEditorKeyDownAsync(object rowKey, string columnId, KeyboardEventArgs args)
    {
        if (!IsActive(rowKey, columnId))
        {
            // A key event from a field that has already been left. Acting on it would commit the text of
            // one cell into another.
            return;
        }

        // Only four keys are intercepted. Everything else — an arrow key, a character, a shortcut — has
        // to reach the field: an arrow key in a text field moves the caret, and stealing it would move
        // the merchant out of the cell they are typing in.
        switch (args.Key)
        {
            case "Enter":
                await MoveAsync(EditableColumns().Count);
                break;
            case "Tab":
                await MoveAsync(args.ShiftKey ? -1 : 1);
                break;
            case "Escape":
                CancelCell(rowKey, columnId);
                break;
        }
    }

    private async Task OnIdleCellKeyDownAsync(object rowKey, string columnId, KeyboardEventArgs args)
    {
        if (args.Key is "Enter" or "F2")
        {
            await BeginEditAsync(rowKey, columnId);
            return;
        }

        if (args.Key is "Escape")
        {
            return;
        }

        // A printable character starts editing with that character, which is what makes direct typing
        // work in a spreadsheet. Length 1 rather than a list of characters: everything from a letter to
        // a digit to punctuation is one character long, and nothing that is a key name is.
        if (!args.CtrlKey && !args.MetaKey && !args.AltKey && args.Key.Length == 1)
        {
            await BeginEditAsync(rowKey, columnId);
            _editorText = args.Key;
            StateHasChanged();
        }
    }

    /// <summary>
    /// Drops the cell's draft and closes the editor — Escape.
    /// </summary>
    /// <remarks>
    /// The whole cell's draft goes, including one committed earlier in the same editing session: Escape
    /// means "not this", and leaving half a cell's value behind would make the second Escape of a pair
    /// do nothing.
    /// </remarks>
    private void CancelCell(object rowKey, string columnId)
    {
        _session.ClearCell(rowKey, columnId);
        CloseEditor();
    }

    private void OnEditorInput(object rowKey, string columnId, PolarisEventArgs args)
    {
        if (!IsActive(rowKey, columnId))
        {
            return;
        }

        // Deliberately no StateHasChanged: this runs on every keystroke, and re-rendering would write
        // the value back into the field on each one — which moves the caret to the end of the text.
        _editorText = args.Value;
    }

    /// <summary>The field reports a committed value: blur, Enter, or a choice. It does not move.</summary>
    private async Task OnEditorChangedAsync(object rowKey, string columnId, string? value)
    {
        if (!IsActive(rowKey, columnId))
        {
            return;
        }

        if (value is not null)
        {
            _editorText = value;
        }

        // A choice is the whole interaction for a select or a date, and Enter in a field also reports a
        // change a moment before the key handler runs: committing again with the same value is a no-op,
        // because a draft equal to the record is not a draft.
        await CommitCellAsync(rowKey, columnId, _editorText);
    }

    private async Task OnBooleanEditorChangedAsync(object rowKey, string columnId, PolarisEventArgs args)
    {
        if (!IsActive(rowKey, columnId))
        {
            return;
        }

        var column = FindColumn(columnId);

        if (column is null || State.FindItem(rowKey) is not { } item)
        {
            return;
        }

        // A tick commits itself. A checkbox whose value only took effect on Enter would be
        // indistinguishable from a broken one, and there is no partial state to type towards.
        _session.SetCell(rowKey, columnId, args.Checked ?? false, column.ValueGetter(item));
        _editorError = null;

        if (SaveMode == PolarisGridSaveMode.Immediate)
        {
            await SaveAsync();
        }

        StateHasChanged();
    }

    private async Task<PolarisGridSaveResult> InvokeSaveAsync(PolarisGridRowEdit<TItem> edit, CancellationToken cancellationToken)
    {
        try
        {
            return await SaveChanges!(edit, cancellationToken);
        }
        catch (Exception ex)
        {
            // A callback that throws is a caller that has not read the contract. Failing this row keeps
            // the merchant's draft and says why; letting it escape takes the whole circuit down.
            return PolarisGridSaveResult.Failure(ex.Message);
        }
    }

    /// <summary>Applies one row's changes to its record, through the columns that declared them.</summary>
    private TItem ApplyChanges(PolarisGridRowEdit<TItem> edit)
    {
        var updated = edit.Original;

        foreach (var (columnId, value) in edit.Changes)
        {
            if (FindColumn(columnId) is { CanWrite: true } column)
            {
                updated = column.ApplyValue(updated, value);
            }
        }

        return updated;
    }

    private PolarisGridColumnDefinition<TItem>? FindColumn(string columnId)
        => State.ColumnDefinitions.FirstOrDefault(column => string.Equals(column.Id, columnId, StringComparison.Ordinal));

    private PolarisGridEditorType EditorTypeFor(IPolarisGridColumn column)
    {
        if (column.EditorType != PolarisGridEditorType.Auto)
        {
            return column.EditorType;
        }

        var type = Nullable.GetUnderlyingType(column.ValueType) ?? column.ValueType;

        if (type == typeof(DateOnly) || type == typeof(DateTime))
        {
            return PolarisGridEditorType.Date;
        }

        if (type.IsEnum)
        {
            return PolarisGridEditorType.Select;
        }

        return Type.GetTypeCode(type) switch
        {
            TypeCode.Boolean => PolarisGridEditorType.Boolean,
            TypeCode.Byte or TypeCode.SByte or TypeCode.Int16 or TypeCode.UInt16
                or TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64
                or TypeCode.Single or TypeCode.Double or TypeCode.Decimal => PolarisGridEditorType.Number,
            TypeCode.DateTime => PolarisGridEditorType.Date,
            _ => PolarisGridEditorType.Text,
        };
    }

    /// <summary>
    /// The choices a select editor offers: the column's own, else an enum's values, else the distinct
    /// values of the dataset.
    /// </summary>
    private IReadOnlyList<object?> EditorOptionsFor(IPolarisGridColumn column)
    {
        if (column.EditorOptions is { Count: > 0 } declared)
        {
            return declared;
        }

        var type = Nullable.GetUnderlyingType(column.ValueType) ?? column.ValueType;

        if (type.IsEnum)
        {
            return [.. Enum.GetValues(type).Cast<object?>()];
        }

        return State.GetDistinctValues(column.Id);
    }

    /// <summary>
    /// Converts what the merchant typed into the column's own type, or explains why it cannot be.
    /// </summary>
    /// <remarks>
    /// The parse is the same one the filter engine uses, so a grid and its filter agree about what a
    /// number is, and an emptied field becomes a null only where the column can hold one — writing null
    /// into an <c>int</c> property throws inside the setter, which reaches the merchant as a stack
    /// trace instead of a message.
    /// </remarks>
    private static bool TryConvert(IPolarisGridColumn column, string? text, out object? value, out string? error)
    {
        value = null;
        error = null;

        if (string.IsNullOrEmpty(text))
        {
            if (column.ValueType == typeof(string))
            {
                value = string.Empty;
                return true;
            }

            if (Nullable.GetUnderlyingType(column.ValueType) is null && column.ValueType.IsValueType)
            {
                error = $"{column.Title} needs a value.";
                return false;
            }

            return true;
        }

        if (!PolarisGridValues.TryCoerce(text, column.ValueType, out value))
        {
            value = null;
            error = $"'{text}' is not {Describe(column.ValueType)}.";
            return false;
        }

        return true;
    }

    private static string Describe(Type type)
    {
        var target = Nullable.GetUnderlyingType(type) ?? type;

        if (target == typeof(bool))
        {
            return "true or false";
        }

        if (target == typeof(DateOnly) || target == typeof(DateTime) || target == typeof(TimeOnly))
        {
            return "a date";
        }

        if (target == typeof(string))
        {
            return "text";
        }

        if (target.IsEnum)
        {
            return string.Concat("one of: ", string.Join(", ", Enum.GetNames(target)));
        }

        return Type.GetTypeCode(target) switch
        {
            TypeCode.Char or TypeCode.String => "text",
            TypeCode.Byte or TypeCode.SByte or TypeCode.Int16 or TypeCode.UInt16
                or TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64 => "a whole number",
            _ => "a number",
        };
    }

    /// <summary>
    /// The text an editor starts with: the value as it would be typed back in, not as it is displayed.
    /// </summary>
    /// <remarks>
    /// A price formatted <c>C2</c> for the table reads "£1,234.50", and seeding the editor with that
    /// would put a currency symbol and a thousands separator in front of a merchant who then has to
    /// delete them. The editor gets the invariant, unformatted value instead.
    /// </remarks>
    private static string? EditorText(object? value) => value switch
    {
        null => null,
        string text => text,
        DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateTime moment => moment.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        TimeOnly time => time.ToString("HH:mm", CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString(),
    };
}
