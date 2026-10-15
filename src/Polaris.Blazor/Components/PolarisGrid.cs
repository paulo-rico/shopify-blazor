using Microsoft.AspNetCore.Components;

namespace Polaris.Blazor.Components;

/// <summary>Wraps <c>&lt;s-grid&gt;</c>, a matrix of rows and columns.</summary>
/// <remarks>
/// <para>
/// For a single axis of spacing use a <see cref="PolarisStack"/>: a grid only earns its complexity
/// when content genuinely spans tracks or must reflow by column count. There is no subgrid support,
/// so a nested grid cannot align to its parent's tracks.
/// </para>
/// <para>
/// Sizes and gaps here are strings because Polaris accepts CSS units, multi-value shorthands and
/// responsive <c>@container</c> values, which no single enum can express.
/// </para>
/// </remarks>
public sealed class PolarisGrid : PolarisContentComponentBase
{
    /// <inheritdoc />
    protected override string TagName => "s-grid";

    /// <summary>Columns and their sizes, for example <c>repeat(2, 1fr)</c>.</summary>
    [Parameter] public string? GridTemplateColumns { get; set; }

    /// <summary>Rows and their sizes, for example <c>1fr auto</c>.</summary>
    [Parameter] public string? GridTemplateRows { get; set; }

    /// <summary>
    /// Gap on both axes: one value, or a two-value shorthand.
    /// </summary>
    /// <remarks>
    /// Shopify's own prose and its type definition disagree about which axis the pair sets, so treat
    /// <see cref="RowGap"/> and <see cref="ColumnGap"/> as the unambiguous spelling.
    /// </remarks>
    [Parameter] public string? Gap { get; set; }

    /// <summary>Gap on the block axis, overriding <see cref="Gap"/> there.</summary>
    [Parameter] public string? RowGap { get; set; }

    /// <summary>Gap on the inline axis, overriding <see cref="Gap"/> there.</summary>
    [Parameter] public string? ColumnGap { get; set; }

    /// <summary>Inline-axis alignment of the tracks themselves.</summary>
    [Parameter] public string? JustifyContent { get; set; }

    /// <summary>Block-axis alignment of the tracks themselves.</summary>
    [Parameter] public string? AlignContent { get; set; }

    /// <summary>Inline-axis alignment of the items within their tracks.</summary>
    [Parameter] public string? JustifyItems { get; set; }

    /// <summary>Block-axis alignment of the items within their tracks.</summary>
    [Parameter] public string? AlignItems { get; set; }

    /// <summary>Shorthand setting both alignment axes for the tracks.</summary>
    [Parameter] public string? PlaceContent { get; set; }

    /// <summary>Shorthand setting both alignment axes for the items.</summary>
    [Parameter] public string? PlaceItems { get; set; }

    /// <summary>ARIA role of the grid.</summary>
    [Parameter] public string? AccessibilityRole { get; set; }

    /// <summary>How assistive technology and sighted users see the grid.</summary>
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

        attributes["gridTemplateColumns"] = GridTemplateColumns;
        attributes["gridTemplateRows"] = GridTemplateRows;
        attributes["gap"] = Gap;
        attributes["rowGap"] = RowGap;
        attributes["columnGap"] = ColumnGap;
        attributes["justifyContent"] = JustifyContent;
        attributes["alignContent"] = AlignContent;
        attributes["justifyItems"] = JustifyItems;
        attributes["alignItems"] = AlignItems;
        attributes["placeContent"] = PlaceContent;
        attributes["placeItems"] = PlaceItems;
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
