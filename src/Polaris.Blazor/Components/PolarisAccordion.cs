using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Polaris.Blazor.Components;

/// <summary>
/// A set of collapsible sections where opening one collapses the one that was open.
/// </summary>
/// <remarks>
/// <para>
/// <b>Polaris has no accordion.</b> That is a measured fact, not an omission here: the element list
/// in the Polaris 1 runtime that this toolkit wraps (60 elements) contains no accordion, collapsible
/// or details component, and the App Home documentation has no such page. So this is the first
/// component in the library that is <em>composed</em> rather than a wrapper: it is built from
/// <see cref="PolarisAccordionItem"/>, which in turn is an <c>s-clickable</c> surface holding an
/// <c>s-section</c> (heading and supporting line) and an <c>s-icon</c> chevron, with an <c>s-box</c>
/// for the content. Nothing here reimplements a Polaris look; what it adds is the structure and the
/// open/closed behaviour Polaris does not provide.
/// </para>
/// <para>
/// State belongs to the items, and this container only arbitrates between them: when an item opens,
/// the others are collapsed in the same render batch, so the two animations run together instead of
/// one after the other.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;PolarisAccordion&gt;
///     &lt;PolarisAccordionItem Header="Product details" InitiallyOpen="true"&gt;
///         &lt;PolarisTextField Label="Title" /&gt;
///     &lt;/PolarisAccordionItem&gt;
///     &lt;PolarisAccordionItem Header="Shipping"&gt;
///         &lt;PolarisNumberField Label="Weight" Unit="kg" /&gt;
///     &lt;/PolarisAccordionItem&gt;
/// &lt;/PolarisAccordion&gt;
/// </code>
/// </example>
public sealed class PolarisAccordion : ComponentBase
{
    private readonly List<PolarisAccordionItem> _items = [];

    // Only meaningful while AllowMultiple is false: the first item to ask keeps the open slot, so
    // two items marked InitiallyOpen cannot leave the accordion in a state it can never render again.
    private PolarisAccordionItem? _initiallyOpenItem;

    /// <summary>The sections. Each one must be a <see cref="PolarisAccordionItem"/>.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// Lets several sections stay open at once. The default is one, which is what "accordion" means
    /// to most people: opening a section collapses the open one.
    /// </summary>
    [Parameter] public bool AllowMultiple { get; set; }

    /// <summary>
    /// Raised with the <see cref="PolarisAccordionItem.Value"/> of the section that was just opened,
    /// or <c>null</c> when the open section was closed. Use it when something outside the accordion
    /// has to follow which section is showing.
    /// </summary>
    [Parameter] public EventCallback<string?> OpenItemChanged { get; set; }

    /// <summary>Registers an item so it can be collapsed by a sibling. Called by the item itself.</summary>
    internal void Register(PolarisAccordionItem item)
    {
        if (!_items.Contains(item))
        {
            _items.Add(item);
        }
    }

    /// <summary>Forgets an item that is being disposed.</summary>
    internal void Unregister(PolarisAccordionItem item)
    {
        _items.Remove(item);

        if (ReferenceEquals(_initiallyOpenItem, item))
        {
            _initiallyOpenItem = null;
        }
    }

    /// <summary>
    /// Decides whether an item may start open. In exclusive mode the first one to ask wins and the
    /// rest start collapsed, because two open sections in an accordion that allows one is a state
    /// the next click would silently correct.
    /// </summary>
    internal bool CanOpenInitially(PolarisAccordionItem item)
    {
        if (AllowMultiple)
        {
            return true;
        }

        if (_initiallyOpenItem is not null)
        {
            return false;
        }

        _initiallyOpenItem = item;
        return true;
    }

    /// <summary>Collapses every section except <paramref name="opened"/>, when exclusivity applies.</summary>
    internal async Task CollapseOthersAsync(PolarisAccordionItem opened)
    {
        if (AllowMultiple)
        {
            return;
        }

        // Snapshot first: collapsing an item re-renders it, and a render is free to dispose an item
        // (a conditional section) while this loop is still walking the list.
        // No ConfigureAwait(false): each item ends in StateHasChanged, which must run on the
        // renderer's dispatcher.
        foreach (var item in _items.ToArray())
        {
            if (!ReferenceEquals(item, opened))
            {
                await item.SetOpenAsync(false);
            }
        }
    }

    /// <summary>Reports the new open section to <see cref="OpenItemChanged"/>.</summary>
    internal Task NotifyToggledAsync(PolarisAccordionItem item)
        => OpenItemChanged.HasDelegate
            ? OpenItemChanged.InvokeAsync(item.IsOpen ? item.Value : null)
            : Task.CompletedTask;

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        // The sections are laid out by Polaris rather than by a bare div, so an accordion sits in a
        // page exactly like any other Polaris block. gap="none" because the items draw their own
        // rules.
        builder.OpenComponent<PolarisStack>(0);
        builder.AddAttribute(1, nameof(PolarisStack.Gap), PolarisGap.None);
        builder.AddAttribute(2, nameof(PolarisStack.ChildContent), (RenderFragment)(content =>
        {
            content.OpenComponent<CascadingValue<PolarisAccordion>>(0);
            content.AddAttribute(1, nameof(CascadingValue<PolarisAccordion>.Value), this);

            // Fixed, so that a container that re-renders does not re-render every item with it: each
            // item owns its own open state and is the only thing that needs to know it changed.
            content.AddAttribute(2, nameof(CascadingValue<PolarisAccordion>.IsFixed), true);
            content.AddAttribute(3, nameof(CascadingValue<PolarisAccordion>.ChildContent), ChildContent);
            content.CloseComponent();
        }));
        builder.CloseComponent();
    }
}
