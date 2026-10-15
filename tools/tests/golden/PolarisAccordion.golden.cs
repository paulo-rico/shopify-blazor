using Microsoft.AspNetCore.Components;

namespace Polaris.Blazor.Components;

/// <summary>Wraps the fixture's <c>&lt;s-accordion&gt;</c>.</summary>
public sealed class PolarisAccordion : PolarisElementComponentBase
{
    /// <inheritdoc />
    protected override string TagName => "s-accordion";

    /// <summary>Panel heading.</summary>
    [Parameter] public string? Heading { get; set; }

    /// <summary>Whether the panel starts open.</summary>
    [Parameter] public bool Open { get; set; }

    /// <inheritdoc />
    protected override void BuildAttributes(Dictionary<string, string?> attributes)
    {
        base.BuildAttributes(attributes);

        attributes["heading"] = Heading;
        attributes["open"] = PolarisAttributeValue.Format(Open);
    }
}
