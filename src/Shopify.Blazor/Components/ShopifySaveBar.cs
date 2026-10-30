using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Shopify.Blazor.AppBridge;

namespace Shopify.Blazor.Components;

/// <summary>
/// The Admin's contextual save bar: "Unsaved changes" with Discard and Save, drawn across the top
/// toolbar — the same bar Shopify's own product editor shows.
/// </summary>
/// <remarks>
/// <para>
/// This is an <strong>App Bridge</strong> component, not a Polaris one. The bar belongs to the admin
/// chrome that surrounds the app, so it cannot be drawn as markup inside the page: the component
/// renders a <c>&lt;ui-save-bar&gt;</c> element, which is a state carrier App Bridge reads, and then
/// asks App Bridge to show it (<c>shopify.saveBar.show(id)</c>).
/// </para>
/// <para>
/// <strong>Outside the Admin the App Bridge bar is not drawn at all.</strong> App Bridge defines
/// <c>ui-save-bar</c> and installs <c>shopify.saveBar</c> only once it has connected to the admin, so an
/// element rendered in a standalone tab would leave its two buttons visible as ordinary buttons at the
/// top of the page, and a show call would go nowhere. Availability is asked once, after the first
/// render; when it says no, <see cref="Fallback"/> is drawn instead — while <see cref="Visible"/> is
/// true — so a page opened directly still says it has unsaved changes and still offers both actions.
/// </para>
/// <para>
/// <strong>Visibility is the caller's decision.</strong> There is no form scanning and no
/// "value changed" detection: App Bridge's automatic <c>data-save-bar</c> form attribute watches
/// native input events, and a Blazor-controlled form re-renders its own values, so the bar would
/// appear and disappear on the renderer's schedule rather than the merchant's. Pass
/// <see cref="Visible"/> from state the page already knows — a comparison of the form against what
/// was loaded — and the bar follows it.
/// </para>
/// <para>
/// <strong>The buttons are real <c>&lt;button&gt;</c> elements in this document.</strong> App Bridge
/// draws the bar itself and forwards a click by calling <c>click()</c> on the corresponding light-DOM
/// button, so <see cref="OnSave"/> and <see cref="OnDiscard"/> run as ordinary Blazor event handlers.
/// They are deliberately native buttons rather than <c>s-button</c>s: App Bridge derives the bar's two
/// actions from the element's own children, and gives the primary style to the child carrying
/// <c>variant="primary"</c>.
/// </para>
/// <para>
/// Place one inside the page's top-level <c>&lt;s-page&gt;</c>, on the screen the form belongs to.
/// Several instances can coexist on a document because each id is unique, and App Bridge shows the
/// last one asked for.
/// </para>
/// </remarks>
public sealed class ShopifySaveBar : ComponentBase, IAsyncDisposable
{
    private readonly string _generatedId = $"shopify-save-bar-{Guid.NewGuid():N}";
    private bool _shown;

    /// <summary>
    /// Whether App Bridge answered that it can draw the bar, so this component renders the
    /// <c>ui-save-bar</c> element instead of <see cref="Fallback"/>.
    /// </summary>
    /// <remarks>
    /// False until App Bridge has connected, and false for ever outside the Admin. App Bridge defines
    /// <c>ui-save-bar</c> and installs <c>shopify.saveBar</c> only once it has connected, so an element
    /// rendered before that would have no shadow root — leaving its two buttons as ordinary buttons in
    /// the page — and a show call would go nowhere.
    /// </remarks>
    private bool _adminBar;

    /// <summary>Whether availability has been asked once, on the first render.</summary>
    private bool _askedAvailability;

    /// <summary>Whether availability has been asked during the current dirty spell.</summary>
    /// <remarks>
    /// App Bridge can connect after this page has rendered, so a first answer of false is not final:
    /// the question is asked again on the next edit rather than caching "no bar" for the circuit's
    /// whole life.
    /// </remarks>
    private bool _askedDuringDirty;

    /// <summary>
    /// Set once a show call has come back false, so the component stops asking and falls back for good.
    /// </summary>
    /// <remarks>
    /// Without this, a show that fails would flip back to "try the admin bar", re-render the element,
    /// fail again, and loop between the two states for as long as the form stayed dirty.
    /// </remarks>
    private bool _adminBarFailed;

    private bool _disposed;

    /// <summary>The App Bridge interop service that shows and hides the bar.</summary>
    [Inject]
    public IAppBridgeInterop AppBridge { get; set; } = default!;

    /// <summary>
    /// The element's <c>id</c>, which is how App Bridge names the bar when showing or hiding it.
    /// </summary>
    /// <remarks>
    /// A unique id is generated when this is null, which is what almost every page should do: the id
    /// is an implementation detail of the App Bridge call, not something a page needs to name.
    /// </remarks>
    [Parameter]
    public string? Id { get; set; }

    /// <summary>
    /// Whether the form has unsaved changes. The bar is shown while this is true and hidden while it
    /// is false.
    /// </summary>
    /// <remarks>
    /// Every change of this value after a render asks App Bridge to show or hide the bar, so a page
    /// sets it from its own dirty comparison on every render. A page that leaves it false has a form
    /// that never looks edited, with no other symptom.
    /// </remarks>
    [Parameter]
    public bool Visible { get; set; }

    /// <summary>True while a save is in flight, which shows the bar's Save button as loading and disables Discard.</summary>
    [Parameter]
    public bool Saving { get; set; }

    /// <summary>
    /// Adds App Bridge's confirmation dialog before Discard resets the form, for a page where the
    /// changes cannot easily be recreated.
    /// </summary>
    /// <remarks>
    /// The dialog is the admin's own; the app never sees the answer. It therefore cannot be used to
    /// widen the set of changes a discard touches — it only asks.
    /// </remarks>
    [Parameter]
    public bool DiscardConfirmation { get; set; }

    /// <summary>The Save button's text.</summary>
    [Parameter]
    public string SaveLabel { get; set; } = "Save";

    /// <summary>The Discard button's text.</summary>
    [Parameter]
    public string DiscardLabel { get; set; } = "Discard";

    /// <summary>
    /// Raised when the merchant presses Save in the bar. The page saves; the bar stays visible until
    /// <see cref="Visible"/> becomes false.
    /// </summary>
    [Parameter]
    public EventCallback OnSave { get; set; }

    /// <summary>
    /// Raised when the merchant presses Discard in the bar. The page is responsible for actually
    /// discarding — the bar does not reset any control, because the values it would reset belong to
    /// the page's state.
    /// </summary>
    [Parameter]
    public EventCallback OnDiscard { get; set; }

    /// <summary>
    /// What to draw in place of the Admin's bar when the app is not running inside the Admin, or when
    /// App Bridge has not installed the save bar API.
    /// </summary>
    /// <remarks>
    /// The Admin's bar is the whole point when it is available, and this is only the standalone
    /// degradation — but without it a page opened directly shows nothing at all when it is edited: no
    /// bar, no message, no explanation. The fragment is drawn in the same place, only while
    /// <see cref="Visible"/> is true, and the page supplies its own buttons because this component has
    /// no styling of its own to give them.
    /// </remarks>
    [Parameter]
    public RenderFragment? Fallback { get; set; }

    private string BarId => string.IsNullOrWhiteSpace(Id) ? _generatedId : Id;

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        if (!_adminBar)
        {
            // See _adminBar: an undefined ui-save-bar is an ordinary inline element, and its buttons
            // would be two plain buttons in the page. Outside the Admin the page supplies its own bar.
            if (Visible && Fallback is not null)
            {
                builder.AddContent(0, Fallback);
            }

            return;
        }

        builder.OpenElement(0, "ui-save-bar");
        builder.AddAttribute(1, "id", BarId);

        if (DiscardConfirmation)
        {
            builder.AddAttribute(2, "discardConfirmation", string.Empty);
        }

        // App Bridge validates this element against a schema of its own: a button, and only the
        // attributes `variant`, `disabled` and `loading`. Anything else — including `type`, which is
        // harmless markup everywhere else — is reported to the console as an unexpected attribute, so
        // the buttons carry nothing the schema does not name.
        builder.OpenElement(3, "button");
        builder.AddAttribute(4, "variant", "primary");

        if (Saving)
        {
            // App Bridge reads `hasAttribute("loading")`, not a property, so the bare attribute is
            // what makes the bar's Save button spin.
            builder.AddAttribute(5, "loading", string.Empty);
        }

        builder.AddAttribute(6, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, SaveAsync));
        builder.AddContent(7, SaveLabel);
        builder.CloseElement();

        builder.OpenElement(8, "button");

        if (Saving)
        {
            builder.AddAttribute(9, "disabled", string.Empty);
        }

        builder.AddAttribute(10, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, DiscardAsync));
        builder.AddContent(11, DiscardLabel);
        builder.CloseElement();

        builder.CloseElement();
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_disposed)
        {
            return;
        }

        if (!_askedAvailability)
        {
            // Asked after the first render because JS interop is not available while prerendering,
            // and the interop layer answers false there. Asked again on the next edit if the answer is
            // no: App Bridge can finish connecting after this page has rendered.
            _askedAvailability = true;
            _adminBar = !_adminBarFailed && await AppBridge.IsSaveBarAvailableAsync();

            if (_adminBar)
            {
                // Draws the element. The next pass shows it, once it is in the document.
                StateHasChanged();
            }

            return;
        }

        if (_adminBar)
        {
            if (Visible && !_shown)
            {
                // The element is rendered by now, so App Bridge can find it by id. A false answer means
                // the bar will never appear on this page, so stop asking and draw the page's own bar
                // rather than leaving the merchant with no sign of an unsaved edit.
                _shown = await AppBridge.ShowSaveBarAsync(BarId);

                if (!_shown)
                {
                    _adminBarFailed = true;
                    _adminBar = false;
                    StateHasChanged();
                }
            }
            else if (!Visible && _shown)
            {
                _shown = false;
                await AppBridge.HideSaveBarAsync(BarId);
            }

            return;
        }

        if (!Visible)
        {
            // A clean form ends the dirty spell, so the next edit asks again.
            _askedDuringDirty = false;
            return;
        }

        if (!_askedDuringDirty && !_adminBarFailed)
        {
            _askedDuringDirty = true;
            _adminBar = await AppBridge.IsSaveBarAvailableAsync();

            if (_adminBar)
            {
                // Replaces the fallback with the admin's bar on the next pass.
                StateHasChanged();
            }
        }
    }

    private Task SaveAsync() => OnSave.InvokeAsync();

    private Task DiscardAsync() => Saving ? Task.CompletedTask : OnDiscard.InvokeAsync();

    /// <summary>
    /// Hides the bar when the page is torn down.
    /// </summary>
    /// <remarks>
    /// The bar is the admin's, not the page's, so it survives the component that asked for it. A bar
    /// left showing would cover the next screen's toolbar and claim changes that no longer exist.
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_shown)
        {
            _shown = false;
            await AppBridge.HideSaveBarAsync(BarId);
        }
    }
}
