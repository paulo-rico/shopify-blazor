using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Polaris.Blazor.Components;
using Xunit;

namespace Polaris.Blazor.Tests;

/// <summary>
/// Tests for the accordion — the library's only composed component, and therefore the only one whose
/// behaviour is not entirely Shopify's.
/// </summary>
/// <remarks>
/// <para>
/// The disclosure is a native <c>&lt;details&gt;</c>/<c>&lt;summary&gt;</c> now, so everything the
/// browser owns is deliberately not asserted here: the keyboard, focus, whether a closed panel is
/// rendered, and the announced expanded state all come from the element. What is asserted is what this
/// component still has to get right — the <c>open</c> attribute is present or absent and never
/// <c>"false"</c>; one section collapses another in the same render; the header's accessible name is
/// its visible text and not the action's label; and a click on an action does not toggle the section it
/// acts on.
/// </para>
/// <para>
/// The last of those is also driven in a real browser, because whether a click reaches the summary is a
/// DOM question this file cannot answer on its own: bUnit fires the event at the element it is given,
/// while Chrome decides whether the disclosure activates. The attributes that stop the click are
/// asserted here; the click itself is asserted in <c>tools/verify-in-browser.mjs</c>.
/// </para>
/// </remarks>
public sealed class AccordionRenderingTests
{
    [Fact]
    public void A_closed_section_renders_a_details_without_the_open_attribute()
    {
        // `open` is a boolean attribute: present means open, whatever its value says. A component that
        // wrote open="false" would draw an open panel while believing it closed, the registry would
        // disagree with the screen, and nothing would report it. Asserting the attribute's absence —
        // not just a value — is what catches that.
        using var context = new PolarisTestContext();

        var component = RenderAccordion(context, ("Details", "details", false, null));

        var details = component.Find("details");
        Assert.False(details.HasAttribute("open"));
        Assert.Null(details.GetAttribute("open"));
    }

    [Fact]
    public void An_initially_open_section_renders_the_open_attribute()
    {
        // The panel is not clipped, sized or marked inert by this component any more: the attribute is
        // the whole state, and the browser does the rest.
        using var context = new PolarisTestContext();

        var component = RenderAccordion(context, ("Details", "details", initiallyOpen: true, null));

        Assert.True(component.Find("details").HasAttribute("open"));
    }

    [Fact]
    public async Task Clicking_a_header_opens_its_section_and_collapses_the_open_one()
    {
        // This is the whole point of an accordion: both sections must reach their new state in the same
        // render, so the merchant does not watch one close and only then the other open.
        using var context = new PolarisTestContext();

        var component = RenderAccordion(
            context,
            ("Details", "details", initiallyOpen: true, null),
            ("Shipping", "shipping", false, null));

        Assert.True(Details(component, 0).HasAttribute("open"));

        await ClickHeaderAsync(component, index: 1);

        Assert.False(Details(component, 0).HasAttribute("open"));
        Assert.True(Details(component, 1).HasAttribute("open"));
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

        await ClickHeaderAsync(component, index: 0);

        Assert.False(Details(component, 0).HasAttribute("open"));
        Assert.Null(reported);
    }

    [Fact]
    public void Only_the_first_initially_open_section_keeps_the_open_slot()
    {
        // Two open sections in an accordion that allows one is a state the next click would silently
        // correct. Registration order is DOM order, so "the first one wins" is deterministic.
        using var context = new PolarisTestContext();

        var component = RenderAccordion(
            context,
            ("Details", "details", initiallyOpen: true, null),
            ("Shipping", "shipping", initiallyOpen: true, null));

        Assert.True(Details(component, 0).HasAttribute("open"));
        Assert.False(Details(component, 1).HasAttribute("open"));
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

        await ClickHeaderAsync(component, index: 1);

        Assert.True(Details(component, 0).HasAttribute("open"));
        Assert.True(Details(component, 1).HasAttribute("open"));
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

        await ClickHeaderAsync(component, index: 0);

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

        Assert.False(component.Find("details").HasAttribute("open"));

        await ClickHeaderAsync(component, index: 0);

        Assert.True(component.Find("details").HasAttribute("open"));
    }

    [Fact]
    public void The_header_is_a_summary_holding_light_dom_text()
    {
        // The heading used to be an s-section heading *attribute*, which Polaris draws inside its shadow
        // root — so the surface had no text of its own and its name had to be restated. It is ordinary
        // content in the summary now, which is what lets aria-labelledby point at what is drawn.
        using var context = new PolarisTestContext();

        var component = RenderAccordion(context, ("Details", "details", false, null));

        var summary = component.Find("summary");
        var heading = summary.QuerySelector("s-heading")!;

        Assert.NotNull(heading);
        Assert.Equal("Details", heading.TextContent.Trim());
        Assert.Null(summary.QuerySelector("s-clickable"));
        Assert.Null(summary.QuerySelector("s-section"));
    }

    [Fact]
    public void The_header_is_named_by_its_visible_text()
    {
        // A <summary>'s name is everything under it — measured in Chrome, adding an action to the header
        // made the disclosure read "Variants … Clear all reviews", announcing the button as part of the
        // title. aria-labelledby narrows the name to the three text elements, and because it references
        // them rather than restating them, the name cannot drift from what is on screen.
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisAccordionItem>(parameters => parameters
            .Add(p => p.Header, "Alloy weight by ring size")
            .Add(p => p.HeaderDetails, "Weigh one variant.")
            .Add(p => p.HeaderStatus, "Alloy weights on every variant")
            .AddChildContent("Content"));

        var ids = component.Find("summary").GetAttribute("aria-labelledby")!.Split(' ');

        Assert.Equal(3, ids.Length);
        Assert.Equal("Alloy weight by ring size", component.Find($"#{ids[0]}").TextContent.Trim());
        Assert.Equal("Weigh one variant.", component.Find($"#{ids[1]}").TextContent.Trim());
        Assert.Equal("Alloy weights on every variant", component.Find($"#{ids[2]}").TextContent.Trim());
    }

    [Fact]
    public void A_blank_header_status_draws_no_badge_and_no_name_segment()
    {
        // A page passes an expression that is blank most of the time. An empty badge would be a small
        // grey pill on every section with nothing to say, and an empty referenced element would make
        // the name end on a gap.
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisAccordionItem>(parameters => parameters
            .Add(p => p.Header, "Standalone")
            .Add(p => p.HeaderStatus, "   ")
            .AddChildContent("Content"));

        Assert.Empty(component.FindAll("s-badge"));
        Assert.Single(component.Find("summary").GetAttribute("aria-labelledby")!.Split(' '));
    }

    [Fact]
    public void Header_status_renders_a_badge_beside_the_heading()
    {
        // The badge is what a collapsed section can say about itself before it is opened — that the work
        // it offers has already been done — so it belongs in the header and not in the panel, on the
        // heading's own line: it qualifies the title rather than competing with it from the far edge.
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisAccordionItem>(parameters => parameters
            .Add(p => p.Header, "Alloy weight by ring size")
            .Add(p => p.HeaderDetails, "Weigh one variant.")
            .Add(p => p.HeaderStatus, "Alloy weights on 2 of 6 variants")
            .Add(p => p.HeaderStatusTone, PolarisTone.Critical)
            .AddChildContent("Content"));

        var badge = component.Find("summary").QuerySelector("s-badge")!;

        Assert.NotNull(badge);
        Assert.Equal("critical", badge.GetAttribute("tone"));
        Assert.Equal("Alloy weights on 2 of 6 variants", badge.TextContent.Trim());

        // On the heading's line, not out at the edge: the heading and the badge share one row.
        var row = badge.ParentElement!;
        Assert.Equal("s-heading", row.Children[0].LocalName);
        Assert.Equal("s-badge", row.Children[1].LocalName);

        // The badge never leaves the header: a state read after opening the section is read too late.
        Assert.Empty(component.Find("s-box").QuerySelectorAll("s-badge"));
    }

    [Fact]
    public void Header_media_stands_beside_the_heading_and_not_above_it()
    {
        // A picture on the header is worth having only if it is to the LEFT of the text: a row that
        // wrapped would push the heading down and every header would grow by the image's height. The
        // pair is one child of the outer row, so the chevron still sits at the far edge — three
        // children under space-between would centre the heading between the picture and the chevron,
        // which reads as an image with a caption rather than as a list of variants.
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisAccordionItem>(parameters => parameters
            .Add(p => p.Header, "Q / 9ct Yellow Gold")
            .Add(p => p.HeaderDetails, "£312.35")
            .Add(p => p.HeaderMedia, (RenderFragment)(media =>
                media.AddMarkupContent(0, "<s-thumbnail src=\"https://cdn.example/ring.png\" alt=\"\"></s-thumbnail>")))
            .AddChildContent("Content"));

        var summary = component.Find("summary");
        var pair = summary.QuerySelector("s-stack")!;

        Assert.NotNull(pair);
        Assert.Equal("inline", pair.GetAttribute("direction"));

        // Centred against the pair, so a 100px thumbnail stands beside both lines rather than level
        // with the heading alone.
        Assert.Equal("center", pair.GetAttribute("alignItems"));

        // The medium is first and the heading second: to the left, not above.
        Assert.Equal("s-thumbnail", pair.Children[0].LocalName);
        Assert.NotNull(pair.QuerySelector("s-heading"));

        // The chevron stays outside the pair, so it remains the last thing in the header.
        Assert.NotNull(summary.QuerySelector("s-icon"));
        Assert.Null(pair.QuerySelector("s-icon"));
    }

    [Fact]
    public void A_section_without_header_media_renders_no_thumbnail()
    {
        // The media pair is only drawn for an item that asked for one: an empty pair would add a gap
        // and a nested stack to every header in every app that never passes it.
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisAccordionItem>(parameters => parameters
            .Add(p => p.Header, "Standalone")
            .AddChildContent("Content"));

        Assert.Empty(component.FindAll("s-thumbnail"));
        Assert.NotNull(component.Find("summary").QuerySelector("s-heading"));
    }

    [Fact]
    public void A_section_renders_a_divider_a_summary_and_a_chevron_on_the_right()
    {
        // The rules that structure the list are unchanged: a divider above each section, clipped by one
        // rounded box owned by the container, so the sections read as a list rather than a stack of
        // cards. What changed underneath is that the header is now a summary.
        using var context = new PolarisTestContext();

        var component = RenderAccordion(
            context,
            ("Details", "details", false, null),
            ("Shipping", "shipping", false, "Sent by courier"));

        Assert.Equal(2, component.FindAll("s-divider").Count);

        var container = component.Find("s-box");
        Assert.Equal("base", container.GetAttribute("border"));
        Assert.Equal("base", container.GetAttribute("borderRadius"));
        Assert.Equal("hidden", container.GetAttribute("overflow"));

        // The header is a summary inside a wrapper that also holds the divider, because a <details> is
        // "an optional summary followed by flow content" — the rule cannot sit inside it.
        var item = component.FindAll("[data-polaris-accordion-item]")[0];
        Assert.Equal("s-divider", item.Children[0].LocalName);
        Assert.Equal("details", item.Children[1].LocalName);

        // The summary is the row: the two groups are its children, and the chevron is last in the
        // second one. A stack wrapped around both of them would be a single shrink-wrapped flex item —
        // a Polaris host computes to display: contents — which measured as a chevron 300px short of
        // the header's edge and an action sitting over the header's centre.
        var summary = item.QuerySelector("summary")!;
        Assert.Equal(2, summary.Children.Length);

        var end = summary.Children[1];
        var chevron = end.QuerySelector("s-icon")!;
        Assert.Equal("chevron-down", chevron.GetAttribute("type"));
        Assert.True(chevron.HasAttribute("data-polaris-accordion-chevron"));

        // The supporting line is body text under the heading, in the same labelled name.
        Assert.Equal("Sent by courier", component.FindAll("summary")[1].QuerySelector("s-text")!.TextContent.Trim());
    }

    [Fact]
    public async Task An_action_in_the_header_is_wrapped_so_it_cannot_toggle_the_section()
    {
        // The capability the s-clickable header could not offer. A button inside that surface was a
        // button within a button, and the button role makes its children presentational, so the action
        // left the accessibility tree. In a summary it is an ordinary button, measured in Chrome — and
        // the wrapper is what stops the same click from also opening the section.
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisAccordionItem>(parameters => parameters
            .Add(p => p.Header, "Variants")
            .Add(p => p.HeaderActions, (RenderFragment)(actions =>
                actions.AddMarkupContent(0, "<button type=\"button\">Approve all changes</button>")))
            .AddChildContent("Content"));

        var summary = component.Find("summary");
        var wrapper = component.Find("[data-polaris-accordion-actions]");

        Assert.NotNull(summary.QuerySelector("button"));

        // Both markers, not just the handler: stopPropagation keeps the component's own header click
        // from running, and preventDefault is what stops a browser that does not skip the disclosure
        // activation for interactive content from toggling a panel this component would never hear
        // about. bUnit spells Blazor's internal markers "blazor:<event>:<marker>"; the real click is
        // driven in tools/verify-in-browser.mjs, where the browser decides whether the panel toggles.
        Assert.True(wrapper.HasAttribute("blazor:onclick:stoppropagation"));
        Assert.True(wrapper.HasAttribute("blazor:onclick:preventdefault"));

        await component.InvokeAsync(() => component.Find("button").Click());

        Assert.False(component.Find("details").HasAttribute("open"));
    }

    [Fact]
    public void The_accordion_no_longer_writes_aria_expanded_into_a_shadow_root()
    {
        // The old header was an s-clickable, whose state had to be written onto the shadow <button> by
        // interop because the element has no property for it. The summary announces its own state, so
        // the call must be gone: leaving it would write to a button that no longer exists, and the
        // regression would be invisible apart from a state that never updates.
        using var context = new PolarisTestContext();

        RenderAccordion(context, ("Details", "details", false, null));

        Assert.Empty(context.Interop.AriaExpanded);
    }

    private static IRenderedComponent<PolarisAccordion> RenderAccordion(
        PolarisTestContext context,
        params (string header, string value, bool initiallyOpen, string? headerDetails)[] sections)
        => context.Render<PolarisAccordion>(parameters => parameters.AddChildContent(builder =>
        {
            var sequence = 0;

            foreach (var (header, value, initiallyOpen, headerDetails) in sections)
            {
                AddItem(builder, sequence, header, value, initiallyOpen, headerDetails);
                sequence += 10;
            }
        }));

    private static void AddItem(
        RenderTreeBuilder builder,
        int sequence,
        string header,
        string value,
        bool initiallyOpen = false,
        string? headerDetails = null)
    {
        // A helper cannot use literal sequence numbers, so the analyzer's literal rule is suppressed
        // here rather than repeating this block at every call site.
#pragma warning disable ASP0006
        builder.OpenComponent<PolarisAccordionItem>(sequence);
        builder.AddAttribute(sequence + 1, nameof(PolarisAccordionItem.Header), header);
        builder.AddAttribute(sequence + 2, nameof(PolarisAccordionItem.Value), value);
        builder.AddAttribute(sequence + 3, nameof(PolarisAccordionItem.InitiallyOpen), initiallyOpen);
        builder.AddAttribute(sequence + 4, nameof(PolarisAccordionItem.HeaderDetails), headerDetails);
        builder.AddAttribute(sequence + 5, nameof(PolarisAccordionItem.ChildContent),
            (RenderFragment)(content => content.AddContent(0, $"{header} content")));
        builder.CloseComponent();
#pragma warning restore ASP0006
    }

    private static AngleSharp.Dom.IElement Details(IRenderedComponent<PolarisAccordion> component, int index)
        => component.FindAll("details")[index];

    private static Task ClickHeaderAsync(
        IRenderedComponent<PolarisAccordion> component,
        int index)
        => ClickHeaderAsync<PolarisAccordion>(component, index);

    private static Task ClickHeaderAsync<TComponent>(
        IRenderedComponent<TComponent> component,
        int index)
        where TComponent : IComponent
        => component.InvokeAsync(() => component
            .FindAll("[data-polaris-accordion-header]")[index]
            .TriggerEventAsync("onclick", new MouseEventArgs()));
}
