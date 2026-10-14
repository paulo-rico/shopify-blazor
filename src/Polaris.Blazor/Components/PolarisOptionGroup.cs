using Microsoft.AspNetCore.Components;
namespace Polaris.Blazor.Components;

/// <summary>Wraps <c>&lt;s-option-group&gt;</c>. Groups options under a label.</summary>
public sealed class PolarisOptionGroup : PolarisContentComponentBase
{
    /// <inheritdoc />
    protected override string TagName => "s-option-group";

    /// <summary>Group label.</summary>
    [Parameter] public string? Label { get; set; }

    /// <summary>Disables every option in the group.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <inheritdoc />
    protected override void BuildAttributes(Dictionary<string, string?> attributes)
    {
        base.BuildAttributes(attributes);

        attributes["label"] = Label;
        attributes["disabled"] = PolarisAttributeValue.Format(Disabled);
    }
}
