namespace Polaris.Blazor.DataGrid;

/// <summary>
/// One page of a query, plus what it took to get there.
/// </summary>
/// <remarks>
/// <see cref="TotalCount"/> counts the records that matched the filters, <em>before</em> paging —
/// which is what a pager needs to say "26–50 of 148" and what a page count is derived from. An
/// empty result reports no pages rather than one empty one, so a pager cannot offer a first page
/// that holds nothing.
/// </remarks>
/// <typeparam name="TItem">The row type.</typeparam>
/// <param name="Items">The rows of the visible page, in sorted order.</param>
/// <param name="TotalCount">How many rows matched the filters, across every page.</param>
/// <param name="PageIndex">The page these rows belong to, after clamping.</param>
/// <param name="PageSize">The page size the page was cut with.</param>
public sealed record PolarisGridQueryResult<TItem>(
    IReadOnlyList<TItem> Items,
    int TotalCount,
    int PageIndex,
    int PageSize)
{
    /// <summary>How many pages the filtered result fills. Zero when nothing matched.</summary>
    public int PageCount => PageSize <= 0 || TotalCount <= 0
        ? 0
        : (int)(((long)TotalCount + PageSize - 1) / PageSize);

    /// <summary>True when this page holds no rows, which is also true of an empty result.</summary>
    public bool IsEmpty => Items.Count == 0;

    /// <summary>True when a page comes before this one.</summary>
    public bool HasPreviousPage => PageIndex > 0 && PageCount > 0;

    /// <summary>True when a page comes after this one.</summary>
    public bool HasNextPage => PageCount > 0 && PageIndex + 1 < PageCount;

    /// <summary>The one-based number of the first row on this page, or zero when there are none.</summary>
    public int FirstRecordNumber => TotalCount <= 0 || Items.Count == 0 ? 0 : PageIndex * PageSize + 1;

    /// <summary>The one-based number of the last row on this page, or zero when there are none.</summary>
    public int LastRecordNumber => TotalCount <= 0 || Items.Count == 0
        ? 0
        : Math.Min(TotalCount, PageIndex * PageSize + Items.Count);

    /// <summary>
    /// An empty result for a page size. Used before the first query runs and after the dataset is
    /// replaced, so a pager never has to handle a <c>null</c> result.
    /// </summary>
    /// <param name="pageSize">The page size the empty result reports.</param>
    public static PolarisGridQueryResult<TItem> Empty(int pageSize = 25)
        => new([], 0, 0, pageSize);
}
