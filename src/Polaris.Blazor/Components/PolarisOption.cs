using Microsoft.AspNetCore.Components;
namespace Polaris.Blazor.Components;

/// <summary>Wraps <c>&lt;s-option&gt;</c>. Only valid inside a <see cref="PolarisSelect"/>.</summary>
public sealed class PolarisOption : PolarisContentComponentBase
{
    /// <inheritdoc />
    protected override string TagName => "s-option";

    /// <summary>The value submitted when this option is chosen.</summary>
    [Parameter] public string? Value { get; set; }

    /// <summary>Marks the option as selected. Not reflected, so it is set as a property.</summary>
    [Parameter] public bool Selected { get; set; }

    /// <summary>Marks the option as selected on first render only.</summary>
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
