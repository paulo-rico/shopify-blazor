using Microsoft.AspNetCore.Components;

namespace Polaris.Blazor.Components;

/// <summary>Wraps <c>&lt;s-number&gt;</c>, an inline numeric value.</summary>
/// <remarks>
/// <para>
/// The point of this component is tabular numerals, so figures line up down a column — use it for
/// every number inside a table or a totalling layout instead of a <see cref="PolarisText"/>.
/// </para>
/// <para>
/// It is an inline element with no spacing of its own, so wrap it in a
/// <see cref="PolarisStack"/> or a paragraph when it needs to stand apart. Polaris deprecates the
/// <c>fontVariantNumeric</c> property on the text components in its favour.
/// </para>
/// </remarks>
public sealed class PolarisNumber : PolarisContentComponentBase
{
    /// <inheritdoc />
    protected override string TagName => "s-number";

    /// <summary>Semantic colour.</summary>
    [Parameter] public PolarisTone Tone { get; set; } = PolarisTone.Auto;

    /// <summary>Standard or subdued.</summary>
    [Parameter] public PolarisColor Color { get; set; } = PolarisColor.Base;

    /// <summary>Font size.</summary>
    [Parameter] public PolarisFontSize FontSize { get; set; } = PolarisFontSize.Auto;

    /// <summary>Font weight.</summary>
    [Parameter] public PolarisFontWeight FontWeight { get; set; } = PolarisFontWeight.Auto;

    /// <summary>Text direction. Unset means the component's own default.</summary>
    [Parameter] public PolarisTextDirection? Dir { get; set; }

    /// <summary>How assistive technology sees the value.</summary>
    [Parameter] public PolarisAccessibilityVisibility? AccessibilityVisibility { get; set; }

    /// <inheritdoc />
    protected override void BuildAttributes(Dictionary<string, string?> attributes)
    {
        base.BuildAttributes(attributes);

        attributes["tone"] = PolarisAttributeValue.EnumValue(Tone);
        attributes["color"] = PolarisAttributeValue.EnumValue(Color);
        attributes["fontSize"] = PolarisAttributeValue.EnumValue(FontSize);
        attributes["fontWeight"] = PolarisAttributeValue.EnumValue(FontWeight);
        attributes["dir"] = Dir is null ? null : PolarisAttributeValue.EnumValue(Dir.Value);
        attributes["accessibilityVisibility"] =
            AccessibilityVisibility is null ? null : PolarisAttributeValue.EnumValue(AccessibilityVisibility.Value);
    }
}
