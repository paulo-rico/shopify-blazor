using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Polaris.Blazor.Interop;

namespace Polaris.Blazor.Components;

/// <summary>Where a popup's panel is drawn relative to the element it belongs to.</summary>
public enum PolarisPopupPlacement
{
    /// <summary>Under the anchor, the usual direction for a dropdown.</summary>
    Below,

    /// <summary>Above the anchor, for a control against the bottom of the viewport.</summary>
    Above,
}

/// <summary>
/// A panel drawn over the page and anchored to the element it belongs to, for content Polaris has no
/// element to show: a suggestion list, a menu, a small picker.
/// </summary>
/// <remarks>
/// <para>
/// <b>Polaris's own popover cannot be used, so this is the popup this library owns.</b>
/// <c>s-popover</c> cannot be opened from code — measured in Chrome, <c>show</c>, <c>hide</c>,
/// <c>toggle</c> and <c>showOverlay</c> are all <c>undefined</c> — and it is opened declaratively by a
/// <em>button</em>'s <c>commandFor</c>, which is a different control from a text field with suggestions
/// or a panel a page wants to show itself. What cannot be borrowed is the behaviour, not the look.
/// </para>
/// <para>
/// <b>The panel is a native popover, which puts it in the browser's top layer.</b> That is not a
/// detail: an absolutely positioned panel inside an <c>s-section</c> was measured being cut off at the
/// section's painted edge, so its lower suggestions could not be clicked — and no <c>z-index</c> fixes
/// that, because clipping is not stacking. The top layer is painted outside every ancestor's clip. The
/// element stays where Blazor put it, so nothing has to be reparented.
/// </para>
/// <para>
/// <b>The surface is system colours, not a palette.</b> <c>Canvas</c> and <c>CanvasText</c> are what the
/// browser resolves for the document's own <c>color-scheme</c>, so the panel is light in a light Admin
/// and dark in a dark one. A Polaris custom property would be empty here: those tokens are defined
/// inside Polaris's shadow roots and do not inherit out of them. Everything inside the panel is still a
/// Polaris element.
/// </para>
/// <para>
/// The popup renders its anchor and its panel and nothing else. It has no dismissal logic: the caller
/// owns <see cref="Open"/>, because only the caller knows whether a blur is the merchant leaving or
/// moving into the panel. <see cref="PolarisAutocomplete{TItem}"/> is the worked example.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;PolarisPopup Open="_open"&gt;
///     &lt;Anchor&gt;
///         &lt;PolarisButton OnClick="Toggle"&gt;Actions&lt;/PolarisButton&gt;
///     &lt;/Anchor&gt;
///     &lt;ChildContent&gt;
///         &lt;PolarisStack Gap="PolarisGap.None"&gt;
///             &lt;PolarisButton Variant="PolarisButtonVariant.Tertiary"&gt;Rename&lt;/PolarisButton&gt;
///             &lt;PolarisButton Variant="PolarisButtonVariant.Tertiary"&gt;Duplicate&lt;/PolarisButton&gt;
///         &lt;/PolarisStack&gt;
///     &lt;/ChildContent&gt;
/// &lt;/PolarisPopup&gt;
/// </code>
/// </example>
public sealed class PolarisPopup : ComponentBase, IAsyncDisposable
{
    private ElementReference _anchorElement;
    private ElementReference _panelElement;
    private bool _shown;

    /// <summary>The interop service that shows and hides the panel and keeps it under the anchor.</summary>
    [Inject] private IPolarisInterop Interop { get; set; } = default!;

    /// <summary>Whether the panel is shown. The caller owns this; the popup never closes itself.</summary>
    [Parameter] public bool Open { get; set; }

    /// <summary>The element the panel is anchored to — usually the trigger, or the field it belongs to.</summary>
    [Parameter] public RenderFragment? Anchor { get; set; }

    /// <summary>
    /// The panel's content. It is drawn over the page in a box matching the anchor's width. Null means
    /// there is no panel at all, so a caller that renders no content pays for no popover element.
    /// </summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>Which side of the anchor the panel appears on.</summary>
    [Parameter] public PolarisPopupPlacement Placement { get; set; } = PolarisPopupPlacement.Below;

    /// <summary>
    /// The panel's preferred height cap, in any CSS length. The module lowers it when the chosen side of
    /// the viewport has less room, and the content scrolls inside it — a panel that runs past the
    /// viewport edge has rows the merchant can see and cannot click.
    /// </summary>
    [Parameter] public string MaxBlockSize { get; set; } = "320px";

    /// <summary>
    /// The stacking order used before the popover enters the top layer, and in a browser without the
    /// popover API. The top layer itself ignores z-index, and a Polaris dialog is in the top layer too.
    /// </summary>
    [Parameter] public int ZIndex { get; set; } = 1000;

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "data-polaris-popup-anchor", string.Empty);
        builder.AddAttribute(2, "style", "position:relative;display:block");
        builder.AddElementReferenceCapture(3, element => _anchorElement = element);
        builder.AddContent(4, Anchor);

        if (ChildContent is not null)
        {
            // The panel is rendered whether or not it is open, so the element the interop shows is a
            // stable one: a panel created only on open would need its first show call to wait for a
            // render that has not happened yet. It is `hidden` and a closed popover, so it is invisible
            // and out of the accessibility tree until it is shown.
            builder.OpenElement(5, "div");
            builder.AddAttribute(6, "data-polaris-popup", string.Empty);
            builder.AddAttribute(7, "popover", "manual");
            builder.AddAttribute(8, "style", PanelStyle());

            if (!Open)
            {
                builder.AddAttribute(9, "hidden", true);
            }

            builder.AddElementReferenceCapture(10, element => _panelElement = element);
            builder.AddContent(11, ChildContent);
            builder.CloseElement();
        }

        builder.CloseElement();
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (Open && !_shown && _panelElement.Context is not null)
        {
            _shown = true;
            await Interop.ShowPopupAsync(_panelElement, _anchorElement, Placement == PolarisPopupPlacement.Above);
        }
        else if (!Open && _shown)
        {
            _shown = false;
            await Interop.HidePopupAsync(_panelElement);
        }
    }

    /// <summary>
    /// The panel's inline style: its closed-state appearance and the cap. Position, width and the
    /// vertical anchor are written by the module when the panel is shown, because they depend on where
    /// the anchor is on screen; everything else is here so a panel that has not been shown yet still
    /// reads as the same surface.
    /// </summary>
    private string PanelStyle()
    {
        return string.Join(
            ";",
            "position:fixed",
            // The width the module measures is the border box: without this the panel is as much wider
            // than its anchor as its own padding and border, which reads as a control that is not
            // aligned with the field it belongs to.
            "box-sizing:border-box",
            $"z-index:{ZIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
            $"max-block-size:{MaxBlockSize}",
            "overflow-y:auto",
            "overscroll-behavior:contain",
            "background:Canvas",
            "color:CanvasText",
            "border:1px solid rgba(0, 0, 0, .15)",
            "border-color:color-mix(in srgb, CanvasText 18%, transparent)",
            "border-radius:12px",
            "box-shadow:0 6px 24px rgba(0, 0, 0, .18), 0 2px 6px rgba(0, 0, 0, .10)",
            "padding:4px");
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (!_shown)
        {
            return;
        }

        _shown = false;
        await Interop.HidePopupAsync(_panelElement);
    }
}
