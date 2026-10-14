using Microsoft.AspNetCore.Components;

namespace Polaris.Blazor.Components;

/// <summary>Wraps <c>&lt;s-table-header&gt;</c>.</summary>
public sealed class PolarisTableHeader : PolarisContentComponentBase
{
    /// <inheritdoc />
    protected override string TagName => "s-table-header";

    /// <summary>Alignment of the column and of the cells beneath it.</summary>
    [Parameter] public PolarisTableHeaderFormat Format { get; set; } = PolarisTableHeaderFormat.Base;

    /// <summary>
    /// How this column appears when the table collapses to a list on a narrow screen.
    /// </summary>
    /// <remarks>
    /// Mark the most identifying column, usually the name, <c>Primary</c>; otherwise the collapsed
    /// row has no headline and every value renders under its own label.
    /// </remarks>
    [Parameter] public PolarisTableListSlot ListSlot { get; set; } = PolarisTableListSlot.Labeled;

    /// <inheritdoc />
    protected override void BuildAttributes(Dictionary<string, string?> attributes)
    {
        base.BuildAttributes(attributes);

        attributes["format"] = PolarisAttributeValue.EnumValue(Format);
        attributes["listSlot"] = PolarisAttributeValue.EnumValue(ListSlot);
    }
}
