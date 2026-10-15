using Bunit;
using Microsoft.AspNetCore.Components;
using Polaris.Blazor.Components;
using Xunit;

namespace Polaris.Blazor.Tests;

/// <summary>
/// Rendering tests for the action and feedback wrappers added with the second inventory pass.
/// </summary>
public sealed class ActionComponentRenderingTests
{
    [Fact]
    public void Link_renders_href_tone_and_target()
    {
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisLink>(parameters => parameters
            .Add(p => p.Href, "/products")
            .Add(p => p.Tone, PolarisTone.Critical)
            .Add(p => p.Target, PolarisLinkTarget.Blank));

        var element = component.Find("s-link");
        Assert.Equal("/products", element.GetAttribute("href"));
        Assert.Equal("critical", element.GetAttribute("tone"));
        Assert.Equal("_blank", element.GetAttribute("target"));
    }

    [Fact]
    public void Link_omits_target_when_there_is_no_href()
    {
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisLink>(parameters => parameters
            .Add(p => p.Target, PolarisLinkTarget.Blank));

        // A target with nothing to open is meaningless, and `target="auto"` on an inert link is
        // exactly the kind of stray attribute that confuses a later reader.
        Assert.False(component.Find("s-link").HasAttribute("target"));
    }

    [Fact]
    public void Menu_renders_its_accessible_label_and_accepts_button_children()
    {
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisMenu>(parameters => parameters
            .Add(p => p.Id, "customer-menu")
            .Add(p => p.AccessibilityLabel, "Customer actions")
            .AddChildContent(builder =>
            {
                builder.OpenComponent<PolarisButton>(0);
                builder.AddAttribute(1, nameof(PolarisButton.Tone), PolarisTone.Critical);
                builder.CloseComponent();
            }));

        var element = component.Find("s-menu");
        Assert.Equal("customer-menu", element.GetAttribute("id"));
        Assert.Equal("Customer actions", element.GetAttribute("accessibilityLabel"));
        Assert.NotNull(component.Find("s-button"));
    }

    [Fact]
    public void Clickable_renders_box_styling_and_presence_booleans()
    {
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisClickable>(parameters => parameters
            .Add(p => p.Padding, "base")
            .Add(p => p.Background, "subdued")
            .Add(p => p.BorderRadius, "base")
            .Add(p => p.Disabled, true)
            .Add(p => p.Loading, false));

        var element = component.Find("s-clickable");
        Assert.Equal("base", element.GetAttribute("padding"));
        Assert.Equal("subdued", element.GetAttribute("background"));
        Assert.Equal("base", element.GetAttribute("borderRadius"));

        // Presence is the only correct spelling of a Polaris boolean.
        Assert.True(element.HasAttribute("disabled"));
        Assert.False(element.HasAttribute("loading"));
    }

    [Fact]
    public void Clickable_registers_only_the_callbacks_it_was_given()
    {
        using var context = new PolarisTestContext();

        context.Render<PolarisClickable>(parameters => parameters
            .Add(p => p.OnClick, EventCallback.Factory.Create<Polaris.Blazor.Events.PolarisEventArgs>(this, _ => { })));

        Assert.True(context.Interop.AttachedEvents.TryGetValue("s-clickable", out var events));
        Assert.Equal(["click"], events);
    }

    [Fact]
    public void ClickableChip_renders_colour_removable_and_hidden()
    {
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisClickableChip>(parameters => parameters
            .Add(p => p.Color, PolarisColor.Strong)
            .Add(p => p.Removable, true)
            .Add(p => p.Hidden, true)
            .Add(p => p.Disabled, false));

        var element = component.Find("s-clickable-chip");
        Assert.Equal("strong", element.GetAttribute("color"));
        Assert.True(element.HasAttribute("removable"));
        Assert.True(element.HasAttribute("hidden"));
        Assert.False(element.HasAttribute("disabled"));
    }

    [Fact]
    public void Chip_renders_colour_and_removable()
    {
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisChip>(parameters => parameters
            .Add(p => p.Color, PolarisColor.Subdued)
            .Add(p => p.Removable, true));

        var element = component.Find("s-chip");
        Assert.Equal("subdued", element.GetAttribute("color"));
        Assert.True(element.HasAttribute("removable"));
    }

    [Fact]
    public void Progress_is_indeterminate_until_a_value_is_given()
    {
        using var context = new PolarisTestContext();

        var indeterminate = context.Render<PolarisProgress>(parameters => parameters
            .Add(p => p.AccessibilityLabel, "Uploading"));

        var determinate = context.Render<PolarisProgress>(parameters => parameters
            .Add(p => p.Value, 3)
            .Add(p => p.Max, 5)
            .Add(p => p.Tone, PolarisTone.Success));

        // Writing value="0" for an unset value would pin an empty determinate bar instead of an
        // indeterminate one.
        Assert.False(indeterminate.Find("s-progress").HasAttribute("value"));
        Assert.Equal("3", determinate.Find("s-progress").GetAttribute("value"));
        Assert.Equal("5", determinate.Find("s-progress").GetAttribute("max"));
        Assert.Equal("success", determinate.Find("s-progress").GetAttribute("tone"));
    }
}
