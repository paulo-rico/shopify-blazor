using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Polaris.Blazor.Events;
using Polaris.Blazor.Interop;

namespace Polaris.Blazor;

/// <summary>
/// Base class for a component that wraps exactly one Shopify custom element.
/// </summary>
/// <remarks>
/// <para>
/// A wrapper's whole job is to render the element Shopify defined, with the right attributes, and
/// to attach listeners for that element's events. Two conventions make that mechanical:
/// </para>
/// <list type="bullet">
/// <item>
/// <see cref="BuildAttributes"/> collects parameters into the attribute dictionary. Booleans are
/// presence attributes, enums are rendered through <see cref="PolarisAttributeValue"/>, and
/// <c>null</c> values are dropped — so "not set" and "set to false" stay distinguishable.
/// </item>
/// <item>
/// <see cref="ConfigureEvents"/> maps DOM event names to callbacks, which are attached once the
/// element exists in the DOM and detached when the component is disposed.
/// </item>
/// </list>
/// <para>
/// Wrappers never implement their own visual appearance. Polaris components live in a shadow root
/// and cannot be restyled, so anything that looks like styling here is a bug waiting to surface.
/// </para>
/// </remarks>
public abstract class PolarisElementComponentBase : ComponentBase, IAsyncDisposable
{
    private static readonly IReadOnlyDictionary<string, object?> EmptyProperties =
        new Dictionary<string, object?>(StringComparer.Ordinal);

    /// <summary>An empty property set, for components whose properties are all optional.</summary>
    protected static readonly IReadOnlyDictionary<string, object?> EmptyPropertySet = EmptyProperties;

    private ElementReference _element;
    private Dictionary<string, EventCallback<PolarisEventArgs>> _registrations = new(StringComparer.Ordinal);
    private bool _attached;
    private bool _disposed;
    private string? _generatedId;

    /// <summary>The interop service, supplied by the application's service collection.</summary>
    [Inject]
    protected IPolarisInterop Interop { get; set; } = default!;

    /// <summary>
    /// The custom element to render, for example <c>s-button</c>.
    /// </summary>
    protected abstract string TagName { get; }

    /// <summary>Name of the attribute carrying the element's id. Defaults to <c>id</c>.</summary>
    protected virtual string IdAttributeName => "id";

    /// <summary>
    /// A stable id for this element, so a trigger button can address it with <c>commandFor</c>.
    /// </summary>
    [Parameter]
    public string? Id { get; set; }

    /// <summary>
    /// Replaces the element's accessible name when the visible content is not descriptive enough.
    /// Required for icon-only controls.
    /// </summary>
    [Parameter]
    public string? AccessibilityLabel { get; set; }

    /// <summary>
    /// An opaque value echoed back on every event from this element, so a single handler can serve
    /// many instances — the usual shape for a list of rows.
    /// </summary>
    [Parameter]
    public string? ItemTag { get; set; }

    /// <summary>
    /// Places this element in one of its parent's named slots.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Polaris slots are read from the light DOM and validated: a <c>primary-action</c> slot
    /// accepts a button and rejects anything else, including a <c>div</c> that merely wraps one.
    /// So the slot belongs on the component itself —
    /// <c>&lt;PolarisButton Slot="primary-action"&gt;</c> — and slots are not exposed here as
    /// <see cref="RenderFragment"/> parameters, because a fragment cannot carry an attribute.
    /// </para>
    /// <para>
    /// This mirrors the web component API exactly, which is deliberate: the wrapper adds type
    /// safety for attribute <em>values</em>, it does not invent a different composition model.
    /// </para>
    /// </remarks>
    [Parameter]
    public string? Slot { get; set; }

    /// <summary>
    /// Extra attributes forwarded to the element unchanged. Use for Polaris attributes this
    /// wrapper does not model yet, rather than forking the component.
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// DOM properties assigned after the element has upgraded. Use for values that cannot be
    /// expressed as attributes, most importantly array-valued ones such as
    /// <c>s-choice-list.values</c>. <c>null</c> entries are skipped.
    /// </summary>
    protected virtual IReadOnlyDictionary<string, object?> Properties => EmptyProperties;

    /// <summary>
    /// DOM properties cleared on every render, so a stale array cannot keep state the server has
    /// since dropped.
    /// </summary>
    protected virtual IEnumerable<string> ClearedProperties => [];

    /// <summary>
    /// True when the element must have an id even if the caller did not supply one, because
    /// another component needs to address it.
    /// </summary>
    protected virtual bool RequiresId => false;

    /// <summary>
    /// The id assigned to the element, whether supplied by the caller or generated in the browser.
    /// </summary>
    public string? ElementId => Id ?? _generatedId;

    /// <summary>
    /// The element itself. Valid only after the first render; used by components that expose
    /// imperative methods such as <c>s-modal.showOverlay()</c>.
    /// </summary>
    protected ElementReference Element => _element;

    /// <summary>
    /// Adds this component's parameters to <paramref name="attributes"/>. Overrides should call
    /// the base implementation first, so the shared parameters and any caller-supplied
    /// <see cref="AdditionalAttributes"/> keep their precedence.
    /// </summary>
    protected virtual void BuildAttributes(Dictionary<string, string?> attributes)
    {
        attributes[IdAttributeName] = Id;
        attributes["accessibilityLabel"] = AccessibilityLabel;
        attributes["slot"] = Slot;

        if (AdditionalAttributes is not null)
        {
            foreach (var (key, value) in AdditionalAttributes)
            {
                attributes[key] = PolarisAttributeValue.Format(value);
            }
        }
    }

    /// <summary>
    /// Maps DOM event names to the callbacks that should receive them. Called on every parameter
    /// pass; the map is rebuilt from scratch so a cleared callback is really removed.
    /// </summary>
    protected virtual void ConfigureEvents(PolarisEventRegistration events)
    {
    }

    /// <summary>Renders the element's children.</summary>
    protected virtual RenderFragment? BuildChildContent() => null;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        _registrations = new Dictionary<string, EventCallback<PolarisEventArgs>>(StringComparer.Ordinal);
        ConfigureEvents(new PolarisEventRegistration(_registrations));
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _attached = true;
        }

        if (_disposed)
        {
            return;
        }

        if (Properties.Count > 0 || ClearedProperties.Any())
        {
            await Interop.SetPropertiesAsync(_element, TagName, Properties).ConfigureAwait(false);
        }

        if (_registrations.Count > 0)
        {
            // Re-attaching on every render would be wasteful, but the callback map changes when
            // parameters change, so the registration is refreshed whenever it is non-empty.
            await Interop.AttachEventsAsync(_element, TagName, _registrations.Keys, _registrations).ConfigureAwait(false);
        }

        if (Id is null && _generatedId is null && RequiresId)
        {
            _generatedId = await Interop.EnsureIdAsync(_element).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var attributes = new Dictionary<string, string?>(StringComparer.Ordinal);
        BuildAttributes(attributes);

        builder.OpenElement(0, TagName);

        foreach (var (key, value) in attributes)
        {
            if (value is null)
            {
                continue;
            }

            if (value.Length == 0)
            {
                // A bare attribute. This is the only correct spelling of a Polaris boolean:
                // `disabled="false"` is still true to a custom element.
                builder.AddAttribute(1, key, true);
            }
            else
            {
                builder.AddAttribute(2, key, value);
            }
        }

        builder.AddElementReferenceCapture(3, element => _element = element);
        builder.AddContent(4, BuildChildContent());
        builder.CloseElement();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_attached)
        {
            await Interop.DetachEventsAsync(_element).ConfigureAwait(false);
        }

        GC.SuppressFinalize(this);
    }
}
