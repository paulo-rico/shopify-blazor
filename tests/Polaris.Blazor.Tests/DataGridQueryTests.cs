using Polaris.Blazor.DataGrid;
using Xunit;

namespace Polaris.Blazor.Tests;

/// <summary>
/// Tests for the local query engine and the shared state, with no renderer anywhere near them.
/// </summary>
/// <remarks>
/// <para>
/// The engine is the part of the grid whose mistakes are invisible: a page count that is one too
/// high shows an empty last page, a case-sensitive filter shows fewer rows than the merchant
/// expects, and a numeric column compared as text puts 9 above 18 — all without an error, and all
/// blamed on the data rather than on the grid. So the order the engine promises — filter, count,
/// sort, page — is asserted directly, on a dataset small enough to check by eye.
/// </para>
/// <para>
/// Every test states the failure it prevents, because a test that cannot say what it would catch is
/// the kind that survives a refactor and stops asserting anything.
/// </para>
/// </remarks>
public sealed class DataGridQueryTests
{
    // ── Filters ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_text_filter_matches_anywhere_in_the_value_and_ignores_case()
    {
        // A case-sensitive contains hides every row whose case differs, which reads as missing data
        // rather than as a filter.
        using var state = NewState();

        state.SetFilter(PolarisGridFilterDescriptor.Text("Name", "GOLD"));

        Assert.Equal(3, state.TotalCount);
        Assert.All(state.Result.Items, alloy => Assert.Contains("gold", alloy.Name, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Filters_on_different_columns_are_ANDed()
    {
        // If they were OR-ed, narrowing a second column would widen the result — the opposite of
        // what a merchant is doing.
        using var state = NewState();

        state.SetFilter(PolarisGridFilterDescriptor.Text("Name", "gold"));
        state.SetFilter(PolarisGridFilterDescriptor.In("Carat", [18]));

        Assert.Equal(2, state.TotalCount);
        Assert.All(state.Result.Items, alloy => Assert.Equal(18, alloy.Carat));
    }

    [Fact]
    public void A_multi_select_filter_accepts_any_of_its_values()
    {
        // The values inside one filter are OR-ed: ticking "9ct" and "18ct" asks for both, not for a
        // row that is somehow both.
        using var state = NewState();

        state.SetFilter(PolarisGridFilterDescriptor.In("Carat", [9, 18]));

        Assert.Equal(3, state.TotalCount);
    }

    [Fact]
    public void A_range_filter_includes_both_of_its_ends()
    {
        // An exclusive end silently drops the row the merchant aimed at — usually the one they were
        // looking for.
        using var state = NewState();

        state.SetFilter(PolarisGridFilterDescriptor.Between("Silver", 92.5m, 99.9m));

        Assert.Equal(["Fine silver 999", "Sterling 925"], state.Result.Items.Select(alloy => alloy.Name));
    }

    [Fact]
    public void A_range_whose_ends_are_the_wrong_way_round_is_still_a_range()
    {
        // Sliders and typed bounds can cross; treating the crossed interval literally would match
        // nothing at all.
        using var state = NewState();

        state.SetFilter(PolarisGridFilterDescriptor.Between("Silver", 99.9m, 92.5m));

        Assert.Equal(2, state.TotalCount);
    }

    [Fact]
    public void A_numeric_filter_compares_numbers_not_their_text()
    {
        // "9" sorts above "18" as text. This is the classic defect of a grid that stringifies its
        // values, and it is invisible unless the dataset spans a digit-count boundary.
        using var state = NewState();

        state.SetFilter(new PolarisGridFilterDescriptor("Carat", PolarisGridOperator.GreaterThan, 9));

        Assert.Equal(2, state.TotalCount);
        Assert.All(state.Result.Items, alloy => Assert.True(alloy.Carat > 9));
    }

    [Fact]
    public void A_filter_value_that_arrives_as_text_is_read_as_the_column_type()
    {
        // Every Polaris form control reports a string, so a numeric filter built from one must still
        // compare numerically rather than lexically.
        using var state = NewState();

        state.SetFilter(new PolarisGridFilterDescriptor("Silver", PolarisGridOperator.LessThan, "92.5"));

        Assert.Equal(4, state.TotalCount);
    }

    [Fact]
    public void A_filter_value_that_cannot_be_read_as_the_column_type_is_dropped()
    {
        // Applying it would compare "abc" against numbers and match nothing, which looks exactly
        // like an empty dataset. Dropping it leaves the grid usable and the mistake visible.
        using var state = NewState();

        state.SetFilter(new PolarisGridFilterDescriptor("Silver", PolarisGridOperator.GreaterThan, "abc"));

        Assert.Equal(SampleRows.Count, state.TotalCount);
    }

    [Fact]
    public void A_filter_on_a_column_that_does_not_exist_is_ignored()
    {
        // A saved view or a renamed column must not empty the grid.
        using var state = NewState();

        state.SetFilter(PolarisGridFilterDescriptor.Text("NoSuchColumn", "gold"));

        Assert.Equal(SampleRows.Count, state.TotalCount);
    }

    [Fact]
    public void An_empty_value_is_excluded_by_default_and_included_on_request()
    {
        // Null handling has to be a decision, not an accident: an empty cell is neither "contains
        // gold" nor "does not contain gold", and a grid that answers either way is guessing.
        using var state = NewState();

        state.SetFilter(PolarisGridFilterDescriptor.Text("Notes", "hall"));
        Assert.Equal(["Yellow gold 18ct"], state.Result.Items.Select(alloy => alloy.Name));

        state.SetFilter(new PolarisGridFilterDescriptor("Notes", PolarisGridOperator.Contains, "hall", IncludeNulls: true));

        // The one matching row, plus the three that have no note at all.
        Assert.Equal(4, state.TotalCount);
    }

    [Fact]
    public void A_null_test_selects_the_rows_with_no_value()
    {
        // The one filter that deliberately looks at emptiness, and the reason IncludeNulls cannot be
        // the only way to do it.
        using var state = NewState();

        state.SetFilter(new PolarisGridFilterDescriptor("Notes", PolarisGridOperator.IsNull));
        Assert.Equal(3, state.TotalCount);
        Assert.All(state.Result.Items, alloy => Assert.Null(alloy.Notes));

        state.SetFilter(new PolarisGridFilterDescriptor("Notes", PolarisGridOperator.IsNotNull));
        Assert.Equal(3, state.TotalCount);
        Assert.All(state.Result.Items, alloy => Assert.NotNull(alloy.Notes));
    }

    [Fact]
    public void The_global_search_matches_searchable_columns_and_ANDs_with_the_column_filters()
    {
        // The search box searches what is displayed; if it also searched a column the merchant
        // cannot see, the result would contain rows with no visible reason for matching.
        using var state = NewState();

        state.SetGlobalSearch("platinum");

        Assert.Equal(["Platinum 950"], state.Result.Items.Select(alloy => alloy.Name));

        state.SetFilter(PolarisGridFilterDescriptor.In("Active", [false]));

        Assert.Equal(0, state.TotalCount);
    }

    [Fact]
    public void The_global_search_skips_a_column_declared_unsearchable()
    {
        // An identifier column that matches a search for "1" would swamp every real match.
        using var state = NewState();
        state.RegisterColumn(Column("Reference", alloy => $"AL-{alloy.Id}", searchable: false));

        state.SetGlobalSearch("AL-");

        Assert.Equal(0, state.TotalCount);
    }

    // ── Sorting ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Sorting_orders_by_direction_and_is_stable_for_equal_values()
    {
        // A sort that is not stable reshuffles equal rows on every refresh, which reads as rows
        // moving on their own.
        using var state = NewState();

        state.SetSort("Carat", PolarisGridSortDirection.Ascending);
        Assert.Equal([0, 0, 0, 9, 18, 18], state.Result.Items.Select(alloy => alloy.Carat));

        state.SetSort("Carat", PolarisGridSortDirection.Descending);

        // Two 18ct alloys keep their relative order from the source snapshot.
        Assert.Equal(["Yellow gold 18ct", "White gold 18ct"], state.Result.Items.Take(2).Select(alloy => alloy.Name));
        Assert.Equal(18, state.Result.Items[0].Carat);
    }

    [Fact]
    public void Several_sorts_apply_in_priority_order()
    {
        // "Carat, then name" is two descriptors. Applied in the wrong order it sorts by name and
        // groups by nothing.
        using var state = NewState();

        state.SetSorts(
        [
            new PolarisGridSortDescriptor("Name", PolarisGridSortDirection.Ascending, Priority: 1),
            new PolarisGridSortDescriptor("Carat", PolarisGridSortDirection.Descending, Priority: 0),
        ]);

        var ordered = state.Result.Items.ToList();

        Assert.Equal(18, ordered[0].Carat);
        Assert.Equal("White gold 18ct", ordered[0].Name);
        Assert.Equal("Yellow gold 18ct", ordered[1].Name);
        Assert.Equal(18, ordered[1].Carat);
    }

    [Fact]
    public void An_empty_value_sorts_last_ascending_and_first_descending()
    {
        // Stated because it is a choice rather than an obvious truth: the comparer treats an empty
        // value as the greatest, so it lands at the bottom of an ascending column.
        using var state = NewState();

        state.SetSort("Notes", PolarisGridSortDirection.Ascending);
        Assert.Null(state.Result.Items[^1].Notes);

        state.SetSort("Notes", PolarisGridSortDirection.Descending);
        Assert.Null(state.Result.Items[0].Notes);
    }

    [Fact]
    public void Sorting_by_a_column_that_does_not_exist_changes_nothing()
    {
        // A stale saved order must not reorder the grid by an unknown key.
        using var state = NewState();

        state.SetSort("NoSuchColumn", PolarisGridSortDirection.Descending);

        Assert.Equal(SampleRows.Select(alloy => alloy.Name), state.Result.Items.Select(alloy => alloy.Name));
    }

    // ── Paging ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void The_total_count_is_taken_after_filtering_and_before_paging()
    {
        // Counting the page gives "1–10 of 10" and a pager that cannot offer a second page, which
        // is the single most common defect in a hand-written grid.
        using var state = NewState();
        state.SetPageSize(2);

        state.SetFilter(PolarisGridFilterDescriptor.Text("Name", "gold"));

        Assert.Equal(3, state.TotalCount);
        Assert.Equal(2, state.Result.Items.Count);
        Assert.Equal(2, state.PageCount);
    }

    [Fact]
    public void A_final_page_may_be_partial_and_a_page_past_the_end_is_clamped()
    {
        // Offer a page that does not exist and the merchant sees an empty grid with a page number
        // in its footer.
        using var state = NewState();
        state.SetPageSize(4);

        state.SetPage(1);
        Assert.Equal(2, state.Result.Items.Count);
        Assert.Equal(5, state.Result.FirstRecordNumber);
        Assert.Equal(6, state.Result.LastRecordNumber);

        state.SetPage(99);
        Assert.Equal(1, state.Query.PageIndex);
        Assert.False(state.HasNextPage);
    }

    [Fact]
    public void An_empty_result_reports_no_pages_at_all()
    {
        // One empty page is a pager showing "page 1 of 1" and a row range of "0–0 of 0".
        using var state = NewState();

        state.SetFilter(PolarisGridFilterDescriptor.Text("Name", "unobtainium"));

        Assert.Equal(0, state.TotalCount);
        Assert.Equal(0, state.PageCount);
        Assert.Equal(0, state.Result.FirstRecordNumber);
        Assert.Equal(0, state.Result.LastRecordNumber);
        Assert.False(state.HasNextPage);
        Assert.False(state.HasPreviousPage);
        Assert.True(state.Result.IsEmpty);
    }

    [Fact]
    public void A_filter_change_returns_to_the_first_page()
    {
        // Staying on page 4 of a result that now has one page shows nothing at all.
        using var state = NewState();
        state.SetPageSize(2);
        state.SetPage(2);

        state.SetFilter(PolarisGridFilterDescriptor.Text("Name", "gold"));

        Assert.Equal(0, state.Query.PageIndex);
    }

    [Fact]
    public void Changing_the_page_size_keeps_the_record_the_merchant_was_looking_at()
    {
        // "The second page" means a different record at a different size; what should be kept is the
        // merchant's place in the data, not the page number.
        using var state = NewState();
        state.SetPageSize(2);
        state.SetPage(2);

        var visible = state.Result.Items[0];
        Assert.Equal(5, state.Result.FirstRecordNumber);

        state.SetPageSize(3);

        // Page 2 of 2 holds records 5 and 6; at three per page that record is on page 1, which starts
        // at record 4 — still on screen rather than replaced by a page of strangers.
        Assert.Equal(4, state.Result.FirstRecordNumber);
        Assert.Contains(visible, state.Result.Items);
    }

    // ── State ────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Replacing_the_dataset_requeries_and_keeps_the_selection_by_key()
    {
        // A reload that clears the selection loses work; one that keeps a selection by position
        // selects whatever record now sits in that row.
        using var state = NewState();
        state.SetSelected(SampleRows[1], true);

        state.SetItems(SampleRows.Where(alloy => alloy.Id is 1 or 2));

        Assert.Equal(2, state.TotalCount);
        Assert.Equal(["Sterling 925"], state.Result.Items.Where(state.IsSelected).Select(alloy => alloy.Name));

        state.SetItems(SampleRows.Where(alloy => alloy.Id == 1));

        Assert.Empty(state.SelectedKeys);
    }

    [Fact]
    public void Unregistering_a_column_also_removes_its_filter_and_its_sort()
    {
        // A filter left pointing at a column that no longer exists would silently keep matching,
        // because an unknown field is ignored — the grid would be filtered and unable to say so.
        using var state = NewState();
        state.SetFilter(PolarisGridFilterDescriptor.Text("Notes", "hall"));
        state.SetSort("Notes", PolarisGridSortDirection.Ascending);

        state.UnregisterColumn("Notes");

        Assert.False(state.HasFilter("Notes"));
        Assert.Null(state.SortDirectionFor("Notes"));
        Assert.Equal(SampleRows.Count, state.TotalCount);
    }

    [Fact]
    public void Two_columns_with_the_same_id_are_refused()
    {
        // Ids identify filters and sorts; two columns sharing one makes both ambiguous, and the
        // symptom is a filter that appears to do nothing.
        using var state = NewState();

        var duplicate = Assert.Throws<ArgumentException>(() => state.RegisterColumn(Column("Name", alloy => alloy.Name)));
        Assert.Contains("Name", duplicate.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_range_bounds_come_from_the_whole_dataset_and_ignore_filters()
    {
        // Bounds taken from the filtered rows shrink as the filter narrows, so a slider can only
        // ever be moved inwards and the range can never be widened again.
        using var state = NewState();
        state.SetFilter(PolarisGridFilterDescriptor.Between("Silver", 90m, 95m));

        var (minimum, maximum) = state.GetNumericBounds("Silver");

        Assert.Equal(0m, minimum);
        Assert.Equal(99.9m, maximum);
    }

    [Fact]
    public void A_multi_select_offers_the_distinct_values_sorted()
    {
        // An option that vanishes once it is ticked can never be un-ticked.
        using var state = NewState();
        state.SetFilter(PolarisGridFilterDescriptor.In("Carat", [0]));

        Assert.Equal([0, 9, 18], state.GetDistinctValues("Carat"));
    }

    [Fact]
    public void The_loading_and_error_state_notify_like_everything_else()
    {
        // These two are the grid's own news, not the query's: a page that starts loading has to be able
        // to redraw the spinner without touching the data. They are settable only through a method, so
        // there is no way to change them silently — which is how the sample app's first live-data page
        // held a spinner for ever.
        using var state = NewState();
        var notifications = 0;
        state.Changed += () => notifications++;

        state.SetLoading(true);
        Assert.True(state.IsLoading);
        Assert.Equal(1, notifications);

        // The same value again is not a change, or a render loop would follow.
        state.SetLoading(true);
        Assert.Equal(1, notifications);

        state.SetError("The database is unreachable.");
        Assert.Equal("The database is unreachable.", state.ErrorMessage);
        Assert.Equal(2, notifications);

        state.SetLoading(false);
        state.SetError(null);
        Assert.False(state.IsLoading);
        Assert.Null(state.ErrorMessage);
        Assert.Equal(4, notifications);
    }

    [Fact]
    public void Every_mutation_notifies_once()
    {
        // A component that subscribes to the state re-renders on every notification; two per change
        // is a wasted query and, during a drag, a visible stutter.
        using var state = NewState();
        var notifications = 0;
        state.Changed += () => notifications++;

        state.SetFilter(PolarisGridFilterDescriptor.Text("Name", "gold"));
        Assert.Equal(1, notifications);

        state.ClearFilter("Name");
        Assert.Equal(2, notifications);

        state.SetGlobalSearch("gold");
        Assert.Equal(3, notifications);

        // The same value again is not a change.
        state.SetGlobalSearch("gold");
        Assert.Equal(3, notifications);

        state.ClearAllFilters();
        Assert.Equal(4, notifications);
    }

    [Fact]
    public void The_date_bounds_of_a_column_are_dates_without_times()
    {
        // A bound of "the 5th" must include the whole of the 5th, so a timestamp is truncated rather
        // than compared against a local midnight.
        using var state = NewState();

        var (minimum, maximum) = state.GetDateBounds("Added");

        Assert.Equal(new DateOnly(2026, 1, 5), minimum);
        Assert.Equal(new DateOnly(2026, 6, 30), maximum);
    }

    // ── Fixtures ─────────────────────────────────────────────────────────────────────────────

    private static readonly List<Alloy> SampleRows =
    [
        new(1, "Fine silver 999", 0, 99.9m, true, new DateOnly(2026, 1, 5), "fine"),
        new(2, "Sterling 925", 0, 92.5m, true, new DateOnly(2026, 2, 11)),
        new(3, "Yellow gold 9ct", 9, 0m, false, new DateOnly(2026, 3, 2), "soft"),
        new(4, "Yellow gold 18ct", 18, 0m, true, new DateOnly(2026, 4, 18), "hallmarked"),
        new(5, "White gold 18ct", 18, 0m, true, new DateOnly(2026, 5, 1)),
        new(6, "Platinum 950", 0, 0m, true, new DateOnly(2026, 6, 30)),
    ];

    private static PolarisGridState<Alloy> NewState()
    {
        var state = new PolarisGridState<Alloy>(SampleRows, alloy => alloy.Id);

        state.RegisterColumn(Column("Name", alloy => alloy.Name));
        state.RegisterColumn(Column("Carat", alloy => alloy.Carat, PolarisGridFilterType.MultiSelect));
        state.RegisterColumn(Column("Silver", alloy => alloy.Silver, PolarisGridFilterType.Range));
        state.RegisterColumn(Column("Active", alloy => alloy.Active, PolarisGridFilterType.Boolean));
        state.RegisterColumn(Column("Notes", alloy => alloy.Notes, PolarisGridFilterType.Text));
        state.RegisterColumn(Column("Added", alloy => alloy.Added, PolarisGridFilterType.DateRange));

        return state;
    }

    private static PolarisGridColumnDefinition<Alloy> Column<TValue>(
        string id,
        Func<Alloy, TValue> read,
        PolarisGridFilterType filterType = PolarisGridFilterType.None,
        bool searchable = true)
        => new()
        {
            Id = id,
            Title = id,
            ValueGetter = alloy => read(alloy),
            ValueType = typeof(TValue),
            FilterType = filterType,
            Searchable = searchable,
        };

    private sealed record Alloy(
        int Id,
        string Name,
        int Carat,
        decimal Silver,
        bool Active,
        DateOnly Added,
        string? Notes = null);
}
