namespace Polaris.Blazor.DataGrid;

/// <summary>
/// The state of one grid over a local collection.
/// </summary>
/// <remarks>
/// <para>
/// Holds a snapshot of the records, the registered columns and the selection, and recomputes the
/// visible page whenever any of the three changes. The records are copied into an array on
/// <see cref="SetItems"/>, so the query engine always enumerates the same sequence — a lazy
/// sequence re-enumerated per query is a dataset that can differ between the count and the page.
/// </para>
/// <para>
/// Selection is kept by key, not by index, so it survives sorting, filtering and paging. The default
/// key is the item itself, compared by <see cref="object.Equals(object?)"/> — correct for a record
/// or a struct with value equality, and reference identity for a class, which is why a class with
/// meaningful identity should pass <c>keySelector</c>.
/// </para>
/// </remarks>
/// <typeparam name="TItem">The row type.</typeparam>
public sealed class PolarisGridState<TItem> : PolarisGridState
{
    private readonly List<TItem> _items = [];
    private readonly List<PolarisGridColumnDefinition<TItem>> _columns = [];
    private readonly HashSet<object> _selected = [];

    private Dictionary<string, (decimal? Minimum, decimal? Maximum)>? _decimalBounds;
    private Dictionary<string, (DateOnly? Minimum, DateOnly? Maximum)>? _dateBounds;
    private Dictionary<string, IReadOnlyList<object?>>? _distinct;

    /// <summary>Creates the state, optionally with its first snapshot of records.</summary>
    /// <param name="items">The local records. Materialised immediately.</param>
    /// <param name="keySelector">
    /// A stable identity per record. Required for a class without value equality if selection is to
    /// survive a reload.
    /// </param>
    public PolarisGridState(IEnumerable<TItem>? items = null, Func<TItem, object>? keySelector = null)
    {
        KeySelector = keySelector ?? (item => item!);
        Result = PolarisGridQueryResult<TItem>.Empty(Query.PageSize);

        if (items is not null)
        {
            SetItems(items);
        }
    }

    /// <summary>The records, materialised. Replaced as a whole, never edited in place.</summary>
    public IReadOnlyList<TItem> Items => _items;

    /// <summary>The columns registered by the grid.</summary>
    public IReadOnlyList<PolarisGridColumnDefinition<TItem>> ColumnDefinitions => _columns;

    /// <summary>The visible page and what it took to get there.</summary>
    public PolarisGridQueryResult<TItem> Result { get; private set; }

    /// <summary>Identifies one record, so selection survives a reload.</summary>
    public Func<TItem, object> KeySelector { get; }

    /// <summary>The keys of the selected records.</summary>
    public IReadOnlyCollection<object> SelectedKeys => _selected;

    /// <summary>How many records matched the filters, across every page.</summary>
    public override int TotalCount => Result.TotalCount;

    /// <summary>The one-based number of the first row on the page, or zero when there are none.</summary>
    public override int FirstRecordNumber => Result.FirstRecordNumber;

    /// <summary>The one-based number of the last row on the page, or zero when there are none.</summary>
    public override int LastRecordNumber => Result.LastRecordNumber;

    /// <summary>True when the visible page holds no rows.</summary>
    public override bool IsEmpty => Result.IsEmpty;

    /// <summary>The columns, as the non-generic components see them.</summary>
    public override IReadOnlyList<IPolarisGridColumn> Columns => _columns;

    /// <summary>Raised when the selection changes. Not raised for a change of page or filter.</summary>
    public event Action? SelectionChanged;

    /// <summary>
    /// Replaces the records. Selection is kept by key, so a reload does not clear what the merchant
    /// had ticked — and a record that no longer exists silently drops out of it.
    /// </summary>
    /// <param name="items">The new records.</param>
    public void SetItems(IEnumerable<TItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        _items.Clear();
        _items.AddRange(items);

        InvalidateCaches();

        if (_selected.Count > 0)
        {
            var present = new HashSet<object>();

            foreach (var item in _items)
            {
                present.Add(KeySelector(item));
            }

            _selected.RemoveWhere(key => !present.Contains(key));
        }

        Refresh();
    }

    /// <summary>
    /// Finds a record by its key, across the whole snapshot rather than the visible page.
    /// </summary>
    /// <param name="key">The row key.</param>
    /// <returns>The record, or <c>default</c> when it is not in the snapshot.</returns>
    /// <remarks>
    /// The visible page is not enough: a merchant can edit a row, page away, and save the lot, and a
    /// lookup that only saw the current page would silently drop the edit.
    /// </remarks>
    public TItem? FindItem(object key)
    {
        foreach (var item in _items)
        {
            if (Equals(KeySelector(item), key))
            {
                return item;
            }
        }

        return default;
    }

    /// <summary>
    /// Replaces records in the snapshot, one per row key, and recomputes the query once.
    /// </summary>
    /// <param name="updates">The replacement records, keyed by row key.</param>
    /// <returns>How many records were found and replaced.</returns>
    /// <remarks>
    /// One call for a whole save, not one per row: every mutation of this state notifies its
    /// subscribers, and a page saving twelve edited rows would otherwise redraw the table twelve times,
    /// each time over a half-saved collection.
    /// </remarks>
    public int UpdateItems(IReadOnlyDictionary<object, TItem> updates)
    {
        ArgumentNullException.ThrowIfNull(updates);

        if (updates.Count == 0)
        {
            return 0;
        }

        var replaced = 0;

        for (var index = 0; index < _items.Count; index++)
        {
            var item = _items[index];

            if (!updates.TryGetValue(KeySelector(item), out var updated))
            {
                continue;
            }

            _items[index] = updated;
            replaced++;
        }

        if (replaced == 0)
        {
            return 0;
        }

        // A value that was edited can move a column's bounds or add a category, so the derived
        // caches are stale in a way the shape of the dataset does not show.
        InvalidateCaches();
        Refresh();
        return replaced;
    }

    /// <summary>
    /// Registers a column. Called by <see cref="PolarisGridColumn{TItem}"/> on behalf of its
    /// declaration; a duplicate id is a mistake in the declaration and is refused rather than
    /// silently shadowed.
    /// </summary>
    /// <param name="column">The column definition.</param>
    public void RegisterColumn(PolarisGridColumnDefinition<TItem> column)
    {
        ArgumentNullException.ThrowIfNull(column);
        ArgumentException.ThrowIfNullOrEmpty(column.Id);

        if (_columns.Exists(existing => string.Equals(existing.Id, column.Id, StringComparison.Ordinal)))
        {
            throw new ArgumentException(
                $"A column with the id '{column.Id}' is already registered. Column ids identify filters and sort orders, so two columns sharing one would make both ambiguous.",
                nameof(column));
        }

        _columns.Add(column);
        InvalidateCaches();
        Refresh();
    }

    /// <inheritdoc />
    /// <remarks>
    /// The column component reaches the state through its host, which knows the state but not the row
    /// type, so this is the door it comes in by. A declaration built for another row type is refused
    /// rather than cast blindly: it would have been declared inside the wrong host, and registering it
    /// would put a getter over the wrong object in front of every row.
    /// </remarks>
    public override void RegisterColumn(IPolarisGridColumn column)
    {
        ArgumentNullException.ThrowIfNull(column);

        RegisterColumn(column as PolarisGridColumnDefinition<TItem>
            ?? throw new ArgumentException(
                $"A column declared for {column.GetType().Name} cannot be registered with a grid of {typeof(TItem).Name} rows. "
                + "Declare it inside a grid or a filter whose rows are of the same type.",
                nameof(column)));
    }

    /// <summary>Forgets a column that is being disposed.</summary>
    /// <param name="id">The column id.</param>
    /// <returns>True when a column was removed.</returns>
    public override bool UnregisterColumn(string id)
    {
        if (_columns.RemoveAll(column => string.Equals(column.Id, id, StringComparison.Ordinal)) == 0)
        {
            return false;
        }

        Query.Filters.RemoveAll(filter => string.Equals(filter.Field, id, StringComparison.Ordinal));
        Query.Sorts.RemoveAll(sort => string.Equals(sort.Field, id, StringComparison.Ordinal));

        InvalidateCaches();
        Refresh();
        return true;
    }

    /// <inheritdoc />
    public override void Refresh()
    {
        Result = PolarisGridQueryEngine.Execute(_items, _columns, Query);

        // The engine clamps the page to the pages that exist; writing the verdict back is what keeps
        // a pager from showing page 3 of a result that has one page.
        Query.PageIndex = Result.PageIndex;
        Query.PageSize = Result.PageSize;

        RaiseChanged();
    }

    /// <summary>
    /// The numeric bounds of a column across the whole dataset, ignoring filters.
    /// </summary>
    /// <param name="columnId">The column id.</param>
    /// <remarks>
    /// Taken from the unfiltered snapshot on purpose. A slider bounded by the <em>filtered</em> data
    /// cannot be widened again once it has been narrowed, because its own ends move inwards with
    /// every drag.
    /// </remarks>
    public override (decimal? Minimum, decimal? Maximum) GetNumericBounds(string columnId)
    {
        _decimalBounds ??= new Dictionary<string, (decimal? Minimum, decimal? Maximum)>(StringComparer.Ordinal);

        if (_decimalBounds.TryGetValue(columnId, out var cached))
        {
            return cached;
        }

        decimal? minimum = null;
        decimal? maximum = null;
        var column = FindColumn(columnId);

        if (column is not null)
        {
            foreach (var item in _items)
            {
                if (!PolarisGridValues.TryToDecimal(column.ValueGetter(item), out var value))
                {
                    continue;
                }

                minimum = minimum is null || value < minimum ? value : minimum;
                maximum = maximum is null || value > maximum ? value : maximum;
            }
        }

        var bounds = (minimum, maximum);
        _decimalBounds[columnId] = bounds;
        return bounds;
    }

    /// <summary>
    /// The date bounds of a column across the whole dataset, ignoring filters. Unfiltered for the
    /// same reason as <see cref="GetNumericBounds"/>.
    /// </summary>
    /// <param name="columnId">The column id.</param>
    public override (DateOnly? Minimum, DateOnly? Maximum) GetDateBounds(string columnId)
    {
        _dateBounds ??= new Dictionary<string, (DateOnly? Minimum, DateOnly? Maximum)>(StringComparer.Ordinal);

        if (_dateBounds.TryGetValue(columnId, out var cached))
        {
            return cached;
        }

        DateOnly? minimum = null;
        DateOnly? maximum = null;
        var column = FindColumn(columnId);

        if (column is not null)
        {
            foreach (var item in _items)
            {
                if (!PolarisGridValues.TryToDateOnly(column.ValueGetter(item), out var value))
                {
                    continue;
                }

                minimum = minimum is null || value < minimum ? value : minimum;
                maximum = maximum is null || value > maximum ? value : maximum;
            }
        }

        var bounds = (minimum, maximum);
        _dateBounds[columnId] = bounds;
        return bounds;
    }

    /// <summary>
    /// The distinct values of a column, sorted, for a multi-select filter's options.
    /// </summary>
    /// <param name="columnId">The column id.</param>
    /// <remarks>
    /// Derived from the whole snapshot rather than from the visible page: an option that vanished
    /// because the merchant filtered on it could never be cleared again.
    /// </remarks>
    public override IReadOnlyList<object?> GetDistinctValues(string columnId)
    {
        _distinct ??= new Dictionary<string, IReadOnlyList<object?>>(StringComparer.Ordinal);

        if (_distinct.TryGetValue(columnId, out var cached))
        {
            return cached;
        }

        var column = FindColumn(columnId);
        IReadOnlyList<object?> values = [];

        if (column is not null)
        {
            var distinct = new List<object?>();

            foreach (var item in _items)
            {
                var value = column.ValueGetter(item);

                if (value is not null && !distinct.Contains(value))
                {
                    distinct.Add(value);
                }
            }

            distinct.Sort(PolarisGridValueComparer.Instance);
            values = distinct;
        }

        _distinct[columnId] = values;
        return values;
    }

    /// <summary>The key that identifies one record.</summary>
    /// <param name="item">The record.</param>
    public object KeyOf(TItem item) => KeySelector(item);

    /// <summary>True when the record is selected.</summary>
    /// <param name="item">The record.</param>
    public bool IsSelected(TItem item) => _selected.Contains(KeySelector(item));

    /// <summary>Selects or deselects one record.</summary>
    /// <param name="item">The record.</param>
    /// <param name="selected">Whether it should be selected.</param>
    public void SetSelected(TItem item, bool selected)
    {
        var changed = selected ? _selected.Add(KeySelector(item)) : _selected.Remove(KeySelector(item));

        if (changed)
        {
            SelectionChanged?.Invoke();
        }
    }

    /// <summary>Turns one record's selection on or off.</summary>
    /// <param name="item">The record.</param>
    public void ToggleSelection(TItem item) => SetSelected(item, !IsSelected(item));

    /// <summary>Clears the selection.</summary>
    public void ClearSelection()
    {
        if (_selected.Count == 0)
        {
            return;
        }

        _selected.Clear();
        SelectionChanged?.Invoke();
    }

    /// <summary>
    /// Selects or deselects every record on the visible page, which is what a header checkbox does.
    /// </summary>
    /// <param name="selected">Whether the rows should be selected.</param>
    public void SetPageSelected(bool selected)
    {
        var changed = false;

        foreach (var item in Result.Items)
        {
            changed |= selected ? _selected.Add(KeySelector(item)) : _selected.Remove(KeySelector(item));
        }

        if (changed)
        {
            SelectionChanged?.Invoke();
        }
    }

    private PolarisGridColumnDefinition<TItem>? FindColumn(string columnId)
        => _columns.Find(column => string.Equals(column.Id, columnId, StringComparison.Ordinal));

    private void InvalidateCaches()
    {
        _decimalBounds = null;
        _dateBounds = null;
        _distinct = null;
    }
}
