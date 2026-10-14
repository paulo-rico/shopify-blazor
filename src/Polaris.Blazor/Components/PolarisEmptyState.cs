using Microsoft.AspNetCore.Components;

namespace Polaris.Blazor.Components;

/// <summary>
/// Wraps <c>&lt;s-empty-state&gt;</c>.
/// </summary>
/// <remarks>
/// Render this conditionally. Outside a table it cannot detect emptiness itself, so leaving it in
/// the markup unconditionally shows "no products" beside a full table.
/// </remarks>
public sealed class PolarisEmptyState : PolarisSlottedComponentBase
{
    /// <inheritdoc />
    protected override string TagName => "s-empty-state";

    /// <summary>Plain-text heading. Markup is not accepted here.</summary>
    [Parameter] public string? Heading { get; set; }

    /// <summary>Supporting text. Goes in the element's subheading slot.</summary>
    [Parameter] public RenderFragment? Subheading { get; set; }

    /// <inheritdoc />
    protected override void BuildAttributes(Dictionary<string, string?> attributes)
    {
        base.BuildAttributes(attributes);

        attributes["heading"] = Heading;
    }

    /// <inheritdoc />
    protected override RenderFragment? BuildChildContent() => ChildContent;
}
