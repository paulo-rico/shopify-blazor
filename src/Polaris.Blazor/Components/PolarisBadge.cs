using Microsoft.AspNetCore.Components;
namespace Polaris.Blazor.Components;

/// <summary>Wraps <c>&lt;s-badge&gt;</c>. Single line, truncates; for user labels use a chip.</summary>
public sealed class PolarisBadge : PolarisContentComponentBase
{
    /// <inheritdoc />
    protected override string TagName => "s-badge";

    /// <summary>Semantic colour.</summary>
    [Parameter] public PolarisTone Tone { get; set; } = PolarisTone.Auto;

    /// <summary>Contrast of the badge.</summary>
    [Parameter] public PolarisColor Color { get; set; } = PolarisColor.Base;

    /// <summary>Badge size.</summary>
    [Parameter] public PolarisSize Size { get; set; } = PolarisSize.Base;

    /// <summary>Name of a predefined admin icon shown before the label.</summary>
    [Parameter] public string? Icon { get; set; }

    /// <inheritdoc />
    protected override void BuildAttributes(Dictionary<string, string?> attributes)
    {
        base.BuildAttributes(attributes);

        attributes["tone"] = PolarisAttributeValue.EnumValue(Tone);
        attributes["color"] = PolarisAttributeValue.EnumValue(Color);
        attributes["size"] = PolarisAttributeValue.EnumValue(Size);
        attributes["icon"] = Icon;
    }
}
