using Microsoft.AspNetCore.Components;

namespace Polaris.Blazor.Components;

/// <summary>Wraps <c>&lt;s-chip&gt;</c>, a user-supplied label or tag.</summary>
/// <remarks>
/// A chip is a visual indicator, not a control: use a <see cref="PolarisClickableChip"/> when it can
/// be selected or removed, and a <see cref="PolarisBadge"/> for the status of a resource. A chip is
/// single-line and truncates, so a long label needs a tooltip or a different component.
/// </remarks>
public sealed class PolarisChip : PolarisContentComponentBase
{
    /// <inheritdoc />
    protected override string TagName => "s-chip";

    /// <summary>Colour intensity.</summary>
    [Parameter] public PolarisColor Color { get; set; } = PolarisColor.Base;

    /// <summary>Shows a control that removes the chip. The chip itself does not act on it; handle the event.</summary>
    [Parameter] public bool Removable { get; set; }

    /// <inheritdoc />
    protected override void BuildAttributes(Dictionary<string, string?> attributes)
    {
        base.BuildAttributes(attributes);

        attributes["color"] = PolarisAttributeValue.EnumValue(Color);
        attributes["removable"] = PolarisAttributeValue.Format(Removable);
    }
}
