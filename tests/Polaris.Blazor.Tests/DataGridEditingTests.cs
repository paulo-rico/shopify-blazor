using System.Globalization;
using System.Linq.Expressions;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Polaris.Blazor.Components;
using Polaris.Blazor.DataGrid;
using Polaris.Blazor.Events;
using Xunit;

namespace Polaris.Blazor.Tests;

/// <summary>
/// Rendering and behaviour tests for the editable <see cref="PolarisDataGrid{TItem}"/> — the half of
/// spreadsheet editing a component test can see: which editor a cell draws, what a commit becomes, and
/// when a draft reaches the collection and the caller's save callback.
/// </summary>
/// <remarks>
/// <para>
/// A keystroke really pressed in a browser cannot be reproduced here: Blazor's own DOM event handlers
/// are invoked by bUnit directly, but whether a key pressed inside a Polaris field's shadow root reaches
/// the handler at all is a question only a browser answers, and
/// <c>tools/verify-in-browser.mjs</c> answers it. What is asserted here is everything that would
/// otherwise fail silently: that a read-only grid still renders exactly the markup it always did, that
/// a value typed as text becomes a decimal, that a refused conversion keeps the editor open, and that a
/// failed save leaves the collection alone.
/// </para>
/// <para>
/// Every test states the failure it prevents.
/// </para>
/// </remarks>
public sealed class DataGridEditingTests
{
    [Fact]
    public void A_read_only_grid_renders_no_cell_wrapper_at_all()
    {
        // The whole feature is opt-in. A grid that is not editable has to render the markup it rendered
        // before editing existed, down to the absence of a wrapper: a focusable cell in a read-only
        // table is a cell that invites a click it will not act on.
        using var context = new PolarisTestContext();
        using var state = NewState();

        var grid = RenderGrid(context, state);

        Assert.Empty(grid.FindAll("[data-polaris-grid-cell]"));
        Assert.Empty(grid.FindAll("[data-polaris-grid-editor]"));
        Assert.DoesNotContain("tabindex", grid.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void An_editable_grid_wraps_only_the_columns_that_are_editable()
    {
        // The split is what lets one grid keep its identifier read-only. A grid-level flag alone would
        // make every column a text box, including the id the row is identified by.
        using var context = new PolarisTestContext();
        using var state = NewState();

        var grid = RenderGrid(context, state, editable: true, editableColumns: ["Name", "Silver"]);

        // Two editable columns across three rows.
        Assert.Equal(6, grid.FindAll("[data-polaris-grid-cell]").Count);
        Assert.Contains("data-polaris-grid-cell=\"Name\"", grid.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("data-polaris-grid-cell=\"Carat\"", grid.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Clicking_an_editable_cell_draws_its_editor_focused()
    {
        // The three things that make a cell editable at all: the editor appears, it is the editor the
        // property's type asks for, and the caret is moved into it. An editor that renders without focus
        // looks right and takes no typing.
        using var context = new PolarisTestContext();
        using var state = NewState();

        var grid = RenderGrid(context, state, editable: true, editableColumns: ["Name", "Silver"]);

        var cell = grid.FindAll("[data-polaris-grid-cell=\"Silver\"]")[0];
        grid.InvokeAsync(() => cell.MouseDown(new MouseEventArgs()));

        var editor = grid.Find("[data-polaris-grid-editing]");
        Assert.NotNull(editor);

        // A decimal is a number field; the value seeded into it is the invariant, unformatted one.
        var field = editor.QuerySelector("s-number-field");
        Assert.NotNull(field);
        Assert.Equal("92.5", field!.GetAttribute("defaultValue"));

        // Focusing a Polaris host does nothing — the input is in its shadow root — so the grid asks the
        // interop layer for it rather than calling FocusAsync on the host.
        Assert.Equal(1, context.Interop.FocusWithinCount);
    }

    [Fact]
    public void A_boolean_column_gets_a_checkbox_and_a_date_column_gets_a_date_field()
    {
        // Auto-inference is the default, so the mapping from a property's type to a control has to be
        // right without the caller saying anything: a date typed into a text box is a string nobody can
        // parse, and a bool typed into one is a spelling test.
        using var context = new PolarisTestContext();
        using var state = NewState();

        var grid = RenderGrid(context, state, editable: true, editableColumns: ["Active", "Added"]);

        grid.InvokeAsync(() => grid.FindAll("[data-polaris-grid-cell=\"Active\"]")[0].MouseDown(new MouseEventArgs()));
        Assert.NotNull(grid.Find("[data-polaris-grid-editing]").QuerySelector("s-checkbox"));

        grid.InvokeAsync(() => grid.FindAll("[data-polaris-grid-cell=\"Added\"]")[0].MouseDown(new MouseEventArgs()));
        Assert.True(grid.Find("[data-polaris-grid-editing]").QuerySelector("s-date-field") is not null, grid.Markup);
    }

    [Fact]
    public void A_select_editor_offers_the_values_the_dataset_contains()
    {
        // Options derived from the data are the ones that can actually be chosen; a hand-written list
        // drifts the moment a record is added. The editor's own value has to be one of them, or the
        // select starts with nothing chosen for a cell that plainly has a value.
        using var context = new PolarisTestContext();
        using var state = NewState();

        var grid = context.Render<PolarisDataGrid<Alloy>>(parameters => parameters
            .Add(p => p.State, state)
            .Add(p => p.Editable, true)
            .Add(p => p.Columns, builder =>
            {
#pragma warning disable ASP0006
                builder.OpenComponent<PolarisGridColumn<Alloy>>(0);
                builder.AddAttribute(1, nameof(PolarisGridColumn<Alloy>.Field), (Expression<Func<Alloy, object?>>)(alloy => alloy.Name));
                builder.AddAttribute(2, nameof(PolarisGridColumn<Alloy>.Id), "Name");
                builder.AddAttribute(3, nameof(PolarisGridColumn<Alloy>.Editable), true);
                builder.AddAttribute(4, nameof(PolarisGridColumn<Alloy>.EditorType), PolarisGridEditorType.Select);
                builder.CloseComponent();
#pragma warning restore ASP0006
            }));

        grid.InvokeAsync(() => grid.FindAll("[data-polaris-grid-cell=\"Name\"]")[0].MouseDown(new MouseEventArgs()));

        var editor = grid.Find("[data-polaris-grid-editing]");
        Assert.NotNull(editor.QuerySelector("s-select"));

        var options = editor.QuerySelectorAll("s-option").Select(option => option.GetAttribute("value")).ToList();
        Assert.Equal(["Fine silver 999", "Yellow gold 18ct", "Yellow gold 9ct"], options);

        // Seeded with the row's own value, as a property: Polaris does not reflect a select's value, so
        // an option merely marked as chosen would leave the control blank.
        Assert.Equal("Fine silver 999", context.Interop.SetProperties["s-select"][^1]["value"]);
    }

    [Fact]
    public async Task Typing_a_number_and_pressing_Enter_drafts_it_moves_down_and_keeps_the_type()
    {
        // The whole point of the feature in one test: the text arrives as a string, becomes a decimal of
        // the column's own type, and lands in the collection as one. A draft stored as the string the
        // field reported would compare, sort and format as text — 9 above 18 — for ever after.
        using var context = new PolarisTestContext();
        using var state = NewState();
        var saved = new List<PolarisGridRowEdit<Alloy>>();

        var grid = RenderGrid(
            context,
            state,
            editable: true,
            editableColumns: ["Silver"],
            saveChanges: (edit, _) => { saved.Add(edit); return Task.FromResult(PolarisGridSaveResult.Success()); });

        var cell = grid.FindAll("[data-polaris-grid-cell=\"Silver\"]")[0];
        await grid.InvokeAsync(() => cell.MouseDown(new MouseEventArgs()));

        // What the browser does: `input` on every keystroke, then a keydown for Enter.
        await grid.InvokeAsync(() => context.Interop.RaiseAsync("s-number-field", "input", "97.25"));
        await grid.InvokeAsync(() => grid.Find("[data-polaris-grid-editing]").KeyDown(new KeyboardEventArgs { Key = "Enter" }));

        var edit = Assert.Single(saved);
        Assert.Equal(97.25m, edit.Changes["Silver"]);

        // Saved immediately, so the collection holds it and the query has been re-run.
        Assert.Equal(97.25m, state.FindItem(1)!.Silver);

        // Enter moved down, so the second row's Silver cell is now the one being edited.
        Assert.Equal("2", grid.Find("[data-polaris-grid-editing]").GetAttribute("data-polaris-grid-row"));
    }

    [Fact]
    public async Task A_typed_value_that_is_not_a_number_keeps_the_editor_open_with_a_message()
    {
        // Silently discarding it is the worst option — the merchant sees the cell revert and concludes
        // the grid lost their work. Committing it as text is the second worst: the column is a decimal
        // and everything downstream would compare it as a string.
        using var context = new PolarisTestContext();
        using var state = NewState();

        var grid = RenderGrid(context, state, editable: true, editableColumns: ["Silver"]);

        await grid.InvokeAsync(() => grid.FindAll("[data-polaris-grid-cell=\"Silver\"]")[0].MouseDown(new MouseEventArgs()));
        await grid.InvokeAsync(() => context.Interop.RaiseAsync("s-number-field", "input", "a lot"));
        await grid.InvokeAsync(() => grid.Find("[data-polaris-grid-editing]").KeyDown(new KeyboardEventArgs { Key = "Enter" }));

        var editor = grid.Find("[data-polaris-grid-editing]");
        Assert.Equal("'a lot' is not a number.", editor.QuerySelector("s-number-field")!.GetAttribute("error"));

        // Still in the same cell, so the merchant can correct it rather than retype it.
        Assert.Equal("1", editor.GetAttribute("data-polaris-grid-row"));
        Assert.False(grid.Instance.HasUnsavedChanges);
    }

    [Fact]
    public async Task Escape_drops_the_cell_draft_and_closes_the_editor()
    {
        // Escape is the only way out of an edit that does not commit one, and without it the only exit
        // from a mistaken entry is to enter a second one.
        using var context = new PolarisTestContext();
        using var state = NewState();

        var grid = RenderGrid(
            context,
            state,
            editable: true,
            editableColumns: ["Silver"],
            saveMode: PolarisGridSaveMode.Manual);

        await grid.InvokeAsync(() => grid.FindAll("[data-polaris-grid-cell=\"Silver\"]")[0].MouseDown(new MouseEventArgs()));
        await grid.InvokeAsync(() => context.Interop.RaiseAsync("s-number-field", "input", "12"));
        await grid.InvokeAsync(() => grid.Find("[data-polaris-grid-editing]").KeyDown(new KeyboardEventArgs { Key = "Escape" }));

        Assert.Empty(grid.FindAll("[data-polaris-grid-editing]"));
        Assert.False(grid.Instance.HasUnsavedChanges);
        Assert.Equal(92.5m, state.FindItem(1)!.Silver);
    }

    [Fact]
    public async Task A_manual_grid_keeps_edits_as_drafts_until_save_and_shows_them_meanwhile()
    {
        // A draft is not a change to the record: that is what keeps the row from moving out from under
        // the merchant when they edit the column it is sorted by — and what makes a rejected save leave
        // the collection exactly as the store has it.
        using var context = new PolarisTestContext();
        using var state = NewState();
        var saves = 0;

        var grid = RenderGrid(
            context,
            state,
            editable: true,
            editableColumns: ["Silver"],
            saveMode: PolarisGridSaveMode.Manual,
            saveChanges: (_, _) => { saves++; return Task.FromResult(PolarisGridSaveResult.Success()); });

        await grid.InvokeAsync(() => grid.FindAll("[data-polaris-grid-cell=\"Silver\"]")[0].MouseDown(new MouseEventArgs()));
        await grid.InvokeAsync(() => context.Interop.RaiseAsync("s-number-field", "input", "50"));
        await grid.InvokeAsync(() => grid.Find("[data-polaris-grid-editing]").KeyDown(new KeyboardEventArgs { Key = "Enter" }));

        // Nothing reached the store or the collection...
        Assert.Equal(0, saves);
        Assert.Equal(92.5m, state.FindItem(1)!.Silver);

        // ...the draft is on screen, marked, and counted by a save bar...
        Assert.Equal(1, grid.Instance.UnsavedRowCount);
        var draftCell = grid.FindAll("[data-polaris-grid-cell=\"Silver\"]").First(cell => cell.HasAttribute("data-polaris-grid-dirty"));

        // Formatted like every other cell in the column, and not like the editor: the draft is a value of
        // the column's type by then, so the column's own format applies to it.
        Assert.Equal("50.0", draftCell.TextContent.Trim());

        var bar = grid.Find("[data-polaris-grid-save-bar]");
        Assert.Contains("1 row changed", bar.TextContent, StringComparison.Ordinal);

        // ...and the button saves it, through the same handler a click in the browser reaches.
        var saveButton = grid.FindComponents<PolarisButton>()
            .Single(button => button.Markup.Contains("Save changes", StringComparison.Ordinal));

        await grid.InvokeAsync(() => saveButton.Instance.OnClick.InvokeAsync(new PolarisEventArgs()));

        Assert.Equal(1, saves);
        Assert.Equal(50m, state.FindItem(1)!.Silver);
        Assert.False(grid.Instance.HasUnsavedChanges);
        Assert.Empty(grid.FindAll("[data-polaris-grid-save-bar]"));
    }

    [Fact]
    public async Task A_failed_save_keeps_the_draft_shows_the_message_and_leaves_the_collection_alone()
    {
        // The one thing a save callback exists to report. Applying the edit to the collection anyway
        // would make the grid disagree with the store for the rest of the session, and dropping the
        // draft would throw away the merchant's typing for a reason they cannot see.
        using var context = new PolarisTestContext();
        using var state = NewState();

        var grid = RenderGrid(
            context,
            state,
            editable: true,
            editableColumns: ["Silver"],
            saveMode: PolarisGridSaveMode.Manual,
            saveChanges: (_, _) => Task.FromResult(PolarisGridSaveResult.Failure("Silver cannot exceed 100.")));

        await grid.InvokeAsync(() => grid.FindAll("[data-polaris-grid-cell=\"Silver\"]")[0].MouseDown(new MouseEventArgs()));
        await grid.InvokeAsync(() => context.Interop.RaiseAsync("s-number-field", "input", "150"));
        await grid.InvokeAsync(() => grid.Find("[data-polaris-grid-editing]").KeyDown(new KeyboardEventArgs { Key = "Enter" }));

        var result = await grid.InvokeAsync(() => grid.Instance.SaveAsync());

        Assert.False(result.Succeeded);
        Assert.Equal("Silver cannot exceed 100.", result.Message);

        Assert.Equal(92.5m, state.FindItem(1)!.Silver);
        Assert.True(grid.Instance.HasUnsavedChanges);
        Assert.Contains("Silver cannot exceed 100.", grid.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_save_callback_that_throws_is_reported_rather_than_taking_the_circuit_down()
    {
        // A caller that has not read the contract should still fail safely: the failure belongs under
        // the merchant's cursor, not in the circuit's exception log.
        using var context = new PolarisTestContext();
        using var state = NewState();

        var grid = RenderGrid(
            context,
            state,
            editable: true,
            editableColumns: ["Silver"],
            saveMode: PolarisGridSaveMode.Manual,
            saveChanges: (_, _) => throw new InvalidOperationException("The database is unreachable."));

        await grid.InvokeAsync(() => grid.FindAll("[data-polaris-grid-cell=\"Silver\"]")[0].MouseDown(new MouseEventArgs()));
        await grid.InvokeAsync(() => context.Interop.RaiseAsync("s-number-field", "input", "50"));
        await grid.InvokeAsync(() => grid.Find("[data-polaris-grid-editing]").KeyDown(new KeyboardEventArgs { Key = "Enter" }));

        var result = await grid.InvokeAsync(() => grid.Instance.SaveAsync());

        Assert.False(result.Succeeded);
        Assert.Contains("The database is unreachable.", result.Message!, StringComparison.Ordinal);
        Assert.Equal(92.5m, state.FindItem(1)!.Silver);
    }

    [Fact]
    public async Task Committing_a_value_the_record_already_holds_leaves_the_row_clean()
    {
        // Tabbing through a row without changing anything must not produce a save of nothing, and must
        // not leave a row marked as edited for a merchant who edited nothing.
        using var context = new PolarisTestContext();
        using var state = NewState();
        var saves = 0;

        var grid = RenderGrid(
            context,
            state,
            editable: true,
            editableColumns: ["Silver"],
            saveMode: PolarisGridSaveMode.Manual,
            saveChanges: (_, _) => { saves++; return Task.FromResult(PolarisGridSaveResult.Success()); });

        await grid.InvokeAsync(() => grid.FindAll("[data-polaris-grid-cell=\"Silver\"]")[0].MouseDown(new MouseEventArgs()));
        await grid.InvokeAsync(() => grid.Find("[data-polaris-grid-editing]").KeyDown(new KeyboardEventArgs { Key = "Enter" }));

        Assert.False(grid.Instance.HasUnsavedChanges);
        Assert.Equal(0, saves);
    }

    [Fact]
    public async Task Tab_moves_right_and_Shift_Tab_left_through_the_editable_cells()
    {
        // The two keys that make a grid usable without a mouse. Tab leaving the cell rather than the
        // table is what lets a merchant fill a row in order; Shift+Tab is the correction.
        using var context = new PolarisTestContext();
        using var state = NewState();

        var grid = RenderGrid(context, state, editable: true, editableColumns: ["Name", "Silver"], saveMode: PolarisGridSaveMode.Manual);

        await grid.InvokeAsync(() => grid.FindAll("[data-polaris-grid-cell=\"Name\"]")[0].MouseDown(new MouseEventArgs()));
        await grid.InvokeAsync(() => grid.Find("[data-polaris-grid-editing]").KeyDown(new KeyboardEventArgs { Key = "Tab" }));

        var moved = grid.Find("[data-polaris-grid-editing]");
        Assert.Equal("Silver", moved.GetAttribute("data-polaris-grid-cell"));
        Assert.Equal("1", moved.GetAttribute("data-polaris-grid-row"));

        await grid.InvokeAsync(() => moved.KeyDown(new KeyboardEventArgs { Key = "Tab", ShiftKey = true }));

        var back = grid.Find("[data-polaris-grid-editing]");
        Assert.Equal("Name", back.GetAttribute("data-polaris-grid-cell"));
        Assert.Equal("1", back.GetAttribute("data-polaris-grid-row"));
    }

    [Fact]
    public async Task Tab_off_the_last_cell_leaves_the_table()
    {
        // A keyboard user has to be able to get out. Wrapping to the first cell instead traps them in the
        // grid with no way to reach anything below it.
        using var context = new PolarisTestContext();
        using var state = NewState();

        var grid = RenderGrid(context, state, editable: true, editableColumns: ["Silver"], saveMode: PolarisGridSaveMode.Manual);

        // The last row's only editable cell.
        await grid.InvokeAsync(() => grid.FindAll("[data-polaris-grid-cell=\"Silver\"]")[2].MouseDown(new MouseEventArgs()));
        await grid.InvokeAsync(() => grid.Find("[data-polaris-grid-editing]").KeyDown(new KeyboardEventArgs { Key = "Tab" }));

        Assert.Empty(grid.FindAll("[data-polaris-grid-editing]"));
    }

    [Fact]
    public async Task A_boolean_cell_commits_itself_as_a_boolean()
    {
        // A checkbox has no draft state to type towards, and a tick that only took effect on Enter would
        // be indistinguishable from a tick that did nothing. The value it commits is a bool, not the
        // string "false" the field reports.
        using var context = new PolarisTestContext();
        using var state = NewState();

        var grid = RenderGrid(
            context,
            state,
            editable: true,
            editableColumns: ["Active"],
            saveMode: PolarisGridSaveMode.Manual);

        Assert.True(state.FindItem(3)!.Active);

        await grid.InvokeAsync(() => grid.FindAll("[data-polaris-grid-cell=\"Active\"]")[2].MouseDown(new MouseEventArgs()));
        await grid.InvokeAsync(() => context.Interop.RaiseAsync("s-checkbox", "change", value: null));

        // A draft, in Manual mode: on screen and counted, and the record untouched.
        Assert.Equal(1, grid.Instance.UnsavedRowCount);
        Assert.True(state.FindItem(3)!.Active);

        await grid.InvokeAsync(() => grid.Instance.SaveAsync());

        Assert.False(state.FindItem(3)!.Active);
        Assert.False(grid.Instance.HasUnsavedChanges);
    }

    [Fact]
    public async Task A_cell_that_is_typed_into_marks_itself_dirty_and_one_that_is_not_does_not()
    {
        // The mark is the only thing on screen that distinguishes a committed draft from a saved value
        // in Manual mode, and it is what the browser check reads to know an edit really landed.
        using var context = new PolarisTestContext();
        using var state = NewState();

        var grid = RenderGrid(context, state, editable: true, editableColumns: ["Silver"], saveMode: PolarisGridSaveMode.Manual);

        Assert.Empty(grid.FindAll("[data-polaris-grid-dirty]"));

        await grid.InvokeAsync(() => grid.FindAll("[data-polaris-grid-cell=\"Silver\"]")[0].MouseDown(new MouseEventArgs()));
        await grid.InvokeAsync(() => context.Interop.RaiseAsync("s-number-field", "input", "42"));
        await grid.InvokeAsync(() => grid.Find("[data-polaris-grid-editing]").KeyDown(new KeyboardEventArgs { Key = "Enter" }));

        var dirty = Assert.Single(grid.FindAll("[data-polaris-grid-dirty]"));
        Assert.Equal("1", dirty.GetAttribute("data-polaris-grid-row"));
    }

    [Fact]
    public void An_editable_column_with_nowhere_to_write_the_value_is_refused()
    {
        // A computed column cannot be edited, and a cell that accepts typing and discards it is worse
        // than one that is plainly read-only — the merchant has no way to tell which they are looking at.
        using var context = new PolarisTestContext();
        using var state = NewState();

        var error = Assert.ThrowsAny<Exception>(() => context.Render<PolarisDataGrid<Alloy>>(parameters => parameters
            .Add(p => p.State, state)
            .Add(p => p.Editable, true)
            .Add(p => p.Columns, builder =>
            {
#pragma warning disable ASP0006
                builder.OpenComponent<PolarisGridColumn<Alloy>>(0);
                builder.AddAttribute(1, nameof(PolarisGridColumn<Alloy>.Title), "Carat in points");
                builder.AddAttribute(2, nameof(PolarisGridColumn<Alloy>.Id), "CaratPoints");
                builder.AddAttribute(3, nameof(PolarisGridColumn<Alloy>.Field), (Expression<Func<Alloy, object?>>)(alloy => alloy.Carat * 100));
                builder.AddAttribute(4, nameof(PolarisGridColumn<Alloy>.Editable), true);
                builder.CloseComponent();
#pragma warning restore ASP0006
            })));

        Assert.Contains("editable", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_computed_column_edited_through_a_value_setter_works()
    {
        // The escape hatch for the previous test: a column whose expression is not a property can still
        // be edited when the caller says how to write it.
        using var context = new PolarisTestContext();
        using var state = NewState();

        var grid = context.Render<PolarisDataGrid<Alloy>>(parameters => parameters
            .Add(p => p.State, state)
            .Add(p => p.Editable, true)
            .Add(p => p.SaveMode, PolarisGridSaveMode.Manual)
            .Add(p => p.Columns, builder =>
            {
#pragma warning disable ASP0006
                builder.OpenComponent<PolarisGridColumn<Alloy>>(0);
                builder.AddAttribute(1, nameof(PolarisGridColumn<Alloy>.Title), "Name in capitals");
                builder.AddAttribute(2, nameof(PolarisGridColumn<Alloy>.Field), (Expression<Func<Alloy, object?>>)(alloy => alloy.Name.ToUpperInvariant()));
                builder.AddAttribute(3, nameof(PolarisGridColumn<Alloy>.Id), "Upper");
                builder.AddAttribute(4, nameof(PolarisGridColumn<Alloy>.Editable), true);
                builder.AddAttribute(5, nameof(PolarisGridColumn<Alloy>.ValueSetter), (Func<Alloy, object?, Alloy>)((alloy, value) => alloy with { Name = (string)value! }));
                builder.CloseComponent();
#pragma warning restore ASP0006
            }));

        await grid.InvokeAsync(() => grid.FindAll("[data-polaris-grid-cell=\"Upper\"]")[0].MouseDown(new MouseEventArgs()));
        await grid.InvokeAsync(() => context.Interop.RaiseAsync("s-text-field", "input", "Changed"));
        await grid.InvokeAsync(() => grid.Find("[data-polaris-grid-editing]").KeyDown(new KeyboardEventArgs { Key = "Enter" }));

        await grid.InvokeAsync(() => grid.Instance.SaveAsync());

        Assert.Equal("Changed", state.FindItem(1)!.Name);
    }

    [Fact]
    public async Task An_edit_that_pushes_the_row_out_of_the_filter_closes_the_editor_rather_than_drawing_it_into_another_row()
    {
        // Saving immediately re-runs the query, and the edited row can leave the page. An editor left
        // active on a row that is no longer rendered would be a draft attached to a cell nobody can see,
        // and the next row would take its place under the same column id.
        using var context = new PolarisTestContext();
        using var state = NewState();
        state.SetFilter(PolarisGridFilterDescriptor.Text("Name", "gold"));

        var grid = RenderGrid(context, state, editable: true, editableColumns: ["Name"]);

        Assert.Equal(2, grid.FindAll("[data-polaris-grid-cell=\"Name\"]").Count);

        await grid.InvokeAsync(() => grid.FindAll("[data-polaris-grid-cell=\"Name\"]")[0].MouseDown(new MouseEventArgs()));
        await grid.InvokeAsync(() => context.Interop.RaiseAsync("s-text-field", "input", "Fine silver"));
        await grid.InvokeAsync(() => grid.Find("[data-polaris-grid-editing]").KeyDown(new KeyboardEventArgs { Key = "Enter" }));

        Assert.Empty(grid.FindAll("[data-polaris-grid-editing]"));
        Assert.Equal(PolarisGridSaveMode.Immediate, grid.Instance.SaveMode);
        Assert.Equal("Fine silver", state.FindItem(2)!.Name);
    }

    // ── Fixtures ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Fresh records for every test, and not a shared static list.
    /// </summary>
    /// <remarks>
    /// A record edited through a compiled property setter is written in place — the grid cannot copy a
    /// type it knows nothing about — so two tests over one shared fixture would each see the other's
    /// edits. That is a property of the model, not a bug in the grid, and the sample screens write
    /// through <c>ValueSetter</c> exactly because they would rather copy.
    /// </remarks>
    private static PolarisGridState<Alloy> NewState() => new(
        [
            new(1, "Fine silver 999", 0, 92.5m, true, new DateOnly(2025, 5, 1)),
            new(2, "Yellow gold 9ct", 9, 75m, false, new DateOnly(2025, 6, 1)),
            new(3, "Yellow gold 18ct", 18, 0m, true, new DateOnly(2025, 7, 1)),
        ],
        alloy => alloy.Id);

    private static IRenderedComponent<PolarisDataGrid<Alloy>> RenderGrid(
        PolarisTestContext context,
        PolarisGridState<Alloy> state,
        bool editable = false,
        string[]? editableColumns = null,
        PolarisGridSaveMode saveMode = PolarisGridSaveMode.Immediate,
        Func<PolarisGridRowEdit<Alloy>, CancellationToken, Task<PolarisGridSaveResult>>? saveChanges = null)
        => context.Render<PolarisDataGrid<Alloy>>(parameters => parameters
            .Add(p => p.State, state)
            .Add(p => p.Editable, editable)
            .Add(p => p.SaveMode, saveMode)
            .Add(p => p.SaveChanges, saveChanges)
            .Add(p => p.Columns, builder =>
            {
                AddColumn(builder, 0, "Name", alloy => alloy.Name, editableColumns?.Contains("Name") == true);
                AddColumn(builder, 10, "Carat", alloy => alloy.Carat);
                AddColumn(builder, 20, "Silver", alloy => alloy.Silver, editableColumns?.Contains("Silver") == true, format: "N1");
                AddColumn(builder, 30, "Active", alloy => alloy.Active, editableColumns?.Contains("Active") == true);

                if (editableColumns?.Contains("Added") == true)
                {
                    AddColumn(builder, 40, "Added", alloy => alloy.Added, true);
                }
            }));

    private static void AddColumn(
        RenderTreeBuilder builder,
        int sequence,
        string title,
        Expression<Func<Alloy, object?>> field,
        bool editable = false,
        string? format = null)
    {
        // A helper cannot use literal sequence numbers, so the analyzer's literal rule is suppressed
        // here rather than repeating this block at every call site.
#pragma warning disable ASP0006
        builder.OpenComponent<PolarisGridColumn<Alloy>>(sequence);
        builder.AddAttribute(sequence + 1, nameof(PolarisGridColumn<Alloy>.Title), title);
        builder.AddAttribute(sequence + 2, nameof(PolarisGridColumn<Alloy>.Field), field);
        builder.AddAttribute(sequence + 3, nameof(PolarisGridColumn<Alloy>.Editable), editable);
        builder.AddAttribute(sequence + 4, nameof(PolarisGridColumn<Alloy>.Searchable), false);
        builder.AddAttribute(sequence + 5, nameof(PolarisGridColumn<Alloy>.Format), format);
        builder.CloseComponent();
#pragma warning restore ASP0006
    }

    private sealed record Alloy(int Id, string Name, int Carat, decimal Silver, bool Active, DateOnly Added = default);
}
