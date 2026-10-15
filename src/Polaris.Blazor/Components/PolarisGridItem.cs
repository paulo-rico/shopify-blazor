using Microsoft.AspNetCore.Components;

namespace Polaris.Blazor.Components;

/// <summary>Wraps <c>&lt;s-grid-item&gt;</c>, one cell of a <see cref="PolarisGrid"/>.</summary>
/// <remarks>
/// Only valid inside a grid. It carries no gap or alignment properties: those belong to the grid.
/// </remarks>
public sealed class PolarisGridItem : PolarisContentComponentBase
{
    /// <inheritdoc />
    protected override string TagName => "s-grid-item";

    /// <summary>How many columns the item spans: <c>auto</c>, or for example <c>span 2</c>.</summary>
    [Parameter] public string? GridColumn { get; set; }

    /// <summary>How many rows the item spans: <c>auto</c>, or for example <c>span 2</c>.</summary>
    [Parameter] public string? GridRow { get; set; }

    /// <summary>ARIA role of the cell.</summary>
    [Parameter] public string? AccessibilityRole { get; set; }

    /// <summary>How assistive technology and sighted users see the cell.</summary>
    [Parameter] public PolarisAccessibilityVisibility? AccessibilityVisibility { get; set; }

    /// <summary>Background treatment.</summary>
    [Parameter] public string? Background { get; set; }

    /// <summary>Padding on all edges, in the 1-to-4-value shorthand.</summary>
    [Parameter] public string? Padding { get; set; }

    /// <summary>Block-axis padding, overriding <see cref="Padding"/>.</summary>
    [Parameter] public string? PaddingBlock { get; set; }

    /// <summary>Block-start padding, overriding <see cref="PaddingBlock"/>.</summary>
    [Parameter] public string? PaddingBlockStart { get; set; }

    /// <summary>Block-end padding, overriding <see cref="PaddingBlock"/>.</summary>
    [Parameter] public string? PaddingBlockEnd { get; set; }

    /// <summary>Inline-axis padding, overriding <see cref="Padding"/>.</summary>
    [Parameter] public string? PaddingInline { get; set; }

    /// <summary>Inline-start padding, overriding <see cref="PaddingInline"/>.</summary>
    [Parameter] public string? PaddingInlineStart { get; set; }

    /// <summary>Inline-end padding, overriding <see cref="PaddingInline"/>.</summary>
    [Parameter] public string? PaddingInlineEnd { get; set; }

    /// <summary>Border shorthand: width, then colour, then style.</summary>
    [Parameter] public string? Border { get; set; }

    /// <summary>Border width, overriding <see cref="Border"/>.</summary>
    [Parameter] public string? BorderWidth { get; set; }

    /// <summary>Border style, overriding <see cref="Border"/>.</summary>
    [Parameter] public string? BorderStyle { get; set; }

    /// <summary>Border colour, overriding <see cref="Border"/>.</summary>
    [Parameter] public string? BorderColor { get; set; }

    /// <summary>Corner radius, in the 1-to-4-value shorthand.</summary>
    [Parameter] public string? BorderRadius { get; set; }

    /// <summary>Block size.</summary>
    [Parameter] public string? BlockSize { get; set; }

    /// <summary>Minimum block size.</summary>
    [Parameter] public string? MinBlockSize { get; set; }

    /// <summary>Maximum block size.</summary>
    [Parameter] public string? MaxBlockSize { get; set; }

    /// <summary>Inline size.</summary>
    [Parameter] public string? InlineSize { get; set; }

    /// <summary>Minimum inline size.</summary>
    [Parameter] public string? MinInlineSize { get; set; }

    /// <summary>Maximum inline size.</summary>
    [Parameter] public string? MaxInlineSize { get; set; }

    /// <summary>Overflow behaviour: <c>visible</c> or <c>hidden</c>.</summary>
    [Parameter] public string? Overflow { get; set; }

    /// <summary>Display: <c>auto</c> or <c>none</c>.</summary>
    [Parameter] public string? Display { get; set; }

    /// <inheritdoc />
    protected override void BuildAttributes(Dictionary<string, string?> attributes)
    {
        base.BuildAttributes(attributes);

        attributes["gridColumn"] = GridColumn;
        attributes["gridRow"] = GridRow;
        attributes["accessibilityRole"] = AccessibilityRole;
        attributes["accessibilityVisibility"] =
            AccessibilityVisibility is null ? null : PolarisAttributeValue.EnumValue(AccessibilityVisibility.Value);
        attributes["background"] = Background;
        attributes["padding"] = Padding;
        attributes["paddingBlock"] = PaddingBlock;
        attributes["paddingBlockStart"] = PaddingBlockStart;
        attributes["paddingBlockEnd"] = PaddingBlockEnd;
        attributes["paddingInline"] = PaddingInline;
        attributes["paddingInlineStart"] = PaddingInlineStart;
        attributes["paddingInlineEnd"] = PaddingInlineEnd;
        attributes["border"] = Border;
        attributes["borderWidth"] = BorderWidth;
        attributes["borderStyle"] = BorderStyle;
        attributes["borderColor"] = BorderColor;
        attributes["borderRadius"] = BorderRadius;
        attributes["blockSize"] = BlockSize;
        attributes["minBlockSize"] = MinBlockSize;
        attributes["maxBlockSize"] = MaxBlockSize;
        attributes["inlineSize"] = InlineSize;
        attributes["minInlineSize"] = MinInlineSize;
        attributes["maxInlineSize"] = MaxInlineSize;
        attributes["overflow"] = Overflow;
        attributes["display"] = Display;
    }
}
