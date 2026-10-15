using Microsoft.AspNetCore.Components;

namespace Polaris.Blazor.Components;

/// <summary>Wraps <c>&lt;s-choice&gt;</c>. Only valid inside a <see cref="PolarisChoiceList"/>.</summary>
/// <remarks>
/// The visible label is the child content. Use the <c>details</c> and <c>secondary-content</c> slots
/// for supporting text and for trailing content such as a badge.
/// </remarks>
public sealed class PolarisChoice : PolarisContentComponentBase
{
    /// <inheritdoc />
    protected override string TagName => "s-choice";

    /// <summary>The value reported in <c>args.Values</c> when this choice is selected.</summary>
    [Parameter] public string? Value { get; set; }

    /// <summary>Marks the choice as selected. Not reflected, so it is set as a property.</summary>
    [Parameter] public bool Selected { get; set; }

    /// <summary>Marks the choice as selected on first render only.</summary>
    [Parameter] public bool DefaultSelected { get; set; }

    /// <summary>Prevents choosing this option.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <inheritdoc />
    protected override void BuildAttributes(Dictionary<string, string?> attributes)
    {
        base.BuildAttributes(attributes);

        attributes["value"] = Value;
        attributes["disabled"] = PolarisAttributeValue.Format(Disabled);
        attributes["defaultSelected"] = PolarisAttributeValue.Format(DefaultSelected);
    }

    /// <inheritdoc />
    protected override IReadOnlyDictionary<string, object?> Properties => new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        ["selected"] = Selected,
    };
}
