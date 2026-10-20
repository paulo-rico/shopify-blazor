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
/// </example>
public sealed class PolarisDataFilter : PolarisGridComponentBase
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

    /// <inheritdoc />
    protected override PolarisGridState? BoundState => State;

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

        var filterable = FilterableColumns();

        builder.OpenComponent<PolarisStack>(0);
        builder.AddAttribute(1, nameof(PolarisStack.Direction), PolarisStackDirection.Inline);
        builder.AddAttribute(2, nameof(PolarisStack.Gap), PolarisGap.Small);
        builder.AddAttribute(3, nameof(PolarisStack.AlignItems), "center");
        builder.AddAttribute(4, nameof(PolarisStack.JustifyContent), "space-between");
        builder.AddAttribute(5, nameof(PolarisStack.ChildContent), (RenderFragment)(content =>
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
                BuildClearAll(bar);
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
            builder.OpenComponent<PolarisClickableChip>(20);
            builder.AddAttribute(21, nameof(PolarisClickableChip.Removable), true);
            builder.AddAttribute(22, nameof(PolarisClickableChip.Color), PolarisColor.Strong);
            builder.AddAttribute(23, nameof(PolarisClickableChip.OnRemove), EventCallback.Factory.Create<PolarisEventArgs>(this, _ => RemoveFilterAsync(column.Id)));
            builder.AddAttribute(24, nameof(PolarisClickableChip.ChildContent), (RenderFragment)(text => text.AddContent(0, Describe(column, filter))));
            builder.CloseComponent();
        }
    }

    private void BuildClearAll(RenderTreeBuilder builder)
    {
        if (!ShowClearAll || !State.Query.HasAnyFilter)
        {
            return;
        }

        builder.OpenComponent<PolarisButton>(30);
        builder.AddAttribute(31, nameof(PolarisButton.Variant), PolarisButtonVariant.Tertiary);
        builder.AddAttribute(32, nameof(PolarisButton.OnClick), EventCallback.Factory.Create<PolarisEventArgs>(this, _ => ClearAllAsync()));
        builder.AddAttribute(33, nameof(PolarisButton.ChildContent), (RenderFragment)(text => text.AddContent(0, "Clear all")));
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
        builder.AddAttribute(42, nameof(PolarisPopover.MaxBlockSize), "420px");

        // The panel's width is stated rather than derived. Measured in a browser: a popover with no
        // inline size lays its slotted content out at zero width — the content has height and no width,
        // so a slider inside it has a 0px track that cannot be dragged and a text field is a sliver.
        builder.AddAttribute(43, nameof(PolarisPopover.InlineSize), "320px");
        builder.AddAttribute(44, nameof(PolarisPopover.ChildContent), (RenderFragment)(panel =>
        {
            panel.OpenComponent<PolarisStack>(0);
            panel.AddAttribute(1, nameof(PolarisStack.Gap), PolarisGap.Base);
            panel.AddAttribute(2, nameof(PolarisStack.ChildContent), (RenderFragment)(content =>
            {
                foreach (var column in filterable)
                {
                    BuildControl(content, column);
                }

                if (ApplyMode == PolarisGridFilterApplyMode.Manual)
                {
                    content.OpenComponent<PolarisStack>(90);
                    content.AddAttribute(91, nameof(PolarisStack.Direction), PolarisStackDirection.Inline);
                    content.AddAttribute(92, nameof(PolarisStack.Gap), PolarisGap.Small);
                    content.AddAttribute(93, nameof(PolarisStack.ChildContent), (RenderFragment)(buttons =>
                    {
                        buttons.OpenComponent<PolarisButton>(0);
                        buttons.AddAttribute(1, nameof(PolarisButton.Variant), PolarisButtonVariant.Primary);
                        buttons.AddAttribute(2, nameof(PolarisButton.CommandFor), _panelId);
                        buttons.AddAttribute(3, nameof(PolarisButton.Command), PolarisCommand.Hide);
                        buttons.AddAttribute(4, nameof(PolarisButton.OnClick), EventCallback.Factory.Create<PolarisEventArgs>(this, _ => ApplyDraftsAsync()));
                        buttons.AddAttribute(5, nameof(PolarisButton.ChildContent), (RenderFragment)(text => text.AddContent(0, "Apply")));
                        buttons.CloseComponent();

                        buttons.OpenComponent<PolarisButton>(10);
                        buttons.AddAttribute(11, nameof(PolarisButton.CommandFor), _panelId);
                        buttons.AddAttribute(12, nameof(PolarisButton.Command), PolarisCommand.Hide);
                        buttons.AddAttribute(13, nameof(PolarisButton.OnClick), EventCallback.Factory.Create<PolarisEventArgs>(this, _ => ClearAllAsync()));
                        buttons.AddAttribute(14, nameof(PolarisButton.ChildContent), (RenderFragment)(text => text.AddContent(0, "Cancel")));
                        buttons.CloseComponent();
                    }));
                    content.CloseComponent();
                }
            }));
            panel.CloseComponent();
        }));
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
        var current = State.Query.FilterFor(column.Id)?.Value;
        var value = current is null ? AnyOption : Convert.ToString(current, CultureInfo.InvariantCulture) ?? AnyOption;
        var columnId = column.Id;

        builder.OpenComponent<PolarisSelect>(0);
        builder.AddAttribute(1, nameof(PolarisSelect.Label), column.Title);
        builder.AddAttribute(2, nameof(PolarisSelect.Value), value);
        builder.AddAttribute(3, nameof(PolarisSelect.OnChange), EventCallback.Factory.Create<PolarisEventArgs>(
            this,
            args => SetFilterAsync(columnId, args.Value is null or AnyOption
                ? null
                : PolarisGridFilterDescriptor.Equals(columnId, args.Value))));
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

    private const string SearchDraftKey = "__search";

    private List<IPolarisGridColumn> FilterableColumns()
        => [.. State.Columns.Where(column => column.Visible && column.FilterType != PolarisGridFilterType.None)];

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
                column.FormatValue(filter.Value),
                " to ",
                column.FormatValue(filter.SecondValue)),
            PolarisGridOperator.GreaterThan => string.Concat(title, " is above ", value),
            PolarisGridOperator.GreaterThanOrEqual => string.Concat(title, " is at least ", value),
            PolarisGridOperator.LessThan => string.Concat(title, " is below ", value),
            PolarisGridOperator.LessThanOrEqual => string.Concat(title, " is at most ", value),
            PolarisGridOperator.IsNull => string.Concat(title, " is empty"),
            PolarisGridOperator.IsNotNull => string.Concat(title, " is not empty"),
            _ => title,
        };
    }
}

