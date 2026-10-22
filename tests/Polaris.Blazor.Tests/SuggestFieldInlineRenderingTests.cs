using Bunit;
using Polaris.Blazor.Components;
using Xunit;

namespace Polaris.Blazor.Tests;

/// <summary>
/// Tests for the select-shaped suggestion field: a search box over the known values, an "Add ..." row
/// for whatever has been typed, and the picked value as a removable chip.
/// </summary>
/// <remarks>
/// <para>
/// The two things worth pinning are the ones a merchant would find and a reviewer would not: that the
/// add row says exactly what it would create, and that there is only ever one add row and no second
/// spelling of a value the list already holds. Both are asserted against the rendered text.
/// </para>
/// <para>
/// Clicks arrive through the interop module, so they are raised through the recording double — the last
/// control of a tag, which is the one a live listener would be.
/// </para>
/// </remarks>
public sealed class SuggestFieldInlineRenderingTests
{
    [Fact]
    public void With_nothing_chosen_it_draws_a_search_box_and_one_row_per_known_value()
    {
        // The rows are buttons: a list of choices a keyboard cannot reach is not a list, and the
        // clickable surface this could also have been built on is neither focusable nor announced.
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisSuggestFieldInline>(parameters => parameters
            .Add(p => p.Label, "Supplier")
            .Add(p => p.Placeholder, "Search or add a supplier")
            .Add(p => p.Suggestions, ["Cookson Gold", "Rio Grande"]));

        var field = component.Find("s-search-field");
        Assert.Equal("Supplier", field.GetAttribute("label"));
        Assert.Equal("Search or add a supplier", field.GetAttribute("placeholder"));

        var rows = component.FindAll("s-button");
        Assert.Equal(["Cookson Gold", "Rio Grande"], rows.Select(row => row.TextContent));
        Assert.All(rows, row => Assert.True(row.HasAttribute("disabled") == false));

        // Nothing chosen yet, so no chip.
        Assert.Empty(component.FindAll("s-clickable-chip"));
    }

    [Fact]
    public async Task The_add_row_is_last_and_carries_what_was_typed()
    {
        // This is the whole point of the control: the last option is the thing the merchant just typed,
        // so a supplier that is not on the list yet is one press away rather than a second screen.
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisSuggestFieldInline>(parameters => parameters
            .Add(p => p.Label, "Supplier")
            .Add(p => p.CollectionName, "your suppliers")
            .Add(p => p.Suggestions, ["Cookson Gold"]));

        await component.InvokeAsync(() => context.Interop.RaiseAsync("s-search-field", "input", "  Acme Metals  "));

        var rows = component.FindAll("s-button");

        // Nothing in the list matches, so the add row is the only row — and it is last by construction,
        // which is the position a merchant's eye goes to after typing something new.
        var row = Assert.Single(rows);
        Assert.Equal("Add \u201cAcme Metals\u201d", row.TextContent);

        // The visible text is the quoted name alone, so the accessible name has to say what pressing it
        // does, and what it would be added to.
        Assert.Equal("Add \"Acme Metals\" to your suppliers", row.GetAttribute("accessibilityLabel"));
    }

    [Fact]
    public async Task Typing_narrows_the_rows_and_offers_nothing_to_add_for_a_value_the_list_holds()
    {
        // A second spelling of a name the shop already has is exactly what makes a supplier list useless,
        // so the add row disappears the moment the typed text IS a known value — compare the test above,
        // where a term that is only part of a name still offers to create it.
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisSuggestFieldInline>(parameters => parameters
            .Add(p => p.Suggestions, ["Cookson Gold", "Rio Grande"]));

        await component.InvokeAsync(() => context.Interop.RaiseAsync("s-search-field", "input", "grand"));

        // A partial term still offers to create it: "grand" is not a supplier, and the rule is the one the
        // merchant can predict — whatever is typed becomes an option.
        Assert.Equal(
            ["Rio Grande", "Add \u201cgrand\u201d"],
            component.FindAll("s-button").Select(row => row.TextContent));

        await component.InvokeAsync(() => context.Interop.RaiseAsync("s-search-field", "input", " rio grande "));

        // Now the typed text IS the known value, ignoring case and spaces, so there is nothing to add.
        Assert.Equal(["Rio Grande"], component.FindAll("s-button").Select(row => row.TextContent));
    }

    [Fact]
    public async Task Pressing_the_add_row_reports_the_value_trimmed_and_does_not_choose_it()
    {
        // The caller owns the vocabulary: it persists the name and then sets the value, so the component
        // must not also raise ValueChanged for something that does not exist yet.
        using var context = new PolarisTestContext();

        string? added = null;
        var chosen = 0;

        var component = context.Render<PolarisSuggestFieldInline>(parameters => parameters
            .Add(p => p.Suggestions, ["Cookson Gold"])
            .Add(p => p.ValueChanged, _ => chosen++)
            .Add(p => p.SuggestionAdded, value => added = value));

        await component.InvokeAsync(() => context.Interop.RaiseAsync("s-search-field", "input", " Acme Metals "));
        await component.InvokeAsync(() => context.Interop.RaiseAsync("s-button", "click"));

        Assert.Equal("Acme Metals", added);
        Assert.Equal(0, chosen);
    }

    [Fact]
    public async Task Pressing_a_row_reports_that_value_and_nothing_else()
    {
        using var context = new PolarisTestContext();

        string? reported = null;
        string? added = null;

        var component = context.Render<PolarisSuggestFieldInline>(parameters => parameters
            .Add(p => p.Suggestions, ["Cookson Gold"])
            .Add(p => p.ValueChanged, value => reported = value)
            .Add(p => p.SuggestionAdded, value => added = value));

        await component.InvokeAsync(() => context.Interop.RaiseAsync("s-button", "click"));

        Assert.Equal("Cookson Gold", reported);
        Assert.Null(added);
    }

    [Fact]
    public void A_chosen_value_replaces_the_picker_with_a_removable_chip()
    {
        // Two states, not one: once a supplier is chosen the list and the search box are gone, because a
        // picker that stays open under a chosen value is a control with two answers on screen at once.
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisSuggestFieldInline>(parameters => parameters
            .Add(p => p.Label, "Supplier")
            .Add(p => p.Value, "Cookson Gold")
            .Add(p => p.Suggestions, ["Cookson Gold", "Rio Grande"]));

        Assert.Empty(component.FindAll("s-search-field"));
        Assert.Empty(component.FindAll("s-button"));

        var chip = Assert.Single(component.FindAll("s-clickable-chip"));
        Assert.Equal("Cookson Gold", chip.TextContent);
        Assert.True(chip.HasAttribute("removable"));

        // The label stays on screen, so the chip is not an unattributed name.
        Assert.Equal("Supplier", component.Find("s-text").TextContent);
    }

    [Fact]
    public async Task Removing_the_chip_clears_the_value_and_leaves_the_list_alone()
    {
        // Removing the chip is how the merchant says "this material has no supplier after all". It is not
        // a request to forget the name, so the caller is told only that the value is gone.
        using var context = new PolarisTestContext();

        string? reported = "not-raised";
        string? added = null;

        var component = context.Render<PolarisSuggestFieldInline>(parameters => parameters
            .Add(p => p.Value, "Cookson Gold")
            .Add(p => p.Suggestions, ["Cookson Gold"])
            .Add(p => p.ValueChanged, value => reported = value)
            .Add(p => p.SuggestionAdded, value => added = value));

        await component.InvokeAsync(() => context.Interop.RaiseAsync("s-clickable-chip", "remove"));

        Assert.Null(reported);
        Assert.Null(added);
    }

    [Fact]
    public void An_empty_list_says_so_rather_than_drawing_nothing()
    {
        // A control that renders an empty panel on first use looks broken. The caller supplies the
        // sentence, because only it knows whether an empty list means "add one" or "nothing here yet".
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisSuggestFieldInline>(parameters => parameters
            .Add(p => p.EmptyLabel, "No suppliers yet — type a name to add one."));

        Assert.Empty(component.FindAll("s-button"));
        Assert.Equal("No suppliers yet — type a name to add one.", component.Find("s-text").TextContent);
    }

    [Fact]
    public async Task A_caller_that_owns_the_list_elsewhere_can_turn_the_add_row_off()
    {
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisSuggestFieldInline>(parameters => parameters
            .Add(p => p.Suggestions, ["Cookson Gold"])
            .Add(p => p.AllowAdd, false));

        await component.InvokeAsync(() => context.Interop.RaiseAsync("s-search-field", "input", "Acme Metals"));

        // No matches and no add row, so no panel at all.
        Assert.Empty(component.FindAll("s-button"));
    }

    [Fact]
    public void Filtering_is_the_same_rule_the_chip_field_uses()
    {
        // Delegated on purpose: the two controls offer the same vocabulary, and a filter that differed
        // between them is a difference nobody would think to test for.
        string[] suggestions = ["Cookson Gold", "Rio Grande", "Acme Metals"];

        Assert.Equal(suggestions, PolarisSuggestFieldInline.Matches(suggestions, null));
        Assert.Equal(["Cookson Gold"], PolarisSuggestFieldInline.Matches(suggestions, "GOLD"));
        Assert.False(PolarisSuggestFieldInline.IsNew(suggestions, " acme metals "));
        Assert.True(PolarisSuggestFieldInline.IsNew(suggestions, "Acme Metals Ltd"));
    }
}
