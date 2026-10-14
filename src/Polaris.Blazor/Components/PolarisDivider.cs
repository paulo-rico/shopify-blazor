using Microsoft.AspNetCore.Components;

namespace Polaris.Blazor.Components;

/// <summary>Wraps <c>&lt;s-divider&gt;</c>.</summary>
/// <remarks>
/// Shopify's names describe the axis the divider spans, not the axis it separates, so
/// <see cref="PolarisDividerDirection.Inline"/> is a horizontal rule and is the default.
/// </remarks>
public sealed class PolarisDivider : PolarisElementComponentBase
{
    /// <inheritdoc />
    protected override string TagName => "s-divider";

    /// <summary>Which axis the rule spans. Inline is horizontal.</summary>
    [Parameter] public PolarisDividerDirection Direction { get; set; } = PolarisDividerDirection.Inline;

    /// <summary>Rule contrast.</summary>
    [Parameter] public PolarisColor Color { get; set; } = PolarisColor.Base;

    /// <inheritdoc />
    protected override void BuildAttributes(Dictionary<string, string?> attributes)
    {
        base.BuildAttributes(attributes);

        attributes["direction"] = PolarisAttributeValue.EnumValue(Direction);
        attributes["color"] = PolarisAttributeValue.EnumValue(Color);
    }
}
