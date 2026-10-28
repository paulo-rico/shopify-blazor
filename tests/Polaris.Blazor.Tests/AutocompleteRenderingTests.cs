using Bunit;
using Microsoft.AspNetCore.Components;
using Polaris.Blazor.Components;
using Xunit;

namespace Polaris.Blazor.Tests;

/// <summary>
/// Tests for the generic autocomplete: the field, the filtered list under it, and the keyboard path
/// through the rows.
/// </summary>
/// <remarks>
/// <para>
/// The two facts worth pinning are the ones a merchant would meet as a fault and a reviewer would not:
/// that only a chosen item is ever reported as the value (half-typed text is not a selection), and that
/// the list is reachable from the keyboard — the arrow keys move real focus onto the row buttons, because
/// this layer cannot write <c>aria-activedescendant</c> into the search field's shadow root.
/// </para>
/// <para>
/// Keyboard and pointer events arrive through the interop module, so they are raised through the
/// recording double, the way a live listener would. DOM focus itself cannot be observed here: the
/// assertion is that the component asked the interop layer to move it, which is the half a component test
/// can own.
/// </para>
/// </remarks>
public sealed class AutocompleteRenderingTests
{
    /// <summary>One item, small enough that a test can state its expectations literally.</summary>
    private sealed record Thing(int Id, string Name, string? Description = null);

    private static readonly Thing Gold = new(1, "Gold");
    private static readonly Thing GoldPlated = new(2, "Gold plated");
    private static readonly Thing Silver = new(3, "Silver");
    private static readonly Thing Sapphire = new(4, "Sapphire");

    [Fact]
    public void Nothing_is_suggested_until_something_is_typed()
    {
        // A page that opens with several autocompletes should not open all their lists: the panel is
        // drawn only once the term is long enough, which is what MinimumLength controls.
        using var context = new PolarisTestContext();

        var component = Render(context, parameters => parameters.Add(p => p.Label, "Material"));

        var field = component.Find("s-text-field");
        Assert.Equal("Material", field.GetAttribute("label"));
        Assert.Equal(string.Empty, field.GetAttribute("defaultValue"));

        Assert.Empty(component.FindAll("[data-polaris-autocomplete-row]"));
        Assert.True(component.Find("[data-polaris-popup]").HasAttribute("hidden"));
    }

    [Fact]
    public async Task The_suggestions_are_drawn_in_a_top_layer_panel_over_the_page()
    {
        // Measured, not assumed: the inline version pushed the page down on every keystroke, and an
        // absolutely positioned version was clipped at the section's edge. The panel is a native
        // popover — a top-layer surface — so the field stays put and an ancestor's overflow cannot cut
        // the list off.
        using var context = new PolarisTestContext();

        var component = Render(context, parameters => parameters.Add(p => p.Items, [Gold, Silver]));

        await TypeAsync(component, context, "go");

        var panel = component.Find("[data-polaris-popup]");
        Assert.Equal("manual", panel.GetAttribute("popover"));
        Assert.Contains("overflow-y:auto", panel.GetAttribute("style"));

        // The field is the anchor, never a child of the panel: the panel is placed relative to it.
        Assert.Empty(panel.QuerySelectorAll("s-text-field"));
        Assert.Single(component.FindAll("[data-polaris-popup-anchor]"));

        component.WaitForAssertion(() => Assert.Contains("show:below", context.Interop.PopupCalls));
    }

    [Fact]
    public async Task Closing_the_list_hides_the_panel_and_leaves_no_suggestion_elements_behind()
    {
        // The popup's element stays in the DOM while closed, so the rows must not: a closed autocomplete
        // that still renders its options is a list nothing can see and everything can tab into.
        using var context = new PolarisTestContext();

        var component = Render(context, parameters => parameters.Add(p => p.Items, [Gold, Silver]));

        await TypeAsync(component, context, "go");
        Assert.NotEmpty(Rows(component));

        await KeyAsync(component, context, "Escape");

        Assert.True(component.Find("[data-polaris-popup]").HasAttribute("hidden"));
        Assert.Empty(Rows(component));
        component.WaitForAssertion(() => Assert.Contains("hide", context.Interop.PopupCalls));
    }

    [Fact]
    public async Task Typing_narrows_the_list_to_the_items_that_contain_the_term()
    {
        // Contains, case-insensitively — "gold" finds "Gold plated". A prefix-only rule would lose it as
        // soon as the merchant typed the second word, which is the rule the suggest fields settled on.
        using var context = new PolarisTestContext();

        var component = Render(context, parameters => parameters.Add(p => p.Items, [Gold, GoldPlated, Silver]));

        await TypeAsync(component, context, "gOlD");

        Assert.Equal(
            ["Gold", "Gold plated"],
            Rows(component).Select(row => row.TextContent));
    }

    [Fact]
    public async Task The_list_is_capped_by_MaxSuggestions()
    {
        // The cap is a rendering cap, not a matching one: the items beyond it still match, they are just
        // not drawn. A page with a large catalogue narrows the term instead.
        using var context = new PolarisTestContext();

        var component = Render(context, parameters => parameters
            .Add(p => p.Items, [Gold, GoldPlated, Silver, Sapphire])
            .Add(p => p.MaxSuggestions, 2));

        await TypeAsync(component, context, "l");

        Assert.Equal(2, Rows(component).Count);
    }

    [Fact]
    public async Task Below_the_minimum_length_the_list_stays_closed()
    {
        // MinimumLength gates OPENING, not matching: the term is typed and nothing appears until it is
        // long enough to be worth searching for.
        using var context = new PolarisTestContext();

        var component = Render(context, parameters => parameters
            .Add(p => p.Items, [Gold, Silver])
            .Add(p => p.MinimumLength, 3));

        await TypeAsync(component, context, "go");

        Assert.Empty(Rows(component));

        await TypeAsync(component, context, "gol");

        Assert.Single(Rows(component));
    }

    [Fact]
    public async Task Choosing_a_row_reports_the_item_and_closes_the_list()
    {
        // The row is chosen on mousedown, before the field's blur can close the panel and take the button
        // with it. That is the whole reason the handler is mousedown rather than click.
        using var context = new PolarisTestContext();

        Thing? reported = null;

        var component = Render(context, parameters => parameters
            .Add(p => p.Items, [Gold, Silver])
            .Add(p => p.ValueChanged, (Thing? value) => reported = value));

        await TypeAsync(component, context, "sil");
        Rows(component)[0].MouseDown();

        Assert.Equal(Silver, reported);
        Assert.Empty(Rows(component));

        // The chosen item's own text is what stays in the field — never the term that found it.
        Assert.Equal("Silver", component.Find("s-text-field").GetAttribute("defaultValue"));
    }

    [Fact]
    public async Task A_choose_by_keyboard_activation_of_a_row_reports_it_too()
    {
        // Enter on a focused row is the browser's own click on a real button, not a key this component
        // handles; the row therefore has to report the item from OnClick as well as from mousedown.
        using var context = new PolarisTestContext();

        Thing? reported = null;

        var component = Render(context, parameters => parameters
            .Add(p => p.Items, [Gold, Silver])
            .Add(p => p.ValueChanged, (Thing? value) => reported = value));

        await TypeAsync(component, context, "sil");
        await RaiseAsync(component, context, "s-button", "click");

        Assert.Equal(Silver, reported);
        Assert.Empty(Rows(component));
    }

    [Fact]
    public async Task An_item_template_replaces_the_bare_text_of_a_row()
    {
        // Custom rendering is how a suggestion carries a description, a price or an icon without the
        // caller having to build its own list.
        using var context = new PolarisTestContext();

        var component = Render(context, parameters => parameters
            .Add(p => p.Items, [new Thing(1, "Gold", "per gram")])
            .Add(p => p.ItemTemplate, (RenderFragment<Thing>)(item => content =>
            {
                content.OpenElement(0, "em");
                content.AddContent(1, $"{item.Name}: {item.Description}");
                content.CloseElement();
            })));

        await TypeAsync(component, context, "go");

        Assert.Equal("Gold: per gram", Rows(component)[0].TextContent);
    }

    [Fact]
    public async Task Arrow_down_opens_the_whole_list_and_moves_focus_onto_the_first_row()
    {
        // ArrowDown from an empty field is an explicit request to browse, so it opens the list even
        // though MinimumLength would keep it shut. Focus moves onto the row, which is what makes Enter
        // work without any keyboard handling on the button.
        using var context = new PolarisTestContext();

        var component = Render(context, parameters => parameters
            .Add(p => p.Items, [Gold, Silver])
            .Add(p => p.MinimumLength, 3));

        await KeyAsync(component, context, "ArrowDown");

        Assert.Equal(2, Rows(component).Count);
        component.WaitForAssertion(() => Assert.Equal(1, context.Interop.FocusWithinCount));

        // No selector: focusWithin searches the row's own subtree, through the button's shadow root.
        Assert.Null(context.Interop.LastFocusSelector);
    }

    [Fact]
    public async Task A_row_handles_the_arrows_and_Escape_because_it_is_the_control_holding_focus()
    {
        // The rows are the focused controls while browsing, so they carry the arrow and Escape keys
        // themselves; nothing is handled on a wrapper whose events would have to cross a shadow boundary.
        using var context = new PolarisTestContext();

        var component = Render(context, parameters => parameters.Add(p => p.Items, [Gold, Silver, Sapphire]));

        await KeyAsync(component, context, "ArrowDown");
        component.WaitForAssertion(() => Assert.Equal(1, context.Interop.FocusWithinCount));

        // Whichever row the double addresses, an arrow must move focus somewhere rather than being lost.
        await RaiseAsync(component, context, "s-button", "keydown", key: "ArrowUp");
        component.WaitForAssertion(() => Assert.Equal(2, context.Interop.FocusWithinCount));

        // Escape closes from a row, which would leave focus on a button that no longer exists.
        await RaiseAsync(component, context, "s-button", "keydown", key: "Escape");

        Assert.Empty(Rows(component));
        component.WaitForAssertion(() => Assert.Equal(3, context.Interop.FocusWithinCount));
    }

    [Fact]
    public async Task Escape_puts_the_chosen_item_back_in_the_field()
    {
        // Escape abandons the search, not the selection: the field must go back to what is actually
        // chosen rather than keep a term the caller was never told about.
        using var context = new PolarisTestContext();

        var component = Render(context, parameters => parameters
            .Add(p => p.Items, [Gold, Silver])
            .Add(p => p.Value, Gold));

        await TypeAsync(component, context, "sil");
        await KeyAsync(component, context, "Escape");

        Assert.Empty(Rows(component));
        Assert.Equal("Gold", component.Find("s-text-field").GetAttribute("defaultValue"));
    }

    [Fact]
    public async Task Enter_in_the_field_takes_the_exact_match_and_reports_it_once()
    {
        // An exact match wins over the first match: typing a full item name and pressing Enter must not
        // pick a different item that merely contains it.
        using var context = new PolarisTestContext();

        var reports = 0;
        Thing? reported = null;

        var component = Render(context, parameters => parameters
            .Add(p => p.Items, [GoldPlated, Gold])
            .Add(p => p.ValueChanged, (Thing? value) =>
            {
                reports++;
                reported = value;
            }));

        await TypeAsync(component, context, "Gold");
        await KeyAsync(component, context, "Enter");

        Assert.Equal(Gold, reported);
        Assert.Equal(1, reports);
    }

    [Fact]
    public async Task A_commit_of_unknown_text_puts_the_chosen_item_back_and_reports_nothing()
    {
        // Selection-only mode's central promise: text that is not an item is not a value. The field goes
        // back to the real choice, so a dependent screen cannot read a name the caller was never told.
        using var context = new PolarisTestContext();

        Thing? reported = null;

        var component = Render(context, parameters => parameters
            .Add(p => p.Items, [Gold, Silver])
            .Add(p => p.Value, Gold)
            .Add(p => p.ValueChanged, (Thing? value) => reported = value));

        await TypeAsync(component, context, "zzz");
        await RaiseAsync(component, context, "s-text-field", "change", "zzz");

        Assert.Null(reported);
        Assert.Equal("Gold", component.Find("s-text-field").GetAttribute("defaultValue"));
    }

    [Fact]
    public async Task Allow_custom_value_hands_the_typed_text_over_instead_of_reverting_it()
    {
        // Free text is reported separately because it is not a Thing: the caller maps it to an item and
        // sets Value, which is also the hook for creating one.
        using var context = new PolarisTestContext();

        string? custom = null;
        Thing? reported = null;

        var component = Render(context, parameters => parameters
            .Add(p => p.Items, [Gold])
            .Add(p => p.AllowCustomValue, true)
            .Add(p => p.CustomValueEntered, (string text) => custom = text)
            .Add(p => p.ValueChanged, (Thing? value) => reported = value));

        await TypeAsync(component, context, "  Rose gold  ");
        await RaiseAsync(component, context, "s-text-field", "change", "  Rose gold  ");

        Assert.Equal("Rose gold", custom);
        Assert.Null(reported);
    }

    [Fact]
    public async Task Clearing_the_text_clears_the_value()
    {
        // The search field has no accessory slot for a clear button, so deleting the text is the clear
        // gesture — and it has to report the cleared value rather than quietly restoring the old one.
        using var context = new PolarisTestContext();

        var cleared = false;
        Thing? reported = Gold;

        var component = Render(context, parameters => parameters
            .Add(p => p.Items, [Gold])
            .Add(p => p.Value, Gold)
            .Add(p => p.ValueChanged, (Thing? value) =>
            {
                cleared = value is null;
                reported = value;
            }));

        await TypeAsync(component, context, string.Empty);
        await RaiseAsync(component, context, "s-text-field", "change", string.Empty);

        Assert.True(cleared);
        Assert.Null(reported);
    }

    [Fact]
    public async Task An_asynchronous_search_supplies_the_rows_and_ignores_Items()
    {
        // With a provider the local list is not the source of truth: filtering it on top of the returned
        // rows would show matches the provider did not return.
        using var context = new PolarisTestContext();

        var asked = string.Empty;

        var component = Render(context, parameters => parameters
            .Add(p => p.Items, [Gold])
            .Add(p => p.DebounceMilliseconds, 0)
            .Add(p => p.SearchFunction, (string query, CancellationToken token) =>
            {
                asked = query;
                return Task.FromResult<IReadOnlyList<Thing>>([Sapphire]);
            }));

        await TypeAsync(component, context, "sa");

        Assert.Equal("sa", asked);
        Assert.Equal(["Sapphire"], Rows(component).Select(row => row.TextContent));
    }

    [Fact]
    public async Task The_no_results_content_is_drawn_when_nothing_matches()
    {
        // An empty panel with no explanation reads as a control that failed to load.
        using var context = new PolarisTestContext();

        var component = Render(context, parameters => parameters
            .Add(p => p.Items, [Gold])
            .Add(p => p.NoResultsContent, BuilderHelper.Text("No materials match")));

        await TypeAsync(component, context, "zzz");

        Assert.Empty(Rows(component));
        Assert.Contains("No materials match", component.Markup);
    }

    [Fact]
    public void A_value_that_arrives_after_the_first_render_is_adopted()
    {
        // The bound shape: the page loads its list and its chosen value separately, so the value arrives
        // on a later parameter pass and the field has to follow it.
        using var context = new PolarisTestContext();

        var component = Render(context, parameters => parameters
            .Add(p => p.Items, [Gold, Silver])
            .Add(p => p.Value, (Thing?)null));

        component.Render(parameters => parameters
            .Add(p => p.Items, [Gold, Silver])
            .Add(p => p.Value, Silver));

        Assert.Equal("Silver", component.Find("s-text-field").GetAttribute("defaultValue"));
    }

    [Fact]
    public async Task A_parent_that_re_renders_with_the_same_value_does_not_reset_the_typed_text()
    {
        // The echo rule: a page that keeps re-rendering while a query is in flight must not have its text
        // replaced by the value it already carries. Comparing against the parameter, not against what was
        // typed, is what tells the two apart.
        using var context = new PolarisTestContext();

        var component = Render(context, parameters => parameters
            .Add(p => p.Items, [Gold])
            .Add(p => p.Value, (Thing?)null));

        await TypeAsync(component, context, "gol");

        component.Render(parameters => parameters
            .Add(p => p.Items, [Gold])
            .Add(p => p.Value, (Thing?)null));

        Assert.Equal("gol", component.Find("s-text-field").GetAttribute("defaultValue"));
    }

    [Fact]
    public async Task A_disabled_autocomplete_neither_opens_nor_reports_anything()
    {
        // Disabled is drawn on the field and on every row, but the handlers guard too: an event that
        // arrives anyway must not turn a control the merchant cannot use into a value change.
        using var context = new PolarisTestContext();

        var reported = 0;

        var component = Render(context, parameters => parameters
            .Add(p => p.Items, [Gold])
            .Add(p => p.Disabled, true)
            .Add(p => p.ValueChanged, (Thing? _) => reported++));

        Assert.True(component.Find("s-text-field").HasAttribute("disabled"));

        await TypeAsync(component, context, "go");
        await KeyAsync(component, context, "ArrowDown");

        Assert.Empty(Rows(component));
        Assert.Equal(0, reported);
    }

    [Fact]
    public async Task The_field_and_every_row_attach_a_keydown_listener()
    {
        // The keyboard path is a DOM listener per control, attached only because a handler was supplied.
        // Without it the arrows are dead in a browser while every component test still passes, which is
        // why the wiring itself is asserted.
        using var context = new PolarisTestContext();

        var component = Render(context, parameters => parameters
            .Add(p => p.Items, [Gold, Silver])
            .Add(p => p.MinimumLength, 1));

        Assert.True(context.Interop.AttachedEvents.TryGetValue("s-text-field", out var fieldEvents));
        Assert.Contains("keydown", fieldEvents);

        await TypeAsync(component, context, "go");

        Assert.True(context.Interop.AttachedEvents.TryGetValue("s-button", out var rowEvents));
        Assert.Contains("keydown", rowEvents);
    }

    [Fact]
    public void Matches_is_the_same_rule_the_component_filters_by()
    {
        // Public so a page can fill a list by the rule the field will filter it by; two copies of "what
        // counts as a match" is a difference nobody would think to test for.
        Thing[] items = [Gold, GoldPlated, Silver];

        Assert.Equal(items, PolarisAutocomplete<Thing>.Matches(items, null, item => item.Name));
        Assert.Equal(items, PolarisAutocomplete<Thing>.Matches(items, "   ", item => item.Name));
        Assert.Equal(
            [Gold, GoldPlated],
            PolarisAutocomplete<Thing>.Matches(items, " GOLD ", item => item.Name));
        Assert.Equal(
            ["Gold"],
            PolarisAutocomplete<Thing>.Matches(items, "gold", item => item.Name, (item, _) => item.Id == 1)
                .Select(item => item.Name));
    }

    /// <summary>Renders the component with the text selection and the item list a test always needs.</summary>
    private static IRenderedComponent<PolarisAutocomplete<Thing>> Render(
        PolarisTestContext context,
        Action<ComponentParameterCollectionBuilder<PolarisAutocomplete<Thing>>> configure)
        => context.Render<PolarisAutocomplete<Thing>>(parameters =>
        {
            parameters.Add(p => p.TextSelector, item => item.Name);
            configure(parameters);
        });

    /// <summary>Raises the DOM events a keystroke in the field produces.</summary>
    private static async Task TypeAsync(
        IRenderedComponent<PolarisAutocomplete<Thing>> component,
        PolarisTestContext context,
        string text)
        => await component.InvokeAsync(() => context.Interop.RaiseAsync("s-text-field", "input", text));

    /// <summary>Raises one DOM event through the dispatcher, the way a live listener would.</summary>
    private static Task RaiseAsync(
        IRenderedComponent<PolarisAutocomplete<Thing>> component,
        PolarisTestContext context,
        string tagName,
        string eventName,
        string? value = null,
        string? key = null)
        => component.InvokeAsync(() => context.Interop.RaiseAsync(tagName, eventName, value, key));

    /// <summary>Raises a keydown on the field.</summary>
    private static async Task KeyAsync(
        IRenderedComponent<PolarisAutocomplete<Thing>> component,
        PolarisTestContext context,
        string key)
        => await component.InvokeAsync(() => context.Interop.RaiseAsync("s-text-field", "keydown", key: key));

    /// <summary>The rows currently drawn, in order.</summary>
    private static IReadOnlyList<AngleSharp.Dom.IElement> Rows(IRenderedComponent<PolarisAutocomplete<Thing>> component)
        => component.FindAll("[data-polaris-autocomplete-row]");

    /// <summary>A minimal fragment, for the parameters that take rendered content.</summary>
    private static class BuilderHelper
    {
        public static RenderFragment Text(string text) => builder => builder.AddContent(0, text);
    }
}
