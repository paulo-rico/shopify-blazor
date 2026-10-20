using System.Globalization;
using System.Linq.Expressions;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Polaris.Blazor.Components;
using Polaris.Blazor.DataGrid;
using Xunit;

namespace Polaris.Blazor.Tests;

/// <summary>
/// Rendering tests for <see cref="PolarisDataGrid{TItem}"/> and <see cref="PolarisGridColumn{TItem}"/>.
/// </summary>
/// <remarks>
/// <para>
/// What is asserted here is the part of the grid a browser cannot explain: that the columns are
/// registered before the rows are drawn, that the table survives a result going empty, and that a cell
/// renders the value the column was told to render. A click on a heading or a checkbox is forwarded
/// through the interop module like every other Polaris event, so a test can prove it is wired — the
/// callback is attached — but not that Shopify delivered it; the browser check covers that half.
/// </para>
/// <para>
/// Every test states the failure it prevents.
/// </para>
/// </remarks>
public sealed class DataGridRenderingTests
{
    [Fact]
    public void The_grid_draws_one_header_per_column_and_one_row_per_record()
    {
        // The columns register themselves as they initialise, which happens after the grid has
        // already rendered its table once. If that second render were missing, the heading row would
        // be empty and the table would show no rows at all — a blank grid that looks like a data
        // problem.
        using var context = new PolarisTestContext();
        using var state = NewState();

        var grid = RenderGrid(context, state);

        Assert.Equal(4, grid.FindAll("s-table-header").Count);
        Assert.Equal(3, grid.FindAll("s-table-row").Count);
        Assert.Contains("Fine silver 999", grid.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_sortable_heading_is_a_button_that_reports_the_direction()
    {
        // Polaris has no sortable header, so the affordance is a button. A plain heading cell would
        // sort for nobody: no keyboard reach, no announcement, no indication of the current order.
        using var context = new PolarisTestContext();
        using var state = NewState();

        var grid = RenderGrid(context, state);

        var button = grid.FindAll("s-table-header")[0].QuerySelector("s-button");
        Assert.NotNull(button);
        Assert.Null(button!.GetAttribute("icon"));
        Assert.Null(button.GetAttribute("accessibilityLabel"));

        // Through the renderer's dispatcher, exactly as the click handler would change it.
        await grid.InvokeAsync(() => state.SetSort("Name", PolarisGridSortDirection.Descending));

        var sorted = grid.FindAll("s-table-header")[0].QuerySelector("s-button")!;
        Assert.Equal("chevron-down", sorted.GetAttribute("icon"));
        Assert.Equal("Name, sorted descending", sorted.GetAttribute("accessibilityLabel"));

        // The click is attached to the element, which is as far as a component test can see.
        Assert.Contains("click", context.Interop.AttachedEvents["s-button"]);
    }

    [Fact]
    public void A_column_declared_unsortable_renders_its_heading_as_text()
    {
        // A computed or free-text column that sorts is a promise the engine cannot keep, and a button
        // that does nothing is worse than no button.
        using var context = new PolarisTestContext();
        using var state = NewState();

        var grid = context.Render<PolarisDataGrid<Alloy>>(parameters => parameters
            .Add(p => p.State, state)
            .Add(p => p.Columns, Columns(sortableName: false)));

        var header = grid.FindAll("s-table-header")[0];
        Assert.Null(header.QuerySelector("s-button"));
        Assert.Equal("Name", header.TextContent.Trim());
    }

    [Fact]
    public void A_cell_renders_its_value_with_the_column_format_and_the_invariant_culture()
    {
        // The grid runs on a server whose culture is whatever the machine was set to. A value
        // formatted with it renders "92,5" here and "92.5" on the developer's machine, so the same
        // record looks different in two places and neither is wrong.
        using var context = new PolarisTestContext();
        using var state = NewState();
        var original = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            var grid = RenderGrid(context, state);

            Assert.Contains("92.5", grid.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain("92,5", grid.Markup, StringComparison.Ordinal);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void A_cell_template_replaces_the_formatted_value()
    {
        // The escape hatch for a value that needs markup — a badge, a link — rather than text.
        using var context = new PolarisTestContext();
        using var state = NewState();

        var grid = context.Render<PolarisDataGrid<Alloy>>(parameters => parameters
            .Add(p => p.State, state)
            .Add(p => p.Columns, Columns(cellTemplate: alloy => builder =>
            {
                builder.OpenElement(0, "strong");
                builder.AddContent(1, string.Concat("carat:", alloy.Carat.ToString(CultureInfo.InvariantCulture)));
                builder.CloseElement();
            })));

        Assert.Contains("carat:18", grid.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void An_empty_result_shows_the_empty_state_and_keeps_the_columns_registered()
    {
        // The filter's controls are drawn from the registered columns, so a column that is disposed
        // with the table takes away the merchant's only way back to their data: the grid is empty,
        // and the control that would un-empty it has nothing to draw.
        using var context = new PolarisTestContext();
        using var state = NewState();
        state.SetFilter(PolarisGridFilterDescriptor.Text("Name", "unobtainium"));

        var grid = RenderGrid(context, state);

        Assert.NotNull(grid.Find("s-empty-state"));
        Assert.Empty(grid.FindAll("s-table"));

        Assert.Equal(4, state.Columns.Count);
    }

    [Fact]
    public void The_empty_heading_and_subheading_are_the_caller_s()
    {
        // "No records" is wrong when the records exist and the filters exclude them, and only the
        // caller knows which.
        using var context = new PolarisTestContext();
        using var state = NewState();
        state.SetFilter(PolarisGridFilterDescriptor.Text("Name", "unobtainium"));

        var grid = context.Render<PolarisDataGrid<Alloy>>(parameters => parameters
            .Add(p => p.State, state)
            .Add(p => p.EmptyHeading, "No alloy matches those filters")
            .Add(p => p.EmptySubheading, "Clear a filter to see the rest.")
            .Add(p => p.Columns, Columns()));

        Assert.Equal("No alloy matches those filters", grid.Find("s-empty-state").GetAttribute("heading"));
        Assert.Contains("Clear a filter to see the rest.", grid.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Selection_renders_a_checkbox_for_each_row_and_one_for_the_page()
    {
        // The header checkbox is the only way to select a page of 25 without 25 clicks, and its
        // indeterminate state is the only thing that says a selection is partial. Both states are
        // DOM properties rather than attributes — Polaris does not reflect them — so a stale
        // `checked` attribute would pin the box and nothing would show it.
        using var context = new PolarisTestContext();
        using var state = NewState();

        var grid = context.Render<PolarisDataGrid<Alloy>>(parameters => parameters
            .Add(p => p.State, state)
            .Add(p => p.AllowSelection, true)
            .Add(p => p.Columns, Columns()));

        var boxes = grid.FindAll("s-checkbox");
        Assert.Equal(4, boxes.Count);

        Assert.False(boxes[0].HasAttribute("checked"));
        Assert.False(boxes[0].HasAttribute("indeterminate"));
        Assert.Equal("exclusive", boxes[0].GetAttribute("labelAccessibilityVisibility"));

        var initial = HeaderBox(context);
        Assert.False((bool)initial["checked"]!);
        Assert.False((bool)initial["indeterminate"]!);

        var first = state.Items[0];
        RenderOnDispatcher(grid, () => state.SetSelected(first, true));

        // Partial: the header is indeterminate rather than ticked, because ticking it would claim
        // every row is selected.
        var partial = HeaderBox(context);
        Assert.False((bool)partial["checked"]!);
        Assert.True((bool)partial["indeterminate"]!);

        RenderOnDispatcher(grid, () => state.SetPageSelected(true));

        var all = HeaderBox(context);
        Assert.True((bool)all["checked"]!);
        Assert.False((bool)all["indeterminate"]!);
    }

    [Fact]
    public void A_loading_state_replaces_the_table_with_a_spinner()
    {
        // Distinguishes "there is nothing" from "there is nothing yet", which are the same empty
        // table and different problems.
        using var context = new PolarisTestContext();
        using var state = NewState();
        state.IsLoading = true;

        var grid = RenderGrid(context, state);

        Assert.NotNull(grid.Find("s-spinner"));
        Assert.Empty(grid.FindAll("s-table"));
    }

    [Fact]
    public void An_error_message_is_shown_as_a_critical_banner()
    {
        // A failed load that renders as an empty grid reads as "no records", and the merchant starts
        // re-entering data that already exists.
        using var context = new PolarisTestContext();
        using var state = NewState();
        state.ErrorMessage = "The database is unreachable.";

        var grid = RenderGrid(context, state);

        var banner = grid.Find("s-banner");
        Assert.Equal("critical", banner.GetAttribute("tone"));
        Assert.Contains("The database is unreachable.", grid.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void A_change_to_the_shared_state_redraws_the_grid()
    {
        // The filter and the pager mutate the same state; if the grid did not listen, a filter change
        // would leave the table showing rows that no longer match.
        using var context = new PolarisTestContext();
        using var state = NewState();

        var grid = RenderGrid(context, state);
        Assert.Equal(3, grid.FindAll("s-table-row").Count);

        RenderOnDispatcher(grid, () => state.SetFilter(PolarisGridFilterDescriptor.Text("Name", "gold")));

        Assert.Equal(2, grid.FindAll("s-table-row").Count);
    }

    [Fact]
    public void Disposing_the_renderer_unregisters_the_columns_and_stops_listening()
    {
        // A page that navigates away leaves the state alive in its own field. A grid that stayed
        // subscribed would keep waking up on a disposed renderer — an exception with no sender in
        // the stack — and one that kept its columns registered would offer a filter for columns
        // nobody is rendering.
        var state = NewState();
        var context = new PolarisTestContext();

        context.Render<PolarisDataGrid<Alloy>>(parameters => parameters
            .Add(p => p.State, state)
            .Add(p => p.Columns, Columns()));

        Assert.Equal(4, state.Columns.Count);

        context.Dispose();

        Assert.Empty(state.Columns);

        // The state outlives the component in this scenario; changing it must not reach the dead
        // renderer.
        state.Refresh();
        state.SetFilter(PolarisGridFilterDescriptor.Text("Name", "gold"));

        state.Dispose();
    }

    [Fact]
    public void A_column_registers_its_id_type_and_filter_metadata()
    {
        // The filter coerces values against the declared type and the column id is what a saved view
        // refers to, so both have to survive the trip from the declaration into the state.
        using var context = new PolarisTestContext();
        using var state = NewState();

        RenderGrid(context, state);

        var silver = Assert.Single(state.Columns, column => column.Id == "Silver");
        Assert.Equal(typeof(decimal), silver.ValueType);
        Assert.Equal(PolarisGridFilterType.Range, silver.FilterType);
        Assert.Equal("Silver %", silver.Title);
        Assert.Equal(PolarisTableHeaderFormat.Numeric, silver.Alignment);
    }

    [Fact]
    public void A_column_outside_a_grid_is_refused_rather_than_rendering_nothing()
    {
        // Declared in the wrong place, a column would register nowhere: no heading, no filter, and no
        // explanation.
        using var context = new PolarisTestContext();

        var error = Assert.ThrowsAny<Exception>(() => context.Render<PolarisGridColumn<Alloy>>(parameters => parameters
            .Add(p => p.Field, alloy => alloy.Name)));

        Assert.Contains("PolarisDataGrid", error.Message, StringComparison.Ordinal);
    }

    // ── Fixtures ─────────────────────────────────────────────────────────────────────────────

    private static readonly List<Alloy> SampleRows =
    [
        new(1, "Fine silver 999", 0, 92.5m),
        new(2, "Yellow gold 9ct", 9, 75m),
        new(3, "Yellow gold 18ct", 18, 0m),
    ];

    /// <summary>
    /// Changes the state the way a handler does: on the renderer's dispatcher, then waits for the
    /// redraw. Mutating it from the test thread is what a real caller never does, and bUnit refuses
    /// it for exactly that reason.
    /// </summary>
    private static void RenderOnDispatcher<TComponent>(IRenderedComponent<TComponent> component, Action mutate)
        where TComponent : Microsoft.AspNetCore.Components.IComponent
        => component.InvokeAsync(mutate).GetAwaiter().GetResult();

    private static PolarisGridState<Alloy> NewState() => new(SampleRows, alloy => alloy.Id);

    private static IRenderedComponent<PolarisDataGrid<Alloy>> RenderGrid(PolarisTestContext context, PolarisGridState<Alloy> state)
        => context.Render<PolarisDataGrid<Alloy>>(parameters => parameters
            .Add(p => p.State, state)
            .Add(p => p.Columns, Columns()));

    /// <summary>
    /// The properties last written to the page's select-all checkbox.
    /// </summary>
    /// <remarks>
    /// <c>checked</c> and <c>indeterminate</c> are DOM properties, never attributes, so the only
    /// place their value exists outside a browser is the interop call. The three rows render after
    /// the header, so the header's write is the fourth from the end.
    /// </remarks>
    private static IReadOnlyDictionary<string, object?> HeaderBox(PolarisTestContext context)
        => context.Interop.SetProperties["s-checkbox"][^4];

    private static RenderFragment Columns(bool sortableName = true, RenderFragment<Alloy>? cellTemplate = null)
        => builder =>
        {
            AddColumn(builder, 0, "Name", alloy => alloy.Name, PolarisGridFilterType.Text, sortable: sortableName, cellTemplate: cellTemplate);
            AddColumn(builder, 10, "Carat", alloy => alloy.Carat, PolarisGridFilterType.MultiSelect);
            AddColumn(builder, 20, "Silver %", alloy => alloy.Silver, PolarisGridFilterType.Range,
                format: "N1", alignment: PolarisTableHeaderFormat.Numeric);
            AddColumn(builder, 30, "Active", alloy => alloy.Active, PolarisGridFilterType.Boolean);
        };

    private static void AddColumn(
        RenderTreeBuilder builder,
        int sequence,
        string title,
        Expression<Func<Alloy, object?>> field,
        PolarisGridFilterType filterType,
        bool sortable = true,
        string? format = null,
        PolarisTableHeaderFormat alignment = PolarisTableHeaderFormat.Base,
        RenderFragment<Alloy>? cellTemplate = null)
    {
        // A helper cannot use literal sequence numbers, so the analyzer's literal rule is suppressed
        // here rather than repeating this block at every call site.
#pragma warning disable ASP0006
        builder.OpenComponent<PolarisGridColumn<Alloy>>(sequence);
        builder.AddAttribute(sequence + 1, nameof(PolarisGridColumn<Alloy>.Title), title);
        builder.AddAttribute(sequence + 2, nameof(PolarisGridColumn<Alloy>.Field), field);
        builder.AddAttribute(sequence + 3, nameof(PolarisGridColumn<Alloy>.Sortable), sortable);
        builder.AddAttribute(sequence + 4, nameof(PolarisGridColumn<Alloy>.Searchable), false);
        builder.AddAttribute(sequence + 5, nameof(PolarisGridColumn<Alloy>.FilterType), filterType);
        builder.AddAttribute(sequence + 6, nameof(PolarisGridColumn<Alloy>.Format), format);
        builder.AddAttribute(sequence + 7, nameof(PolarisGridColumn<Alloy>.Alignment), alignment);
        builder.AddAttribute(sequence + 8, nameof(PolarisGridColumn<Alloy>.CellTemplate), cellTemplate);
        builder.CloseComponent();
#pragma warning restore ASP0006
    }

    private sealed record Alloy(int Id, string Name, int Carat, decimal Silver, bool Active = true);
}
