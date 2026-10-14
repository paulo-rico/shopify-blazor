using Microsoft.AspNetCore.Components;

namespace Polaris.Blazor.Components;

/// <summary>Wraps <c>&lt;s-text&gt;</c>, and with <c>Paragraph</c> set, <c>&lt;s-paragraph&gt;</c>.</summary>
public sealed class PolarisText : PolarisContentComponentBase
{
    /// <inheritdoc />
    protected override string TagName => Paragraph ? "s-paragraph" : "s-text";

    /// <summary>
    /// Renders a block-level paragraph instead of inline text.
    /// </summary>
    /// <remarks>
    /// Paragraphs carry their own vertical spacing, which can add unwanted gaps inside a tight
    /// layout; inline text has none.
    /// </remarks>
    [Parameter] public bool Paragraph { get; set; }

    /// <summary>Text type. Only meaningful for inline text.</summary>
    [Parameter] public PolarisTextType Type { get; set; } = PolarisTextType.Generic;

    /// <summary>Semantic colour.</summary>
    [Parameter] public PolarisTone Tone { get; set; } = PolarisTone.Auto;

    /// <summary>Standard or subdued.</summary>
    [Parameter] public PolarisColor Color { get; set; } = PolarisColor.Base;

    /// <summary>Font size.</summary>
    [Parameter] public PolarisFontSize FontSize { get; set; } = PolarisFontSize.Auto;

    /// <summary>Font weight.</summary>
    [Parameter] public PolarisFontWeight FontWeight { get; set; } = PolarisFontWeight.Auto;

    /// <summary>Lines after which the text is truncated visually.</summary>
    [Parameter] public int? LineClamp { get; set; }

    /// <summary>Id of a tooltip or popover shown on hover.</summary>
    [Parameter] public string? InterestFor { get; set; }

    /// <inheritdoc />
    protected override void BuildAttributes(Dictionary<string, string?> attributes)
    {
        base.BuildAttributes(attributes);

        // `type` and `interestFor` exist on s-text but not on s-paragraph.
        if (!Paragraph)
        {
            attributes["type"] = PolarisAttributeValue.EnumValue(Type);
            attributes["interestFor"] = InterestFor;
        }

        attributes["tone"] = PolarisAttributeValue.EnumValue(Tone);
        attributes["color"] = PolarisAttributeValue.EnumValue(Color);
        attributes["fontSize"] = PolarisAttributeValue.EnumValue(FontSize);
        attributes["fontWeight"] = PolarisAttributeValue.EnumValue(FontWeight);
        attributes["lineClamp"] = PolarisAttributeValue.Format(LineClamp);
    }
}
