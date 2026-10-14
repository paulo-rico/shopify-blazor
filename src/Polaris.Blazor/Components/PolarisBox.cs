using Microsoft.AspNetCore.Components;

namespace Polaris.Blazor.Components;

/// <summary>
/// Wraps <c>&lt;s-box&gt;</c>, the generic styling container.
/// </summary>
/// <remarks>
/// Reach for this only when no other layout component expresses the intent. Polaris components
/// cannot be restyled with your own CSS, so a box plus custom CSS will not do what it looks like
/// it should.
/// </remarks>
public sealed class PolarisBox : PolarisContentComponentBase
{
    /// <inheritdoc />
    protected override string TagName => "s-box";

    /// <summary>Background treatment, for example <c>subdued</c> or <c>transparent</c>.</summary>
    [Parameter] public string? Background { get; set; }

    /// <summary>Padding on all sides.</summary>
    [Parameter] public string? Padding { get; set; }

    /// <summary>Block-axis padding, overriding <see cref="Padding"/>.</summary>
    [Parameter] public string? PaddingBlock { get; set; }

    /// <summary>Inline-axis padding, overriding <see cref="Padding"/>.</summary>
    [Parameter] public string? PaddingInline { get; set; }

    /// <summary>Border treatment, for example <c>base</c>.</summary>
    [Parameter] public string? Border { get; set; }

    /// <summary>Border style, for example <c>dashed</c>.</summary>
    [Parameter] public string? BorderStyle { get; set; }

    /// <summary>Border radius, for example <c>base</c>.</summary>
    [Parameter] public string? BorderRadius { get; set; }

    /// <summary>Overflow behaviour: <c>visible</c> or <c>hidden</c>.</summary>
    [Parameter] public string? Overflow { get; set; }

    /// <summary>Display: <c>auto</c> or <c>none</c>.</summary>
    [Parameter] public string? Display { get; set; }

    /// <summary>Block size, including the responsive <c>@container (...)</c> form.</summary>
    [Parameter] public string? BlockSize { get; set; }

    /// <summary>Inline size.</summary>
    [Parameter] public string? InlineSize { get; set; }

    /// <summary>ARIA role of the container.</summary>
    [Parameter] public string? AccessibilityRole { get; set; }

    /// <inheritdoc />
    protected override void BuildAttributes(Dictionary<string, string?> attributes)
    {
        base.BuildAttributes(attributes);

        attributes["background"] = Background;
        attributes["padding"] = Padding;
        attributes["paddingBlock"] = PaddingBlock;
        attributes["paddingInline"] = PaddingInline;
        attributes["border"] = Border;
        attributes["borderStyle"] = BorderStyle;
        attributes["borderRadius"] = BorderRadius;
        attributes["overflow"] = Overflow;
        attributes["display"] = Display;
        attributes["blockSize"] = BlockSize;
        attributes["inlineSize"] = InlineSize;
        attributes["accessibilityRole"] = AccessibilityRole;
    }
}
