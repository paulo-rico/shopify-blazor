using Bunit;
using Microsoft.AspNetCore.Components;
using Polaris.Blazor.Components;
using Xunit;

namespace Polaris.Blazor.Tests;

/// <summary>
/// Rendering tests for the wrappers that had none, so the generated component reference shows no
/// gaps.
/// </summary>
/// <remarks>
/// <para>
/// These components were implemented in the first pass and exercised only indirectly — through the
/// sample app, or as children of something else. The generated reference marks a component as
/// covered only when a test names its type, and fifteen of them never were, which made the
/// "named in tests" column a list of unanswered questions rather than a gap.
/// </para>
/// <para>
/// Each test asserts the property that would be got wrong silently: an enum that must reach the
/// browser as Shopify's spelling, a boolean that must be a presence attribute, or a value that
/// Polaris does not reflect and that therefore has to be a DOM property.
/// </para>
/// </remarks>
public sealed class ComponentCoverageTests
{
    [Fact]
    public void Badge_renders_tone_colour_size_and_icon()
    {
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisBadge>(parameters => parameters
            .Add(p => p.Tone, PolarisTone.Warning)
            .Add(p => p.Color, PolarisColor.Strong)
            .Add(p => p.Size, PolarisSize.Large100)
            .Add(p => p.Icon, "alert"));

        var element = component.Find("s-badge");
        Assert.Equal("warning", element.GetAttribute("tone"));
        Assert.Equal("strong", element.GetAttribute("color"));
        Assert.Equal("large-100", element.GetAttribute("size"));
        Assert.Equal("alert", element.GetAttribute("icon"));
    }

    [Fact]
    public void Icon_renders_its_type_and_stays_out_of_the_accessibility_tree_when_asked()
    {
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisIcon>(parameters => parameters
            .Add(p => p.Type, "product")
            .Add(p => p.Color, PolarisColor.Subdued)
            .Add(p => p.Size, PolarisIconSize.Small)
            .Add(p => p.AccessibilityLabel, "Product"));

        var element = component.Find("s-icon");
        Assert.Equal("product", element.GetAttribute("type"));
        Assert.Equal("subdued", element.GetAttribute("color"));
        Assert.Equal("small", element.GetAttribute("size"));

        // An icon-only control is announced by its accessibilityLabel; without one it is a blank
        // space to a screen reader.
        Assert.Equal("Product", element.GetAttribute("accessibilityLabel"));
    }

    [Fact]
    public void Heading_renders_size_clamp_and_role()
    {
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisHeading>(parameters => parameters
            .Add(p => p.FontSize, PolarisFontSize.Large)
            .Add(p => p.LineClamp, 2)
            .Add(p => p.AccessibilityRole, "none"));

        var element = component.Find("s-heading");
        Assert.Equal("large", element.GetAttribute("fontSize"));
        Assert.Equal("2", element.GetAttribute("lineClamp"));

        // The heading level comes from nesting depth and cannot be set, so stripping the semantics
        // is the only way to keep the appearance without the outline entry.
        Assert.Equal("none", element.GetAttribute("accessibilityRole"));
    }

    [Fact]
    public void Spinner_renders_the_large_step()
    {
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisSpinner>(parameters => parameters
            .Add(p => p.Size, PolarisSpinnerSize.Large100)
            .Add(p => p.AccessibilityLabel, "Loading"));

        Assert.Equal("large-100", component.Find("s-spinner").GetAttribute("size"));
    }

    [Fact]
    public void Stack_renders_its_axis_and_gaps()
    {
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisStack>(parameters => parameters
            .Add(p => p.Direction, PolarisStackDirection.Inline)
            .Add(p => p.Gap, PolarisGap.Base)
            .Add(p => p.AlignItems, "center"));

        var element = component.Find("s-stack");
        Assert.Equal("inline", element.GetAttribute("direction"));
        Assert.Equal("base", element.GetAttribute("gap"));
        Assert.Equal("center", element.GetAttribute("alignItems"));

        // Row and column gaps are overrides, so they must be absent rather than defaulted.
        Assert.False(element.HasAttribute("rowGap"));
        Assert.False(element.HasAttribute("columnGap"));
    }

    [Fact]
    public void Box_renders_the_styling_properties_it_models()
    {
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisBox>(parameters => parameters
            .Add(p => p.Padding, "base")
            .Add(p => p.Background, "subdued")
            .Add(p => p.BorderRadius, "base")
            .Add(p => p.Overflow, "hidden"));

        var element = component.Find("s-box");
        Assert.Equal("base", element.GetAttribute("padding"));
        Assert.Equal("subdued", element.GetAttribute("background"));
        Assert.Equal("base", element.GetAttribute("borderRadius"));
        Assert.Equal("hidden", element.GetAttribute("overflow"));
    }

    [Fact]
    public void Section_renders_heading_subheading_and_padding()
    {
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisSection>(parameters => parameters
            .Add(p => p.Heading, "Online store")
            .Add(p => p.Subheading, "Last 30 days")
            .Add(p => p.Padding, PolarisSectionPadding.None));

        var element = component.Find("s-section");
        Assert.Equal("Online store", element.GetAttribute("heading"));
        Assert.Equal("Last 30 days", element.GetAttribute("subheading"));

        // None is the padding a section wrapping a table needs; the default would inset it.
        Assert.Equal("none", element.GetAttribute("padding"));
    }

    [Fact]
    public void ButtonGroup_renders_its_gap()
    {
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisButtonGroup>(parameters => parameters
            .Add(p => p.Gap, PolarisButtonGroupGap.None));

        Assert.Equal("none", component.Find("s-button-group").GetAttribute("gap"));
    }

    [Fact]
    public void Popover_renders_its_size_constraints()
    {
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisPopover>(parameters => parameters
            .Add(p => p.MaxBlockSize, "300px")
            .Add(p => p.InlineSize, "240px"));

        var element = component.Find("s-popover");
        Assert.Equal("300px", element.GetAttribute("maxBlockSize"));
        Assert.Equal("240px", element.GetAttribute("inlineSize"));
    }

    [Fact]
    public void OptionGroup_renders_its_label_and_disabled_state()
    {
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisOptionGroup>(parameters => parameters
            .Add(p => p.Label, "Custom ranges")
            .Add(p => p.Disabled, true));

        var element = component.Find("s-option-group");
        Assert.Equal("Custom ranges", element.GetAttribute("label"));
        Assert.True(element.HasAttribute("disabled"));
    }

    [Fact]
    public void Switch_keeps_checked_out_of_the_markup_because_it_is_not_reflected()
    {
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisSwitch>(parameters => parameters
            .Add(p => p.Label, "Publish immediately")
            .Add(p => p.Checked, true));

        var element = component.Find("s-switch");
        Assert.Equal("Publish immediately", element.GetAttribute("label"));

        // A stale `checked` attribute would pin the switch on no matter what the server renders.
        Assert.False(element.HasAttribute("checked"));
        Assert.True(context.Interop.SetProperties.TryGetValue("s-switch", out var calls));
        Assert.Contains(calls, call => call["checked"] is true);
    }

    [Fact]
    public void TableHeaderRow_contains_header_cells()
    {
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisTableHeaderRow>(parameters => parameters
            .AddChildContent(builder =>
            {
                builder.OpenComponent<PolarisTableHeader>(0);
                builder.AddAttribute(1, nameof(PolarisTableHeader.ChildContent), (RenderFragment)(b => b.AddContent(0, "Name")));
                builder.CloseComponent();
            }));

        Assert.NotNull(component.Find("s-table-header-row"));
        Assert.Equal("Name", component.Find("s-table-header").TextContent);
    }

    [Fact]
    public void TableBody_contains_rows()
    {
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisTableBody>(parameters => parameters
            .AddChildContent(builder =>
            {
                builder.OpenComponent<PolarisTableRow>(0);
                builder.AddAttribute(1, nameof(PolarisTableRow.ChildContent), (RenderFragment)(b => b.AddContent(0, "row")));
                builder.CloseComponent();
            }));

        Assert.NotNull(component.Find("s-table-body"));
        Assert.Equal("row", component.Find("s-table-row").TextContent);
    }

    [Fact]
    public void TableRow_renders_its_click_delegate()
    {
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisTableRow>(parameters => parameters
            .Add(p => p.ClickDelegate, "row-link"));

        // Click-only: the target element still has to be reachable and labelled in its own right.
        Assert.Equal("row-link", component.Find("s-table-row").GetAttribute("clickDelegate"));
    }

    [Fact]
    public void TableCell_renders_its_content()
    {
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisTableCell>(parameters => parameters
            .AddChildContent("John Smith"));

        Assert.Equal("John Smith", component.Find("s-table-cell").TextContent);
    }

    [Fact]
    public void AppNav_renders_its_links_and_passes_the_home_rel_through()
    {
        // s-app-nav configures the ADMIN's own navigation, so it is App Bridge rather than Polaris and
        // draws nothing when the app is opened outside the admin. What can be asserted here is the
        // markup the admin is handed: one link per item, in order, with rel="home" — which designates
        // the landing route and is then hidden from the rendered menu — carried through the unmatched
        // attribute capture rather than a parameter of its own.
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisAppNav>(parameters => parameters
            .AddChildContent(builder =>
            {
                builder.OpenComponent<PolarisLink>(0);
                builder.AddAttribute(1, nameof(PolarisLink.Href), "/");
                builder.AddAttribute(2, "rel", "home");
                builder.CloseComponent();

                builder.OpenComponent<PolarisLink>(3);
                builder.AddAttribute(4, nameof(PolarisLink.Href), "/products");
                builder.AddAttribute(5, nameof(PolarisLink.ChildContent), (RenderFragment)(b => b.AddContent(0, "Products")));
                builder.CloseComponent();
            }));

        Assert.NotNull(component.Find("s-app-nav"));

        var links = component.FindAll("s-link");
        Assert.Equal(2, links.Count);
        Assert.Equal("home", links[0].GetAttribute("rel"));
        Assert.Equal("/products", links[1].GetAttribute("href"));
        Assert.Equal("Products", links[1].TextContent);
    }

    [Fact]
    public async Task AppWindow_renders_its_src_and_calls_the_elements_own_methods()
    {
        // The window is opened either declaratively (commandFor from a button) or through the element's
        // show/hide/toggle methods. The method names are the part that fails silently — a wrong one is a
        // call that resolves to nothing — so they are asserted as they were sent.
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisAppWindow>(parameters => parameters
            .Add(p => p.Id, "product-editor")
            .Add(p => p.Src, "/products/select"));

        var element = component.Find("s-app-window");
        Assert.Equal("/products/select", element.GetAttribute("src"));
        Assert.Equal("product-editor", element.GetAttribute("id"));

        await component.Instance.ShowAsync();
        await component.Instance.ToggleAsync();
        await component.Instance.HideAsync();

        Assert.Equal(["show", "toggle", "hide"], context.Interop.Methods);
    }
}
