using Bunit;
using Polaris.Blazor.Components;
using Polaris.Blazor.DataGrid;
using Xunit;

namespace Polaris.Blazor.Tests;

/// <summary>
/// Rendering tests for <see cref="PolarisDataPager"/>.
/// </summary>
/// <remarks>
/// A pager is judged by the numbers it shows, and every one of them is a chance to disagree with the
/// table: a range that counts the page instead of the result, a page count that is one too high, or
/// a current-page button that is not marked as current. Those are asserted here; a real click is not,
/// because the buttons forward their clicks through the interop module — the browser check covers
/// that half.
/// </remarks>
public sealed class DataPagerRenderingTests
{
    [Fact]
    public void The_pager_reports_the_visible_range_and_the_page_count()
    {
        // "1–2 of 6" is the only place the merchant learns both the size of the result and their
        // place in it; counting the page gives "1–2 of 2" and a pager that cannot reach page three.
        using var context = new PolarisTestContext();
        using var state = NewState(pageSize: 2);

        var pager = RenderPager(context, state);

        Assert.Contains("1–2 of 6", pager.Markup, StringComparison.Ordinal);

        RenderOnDispatcher(pager, () => state.SetPage(2));

        Assert.Contains("5–6 of 6", pager.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void The_first_and_previous_buttons_are_disabled_on_the_first_page()
    {
        // An enabled button that does nothing is indistinguishable from a broken one, and a disabled
        // button also leaves the tab order.
        using var context = new PolarisTestContext();
        using var state = NewState(pageSize: 2);

        var pager = RenderPager(context, state);

        Assert.True(StepButton(pager, "First page").HasAttribute("disabled"));
        Assert.True(StepButton(pager, "Previous page").HasAttribute("disabled"));
        Assert.False(StepButton(pager, "Next page").HasAttribute("disabled"));
        Assert.False(StepButton(pager, "Last page").HasAttribute("disabled"));

        RenderOnDispatcher(pager, () => state.SetPage(2));

        Assert.False(StepButton(pager, "First page").HasAttribute("disabled"));
        Assert.True(StepButton(pager, "Next page").HasAttribute("disabled"));
        Assert.True(StepButton(pager, "Last page").HasAttribute("disabled"));
    }

    [Fact]
    public void The_current_page_is_marked_and_is_not_just_a_number()
    {
        // Colour or weight alone does not announce anything, so the current page is named as current
        // and rendered with a different emphasis.
        using var context = new PolarisTestContext();
        using var state = NewState(pageSize: 2);

        var pager = RenderPager(context, state);

        Assert.Contains("Page 1, current page", pager.Markup, StringComparison.Ordinal);
        Assert.Equal("secondary", PageButton(pager, "Page 1, current page").GetAttribute("variant"));
        Assert.Equal("tertiary", PageButton(pager, "Page 2").GetAttribute("variant"));
    }

    [Fact]
    public void A_long_result_shows_the_first_and_last_pages_and_an_ellipsis()
    {
        // The window has to follow the current page without losing the two ends of a long list, and
        // "…" must not be a button: it stands for pages the labels do not describe.
        using var context = new PolarisTestContext();
        using var state = NewState(rows: 60, pageSize: 10);
        state.SetPage(2);

        var pager = RenderPager(context, state);

        var labels = pager.FindAll("s-button")
            .Select(button => button.GetAttribute("accessibilityLabel"))
            .Where(label => label is not null && label.StartsWith("Page ", StringComparison.Ordinal))
            .ToList();

        Assert.Contains("Page 1", labels);
        Assert.Contains("Page 6", labels);
        Assert.Contains("Page 3, current page", labels);

        // Five numbered buttons at most, and the ellipsis is text rather than a control.
        Assert.True(labels.Count <= 5, $"{labels.Count} numbered buttons were rendered.");
        Assert.Contains("…", pager.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void The_page_size_select_offers_the_configured_sizes_and_the_current_one()
    {
        // A select whose selected value is not among its options shows the first option instead,
        // describing a page size that is not in force.
        using var context = new PolarisTestContext();
        using var state = NewState(pageSize: 2);

        var pager = RenderPager(context, state);

        Assert.Equal(5, pager.FindAll("s-option").Count);
        Assert.Equal("2", context.Interop.SetProperties["s-select"][^1]["value"]);
        Assert.Contains("Rows per page", pager.Find("s-select").GetAttribute("label"), StringComparison.Ordinal);

        // The label is for a screen reader: the select sits in a row of numbers, where a visible
        // label would push the controls apart.
        Assert.Equal("exclusive", pager.Find("s-select").GetAttribute("labelAccessibilityVisibility"));
    }

    [Fact]
    public void Hiding_the_numbered_buttons_leaves_a_page_counter_in_their_place()
    {
        // Without either, the merchant can move but cannot tell where they are.
        using var context = new PolarisTestContext();
        using var state = NewState(pageSize: 2);

        var pager = context.Render<PolarisDataPager>(parameters => parameters
            .Add(p => p.State, state)
            .Add(p => p.ShowPageNumbers, false));

        Assert.Contains("Page 1 of 3", pager.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Page 1, current page", pager.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void An_empty_result_says_so_rather_than_showing_zero_of_zero()
    {
        // "0–0 of 0" is a range that reads as a defect; there is no range.
        using var context = new PolarisTestContext();
        using var state = NewState();
        state.SetFilter(PolarisGridFilterDescriptor.Text("Name", "unobtainium"));

        var pager = RenderPager(context, state);

        Assert.Contains("No records", pager.Markup, StringComparison.Ordinal);
        Assert.True(StepButton(pager, "Next page").HasAttribute("disabled"));
        Assert.True(StepButton(pager, "Previous page").HasAttribute("disabled"));
    }

    [Fact]
    public void The_record_range_can_be_turned_off_without_losing_the_controls()
    {
        // A pager in a page's action bar usually wants the buttons alone.
        using var context = new PolarisTestContext();
        using var state = NewState(pageSize: 2);

        var pager = context.Render<PolarisDataPager>(parameters => parameters
            .Add(p => p.State, state)
            .Add(p => p.ShowRecordRange, false));

        Assert.DoesNotContain("1–2 of 6", pager.Markup, StringComparison.Ordinal);
        Assert.NotEmpty(pager.FindAll("s-button"));
    }

    [Fact]
    public void A_change_to_the_shared_state_redraws_the_pager()
    {
        // The grid and the filter mutate the same state; a pager that did not listen would leave the
        // merchant on page 3 of a result that now has one page.
        using var context = new PolarisTestContext();
        using var state = NewState(pageSize: 2);

        var pager = RenderPager(context, state);
        Assert.Contains("1–2 of 6", pager.Markup, StringComparison.Ordinal);

        RenderOnDispatcher(pager, () => state.SetFilter(PolarisGridFilterDescriptor.Text("Name", "gold")));

        // Three of the six rows are gold, and the range is the result's, not the page's.
        Assert.Contains("1–2 of 3", pager.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void The_pager_is_a_navigation_landmark()
    {
        // So that a screen reader can jump to it and a keyboard user can find it, rather than
        // meeting four anonymous buttons after the last table cell.
        using var context = new PolarisTestContext();
        using var state = NewState(pageSize: 2);

        var pager = RenderPager(context, state);

        var navigation = pager.Find("nav");
        Assert.Equal("Pagination", navigation.GetAttribute("aria-label"));
    }

    // ── Fixtures ─────────────────────────────────────────────────────────────────────────────

    private static PolarisGridState<Row> NewState(int rows = 6, int pageSize = 25)
    {
        var state = new PolarisGridState<Row>(
            Enumerable.Range(1, rows).Select(id => new Row(id, id % 2 == 0 ? "gold" : "silver")),
            row => row.Id);

        state.RegisterColumn(new PolarisGridColumnDefinition<Row>
        {
            Id = "Name",
            Title = "Name",
            ValueGetter = row => row.Name,
            ValueType = typeof(string),
            FilterType = PolarisGridFilterType.Text,
        });

        state.SetPageSize(pageSize);
        return state;
    }

    private static IRenderedComponent<PolarisDataPager> RenderPager(PolarisTestContext context, PolarisGridState state)
        => context.Render<PolarisDataPager>(parameters => parameters.Add(p => p.State, state));

    private static AngleSharp.Dom.IElement StepButton<TRendered>(IRenderedComponent<TRendered> pager, string label)
        where TRendered : Microsoft.AspNetCore.Components.IComponent
        => pager.FindAll("s-button").Single(button => button.GetAttribute("accessibilityLabel") == label);

    /// <summary>
    /// Changes the state the way a handler does: on the renderer's dispatcher, then waits for the
    /// redraw. A click on a pager button does exactly this.
    /// </summary>
    private static void RenderOnDispatcher<TRendered>(IRenderedComponent<TRendered> component, Action mutate)
        where TRendered : Microsoft.AspNetCore.Components.IComponent
        => component.InvokeAsync(mutate).GetAwaiter().GetResult();

    private static AngleSharp.Dom.IElement PageButton<TRendered>(IRenderedComponent<TRendered> pager, string label)
        where TRendered : Microsoft.AspNetCore.Components.IComponent
        => pager.FindAll("s-button").Single(button => button.GetAttribute("accessibilityLabel") == label);

    private sealed record Row(int Id, string Name);
}
