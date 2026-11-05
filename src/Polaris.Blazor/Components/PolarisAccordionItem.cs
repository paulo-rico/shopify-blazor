using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

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
/// and opened with Enter or Space. Inside it, a <see cref="PolarisSection"/> holds the heading, with an
/// optional <see cref="HeaderStatus"/> badge in the section's accessory slot — beside the title rather
/// than out at the edge — and an optional <see cref="HeaderMedia"/> laid to the left of it and centred
/// against both of its lines. The chevron sits at the far end of the surface.
/// </para>
/// <para>
/// Three things about that header are not what the markup suggests, and each is forced by Polaris:
/// the border and background are on the surface, because <c>s-section</c> takes no styling properties;
/// the accessible name is set explicitly, because a heading given as an attribute lives in Polaris's
/// shadow root and leaves the surface with no text; and <c>aria-expanded</c> is written onto the shadow
/// button, because <c>s-clickable</c> has no property for the state.
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

    /// <summary>
    /// Content shown to the left of <see cref="Header"/> and <see cref="HeaderDetails"/>, standing
    /// beside both of them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A picture, in practice — a <see cref="PolarisThumbnail"/> of the thing the section is about. The
    /// header is read as "which one is this", and an image answers that before the words do, which is
    /// why it goes here rather than as the first thing inside the panel: a collapsed section still
    /// shows it.
    /// </para>
    /// <para>
    /// It is laid out in a row with the heading and the supporting line and centred against the pair,
    /// so it stands beside <em>both</em> lines rather than level with the heading alone. It carries no
    /// accessible name of its own: the header's accessible name is built from the text parameters, and
    /// a picture that repeats them is noise — pass <c>alt=""</c> on a
    /// <see cref="PolarisThumbnail"/> that only illustrates the title beside it.
    /// </para>
    /// </remarks>
    [Parameter] public RenderFragment? HeaderMedia { get; set; }

    /// <summary>
    /// A short state badge drawn beside the heading, on the heading's own line — visible while the
    /// section is closed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For the one thing a collapsed section often has to say before it is opened: that the work it
    /// offers has already been done, on some or all of what it acts on. <see cref="HeaderDetails"/>
    /// cannot carry it, because an attribute is one plain string and Polaris styles the whole
    /// subheading subdued — there is no way to give part of that line a tone.
    /// </para>
    /// <para>
    /// It is a string plus a tone rather than a <see cref="RenderFragment"/>, so its text joins the
    /// header's accessible name without the caller having to repeat it. Content drawn as markup would be
    /// projected into Polaris's shadow root and absent from the name built for
    /// <see cref="PolarisClickable.AccessibilityLabel"/>, leaving the state announced to nobody.
    /// </para>
    /// <para>
    /// It goes into the section's <c>accessory</c> slot, which is where Polaris renders content inline
    /// after the heading text. As a sibling of the section it would be pushed to the far edge of the
    /// surface instead, reading as a second status competing with the title rather than qualifying it.
    /// </para>
    /// </remarks>
    [Parameter] public string? HeaderStatus { get; set; }

    /// <summary>Semantic colour of <see cref="HeaderStatus"/>. Ignored when that is not set.</summary>
    [Parameter] public PolarisTone HeaderStatusTone { get; set; } = PolarisTone.Auto;

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

    // MouseEventArgs, not PolarisEventArgs: this is a Blazor DOM handler on our own element, not an
    // event forwarded from a Polaris custom element.
    private Task ToggleFromHeaderAsync(Microsoft.AspNetCore.Components.Web.MouseEventArgs args) => ToggleAsync();

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

        // The whole header is the click target, and the handler is Blazor's own onclick rather than an
        // interop listener on the Polaris element.
        //
        // That is not a style choice. A listener attached to an s-clickable is attached, detached and
        // re-attached on every render of this component, because the base class refreshes its
        // registrations after each one — and every toggle re-renders the page that binds
        // OpenItemChanged. Whether a given click lands while the listener is in place therefore depended
        // on render history, which is why it was intermittent with no pattern. A click is also a
        // *composed* event, so one anywhere in this surface — the heading text, the chevron, the padding
        // — bubbles to here, including the click a keyboard Enter or Space produces on the button inside
        // Polaris's shadow root. One handler, installed once by Blazor's own delegation.
        builder.OpenElement(4, "div");
        builder.AddAttribute(5, "data-polaris-accordion-header", true);
        builder.AddAttribute(6, "onclick", EventCallback.Factory.Create<Microsoft.AspNetCore.Components.Web.MouseEventArgs>(this, ToggleFromHeaderAsync));

        // The section's border and background are on the SURFACE, not on the s-section: measured from
        // the docs, s-section takes only heading, subheading, padding and accessibilityLabel, and it is
        // not one of the elements that inherit Polaris's box styling set. Attributes it does not know
        // are ignored without a word.
        //
        // No border and no radius of its own: the accordion draws one rounded box around every section,
        // and a header with its own radius inside it is a stack of separate cards rather than a list.
        builder.OpenComponent<PolarisClickable>(10);
        builder.AddAttribute(11, nameof(PolarisClickable.AccessibilityLabel), AccessibleName);
        builder.AddAttribute(12, nameof(PolarisClickable.AccessibilityExpanded),
            _isOpen ? PolarisAccessibilityExpanded.Expanded : PolarisAccessibilityExpanded.Collapsed);
        builder.AddAttribute(13, nameof(PolarisClickable.Background), "subdued");
        builder.AddAttribute(14, nameof(PolarisClickable.Padding), "base");
        builder.AddAttribute(15, nameof(PolarisClickable.ChildContent), (RenderFragment)BuildHeaderContent);
        builder.CloseComponent();

        builder.CloseElement();

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
    /// announced with the heading instead of being skipped — and the status badge is quoted for the
    /// same reason: it is the one part of the header a merchant must hear without opening the section.
    /// </remarks>
    private string? AccessibleName
    {
        get
        {
            var parts = new List<string>(3);

            foreach (var part in new[] { Header, HeaderDetails, HeaderStatus })
            {
                // A trailing full stop is a display habit, not part of the name: a supporting line written
                // as a sentence would otherwise be announced as "…and alloy.. Alloy weights on 2 of 6
                // variants", with the join's own stop after the sentence's.
                var text = part?.Trim().TrimEnd('.');

                if (!string.IsNullOrWhiteSpace(text))
                {
                    parts.Add(text);
                }
            }

            return parts.Count == 0 ? null : string.Join(". ", parts);
        }
    }

    private void BuildHeaderContent(RenderTreeBuilder builder)
    {
        // A row, so the text sits at the start and the chevron at the end. The section is a block
        // element sized to its content, and space-between puts the two at opposite edges of a surface
        // that is already full width. The chevron belongs in here, inside the surface's padding, so
        // that the whole header — including the padding behind it — is one hover and one click.
        builder.OpenComponent<PolarisStack>(0);
        builder.AddAttribute(1, nameof(PolarisStack.Direction), PolarisStackDirection.Inline);
        builder.AddAttribute(2, nameof(PolarisStack.Gap), PolarisGap.Base);
        builder.AddAttribute(3, nameof(PolarisStack.JustifyContent), "space-between");
        builder.AddAttribute(4, nameof(PolarisStack.AlignItems), "center");
        builder.AddAttribute(5, nameof(PolarisStack.ChildContent), (RenderFragment)(content =>
        {
            if (HeaderMedia is null)
            {
                BuildHeading(content);
            }
            else
            {
                // The picture and the text are ONE child of the outer row, so that space-between still
                // puts the chevron at the surface's far edge: with three children it would centre the
                // heading between the picture and the chevron instead. Centring the group's own items is
                // what makes a 100px picture stand beside BOTH lines — the heading and the supporting
                // line under it — rather than level with the heading alone.
                content.OpenComponent<PolarisStack>(10);
                content.AddAttribute(11, nameof(PolarisStack.Direction), PolarisStackDirection.Inline);
                content.AddAttribute(12, nameof(PolarisStack.Gap), PolarisGap.Base);
                content.AddAttribute(13, nameof(PolarisStack.AlignItems), "center");
                content.AddAttribute(14, nameof(PolarisStack.ChildContent), (RenderFragment)(group =>
                {
                    // 10, then 20 upwards in BuildHeading and 30 upwards in BuildHeaderStatus: sequence
                    // numbers must not go backwards inside one scope, or the renderer throws while
                    // removing the old frame.
                    group.AddContent(10, HeaderMedia);
                    BuildHeading(group);
                }));
                content.CloseComponent();
            }

            // The visible affordance for the state. It carries no meaning of its own — aria-expanded on
            // the button is what announces it — and the stylesheet rotates it from the item's data-open.
            content.OpenComponent<PolarisIcon>(40);
            content.AddAttribute(41, nameof(PolarisIcon.Type), "chevron-down");
            content.AddAttribute(42, nameof(PolarisIcon.Size), PolarisIconSize.Small);
            content.AddAttribute(43, nameof(PolarisIcon.Color), PolarisColor.Subdued);
            content.AddAttribute(44, nameof(PolarisIcon.AdditionalAttributes),
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["data-polaris-accordion-chevron"] = true,
                });
            content.CloseComponent();
        }));
        builder.CloseComponent();
    }

    /// <summary>Whether a <see cref="HeaderStatus"/> badge is drawn at all.</summary>
    private bool HasStatus => !string.IsNullOrWhiteSpace(HeaderStatus);

    /// <summary>The heading, its supporting line, and its state badge.</summary>
    private void BuildHeading(RenderTreeBuilder builder)
    {
        builder.OpenComponent<PolarisSection>(20);
        builder.AddAttribute(21, nameof(PolarisSection.Heading), Header);
        builder.AddAttribute(22, nameof(PolarisSection.Subheading), HeaderDetails);

        // None: the surface around it already pads, and two paddings make a header that is mostly
        // space.
        builder.AddAttribute(23, nameof(PolarisSection.Padding), PolarisSectionPadding.None);

        if (HasStatus)
        {
            builder.AddAttribute(24, nameof(PolarisSection.ChildContent),
                (RenderFragment)BuildHeaderStatus);
        }

        builder.CloseComponent();
    }

    /// <summary>The state badge, in the section's accessory slot.</summary>
    /// <remarks>
    /// <para>
    /// The accessory slot is the only place Polaris puts content "inline beside the heading text", and
    /// that is the whole point of the badge: next to the title it qualifies, not out at the far end of
    /// the header where it reads as a second, competing status. Rendering it as a sibling of the
    /// section cannot do that — <c>s-section</c> is a block that fills the width it is given, so the
    /// badge is pushed to the far edge of the surface instead of following the words.
    /// </para>
    /// <para>
    /// A badge rather than a paragraph of our own: <c>s-badge</c> is Polaris's own treatment for a short
    /// state, it takes a tone, and — unlike a styled subscript — it still reads as a badge if the page's
    /// own stylesheet never loads. Polaris warns if an accessory badge is not <c>size="base"</c> and
    /// <c>color="base"</c>; both are <see cref="PolarisBadge"/>'s defaults, and the tone is what carries
    /// the meaning.
    /// </para>
    /// </remarks>
    private void BuildHeaderStatus(RenderTreeBuilder builder)
    {
        builder.OpenComponent<PolarisBadge>(30);
        builder.AddAttribute(31, nameof(PolarisBadge.Slot), "accessory");
        builder.AddAttribute(32, nameof(PolarisBadge.Tone), HeaderStatusTone);
        builder.AddAttribute(33, nameof(PolarisBadge.ChildContent),
            (RenderFragment)(badge => badge.AddContent(0, HeaderStatus)));
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
