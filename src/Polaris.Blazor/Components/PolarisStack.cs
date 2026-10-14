using Microsoft.AspNetCore.Components;

namespace Polaris.Blazor.Components;

/// <summary>Wraps <c>&lt;s-stack&gt;</c>, the layout primitive for a single axis of spacing.</summary>
public sealed class PolarisStack : PolarisContentComponentBase
{
    /// <inheritdoc />
    protected override string TagName => "s-stack";

    /// <summary>Axis the children are laid out along. Block, meaning vertical, by default.</summary>
    [Parameter] public PolarisStackDirection Direction { get; set; } = PolarisStackDirection.Block;

    /// <summary>Gap between children on both axes.</summary>
    [Parameter] public PolarisGap Gap { get; set; } = PolarisGap.None;

    /// <summary>Gap between rows, overriding <see cref="Gap"/> on that axis.</summary>
    [Parameter] public PolarisGap? RowGap { get; set; }

    /// <summary>Gap between columns, overriding <see cref="Gap"/> on that axis.</summary>
    [Parameter] public PolarisGap? ColumnGap { get; set; }

    /// <summary>Main-axis alignment.</summary>
    [Parameter] public string? JustifyContent { get; set; }

    /// <summary>
    /// Cross-axis alignment. Use this rather than <see cref="AlignContent"/> for a single line,
    /// which is almost always what a stack is.
    /// </summary>
    [Parameter] public string? AlignItems { get; set; }

    /// <summary>Cross-axis alignment for wrapping content.</summary>
    [Parameter] public string? AlignContent { get; set; }

    /// <inheritdoc />
    protected override void BuildAttributes(Dictionary<string, string?> attributes)
    {
        base.BuildAttributes(attributes);

        attributes["direction"] = PolarisAttributeValue.EnumValue(Direction);
        attributes["gap"] = PolarisAttributeValue.EnumValue(Gap);
        attributes["rowGap"] = RowGap is null ? null : PolarisAttributeValue.EnumValue(RowGap.Value);
        attributes["columnGap"] = ColumnGap is null ? null : PolarisAttributeValue.EnumValue(ColumnGap.Value);
        attributes["justifyContent"] = JustifyContent;
        attributes["alignItems"] = AlignItems;
        attributes["alignContent"] = AlignContent;
    }
}
