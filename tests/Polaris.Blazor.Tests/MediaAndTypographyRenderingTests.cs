using Bunit;
using Microsoft.AspNetCore.Components;
using Polaris.Blazor.Components;
using Xunit;

namespace Polaris.Blazor.Tests;

/// <summary>
/// Rendering tests for the media and typography wrappers added with the second inventory pass.
/// </summary>
public sealed class MediaAndTypographyRenderingTests
{
    [Fact]
    public void Avatar_renders_initials_and_size()
    {
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisAvatar>(parameters => parameters
            .Add(p => p.Initials, "JD")
            .Add(p => p.Alt, "John Doe")
            .Add(p => p.Size, PolarisSize.Large100));

        var element = component.Find("s-avatar");
        Assert.Equal("JD", element.GetAttribute("initials"));
        Assert.Equal("John Doe", element.GetAttribute("alt"));
        Assert.Equal("large-100", element.GetAttribute("size"));
    }

    [Fact]
    public void Avatar_forwards_load_and_error()
    {
        using var context = new PolarisTestContext();

        context.Render<PolarisAvatar>(parameters => parameters
            .Add(p => p.Src, "https://cdn.example.com/avatar.png")
            .Add(p => p.OnLoad, EventCallback.Factory.Create<Polaris.Blazor.Events.PolarisEventArgs>(this, _ => { }))
            .Add(p => p.OnError, EventCallback.Factory.Create<Polaris.Blazor.Events.PolarisEventArgs>(this, _ => { })));

        Assert.True(context.Interop.AttachedEvents.TryGetValue("s-avatar", out var events));
        Assert.Equal(["load", "error"], events);
    }

    [Fact]
    public void Image_renders_src_and_presentation_properties()
    {
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisImage>(parameters => parameters
            .Add(p => p.Src, "https://cdn.example.com/plant.png")
            .Add(p => p.Alt, "Indoor plant")
            .Add(p => p.ObjectFit, PolarisImageObjectFit.Cover)
            .Add(p => p.Loading, PolarisImageLoading.Lazy)
            .Add(p => p.AccessibilityRole, PolarisImageAccessibilityRole.Img)
            .Add(p => p.BorderRadius, "base"));

        var element = component.Find("s-image");
        Assert.Equal("https://cdn.example.com/plant.png", element.GetAttribute("src"));
        Assert.Equal("Indoor plant", element.GetAttribute("alt"));
        Assert.Equal("cover", element.GetAttribute("objectFit"));
        Assert.Equal("lazy", element.GetAttribute("loading"));
        Assert.Equal("img", element.GetAttribute("accessibilityRole"));
        Assert.Equal("base", element.GetAttribute("borderRadius"));
    }

    [Fact]
    public void Thumbnail_renders_src_alt_and_size()
    {
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisThumbnail>(parameters => parameters
            .Add(p => p.Src, "https://cdn.example.com/thumb.jpg")
            .Add(p => p.Alt, "White sneakers")
            .Add(p => p.Size, PolarisSize.Small));

        var element = component.Find("s-thumbnail");
        Assert.Equal("https://cdn.example.com/thumb.jpg", element.GetAttribute("src"));
        Assert.Equal("White sneakers", element.GetAttribute("alt"));
        Assert.Equal("small", element.GetAttribute("size"));
    }

    [Fact]
    public void Number_renders_its_typography_and_direction()
    {
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisNumber>(parameters => parameters
            .Add(p => p.FontSize, PolarisFontSize.Large)
            .Add(p => p.FontWeight, PolarisFontWeight.Bold)
            .Add(p => p.Color, PolarisColor.Subdued)
            .Add(p => p.Dir, PolarisTextDirection.Rtl)
            .Add(p => p.AccessibilityVisibility, PolarisAccessibilityVisibility.Exclusive)
            .AddChildContent("1,204"));

        var element = component.Find("s-number");
        Assert.Equal("large", element.GetAttribute("fontSize"));
        Assert.Equal("bold", element.GetAttribute("fontWeight"));
        Assert.Equal("subdued", element.GetAttribute("color"));
        Assert.Equal("rtl", element.GetAttribute("dir"));
        Assert.Equal("exclusive", element.GetAttribute("accessibilityVisibility"));
        Assert.Equal("1,204", element.TextContent);
    }

    [Fact]
    public void Number_omits_direction_when_it_is_unset()
    {
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisNumber>(parameters => parameters.AddChildContent("42"));

        // Unset means the component's own empty default; "auto" would be a different instruction.
        Assert.False(component.Find("s-number").HasAttribute("dir"));
    }

    [Fact]
    public void Text_renders_the_inline_element_with_its_text_only_attributes()
    {
        // s-text and s-paragraph share several properties but not all of them, and they were once one
        // wrapper that chose its element from a Paragraph flag. The failure that hid: `type` and
        // `interestFor` exist only on s-text and `lineClamp` only on s-paragraph, so a merged wrapper
        // silently dropped whichever half it was not rendering.
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisText>(parameters => parameters
            .Add(p => p.Type, PolarisTextType.Strong)
            .Add(p => p.Tone, PolarisTone.Critical)
            .Add(p => p.Color, PolarisColor.Subdued)
            .Add(p => p.FontSize, PolarisFontSize.Small)
            .Add(p => p.FontWeight, PolarisFontWeight.Semibold)
            .Add(p => p.InterestFor, "sku-tooltip")
            .Add(p => p.Dir, PolarisTextDirection.Rtl)
            .Add(p => p.AccessibilityVisibility, PolarisAccessibilityVisibility.Exclusive)
            .AddChildContent("SKU"));

        var element = component.Find("s-text");
        Assert.Equal("strong", element.GetAttribute("type"));
        Assert.Equal("critical", element.GetAttribute("tone"));
        Assert.Equal("subdued", element.GetAttribute("color"));
        Assert.Equal("small", element.GetAttribute("fontSize"));
        Assert.Equal("semibold", element.GetAttribute("fontWeight"));
        Assert.Equal("sku-tooltip", element.GetAttribute("interestFor"));
        Assert.Equal("rtl", element.GetAttribute("dir"));
        Assert.Equal("exclusive", element.GetAttribute("accessibilityVisibility"));
        Assert.Equal("SKU", element.TextContent);

        // The inline wrapper renders the inline element and never the block one.
        Assert.Empty(component.FindAll("s-paragraph"));
    }

    [Fact]
    public void Paragraph_renders_the_block_element_with_its_paragraph_only_clamp()
    {
        // The other half of the same split: `lineClamp` belongs to s-paragraph alone, and a paragraph
        // was previously reachable only by setting Paragraph on the text wrapper — which rendered
        // inline text, with no truncation, whenever someone forgot the flag.
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisParagraph>(parameters => parameters
            .Add(p => p.Tone, PolarisTone.Caution)
            .Add(p => p.FontSize, PolarisFontSize.Large)
            .Add(p => p.FontWeight, PolarisFontWeight.Base)
            .Add(p => p.LineClamp, 2)
            .Add(p => p.Dir, PolarisTextDirection.Ltr)
            .Add(p => p.AccessibilityVisibility, PolarisAccessibilityVisibility.Visible)
            .AddChildContent("Review the shipping address before processing."));

        var element = component.Find("s-paragraph");
        Assert.Equal("caution", element.GetAttribute("tone"));
        Assert.Equal("large", element.GetAttribute("fontSize"));
        Assert.Equal("base", element.GetAttribute("fontWeight"));
        Assert.Equal("2", element.GetAttribute("lineClamp"));
        Assert.Equal("ltr", element.GetAttribute("dir"));
        Assert.Equal("visible", element.GetAttribute("accessibilityVisibility"));
        Assert.Equal("Review the shipping address before processing.", element.TextContent);

        // The block wrapper renders the block element, so a paragraph can never be silently emitted as
        // an inline run.
        Assert.Empty(component.FindAll("s-text"));
    }

    [Fact]
    public void Paragraph_omits_the_clamp_when_it_is_unset()
    {
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisParagraph>(parameters => parameters.AddChildContent("Prose."));

        // Unset means "no truncation"; an empty or zero lineClamp is a different instruction to Polaris,
        // which would clamp the paragraph away entirely.
        Assert.False(component.Find("s-paragraph").HasAttribute("lineClamp"));
    }

    [Fact]
    public void Tooltip_renders_its_id_and_text_children()
    {
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisTooltip>(parameters => parameters
            .Add(p => p.Id, "bold-tooltip")
            .AddChildContent("Bold"));

        var element = component.Find("s-tooltip");
        Assert.Equal("bold-tooltip", element.GetAttribute("id"));
        Assert.Equal("Bold", element.TextContent);
    }
}
