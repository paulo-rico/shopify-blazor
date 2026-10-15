using Bunit;
using Microsoft.AspNetCore.Components;
using Polaris.Blazor.Components;
using Xunit;

namespace Polaris.Blazor.Tests;

/// <summary>
/// Rendering tests for the layout wrappers: grid, the lists, and the query container.
/// </summary>
public sealed class LayoutComponentRenderingTests
{
    [Fact]
    public void Grid_renders_its_tracks_and_gap()
    {
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisGrid>(parameters => parameters
            .Add(p => p.GridTemplateColumns, "repeat(2, 1fr)")
            .Add(p => p.Gap, "small")
            .Add(p => p.JustifyContent, "center"));

        var element = component.Find("s-grid");
        Assert.Equal("repeat(2, 1fr)", element.GetAttribute("gridTemplateColumns"));
        Assert.Equal("small", element.GetAttribute("gap"));
        Assert.Equal("center", element.GetAttribute("justifyContent"));
    }

    [Fact]
    public void Grid_renders_padding_and_border_as_strings_because_they_take_shorthands()
    {
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisGrid>(parameters => parameters
            .Add(p => p.Padding, "base large-100 base small")
            .Add(p => p.Border, "base")
            .Add(p => p.BorderStyle, "dashed")
            .Add(p => p.PaddingInlineStart, "small"));

        var element = component.Find("s-grid");

        // A 1-to-4-value shorthand and a responsive @container value are both legal here, which is
        // why these are strings rather than an enum.
        Assert.Equal("base large-100 base small", element.GetAttribute("padding"));
        Assert.Equal("base", element.GetAttribute("border"));
        Assert.Equal("dashed", element.GetAttribute("borderStyle"));
        Assert.Equal("small", element.GetAttribute("paddingInlineStart"));
    }

    [Fact]
    public void GridItem_renders_its_spans()
    {
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisGridItem>(parameters => parameters
            .Add(p => p.GridColumn, "span 2")
            .Add(p => p.GridRow, "span 1"));

        var element = component.Find("s-grid-item");
        Assert.Equal("span 2", element.GetAttribute("gridColumn"));
        Assert.Equal("span 1", element.GetAttribute("gridRow"));
    }

    [Fact]
    public void UnorderedList_contains_list_items()
    {
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisUnorderedList>(parameters => parameters
            .AddChildContent(builder =>
            {
                builder.OpenComponent<PolarisListItem>(0);
                builder.AddAttribute(1, nameof(PolarisListItem.ChildContent), (RenderFragment)(b => b.AddContent(0, "Add products")));
                builder.CloseComponent();
            }));

        Assert.NotNull(component.Find("s-unordered-list"));
        Assert.Equal("Add products", component.Find("s-list-item").TextContent);
    }

    [Fact]
    public void OrderedList_contains_list_items()
    {
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisOrderedList>(parameters => parameters
            .AddChildContent(builder =>
            {
                builder.OpenComponent<PolarisListItem>(0);
                builder.AddAttribute(1, nameof(PolarisListItem.ChildContent), (RenderFragment)(b => b.AddContent(0, "Set up payments")));
                builder.CloseComponent();
            }));

        Assert.NotNull(component.Find("s-ordered-list"));
        Assert.Equal("Set up payments", component.Find("s-list-item").TextContent);
    }

    [Fact]
    public void QueryContainer_renders_its_container_name()
    {
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisQueryContainer>(parameters => parameters
            .Add(p => p.ContainerName, "card"));

        Assert.Equal("card", component.Find("s-query-container").GetAttribute("containerName"));
    }
}
