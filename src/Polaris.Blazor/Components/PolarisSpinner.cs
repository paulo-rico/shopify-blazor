using Microsoft.AspNetCore.Components;
namespace Polaris.Blazor.Components;

/// <summary>Wraps <c>&lt;s-spinner&gt;</c>. Indeterminate and spins until removed.</summary>
/// <remarks>
/// For a button's loading state use <see cref="PolarisButton.Loading"/> instead. For progress with
/// a measurable end, use a progress component.
/// </remarks>
public sealed class PolarisSpinner : PolarisElementComponentBase
{
    /// <inheritdoc />
    protected override string TagName => "s-spinner";

    /// <summary>Spinner size.</summary>
    [Parameter] public PolarisSpinnerSize Size { get; set; } = PolarisSpinnerSize.Base;

    /// <inheritdoc />
    protected override void BuildAttributes(Dictionary<string, string?> attributes)
    {
        base.BuildAttributes(attributes);

        attributes["size"] = PolarisAttributeValue.EnumValue(Size);
    }
}
