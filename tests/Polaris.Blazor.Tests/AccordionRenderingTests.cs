using Bunit;
using Microsoft.AspNetCore.Components;
using Polaris.Blazor.Components;
using Xunit;

namespace Polaris.Blazor.Tests;

/// <summary>
/// Tests for the accordion — the library's only composed component, and therefore the only one whose
/// behaviour is not entirely Shopify's.
/// </summary>
/// <remarks>
/// <para>
/// What is asserted here is the part that can be got wrong without looking wrong: which panel is
/// clipped, whether clipped content is still reachable, whether Polaris is told the expanded state
/// through a DOM property rather than an attribute, and whether opening one section really collapses
/// the other in the same render.
/// </para>
/// <para>
/// Clicks arrive through the interop module, not through a Blazor <c>@onclick</c> handler, so they
/// are raised through the recording double. What that cannot prove is the animation itself — a test
/// sees the state change, not the frames — which is why the browser check exercises it.
/// </para>
/// </remarks>
public sealed class AccordionRenderingTests
{
    [Fact]
    public void A_closed_section_renders_a_collapsed_panel_and_hides_its_content_from_keyboards()
    {
        // Clipping alone would hide the content visually while leaving every field in it reachable
        // with Tab and readable by a screen reader, which is the classic broken disclosure. The panel
        // is collapsed with grid rows and the content is inert.
        using var context = new PolarisTestContext();

        var component = RenderAccordion(context, ("Details", "details", false));

        var panel = component.Find("[data-polaris-accordion-panel]");
        Assert.Contains("grid-template-rows:0fr", panel.GetAttribute("style"));

        var inner = panel.QuerySelector("[data-polaris-accordion-panel-inner]");
        Assert.NotNull(inner);
        Assert.True(inner!.HasAttribute("inert"));
    }

    [Fact]
    public void An_initially_open_section_renders_an_expanded_panel()
    {
        // 1fr is the open end state: the row resolves to the content's height, so nothing is
        // hard-coded and content of any size fits.
        using var context = new PolarisTestContext();

        var component = RenderAccordion(context, ("Details", "details", initiallyOpen: true));

        var panel = component.Find("[data-polaris-accordion-panel]");
        Assert.Contains("grid-template-rows:1fr", panel.GetAttribute("style"));
        Assert.False(panel.QuerySelector("[data-polaris-accordion-panel-inner]")!.HasAttribute("inert"));

        Assert.Equal("true", component.Find("[data-polaris-accordion-item]").GetAttribute("data-open"));
    }

    [Fact]
    public async Task Clicking_a_header_opens_its_section_and_collapses_the_open_one()
    {
        // This is the whole point of an accordion, and it is the interaction a browser would hide:
        // both sections must reach their new state in the same render, so the opening and closing
        // transitions run together instead of one after the other.
        using var context = new PolarisTestContext();

        var component = RenderAccordion(
            context,
            ("Details", "details", initiallyOpen: true),
            ("Shipping", "shipping", false));

        Assert.Equal("true", Item(component, 0).GetAttribute("data-open"));

        await ClickHeaderAsync(component, context, index: 1);

        Assert.Equal("false", Item(component, 0).GetAttribute("data-open"));
        Assert.Equal("true", Item(component, 1).GetAttribute("data-open"));

        Assert.Contains("grid-template-rows:0fr", Panel(component, 0).GetAttribute("style"));
        Assert.Contains("grid-template-rows:1fr", Panel(component, 1).GetAttribute("style"));
    }

    [Fact]
    public async Task Clicking_an_open_header_closes_it_again()
    {
        // A disclosure that cannot be closed is a one-way control; the toggle has to work in both
        // directions, and closing the open section leaves the accordion fully collapsed.
        using var context = new PolarisTestContext();
        string? reported = "not reported";

        var component = context.Render<PolarisAccordion>(parameters => parameters
            .Add(p => p.OpenItemChanged, value => reported = value)
            .AddChildContent(builder => AddItem(builder, 0, "Details", "details", initiallyOpen: true)));

        await ClickHeaderAsync(component, context, index: 0);

        Assert.Equal("false", Item(component, 0).GetAttribute("data-open"));
        Assert.Null(reported);
    }

    [Fact]
    public void The_expanded_state_is_a_dom_property_because_polaris_does_not_reflect_it()
    {
        // aria-expanded has to reach the <button> Polaris renders inside its shadow root, and an
        // attribute on the <s-button> host lands on an element with no role. A stale attribute would
        // also pin the announced state at first paint. The value is Polaris's own spelling rather
        // than a boolean: a bool is read as collapsed, which a browser check caught the hard way.
        using var context = new PolarisTestContext();

        RenderAccordion(context, ("Details", "details", false));

        Assert.True(context.Interop.SetProperties.TryGetValue("s-button", out var calls));
        Assert.Equal("collapsed", calls[0]["accessibilityExpanded"]);

        var button = context.Render<PolarisButton>(parameters => parameters
            .Add(p => p.AccessibilityExpanded, PolarisAccessibilityExpanded.Expanded)
            .AddChildContent("Toggle"));

        Assert.False(button.Find("s-button").HasAttribute("accessibilityExpanded"));
        Assert.Equal("expanded", context.Interop.SetProperties["s-button"][^1]["accessibilityExpanded"]);
    }

    [Fact]
    public void Only_the_first_initially_open_section_keeps_the_open_slot()
    {
        // Two open sections in an accordion that allows one is a state the next click would silently
        // correct. Registration order is DOM order, so "the first one wins" is deterministic.
        using var context = new PolarisTestContext();

        var component = RenderAccordion(
            context,
            ("Details", "details", initiallyOpen: true),
            ("Shipping", "shipping", initiallyOpen: true));

        Assert.Equal("true", Item(component, 0).GetAttribute("data-open"));
        Assert.Equal("false", Item(component, 1).GetAttribute("data-open"));
    }

    [Fact]
    public async Task AllowMultiple_lets_every_section_stay_open()
    {
        // The escape hatch for the same component: sections that are not mutually exclusive.
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisAccordion>(parameters => parameters
            .Add(p => p.AllowMultiple, true)
            .AddChildContent(builder =>
            {
                AddItem(builder, 0, "Details", "details", initiallyOpen: true);
                AddItem(builder, 100, "Shipping", "shipping");
            }));

        await ClickHeaderAsync(component, context, index: 1);

        Assert.Equal("true", Item(component, 0).GetAttribute("data-open"));
        Assert.Equal("true", Item(component, 1).GetAttribute("data-open"));
    }

    [Fact]
    public async Task OpenItemChanged_reports_the_value_of_the_section_that_is_now_open()
    {
        // The container cannot see into the items, so the value is the only handle an outside handler
        // has on "which section is showing".
        using var context = new PolarisTestContext();
        string? reported = null;

        var component = context.Render<PolarisAccordion>(parameters => parameters
            .Add(p => p.OpenItemChanged, value => reported = value)
            .AddChildContent(builder => AddItem(builder, 0, "Details", "details")));

        await ClickHeaderAsync(component, context, index: 0);

        Assert.Equal("details", reported);
    }

    [Fact]
    public async Task A_section_outside_an_accordion_still_toggles_itself()
    {
        // An item rendered on its own has no container to ask, so it must not throw or freeze; it
        // simply cannot enforce exclusivity against a sibling it does not know about.
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisAccordionItem>(parameters => parameters
            .Add(p => p.Header, "Standalone")
            .AddChildContent("Content"));

        Assert.Equal("false", component.Find("[data-polaris-accordion-item]").GetAttribute("data-open"));

        await ClickHeaderAsync(component, context, index: 0);

        Assert.Equal("true", component.Find("[data-polaris-accordion-item]").GetAttribute("data-open"));
    }

    [Fact]
    public void A_section_renders_a_divider_a_header_button_and_a_chevron()
    {
        // The three existing Polaris components the accordion is built from. The chevron is the only
        // sighted affordance for the state, and it is decorative: the button's aria-expanded is what
        // announces it.
        using var context = new PolarisTestContext();

        var component = RenderAccordion(context, ("Details", "details", false));

        Assert.Single(component.FindAll("s-divider"));
        Assert.Equal("tertiary", component.Find("s-button").GetAttribute("variant"));
        Assert.Equal("fill", component.Find("s-button").GetAttribute("inlineSize"));
        Assert.Equal("Details", component.Find("s-button").TextContent.Trim());

        var chevron = component.Find("s-icon");
        Assert.Equal("chevron-down", chevron.GetAttribute("type"));
        Assert.True(chevron.HasAttribute("data-polaris-accordion-chevron"));
    }

    private static IRenderedComponent<PolarisAccordion> RenderAccordion(
        PolarisTestContext context,
        params (string header, string value, bool initiallyOpen)[] sections)
        => context.Render<PolarisAccordion>(parameters => parameters.AddChildContent(builder =>
        {
            var sequence = 0;

            foreach (var (header, value, initiallyOpen) in sections)
            {
                AddItem(builder, sequence, header, value, initiallyOpen);
                sequence += 10;
            }
        }));

    private static void AddItem(
        Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder,
        int sequence,
        string header,
        string value,
        bool initiallyOpen = false)
    {
        // A helper cannot use literal sequence numbers, so the analyzer's literal rule is suppressed
        // here rather than repeating this block at every call site.
#pragma warning disable ASP0006
        builder.OpenComponent<PolarisAccordionItem>(sequence);
        builder.AddAttribute(sequence + 1, nameof(PolarisAccordionItem.Header), header);
        builder.AddAttribute(sequence + 2, nameof(PolarisAccordionItem.Value), value);
        builder.AddAttribute(sequence + 3, nameof(PolarisAccordionItem.InitiallyOpen), initiallyOpen);
        builder.AddAttribute(sequence + 4, nameof(PolarisAccordionItem.ChildContent),
            (RenderFragment)(content => content.AddContent(0, $"{header} content")));
        builder.CloseComponent();
#pragma warning restore ASP0006
    }

    private static AngleSharp.Dom.IElement Item(IRenderedComponent<PolarisAccordion> component, int index)
        => component.FindAll("[data-polaris-accordion-item]")[index];

    private static AngleSharp.Dom.IElement Panel(IRenderedComponent<PolarisAccordion> component, int index)
        => component.FindAll("[data-polaris-accordion-panel]")[index];

    private static Task ClickHeaderAsync(
        IRenderedComponent<PolarisAccordion> component,
        PolarisTestContext context,
        int index)
        => ClickHeaderAsync<PolarisAccordion>(component, context, index);

    private static Task ClickHeaderAsync<TComponent>(
        IRenderedComponent<TComponent> component,
        PolarisTestContext context,
        int index)
        where TComponent : Microsoft.AspNetCore.Components.IComponent
        => component.InvokeAsync(() => context.Interop.RaiseAsync("s-button", index));
}
