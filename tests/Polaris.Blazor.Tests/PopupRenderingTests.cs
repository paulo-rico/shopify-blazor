using Bunit;
using Microsoft.AspNetCore.Components;
using Polaris.Blazor.Components;
using Xunit;

namespace Polaris.Blazor.Tests;

/// <summary>
/// Tests for the composed popup: an anchored panel drawn over the page in the browser's top layer, for
/// content Polaris has no element to show.
/// </summary>
/// <remarks>
/// What a component test can own here is the markup that decides whether the panel is a floating layer
/// or another block in the flow — the <c>popover</c> attribute, the closed state and the calls that show
/// and hide it. Whether it really escapes a section's clipping, and whether the anchor stays put when it
/// opens, is asserted in the browser check; a DOM test cannot see layout.
/// </remarks>
public sealed class PopupRenderingTests
{
    [Fact]
    public void The_panel_is_a_native_popover_which_is_what_puts_it_in_the_top_layer()
    {
        // The attribute is the whole anti-clipping mechanism: an absolutely positioned panel inside a
        // section was measured being cut off at the section's edge, and z-index cannot fix clipping.
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisPopup>(parameters => parameters
            .Add(p => p.Open, true)
            .Add(p => p.Anchor, Builder.Text("Trigger"))
            .Add(p => p.ChildContent, Builder.Text("Panel")));

        var panel = component.Find("[data-polaris-popup]");
        Assert.Equal("manual", panel.GetAttribute("popover"));
        Assert.False(panel.HasAttribute("hidden"));
    }

    [Fact]
    public void The_panel_is_in_the_markup_but_hidden_while_closed_and_shown_when_it_opens()
    {
        // A panel created only on open would make the first show call wait for a render that has not
        // happened yet. The element is always there, hidden by two independent mechanisms.
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisPopup>(parameters => parameters
            .Add(p => p.Open, false)
            .Add(p => p.Anchor, Builder.Text("Trigger"))
            .Add(p => p.ChildContent, Builder.Text("Panel")));

        Assert.True(component.Find("[data-polaris-popup]").HasAttribute("hidden"));
        Assert.Empty(context.Interop.PopupCalls);

        component.Render(parameters => parameters
            .Add(p => p.Open, true)
            .Add(p => p.Anchor, Builder.Text("Trigger"))
            .Add(p => p.ChildContent, Builder.Text("Panel")));

        Assert.False(component.Find("[data-polaris-popup]").HasAttribute("hidden"));
        component.WaitForAssertion(() => Assert.Equal(["show:below"], context.Interop.PopupCalls));
    }

    [Fact]
    public void Closing_asks_the_module_to_hide_the_panel_so_its_listeners_are_released()
    {
        // The module listens for scroll and resize while a panel is up. A close that only re-rendered
        // would leave those listeners holding a detached element for the life of the page.
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisPopup>(parameters => parameters
            .Add(p => p.Open, true)
            .Add(p => p.Anchor, Builder.Text("Trigger"))
            .Add(p => p.ChildContent, Builder.Text("Panel")));

        component.WaitForAssertion(() => Assert.Equal(["show:below"], context.Interop.PopupCalls));

        component.Render(parameters => parameters
            .Add(p => p.Open, false)
            .Add(p => p.Anchor, Builder.Text("Trigger"))
            .Add(p => p.ChildContent, Builder.Text("Panel")));

        component.WaitForAssertion(() => Assert.Equal(["show:below", "hide"], context.Interop.PopupCalls));
    }

    [Fact]
    public void The_panel_opens_below_by_default_and_above_when_asked()
    {
        // Placement is a declared choice, not a guess: the module has the anchor's rect and can honour
        // either side, but it must not decide on its own that a control near the bottom should flip.
        using var context = new PolarisTestContext();

        var below = context.Render<PolarisPopup>(parameters => parameters
            .Add(p => p.Open, true)
            .Add(p => p.ChildContent, Builder.Text("Panel")));
        below.WaitForAssertion(() => Assert.Equal(["show:below"], context.Interop.PopupCalls));

        var above = context.Render<PolarisPopup>(parameters => parameters
            .Add(p => p.Open, true)
            .Add(p => p.Placement, PolarisPopupPlacement.Above)
            .Add(p => p.ChildContent, Builder.Text("Panel")));

        above.WaitForAssertion(() => Assert.Equal(["show:below", "show:above"], context.Interop.PopupCalls));
    }

    [Fact]
    public void The_panel_scrolls_inside_its_cap_instead_of_painting_past_it()
    {
        // Polaris's own popover was measured clipping its content rather than scrolling it, which leaves
        // the lower options painted outside the box and unclickable. The cap is a style here and the
        // module lowers it to the room on the chosen side.
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisPopup>(parameters => parameters
            .Add(p => p.Open, true)
            .Add(p => p.MaxBlockSize, "12rem")
            .Add(p => p.ChildContent, Builder.Text("Panel")));

        var style = component.Find("[data-polaris-popup]").GetAttribute("style");

        Assert.Contains("max-block-size:12rem", style);
        Assert.Contains("overflow-y:auto", style);

        // The module gives the panel the anchor's width. Without border-box that width is the content
        // width, so the panel ends up 10px wider than the field it is supposed to line up with.
        Assert.Contains("box-sizing:border-box", style);
    }

    [Fact]
    public void The_surface_is_painted_with_system_colours_so_it_follows_the_admin_theme()
    {
        // A hard-coded white panel is white in a dark admin. `Canvas` and `CanvasText` are resolved by
        // the browser from the document's own colour scheme, which is the only theme signal a light-DOM
        // element has: Polaris's custom properties live inside its shadow roots and do not inherit out.
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisPopup>(parameters => parameters
            .Add(p => p.Open, true)
            .Add(p => p.ChildContent, Builder.Text("Panel")));

        var style = component.Find("[data-polaris-popup]").GetAttribute("style");

        Assert.Contains("background:Canvas", style);
        Assert.Contains("color:CanvasText", style);
    }

    [Fact]
    public void The_anchor_is_the_positioning_context()
    {
        // The module measures the anchor, not the panel: the wrapper is what the panel is aligned to,
        // and an anchor that is not a box would make every measurement zero.
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisPopup>(parameters => parameters
            .Add(p => p.Open, true)
            .Add(p => p.Anchor, Builder.Text("Trigger")));

        var anchor = component.Find("[data-polaris-popup-anchor]");
        Assert.Contains("position:relative", anchor.GetAttribute("style"));
    }

    [Fact]
    public void A_popup_with_no_content_renders_no_panel_at_all()
    {
        // A caller that only wants the anchor pays for no popover element and no module call.
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisPopup>(parameters => parameters
            .Add(p => p.Open, true)
            .Add(p => p.Anchor, Builder.Text("Trigger")));

        Assert.Empty(component.FindAll("[data-polaris-popup]"));
        Assert.Empty(context.Interop.PopupCalls);
    }

    /// <summary>A minimal fragment, for the parameters that take rendered content.</summary>
    private static class Builder
    {
        public static RenderFragment Text(string text) => builder => builder.AddContent(0, text);
    }
}
