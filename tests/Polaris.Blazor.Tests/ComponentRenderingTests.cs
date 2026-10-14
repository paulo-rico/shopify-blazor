using Bunit;
using Microsoft.AspNetCore.Components;
using Polaris.Blazor.Components;
using Xunit;

namespace Polaris.Blazor.Tests;

/// <summary>
/// Rendering tests for the component wrappers: the markup each one produces for a given set of
/// parameters.
/// </summary>
/// <remarks>
/// These assert on the wire format the browser receives, because that is where the traps live:
/// a boolean that must be a bare attribute, an enum that must be Shopify's spelling rather than
/// the .NET member name, and a slot wrapper that must carry <c>slot="…"</c>.
/// </remarks>
public sealed class ComponentRenderingTests
{
    [Fact]
    public void Button_renders_the_custom_element()
    {
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisButton>();

        Assert.NotNull(component.Find("s-button"));
    }

    [Fact]
    public void Button_renders_variant_and_tone_as_shopify_values()
    {
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisButton>(parameters => parameters
            .Add(p => p.Variant, PolarisButtonVariant.Primary)
            .Add(p => p.Tone, PolarisTone.Critical));

        var element = component.Find("s-button");
        Assert.Equal("primary", element.GetAttribute("variant"));
        Assert.Equal("critical", element.GetAttribute("tone"));

        // The .NET member name must never reach the browser.
        Assert.DoesNotContain("Primary", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Button_renders_a_true_boolean_as_a_bare_attribute()
    {
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisButton>(parameters => parameters
            .Add(p => p.Disabled, true));

        var element = component.Find("s-button");
        Assert.True(element.HasAttribute("disabled"));

        // Presence is the only correct spelling: `disabled="false"` is still true to a custom
        // element, so false must remove the attribute entirely.
        Assert.Equal(string.Empty, element.GetAttribute("disabled"));
    }

    [Fact]
    public void Button_omits_a_false_boolean_entirely()
    {
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisButton>(parameters => parameters
            .Add(p => p.Disabled, false)
            .Add(p => p.Loading, false));

        var element = component.Find("s-button");
        Assert.False(element.HasAttribute("disabled"));
        Assert.False(element.HasAttribute("loading"));
    }

    [Fact]
    public void Button_forwards_command_for_so_a_modal_opens_without_javascript()
    {
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisButton>(parameters => parameters
            .Add(p => p.CommandFor, "save-modal")
            .Add(p => p.Command, PolarisCommand.Show));

        var element = component.Find("s-button");
        Assert.Equal("save-modal", element.GetAttribute("commandFor"));
        Assert.Equal("--show", element.GetAttribute("command"));
    }

    [Fact]
    public void Button_registers_an_interop_listener_only_for_supplied_callbacks()
    {
        using var context = new PolarisTestContext();

        context.Render<PolarisButton>(parameters => parameters
            .Add(p => p.OnClick, EventCallback.Factory.Create<Polaris.Blazor.Events.PolarisEventArgs>(this, _ => { })));

        // One listener for one subscribed event: an unsubscribed event would cost a JavaScript
        // listener and a SignalR round trip for every occurrence.
        Assert.True(context.Interop.AttachedEvents.TryGetValue("s-button", out var events));
        Assert.Equal(["click"], events);
    }

    [Fact]
    public void TextField_renders_label_and_initial_value()
    {
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisTextField>(parameters => parameters
            .Add(p => p.Label, "Store name")
            .Add(p => p.Value, "Jaded Pixel"));

        var element = component.Find("s-text-field");
        Assert.Equal("Store name", element.GetAttribute("label"));

        // `value` is not reflected, so it is emitted as the initial value only; the property is
        // what keeps it correct afterwards, asserted below.
        Assert.Equal("Jaded Pixel", element.GetAttribute("defaultValue"));
    }

    [Fact]
    public void TextField_sets_the_value_as_a_dom_property_after_render()
    {
        using var context = new PolarisTestContext();

        context.Render<PolarisTextField>(parameters => parameters
            .Add(p => p.Label, "Store name")
            .Add(p => p.Value, "Jaded Pixel"));

        Assert.True(context.Interop.SetProperties.TryGetValue("s-text-field", out var calls));
        Assert.Contains(calls, call => (string?)call["value"] == "Jaded Pixel");
    }

    [Fact]
    public void TextField_renders_error_and_details_for_validation_feedback()
    {
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisTextField>(parameters => parameters
            .Add(p => p.Label, "Title")
            .Add(p => p.Error, "Title is required.")
            .Add(p => p.Details, "Shown in the storefront.")
            .Add(p => p.Required, true));

        var element = component.Find("s-text-field");
        Assert.Equal("Title is required.", element.GetAttribute("error"));
        Assert.Equal("Shown in the storefront.", element.GetAttribute("details"));
        Assert.True(element.HasAttribute("required"));
    }

    [Fact]
    public void TextArea_never_puts_the_value_between_the_tags()
    {
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisTextArea>(parameters => parameters
            .Add(p => p.Label, "Address")
            .Add(p => p.Value, "1 Engine Way"));

        var element = component.Find("s-text-area");

        // Native <textarea>content</textarea> puts its value in the children, and copying that
        // habit here submits an empty string with no warning. The value must live in the attribute
        // (first render) or the property (afterwards), never in the content.
        Assert.Equal(string.Empty, element.TextContent);
        Assert.Equal("1 Engine Way", element.GetAttribute("defaultValue"));
    }

    [Fact]
    public void Checkbox_keeps_checked_out_of_the_markup_because_it_is_not_reflected()
    {
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisCheckbox>(parameters => parameters
            .Add(p => p.Label, "Require confirmation")
            .Add(p => p.Checked, true));

        var element = component.Find("s-checkbox");
        Assert.Equal("Require confirmation", element.GetAttribute("label"));

        // A stale `checked` attribute is exactly the bug this avoids: the element would keep the
        // old state no matter what the server renders next.
        Assert.False(element.HasAttribute("checked"));
        Assert.True(context.Interop.SetProperties.TryGetValue("s-checkbox", out var calls));
        Assert.Contains(calls, call => call["checked"] is true);
    }

    [Fact]
    public void Banner_renders_hidden_only_when_asked()
    {
        using var context = new PolarisTestContext();

        var visible = context.Render<PolarisBanner>(parameters => parameters
            .Add(p => p.Heading, "Saved")
            .Add(p => p.Hidden, false));

        var hidden = context.Render<PolarisBanner>(parameters => parameters
            .Add(p => p.Heading, "Saved")
            .Add(p => p.Hidden, true));

        Assert.False(visible.Find("s-banner").HasAttribute("hidden"));
        Assert.True(hidden.Find("s-banner").HasAttribute("hidden"));
    }

    [Fact]
    public void Table_loading_is_a_reflected_boolean()
    {
        using var context = new PolarisTestContext();

        var loading = context.Render<PolarisTable>(parameters => parameters.Add(p => p.Loading, true));
        var loaded = context.Render<PolarisTable>(parameters => parameters.Add(p => p.Loading, false));

        Assert.True(loading.Find("s-table").HasAttribute("loading"));
        Assert.False(loaded.Find("s-table").HasAttribute("loading"));
    }

    [Fact]
    public void TableHeader_renders_format_and_list_slot_as_shopify_values()
    {
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisTableHeader>(parameters => parameters
            .Add(p => p.Format, PolarisTableHeaderFormat.Currency)
            .Add(p => p.ListSlot, PolarisTableListSlot.Primary));

        var element = component.Find("s-table-header");
        Assert.Equal("currency", element.GetAttribute("format"));
        Assert.Equal("primary", element.GetAttribute("listSlot"));
    }

    [Fact]
    public void Page_renders_heading_and_width()
    {
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisPage>(parameters => parameters
            .Add(p => p.Heading, "Products")
            .Add(p => p.InlineSize, PolarisPageInlineSize.Large));

        var element = component.Find("s-page");
        Assert.Equal("Products", element.GetAttribute("heading"));
        Assert.Equal("large", element.GetAttribute("inlineSize"));
    }

    [Fact]
    public void Child_components_carry_their_slot_attribute()
    {
        using var context = new PolarisTestContext();

        // Polaris validates slot children and rejects a wrapper element, so the slot has to be set
        // on the component itself. This is the contract every slotted page depends on.
        var component = context.Render<PolarisPage>(parameters => parameters
            .Add(p => p.Heading, "Products")
            .AddChildContent(builder =>
            {
                builder.OpenComponent<PolarisButton>(0);
                builder.AddAttribute(1, nameof(PolarisButton.Slot), "primary-action");
                builder.AddAttribute(2, nameof(PolarisButton.Variant), PolarisButtonVariant.Primary);
                builder.AddAttribute(3, nameof(PolarisButton.ChildContent), (RenderFragment)(b => b.AddContent(0, "Add product")));
                builder.CloseComponent();
            }));

        var button = component.Find("s-button");
        Assert.Equal("primary-action", button.GetAttribute("slot"));
        Assert.Equal("primary", button.GetAttribute("variant"));
    }

    [Fact]
    public void Divider_uses_inline_for_the_horizontal_rule()
    {
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisDivider>(parameters => parameters
            .Add(p => p.Direction, PolarisDividerDirection.Inline));

        Assert.Equal("inline", component.Find("s-divider").GetAttribute("direction"));
    }

    [Fact]
    public void Modal_renders_heading_and_size()
    {
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisModal>(parameters => parameters
            .Add(p => p.Heading, "Delete product")
            .Add(p => p.Size, PolarisModalSize.Small));

        var element = component.Find("s-modal");
        Assert.Equal("Delete product", element.GetAttribute("heading"));
        Assert.Equal("small", element.GetAttribute("size"));
    }

    [Fact]
    public void EmptyState_accepts_its_subheading_slot()
    {
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisEmptyState>(parameters => parameters
            .Add(p => p.Heading, "No products yet")
            .AddChildContent(builder =>
            {
                builder.OpenComponent<PolarisText>(0);
                builder.AddAttribute(1, nameof(PolarisText.Slot), "subheading");
                builder.AddAttribute(2, nameof(PolarisText.ChildContent), (RenderFragment)(b => b.AddContent(0, "Add your first product.")));
                builder.CloseComponent();
            }));

        var text = component.Find("s-text");
        Assert.Equal("subheading", text.GetAttribute("slot"));
    }

    [Fact]
    public void Select_renders_its_options_as_children()
    {
        using var context = new PolarisTestContext();

        var component = context.Render<PolarisSelect>(parameters => parameters
            .Add(p => p.Label, "Status")
            .AddChildContent(builder =>
            {
                builder.OpenComponent<PolarisOption>(0);
                builder.AddAttribute(1, nameof(PolarisOption.Value), "draft");
                builder.AddAttribute(2, nameof(PolarisOption.ChildContent), (RenderFragment)(b => b.AddContent(0, "Draft")));
                builder.CloseComponent();
            }));

        var select = component.Find("s-select");
        Assert.Equal("Status", select.GetAttribute("label"));
        Assert.Equal("draft", component.Find("s-option").GetAttribute("value"));
    }
}
