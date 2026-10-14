using Microsoft.AspNetCore.Components;
namespace Polaris.Blazor.Components;

/// <summary>Wraps <c>&lt;s-icon&gt;</c>. Only the admin's predefined icon set is available.</summary>
public sealed class PolarisIcon : PolarisElementComponentBase
{
    /// <inheritdoc />
    protected override string TagName => "s-icon";

    /// <summary>
    /// Icon name from the admin set, for example <c>product</c>, <c>delete</c> or <c>home</c>.
    /// An empty string hides the icon; <c>empty</c> reserves its space.
    /// </summary>
    [Parameter] public string? Type { get; set; }

    /// <summary>Semantic colour.</summary>
    [Parameter] public PolarisTone Tone { get; set; } = PolarisTone.Auto;

    /// <summary>Standard or subdued. Use <c>subdued</c> for a decorative icon.</summary>
    [Parameter] public PolarisColor Color { get; set; } = PolarisColor.Base;

    /// <summary>Icon size.</summary>
    [Parameter] public PolarisIconSize Size { get; set; } = PolarisIconSize.Base;

    /// <summary>Id of a tooltip or popover shown on hover or focus.</summary>
    [Parameter] public string? InterestFor { get; set; }

    /// <inheritdoc />
    protected override void BuildAttributes(Dictionary<string, string?> attributes)
    {
        base.BuildAttributes(attributes);

        attributes["type"] = Type;
        attributes["tone"] = PolarisAttributeValue.EnumValue(Tone);
        attributes["color"] = PolarisAttributeValue.EnumValue(Color);
        attributes["size"] = PolarisAttributeValue.EnumValue(Size);
        attributes["interestFor"] = InterestFor;
    }
}
