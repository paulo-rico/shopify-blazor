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
/// <strong>Nothing is rendered outside the Admin.</strong> App Bridge defines <c>ui-save-bar</c> only
/// once it has connected to the admin, so an undefined element would leave the two buttons visible as
/// ordinary buttons at the top of the page. Availability is therefore asked once, after the first
/// render, and the element is drawn only when it answers yes. That is also why a page keeps its own
/// Save control as the standalone fallback.
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
    private bool _disposed;

    /// <summary>
    /// Whether App Bridge is present, asked once after the first render.
    /// </summary>
    /// <remarks>
    /// Nothing is rendered until this is true. Outside the Admin the <c>ui-save-bar</c> element is not
    /// defined — App Bridge defines it only once it has connected to the admin — so leaving it in the
    /// markup would put two unstyled, visible buttons at the top of the page. The element's own
    /// <c>:host { display: none }</c> comes from App Bridge's shadow root, and there is no shadow root
    /// without the definition.
    /// </remarks>
    private bool _available;

    private bool _checkedAvailability;

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

    private string BarId => string.IsNullOrWhiteSpace(Id) ? _generatedId : Id;

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        if (!_available)
        {
            // See _available: an undefined ui-save-bar is an ordinary inline element, and its buttons
            // would be two plain buttons in the page.
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

        if (!_checkedAvailability)
        {
            // Asked after the first render because JS interop is not available while prerendering,
            // and the interop layer answers false there — which would look like a standalone page for
            // ever. One answer is kept: availability does not change while the circuit lives.
            _checkedAvailability = true;
            _available = await AppBridge.IsAvailableAsync();

            if (_available)
            {
                StateHasChanged();
            }

            return;
        }

        if (!_available || _shown == Visible)
        {
            return;
        }

        _shown = Visible;

        // Only a change is sent: App Bridge keeps its own shown/hidden state, and a call per render
        // would fight it. False answers are expected outside the Admin and need no handling — the
        // page's own Save control is the fallback there.
        if (Visible)
        {
            await AppBridge.ShowSaveBarAsync(BarId);
        }
        else
        {
            await AppBridge.HideSaveBarAsync(BarId);
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
