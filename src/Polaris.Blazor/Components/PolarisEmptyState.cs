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
    /// <remarks>
    /// The subheading is rendered into the element's <c>subheading</c> slot, inside an <c>s-text</c> — which
    /// is the markup Shopify's own examples use, and the only way a <see cref="RenderFragment"/> can carry a
    /// slot attribute (a fragment cannot have attributes of its own). Before this, the parameter was
    /// declared and never rendered, so anything passed to it disappeared without a word: an empty state that
    /// said nothing about what to do next.
    /// </remarks>
    protected override RenderFragment? BuildChildContent() => builder =>
    {
        var sequence = 0;

        if (Subheading is not null)
        {
            builder.OpenElement(sequence++, "s-text");
            builder.AddAttribute(sequence++, "slot", "subheading");
            builder.AddContent(sequence++, Subheading);
            builder.CloseElement();
        }

        if (ChildContent is not null)
        {
            builder.AddContent(sequence++, ChildContent);
        }
    };
}
