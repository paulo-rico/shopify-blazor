using Microsoft.AspNetCore.Components;

namespace Polaris.Blazor.Components;

/// <summary>Wraps <c>&lt;s-progress&gt;</c>.</summary>
/// <remarks>
/// <para>
/// The bar is indeterminate while <see cref="Value"/> is unset, so leaving it null is the correct
/// way to express "working, end unknown". A measurable task should set both it and
/// <see cref="Max"/>.
/// </para>
/// <para>
/// The component renders no text and no percentage, so the label has to come from the surrounding
/// markup, and assistive technology needs <c>AccessibilityLabel</c>.
/// </para>
/// </remarks>
public sealed class PolarisProgress : PolarisElementComponentBase
{
    /// <inheritdoc />
    protected override string TagName => "s-progress";

    /// <summary>
    /// How far the task has advanced, clamped by Polaris to the range <c>0..Max</c>.
    /// </summary>
    /// <remarks>Omit this for an indeterminate bar. It defaults to the element's <c>0</c>.</remarks>
    [Parameter] public double? Value { get; set; }

    /// <summary>Value at which the task is complete. Polaris falls back to <c>1</c> when it is zero or less.</summary>
    [Parameter] public double? Max { get; set; }

    /// <summary>Semantic colour.</summary>
    [Parameter] public PolarisTone Tone { get; set; } = PolarisTone.Auto;

    /// <inheritdoc />
    protected override void BuildAttributes(Dictionary<string, string?> attributes)
    {
        base.BuildAttributes(attributes);

        // Omitting `value` is what makes the bar indeterminate; writing `value="0"` would pin it to
        // an empty determinate bar instead.
        attributes["value"] = PolarisAttributeValue.Format(Value);
        attributes["max"] = PolarisAttributeValue.Format(Max);
        attributes["tone"] = PolarisAttributeValue.EnumValue(Tone);
    }
}
