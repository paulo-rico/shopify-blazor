using System.Globalization;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Polaris.Blazor.Components;
using Xunit;

namespace Polaris.Blazor.Tests;

/// <summary>
/// Tests for <see cref="PolarisRangeSlider"/> and the value type it emits.
/// </summary>
/// <remarks>
/// Polaris has no slider, so this is the one control in the library whose behaviour is entirely ours —
/// and whose handles can be left in a state they cannot be moved out of. The crossing rule, the
/// collapsed-interval rule and the inline interval are asserted here because a browser shows them as
/// "it feels wrong" rather than as an error.
/// </remarks>
public sealed class RangeSliderRenderingTests
{
    [Fact]
    public void The_slider_renders_two_native_inputs_with_a_label_each()
    {
        // Two anonymous sliders are unusable with a screen reader: neither says which end of the
        // range it is.
        using var context = new PolarisTestContext();

        var slider = RenderSlider(context, min: 0, max: 100, step: 5);

        var handles = slider.FindAll("input[type=range]");
        Assert.Equal(2, handles.Count);
        Assert.Equal("Minimum", handles[0].GetAttribute("aria-label"));
        Assert.Equal("Maximum", handles[1].GetAttribute("aria-label"));
        Assert.Equal("0", handles[0].GetAttribute("min"));
        Assert.Equal("100", handles[0].GetAttribute("max"));
        Assert.Equal("5", handles[0].GetAttribute("step"));
    }

    [Fact]
    public void The_interval_is_written_inline_so_a_missing_stylesheet_costs_looks_not_behaviour()
    {
        // The handles and the keyboard support are native. What the stylesheet adds is the filled
        // interval, and it reads it from two custom properties — written here, so the control is
        // still correct with no CSS file at all.
        using var context = new PolarisTestContext();

        var slider = RenderSlider(context, min: 0, max: 100, step: 1, value: new PolarisNumberRange(25m, 75m));

        var style = slider.Find("[data-polaris-range-slider]").GetAttribute("style");
        Assert.Contains("--polaris-range-start:25%", style, StringComparison.Ordinal);
        Assert.Contains("--polaris-range-end:75%", style, StringComparison.Ordinal);
    }

    [Fact]
    public void An_unbound_slider_starts_at_the_whole_domain()
    {
        // A filter that has not been touched must not narrow anything, and "null means the full
        // domain" is how the caller says so.
        using var context = new PolarisTestContext();

        var slider = RenderSlider(context, min: 10, max: 20);

        Assert.Equal("10", slider.Find("input[data-polaris-range-handle=lower]").GetAttribute("value"));
        Assert.Equal("20", slider.Find("input[data-polaris-range-handle=upper]").GetAttribute("value"));
    }

    [Fact]
    public void Moving_a_handle_reports_the_new_interval_and_leaves_the_other_end_alone()
    {
        // The whole point of a dual control: moving one end must not move the other. Reported on `change`
        // rather than `input`, because a drag reports once — on release — and not on every pixel.
        using var context = new PolarisTestContext();
        PolarisNumberRange? reported = null;

        var slider = context.Render<PolarisRangeSlider>(parameters => parameters
            .Add(p => p.Minimum, 0m)
            .Add(p => p.Maximum, 100m)
            .Add(p => p.Step, 1m)
            .Add(p => p.Value, new PolarisNumberRange(20m, 80m))
            .Add(p => p.ValueChanged, range => reported = range));

        slider.Find("input[data-polaris-range-handle=lower]")
            .TriggerEvent("onchange", new ChangeEventArgs { Value = "35" });

        Assert.Equal(new PolarisNumberRange(35m, 80m), reported);

        slider.Find("input[data-polaris-range-handle=upper]")
            .TriggerEvent("onchange", new ChangeEventArgs { Value = "60" });

        Assert.Equal(new PolarisNumberRange(35m, 60m), reported);
    }

    [Fact]
    public void A_domain_that_grows_reopens_a_full_range_slider()
    {
        // The real failure this prevents, found by pointing the grid at an asynchronously loaded
        // catalogue: a filter panel renders before its rows do, reads a placeholder domain, and a slider
        // that only ever clamped kept that placeholder for ever. Both handles sat on the first value of
        // a domain nobody could widen — a range filter that matched everything and could not be dragged.
        using var context = new PolarisTestContext();

        var slider = RenderSlider(context, min: 0, max: 1);
        Assert.Equal("1", slider.Find("input[data-polaris-range-handle=upper]").GetAttribute("value"));

        slider.Render(parameters => parameters
            .Add(p => p.Minimum, 0m)
            .Add(p => p.Maximum, 24m)
            .Add(p => p.Step, 0.1m)
            .Add(p => p.Value, (PolarisNumberRange?)null)
            .Add(p => p.ShowInputs, false));

        Assert.Equal("0", slider.Find("input[data-polaris-range-handle=lower]").GetAttribute("value"));
        Assert.Equal("24", slider.Find("input[data-polaris-range-handle=upper]").GetAttribute("value"));
    }

    [Fact]
    public void A_handle_dragged_past_its_neighbour_stops_instead_of_swapping()
    {
        // Swapping would hand the merchant the other handle mid-drag: the value they are holding
        // would jump and keep going. Stopping is the behaviour the platform's own controls have.
        using var context = new PolarisTestContext();
        PolarisNumberRange? reported = null;

        var slider = context.Render<PolarisRangeSlider>(parameters => parameters
            .Add(p => p.Minimum, 0m)
            .Add(p => p.Maximum, 100m)
            .Add(p => p.Step, 1m)
            .Add(p => p.Value, new PolarisNumberRange(20m, 80m))
            .Add(p => p.ValueChanged, range => reported = range));

        slider.Find("input[data-polaris-range-handle=lower]")
            .TriggerEvent("onchange", new ChangeEventArgs { Value = "95" });

        Assert.Equal(new PolarisNumberRange(80m, 80m), reported);
    }

    [Fact]
    public void A_collapsed_slider_raises_the_handle_that_can_still_move()
    {
        // Both handles on the same value means the thumb on top decides where a drag starts. If that
        // thumb is the one that cannot move in either direction, the control is stuck for ever.
        using var context = new PolarisTestContext();

        var atTop = RenderSlider(context, min: 0, max: 100, step: 1, value: new PolarisNumberRange(100m, 100m));
        Assert.Equal("z-index:3", atTop.Find("input[data-polaris-range-handle=lower]").GetAttribute("style"));
        Assert.Equal("z-index:2", atTop.Find("input[data-polaris-range-handle=upper]").GetAttribute("style"));

        var inMiddle = RenderSlider(context, min: 0, max: 100, step: 1, value: new PolarisNumberRange(40m, 40m));
        Assert.Equal("z-index:3", inMiddle.Find("input[data-polaris-range-handle=upper]").GetAttribute("style"));
    }

    [Fact]
    public void The_numeric_inputs_show_the_values_invariantly_and_carry_the_unit()
    {
        // A slider at 92.5 must not read "92,5" on a German server and "92.5" on an English one.
        using var context = new PolarisTestContext();
        var original = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            var slider = context.Render<PolarisRangeSlider>(parameters => parameters
                .Add(p => p.Minimum, 0m)
                .Add(p => p.Maximum, 100m)
                .Add(p => p.Step, 0.5m)
                .Add(p => p.Value, new PolarisNumberRange(12.5m, 92.5m))
                .Add(p => p.Unit, "%"));

            var values = slider.FindAll("s-number-field")
                .Select(field => context.Interop.SetProperties["s-number-field"].Last()["value"])
                .ToList();

            Assert.NotEmpty(values);

            // Invariant and two decimals: "92,50" would be a broken number on a German server, and the
            // field states the same rounding the control uses for both ends. See
            // The_two_typed_fields_show_two_decimal_places for why that rounding exists at all.
            Assert.Equal("92.50", slider.FindAll("s-number-field")[1].GetAttribute("defaultValue"));

            // The unit is a suffix rather than part of the value, so submitting the field submits a
            // number.
            Assert.Equal("%", slider.FindAll("s-number-field")[1].GetAttribute("suffix"));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void A_disabled_slider_disables_both_handles_and_both_fields()
    {
        // Half-disabled is worse than enabled: the merchant moves a handle and nothing happens.
        using var context = new PolarisTestContext();

        var slider = context.Render<PolarisRangeSlider>(parameters => parameters
            .Add(p => p.Minimum, 0m)
            .Add(p => p.Maximum, 10m)
            .Add(p => p.Value, new PolarisNumberRange(2m, 8m))
            .Add(p => p.Disabled, true));

        Assert.All(slider.FindAll("input[type=range]"), handle => Assert.True(handle.HasAttribute("disabled")));
        Assert.All(slider.FindAll("s-number-field"), field => Assert.True(field.HasAttribute("disabled")));
    }

    [Fact]
    public void A_domain_that_changes_clamps_the_handles_into_it()
    {
        // The dataset reloads, the widest alloy is now 50, and a handle at 80 would render off the end
        // of the track — and filter on a value the slider cannot express.
        using var context = new PolarisTestContext();

        var slider = RenderSlider(context, min: 0, max: 100, step: 1, value: new PolarisNumberRange(20m, 80m));

        slider.Render(parameters => parameters
            .Add(p => p.Minimum, 0m)
            .Add(p => p.Maximum, 50m)
            .Add(p => p.Step, 1m)
            .Add(p => p.Value, new PolarisNumberRange(20m, 80m)));

        Assert.Equal("50", slider.Find("input[data-polaris-range-handle=upper]").GetAttribute("value"));
    }

    [Fact]
    public void Ticks_are_a_native_datalist()
    {
        // Drawing ticks by hand means re-deriving the browser's own thumb geometry, which drifts on
        // every platform. A datalist is placed by the browser.
        using var context = new PolarisTestContext();

        var slider = context.Render<PolarisRangeSlider>(parameters => parameters
            .Add(p => p.Minimum, 0m)
            .Add(p => p.Maximum, 4m)
            .Add(p => p.Step, 1m)
            .Add(p => p.ShowTicks, true)
            .Add(p => p.ShowInputs, false));

        var datalist = slider.Find("datalist");
        Assert.Equal(5, datalist.QuerySelectorAll("option").Length);
    }

    [Fact]
    public void A_drag_previews_on_input_and_reports_only_when_it_ends()
    {
        // The reason this exists: a range filter re-queried the grid on every pixel of a drag. Input
        // events arrive continuously (measured: four across one drag) and `change` arrives once, on
        // release — so the interval may follow the thumb on `input`, but nothing upstream may hear about
        // it until `change`. A component test can see both halves: the handler, and what it did NOT call.
        using var context = new PolarisTestContext();
        var reported = new List<PolarisNumberRange>();

        var slider = context.Render<PolarisRangeSlider>(parameters => parameters
            .Add(p => p.Minimum, 0m)
            .Add(p => p.Maximum, 100m)
            .Add(p => p.Step, 1m)
            .Add(p => p.Value, new PolarisNumberRange(20m, 80m))
            .Add(p => p.ValueChanged, range => reported.Add(range)));

        var upper = slider.Find("input[data-polaris-range-handle=upper]");

        upper.TriggerEvent("oninput", new ChangeEventArgs { Value = "60" });
        upper.TriggerEvent("oninput", new ChangeEventArgs { Value = "50" });

        Assert.Empty(reported);

        // The preview still moved this control: the interval is what draws the filled span, and a preview
        // that did not move it would look exactly like a slider that ignores the mouse.
        var style = slider.Find("[data-polaris-range-slider]").GetAttribute("style");
        Assert.Contains("--polaris-range-end:50%", style, StringComparison.Ordinal);

        upper = slider.Find("input[data-polaris-range-handle=upper]");
        upper.TriggerEvent("onchange", new ChangeEventArgs { Value = "50" });

        Assert.Equal([new PolarisNumberRange(20m, 50m)], reported);
    }

    [Fact]
    public void A_stale_caller_value_does_not_pull_a_handle_back()
    {
        // Measured in a browser, with the filter's 300ms debounce in front of the query: while the new
        // interval was still in flight the parent re-rendered with the *previous* one, and comparing that
        // against "the value we last reported" treated it as an instruction and reset the control. Every
        // second key press undid the one before it, and a drag had to be released twice. The distinguishing
        // question is not "is this what we reported?" but "is this a new instruction at all?" — the same
        // parameter arriving again is not.
        using var context = new PolarisTestContext();
        PolarisNumberRange? reported = null;

        var slider = context.Render<PolarisRangeSlider>(parameters => parameters
            .Add(p => p.Minimum, 0m)
            .Add(p => p.Maximum, 100m)
            .Add(p => p.Step, 1m)
            .Add(p => p.Value, new PolarisNumberRange(20m, 80m))
            .Add(p => p.ShowInputs, false)
            .Add(p => p.ValueChanged, range => reported = range));

        slider.Find("input[data-polaris-range-handle=upper]")
            .TriggerEvent("onchange", new ChangeEventArgs { Value = "60" });

        Assert.Equal(new PolarisNumberRange(20m, 60m), reported);

        // The parent re-renders before its own state catches up: the same 20-80 it passed in before.
        slider.Render(parameters => parameters
            .Add(p => p.Minimum, 0m)
            .Add(p => p.Maximum, 100m)
            .Add(p => p.Step, 1m)
            .Add(p => p.Value, new PolarisNumberRange(20m, 80m))
            .Add(p => p.ShowInputs, false));

        Assert.Equal("60", slider.Find("input[data-polaris-range-handle=upper]").GetAttribute("value"));

        // A genuinely new value from the caller is still adopted: a cleared filter, a restored saved view.
        slider.Render(parameters => parameters
            .Add(p => p.Minimum, 0m)
            .Add(p => p.Maximum, 100m)
            .Add(p => p.Step, 1m)
            .Add(p => p.Value, new PolarisNumberRange(10m, 40m))
            .Add(p => p.ShowInputs, false));

        Assert.Equal("40", slider.Find("input[data-polaris-range-handle=upper]").GetAttribute("value"));
    }

    [Fact]
    public void The_two_numeric_fields_share_one_row_of_equal_columns()
    {
        // Measured: an `s-stack direction="inline"` around the two fields laid them out one above the
        // other at full width, because a Polaris host computes to `display: contents` and its contents are
        // not the flex items the stack lays out. A grid of real cells cannot be flattened away, so this
        // asserts the layout is stated here rather than inherited from a framework's internals.
        using var context = new PolarisTestContext();

        var slider = context.Render<PolarisRangeSlider>(parameters => parameters
            .Add(p => p.Minimum, 0m)
            .Add(p => p.Maximum, 100m)
            .Add(p => p.Value, new PolarisNumberRange(20m, 80m)));

        var row = slider.Find("[data-polaris-range-inputs]");
        var style = row.GetAttribute("style") ?? string.Empty;

        Assert.Contains("display:grid", style, StringComparison.Ordinal);
        Assert.Contains("grid-template-columns:minmax(0,1fr) minmax(0,1fr)", style, StringComparison.Ordinal);

        var cells = row.QuerySelectorAll("[data-polaris-range-input]");
        Assert.Equal(2, cells.Length);
        Assert.All(cells, cell => Assert.Single(cell.QuerySelectorAll("s-number-field")));
        Assert.Equal(["lower", "upper"], cells.Select(cell => cell.GetAttribute("data-polaris-range-input")));
    }

    [Fact]
    public void The_title_is_on_screen_as_well_as_in_the_group_name()
    {
        // A title that only exists in an aria-label is invisible: the filter panel showed two sliders with
        // no caption at all, which is what "the title isn't displaying" meant. It still names the group,
        // and the visible copy is hidden from assistive technology so the name is not announced twice.
        using var context = new PolarisTestContext();

        var slider = context.Render<PolarisRangeSlider>(parameters => parameters
            .Add(p => p.Minimum, 0m)
            .Add(p => p.Maximum, 100m)
            .Add(p => p.Label, "Price"));

        var group = slider.Find("[data-polaris-range-slider]");
        Assert.Equal("group", group.GetAttribute("role"));
        Assert.Equal("Price", group.GetAttribute("aria-label"));

        var visible = slider.Find("[data-polaris-range-label]");
        Assert.Contains("Price", visible.TextContent, StringComparison.Ordinal);
        Assert.Equal("true", visible.GetAttribute("aria-hidden"));
    }

    [Fact]
    public void A_range_cannot_be_built_the_wrong_way_round()
    {
        // The type normalises its ends, so no caller has to validate what a slider already swaps.
        var range = new PolarisNumberRange(80m, 20m);

        Assert.Equal(20m, range.Lower);
        Assert.Equal(80m, range.Upper);
        Assert.True(range.Contains(20m));
        Assert.True(range.Contains(80m));
        Assert.False(range.Contains(80.1m));
        Assert.Equal(60m, range.Span);
    }

    [Fact]
    public void The_two_typed_fields_show_two_decimal_places()
    {
        // A range filter's domain is the cheapest and dearest row in the data, not a number anybody typed:
        // a lower bound of 4.1666667m is what "the least expensive piece costs 4.17" looks like when it is
        // shown raw, and the Min field then reads like a bug rather than like a limit. Two decimals is the
        // default, and it is stated by this control because a caller cannot know the field exists.
        using var context = new PolarisTestContext();

        var slider = context.Render<PolarisRangeSlider>(parameters => parameters
            .Add(p => p.Minimum, 4.1666667m)
            .Add(p => p.Maximum, 480m)
            .Add(p => p.Step, 0.0001m));

        var fields = slider.FindAll("s-number-field");

        Assert.Equal("4.17", fields[0].GetAttribute("defaultValue"));
        Assert.Equal("480.00", fields[1].GetAttribute("defaultValue"));

        // Not only a first-paint attribute: `value` is not reflected by the element, so the same string is
        // also written to it as a DOM property once it has upgraded.
        Assert.Equal("480.00", context.Interop.SetProperties["s-number-field"][^1]["value"]);
    }

    [Fact]
    public void A_halfway_value_is_shown_rounded_away_from_zero()
    {
        // `decimal.ToString("F2")` rounds to even, so a 4.165 lower bound would read "4.16" — a penny
        // short of the number a merchant would write down, and the exact class of mistake the toolkit
        // pins elsewhere with an explicit MidpointRounding. 4.175 rounds to "4.18" either way, so it is
        // asserted too: a fix that simply rounded every value up would pass the first half and fail here.
        using var context = new PolarisTestContext();

        var slider = context.Render<PolarisRangeSlider>(parameters => parameters
            .Add(p => p.Minimum, 4.165m)
            .Add(p => p.Maximum, 4.175m)
            .Add(p => p.Step, 0.001m));

        var fields = slider.FindAll("s-number-field");

        Assert.Equal("4.17", fields[0].GetAttribute("defaultValue"));
        Assert.Equal("4.18", fields[1].GetAttribute("defaultValue"));
    }

    [Fact]
    public void A_display_format_does_not_leak_into_the_typed_values()
    {
        // PolarisDataFilter hands this control the column's format, which for a price is "C2". That is right
        // for the announced value and wrong for a field's: a currency symbol and a group separator are not
        // a number, and s-number-field parses what it shows. So the two formats stay separate, and the
        // field keeps a plain number while the handle still announces the formatted one.
        using var context = new PolarisTestContext();

        var slider = context.Render<PolarisRangeSlider>(parameters => parameters
            .Add(p => p.Minimum, 0m)
            .Add(p => p.Maximum, 2000m)
            // Off-step on purpose: a step of 1 would round 1234.567 to 1235 before the field saw it, and
            // the assertion would then pass for the wrong reason.
            .Add(p => p.Step, 0.001m)
            .Add(p => p.Value, new PolarisNumberRange(1234.567m, 2000m))
            .Add(p => p.Format, "N2"));

        Assert.Equal("1234.57", slider.FindAll("s-number-field")[0].GetAttribute("defaultValue"));
        Assert.Equal(
            "1,234.57",
            slider.Find("input[data-polaris-range-handle=lower]").GetAttribute("aria-valuetext"));
    }

    [Fact]
    public void The_typed_fields_can_be_given_another_format_or_none()
    {
        // Two decimals is the default, not the law: a count is not money, and a caller that needs the exact
        // value back — a tolerance band in a CAD screen — turns the rounding off entirely.
        using var context = new PolarisTestContext();

        var whole = context.Render<PolarisRangeSlider>(parameters => parameters
            .Add(p => p.Minimum, 0m)
            .Add(p => p.Maximum, 12.6m)
            .Add(p => p.Step, 0.1m)
            .Add(p => p.ShowInputs, true)
            .Add(p => p.InputFormat, "N0"));

        Assert.Equal("13", whole.FindAll("s-number-field")[1].GetAttribute("defaultValue"));

        var raw = context.Render<PolarisRangeSlider>(parameters => parameters
            .Add(p => p.Minimum, 0m)
            .Add(p => p.Maximum, 12.6m)
            .Add(p => p.Step, 0.1m)
            .Add(p => p.ShowInputs, true)
            .Add(p => p.InputFormat, string.Empty));

        Assert.Equal("12.6", raw.FindAll("s-number-field")[1].GetAttribute("defaultValue"));
    }

    private static IRenderedComponent<PolarisRangeSlider> RenderSlider(
        PolarisTestContext context,
        decimal min,
        decimal max,
        decimal? step = null,
        PolarisNumberRange? value = null)
        => context.Render<PolarisRangeSlider>(parameters => parameters
            .Add(p => p.Minimum, min)
            .Add(p => p.Maximum, max)
            .Add(p => p.Step, step ?? 1m)
            .Add(p => p.Value, value)
            .Add(p => p.ShowInputs, false));
}
