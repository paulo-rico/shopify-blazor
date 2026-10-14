using Microsoft.AspNetCore.Components;

namespace Polaris.Blazor.Components;

/// <summary>Wraps <c>&lt;s-heading&gt;</c>.</summary>
/// <remarks>
/// The heading level follows nesting depth and cannot be set. Use
/// <see cref="AccessibilityRole"/> of <c>none</c> to strip the heading semantics while keeping the
/// appearance.
/// </remarks>
public sealed class PolarisHeading : PolarisContentComponentBase
{
    /// <inheritdoc />
    protected override string TagName => "s-heading";

    /// <summary>Font size.</summary>
    [Parameter] public PolarisFontSize FontSize { get; set; } = PolarisFontSize.Auto;

    /// <summary>Lines after which the heading is truncated visually.</summary>
    [Parameter] public int? LineClamp { get; set; }

    /// <summary>Set to <c>none</c> to remove the heading semantics.</summary>
    [Parameter] public string? AccessibilityRole { get; set; }

    /// <summary>How assistive technology sees the heading.</summary>
    [Parameter] public PolarisAccessibilityVisibility? AccessibilityVisibility { get; set; }

    /// <inheritdoc />
    protected override void BuildAttributes(Dictionary<string, string?> attributes)
    {
        base.BuildAttributes(attributes);

        attributes["fontSize"] = PolarisAttributeValue.EnumValue(FontSize);
        attributes["lineClamp"] = PolarisAttributeValue.Format(LineClamp);
        attributes["accessibilityRole"] = AccessibilityRole;
        attributes["accessibilityVisibility"] =
            AccessibilityVisibility is null ? null : PolarisAttributeValue.EnumValue(AccessibilityVisibility.Value);
    }
}
