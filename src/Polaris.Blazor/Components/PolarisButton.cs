using Microsoft.AspNetCore.Components;
using Polaris.Blazor.Events;

namespace Polaris.Blazor.Components;

/// <summary>Wraps Shopify's <c>&lt;s-button&gt;</c>.</summary>
public sealed class PolarisButton : PolarisContentComponentBase
{
    /// <inheritdoc />
    protected override string TagName => "s-button";

    /// <summary>Emphasis of the button.</summary>
    [Parameter] public PolarisButtonVariant Variant { get; set; } = PolarisButtonVariant.Auto;

    /// <summary>Semantic colour, usually <see cref="PolarisTone.Critical"/> for a destructive action.</summary>
    [Parameter] public PolarisTone Tone { get; set; } = PolarisTone.Auto;

    /// <summary>Name of a predefined admin icon, rendered before the label.</summary>
    [Parameter] public string? Icon { get; set; }

    /// <summary>Prevents interaction. A disabled button also leaves the tab order.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>
    /// Shows the button's own loading state.
    /// </summary>
    /// <remarks>
    /// Polaris documents that this gives feedback but does <em>not</em> debounce or block repeat
    /// clicks. Guard against double submission in C# as well.
    /// </remarks>
    [Parameter] public bool Loading { get; set; }

    /// <summary>Form behaviour: submit or reset the closest form.</summary>
    [Parameter] public PolarisButtonType Type { get; set; } = PolarisButtonType.Button;

    /// <summary>Renders the button as a link. Shopify may block external URLs inside the Admin.</summary>
    [Parameter] public string? Href { get; set; }

    /// <summary>Browsing context for <see cref="Href"/>.</summary>
    [Parameter] public PolarisLinkTarget Target { get; set; } = PolarisLinkTarget.Auto;

    /// <summary>Width behaviour of the button.</summary>
    [Parameter] public PolarisButtonInlineSize InlineSize { get; set; } = PolarisButtonInlineSize.Auto;

    /// <summary>
    /// Id of the element this button controls: the declarative way to open a modal or popover with
    /// no JavaScript at all.
    /// </summary>
    [Parameter] public string? CommandFor { get; set; }

    /// <summary>The command sent to <see cref="CommandFor"/>.</summary>
    [Parameter] public PolarisCommand Command { get; set; } = PolarisCommand.Auto;

    /// <summary>Id of a tooltip or popover shown when the button is hovered or focused.</summary>
    [Parameter] public string? InterestFor { get; set; }

    /// <summary>Raised on click. Modifier keys are on the event args.</summary>
    [Parameter] public EventCallback<PolarisEventArgs> OnClick { get; set; }

    /// <summary>Raised when the button loses focus.</summary>
    [Parameter] public EventCallback<PolarisEventArgs> OnBlur { get; set; }

    /// <summary>Raised when the button receives focus.</summary>
    [Parameter] public EventCallback<PolarisEventArgs> OnFocus { get; set; }

    /// <inheritdoc />
    protected override void ConfigureEvents(PolarisEventRegistration events)
    {
        events.On(PolarisEvents.Click, OnClick);
        events.On(PolarisEvents.Blur, OnBlur);
        events.On(PolarisEvents.Focus, OnFocus);
    }

    /// <inheritdoc />
    protected override void BuildAttributes(Dictionary<string, string?> attributes)
    {
        base.BuildAttributes(attributes);

        attributes["variant"] = PolarisAttributeValue.EnumValue(Variant);
        attributes["tone"] = PolarisAttributeValue.EnumValue(Tone);
        attributes["icon"] = Icon;
        attributes["disabled"] = PolarisAttributeValue.Format(Disabled);
        attributes["loading"] = PolarisAttributeValue.Format(Loading);
        attributes["type"] = PolarisAttributeValue.EnumValue(Type);
        attributes["href"] = Href;
        attributes["target"] = Href is null ? null : PolarisAttributeValue.EnumValue(Target);
        attributes["inlineSize"] = PolarisAttributeValue.EnumValue(InlineSize);
        attributes["commandFor"] = CommandFor;
        attributes["command"] = CommandFor is null ? null : PolarisAttributeValue.EnumValue(Command);
        attributes["interestFor"] = InterestFor;
    }
}
