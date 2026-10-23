using Bunit;
using Microsoft.AspNetCore.Components;
using Polaris.Blazor.Components;
using Polaris.Blazor.Events;
using Xunit;

namespace Polaris.Blazor.Tests;

/// <summary>
/// Tests for the addable select: a plain select of saved values, with a dialog that shows them and takes
/// a new one.
/// </summary>
/// <remarks>
/// <para>
/// What is pinned here is the part a merchant would have to discover: that a value the list already holds
/// is selected rather than added twice, that a new one is persisted before it is selected, and that a
/// blank field is refused with a message rather than quietly doing nothing.
/// </para>
/// <para>
/// The component renders three buttons — the add button, then the dialog's confirm and cancel — and each
/// attaches its own listeners. <see cref="RecordingPolarisInterop.Registrations"/> appends in tree order
/// on every render, so the last three entries are those buttons; <see cref="DialogButton"/> addresses them
/// from the end, which survives any number of renders in between.
/// </para>
/// </remarks>
public sealed class AddableSelectRenderingTests
{
    [Fact]
    public void It_renders_a_select_of_the_saved_values_and_a_button_that_opens_the_dialog()
    {
        // The button has to address the dialog declaratively: a modal cannot be opened from code on load,
        // and commandFor is the one form that cannot break that rule.
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisAddableSelect>(parameters => parameters
            .Add(p => p.Label, "Supplier")
            .Add(p => p.Value, "Cookson Gold")
            .Add(p => p.Options, ["Cookson Gold", "Rio Grande"])
            .Add(p => p.EmptyOptionLabel, "No supplier"));

        var select = component.Find("s-select");
        Assert.Equal("Supplier", select.GetAttribute("label"));

        // s-select does not reflect its value, so it goes in as a DOM property after the element
        // upgrades — asserting the attribute would pass against markup that selects nothing.
        Assert.True(context.Interop.SetProperties.TryGetValue("s-select", out var properties));
        Assert.Contains(properties!, property => property.TryGetValue("value", out var value) && (string?)value == "Cookson Gold");

        var options = component.FindAll("s-option");
        Assert.Equal(["No supplier", "Cookson Gold", "Rio Grande"], options.Select(o => o.TextContent));

        var dialog = component.Find("s-modal");
        Assert.NotNull(dialog.GetAttribute("id"));

        var add = component.FindAll("s-button")[0];
        Assert.Equal("Add new", add.TextContent);
        Assert.Equal("plus", add.GetAttribute("icon"));
        Assert.Equal(dialog.GetAttribute("id"), add.GetAttribute("commandFor"));
    }

    [Fact]
    public async Task Choosing_in_the_select_reports_the_value_and_the_empty_option_says_nothing_chosen()
    {
        // The empty option is not the select's placeholder: a merchant who has chosen a value must be able
        // to choose "none" again, and a placeholder disappears the moment a choice is made. It carries a
        // sentinel rather than an empty value, because measured in a browser an s-option with an empty
        // value reports its own LABEL — which would have saved the supplier "No supplier". The two states
        // travel as null, not as two spellings, so the rest of the app has one meaning for "nothing".
        using var context = new PolarisTestContext();

        string? reported = "not-raised";

        var component = context.Render<PolarisAddableSelect>(parameters => parameters
            .Add(p => p.Options, ["Cookson Gold"])
            .Add(p => p.EmptyOptionLabel, "No supplier")
            .Add(p => p.ValueChanged, value => reported = value));

        Assert.Equal(PolarisAddableSelect.EmptyOptionValue, component.FindAll("s-option")[0].GetAttribute("value"));

        await component.InvokeAsync(() => context.Interop.RaiseAsync("s-select", "change", "Cookson Gold"));
        Assert.Equal("Cookson Gold", reported);

        await component.InvokeAsync(() =>
            context.Interop.RaiseAsync("s-select", "change", PolarisAddableSelect.EmptyOptionValue));
        Assert.Null(reported);
    }

    [Fact]
    public void With_nothing_chosen_the_select_shows_the_none_option_rather_than_a_blank()
    {
        // The select is given the sentinel, not null, so its own state says "none chosen" and the label the
        // merchant reads is the one they chose on the form. Null would leave the select blank, which is a
        // third state nobody asked for.
        using var context = new PolarisTestContext();

        context.Render<PolarisAddableSelect>(parameters => parameters
            .Add(p => p.Value, null)
            .Add(p => p.Options, ["Cookson Gold"])
            .Add(p => p.EmptyOptionLabel, "No supplier"));

        Assert.True(context.Interop.SetProperties.TryGetValue("s-select", out var properties));
        Assert.Contains(
            properties!,
            property => property.TryGetValue("value", out var value) && (string?)value == PolarisAddableSelect.EmptyOptionValue);
    }

    [Fact]
    public void With_no_caller_content_the_dialog_lists_what_is_already_saved()
    {
        // The built-in list is the fallback for a caller that has nothing to manage: it is there so the
        // field below is not the only thing on screen. It is a read-only record of what exists, not a
        // second chooser, because the select behind the dialog is where a value is chosen.
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisAddableSelect>(parameters => parameters
            .Add(p => p.Options, ["Cookson Gold", "Rio Grande"])
            .Add(p => p.DialogListLabel, "Your suppliers"));

        var dialog = component.Find("s-modal");
        Assert.Equal("Your suppliers", dialog.QuerySelector("s-text")!.TextContent);
        Assert.Equal(
            ["Cookson Gold", "Rio Grande"],
            dialog.QuerySelectorAll("s-list-item").Select(item => item.TextContent));

        // The field below the list is always there: the list says what exists, the field is how a new one
        // is added, and a dialog with one of the two is a dialog that cannot do the half it is missing.
        Assert.NotNull(dialog.QuerySelector("s-text-field"));
    }

    [Fact]
    public void A_caller_that_draws_its_own_list_does_not_get_the_built_in_one_as_well()
    {
        // The same list twice, one of them read-only, is what the merchant sees when both are drawn.
        // Supplying DialogContent is the caller saying it has the saved values covered — usually with an
        // editable grid, which can also rename and delete.
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisAddableSelect>(parameters => parameters
            .Add(p => p.Options, ["Cookson Gold", "Rio Grande"])
            .Add(p => p.DialogListLabel, "Your suppliers")
            .Add(p => p.DialogContent, (RenderFragment)(content =>
                content.AddMarkupContent(0, "<s-table data-managed=\"true\"></s-table>"))));

        var dialog = component.Find("s-modal");
        Assert.NotNull(dialog.QuerySelector("s-table[data-managed='true']"));
        Assert.Empty(dialog.QuerySelectorAll("s-list-item"));
        Assert.Null(dialog.QuerySelector("s-unordered-list"));
    }

    [Fact]
    public void The_dialog_says_so_when_nothing_has_been_saved_yet()
    {
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisAddableSelect>(parameters => parameters
            .Add(p => p.Options, [])
            .Add(p => p.DialogEmptyLabel, "No suppliers saved yet."));

        var dialog = component.Find("s-modal");
        Assert.Empty(dialog.QuerySelectorAll("s-list-item"));
        Assert.Contains("No suppliers saved yet.", dialog.TextContent);
    }

    [Fact]
    public async Task Confirming_a_new_value_persists_it_first_and_then_selects_it()
    {
        // The order matters and is not cosmetic: the caller saves the value and refreshes the list, so by
        // the time the select is told what to show, the option it has to show exists. Selecting first
        // would leave a select briefly showing a value it has no option for.
        using var context = new PolarisTestContext();

        var order = new List<string>();
        string? selected = null;

        var component = context.Render<PolarisAddableSelect>(parameters => parameters
            .Add(p => p.Options, ["Cookson Gold"])
            .Add(p => p.OptionAdded, value => order.Add($"added:{value}"))
            .Add(p => p.ValueChanged, value =>
            {
                selected = value;
                order.Add($"selected:{value}");
            }));

        await component.InvokeAsync(() => context.Interop.RaiseAsync("s-text-field", "input", "  Acme Metals "));
        await ConfirmAsync(component, context);

        Assert.Equal(["added:Acme Metals", "selected:Acme Metals"], order);
        Assert.Equal("Acme Metals", selected);
    }

    [Fact]
    public async Task A_value_the_list_already_holds_is_selected_rather_than_added_twice()
    {
        // Typed by hand, "acme metals" must land on "Acme Metals" rather than beside it — the same
        // case-insensitive comparison the service and the database use. The caller is told, because the
        // list not growing is otherwise a mystery.
        using var context = new PolarisTestContext();

        var added = new List<string>();
        var duplicate = new List<string>();
        string? selected = null;

        var component = context.Render<PolarisAddableSelect>(parameters => parameters
            .Add(p => p.Options, ["Acme Metals"])
            .Add(p => p.OptionAdded, value => added.Add(value))
            .Add(p => p.DuplicateIgnored, value => duplicate.Add(value))
            .Add(p => p.ValueChanged, value => selected = value));

        await component.InvokeAsync(() => context.Interop.RaiseAsync("s-text-field", "input", " acme METALS "));
        await ConfirmAsync(component, context);

        Assert.Empty(added);
        Assert.Equal(["Acme Metals"], duplicate);

        // The stored spelling wins: selecting what the merchant typed would have put a second spelling
        // of an existing supplier onto the record.
        Assert.Equal("Acme Metals", selected);
    }

    [Fact]
    public async Task A_blank_field_is_refused_with_a_message_and_nothing_is_saved()
    {
        // Otherwise the confirm button appears to work and nothing happens, which reads as a broken dialog.
        using var context = new PolarisTestContext();

        var added = new List<string>();
        var selected = new List<string?>();

        var component = context.Render<PolarisAddableSelect>(parameters => parameters
            .Add(p => p.Options, ["Cookson Gold"])
            .Add(p => p.BlankMessage, "Enter a supplier first.")
            .Add(p => p.OptionAdded, value => added.Add(value))
            .Add(p => p.ValueChanged, value => selected.Add(value)));

        await component.InvokeAsync(() => context.Interop.RaiseAsync("s-text-field", "input", "   "));
        await ConfirmAsync(component, context);

        Assert.Empty(added);
        Assert.Empty(selected);

        var field = component.Find("s-modal").QuerySelector("s-text-field")!;
        Assert.Equal("Enter a supplier first.", field.GetAttribute("error"));
    }

    [Fact]
    public async Task Typing_again_clears_the_complaint()
    {
        // A message that outlives the correction accuses a field the merchant has already fixed.
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisAddableSelect>(parameters => parameters
            .Add(p => p.Options, ["Cookson Gold"]));

        await component.InvokeAsync(() => context.Interop.RaiseAsync("s-text-field", "input", ""));
        await ConfirmAsync(component, context);
        Assert.NotNull(component.Find("s-modal").QuerySelector("s-text-field")!.GetAttribute("error"));

        await component.InvokeAsync(() => context.Interop.RaiseAsync("s-text-field", "input", "Acme"));

        Assert.Null(component.Find("s-modal").QuerySelector("s-text-field")!.GetAttribute("error"));
    }

    [Fact]
    public void The_dialogs_actions_are_direct_children_of_the_modal_in_their_polaris_slots()
    {
        // Two things have to be true at once, and both were wrong at first: a validated slot rejects a
        // wrapper element, so the slot attribute goes on the button itself; and Polaris only assigns a
        // slot for the HOST's own children, so a button nested inside the body's stack is ignored and the
        // dialog shows no OK and no Cancel at all. The buttons are therefore siblings of the body.
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisAddableSelect>(parameters => parameters
            .Add(p => p.Options, ["Cookson Gold"])
            .Add(p => p.DialogConfirmLabel, "OK")
            .Add(p => p.CancelLabel, "Cancel"));

        var dialog = component.Find("s-modal");

        var directButtons = dialog.Children
            .Where(child => child.LocalName == "s-button")
            .ToList();

        Assert.Equal(2, directButtons.Count);
        Assert.Equal("primary-action", directButtons[0].GetAttribute("slot"));
        Assert.Equal("OK", directButtons[0].TextContent);
        Assert.Equal("secondary-actions", directButtons[1].GetAttribute("slot"));
        Assert.Equal("Cancel", directButtons[1].TextContent);

        // Cancel closes declaratively; confirm runs in C#, because saving is not a gesture.
        Assert.Equal(dialog.GetAttribute("id"), directButtons[1].GetAttribute("commandFor"));
        Assert.Equal("--hide", directButtons[1].GetAttribute("command"));

        // And nothing that looks like an action is buried in the body, where its slot would be ignored.
        Assert.Empty(dialog.QuerySelectorAll("s-stack s-button[slot]"));
    }

    [Fact]
    public void The_dialog_has_OK_and_Cancel_by_default()
    {
        // A dialog with a field and no way to finish it is a dialog the merchant has to escape from with
        // the backdrop, which is not a decision they can see.
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisAddableSelect>(parameters => parameters
            .Add(p => p.Options, ["Cookson Gold"]));

        var labels = component.Find("s-modal").Children
            .Where(child => child.LocalName == "s-button")
            .Select(child => child.TextContent)
            .ToList();

        Assert.Equal(["OK", "Cancel"], labels);
    }

    [Fact]
    public void A_caller_can_put_its_own_content_in_the_dialog()
    {
        // The component owns the select, the add field and the actions; whatever manages the saved values
        // — an editable grid, a warning, nothing — is the caller's, because only it knows what a rename
        // or a delete should do to the records that already use the value.
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisAddableSelect>(parameters => parameters
            .Add(p => p.Options, ["Cookson Gold"])
            .Add(p => p.DialogContent, (RenderFragment)(content =>
                content.AddMarkupContent(0, "<s-table data-managed=\"true\"></s-table>"))));

        var dialog = component.Find("s-modal");
        var managed = dialog.QuerySelector("s-table[data-managed='true']");

        Assert.NotNull(managed);

        // Above the field, so the merchant reads what exists before adding to it.
        var field = dialog.QuerySelector("s-text-field");
        Assert.NotNull(field);
        Assert.True(managed!.CompareDocumentPosition(field!) == AngleSharp.Dom.DocumentPositions.Following);
    }

    [Fact]
    public void A_disabled_field_disables_both_the_select_and_the_add_button()
    {
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisAddableSelect>(parameters => parameters
            .Add(p => p.Options, ["Cookson Gold"])
            .Add(p => p.Disabled, true));

        Assert.True(component.Find("s-select").HasAttribute("disabled"));
        Assert.True(component.FindAll("s-button")[0].HasAttribute("disabled"));
    }

    /// <summary>
    /// Presses the dialog's confirm button, which is the second of the last three attached buttons.
    /// </summary>
    /// <param name="component">The rendered component.</param>
    /// <param name="context">Its test context, holding the recorded registrations.</param>
    /// <returns>Nothing.</returns>
    private static Task ConfirmAsync(IRenderedComponent<PolarisAddableSelect> component, PolarisTestContext context)
    {
        var buttons = context.Interop.Registrations["s-button"];
        var confirm = buttons[^2]["click"];

        return component.InvokeAsync(() => confirm.InvokeAsync(new PolarisEventArgs()));
    }
}
