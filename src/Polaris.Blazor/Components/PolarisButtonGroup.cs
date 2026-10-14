using Microsoft.AspNetCore.Components;
namespace Polaris.Blazor.Components;

/// <summary>Wraps <c>&lt;s-button-group&gt;</c>.</summary>
/// <remarks>
/// Put the buttons inside and mark them with the slot they belong to:
/// <c>&lt;PolarisButton Slot="primary-action"&gt;</c> and
/// <c>&lt;PolarisButton Slot="secondary-actions"&gt;</c>. Polaris validates slot children, so the
/// slot has to be on the button itself.
/// </remarks>
public sealed class PolarisButtonGroup : PolarisSlottedComponentBase
{
    /// <inheritdoc />
    protected override string TagName => "s-button-group";

    /// <summary>Gap between the buttons.</summary>
    [Parameter] public PolarisButtonGroupGap Gap { get; set; } = PolarisButtonGroupGap.Base;

    /// <inheritdoc />
    protected override void BuildAttributes(Dictionary<string, string?> attributes)
    {
        base.BuildAttributes(attributes);

        attributes["gap"] = PolarisAttributeValue.EnumValue(Gap);
    }
}

/// <summary>Gap between buttons in an <c>s-button-group</c>.</summary>
public enum PolarisButtonGroupGap
{
    /// <summary>The default spacing.</summary>
    [PolarisValue("base")]
    Base,

    /// <summary>No gap, which forbids the primary-action slot.</summary>
    [PolarisValue("none")]
    None,
}
