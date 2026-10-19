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
/// The header is a real <see cref="PolarisButton"/>, so it is a <c>&lt;button&gt;</c> inside
/// Polaris's shadow root: focusable, announced, and opened with Enter or Space. Its expanded state
/// reaches assistive technology as <c>aria-expanded</c>; the chevron is the sighted equivalent and
/// carries no meaning of its own.
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

    /// <summary>
    /// The header's text, which is also the button's accessible name.
    /// </summary>
    /// <remarks>Ignored when <see cref="HeaderContent"/> is set.</remarks>
    [Parameter] public string? Header { get; set; }

    /// <summary>
    /// The header's content, for a heading that needs more than text — a badge beside a title, for
    /// example.
    /// </summary>
    /// <remarks>
    /// It is rendered inside a Polaris button, which lays its children out on one line and truncates
    /// them, so keep it short. When both are supplied this wins over <see cref="Header"/>.
    /// </remarks>
    [Parameter] public RenderFragment? HeaderContent { get; set; }

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

        builder.OpenComponent<PolarisButton>(10);
        builder.AddAttribute(11, nameof(PolarisButton.Variant), PolarisButtonVariant.Tertiary);
        builder.AddAttribute(12, nameof(PolarisButton.InlineSize), PolarisButtonInlineSize.Fill);
        builder.AddAttribute(13, nameof(PolarisButton.AccessibilityExpanded),
            _isOpen ? PolarisAccessibilityExpanded.Expanded : PolarisAccessibilityExpanded.Collapsed);
        builder.AddAttribute(14, nameof(PolarisButton.OnClick),
            EventCallback.Factory.Create<PolarisEventArgs>(this, ToggleFromHeaderAsync));
        builder.AddAttribute(15, nameof(PolarisButton.ChildContent), (RenderFragment)BuildHeaderContent);
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

    private void BuildHeaderContent(RenderTreeBuilder builder)
    {
        if (HeaderContent is not null)
        {
            builder.AddContent(0, HeaderContent);
        }
        else if (Header is not null)
        {
            builder.AddContent(1, Header);
        }

        builder.OpenComponent<PolarisIcon>(2);
        builder.AddAttribute(3, nameof(PolarisIcon.Type), "chevron-down");
        builder.AddAttribute(4, nameof(PolarisIcon.Size), PolarisIconSize.Small);
        builder.AddAttribute(5, nameof(PolarisIcon.Color), PolarisColor.Subdued);
        builder.AddAttribute(6, nameof(PolarisIcon.AdditionalAttributes),
            new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["data-polaris-accordion-chevron"] = true,
            });
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
