using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Polaris.Blazor;

/// <summary>
/// Base class for a wrapper whose element accepts children in Shopify's named slots.
/// </summary>
/// <remarks>
/// Only the default slot is exposed as a fragment. Named slots are set with the <c>Slot</c>
/// parameter on the child component instead — <c>&lt;PolarisButton Slot="primary-action"&gt;</c> —
/// because Polaris validates slot children and rejects a wrapper element, and a
/// <see cref="RenderFragment"/> cannot carry an attribute. See
/// <see cref="PolarisElementComponentBase.Slot"/>.
/// </remarks>
public abstract class PolarisSlottedComponentBase : PolarisContentComponentBase
{
    /// <summary>
    /// Content rendered before the element's main content, in the <c>supplemental-start</c> slot.
    /// </summary>
    /// <remarks>
    /// This one slot is offered as a fragment because its content is not validated: it takes any
    /// element, which is why a banner or an informational block belongs there.
    /// </remarks>
    [Parameter]
    public RenderFragment? SupplementalStart { get; set; }

    /// <summary>Content for the <c>aside</c> column, where the element has one.</summary>
    /// <remarks>Not validated, so a fragment is safe.</remarks>
    [Parameter]
    public RenderFragment? Aside { get; set; }

    /// <inheritdoc />
    protected override RenderFragment? BuildChildContent() => builder =>
    {
        AddUnvalidatedSlot(builder, 0, "supplemental-start", SupplementalStart);

        if (ChildContent is not null)
        {
            builder.AddContent(100, ChildContent);
        }

        AddUnvalidatedSlot(builder, 200, "aside", Aside);
    };

    /// <summary>
    /// Adds a slot fragment wrapped in a plain element.
    /// </summary>
    /// <remarks>
    /// Only for slots Shopify does not type-check — <c>supplemental-start</c> and <c>aside</c>.
    /// A validated slot such as <c>primary-action</c> must be set from the child with
    /// <see cref="PolarisElementComponentBase.Slot"/>, because a wrapper element is rejected.
    /// </remarks>
    /// <param name="builder">The render tree being built.</param>
    /// <param name="openSequence">Sequence number for the wrapper element.</param>
    /// <param name="slot">The slot name to set.</param>
    /// <param name="fragment">The fragment to place in the slot.</param>
    protected static void AddUnvalidatedSlot(RenderTreeBuilder builder, int openSequence, string slot, RenderFragment? fragment)
    {
        if (fragment is null)
        {
            return;
        }

        // A helper cannot use literal sequence numbers, so the analyzer's literal rule is
        // suppressed here rather than repeating this block at both call sites. Only the
        // open/content frames need sequences that increase together.
#pragma warning disable ASP0006
        builder.OpenElement(openSequence, "div");
        builder.AddAttribute(openSequence + 1, "slot", slot);
        builder.AddContent(openSequence + 2, fragment);
        builder.CloseElement();
#pragma warning restore ASP0006
    }
}
