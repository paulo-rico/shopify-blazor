using Bunit;
using Polaris.Blazor.Components;
using Xunit;

namespace Polaris.Blazor.Tests;

/// <summary>
/// Tests for the suggest field, the free-text-plus-chips control that stands in for the combobox
/// Polaris 1 does not have.
/// </summary>
/// <remarks>
/// <para>
/// The behaviour worth pinning is the part that would otherwise be discovered by a merchant rather
/// than by a test: which chips are drawn for what has been typed, when the field offers to remember
/// a new value, and which of the four callbacks each interaction raises. A field that raised
/// <see cref="PolarisSuggestField.ValueChanged"/> for a chip removal, or offered to add a value it
/// already had, would look entirely normal.
/// </para>
/// <para>
/// Clicks arrive through the interop module rather than a Blazor <c>@onclick</c>, so they are raised
/// through the recording double — the last chip of a tag, which is the one a live listener would be.
/// </para>
/// </remarks>
public sealed class SuggestFieldRenderingTests
{
    [Fact]
    public void The_field_renders_its_label_details_placeholder_and_current_value()
    {
        // The value goes in as defaultValue so the first paint is right before the element upgrades;
        // afterwards the property is authoritative, which is why it is not asserted here.
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisSuggestField>(parameters => parameters
            .Add(p => p.Label, "Supplier")
            .Add(p => p.Details, "Who you buy this from.")
            .Add(p => p.Placeholder, "Search or add a supplier")
            .Add(p => p.Value, "Cookson Gold")
            .Add(p => p.Suggestions, ["Cookson Gold", "Rio Grande"]));

        var field = component.Find("s-text-field");
        Assert.Equal("Supplier", field.GetAttribute("label"));
        Assert.Equal("Who you buy this from.", field.GetAttribute("details"));
        Assert.Equal("Search or add a supplier", field.GetAttribute("placeholder"));
        Assert.Equal("Cookson Gold", field.GetAttribute("defaultValue"));
    }

    [Fact]
    public void A_blank_field_draws_every_known_value_as_a_removable_chip()
    {
        // A chip is what makes the list useful without a search box: the whole vocabulary is visible
        // until the merchant starts typing. removable belongs on the chip, because a vocabulary nobody
        // can prune fills with typos.
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisSuggestField>(parameters => parameters
            .Add(p => p.Label, "Supplier")
            .Add(p => p.Suggestions, ["Cookson Gold", "Rio Grande"]));

        var chips = component.FindAll("s-clickable-chip");
        Assert.Equal(["Cookson Gold", "Rio Grande"], chips.Select(chip => chip.TextContent));
        Assert.All(chips, chip => Assert.True(chip.HasAttribute("removable")));
    }

    [Fact]
    public async Task Typing_narrows_the_chips_to_the_ones_that_contain_the_text()
    {
        // Filtering on input rather than only on change is what makes the chips behave like a search
        // result list; a field that filtered on change would leave the previous list on screen for the
        // whole time the merchant is still typing.
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisSuggestField>(parameters => parameters
            .Add(p => p.Label, "Supplier")
            .Add(p => p.Suggestions, ["Cookson Gold", "Rio Grande"]));

        await component.InvokeAsync(() => context.Interop.RaiseAsync("s-text-field", "input", "grand"));

        Assert.Equal(["Rio Grande"], component.FindAll("s-clickable-chip").Select(chip => chip.TextContent));
    }

    [Fact]
    public async Task Typing_a_new_value_offers_to_add_it_and_adding_reports_it_trimmed()
    {
        // This is the "or add it" half of "search or add". Without it a merchant types the same new
        // supplier into every material and the list never grows.
        using var context = new PolarisTestContext();

        string? added = null;

        var component = context.Render<PolarisSuggestField>(parameters => parameters
            .Add(p => p.Label, "Supplier")
            .Add(p => p.CollectionName, "your suppliers")
            .Add(p => p.Suggestions, ["Cookson Gold"])
            .Add(p => p.SuggestionAdded, value => added = value));

        // Nothing to add while the field is empty.
        Assert.Empty(component.FindAll("s-button"));

        await component.InvokeAsync(() => context.Interop.RaiseAsync("s-text-field", "input", "  Acme Metals  "));

        var button = Assert.Single(component.FindAll("s-button"));
        Assert.Equal("Add \u201cAcme Metals\u201d to your suppliers", button.TextContent);

        await component.InvokeAsync(() => context.Interop.RaiseAsync("s-button", "click"));

        Assert.Equal("Acme Metals", added);
    }

    [Fact]
    public async Task A_value_the_list_already_holds_is_not_offered_for_adding_again()
    {
        // Compared without regard to case or surrounding spaces, which is how the value is matched when
        // it is saved: otherwise re-typing "acme" offers to add a second copy of a supplier the shop
        // already has, and the pair rule then complains about a duplicate nobody can see.
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisSuggestField>(parameters => parameters
            .Add(p => p.Suggestions, ["Acme Metals"]));

        await component.InvokeAsync(() => context.Interop.RaiseAsync("s-text-field", "input", "  acme METALS "));

        Assert.Empty(component.FindAll("s-button"));
        Assert.Single(component.FindAll("s-clickable-chip"));
    }

    [Fact]
    public async Task Picking_a_chip_fills_the_field_and_reports_the_choice()
    {
        // Both callbacks fire: ValueChanged because that is how every field reports its value, and
        // SuggestionChosen for a caller that wants to do more than store the name.
        using var context = new PolarisTestContext();

        string? reported = null;
        string? chosen = null;

        var component = context.Render<PolarisSuggestField>(parameters => parameters
            .Add(p => p.Suggestions, ["Cookson Gold"])
            .Add(p => p.ValueChanged, value => reported = value)
            .Add(p => p.SuggestionChosen, value => chosen = value));

        await component.InvokeAsync(() => context.Interop.RaiseAsync("s-clickable-chip", "click"));

        Assert.Equal("Cookson Gold", reported);
        Assert.Equal("Cookson Gold", chosen);
    }

    [Fact]
    public async Task Removing_a_chip_reports_the_removal_and_leaves_the_field_alone()
    {
        // Removing a name from the vocabulary is not the same as clearing the value: the text already
        // typed is free text, and blanking it would discard an edit the merchant did not ask to lose.
        using var context = new PolarisTestContext();

        string? removed = null;
        var reported = 0;

        var component = context.Render<PolarisSuggestField>(parameters => parameters
            .Add(p => p.Suggestions, ["Cookson Gold"])
            .Add(p => p.Value, "Cookson Gold")
            .Add(p => p.ValueChanged, _ => reported++)
            .Add(p => p.SuggestionRemoved, value => removed = value));

        await component.InvokeAsync(() => context.Interop.RaiseAsync("s-clickable-chip", "remove"));

        Assert.Equal("Cookson Gold", removed);
        Assert.Equal(0, reported);
        Assert.Equal("Cookson Gold", component.Find("s-text-field").GetAttribute("defaultValue"));
    }

    [Fact]
    public void A_caller_that_does_not_want_an_add_button_can_say_so()
    {
        // The add half is optional because a caller may own that vocabulary elsewhere — a settings
        // screen, or an import — and a second way to add to it would be a second source of truth.
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisSuggestField>(parameters => parameters
            .Add(p => p.AllowAdd, false)
            .Add(p => p.Value, "Something New"));

        Assert.Empty(component.FindAll("s-button"));
    }

    [Fact]
    public void Matching_shows_everything_for_a_blank_query_and_matches_any_part_otherwise()
    {
        // "Contains", not "starts with": a merchant looking for Cookson types "cook", but one looking
        // for the alloy house types "gold" — and a prefix rule would hide a value whose first word is
        // not the one they remember.
        string[] suggestions = ["Cookson Gold", "Rio Grande", "Acme Metals"];

        Assert.Equal(suggestions, PolarisSuggestField.Matches(suggestions, null));
        Assert.Equal(suggestions, PolarisSuggestField.Matches(suggestions, "   "));
        Assert.Equal(["Cookson Gold"], PolarisSuggestField.Matches(suggestions, "cook"));
        Assert.Equal(["Cookson Gold"], PolarisSuggestField.Matches(suggestions, "GOLD"));
        Assert.Empty(PolarisSuggestField.Matches(suggestions, "zzz"));
    }

    [Fact]
    public void IsNew_ignores_case_and_surrounding_spaces()
    {
        string[] suggestions = ["Acme Metals"];

        Assert.False(PolarisSuggestField.IsNew(suggestions, " acme metals "));
        Assert.False(PolarisSuggestField.IsNew(suggestions, string.Empty));
        Assert.False(PolarisSuggestField.IsNew(suggestions, null));
        Assert.True(PolarisSuggestField.IsNew(suggestions, "Acme Metals Ltd"));
        Assert.True(PolarisSuggestField.IsNew([], "Acme Metals"));
    }
}

