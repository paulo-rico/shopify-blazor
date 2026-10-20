namespace Polaris.Blazor.DataGrid;

/// <summary>
/// The state one grid shares with the components composed around it: the filter it applies, the
/// order it is in, and the page it is on.
/// </summary>
/// <remarks>
/// <para>
/// The page owns one of these and hands the same instance to <see cref="PolarisDataGrid{TItem}"/>,
/// <see cref="PolarisDataFilter"/> and <see cref="PolarisDataPager"/>. They are separate components
/// — placed wherever the layout needs them — and this is the only thing that makes them one grid:
/// each mutation recomputes once and notifies once, so a filter change reaches the grid, the chips
/// and the pager in the same render rather than in three.
/// </para>
/// <para>
/// Deliberately not a component: it renders nothing, it has no lifecycle, and it can be created,
/// driven and asserted on in a plain unit test. Components must not create or dispose it.
/// </para>
/// <para>
/// Not thread-safe, and not meant to be: it lives on the circuit's dispatcher, and it is mutated
/// from event handlers there.
/// </para>
/// </remarks>
public abstract class PolarisGridState : IDisposable
{
    private bool _disposed;

    /// <summary>The filters, sorts and paging the grid applies.</summary>
    public PolarisGridQueryState Query { get; } = new();

    /// <summary>The columns registered by the grid, in declaration order.</summary>
    public abstract IReadOnlyList<IPolarisGridColumn> Columns { get; }

    /// <summary>How many rows matched the filters, before paging.</summary>
    public abstract int TotalCount { get; }

    /// <summary>The one-based number of the first row on the visible page, or zero when there are none.</summary>
    public abstract int FirstRecordNumber { get; }

    /// <summary>The one-based number of the last row on the visible page, or zero when there are none.</summary>
    public abstract int LastRecordNumber { get; }

    /// <summary>True when the visible page holds no rows.</summary>
    public abstract bool IsEmpty { get; }

    /// <summary>
    /// The numeric bounds of a column across the whole dataset, ignoring filters.
    /// </summary>
    /// <param name="columnId">The column id.</param>
    public abstract (decimal? Minimum, decimal? Maximum) GetNumericBounds(string columnId);

    /// <summary>
    /// The date bounds of a column across the whole dataset, ignoring filters.
    /// </summary>
    /// <param name="columnId">The column id.</param>
    public abstract (DateOnly? Minimum, DateOnly? Maximum) GetDateBounds(string columnId);

    /// <summary>
    /// The distinct values of a column, sorted, for a multi-select filter's options.
    /// </summary>
    /// <param name="columnId">The column id.</param>
    public abstract IReadOnlyList<object?> GetDistinctValues(string columnId);

    /// <summary>How many pages the filtered result fills. Zero when nothing matched.</summary>
    public int PageCount => Query.PageSize <= 0 || TotalCount <= 0
        ? 0
        : (int)(((long)TotalCount + Query.PageSize - 1) / Query.PageSize);

    /// <summary>True when a page comes before the visible one.</summary>
    public bool HasPreviousPage => Query.PageIndex > 0 && PageCount > 0;

    /// <summary>True when a page comes after the visible one.</summary>
    public bool HasNextPage => PageCount > 0 && Query.PageIndex + 1 < PageCount;

    /// <summary>True while the caller is fetching or recomputing, so the pager can disable itself.</summary>
    public bool IsLoading { get; set; }

    /// <summary>A message to show instead of the rows, when a load failed outside the query.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Raised once per change, after the result has been recomputed.</summary>
    /// <remarks>
    /// One event rather than one per aspect: a subscriber that has to know "did anything that
    /// affects me change" is a subscriber that will eventually miss a case, and every component here
    /// re-renders from the state anyway.
    /// </remarks>
    public event Action? Changed;

    /// <summary>Replaces the filter for one column, or removes it when the descriptor is empty.</summary>
    /// <param name="filter">The filter to apply.</param>
    /// <remarks>
    /// Replaces rather than appends: two filters on one column are an AND of two conditions the
    /// controls cannot express, and keeping both would make the chip for each one wrong.
    /// </remarks>
    public void SetFilter(PolarisGridFilterDescriptor filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentException.ThrowIfNullOrEmpty(filter.Field);

        Query.Filters.RemoveAll(existing => string.Equals(existing.Field, filter.Field, StringComparison.Ordinal));

        if (!filter.IsEmpty)
        {
            Query.Filters.Add(filter);
        }

        // A filter change makes the current page meaningless: page 4 of the old result may be page 1
        // of the new one, or not exist at all.
        Query.PageIndex = 0;
        Refresh();
    }

    /// <summary>Removes the filter for one column.</summary>
    /// <param name="field">The column id.</param>
    public void ClearFilter(string field)
    {
        if (Query.Filters.RemoveAll(existing => string.Equals(existing.Field, field, StringComparison.Ordinal)) == 0)
        {
            return;
        }

        Query.PageIndex = 0;
        Refresh();
    }

    /// <summary>Removes every column filter and the global search.</summary>
    public void ClearAllFilters()
    {
        if (!Query.HasAnyFilter)
        {
            return;
        }

        Query.Filters.Clear();
        Query.GlobalSearch = null;
        Query.PageIndex = 0;
        Refresh();
    }

    /// <summary>Sets the text every searchable column is matched against.</summary>
    /// <param name="term">The search text. Null or whitespace clears it.</param>
    public void SetGlobalSearch(string? term)
    {
        var normalised = string.IsNullOrWhiteSpace(term) ? null : term.Trim();

        if (string.Equals(Query.GlobalSearch, normalised, StringComparison.Ordinal))
        {
            return;
        }

        Query.GlobalSearch = normalised;
        Query.PageIndex = 0;
        Refresh();
    }

    /// <summary>True when <paramref name="field"/> has a filter.</summary>
    /// <param name="field">The column id.</param>
    public bool HasFilter(string field) => Query.FilterFor(field) is not null;

    /// <summary>Sorts by one column, replacing any existing order.</summary>
    /// <param name="field">The column id.</param>
    /// <param name="direction">Which way to order.</param>
    public void SetSort(string field, PolarisGridSortDirection direction)
    {
        ArgumentException.ThrowIfNullOrEmpty(field);

        Query.Sorts.Clear();
        Query.Sorts.Add(new PolarisGridSortDescriptor(field, direction));
        Refresh();
    }

    /// <summary>Adds a column to the order, after the ones already applied.</summary>
    /// <param name="field">The column id.</param>
    /// <param name="direction">Which way to order.</param>
    public void AddSort(string field, PolarisGridSortDirection direction)
    {
        ArgumentException.ThrowIfNullOrEmpty(field);

        Query.Sorts.RemoveAll(sort => string.Equals(sort.Field, field, StringComparison.Ordinal));
        Query.Sorts.Add(new PolarisGridSortDescriptor(field, direction, Query.Sorts.Count));
        Refresh();
    }

    /// <summary>
    /// Cycles one column's order: ascending, then descending, then unsorted. This is what a header
    /// click does, and the third state is what lets a merchant get back to the dataset's own order.
    /// </summary>
    /// <param name="field">The column id.</param>
    public void ToggleSort(string field)
    {
        var current = SortDirectionFor(field);

        switch (current)
        {
            case null:
                SetSort(field, PolarisGridSortDirection.Ascending);
                return;
            case PolarisGridSortDirection.Ascending:
                SetSort(field, PolarisGridSortDirection.Descending);
                return;
            default:
                ClearSort();
                return;
        }
    }

    /// <summary>Drops the sort order, restoring the dataset's own order.</summary>
    public void ClearSort()
    {
        if (Query.Sorts.Count == 0)
        {
            return;
        }

        Query.Sorts.Clear();
        Refresh();
    }

    /// <summary>Replaces the whole order, which is what restoring a saved view does.</summary>
    /// <param name="sorts">The descriptors, in priority order.</param>
    public void SetSorts(IEnumerable<PolarisGridSortDescriptor> sorts)
    {
        ArgumentNullException.ThrowIfNull(sorts);

        Query.Sorts.Clear();
        Query.Sorts.AddRange(sorts);
        Refresh();
    }

    /// <summary>Which way <paramref name="field"/> is sorted, or <c>null</c> when it is not.</summary>
    /// <param name="field">The column id.</param>
    public PolarisGridSortDirection? SortDirectionFor(string field)
    {
        var sort = Query.Sorts.Find(candidate => string.Equals(candidate.Field, field, StringComparison.Ordinal));
        return sort?.Direction;
    }

    /// <summary>Moves to a page, clamped to the pages that exist.</summary>
    /// <param name="pageIndex">The zero-based page wanted.</param>
    public void SetPage(int pageIndex)
    {
        var clamped = PolarisGridQueryEngine.NormalizePageIndex(pageIndex, TotalCount, Query.PageSize);

        if (clamped == Query.PageIndex)
        {
            return;
        }

        Query.PageIndex = clamped;
        Refresh();
    }

    /// <summary>
    /// Changes the page size, staying on the record the merchant was looking at rather than on the
    /// page number: the second page of 25 is not the second page of 100.
    /// </summary>
    /// <param name="pageSize">The page size wanted, clamped to what the engine accepts.</param>
    public void SetPageSize(int pageSize)
    {
        var size = PolarisGridQueryEngine.NormalizePageSize(pageSize);

        if (size == Query.PageSize)
        {
            return;
        }

        var firstVisibleRecord = (long)Query.PageIndex * Query.PageSize;

        Query.PageSize = size;
        Query.PageIndex = (int)(firstVisibleRecord / size);
        Refresh();
    }

    /// <summary>Re-runs the query over the current snapshot and notifies subscribers.</summary>
    public abstract void Refresh();

    /// <summary>Notifies subscribers that the state has changed.</summary>
    protected void RaiseChanged()
    {
        if (!_disposed)
        {
            Changed?.Invoke();
        }
    }

    /// <summary>Stops notifying subscribers. Does not dispose anything the caller owns.</summary>
    public void Dispose()
    {
        _disposed = true;
        Changed = null;
        GC.SuppressFinalize(this);
    }
}
