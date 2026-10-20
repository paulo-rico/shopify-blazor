using System.Globalization;

namespace Polaris.Blazor.DataGrid;

/// <summary>
/// The query behind a grid: which filters apply, how the result is ordered, and which page is
/// visible.
/// </summary>
/// <remarks>
/// Mutable and owned by <see cref="PolarisGridState{TItem}"/>. Components never change it directly —
/// they call the state, which is what guarantees that every mutation recomputes once and notifies
/// once.
/// </remarks>
public sealed class PolarisGridQueryState
{
    /// <summary>The column filters that apply. One per column, AND-ed together.</summary>
    public List<PolarisGridFilterDescriptor> Filters { get; } = [];

    /// <summary>The sort order. Applied in <see cref="PolarisGridSortDescriptor.Priority"/> order.</summary>
    public List<PolarisGridSortDescriptor> Sorts { get; } = [];

    /// <summary>The zero-based page currently visible.</summary>
    public int PageIndex { get; set; }

    /// <summary>How many rows a page holds. The default is 25.</summary>
    public int PageSize { get; set; } = 25;

    /// <summary>Text matched against every searchable column, AND-ed with the column filters.</summary>
    public string? GlobalSearch { get; set; }

    /// <summary>The filter that applies to <paramref name="field"/>, or <c>null</c>.</summary>
    /// <param name="field">The column id.</param>
    public PolarisGridFilterDescriptor? FilterFor(string field)
        => Filters.Find(filter => string.Equals(filter.Field, field, StringComparison.Ordinal));

    /// <summary>True when any filter or the global search is active.</summary>
    public bool HasAnyFilter => Filters.Count > 0 || !string.IsNullOrWhiteSpace(GlobalSearch);

    /// <summary>A copy, so a caller can compare two states without the snapshot changing underneath.</summary>
    public PolarisGridQueryState Clone()
    {
        var clone = new PolarisGridQueryState
        {
            PageIndex = PageIndex,
            PageSize = PageSize,
            GlobalSearch = GlobalSearch,
        };

        clone.Filters.AddRange(Filters);
        clone.Sorts.AddRange(Sorts);
        return clone;
    }

    /// <summary>The value formatted invariantly, which is how the state describes itself in logs.</summary>
    public override string ToString()
        => string.Create(
            CultureInfo.InvariantCulture,
            $"filters={Filters.Count}, sorts={Sorts.Count}, page={PageIndex}, size={PageSize}, search=\"{GlobalSearch}\"");
}
