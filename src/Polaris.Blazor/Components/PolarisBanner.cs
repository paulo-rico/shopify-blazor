using Microsoft.AspNetCore.Components;
using Polaris.Blazor.Events;

namespace Polaris.Blazor.Components;

/// <summary>Wraps <c>&lt;s-banner&gt;</c>.</summary>
/// <remarks>
/// Polaris does not persist dismissal, so recording that the merchant closed a banner is the
/// application's job — handle <see cref="OnDismiss"/> and stop rendering it.
/// </remarks>
public sealed class PolarisBanner : PolarisSlottedComponentBase
{
    /// <inheritdoc />
    protected override string TagName => "s-banner";

    /// <summary>Banner title.</summary>
    [Parameter] public string? Heading { get; set; }

    /// <summary>Semantic colour. Use <see cref="PolarisTone.Critical"/> for failures.</summary>
    [Parameter] public PolarisTone Tone { get; set; } = PolarisTone.Auto;

    /// <summary>
    /// Renders the banner hidden. Useful for toggling an inline error without re-rendering the
    /// surrounding page.
    /// </summary>
    [Parameter] public bool Hidden { get; set; }

    /// <summary>Shows a close button.</summary>
    [Parameter] public bool Dismissible { get; set; }

    /// <summary>Raised when the merchant dismisses the banner.</summary>
    [Parameter] public EventCallback<PolarisEventArgs> OnDismiss { get; set; }

    /// <summary>Raised after the banner has finished hiding.</summary>
    [Parameter] public EventCallback<PolarisEventArgs> OnAfterHide { get; set; }

    /// <inheritdoc />
    protected override void ConfigureEvents(PolarisEventRegistration events)
    {
        events.On(PolarisEvents.Dismiss, OnDismiss);
        events.On("afterhide", OnAfterHide);
    }

    /// <inheritdoc />
    protected override void BuildAttributes(Dictionary<string, string?> attributes)
    {
        base.BuildAttributes(attributes);

        attributes["heading"] = Heading;
        attributes["tone"] = PolarisAttributeValue.EnumValue(Tone);

        // `hidden` is the standard global HTML attribute: presence means hidden.
        attributes["hidden"] = Hidden ? string.Empty : null;
        attributes["dismissible"] = PolarisAttributeValue.Format(Dismissible);
    }

    /// <inheritdoc />
    protected override RenderFragment? BuildChildContent() => ChildContent;
}
