using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Polaris.Blazor.DataGrid;
using Polaris.Blazor.Events;

namespace Polaris.Blazor.Components;

/// <summary>
/// The filter bar for a <see cref="PolarisDataGrid{TItem}"/>: a search box, a panel of per-column
/// controls, and a chip for every filter that is applied.
/// </summary>
/// <remarks>
/// <para>
/// The controls are generated from the columns the grid registered, so a column that declares a
/// filter type gets a filter without anything else being wired up. It never touches the collection:
/// it writes a <see cref="PolarisGridFilterDescriptor"/> into the shared state and the state re-runs
/// the query, which is what lets this component be moved, replaced or left out.
/// </para>
/// <para>
/// The panel is an <c>s-popover</c> opened by the button's <c>commandFor</c> — declaratively, with no
/// JavaScript of this library's own. A dozen inline controls would push a twelve-column table off the
/// screen, and the admin's own list screens put filtering behind a button for the same reason.
/// </para>
/// <para>
/// <see cref="PolarisDataFilterPresentation.Panel"/> drops the bar and draws those same controls in
/// place, with the chips and Clear all above them, for a screen that has given the filter a column of
/// its own — a sidebar beside a grid of cards. Nothing else changes: the controls, the chips, the
/// apply modes and the state are one implementation.
/// </para>
/// <para>
/// The chip row is the other half of the same idea: whatever is narrowing the result is visible
/// without opening the panel, and removable in one click. A filter the merchant cannot see is a
/// filter they will report as missing data.
/// </para>
/// <para>
/// A full-range slider clears its own filter rather than sending a criterion that matches everything,
/// because a filter that cannot exclude anything should not make the grid recompute — and it should
/// not leave a chip behind claiming the result is narrowed.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;PolarisDataFilter State="@State" ApplyMode="PolarisGridFilterApplyMode.Debounced" /&gt;
/// </code>
/// </remarks>
/// <example>
/// <code>
/// &lt;PolarisDataFilter State="@State" Presentation="PolarisDataFilterPresentation.Panel"&gt;
///     &lt;Columns&gt;
///         &lt;PolarisGridColumn TItem="CatalogueRow" Field="row =&gt; row.Title"
///                            Title="Product" FilterType="PolarisGridFilterType.Text" /&gt;
///     &lt;/Columns&gt;
/// &lt;/PolarisDataFilter&gt;
/// </code>
/// </example>
public sealed class PolarisDataFilter : PolarisGridComponentBase, IPolarisGridColumnHost
{
    private static int _instanceCount;

    private readonly string _panelId = string.Create(
        CultureInfo.InvariantCulture,
        $"polaris-data-filter-{Interlocked.Increment(ref _instanceCount)}");

    private readonly Dictionary<string, string> _textEchoes = new(StringComparer.Ordinal);
    private readonly HashSet<string> _pendingFields = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PolarisGridFilterDescriptor?> _drafts = new(StringComparer.Ordinal);

    private CancellationTokenSource? _debounce;
    private string? _searchEcho;
    private bool _searchPending;
    private string _appliedSignature = string.Empty;

    /// <summary>The shared state. Required, and the same instance the grid was given.</summary>
    [Parameter, EditorRequired]
    public PolarisGridState State { get; set; } = default!;

    /// <summary>When a change reaches the query.</summary>
    [Parameter] public PolarisGridFilterApplyMode ApplyMode { get; set; } = PolarisGridFilterApplyMode.Debounced;

    /// <summary>How long typing must pause before a debounced change applies.</summary>
    [Parameter] public int DebounceMilliseconds { get; set; } = 300;

    /// <summary>Placeholder for the search box.</summary>
    [Parameter] public string? SearchPlaceholder { get; set; } = "Search";

    /// <summary>Shows a chip for each applied filter.</summary>
    [Parameter] public bool ShowActiveFilters { get; set; } = true;

    /// <summary>Shows a button that clears every filter and the search at once.</summary>
    [Parameter] public bool ShowClearAll { get; set; } = true;

    /// <summary>Shows the search box, which filters every searchable column at once.</summary>
    [Parameter] public bool ShowSearch { get; set; } = true;

    /// <summary>Extra controls shown at the end of the bar, such as an export button.</summary>
    [Parameter] public RenderFragment? Actions { get; set; }

    /// <summary>
    /// Where the controls are drawn: behind the Filter button, as a bar, or in place as a panel.
    /// </summary>
    /// <remarks>
    /// <see cref="PolarisDataFilterPresentation.Bar"/> is the default, so every screen written before
    /// this parameter existed keeps the filter it had. <see
    /// cref="PolarisDataFilterPresentation.Panel"/> is for a screen that has made room by placing this
    /// component in a column of its own: the same controls, the chips and Clear all above them, and no
    /// search box, button or popover.
    /// </remarks>
    [Parameter] public PolarisDataFilterPresentation Presentation { get; set; }

    /// <summary>
    /// Column declarations, for a screen whose rows are drawn as something other than a table.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A grid registers the columns declared in its own <c>Columns</c> fragment. A screen that draws
    /// cards, tiles or a gallery has no grid, so it declares them here instead and this component is
    /// the host they register with — the controls are generated from them exactly as they would be
    /// from a table's.
    /// </para>
    /// <para>
    /// The declarations are rendered before the controls are read, and never removed, so a column
    /// stays registered while the result is empty.
    /// </para>
    /// </remarks>
    [Parameter] public RenderFragment? Columns { get; set; }

    /// <inheritdoc />
    protected override PolarisGridState? BoundState => State;

    /// <summary>The state a column declared inside this component registers with.</summary>
    PolarisGridState IPolarisGridColumnHost.State => State;

    /// <summary>Redraws after a column's declaration changed in place.</summary>
    void IPolarisGridColumnHost.NotifyColumnChanged()
    {
        if (State is not null)
        {
            _ = InvokeAsync(StateHasChanged);
        }
    }

    /// <summary>
    /// Applies a search term as if the merchant had typed it, honouring <see cref="ApplyMode"/>.
    /// </summary>
    /// <param name="term">The term. Null or whitespace clears the search.</param>
    public Task SearchAsync(string? term)
    {
        _searchEcho = term;

        switch (ApplyMode)
        {
            case PolarisGridFilterApplyMode.Manual:
                // Manual mode only records the intent; nothing reaches the query until Apply.
                _drafts[SearchDraftKey] = string.IsNullOrWhiteSpace(term)
                    ? null
                    : new PolarisGridFilterDescriptor(SearchDraftKey, PolarisGridOperator.Contains, term);
                return Task.CompletedTask;

            case PolarisGridFilterApplyMode.Debounced:
                _searchPending = true;
                Schedule(
                    apply: () =>
                    {
                        _searchPending = false;
                        State.SetGlobalSearch(_searchEcho);
                    },
                    onCancel: () => _searchPending = false);
                return Task.CompletedTask;

            default:
                _searchPending = false;
                State.SetGlobalSearch(term);
                return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Applies or clears the filter for one column as if the merchant had used its control.
    /// </summary>
    /// <param name="field">The column id.</param>
    /// <param name="filter">The filter, or <c>null</c> to clear the column's filter.</param>
    public Task SetFilterAsync(string field, PolarisGridFilterDescriptor? filter)
    {
        ArgumentException.ThrowIfNullOrEmpty(field);

        switch (ApplyMode)
        {
            case PolarisGridFilterApplyMode.Manual:
                _drafts[field] = filter;
                return Task.CompletedTask;

            case PolarisGridFilterApplyMode.Debounced:
                _pendingFields.Add(field);
                Schedule(
                    () => ApplyFilter(field, filter),
                    () => _pendingFields.Remove(field));
                return Task.CompletedTask;

            default:
                ApplyFilter(field, filter);
                return Task.CompletedTask;
        }
    }

    /// <summary>Clears every column filter and the search, whatever the apply mode.</summary>
    public Task ClearAllAsync()
    {
        _drafts.Clear();
        _textEchoes.Clear();
        _searchEcho = null;
        _pendingFields.Clear();
        _searchPending = false;
        _appliedSignature = string.Empty;

        State.ClearAllFilters();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        base.OnParametersSet();

        if (State is null)
        {
            return;
        }

        // Manual mode edits drafts, so they have to start from what is applied — including after an
        // Apply, which is the moment the two agree again.
        if (ApplyMode == PolarisGridFilterApplyMode.Manual && _appliedSignature != Signature(State))
        {
            RebuildDrafts();
        }
    }

    /// <inheritdoc />
    protected override void OnStateChanged()
    {
        // The state is authoritative for anything this component is not in the middle of changing: a
        // "clear all" from the page, a restored view, or a chip removed elsewhere. A pending change is
        // skipped, or the echo would be overwritten mid-keystroke and the cursor would jump.
        if (State is not null)
        {
            if (!_searchPending && !IsDebouncing(SearchDraftKey))
            {
                _searchEcho = State.Query.GlobalSearch;
            }

            foreach (var column in State.Columns)
            {
                if (_pendingFields.Contains(column.Id))
                {
                    continue;
                }

                var filter = State.Query.FilterFor(column.Id);

                if (filter is null)
                {
                    _textEchoes.Remove(column.Id);
                }
                else
                {
                    _textEchoes[column.Id] = Convert.ToString(filter.Value, CultureInfo.InvariantCulture) ?? string.Empty;
                }
            }
        }

        base.OnStateChanged();
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        _debounce?.Cancel();
        _debounce?.Dispose();
        _debounce = null;

        base.Dispose();
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        if (State is null)
        {
            return;
        }

        // The declarations first, in either presentation: a column registers with the state as it
        // mounts, and every control below is generated from what is registered. A screen with no table
        // has nowhere else to declare them, and rendering them here is what gives it the same filter.
        if (Columns is not null)
        {
            builder.OpenComponent<CascadingValue<IPolarisGridColumnHost>>(0);
            builder.AddAttribute(1, nameof(CascadingValue<IPolarisGridColumnHost>.Value), this);
            builder.AddAttribute(2, nameof(CascadingValue<IPolarisGridColumnHost>.IsFixed), true);
            builder.AddAttribute(3, nameof(CascadingValue<IPolarisGridColumnHost>.ChildContent), Columns);
            builder.CloseComponent();
        }

        var filterable = FilterableColumns();

        if (Presentation == PolarisDataFilterPresentation.Panel)
        {
            builder.OpenComponent<PolarisStack>(10);
            builder.AddAttribute(11, nameof(PolarisStack.Gap), PolarisGap.Base);
            builder.AddAttribute(12, nameof(PolarisStack.ChildContent), (RenderFragment)(panel =>
            {
                BuildChipRow(panel, 0);
                BuildControls(panel, filterable, panelId: null);
            }));
            builder.CloseComponent();

            return;
        }

        builder.OpenComponent<PolarisStack>(20);
        builder.AddAttribute(21, nameof(PolarisStack.Direction), PolarisStackDirection.Inline);
        builder.AddAttribute(22, nameof(PolarisStack.Gap), PolarisGap.Small);
        builder.AddAttribute(23, nameof(PolarisStack.AlignItems), "center");
        builder.AddAttribute(24, nameof(PolarisStack.JustifyContent), "space-between");
        builder.AddAttribute(25, nameof(PolarisStack.ChildContent), (RenderFragment)(content =>
        {
            content.OpenComponent<PolarisStack>(0);
            content.AddAttribute(1, nameof(PolarisStack.Direction), PolarisStackDirection.Inline);
            content.AddAttribute(2, nameof(PolarisStack.Gap), PolarisGap.Small);
            content.AddAttribute(3, nameof(PolarisStack.AlignItems), "center");
            content.AddAttribute(4, nameof(PolarisStack.ChildContent), (RenderFragment)(bar =>
            {
                BuildSearch(bar);
                BuildFilterButton(bar, filterable);
                BuildChips(bar);
                BuildClearAll(bar, 30);
            }));
            content.CloseComponent();

            if (Actions is not null)
            {
                content.OpenComponent<PolarisStack>(10);
                content.AddAttribute(11, nameof(PolarisStack.Direction), PolarisStackDirection.Inline);
                content.AddAttribute(12, nameof(PolarisStack.Gap), PolarisGap.Small);
                content.AddAttribute(13, nameof(PolarisStack.ChildContent), Actions);
                content.CloseComponent();
            }

            BuildPanel(content, filterable);
        }));
        builder.CloseComponent();
    }

    /// <summary>
    /// The applied chips and Clear all, above the controls of a panel — or nothing at all, so a panel
    /// with nothing applied is its controls and no empty row.
    /// </summary>
    /// <remarks>
    /// The chips are a wrapping grid rather than the bar's inline stack: measured, an inline Polaris
    /// stack does not wrap, so three chips in a sidebar column would run off its edge with no way to
    /// reach the third. The Clear all button sits on its own line under them, because a wrapping grid
    /// cannot hold one item that stays at the end.
    /// </remarks>
    private void BuildChipRow(RenderTreeBuilder builder, int sequence)
    {
        var hasChips = ShowActiveFilters && ActiveFilters().Count > 0;
        var hasClearAll = ShowClearAll && State.Query.HasAnyFilter;

        if (!hasChips && !hasClearAll)
        {
            return;
        }

        builder.OpenComponent<PolarisStack>(sequence);
        builder.AddAttribute(sequence + 1, nameof(PolarisStack.Gap), PolarisGap.Small);
        builder.AddAttribute(sequence + 2, nameof(PolarisStack.ChildContent), (RenderFragment)(row =>
        {
            if (hasChips)
            {
                row.OpenComponent<PolarisGrid>(0);
                row.AddAttribute(1, nameof(PolarisGrid.GridTemplateColumns), ChipsGridColumns);

                // Polaris's own small step, written as the string the grid passes through: its gap
                // parameters are CSS strings, not the stack's enum.
                row.AddAttribute(2, nameof(PolarisGrid.Gap), "small");
                row.AddAttribute(3, nameof(PolarisGrid.ChildContent), (RenderFragment)(grid =>
                {
                    foreach (var (column, filter) in ActiveFilters())
                    {
                        BuildChip(grid, column, filter);
                    }
                }));
                row.CloseComponent();
            }

            if (hasClearAll)
            {
                row.OpenComponent<PolarisStack>(10);
                row.AddAttribute(11, nameof(PolarisStack.Direction), PolarisStackDirection.Inline);
                row.AddAttribute(12, nameof(PolarisStack.Gap), PolarisGap.Small);
                row.AddAttribute(13, nameof(PolarisStack.ChildContent), (RenderFragment)(line => BuildClearAll(line, 0)));
                row.CloseComponent();
            }
        }));
        builder.CloseComponent();
    }

    private void BuildSearch(RenderTreeBuilder builder)
    {
        if (!ShowSearch)
        {
            return;
        }

        builder.OpenComponent<PolarisSearchField>(0);
        builder.AddAttribute(1, nameof(PolarisSearchField.Label), "Search");
        builder.AddAttribute(2, nameof(PolarisSearchField.LabelAccessibilityVisibility), PolarisAccessibilityVisibility.Exclusive);
        builder.AddAttribute(3, nameof(PolarisSearchField.Placeholder), SearchPlaceholder);
        builder.AddAttribute(4, nameof(PolarisSearchField.Value), _searchEcho ?? string.Empty);
        builder.AddAttribute(5, nameof(PolarisSearchField.OnInput), EventCallback.Factory.Create<PolarisEventArgs>(this, args => SearchAsync(args.Value)));
        builder.AddAttribute(6, nameof(PolarisSearchField.OnChange), EventCallback.Factory.Create<PolarisEventArgs>(this, args => SearchAsync(args.Value)));
        builder.CloseComponent();
    }

    private void BuildFilterButton(RenderTreeBuilder builder, IReadOnlyList<IPolarisGridColumn> filterable)
    {
        if (filterable.Count == 0)
        {
            return;
        }

        var applied = ActiveFilters().Count;

        builder.OpenComponent<PolarisButton>(10);
        builder.AddAttribute(11, nameof(PolarisButton.CommandFor), _panelId);
        builder.AddAttribute(12, nameof(PolarisButton.Command), PolarisCommand.Show);
        builder.AddAttribute(13, nameof(PolarisButton.Variant), applied > 0 ? PolarisButtonVariant.Secondary : PolarisButtonVariant.Auto);
        builder.AddAttribute(14, nameof(PolarisButton.ChildContent), (RenderFragment)(text => text.AddContent(
            0,
            applied > 0
                ? string.Create(CultureInfo.InvariantCulture, $"Filters ({applied})")
                : "Filter")));
        builder.CloseComponent();
    }

    private void BuildChips(RenderTreeBuilder builder)
    {
        if (!ShowActiveFilters)
        {
            return;
        }

        foreach (var (column, filter) in ActiveFilters())
        {
            BuildChip(builder, column, filter);
        }
    }

    private void BuildChip(
        RenderTreeBuilder builder,
        IPolarisGridColumn column,
        PolarisGridFilterDescriptor filter)
    {
        builder.OpenComponent<PolarisClickableChip>(20);
        builder.AddAttribute(21, nameof(PolarisClickableChip.Removable), true);
        builder.AddAttribute(22, nameof(PolarisClickableChip.Color), PolarisColor.Strong);
        builder.AddAttribute(23, nameof(PolarisClickableChip.OnRemove), EventCallback.Factory.Create<PolarisEventArgs>(this, _ => RemoveFilterAsync(column.Id)));
        builder.AddAttribute(24, nameof(PolarisClickableChip.ChildContent), (RenderFragment)(text => text.AddContent(0, Describe(column, filter))));
        builder.CloseComponent();
    }

    private void BuildClearAll(RenderTreeBuilder builder, int sequence)
    {
        if (!ShowClearAll || !State.Query.HasAnyFilter)
        {
            return;
        }

        builder.OpenComponent<PolarisButton>(sequence);
        builder.AddAttribute(sequence + 1, nameof(PolarisButton.Variant), PolarisButtonVariant.Tertiary);
        builder.AddAttribute(sequence + 2, nameof(PolarisButton.OnClick), EventCallback.Factory.Create<PolarisEventArgs>(this, _ => ClearAllAsync()));
        builder.AddAttribute(sequence + 3, nameof(PolarisButton.ChildContent), (RenderFragment)(text => text.AddContent(0, "Clear all")));
        builder.CloseComponent();
    }

    private void BuildPanel(RenderTreeBuilder builder, IReadOnlyList<IPolarisGridColumn> filterable)
    {
        if (filterable.Count == 0)
        {
            return;
        }

        builder.OpenComponent<PolarisPopover>(40);
        builder.AddAttribute(41, nameof(PolarisPopover.Id), _panelId);

        // The panel's size is stated rather than derived, and both halves of that were measured in a
        // browser against real data:
        //
        //   - with no inline size the slotted content lays out at zero width, so a slider inside it has
        //     a 0px track that cannot be dragged and a text field is a sliver;
        //   - with a block-size cap Polaris *clips* the content and does not scroll it, so with four
        //     sliders and a date range the lower controls were simply unreachable — painted below the
        //     panel's edge, in space the panel does not occupy.
        //
        // Hence a generous cap here and a scroll box of our own inside it: the frame is Polaris's, the
        // scrolling is a plain div that cannot be restyled out from under us.
        builder.AddAttribute(42, nameof(PolarisPopover.MaxBlockSize), "70vh");
        builder.AddAttribute(43, nameof(PolarisPopover.InlineSize), "320px");
        builder.AddAttribute(44, nameof(PolarisPopover.ChildContent), (RenderFragment)(panel =>
        {
            panel.OpenElement(0, "div");
            panel.AddAttribute(1, "data-polaris-filter-panel", true);

            // A pixel height, not a viewport one: the cap belongs to Polaris, and the content has to be
            // *smaller* than it for the scroll box to be inside the visible panel at all. Measured: with
            // this box taller than the panel, everything below the panel's edge was clipped away with no
            // way to reach it.
            //
            // The padding is stated here because an s-popover paints none of its own: measured in a
            // browser, its frame (the `div.popover` inside the shadow root) computes to 0px padding on
            // every side and has the same rect as this div, so the slotted controls sat flush against
            // the panel's edge — a text field's own border touching the popover's border, with nothing
            // in the markup to say so. 16px is Polaris's `space-400` step, the inset its own panels use;
            // the token is read first so an app that themes Polaris moves this with everything else, and
            // the fallback is what applies today, because the runtime defines these properties inside
            // its shadow roots and not on :root (also measured).
            //
            // box-sizing keeps the cap a cap: the padding counts inside the 300px, so the scroll box can
            // never grow taller than the frame that clips it.
            panel.AddAttribute(2, "style", "box-sizing:border-box;max-block-size:300px;overflow-y:auto;padding:var(--p-space-400,16px)");

            panel.OpenComponent<PolarisStack>(3);
            panel.AddAttribute(4, nameof(PolarisStack.Gap), PolarisGap.Base);
            panel.AddAttribute(5, nameof(PolarisStack.ChildContent), (RenderFragment)(content => BuildControls(content, filterable, _panelId)));
            panel.CloseComponent();
            panel.CloseElement();
        }));
        builder.CloseComponent();
    }

    /// <summary>
    /// One control per filterable column, in <c>FilterIndex</c> order, plus Apply and Cancel under
    /// <see cref="PolarisGridFilterApplyMode.Manual"/>.
    /// </summary>
    /// <param name="builder">The render tree.</param>
    /// <param name="filterable">The columns to draw controls for.</param>
    /// <param name="panelId">
    /// The popover these buttons belong to, or <c>null</c> when the controls are drawn in place.
    /// </param>
    /// <remarks>
    /// Shared by the popover and by <see cref="PolarisDataFilterPresentation.Panel"/>, so the two
    /// cannot drift: a filter type added here appears in both, and Manual mode works in both. The
    /// buttons close the popover only when there is one — a <c>commandFor</c> naming an element that
    /// does not exist is a button that quietly sends nothing.
    /// </remarks>
    private void BuildControls(
        RenderTreeBuilder builder,
        IReadOnlyList<IPolarisGridColumn> filterable,
        string? panelId)
    {
        foreach (var column in filterable)
        {
            BuildControl(builder, column);
        }

        if (ApplyMode != PolarisGridFilterApplyMode.Manual)
        {
            return;
        }

        builder.OpenComponent<PolarisStack>(90);
        builder.AddAttribute(91, nameof(PolarisStack.Direction), PolarisStackDirection.Inline);
        builder.AddAttribute(92, nameof(PolarisStack.Gap), PolarisGap.Small);
        builder.AddAttribute(93, nameof(PolarisStack.ChildContent), (RenderFragment)(buttons =>
        {
            BuildActionButton(
                buttons,
                0,
                PolarisButtonVariant.Primary,
                "Apply",
                panelId,
                EventCallback.Factory.Create<PolarisEventArgs>(this, _ => ApplyDraftsAsync()));

            BuildActionButton(
                buttons,
                10,
                PolarisButtonVariant.Auto,
                "Cancel",
                panelId,
                EventCallback.Factory.Create<PolarisEventArgs>(this, _ => ClearAllAsync()));
        }));
        builder.CloseComponent();
    }

    /// <summary>
    /// One of the panel's two buttons, whose declarative close is added only when there is a popover
    /// to close — a <c>commandFor</c> naming an element that does not exist is a button that quietly
    /// sends nothing.
    /// </summary>
    private static void BuildActionButton(
        RenderTreeBuilder builder,
        int sequence,
        PolarisButtonVariant variant,
        string text,
        string? panelId,
        EventCallback<PolarisEventArgs> onClick)
    {
        builder.OpenComponent<PolarisButton>(sequence);

        if (variant != PolarisButtonVariant.Auto)
        {
            builder.AddAttribute(sequence + 1, nameof(PolarisButton.Variant), variant);
        }

        if (panelId is not null)
        {
            builder.AddAttribute(sequence + 2, nameof(PolarisButton.CommandFor), panelId);
            builder.AddAttribute(sequence + 3, nameof(PolarisButton.Command), PolarisCommand.Hide);
        }

        builder.AddAttribute(sequence + 4, nameof(PolarisButton.OnClick), onClick);
        builder.AddAttribute(sequence + 5, nameof(PolarisButton.ChildContent), (RenderFragment)(content => content.AddContent(0, text)));
        builder.CloseComponent();
    }

    private void BuildControl(RenderTreeBuilder builder, IPolarisGridColumn column)
    {
        switch (column.FilterType)
        {
            case PolarisGridFilterType.Text:
                BuildTextControl(builder, column);
                break;

            case PolarisGridFilterType.MultiSelect:
                BuildChoiceControl(builder, column);
                break;

            case PolarisGridFilterType.Boolean:
                BuildBooleanControl(builder, column);
                break;

            case PolarisGridFilterType.Range:
                BuildRangeControl(builder, column);
                break;

            case PolarisGridFilterType.DateRange:
                BuildDateRangeControl(builder, column);
                break;

            case PolarisGridFilterType.Custom:
                if (column.FilterTemplate is not null)
                {
                    builder.AddContent(0, column.FilterTemplate);
                }

                break;
        }
    }

    private void BuildTextControl(RenderTreeBuilder builder, IPolarisGridColumn column)
    {
        var echo = _textEchoes.TryGetValue(column.Id, out var text) ? text : string.Empty;

        builder.OpenComponent<PolarisTextField>(0);
        builder.AddAttribute(1, nameof(PolarisTextField.Label), column.Title);
        builder.AddAttribute(2, nameof(PolarisTextField.Value), echo);
        builder.AddAttribute(3, nameof(PolarisTextField.OnInput), EventCallback.Factory.Create<PolarisEventArgs>(
            this,
            args => SetFilterAsync(column.Id, PolarisGridFilterDescriptor.Text(column.Id, args.Value))));
        builder.AddAttribute(4, nameof(PolarisTextField.OnChange), EventCallback.Factory.Create<PolarisEventArgs>(
            this,
            args => SetFilterAsync(column.Id, PolarisGridFilterDescriptor.Text(column.Id, args.Value))));
        builder.CloseComponent();
    }

    private void BuildChoiceControl(RenderTreeBuilder builder, IPolarisGridColumn column)
    {
        var options = column.FilterOptions ?? State.GetDistinctValues(column.Id);
        var selected = SelectedValues(column.Id);
        var columnId = column.Id;

        builder.OpenComponent<PolarisChoiceList>(0);
        builder.AddAttribute(1, nameof(PolarisChoiceList.Label), column.Title);
        builder.AddAttribute(2, nameof(PolarisChoiceList.Multiple), true);

        // Polaris warns — in the console, on every render — when a choice list has no name, and an
        // unnamed group is not a form control either. The column id is a name, and a stable one.
        builder.AddAttribute(3, nameof(PolarisChoiceList.Name), column.Id);
        builder.AddAttribute(4, nameof(PolarisChoiceList.Values), selected);
        builder.AddAttribute(5, nameof(PolarisChoiceList.OnChange), EventCallback.Factory.Create<PolarisEventArgs>(
            this,
            args => SetFilterAsync(columnId, PolarisGridFilterDescriptor.In(columnId, args.Values ?? []))));
        builder.AddAttribute(6, nameof(PolarisChoiceList.ChildContent), (RenderFragment)(list =>
        {
            foreach (var option in options)
            {
                var value = Convert.ToString(option, CultureInfo.InvariantCulture) ?? string.Empty;

                list.OpenComponent<PolarisChoice>(0);
                list.AddAttribute(1, nameof(PolarisChoice.Value), value);
                list.AddAttribute(2, nameof(PolarisChoice.ChildContent), (RenderFragment)(label => label.AddContent(0, column.FormatValue(option))));
                list.CloseComponent();
            }
        }));
        builder.CloseComponent();
    }

    private void BuildBooleanControl(RenderTreeBuilder builder, IPolarisGridColumn column)
    {
        // Read back as a bool OR as the string a previous control wrote. The descriptor carries a real
        // boolean, but a filter seeded by a page — `Equals("InShopify", true)` — and one set through the
        // control must describe the same thing, or the select shows no selection for a filter that is
        // plainly applied.
        var current = State.Query.FilterFor(column.Id)?.Value;
        var value = current switch
        {
            bool flag => flag ? "true" : "false",
            null => AnyOption,
            _ => Convert.ToString(current, CultureInfo.InvariantCulture) ?? AnyOption,
        };

        var columnId = column.Id;

        builder.OpenComponent<PolarisSelect>(0);
        builder.AddAttribute(1, nameof(PolarisSelect.Label), column.Title);
        builder.AddAttribute(2, nameof(PolarisSelect.Value), value);
        builder.AddAttribute(3, nameof(PolarisSelect.OnChange), EventCallback.Factory.Create<PolarisEventArgs>(
            this,
            // The descriptor gets a BOOLEAN rather than the option's own text: the engine coerces either, but
            // a chip describing the filter and a formatter rendering it can only speak about a boolean. A
            // string here made every chip read "In Shopify is false" beside a control offering Yes and No.
            args => SetFilterAsync(columnId, args.Value is null or AnyOption
                ? null
                : PolarisGridFilterDescriptor.Equals(columnId, string.Equals(args.Value, "true", StringComparison.OrdinalIgnoreCase)))));
        builder.AddAttribute(4, nameof(PolarisSelect.ChildContent), (RenderFragment)(select =>
        {
            select.OpenComponent<PolarisOption>(0);
            select.AddAttribute(1, nameof(PolarisOption.Value), AnyOption);
            select.AddAttribute(2, nameof(PolarisOption.ChildContent), (RenderFragment)(text => text.AddContent(0, "All")));
            select.CloseComponent();

            select.OpenComponent<PolarisOption>(10);
            select.AddAttribute(11, nameof(PolarisOption.Value), "true");
            select.AddAttribute(12, nameof(PolarisOption.ChildContent), (RenderFragment)(text => text.AddContent(0, "Yes")));
            select.CloseComponent();

            select.OpenComponent<PolarisOption>(20);
            select.AddAttribute(21, nameof(PolarisOption.Value), "false");
            select.AddAttribute(22, nameof(PolarisOption.ChildContent), (RenderFragment)(text => text.AddContent(0, "No")));
            select.CloseComponent();
        }));
        builder.CloseComponent();
    }

    private void BuildRangeControl(RenderTreeBuilder builder, IPolarisGridColumn column)
    {
        // Bounds from the whole dataset, not the filtered one: bounds that follow the filter move
        // inwards with the handles, so a narrow range can never be widened again.
        var (minimum, maximum) = State.GetNumericBounds(column.Id);
        var domain = new PolarisNumberRange(
            minimum ?? 0m,
            maximum ?? (minimum ?? 0m) + 1m);

        var filter = State.Query.FilterFor(column.Id);
        var value = filter is null
            ? (PolarisNumberRange?)null
            : new PolarisNumberRange(ToDecimal(filter.Value, domain.Lower), ToDecimal(filter.SecondValue, domain.Upper));

        var columnId = column.Id;

        builder.OpenComponent<PolarisRangeSlider>(0);
        builder.AddAttribute(1, nameof(PolarisRangeSlider.Label), column.Title);
        builder.AddAttribute(2, nameof(PolarisRangeSlider.Minimum), domain.Lower);
        builder.AddAttribute(3, nameof(PolarisRangeSlider.Maximum), domain.Upper);
        builder.AddAttribute(4, nameof(PolarisRangeSlider.Step), StepFor(domain));
        builder.AddAttribute(5, nameof(PolarisRangeSlider.Value), value);
        builder.AddAttribute(6, nameof(PolarisRangeSlider.Format), column.Format);
        builder.AddAttribute(7, nameof(PolarisRangeSlider.LowerLabel), string.Concat(column.Title, " from"));
        builder.AddAttribute(8, nameof(PolarisRangeSlider.UpperLabel), string.Concat(column.Title, " to"));
        builder.AddAttribute(9, nameof(PolarisRangeSlider.ValueChanged), EventCallback.Factory.Create<PolarisNumberRange>(
            this,
            range => SetFilterAsync(
                columnId,
                // A full range excludes nothing, so it is not a filter: it is the absence of one.
                range.Lower <= domain.Lower && range.Upper >= domain.Upper
                    ? null
                    : PolarisGridFilterDescriptor.InRange(columnId, range))));
        builder.CloseComponent();
    }

    private void BuildDateRangeControl(RenderTreeBuilder builder, IPolarisGridColumn column)
    {
        var filter = State.Query.FilterFor(column.Id);
        var columnId = column.Id;

        builder.OpenComponent<PolarisStack>(0);
        builder.AddAttribute(1, nameof(PolarisStack.Direction), PolarisStackDirection.Inline);
        builder.AddAttribute(2, nameof(PolarisStack.Gap), PolarisGap.Small);
        builder.AddAttribute(3, nameof(PolarisStack.ChildContent), (RenderFragment)(content =>
        {
            content.OpenComponent<PolarisDateField>(0);
            content.AddAttribute(1, nameof(PolarisDateField.Label), string.Concat(column.Title, " from"));
            content.AddAttribute(2, nameof(PolarisDateField.Value), Convert.ToString(filter?.Value, CultureInfo.InvariantCulture) ?? string.Empty);
            content.AddAttribute(3, nameof(PolarisDateField.OnChange), EventCallback.Factory.Create<PolarisEventArgs>(
                this,
                args => SetFilterAsync(columnId, BetweenOrNull(columnId, args.Value, filter?.SecondValue))));
            content.CloseComponent();

            content.OpenComponent<PolarisDateField>(10);
            content.AddAttribute(11, nameof(PolarisDateField.Label), string.Concat(column.Title, " to"));
            content.AddAttribute(12, nameof(PolarisDateField.Value), Convert.ToString(filter?.SecondValue, CultureInfo.InvariantCulture) ?? string.Empty);
            content.AddAttribute(13, nameof(PolarisDateField.OnChange), EventCallback.Factory.Create<PolarisEventArgs>(
                this,
                args => SetFilterAsync(columnId, BetweenOrNull(columnId, filter?.Value, args.Value))));
            content.CloseComponent();
        }));
        builder.CloseComponent();
    }

    private static PolarisGridFilterDescriptor? BetweenOrNull(string field, object? lower, object? upper)
    {
        var hasLower = lower is string lowerText ? !string.IsNullOrWhiteSpace(lowerText) : lower is not null;
        var hasUpper = upper is string upperText ? !string.IsNullOrWhiteSpace(upperText) : upper is not null;

        return hasLower || hasUpper
            ? new PolarisGridFilterDescriptor(field, PolarisGridOperator.Between, lower, upper)
            : null;
    }

    private const string AnyOption = "any";

    /// <summary>
    /// The chip row's tracks for a panel: as many as fit, each as wide as its chip wants.
    /// </summary>
    /// <remarks>
    /// A wrapping grid rather than an inline stack, because an inline Polaris stack does not wrap —
    /// three chips in a sidebar column would run off its edge with no way to reach the third. The
    /// minimum is a chip's worth of width, so a chip shorter than that is not stretched to a slab.
    /// </remarks>
    private const string ChipsGridColumns = "repeat(auto-fill, minmax(9rem, max-content))";

    private const string SearchDraftKey = "__search";

    /// <summary>
    /// The columns this bar offers a control for.
    /// </summary>
    /// <returns>Every column that declares a filter type, drawn or not.</returns>
    /// <remarks>
    /// <strong><see cref="IPolarisGridColumn.Visible"/> is deliberately not consulted.</strong> The two
    /// declarations answer different questions — a filter type says "this value can narrow the result", and
    /// visibility says "this value is drawn in the table" — and a screen often needs the first without the
    /// second: a status already shown as a badge, a boolean the cell conveys in words, or a facet the table
    /// has no room for.
    ///
    /// Tying them together was wrong in both directions. It made a hidden facet impossible to filter on, and
    /// it left a hidden column's filter applying with its control gone — measured on the products list, where
    /// hiding a column removed the only way to see or clear a filter that still narrowed the result. A column
    /// that should not be filterable says <c>FilterType="None"</c>.
    ///
    /// The controls are drawn in <see cref="IPolarisGridColumn.FilterIndex"/> order, so the bar can be
    /// ordered independently of the table. <c>OrderBy</c> is stable, so columns that share an index —
    /// including every column that leaves the default — keep the order they were registered in.
    /// </remarks>
    private List<IPolarisGridColumn> FilterableColumns()
        => [.. State.Columns
            .Where(column => column.FilterType != PolarisGridFilterType.None)
            .OrderBy(column => column.FilterIndex)];

    private List<(IPolarisGridColumn Column, PolarisGridFilterDescriptor Filter)> ActiveFilters()
    {
        var active = new List<(IPolarisGridColumn Column, PolarisGridFilterDescriptor Filter)>();

        foreach (var filter in State.Query.Filters)
        {
            var column = State.Columns.FirstOrDefault(candidate => string.Equals(candidate.Id, filter.Field, StringComparison.Ordinal));

            if (column is not null)
            {
                active.Add((column, filter));
            }
        }

        return active;
    }

    private IReadOnlyList<string> SelectedValues(string columnId)
    {
        var filter = State.Query.FilterFor(columnId);

        if (filter?.Values is null)
        {
            return [];
        }

        return [.. filter.Values.Select(value => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty)];
    }

    private Task RemoveFilterAsync(string field)
    {
        _drafts.Remove(field);
        return SetFilterAsync(field, null);
    }

    private void ApplyFilter(string field, PolarisGridFilterDescriptor? filter)
    {
        _pendingFields.Remove(field);

        if (filter is null)
        {
            State.ClearFilter(field);
            return;
        }

        State.SetFilter(filter);
    }

    private Task ApplyDraftsAsync()
    {
        var drafts = _drafts.ToList();
        _drafts.Clear();

        foreach (var (field, filter) in drafts)
        {
            if (string.Equals(field, SearchDraftKey, StringComparison.Ordinal))
            {
                State.SetGlobalSearch(Convert.ToString(filter?.Value, CultureInfo.InvariantCulture));
            }
            else if (filter is null)
            {
                State.ClearFilter(field);
            }
            else
            {
                State.SetFilter(filter);
            }
        }

        _appliedSignature = Signature(State);
        return Task.CompletedTask;
    }

    private void RebuildDrafts()
    {
        _drafts.Clear();

        foreach (var filter in State.Query.Filters)
        {
            _drafts[filter.Field] = filter;
        }

        _appliedSignature = Signature(State);
    }

    private static string Signature(PolarisGridState state)
        => string.Join(
            "|",
            state.Query.Filters.Select(filter => string.Create(CultureInfo.InvariantCulture, $"{filter.Field}:{filter.Operator}:{filter.Value}:{filter.SecondValue}:{filter.Values?.Count}")));

    private bool IsDebouncing(string field) => field == SearchDraftKey ? _searchPending : _pendingFields.Contains(field);

    /// <summary>
    /// Runs an apply after the debounce interval, cancelling the one queued before it.
    /// </summary>
    /// <remarks>
    /// One timer for the whole bar rather than one per control: typing in the search box and then
    /// nudging a slider should produce one query, not two, and the second change supersedes the first
    /// anyway.
    /// </remarks>
    private void Schedule(Action apply, Action onCancel)
    {
        _debounce?.Cancel();
        _debounce?.Dispose();

        var source = new CancellationTokenSource();
        _debounce = source;

        var delay = Math.Max(0, DebounceMilliseconds);

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delay, source.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                onCancel();
                return;
            }

            if (source.Token.IsCancellationRequested)
            {
                onCancel();
                return;
            }

            await InvokeAsync(() =>
            {
                apply();
                StateHasChanged();
            }).ConfigureAwait(false);
        });
    }

    private static decimal ToDecimal(object? value, decimal fallback)
        => value is not null
            && decimal.TryParse(
                Convert.ToString(value, CultureInfo.InvariantCulture),
                NumberStyles.Number | NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var parsed)
            ? parsed
            : fallback;

    private static decimal StepFor(PolarisNumberRange domain)
    {
        var span = domain.Span;

        // A hundred steps over whatever the data spans: fine enough to feel continuous, coarse enough
        // that a keyboard user reaches a value in a reasonable number of key presses.
        var step = span / 100m;

        if (step >= 1m)
        {
            return Math.Round(step, MidpointRounding.AwayFromZero);
        }

        if (step >= 0.1m)
        {
            return 0.1m;
        }

        return step <= 0m ? 1m : Math.Round(step, 3, MidpointRounding.AwayFromZero);
    }

    private static string Describe(IPolarisGridColumn column, PolarisGridFilterDescriptor filter)
    {
        var title = column.Title;
        var value = column.FormatValue(filter.Value);

        return filter.Operator switch
        {
            PolarisGridOperator.Contains => string.Concat(title, " contains “", filter.Value, "”"),
            PolarisGridOperator.StartsWith => string.Concat(title, " starts with “", filter.Value, "”"),
            PolarisGridOperator.EndsWith => string.Concat(title, " ends with “", filter.Value, "”"),
            PolarisGridOperator.Equals => string.Concat(title, " is ", value),
            PolarisGridOperator.NotEquals => string.Concat(title, " is not ", value),
            PolarisGridOperator.In => string.Concat(
                title,
                " is ",
                string.Join(", ", (filter.Values ?? []).Select(column.FormatValue))),
            PolarisGridOperator.Between => string.Concat(
                title,
                " is ",
                DescribeBound(column, filter.Value),
                " to ",
                DescribeBound(column, filter.SecondValue)),
            PolarisGridOperator.GreaterThan => string.Concat(title, " is above ", value),
            PolarisGridOperator.GreaterThanOrEqual => string.Concat(title, " is at least ", value),
            PolarisGridOperator.LessThan => string.Concat(title, " is below ", value),
            PolarisGridOperator.LessThanOrEqual => string.Concat(title, " is at most ", value),
            PolarisGridOperator.IsNull => string.Concat(title, " is empty"),
            PolarisGridOperator.IsNotNull => string.Concat(title, " is not empty"),
            _ => title,
        };
    }

    /// <summary>
    /// One end of a range, written the way the range control's own Min and Max fields write it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The chip is the only other place a bound is shown, and the two must agree: fields reading "4.17"
    /// with a chip beside them reading "4.1666667" look like the filter is not what the panel said. The
    /// bound is a row's own value — the cheapest and dearest in the data — so it is rarely a number
    /// anybody typed, which is why it needs a format at all.
    /// </para>
    /// <para>
    /// A column that states a format keeps it: <c>C2</c> already fixes two decimals and carries the
    /// currency, and a column formatted <c>N0</c> means whole numbers. This only fills in the default the
    /// slider's fields use. A date range is not a number and goes through the column's formatter
    /// untouched, as does anything a caller's own <see cref="ICustomFormatter"/> claims.
    /// </para>
    /// </remarks>
    private static string DescribeBound(IPolarisGridColumn column, object? value)
    {
        if (value is null)
        {
            return string.Empty;
        }

        if (!IsNumber(value) || column.FormatProvider?.GetFormat(typeof(ICustomFormatter)) is not null)
        {
            return column.FormatValue(value);
        }

        return PolarisNumberText.Format(
            Convert.ToDecimal(value, CultureInfo.InvariantCulture),
            column.Format ?? PolarisRangeSlider.DefaultInputFormat,
            column.FormatProvider ?? CultureInfo.InvariantCulture);
    }

    /// <summary>True for the value types a range control can bound.</summary>
    private static bool IsNumber(object value)
        => Type.GetTypeCode(value.GetType()) is
            TypeCode.Decimal or TypeCode.Double or TypeCode.Single or
            TypeCode.Int16 or TypeCode.Int32 or TypeCode.Int64 or
            TypeCode.UInt16 or TypeCode.UInt32 or TypeCode.UInt64 or
            TypeCode.Byte or TypeCode.SByte;
}

