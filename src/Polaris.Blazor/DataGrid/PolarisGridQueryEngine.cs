using System.Globalization;

namespace Polaris.Blazor.DataGrid;

/// <summary>
/// The local query: filters, counts, sorts and pages an in-memory collection.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately a plain static class with no reference to a component, a lifecycle method or the
/// renderer. Everything about a grid's result is decided here, which is why it can be tested as
/// ordinary C# — and why a wrong page count is a failing test rather than a visibly odd page.
/// </para>
/// <para>
/// The order is fixed and is the whole contract: source snapshot, then filters, then the total count
/// of what matched, then sorting, then paging. Counting before paging is what makes "26–50 of 148"
/// possible; sorting before paging is what makes the order stable across pages instead of within
/// one.
/// </para>
/// <para>
/// A filter that names a column that does not exist, or whose value cannot be read as the column's
/// type, is <em>ignored</em> rather than applied. That is a deliberate choice: a filter that
/// silently matches nothing looks exactly like an empty dataset, and the merchant has no way to tell
/// the two apart.
/// </para>
/// </remarks>
public static class PolarisGridQueryEngine
{
    /// <summary>The smallest page size a pager is allowed to use.</summary>
    public const int MinimumPageSize = 1;

    /// <summary>
    /// The largest page size a pager is allowed to use. A page is rendered into the DOM, so an
    /// unbounded size is a request for the whole dataset in markup.
    /// </summary>
    public const int MaximumPageSize = 10_000;

    /// <summary>
    /// Runs the query and returns the visible page.
    /// </summary>
    /// <typeparam name="TItem">The row type.</typeparam>
    /// <param name="items">The local snapshot to query.</param>
    /// <param name="columns">The registered columns.</param>
    /// <param name="state">The filters, sorts and page to apply.</param>
    public static PolarisGridQueryResult<TItem> Execute<TItem>(
        IReadOnlyList<TItem> items,
        IReadOnlyList<PolarisGridColumnDefinition<TItem>> columns,
        PolarisGridQueryState state)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(state);

        var pageSize = NormalizePageSize(state.PageSize);

        if (items.Count == 0)
        {
            return PolarisGridQueryResult<TItem>.Empty(pageSize);
        }

        var byId = new Dictionary<string, PolarisGridColumnDefinition<TItem>>(StringComparer.Ordinal);
        foreach (var column in columns)
        {
            byId[column.Id] = column;
        }

        var predicates = BuildPredicates(columns, byId, state);

        var matched = new List<TItem>(items.Count);
        foreach (var item in items)
        {
            var keep = true;

            foreach (var predicate in predicates)
            {
                if (!predicate(item))
                {
                    keep = false;
                    break;
                }
            }

            if (keep)
            {
                matched.Add(item);
            }
        }

        var total = matched.Count;
        var pageCount = total == 0 ? 0 : (int)(((long)total + pageSize - 1) / pageSize);
        var pageIndex = pageCount == 0 ? 0 : Math.Clamp(state.PageIndex, 0, pageCount - 1);

        var sorted = Sort(matched, byId, state);

        var page = new List<TItem>(Math.Min(pageSize, sorted.Count));

        if (pageCount > 0)
        {
            var skip = pageIndex * pageSize;

            for (var index = skip; index < sorted.Count && page.Count < pageSize; index++)
            {
                page.Add(sorted[index]);
            }
        }

        return new PolarisGridQueryResult<TItem>(page, total, pageIndex, pageSize);
    }

    /// <summary>Clamps a page size into the range the engine accepts.</summary>
    /// <param name="pageSize">The requested size.</param>
    public static int NormalizePageSize(int pageSize)
        => Math.Clamp(pageSize, MinimumPageSize, MaximumPageSize);

    /// <summary>
    /// Clamps a page index against the number of pages a result fills. An empty result has exactly
    /// one valid index, zero, so a pager never shows a page number that cannot exist.
    /// </summary>
    /// <param name="pageIndex">The requested index.</param>
    /// <param name="totalCount">The number of rows that matched.</param>
    /// <param name="pageSize">The page size.</param>
    public static int NormalizePageIndex(int pageIndex, int totalCount, int pageSize)
    {
        var size = NormalizePageSize(pageSize);

        if (totalCount <= 0)
        {
            return 0;
        }

        var pageCount = (int)(((long)totalCount + size - 1) / size);
        return Math.Clamp(pageIndex, 0, pageCount - 1);
    }

    private static List<Func<TItem, bool>> BuildPredicates<TItem>(
        IReadOnlyList<PolarisGridColumnDefinition<TItem>> columns,
        Dictionary<string, PolarisGridColumnDefinition<TItem>> byId,
        PolarisGridQueryState state)
    {
        var predicates = new List<Func<TItem, bool>>();

        foreach (var filter in state.Filters)
        {
            if (string.IsNullOrEmpty(filter.Field) || filter.IsEmpty)
            {
                continue;
            }

            if (!byId.TryGetValue(filter.Field, out var column))
            {
                continue;
            }

            var predicate = BuildFilter(column, filter);

            if (predicate is not null)
            {
                predicates.Add(predicate);
            }
        }

        var term = state.GlobalSearch?.Trim();

        if (!string.IsNullOrEmpty(term))
        {
            var searchable = new List<Func<TItem, object?>>();

            foreach (var column in columns)
            {
                if (column.Searchable && column.Visible)
                {
                    searchable.Add(column.ValueGetter);
                }
            }

            predicates.Add(item =>
            {
                foreach (var read in searchable)
                {
                    var text = Convert.ToString(read(item), CultureInfo.InvariantCulture);

                    if (text is not null && text.Contains(term, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                return false;
            });
        }

        return predicates;
    }

    private static Func<TItem, bool>? BuildFilter<TItem>(
        PolarisGridColumnDefinition<TItem> column,
        PolarisGridFilterDescriptor filter)
    {
        switch (filter.Operator)
        {
            case PolarisGridOperator.IsNull:
                return item => column.ValueGetter(item) is null;

            case PolarisGridOperator.IsNotNull:
                return item => column.ValueGetter(item) is not null;

            case PolarisGridOperator.In:
            {
                if (filter.Values is null || filter.Values.Count == 0)
                {
                    return null;
                }

                var accepted = new List<object?>(filter.Values.Count);

                foreach (var candidate in filter.Values)
                {
                    if (!PolarisGridValues.TryCoerce(candidate, column.ValueType, out var coerced))
                    {
                        return null;
                    }

                    accepted.Add(coerced);
                }

                var type = column.ValueType;
                return item =>
                {
                    var value = column.ValueGetter(item);

                    if (value is null)
                    {
                        return filter.IncludeNulls;
                    }

                    foreach (var candidate in accepted)
                    {
                        if (PolarisGridValues.Compare(value, candidate, type) == 0)
                        {
                            return true;
                        }
                    }

                    return false;
                };
            }

            case PolarisGridOperator.Between:
            {
                if (!PolarisGridValues.TryCoerce(filter.Value, column.ValueType, out var lowerValue))
                {
                    return null;
                }

                if (!PolarisGridValues.TryCoerce(filter.SecondValue, column.ValueType, out var upperValue))
                {
                    return null;
                }

                if (lowerValue is null && upperValue is null)
                {
                    return null;
                }

                // A range whose ends arrive the wrong way round is still a range. Swapping costs one
                // comparison and spares every caller from validating what a slider already swaps.
                if (lowerValue is not null && upperValue is not null
                    && PolarisGridValues.Compare(lowerValue, upperValue, column.ValueType) > 0)
                {
                    (lowerValue, upperValue) = (upperValue, lowerValue);
                }

                var type = column.ValueType;
                return item =>
                {
                    var value = column.ValueGetter(item);

                    if (value is null)
                    {
                        return filter.IncludeNulls;
                    }

                    if (lowerValue is not null && PolarisGridValues.Compare(value, lowerValue, type) < 0)
                    {
                        return false;
                    }

                    return upperValue is null || PolarisGridValues.Compare(value, upperValue, type) <= 0;
                };
            }

            case PolarisGridOperator.Contains:
            case PolarisGridOperator.StartsWith:
            case PolarisGridOperator.EndsWith:
            {
                var needle = Convert.ToString(filter.Value, CultureInfo.InvariantCulture);

                if (string.IsNullOrWhiteSpace(needle))
                {
                    return null;
                }

                var comparison = filter.Operator switch
                {
                    PolarisGridOperator.StartsWith => StringComparison.OrdinalIgnoreCase,
                    PolarisGridOperator.EndsWith => StringComparison.OrdinalIgnoreCase,
                    _ => StringComparison.OrdinalIgnoreCase,
                };

                return item =>
                {
                    // The null test has to happen on the value itself: Convert.ToString(null) is the
                    // empty string, so an empty cell would test as a value that does not match and
                    // IncludeNulls would silently have no effect.
                    var value = column.ValueGetter(item);

                    if (value is null)
                    {
                        return filter.IncludeNulls;
                    }

                    var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;

                    return filter.Operator switch
                    {
                        PolarisGridOperator.StartsWith => text.StartsWith(needle, comparison),
                        PolarisGridOperator.EndsWith => text.EndsWith(needle, comparison),
                        _ => text.Contains(needle, comparison),
                    };
                };
            }

            default:
            {
                if (!PolarisGridValues.TryCoerce(filter.Value, column.ValueType, out var expected))
                {
                    return null;
                }

                if (expected is null)
                {
                    // "Equals nothing" is a null test, not a filter that matches no row.
                    return filter.Operator == PolarisGridOperator.NotEquals
                        ? item => column.ValueGetter(item) is not null
                        : item => column.ValueGetter(item) is null;
                }

                var type = column.ValueType;
                return item =>
                {
                    var value = column.ValueGetter(item);

                    if (value is null)
                    {
                        return filter.IncludeNulls;
                    }

                    var order = PolarisGridValues.Compare(value, expected, type);

                    return filter.Operator switch
                    {
                        PolarisGridOperator.Equals => order == 0,
                        PolarisGridOperator.NotEquals => order != 0,
                        PolarisGridOperator.GreaterThan => order > 0,
                        PolarisGridOperator.GreaterThanOrEqual => order >= 0,
                        PolarisGridOperator.LessThan => order < 0,
                        _ => order <= 0,
                    };
                };
            }
        }
    }

    private static List<TItem> Sort<TItem>(
        List<TItem> items,
        Dictionary<string, PolarisGridColumnDefinition<TItem>> byId,
        PolarisGridQueryState state)
    {
        if (state.Sorts.Count == 0 || items.Count < 2)
        {
            return items;
        }

        // Ordered by priority, and by the order they were added for equal priorities, so
        // "carat, then name" is what the caller wrote and not an accident of list order.
        var active = new List<(PolarisGridColumnDefinition<TItem> Column, PolarisGridSortDirection Direction)>();
        var index = 0;

        foreach (var sort in state.Sorts
                     .Select(sort => (Sort: sort, Index: index++))
                     .OrderBy(entry => entry.Sort.Priority)
                     .ThenBy(entry => entry.Index))
        {
            if (byId.TryGetValue(sort.Sort.Field, out var column))
            {
                active.Add((column, sort.Sort.Direction));
            }
        }

        if (active.Count == 0)
        {
            return items;
        }

        IOrderedEnumerable<TItem>? ordered = null;

        foreach (var (column, direction) in active)
        {
            Func<TItem, object?> read = column.ValueGetter;

            ordered = (ordered, direction) switch
            {
                (null, PolarisGridSortDirection.Ascending) => items.OrderBy(read, PolarisGridValueComparer.Instance),
                (null, _) => items.OrderByDescending(read, PolarisGridValueComparer.Instance),
                (_, PolarisGridSortDirection.Ascending) => ordered.ThenBy(read, PolarisGridValueComparer.Instance),
                _ => ordered.ThenByDescending(read, PolarisGridValueComparer.Instance),
            };
        }

        return ordered is null ? items : [.. ordered];
    }
}
