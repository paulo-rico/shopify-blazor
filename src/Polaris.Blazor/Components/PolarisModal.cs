using Microsoft.AspNetCore.Components;
using Polaris.Blazor.Events;

namespace Polaris.Blazor.Components;

/// <summary>Wraps <c>&lt;s-modal&gt;</c>.</summary>
/// <remarks>
/// <para>
/// Shopify only opens a modal in response to a user gesture, so prefer the declarative form: give
/// the modal an <see cref="PolarisElementComponentBase.Id"/> and point a
/// <see cref="PolarisButton.CommandFor"/> at it. That cannot fall foul of the gesture rule.
/// </para>
/// <para>
/// <see cref="ShowAsync"/> and <see cref="HideAsync"/> exist for imperative cases. Because they
/// call the element's own methods, they also require the element to have upgraded; the interop
/// layer waits for that before calling.
/// </para>
/// </remarks>
public sealed class PolarisModal : PolarisSlottedComponentBase
{
    /// <inheritdoc />
    protected override string TagName => "s-modal";

    /// <summary>Modal title. Also the accessible name unless <c>AccessibilityLabel</c> is set.</summary>
    [Parameter] public string? Heading { get; set; }

    /// <summary>Padding around the content.</summary>
    [Parameter] public PolarisModalPadding Padding { get; set; } = PolarisModalPadding.Base;

    /// <summary>Modal width.</summary>
    [Parameter] public PolarisModalSize Size { get; set; } = PolarisModalSize.Base;

    /// <summary>Raised when the modal is shown.</summary>
    [Parameter] public EventCallback<PolarisEventArgs> OnShow { get; set; }

    /// <summary>Raised when the modal is hidden.</summary>
    [Parameter] public EventCallback<PolarisEventArgs> OnHide { get; set; }

    /// <inheritdoc />
    protected override void ConfigureEvents(PolarisEventRegistration events)
    {
        events.On(PolarisEvents.Show, OnShow);
        events.On(PolarisEvents.Hide, OnHide);
    }

    /// <inheritdoc />
    protected override void BuildAttributes(Dictionary<string, string?> attributes)
    {
        base.BuildAttributes(attributes);

        attributes["heading"] = Heading;
        attributes["padding"] = PolarisAttributeValue.EnumValue(Padding);
        attributes["size"] = PolarisAttributeValue.EnumValue(Size);
    }

    /// <summary>Opens the modal through the element's own <c>showOverlay()</c>.</summary>
    public Task ShowAsync() => Interop.InvokeElementMethodAsync(Element, "showOverlay").AsTask();

    /// <summary>Closes the modal through the element's own <c>hideOverlay()</c>.</summary>
    public Task HideAsync() => Interop.InvokeElementMethodAsync(Element, "hideOverlay").AsTask();
}
