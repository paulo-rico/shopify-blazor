using Microsoft.AspNetCore.Components;
using Polaris.Blazor.Events;

namespace Polaris.Blazor.Components;

/// <summary>Wraps <c>&lt;s-clickable-chip&gt;</c>, an interactive or dismissible chip.</summary>
/// <remarks>
/// Use this rather than a <see cref="PolarisChip"/> when the merchant can act on it — selecting a
/// filter, or removing a tag — because a plain chip is a visual indicator and not a control. Give
/// the graphic slot an icon component only.
/// </remarks>
public sealed class PolarisClickableChip : PolarisContentComponentBase
{
    /// <inheritdoc />
    protected override string TagName => "s-clickable-chip";

    /// <summary>Colour intensity.</summary>
    [Parameter] public PolarisColor Color { get; set; } = PolarisColor.Base;

    /// <summary>Shows a control that removes the chip.</summary>
    [Parameter] public bool Removable { get; set; }

    /// <summary>Renders the chip hidden. Presence means hidden; the interop layer also clears the attribute.</summary>
    [Parameter] public bool Hidden { get; set; }

    /// <summary>Prevents interaction.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>Renders the chip as a link.</summary>
    [Parameter] public string? Href { get; set; }

    /// <summary>Id of the element this chip controls, for a declarative command.</summary>
    [Parameter] public string? CommandFor { get; set; }

    /// <summary>The command sent to <see cref="CommandFor"/>.</summary>
    [Parameter] public PolarisCommand Command { get; set; } = PolarisCommand.Auto;

    /// <summary>Id of a tooltip or popover shown when the chip is hovered or focused.</summary>
    [Parameter] public string? InterestFor { get; set; }

    /// <summary>Raised on click.</summary>
    [Parameter] public EventCallback<PolarisEventArgs> OnClick { get; set; }

    /// <summary>Raised when the merchant removes the chip through its remove control.</summary>
    [Parameter] public EventCallback<PolarisEventArgs> OnRemove { get; set; }

    /// <summary>Raised after the chip has finished hiding.</summary>
    [Parameter] public EventCallback<PolarisEventArgs> OnAfterHide { get; set; }

    /// <inheritdoc />
    protected override void ConfigureEvents(PolarisEventRegistration events)
    {
        events.On(PolarisEvents.Click, OnClick);
        events.On(PolarisEvents.Remove, OnRemove);
        events.On(PolarisEvents.AfterHide, OnAfterHide);
    }

    /// <inheritdoc />
    protected override void BuildAttributes(Dictionary<string, string?> attributes)
    {
        base.BuildAttributes(attributes);

        attributes["color"] = PolarisAttributeValue.EnumValue(Color);
        attributes["removable"] = PolarisAttributeValue.Format(Removable);
        attributes["hidden"] = Hidden ? string.Empty : null;
        attributes["disabled"] = PolarisAttributeValue.Format(Disabled);
        attributes["href"] = Href;
        attributes["commandFor"] = CommandFor;
        attributes["command"] = CommandFor is null ? null : PolarisAttributeValue.EnumValue(Command);
        attributes["interestFor"] = InterestFor;
    }
}
