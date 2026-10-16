using Microsoft.AspNetCore.Components;

namespace Polaris.Blazor.Components;

/// <summary>Wraps <c>&lt;s-paragraph&gt;</c>, a block of prose.</summary>
/// <remarks>
/// <para>
/// A paragraph owns vertical spacing above and below and may contain inline elements — text runs,
/// links, buttons — which is what makes it a block. That spacing is deliberate, and it is also the
/// usual reason to reach for something else: inside a tightly packed layout a paragraph leaves gaps
/// nobody asked for, so a <see cref="PolarisText"/> or a layout component is the better fit there.
/// </para>
/// <para>
/// For styling a few words inside a paragraph, nest <see cref="PolarisText"/> rather than setting the
/// properties here; the paragraph's own properties style the whole block.
/// </para>
/// <para>
/// Polaris deprecates <c>fontVariantNumeric</c> on this element in favour of
/// <see cref="PolarisNumber"/>, which is left to that wrapper.
/// </para>
/// </remarks>
public sealed class PolarisParagraph : PolarisContentComponentBase
{
    /// <inheritdoc />
    protected override string TagName => "s-paragraph";

    /// <summary>Semantic colour.</summary>
    [Parameter] public PolarisTone Tone { get; set; } = PolarisTone.Auto;

    /// <summary>Standard or subdued.</summary>
    [Parameter] public PolarisColor Color { get; set; } = PolarisColor.Base;

    /// <summary>Font size.</summary>
    [Parameter] public PolarisFontSize FontSize { get; set; } = PolarisFontSize.Auto;

    /// <summary>Font weight.</summary>
    [Parameter] public PolarisFontWeight FontWeight { get; set; } = PolarisFontWeight.Auto;

    /// <summary>Lines after which the paragraph is truncated visually.</summary>
    /// <remarks>
    /// This exists on <c>s-paragraph</c> and not on <c>s-text</c>. Truncation hides content, so it
    /// needs a way to reach the full text — a tooltip on the same element, say.
    /// </remarks>
    [Parameter] public int? LineClamp { get; set; }

    /// <summary>Text direction. Unset means the component's own default.</summary>
    [Parameter] public PolarisTextDirection? Dir { get; set; }

    /// <summary>How assistive technology sees the paragraph.</summary>
    /// <remarks>
    /// Use <see cref="PolarisAccessibilityVisibility.Exclusive"/> for a screen-reader-only paragraph,
    /// such as the sort order a table communicates visually.
    /// </remarks>
    [Parameter] public PolarisAccessibilityVisibility? AccessibilityVisibility { get; set; }

    /// <inheritdoc />
    protected override void BuildAttributes(Dictionary<string, string?> attributes)
    {
        base.BuildAttributes(attributes);

        attributes["tone"] = PolarisAttributeValue.EnumValue(Tone);
        attributes["color"] = PolarisAttributeValue.EnumValue(Color);
        attributes["fontSize"] = PolarisAttributeValue.EnumValue(FontSize);
        attributes["fontWeight"] = PolarisAttributeValue.EnumValue(FontWeight);
        attributes["lineClamp"] = PolarisAttributeValue.Format(LineClamp);
        attributes["dir"] = Dir is null ? null : PolarisAttributeValue.EnumValue(Dir.Value);
        attributes["accessibilityVisibility"] =
            AccessibilityVisibility is null ? null : PolarisAttributeValue.EnumValue(AccessibilityVisibility.Value);
    }
}
