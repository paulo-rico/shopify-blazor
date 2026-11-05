using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

// AddEventStopPropagationAttribute and AddEventPreventDefaultAttribute live on
// WebRenderTreeBuilderExtensions in this namespace, not on RenderTreeBuilder itself.
using Microsoft.AspNetCore.Components.Web;

namespace Polaris.Blazor.Components;

/// <summary>
/// One section of a <see cref="PolarisAccordion"/>: a header that opens and closes the content
/// beneath it.
/// </summary>
/// <remarks>
/// <para>
/// The disclosure is the browser's own <c>&lt;details&gt;</c>/<c>&lt;summary&gt;</c> pair. That is a
/// deliberate choice, and it is the opposite of what this component first did. Its first header was a
/// <see cref="PolarisClickable"/> — an <c>s-clickable</c> surface — because a heading with a supporting
/// line does not fit in a button's one truncating line. That surface was a shadow <c>&lt;button&gt;</c>,
/// and everything below followed from it: the accessible name had to be stated by hand, because the
/// heading lived in Polaris's shadow root; <c>aria-expanded</c> had to be written onto that shadow
/// button through interop, because <c>s-clickable</c> has no property for it; the panel had to be
/// clipped by hand and marked <c>inert</c>; and a header action was impossible, because a button inside
/// a button leaves the accessibility tree — which is why <see cref="HeaderActions"/> was once removed
/// again, and why the jeweller app gave up on this component and wrote its own disclosures.
/// </para>
/// <para>
/// A <c>&lt;summary&gt;</c> has none of those properties. Chrome (measured through CDP's accessibility
/// tree) exposes it as role <c>DisclosureTriangle</c>, not <c>button</c>, and a <c>&lt;button&gt;</c>
/// inside it is a separate node with its own name — so an action in the header is announced rather than
/// swallowed. The browser owns the expanded state, the keyboard (Space and Enter both toggle), focus,
/// and the fact that a closed panel is not rendered at all, and it needs no stylesheet to do any of
/// it. The cost is the one thing this file no longer gets for free: the header's appearance is ours
/// rather than Polaris's, and it lives in <c>polaris-accordion.css</c>. A missing stylesheet now
/// degrades to a plain, marker-showing summary that still works, which is the right direction for a
/// stylesheet to fail.
/// </para>
/// <para>
/// Three details of the markup are not obvious. The <c>open</c> attribute is written as a bare
/// attribute only while open, so a browser may never read <c>open="false"</c> — which is still open.
/// The header's accessible name is <c>aria-labelledby</c> pointing at the heading, the supporting line
/// and the status badge, because a <c>&lt;summary&gt;</c>'s name is everything under it: without it the
/// action's own label joins the disclosure's name (measured: "Variants … Clear all reviews"), which
/// makes the action read as part of the section's title. And <see cref="HeaderActions"/> is wrapped in
/// an element that stops the click, so pressing an action runs it without opening or closing the
/// section; the wrapper prevents the default action too, which is what stops a non-Chromium browser
/// from toggling the panel under the action.
/// </para>
/// </remarks>
public sealed class PolarisAccordionItem : ComponentBase, IDisposable
{
    // The wrapper around HeaderActions. It states its own layout because the actions are a row of
    // buttons at the end of the header, and it is the one place a click must not reach the summary.
    private const string ActionsStyle = "display:inline-flex;align-items:center;gap:0.5rem";

    // Ids for aria-labelledby. Per instance and per render tree: the summary's name is the referenced
    // elements' own text, so nothing has to be restated and nothing can drift from what is drawn.
    private readonly string _idPrefix = $"polaris-accordion-{Guid.NewGuid():N}";

    private bool _isOpen;
    private bool _disposed;

    /// <summary>The accordion this section belongs to, when it is inside one.</summary>
    [CascadingParameter] private PolarisAccordion? Accordion { get; set; }

    /// <summary>The header's heading.</summary>
    /// <remarks>
    /// It is drawn as ordinary text inside the summary — a <see cref="PolarisHeading"/> in the light
    /// DOM, not a <c>s-section</c> heading attribute — and referenced by <c>aria-labelledby</c>. That is
    /// what makes the header's accessible name follow the visible text instead of being restated and
    /// kept in step by hand.
    /// </remarks>
    [Parameter] public string? Header { get; set; }

    /// <summary>
    /// A line of supporting text under <see cref="Header"/>, for what the section is about.
    /// </summary>
    /// <remarks>
    /// It is body text rather than the section's <c>subheading</c>: there is no <c>s-section</c> in this
    /// header any more, because its heading and subheading are attributes that live in Polaris's shadow
    /// root and would therefore be invisible to the summary's accessible name.
    /// </remarks>
    [Parameter] public string? HeaderDetails { get; set; }

    /// <summary>
    /// Extra markup on the supporting line, beside <see cref="HeaderDetails"/> — in practice a value
    /// that carries its own colour, such as a signed difference.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="HeaderDetails"/> is one plain sentence in one subdued colour, which is right for prose
    /// and wrong for a figure whose direction matters: a price that has gone down and one that has gone
    /// up are not the same fact, and a merchant reads the colour before the sign. This slot is the
    /// caller's own markup, so it can carry that colour.
    /// </para>
    /// <para>
    /// It is wrapped in an element of this component's own with an id, and that id joins
    /// <c>aria-labelledby</c>, so whatever is passed is announced with the header rather than skipped —
    /// the same rule as <see cref="HeaderDetails"/>, and the reason it is not left to the caller to
    /// place.
    /// </para>
    /// </remarks>
    [Parameter] public RenderFragment? HeaderMeta { get; set; }

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
    /// accessible name of its own: the header's name is built from the text parameters, and a picture
    /// that repeats them is noise — pass <c>alt=""</c> on a <see cref="PolarisThumbnail"/> that only
    /// illustrates the title beside it.
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
    /// cannot carry it, because that is one plain sentence in one subdued colour and there is no way to
    /// give part of it a tone.
    /// </para>
    /// <para>
    /// It is a string plus a tone rather than a <see cref="RenderFragment"/>, so its text joins the
    /// header's accessible name without the caller having to repeat it. A badge would be announced
    /// anyway, now that the header is real text — but the component still needs its text to reference
    /// it from <c>aria-labelledby</c> alongside the heading and the supporting line.
    /// </para>
    /// </remarks>
    [Parameter] public string? HeaderStatus { get; set; }

    /// <summary>Semantic colour of <see cref="HeaderStatus"/>. Ignored when that is not set.</summary>
    [Parameter] public PolarisTone HeaderStatusTone { get; set; } = PolarisTone.Auto;

    /// <summary>
    /// Actions drawn at the inline end of the header, immediately before the chevron — in practice
    /// buttons that act on what the section shows, without opening it first.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the capability the <c>s-clickable</c> header could not offer: a button placed inside that
    /// surface was a button within a button, whose children the <c>button</c> role makes presentational,
    /// so the action left the accessibility tree entirely. A <c>&lt;summary&gt;</c> is a disclosure
    /// control rather than a button — measured as role <c>DisclosureTriangle</c> — so a button inside it
    /// is an ordinary button, announced with its own name.
    /// </para>
    /// <para>
    /// A click inside it does not open or close the section: propagation is stopped on a wrapper of this
    /// component's own, so a caller passes the buttons and nothing else. The click still reaches them
    /// because they are the event's target, and only the bubble beyond them is stopped.
    /// </para>
    /// <para>
    /// <b>Buttons only.</b> The wrapper also prevents the default action, because that is what stops a
    /// browser which does not skip the disclosure activation for interactive content from opening the
    /// panel under the action — and that would leave the panel open while this component believed it
    /// closed. A link or a checkbox in the header would have its own default cancelled by the same
    /// rule, so an action that navigates belongs at the top of the section's content, where the caller
    /// owns the markup.
    /// </para>
    /// <para>
    /// <b>The panel becomes a named <c>&lt;ChildContent&gt;</c>.</b> That is Razor's rule rather than
    /// this component's: once a component takes more than one <see cref="RenderFragment"/>, unnamed
    /// content cannot be attributed to a parameter, so a section with an action — or with
    /// <see cref="HeaderMedia"/> — writes its body as
    /// <c>&lt;ChildContent&gt;…&lt;/ChildContent&gt;</c>. A section with neither keeps the bare form.
    /// </para>
    /// </remarks>
    [Parameter] public RenderFragment? HeaderActions { get; set; }

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
                // Collapsing the open section before this one opens puts both state changes in the
                // same render batch, so the browser applies both attributes together rather than the
                // merchant watching one close and then the other open.
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
    private Task ToggleFromHeaderAsync(MouseEventArgs args) => ToggleAsync();

    // The actions' wrapper needs a handler of its own for the stop markers to attach to; the handler
    // does nothing, because the buttons inside it are the event's target.
    private void IgnoreClick(MouseEventArgs args)
    {
    }

    /// <summary>
    /// The ids whose text makes up the header's accessible name, or <c>null</c> when there is none.
    /// </summary>
    /// <remarks>
    /// A <c>&lt;summary&gt;</c>'s name is the whole of its content, which is true and wrong at once:
    /// measured, adding a button to the header made the disclosure read "Variants Each variant's price
    /// and recipe at a glance. Clear all reviews" — the action announced as part of the title. Pointing
    /// at the three text elements keeps the name to what the header is about while leaving the action's
    /// own node, and its own name, alone.
    /// </remarks>
    private string? LabelledBy
    {
        get
        {
            var ids = new List<string>(3);

            if (!string.IsNullOrWhiteSpace(Header))
            {
                ids.Add($"{_idPrefix}-heading");
            }

            if (!string.IsNullOrWhiteSpace(HeaderDetails))
            {
                ids.Add($"{_idPrefix}-details");
            }

            if (HeaderMeta is not null)
            {
                ids.Add($"{_idPrefix}-meta");
            }

            if (HasStatus)
            {
                ids.Add($"{_idPrefix}-status");
            }

            return ids.Count == 0 ? null : string.Join(' ', ids);
        }
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "data-polaris-accordion-item", true);

        if (ShowDivider)
        {
            // Outside the <details>, because a <details> is "an optional summary followed by flow
            // content" — an element before the summary is not part of that model.
            builder.OpenComponent<PolarisDivider>(2);
            builder.CloseComponent();
        }

        builder.OpenElement(10, "details");

        // Bare and present only while open, never open="false": a boolean attribute is true by
        // presence, so the closed state has to be the attribute's absence.
        if (_isOpen)
        {
            builder.AddAttribute(11, "open", true);
        }

        builder.OpenElement(12, "summary");
        builder.AddAttribute(13, "data-polaris-accordion-header", true);

        // The click arrives here from anywhere in the summary — the heading, the padding, the chevron,
        // or the keyboard's Space and Enter, which the browser turns into a click on the summary. It is
        // Blazor's own handler rather than a listener on a Polaris element, because a listener on one is
        // detached and re-attached on every render, which made "the click does nothing" intermittent.
        builder.AddAttribute(14, "onclick",
            EventCallback.Factory.Create<MouseEventArgs>(this, ToggleFromHeaderAsync));

        if (LabelledBy is { } labelledBy)
        {
            builder.AddAttribute(15, "aria-labelledby", labelledBy);
        }

        builder.AddContent(16, (RenderFragment)BuildHeader);
        builder.CloseElement();

        // The panel needs no clipping of its own: a closed <details> does not render its content, so
        // there is no hidden-but-focusable subtree and nothing to mark inert.
        builder.OpenComponent<PolarisBox>(40);
        builder.AddAttribute(41, nameof(PolarisBox.Padding), ContentPadding);
        builder.AddAttribute(42, nameof(PolarisBox.ChildContent), ChildContent);
        builder.CloseComponent();

        builder.CloseElement();
        builder.CloseElement();
    }

    /// <summary>Two groups in the header: the text at the start, the actions and the chevron at the end.</summary>
    /// <remarks>
    /// There is no row stack around the two groups, and that is load-bearing. A Polaris host computes to
    /// <c>display: contents</c>, so a stack wrapper ahead of them hands the summary a single
    /// shrink-wrapped flex item: measured, the action and the chevron sat mid-row, the chevron about
    /// 300px short of the header's edge, and a click at the header's own centre landed on the action
    /// instead of opening the section. The summary is the row — its stylesheet sets the flex layout —
    /// and the two groups are its items.
    /// </remarks>
    private void BuildHeader(RenderTreeBuilder builder)
    {
        if (HeaderMedia is null)
        {
            BuildHeading(builder);
        }
        else
        {
            // The picture and the text are ONE child of the row, so that space-between still puts the
            // chevron at the header's far edge: with three children it would centre the heading
            // between the picture and the chevron instead. Centring the group's own items is what makes
            // a 100px picture stand beside BOTH lines rather than level with the heading alone.
            builder.OpenComponent<PolarisStack>(0);
            builder.AddAttribute(1, nameof(PolarisStack.Direction), PolarisStackDirection.Inline);
            builder.AddAttribute(2, nameof(PolarisStack.Gap), PolarisGap.Base);
            builder.AddAttribute(3, nameof(PolarisStack.AlignItems), "center");
            builder.AddAttribute(4, nameof(PolarisStack.ChildContent), (RenderFragment)(group =>
            {
                // 10, then 20 upwards in BuildHeading and 40 upwards in BuildHeaderEnd: sequence numbers
                // must not go backwards inside one scope, or the renderer throws while removing the old
                // frame.
                group.AddContent(10, HeaderMedia);
                BuildHeading(group);
            }));
            builder.CloseComponent();
        }

        BuildHeaderEnd(builder);
    }

    /// <summary>The heading, its state badge, and its supporting line.</summary>
    private void BuildHeading(RenderTreeBuilder builder)
    {
        builder.OpenComponent<PolarisStack>(20);
        builder.AddAttribute(21, nameof(PolarisStack.Gap), PolarisGap.Small);
        builder.AddAttribute(22, nameof(PolarisStack.ChildContent), (RenderFragment)(text =>
        {
            // The heading and the badge share a line, so the badge qualifies the title rather than
            // competing with it from the far edge of the header.
            text.OpenComponent<PolarisStack>(0);
            text.AddAttribute(1, nameof(PolarisStack.Direction), PolarisStackDirection.Inline);
            text.AddAttribute(2, nameof(PolarisStack.Gap), PolarisGap.Small);
            text.AddAttribute(3, nameof(PolarisStack.AlignItems), "center");
            text.AddAttribute(4, nameof(PolarisStack.ChildContent), (RenderFragment)(row =>
            {
                row.OpenComponent<PolarisHeading>(0);
                row.AddAttribute(1, nameof(PolarisHeading.FontSize), PolarisFontSize.Small);
                row.AddAttribute(2, nameof(PolarisHeading.Id), $"{_idPrefix}-heading");
                row.AddAttribute(3, nameof(PolarisHeading.ChildContent),
                    (RenderFragment)(heading => heading.AddContent(0, Header)));
                row.CloseComponent();

                if (HasStatus)
                {
                    row.OpenComponent<PolarisBadge>(10);
                    row.AddAttribute(11, nameof(PolarisBadge.Id), $"{_idPrefix}-status");
                    row.AddAttribute(12, nameof(PolarisBadge.Tone), HeaderStatusTone);
                    row.AddAttribute(13, nameof(PolarisBadge.ChildContent),
                        (RenderFragment)(badge => badge.AddContent(0, HeaderStatus)));
                    row.CloseComponent();
                }
            }));
            text.CloseComponent();

            if (!string.IsNullOrWhiteSpace(HeaderDetails) || HeaderMeta is not null)
            {
                // The supporting line: the plain sentence and the caller's own markup share one row, so
                // a coloured figure sits beside the text it qualifies rather than under it.
                text.OpenComponent<PolarisStack>(20);
                text.AddAttribute(21, nameof(PolarisStack.Direction), PolarisStackDirection.Inline);
                text.AddAttribute(22, nameof(PolarisStack.Gap), PolarisGap.Small);
                text.AddAttribute(23, nameof(PolarisStack.AlignItems), "center");
                text.AddAttribute(24, nameof(PolarisStack.ChildContent), (RenderFragment)(line =>
                {
                    if (!string.IsNullOrWhiteSpace(HeaderDetails))
                    {
                        line.OpenComponent<PolarisText>(0);
                        line.AddAttribute(1, nameof(PolarisText.Id), $"{_idPrefix}-details");
                        line.AddAttribute(2, nameof(PolarisText.FontSize), PolarisFontSize.Small);
                        line.AddAttribute(3, nameof(PolarisText.Color), PolarisColor.Subdued);
                        line.AddAttribute(4, nameof(PolarisText.ChildContent),
                            (RenderFragment)(details => details.AddContent(0, HeaderDetails)));
                        line.CloseComponent();
                    }

                    if (HeaderMeta is not null)
                    {
                        // A span rather than a Polaris element: it has to be a real box in the row (every
                        // Polaris host computes to display: contents), and it carries the id the
                        // header's name points at.
                        line.OpenElement(10, "span");
                        line.AddAttribute(11, "id", $"{_idPrefix}-meta");
                        line.AddAttribute(12, "data-polaris-accordion-meta", true);
                        line.AddContent(13, HeaderMeta);
                        line.CloseElement();
                    }
                }));
                text.CloseComponent();
            }
        }));
        builder.CloseComponent();
    }

    /// <summary>The header's actions and the chevron, at the inline end of the row.</summary>
    private void BuildHeaderEnd(RenderTreeBuilder builder)
    {
        builder.OpenComponent<PolarisStack>(40);
        builder.AddAttribute(41, nameof(PolarisStack.Direction), PolarisStackDirection.Inline);
        builder.AddAttribute(42, nameof(PolarisStack.Gap), PolarisGap.Small);
        builder.AddAttribute(43, nameof(PolarisStack.AlignItems), "center");
        builder.AddAttribute(44, nameof(PolarisStack.ChildContent), (RenderFragment)(end =>
        {
            if (HeaderActions is not null)
            {
                // The one place a click must not reach the summary. stopPropagation is what keeps the
                // component's own header handler from running; preventDefault is what keeps a browser
                // that does not skip the disclosure activation for interactive content from toggling
                // the panel underneath the action — a toggle this component would never hear about,
                // leaving the panel open while its state said closed.
                end.OpenElement(0, "span");
                end.AddAttribute(1, "style", ActionsStyle);
                end.AddAttribute(2, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, IgnoreClick));
                end.AddEventStopPropagationAttribute(3, "onclick", true);
                end.AddEventPreventDefaultAttribute(4, "onclick", true);
                end.AddAttribute(5, "data-polaris-accordion-actions", true);
                end.AddContent(6, HeaderActions);
                end.CloseElement();
            }

            // The visible affordance for the state. It carries no meaning of its own — the summary is
            // what announces expanded or collapsed — and the stylesheet rotates it from details[open].
            end.OpenComponent<PolarisIcon>(10);
            end.AddAttribute(11, nameof(PolarisIcon.Type), "chevron-down");
            end.AddAttribute(12, nameof(PolarisIcon.Size), PolarisIconSize.Small);
            end.AddAttribute(13, nameof(PolarisIcon.Color), PolarisColor.Subdued);
            end.AddAttribute(14, nameof(PolarisIcon.AdditionalAttributes),
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["data-polaris-accordion-chevron"] = true,
                });
            end.CloseComponent();
        }));
        builder.CloseComponent();
    }

    /// <summary>Whether a <see cref="HeaderStatus"/> badge is drawn at all.</summary>
    private bool HasStatus => !string.IsNullOrWhiteSpace(HeaderStatus);

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
