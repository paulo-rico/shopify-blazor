using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Polaris.Blazor.Events;

namespace Polaris.Blazor.Components;

/// <summary>
/// One section of a <see cref="PolarisAccordion"/>: a header that opens and closes the content
/// beneath it.
/// </summary>
/// <remarks>
/// <para>
/// The header is a <see cref="PolarisClickable"/> — an interactive surface, because a heading with a
/// line of supporting text under it does not fit in a button's single truncating line. It still
/// renders a real <c>&lt;button&gt;</c> inside Polaris's shadow root, so it is focusable, announced,
/// and opened with Enter or Space. Inside it, an <see cref="PolarisSection"/> holds the heading on the
/// left and the chevron sits at the far right.
/// </para>
/// <para>
/// Three things about that header are not what the markup suggests, and each is forced by Polaris:
/// the border and background are on the surface, because <c>s-section</c> takes no styling
/// properties; the accessible name is set explicitly, because a heading given as an attribute lives in
/// Polaris's shadow root and leaves the surface with no text; and <c>aria-expanded</c> is written onto
/// the shadow button, because <c>s-clickable</c> has no property for the state.
/// </para>
/// <para>
/// The content is clipped by a plain <c>&lt;div&gt;</c> pair around it, because Polaris elements live
/// in a shadow root and cannot be restyled. The open state is written inline —
/// <c>grid-template-rows: 0fr</c> for closed, <c>1fr</c> for open — and the <em>transition</em>
/// between them lives in <c>polaris-accordion.css</c>. That split is deliberate: if the stylesheet
/// never arrives, the panel still opens and closes correctly and merely stops animating, which is
/// the only acceptable way for a stylesheet to fail.
/// </para>
/// <para>
/// A closed panel is also <c>inert</c>, so content that is clipped out of sight cannot be reached
/// with Tab or read out of order by a screen reader.
/// </para>
/// </remarks>
public sealed class PolarisAccordionItem : ComponentBase, IDisposable
{
    private const string PanelStyle = "display:grid;overflow:hidden;grid-template-rows:";
    private const string PanelInnerStyle = "min-height:0;overflow:hidden";

    private bool _isOpen;
    private bool _disposed;

    /// <summary>The accordion this section belongs to, when it is inside one.</summary>
    [CascadingParameter] private PolarisAccordion? Accordion { get; set; }

    /// <summary>The header's heading. The header's accessible name is built from it.</summary>
    [Parameter] public string? Header { get; set; }

    /// <summary>
    /// A line of supporting text under <see cref="Header"/>, for what the section is about.
    /// </summary>
    /// <remarks>
    /// It is rendered as the section's <c>subheading</c> rather than as a paragraph inside it: Polaris
    /// styles that below the heading and above the content, which is where this belongs, and keeping it
    /// an attribute leaves the header's markup to the two elements rather than to a wrapper.
    /// </remarks>
    [Parameter] public string? HeaderDetails { get; set; }

    /// <summary>The content that opens and closes.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// An identifier reported to the container's <see cref="PolarisAccordion.OpenItemChanged"/> so
    /// that code outside the accordion can tell which section is showing.
    /// </summary>
    [Parameter] public string? Value { get; set; }

    /// <summary>Starts this section open. In an exclusive accordion only the first such section keeps it.</summary>
    [Parameter] public bool InitiallyOpen { get; set; }

    /// <summary>Padding inside the content area, in Polaris's spacing scale. <c>None</c> for a table.</summary>
    [Parameter] public string? ContentPadding { get; set; } = "base";

    /// <summary>
    /// Draws a rule above the section. Every section has one by default, which reads as a rule
    /// between sections and a rule above the first.
    /// </summary>
    [Parameter] public bool ShowDivider { get; set; } = true;

    /// <summary>Raised with the new open state, whether the merchant opened it or a sibling closed it.</summary>
    [Parameter] public EventCallback<bool> IsOpenChanged { get; set; }

    /// <summary>Whether the content is currently showing.</summary>
    public bool IsOpen => _isOpen;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        // Registration is what lets one section collapse another; without a container an item still
        // opens and closes, it just cannot enforce exclusivity against a sibling.
        Accordion?.Register(this);

        if (InitiallyOpen)
        {
            _isOpen = Accordion?.CanOpenInitially(this) ?? true;
        }
    }

    /// <summary>Opens a closed section, or closes an open one.</summary>
    /// <remarks>
    /// Nothing here uses <c>ConfigureAwait(false)</c>, and that is deliberate: this method ends in
    /// <see cref="ComponentBase.StateHasChanged"/>, which asserts it is running on the renderer's
    /// dispatcher. A continuation resumed on a thread-pool thread throws
    /// "The current thread is not associated with the Dispatcher" — but only when a handler is
    /// genuinely asynchronous, so it works until the first handler awaits something.
    /// </remarks>
    public async Task ToggleAsync()
    {
        if (_isOpen)
        {
            await SetOpenAsync(false);
        }
        else
        {
            if (Accordion is not null)
            {
                // Collapsing the open section before this one opens puts both rows changes in the
                // same render batch, so the two transitions run together rather than in sequence.
                await Accordion.CollapseOthersAsync(this);
            }

            await SetOpenAsync(true);
        }

        if (Accordion is not null)
        {
            await Accordion.NotifyToggledAsync(this);
        }
    }

    /// <summary>Sets the open state without asking the container, and reports the change.</summary>
    internal async Task SetOpenAsync(bool open)
    {
        if (_isOpen == open)
        {
            return;
        }

        _isOpen = open;

        if (IsOpenChanged.HasDelegate)
        {
            await IsOpenChanged.InvokeAsync(_isOpen);
        }

        if (!_disposed)
        {
            StateHasChanged();
        }
    }

    private Task ToggleFromHeaderAsync(PolarisEventArgs args) => ToggleAsync();

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "data-polaris-accordion-item", true);

        // Kept in the markup as well as in C#: it is what the stylesheet rotates the chevron from,
        // and the stylesheet is the only thing allowed to know about motion.
        builder.AddAttribute(2, "data-open", _isOpen ? "true" : "false");

        if (ShowDivider)
        {
            builder.OpenComponent<PolarisDivider>(3);
            builder.CloseComponent();
        }

        // The header is a clickable surface rather than a button, because a button lays its children
        // out on one line and truncates them, and this header is a heading with a line of supporting
        // text under it. s-clickable renders a real <button> inside its shadow root, so it is still
        // focusable, still announced, and still opened with Enter or Space.
        //
        // The section's border and background are on the SURFACE, not on the s-section: measured from
        // the docs, s-section takes only heading, subheading, padding and accessibilityLabel, and it is
        // not one of the elements that inherit Polaris's box styling set. Attributes it does not know
        // are ignored without a word.
        builder.OpenComponent<PolarisClickable>(10);
        builder.AddAttribute(11, nameof(PolarisClickable.AccessibilityLabel), AccessibleName);
        builder.AddAttribute(12, nameof(PolarisClickable.AccessibilityExpanded),
            _isOpen ? PolarisAccessibilityExpanded.Expanded : PolarisAccessibilityExpanded.Collapsed);
        builder.AddAttribute(13, nameof(PolarisClickable.Border), "base");
        builder.AddAttribute(14, nameof(PolarisClickable.Background), "subdued");
        builder.AddAttribute(15, nameof(PolarisClickable.Padding), "base");
        builder.AddAttribute(16, nameof(PolarisClickable.BorderRadius), "base");
        builder.AddAttribute(17, nameof(PolarisClickable.OnClick),
            EventCallback.Factory.Create<PolarisEventArgs>(this, ToggleFromHeaderAsync));
        builder.AddAttribute(18, nameof(PolarisClickable.ChildContent), (RenderFragment)BuildHeaderContent);
        builder.CloseComponent();

        builder.OpenElement(20, "div");
        builder.AddAttribute(21, "data-polaris-accordion-panel", true);

        // Inline, not in the stylesheet: a panel that lost its stylesheet must still be collapsed,
        // never visible-with-no-way-to-close-it.
        builder.AddAttribute(22, "style", string.Concat(PanelStyle, _isOpen ? "1fr" : "0fr"));
        builder.OpenElement(23, "div");
        builder.AddAttribute(24, "data-polaris-accordion-panel-inner", true);
        builder.AddAttribute(25, "style", PanelInnerStyle);

        if (!_isOpen)
        {
            // Clipping hides the content but leaves it in the tab order and the accessibility tree.
            builder.AddAttribute(26, "inert", true);
        }

        builder.OpenComponent<PolarisBox>(27);
        builder.AddAttribute(28, nameof(PolarisBox.Padding), ContentPadding);
        builder.AddAttribute(29, nameof(PolarisBox.ChildContent), ChildContent);
        builder.CloseComponent();
        builder.CloseElement();
        builder.CloseElement();
        builder.CloseElement();
    }

    /// <summary>
    /// What assistive technology announces for the header.
    /// </summary>
    /// <remarks>
    /// It has to be stated rather than derived. A heading and a subheading set as attributes live in
    /// Polaris's shadow root, so this surface's own text is empty apart from the chevron, and a button
    /// with no text has no name. Quoting the visible text here also means the supporting line is
    /// announced with the heading instead of being skipped.
    /// </remarks>
    private string? AccessibleName => HeaderDetails is null or ""
        ? Header
        : string.Concat(Header ?? string.Empty, ". ", HeaderDetails);

    private void BuildHeaderContent(RenderTreeBuilder builder)
    {
        // A row, so the text sits at the start and the chevron at the end. The section is a block
        // element sized to its content, and space-between puts the two at opposite edges of a surface
        // that is already full width.
        builder.OpenComponent<PolarisStack>(0);
        builder.AddAttribute(1, nameof(PolarisStack.Direction), PolarisStackDirection.Inline);
        builder.AddAttribute(2, nameof(PolarisStack.Gap), PolarisGap.Base);
        builder.AddAttribute(3, nameof(PolarisStack.JustifyContent), "space-between");
        builder.AddAttribute(4, nameof(PolarisStack.AlignItems), "center");
        builder.AddAttribute(5, nameof(PolarisStack.ChildContent), (RenderFragment)(content =>
        {
            content.OpenComponent<PolarisSection>(0);
            content.AddAttribute(1, nameof(PolarisSection.Heading), Header);
            content.AddAttribute(2, nameof(PolarisSection.Subheading), HeaderDetails);
            // None: the surface around it already pads, and two paddings make a header that is mostly
            // space.
            content.AddAttribute(3, nameof(PolarisSection.Padding), PolarisSectionPadding.None);
            content.CloseComponent();

            // The visible affordance for the state. It carries no meaning of its own — aria-expanded on
            // the button is what announces it — and the stylesheet rotates it from the item's data-open.
            content.OpenComponent<PolarisIcon>(4);
            content.AddAttribute(5, nameof(PolarisIcon.Type), "chevron-down");
            content.AddAttribute(6, nameof(PolarisIcon.Size), PolarisIconSize.Small);
            content.AddAttribute(7, nameof(PolarisIcon.Color), PolarisColor.Subdued);
            content.AddAttribute(8, nameof(PolarisIcon.AdditionalAttributes),
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["data-polaris-accordion-chevron"] = true,
                });
            content.CloseComponent();
        }));
        builder.CloseComponent();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Accordion?.Unregister(this);
    }
}
