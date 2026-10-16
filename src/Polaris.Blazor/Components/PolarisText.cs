using Microsoft.AspNetCore.Components;

namespace Polaris.Blazor.Components;

/// <summary>Wraps <c>&lt;s-text&gt;</c>, an inline run of text.</summary>
/// <remarks>
/// <para>
/// Text is inline: it flows with the words around it and has no spacing or truncation of its own, so
/// it is what emphasises or de-emphasises part of a sentence — <c>type="strong"</c> for a label,
/// <c>color="subdued"</c> for metadata. For a block of prose with its own margins, use
/// <see cref="PolarisParagraph"/>, which is a different element.
/// </para>
/// <para>
/// The two are separate wrappers because the elements differ: <c>lineClamp</c> exists on
/// <c>s-paragraph</c> and not here, and Polaris documents inline text as having no truncation at all.
/// </para>
/// <para>
/// Polaris deprecates <c>fontVariantNumeric</c> on this element in favour of
/// <see cref="PolarisNumber"/>, which is left to that wrapper.
/// </para>
/// </remarks>
public sealed class PolarisText : PolarisContentComponentBase
{
    /// <inheritdoc />
    protected override string TagName => "s-text";

    /// <summary>Semantic type of the run: strong, generic, address or redundant.</summary>
    [Parameter] public PolarisTextType Type { get; set; } = PolarisTextType.Generic;

    /// <summary>Semantic colour.</summary>
    [Parameter] public PolarisTone Tone { get; set; } = PolarisTone.Auto;

    /// <summary>Standard or subdued.</summary>
    [Parameter] public PolarisColor Color { get; set; } = PolarisColor.Base;

    /// <summary>Font size.</summary>
    [Parameter] public PolarisFontSize FontSize { get; set; } = PolarisFontSize.Auto;

    /// <summary>Font weight.</summary>
    [Parameter] public PolarisFontWeight FontWeight { get; set; } = PolarisFontWeight.Auto;

    /// <summary>Id of a tooltip or popover shown on hover.</summary>
    /// <remarks>
    /// This exists on <c>s-text</c> and not on <c>s-paragraph</c>: it is how a run of explanatory text
    /// is wired to the tooltip that explains it.
    /// </remarks>
    [Parameter] public string? InterestFor { get; set; }

    /// <summary>Text direction. Unset means the component's own default.</summary>
    [Parameter] public PolarisTextDirection? Dir { get; set; }

    /// <summary>How assistive technology sees the run.</summary>
    [Parameter] public PolarisAccessibilityVisibility? AccessibilityVisibility { get; set; }

    /// <inheritdoc />
    protected override void BuildAttributes(Dictionary<string, string?> attributes)
    {
        base.BuildAttributes(attributes);

        attributes["type"] = PolarisAttributeValue.EnumValue(Type);
        attributes["tone"] = PolarisAttributeValue.EnumValue(Tone);
        attributes["color"] = PolarisAttributeValue.EnumValue(Color);
        attributes["fontSize"] = PolarisAttributeValue.EnumValue(FontSize);
        attributes["fontWeight"] = PolarisAttributeValue.EnumValue(FontWeight);
        attributes["interestFor"] = InterestFor;
        attributes["dir"] = Dir is null ? null : PolarisAttributeValue.EnumValue(Dir.Value);
        attributes["accessibilityVisibility"] =
            AccessibilityVisibility is null ? null : PolarisAttributeValue.EnumValue(AccessibilityVisibility.Value);
    }
}
