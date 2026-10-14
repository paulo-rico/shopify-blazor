using Microsoft.AspNetCore.Components;

namespace Polaris.Blazor.Components;

/// <summary>Wraps <c>&lt;s-section&gt;</c>, a labelled group inside a page.</summary>
public sealed class PolarisSection : PolarisSlottedComponentBase
{
    /// <inheritdoc />
    protected override string TagName => "s-section";

    /// <summary>Section title. Its heading level follows nesting depth automatically.</summary>
    [Parameter] public string? Heading { get; set; }

    /// <summary>Supporting text below the heading.</summary>
    [Parameter] public string? Subheading { get; set; }

    /// <summary>Padding. Use <c>None</c> for a section that wraps a table.</summary>
    [Parameter] public PolarisSectionPadding Padding { get; set; } = PolarisSectionPadding.Base;

    /// <inheritdoc />
    protected override void BuildAttributes(Dictionary<string, string?> attributes)
    {
        base.BuildAttributes(attributes);

        attributes["heading"] = Heading;
        attributes["subheading"] = Subheading;
        attributes["padding"] = PolarisAttributeValue.EnumValue(Padding);
    }
}
