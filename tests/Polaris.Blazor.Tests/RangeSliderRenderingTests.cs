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
        // The whole point of a dual control: moving one end must not move the other.
        using var context = new PolarisTestContext();
        PolarisNumberRange? reported = null;

        var slider = context.Render<PolarisRangeSlider>(parameters => parameters
            .Add(p => p.Minimum, 0m)
            .Add(p => p.Maximum, 100m)
            .Add(p => p.Step, 1m)
            .Add(p => p.Value, new PolarisNumberRange(20m, 80m))
            .Add(p => p.ValueChanged, range => reported = range));

        slider.Find("input[data-polaris-range-handle=lower]")
            .TriggerEvent("oninput", new ChangeEventArgs { Value = "35" });

        Assert.Equal(new PolarisNumberRange(35m, 80m), reported);

        slider.Find("input[data-polaris-range-handle=upper]")
            .TriggerEvent("oninput", new ChangeEventArgs { Value = "60" });

        Assert.Equal(new PolarisNumberRange(35m, 60m), reported);
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
            .TriggerEvent("oninput", new ChangeEventArgs { Value = "95" });

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
            Assert.Equal("92.5", slider.FindAll("s-number-field")[1].GetAttribute("defaultValue"));

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
