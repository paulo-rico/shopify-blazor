using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Polaris.Blazor.Components;
using Polaris.Blazor.DataGrid;
using Xunit;

namespace Polaris.Blazor.Tests;

/// <summary>
/// Rendering tests for <see cref="PolarisDataFilter"/>.
/// </summary>
/// <remarks>
/// <para>
/// The filter is the component that decides which control a column gets and what a merchant's change
/// becomes, so what is asserted here is the mapping: one control per filter type, a chip per applied
/// filter, and an apply mode that really does defer. A click on a Polaris control arrives through the
/// interop module, so the control's own handler is exercised through
/// <see cref="PolarisDataFilter.SetFilterAsync"/> — the entry point a click ends up in — except for
/// the range slider, whose inputs are native and can be driven directly.
/// </para>
/// <para>
/// Every test states the failure it prevents.
/// </para>
/// </remarks>
public sealed class DataFilterRenderingTests
{
    [Fact]
    public void The_bar_shows_a_search_box_and_a_button_that_opens_the_panel()
    {
        // The panel is opened declaratively with commandFor, so no JavaScript of this library's own is
        // involved — and a button that pointed at nothing would look like a button that does nothing.
        using var context = new PolarisTestContext();
        using var state = NewState();

        var filter = RenderFilter(context, state);

        Assert.NotNull(filter.Find("s-search-field"));

        var trigger = filter.FindAll("s-button").First();
        var panelId = trigger.GetAttribute("commandFor");
        Assert.False(string.IsNullOrEmpty(panelId));
        Assert.Equal("--show", trigger.GetAttribute("command"));

        Assert.Equal(panelId, filter.Find("s-popover").GetAttribute("id"));
    }

    [Fact]
    public void The_panel_insets_its_controls_from_the_popover_edge()
    {
        // Measured in a browser: an s-popover paints no padding of its own — its frame computes to 0px on
        // every side and has the same rect as this panel — so the controls sat flush against the
        // popover's border, a text field's own border touching the frame's, with nothing in the markup to
        // say so. A layout fault reports through no other channel, so what is asserted here is the one
        // thing a component test can see: that the inset is stated, at Polaris's space-400 step, and that
        // it is inside the box so the height cap still caps. The real geometry is checked in a browser by
        // tools/verify-in-browser.mjs.
        using var context = new PolarisTestContext();
        using var state = NewState();

        var filter = RenderFilter(context, state);

        var style = filter.Find("[data-polaris-filter-panel]").GetAttribute("style") ?? string.Empty;

        Assert.Contains("padding:var(--p-space-400,16px)", style, StringComparison.Ordinal);
        Assert.Contains("box-sizing:border-box", style, StringComparison.Ordinal);
    }

    [Fact]
    public void Each_filterable_column_gets_the_control_its_type_asks_for()
    {
        // A single control for every column would filter the wrong way round: a text box on a numeric
        // column compares numbers as strings, and a range on a category offers a slider nobody can use.
        using var context = new PolarisTestContext();
        using var state = NewState();

        var filter = RenderFilter(context, state);

        Assert.Single(filter.FindAll("s-text-field"));
        Assert.Single(filter.FindAll("s-choice-list"));
        Assert.Single(filter.FindAll("[data-polaris-range-slider]"));
        Assert.Single(filter.FindAll("s-select"));
        Assert.Equal(2, filter.FindAll("s-date-field").Count);
    }

    [Fact]
    public void A_column_with_no_filter_type_offers_no_control()
    {
        // A computed or free-text column that offers a filter can only filter wrongly, and the
        // merchant has no way to know that is what happened.
        using var context = new PolarisTestContext();
        using var state = NewState();

        var filter = RenderFilter(context, state);

        Assert.DoesNotContain("Notes", filter.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void A_multi_select_offers_the_distinct_values_of_the_dataset()
    {
        // Options derived from the data are the ones that can actually match; a hand-written list
        // drifts the moment a new carat is added.
        using var context = new PolarisTestContext();
        using var state = NewState();

        var filter = RenderFilter(context, state);

        var choices = filter.FindAll("s-choice")
            .Select(choice => choice.GetAttribute("value"))
            .ToList();

        Assert.Equal(["0", "9", "18"], choices);
    }

    [Fact]
    public void A_range_slider_is_bounded_by_the_whole_dataset()
    {
        // Bounds taken from the filtered rows move inwards as the filter narrows, and the range can
        // then never be widened again. This is the one bug a slider-based filter always has first.
        using var context = new PolarisTestContext();
        using var state = NewState();

        var filter = RenderFilter(context, state);

        var track = filter.Find("[data-polaris-range-track]");
        var lower = track.QuerySelector("input[data-polaris-range-handle=lower]")!;

        Assert.Equal("0", lower.GetAttribute("min"));
        Assert.Equal("99.9", lower.GetAttribute("max"));
    }

    [Fact]
    public void An_applied_filter_becomes_a_removable_chip_that_says_what_it_does()
    {
        // A filter that is applied but not visible is reported as missing data. The chip has to name
        // the column and the value, and be removable without opening the panel.
        using var context = new PolarisTestContext();
        using var state = NewState();
        state.SetFilter(PolarisGridFilterDescriptor.Text("Name", "gold"));

        var filter = RenderFilter(context, state);

        var chips = filter.FindAll("s-clickable-chip");
        Assert.Single(chips);
        Assert.Contains("Name contains", chips[0].TextContent, StringComparison.Ordinal);
        Assert.Contains("gold", chips[0].TextContent, StringComparison.Ordinal);
        Assert.True(chips[0].HasAttribute("removable"));
    }

    [Fact]
    public void Clear_all_is_offered_only_when_something_is_filtered_and_clears_everything()
    {
        // A permanently visible "clear all" is a button that usually does nothing; and one that
        // clears the chips but not the search leaves the result narrowed with no explanation.
        using var context = new PolarisTestContext();
        using var state = NewState();

        var filter = RenderFilter(context, state);
        Assert.DoesNotContain("Clear all", filter.Markup, StringComparison.Ordinal);

        RenderOnDispatcher(filter, () =>
        {
            state.SetFilter(PolarisGridFilterDescriptor.Text("Name", "gold"));
            state.SetGlobalSearch("silver");
        });

        Assert.Contains("Clear all", filter.Markup, StringComparison.Ordinal);

        filter.InvokeAsync(() => filter.Instance.ClearAllAsync()).GetAwaiter().GetResult();

        Assert.Empty(state.Query.Filters);
        Assert.Null(state.Query.GlobalSearch);
    }

    [Fact]
    public void An_immediate_change_reaches_the_query_at_once()
    {
        // The default for a checkbox or a select: waiting for a timer on a control that commits when
        // it changes makes the grid feel broken.
        using var context = new PolarisTestContext();
        using var state = NewState();

        var filter = RenderFilter(context, state, PolarisGridFilterApplyMode.Immediate);

        filter.InvokeAsync(() => filter.Instance.SetFilterAsync("Carat", PolarisGridFilterDescriptor.In("Carat", [9]))).GetAwaiter().GetResult();

        Assert.NotNull(state.Query.FilterFor("Carat"));
        Assert.Equal(1, state.TotalCount);
    }

    [Fact]
    public async Task A_debounced_change_waits_for_typing_to_stop_and_then_applies()
    {
        // Applying on every keystroke runs the whole query once per character. Waiting for ever is
        // the other failure, so the deferred apply is asserted as well as the deferral.
        using var context = new PolarisTestContext();
        using var state = NewState();

        var filter = RenderFilter(context, state, PolarisGridFilterApplyMode.Debounced, debounceMilliseconds: 5);

        await filter.InvokeAsync(() => filter.Instance.SetFilterAsync("Name", PolarisGridFilterDescriptor.Text("Name", "gold")));

        Assert.Null(state.Query.FilterFor("Name"));

        await WaitForAsync(() => state.Query.FilterFor("Name") is not null);

        Assert.Equal(3, state.TotalCount);
    }

    [Fact]
    public async Task A_manual_change_waits_for_apply_and_neither_narrows_nor_chips()
    {
        // Manual mode is a promise that nothing happens until Apply. A filter that applied early
        // would make the Apply button a decoration, and the chip would appear before the merchant
        // meant it.
        using var context = new PolarisTestContext();
        using var state = NewState();

        var filter = RenderFilter(context, state, PolarisGridFilterApplyMode.Manual);

        await filter.InvokeAsync(() => filter.Instance.SetFilterAsync("Name", PolarisGridFilterDescriptor.Text("Name", "gold")));

        Assert.Null(state.Query.FilterFor("Name"));
        Assert.Single(filter.FindAll("s-button"), button => button.TextContent.Contains("Apply", StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_search_box_shows_a_term_that_was_set_elsewhere()
    {
        // A "clear all" from the page, a restored saved view, or a deep link sets the term without
        // this component being involved. A search box that kept showing the old text would contradict
        // the rows beneath it.
        using var context = new PolarisTestContext();
        using var state = NewState();

        var filter = RenderFilter(context, state);

        await filter.InvokeAsync(() => state.SetGlobalSearch("silver"));

        var written = context.Interop.SetProperties["s-search-field"][^1];
        Assert.Equal("silver", written["value"]);
    }

    [Fact]
    public async Task Moving_a_range_handle_writes_an_inclusive_interval()
    {
        // The slider knows nothing about filters, so this is the join between them: a range becomes a
        // Between descriptor, and both ends are inclusive.
        using var context = new PolarisTestContext();
        using var state = NewState();

        var filter = RenderFilter(context, state, PolarisGridFilterApplyMode.Immediate);

        var upper = filter.Find("input[data-polaris-range-handle=upper]");
        await filter.InvokeAsync(() => upper.TriggerEventAsync("onchange", new ChangeEventArgs { Value = "15" }));

        var applied = state.Query.FilterFor("Silver");

        Assert.NotNull(applied);
        Assert.Equal(PolarisGridOperator.Between, applied.Operator);

        // Compared as numbers: the slider snaps to its step, so 0 arrives as 0.0 — numerically the
        // same bound, and the descriptor is data rather than text.
        Assert.Equal(0m, Convert.ToDecimal(applied.Value, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(15m, Convert.ToDecimal(applied.SecondValue, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task A_full_range_slider_removes_its_filter_instead_of_matching_everything()
    {
        // A criterion that cannot exclude anything is not a filter: it makes the grid recompute and
        // leaves a chip claiming the result is narrowed when it is not.
        using var context = new PolarisTestContext();
        using var state = NewState();

        var filter = RenderFilter(context, state, PolarisGridFilterApplyMode.Immediate);

        var upper = filter.Find("input[data-polaris-range-handle=upper]");
        await filter.InvokeAsync(() => upper.TriggerEventAsync("onchange", new ChangeEventArgs { Value = "15" }));
        Assert.NotNull(state.Query.FilterFor("Silver"));

        upper = filter.Find("input[data-polaris-range-handle=upper]");
        await filter.InvokeAsync(() => upper.TriggerEventAsync("onchange", new ChangeEventArgs { Value = "99.9" }));

        Assert.Null(state.Query.FilterFor("Silver"));
    }

    [Fact]
    public void A_hidden_column_still_offers_its_filter_and_keeps_it()
    {
        // Visibility says what is DRAWN, not what can be filtered on — and the two being tied together was
        // wrong in both directions. A hidden column's filter kept applying while its control disappeared, so
        // the result was narrowed by something the merchant could neither see nor clear; and a facet behind
        // a badge could not be filtered on at all, because it had no column.
        using var context = new PolarisTestContext();
        using var state = NewState();
        state.SetFilter(PolarisGridFilterDescriptor.In("Carat", [18]));

        var column = state.ColumnDefinitions[1];
        column.Visible = false;

        var filter = RenderFilter(context, state);

        Assert.NotEmpty(filter.FindAll("s-choice-list"));
        Assert.NotNull(state.Query.FilterFor("Carat"));
    }

    [Fact]
    public void A_hidden_column_is_not_drawn_in_the_table_but_is_offered_a_filter()
    {
        // The products list's shape: "In Shopify" is a facet a merchant filters by and never needs as a
        // column, because the cell already says so in words. Declared once, used by the filter bar only.
        using var context = new PolarisTestContext();
        using var state = NewState();

        state.RegisterColumn(new PolarisGridColumnDefinition<Alloy>
        {
            Id = "Gone",
            Title = "Gone from Shopify",
            ValueGetter = alloy => alloy.Carat > 18,
            ValueType = typeof(bool),
            Sortable = false,
            FilterType = PolarisGridFilterType.Boolean,
            Visible = false,
        });

        var filter = RenderFilter(context, state, PolarisGridFilterApplyMode.Immediate);

        // A select labelled with the hidden column's title — the boolean control — and no chip yet.
        var labels = filter.FindAll("s-select").Select(select => select.GetAttribute("label")).ToArray();

        Assert.Contains("Gone from Shopify", labels);

        // The filter reaches the query like any other, and shows up as a chip that can be removed.
        state.SetFilter(PolarisGridFilterDescriptor.Equals("Gone", true));

        filter = RenderFilter(context, state, PolarisGridFilterApplyMode.Immediate);

        Assert.Contains("Gone from Shopify", filter.Markup, StringComparison.Ordinal);
        Assert.NotEmpty(filter.FindAll("s-clickable-chip"));
    }

    [Fact]
    public void The_search_box_can_be_turned_off_without_losing_the_panel()
    {
        // A screen that filters through its own controls, or searches through the Admin's, does not
        // want a second search box.
        using var context = new PolarisTestContext();
        using var state = NewState();

        var filter = context.Render<PolarisDataFilter>(parameters => parameters
            .Add(p => p.State, state)
            .Add(p => p.ShowSearch, false));

        Assert.Empty(filter.FindAll("s-search-field"));
        Assert.NotEmpty(filter.FindAll("s-popover"));
    }

    [Fact]
    public async Task The_boolean_control_writes_a_real_boolean_and_reads_one_back()
    {
        // Two halves of one contract. A filter that stored the option's TEXT compared fine — the engine
        // coerces — but anything that describes or formats it spoke about the string: measured, the chip on
        // the products list read "In Shopify is false" beside a control offering Yes and No. And a boolean
        // seeded by a page (`Equals(field, true)`) has to come back as the Yes option selected, or the
        // panel shows no selection for a filter that is plainly applied.
        using var context = new PolarisTestContext();
        using var state = NewState();

        var filter = RenderFilter(context, state, PolarisGridFilterApplyMode.Immediate);

        // The fixture has one boolean column, so its select is the only one that attached. The callback runs
        // on the renderer's dispatcher, exactly as the real interop marshals it.
        await filter.InvokeAsync(() => context.Interop.RaiseAsync("s-select", "change", "true"));

        Assert.Equal(true, state.Query.FilterFor("Active")?.Value);

        // Seeded rather than clicked: the control must adopt it. "True" would match no option, and a
        // Polaris select takes its value as a DOM property, so the interop call is where the answer is.
        filter = RenderFilter(context, state, PolarisGridFilterApplyMode.Immediate);

        Assert.Single(filter.FindAll("s-select"));
        Assert.Equal("true", context.Interop.SetProperties["s-select"][^1]["value"]);
    }

    [Fact]
    public void Extra_actions_are_rendered_beside_the_filters()
    {
        // A list screen usually puts its export or "new" button on the same row as the filters.
        using var context = new PolarisTestContext();
        using var state = NewState();

        var filter = context.Render<PolarisDataFilter>(parameters => parameters
            .Add(p => p.State, state)
            .Add(p => p.Actions, builder => builder.AddContent(0, "an action")));

        Assert.Contains("an action", filter.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void A_range_chip_writes_both_bounds_to_two_decimals()
    {
        // The chip and the range control's Min and Max fields are the only two places a bound is shown, and
        // the two must agree: fields reading "4.17" beside a chip reading "4.1666667" look like the filter
        // is not what the panel said. A filter's bounds are the cheapest and dearest row in the data, so
        // they are rarely numbers anybody typed — which is why an unformatted column needs a default at all.
        using var context = new PolarisTestContext();
        using var state = NewState();
        RegisterCostColumn(state);

        state.SetFilter(PolarisGridFilterDescriptor.InRange("Cost", new PolarisNumberRange(4.1666667m, 480m)));

        var filter = RenderFilter(context, state);

        Assert.Contains("Cost is 4.17 to 480.00", filter.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void A_range_chip_rounds_a_halfway_bound_away_from_zero()
    {
        // `decimal.ToString("F2")` rounds to even, so 4.165 would read "4.16" — a penny under the number a
        // merchant writes down, and the same rule the range control's own fields follow.
        using var context = new PolarisTestContext();
        using var state = NewState();
        RegisterCostColumn(state);

        state.SetFilter(PolarisGridFilterDescriptor.InRange("Cost", new PolarisNumberRange(4.165m, 4.175m)));

        var filter = RenderFilter(context, state);

        Assert.Contains("Cost is 4.17 to 4.18", filter.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void A_range_chip_keeps_the_format_the_column_states()
    {
        // Filling in a default must not overrule a stated intent: this column asks for one decimal, and a
        // currency or whole-number format would be just as deliberate. The chip describes the cell, so the
        // column's format is the one that wins.
        using var context = new PolarisTestContext();
        using var state = NewState();

        state.SetFilter(PolarisGridFilterDescriptor.InRange("Silver", new PolarisNumberRange(0m, 15m)));

        var filter = RenderFilter(context, state);

        Assert.Contains("Silver % is 0.0 to 15.0", filter.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void A_date_range_chip_is_not_a_number_and_keeps_its_own_text()
    {
        // The same code path draws a date range, whose bounds are dates: running those through the number
        // formatter would have to fail somehow — an exception, or a fabricated 0. So the chip must write
        // exactly what the column's own formatter writes, which is what the cells show.
        using var context = new PolarisTestContext();
        using var state = NewState();
        var added = state.ColumnDefinitions.Single(column => column.Id == "Added");

        state.SetFilter(PolarisGridFilterDescriptor.Between(
            "Added",
            new DateOnly(2024, 1, 1),
            new DateOnly(2024, 2, 1)));

        var filter = RenderFilter(context, state);

        Assert.Contains(
            string.Concat(
                "Added is ",
                added.FormatValue(new DateOnly(2024, 1, 1)),
                " to ",
                added.FormatValue(new DateOnly(2024, 2, 1))),
            filter.Markup,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// A decimal range column that states no format — the shape most grids declare, because the cell draws
    /// its own currency text in a template.
    /// </summary>
    private static void RegisterCostColumn(PolarisGridState<Alloy> state)
        => state.RegisterColumn(new PolarisGridColumnDefinition<Alloy>
        {
            Id = "Cost",
            Title = "Cost",
            ValueGetter = alloy => alloy.Silver * 2,
            ValueType = typeof(decimal),
            FilterType = PolarisGridFilterType.Range,
        });

    // ── Fixtures ─────────────────────────────────────────────────────────────────────────────

    private static readonly List<Alloy> SampleRows =
    [
        new(1, "Fine silver 999", 0, 99.9m, true),
        new(2, "Sterling 925", 0, 92.5m, true),
        new(3, "Yellow gold 9ct", 9, 0m, false),
        new(4, "Yellow gold 18ct", 18, 0m, true),
        new(5, "White gold 18ct", 18, 0m, true),
    ];

    private static PolarisGridState<Alloy> NewState()
    {
        var state = new PolarisGridState<Alloy>(SampleRows, alloy => alloy.Id);

        state.RegisterColumn(new PolarisGridColumnDefinition<Alloy>
        {
            Id = "Name",
            Title = "Name",
            ValueGetter = alloy => alloy.Name,
            ValueType = typeof(string),
            FilterType = PolarisGridFilterType.Text,
        });

        state.RegisterColumn(new PolarisGridColumnDefinition<Alloy>
        {
            Id = "Carat",
            Title = "Carat",
            ValueGetter = alloy => alloy.Carat,
            ValueType = typeof(int),
            FilterType = PolarisGridFilterType.MultiSelect,
        });

        state.RegisterColumn(new PolarisGridColumnDefinition<Alloy>
        {
            Id = "Silver",
            Title = "Silver %",
            ValueGetter = alloy => alloy.Silver,
            ValueType = typeof(decimal),
            FilterType = PolarisGridFilterType.Range,
            Format = "N1",
        });

        state.RegisterColumn(new PolarisGridColumnDefinition<Alloy>
        {
            Id = "Active",
            Title = "Active",
            ValueGetter = alloy => alloy.Active,
            ValueType = typeof(bool),
            FilterType = PolarisGridFilterType.Boolean,
        });

        state.RegisterColumn(new PolarisGridColumnDefinition<Alloy>
        {
            Id = "Added",
            Title = "Added",
            ValueGetter = alloy => alloy.Added,
            ValueType = typeof(DateOnly),
            FilterType = PolarisGridFilterType.DateRange,
        });

        state.RegisterColumn(new PolarisGridColumnDefinition<Alloy>
        {
            Id = "Notes",
            Title = "Notes",
            ValueGetter = alloy => alloy.Notes,
            ValueType = typeof(string),
            FilterType = PolarisGridFilterType.None,
        });

        return state;
    }

    private static IRenderedComponent<PolarisDataFilter> RenderFilter(
        PolarisTestContext context,
        PolarisGridState state,
        PolarisGridFilterApplyMode applyMode = PolarisGridFilterApplyMode.Debounced,
        int debounceMilliseconds = 300)
        => context.Render<PolarisDataFilter>(parameters => parameters
            .Add(p => p.State, state)
            .Add(p => p.ApplyMode, applyMode)
            .Add(p => p.DebounceMilliseconds, debounceMilliseconds));

    private static void RenderOnDispatcher<TRendered>(IRenderedComponent<TRendered> component, Action mutate)
        where TRendered : IComponent
        => component.InvokeAsync(mutate).GetAwaiter().GetResult();

    private static async Task WaitForAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 200; attempt++)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(10);
        }

        Assert.Fail("The deferred change never reached the query.");
    }

    private sealed record Alloy(
        int Id,
        string Name,
        int Carat,
        decimal Silver,
        bool Active,
        DateOnly Added = default,
        string? Notes = null);
}
