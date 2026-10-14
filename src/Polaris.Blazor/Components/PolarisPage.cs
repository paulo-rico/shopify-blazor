using Microsoft.AspNetCore.Components;

namespace Polaris.Blazor.Components;

/// <summary>Wraps <c>&lt;s-page&gt;</c>, the top-level container for a screen.</summary>
public sealed class PolarisPage : PolarisSlottedComponentBase
{
    /// <inheritdoc />
    protected override string TagName => "s-page";

    /// <summary>The page title. Polaris supplies the heading semantics; do not add your own h1.</summary>
    [Parameter] public string? Heading { get; set; }

    /// <summary>Page width. The aside slot renders only at <c>base</c>.</summary>
    [Parameter] public PolarisPageInlineSize InlineSize { get; set; } = PolarisPageInlineSize.Base;

    /// <inheritdoc />
    protected override void BuildAttributes(Dictionary<string, string?> attributes)
    {
        base.BuildAttributes(attributes);

        attributes["heading"] = Heading;
        attributes["inlineSize"] = PolarisAttributeValue.EnumValue(InlineSize);
    }
}
