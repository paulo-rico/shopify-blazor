using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Polaris.Blazor.Events;

namespace Polaris.Blazor.Interop;

/// <summary>Options for the Polaris JavaScript interop layer.</summary>
public sealed class PolarisInteropOptions
{
    /// <summary>
    /// Path of the ES module that ships with this package. Served as a static web asset from the
    /// Razor class library, so a consuming app does not copy it.
    /// </summary>
    public string ModulePath { get; set; } = "./_content/Polaris.Blazor/polaris-interop.js";

    /// <summary>How long to wait for a Polaris custom element to be defined before giving up.</summary>
    public TimeSpan ComponentReadyTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>When false, DOM events are not forwarded and callbacks never fire. For static rendering.</summary>
    public bool EnableEvents { get; set; } = true;
}

/// <summary>
/// The single JavaScript entry point for the component library.
/// </summary>
/// <remarks>
/// Components do not touch <see cref="IJSRuntime"/> themselves. They queue property writes and
/// event registrations here, which keeps the interop surface small enough to test and gives one
/// place to add cleanup.
/// </remarks>
public interface IPolarisInterop : IAsyncDisposable
{
    /// <summary>Ensures the module is loaded. Safe to call repeatedly.</summary>
    ValueTask<bool> EnsureInitializedAsync();

    /// <summary>Ensures the element has an id and returns it, for <c>commandFor</c>/<c>interestFor</c>.</summary>
    ValueTask<string?> EnsureIdAsync(ElementReference element);

    /// <summary>Attaches the named DOM events and forwards them to <paramref name="registrations"/>.</summary>
    ValueTask AttachEventsAsync(
        ElementReference element,
        string tagName,
        IEnumerable<string> eventNames,
        IReadOnlyDictionary<string, EventCallback<PolarisEventArgs>> registrations);

    /// <summary>Detaches every listener attached to the element.</summary>
    ValueTask DetachEventsAsync(ElementReference element);

    /// <summary>
    /// Calls a method on the element itself, such as <c>showOverlay</c> or <c>hideOverlay</c> on a
    /// modal. Polaris only defines its methods once the element has upgraded, so the call waits.
    /// </summary>
    ValueTask InvokeElementMethodAsync(ElementReference element, string methodName, params object?[] arguments);

    /// <summary>Sets DOM properties that attributes cannot carry, after the element upgrades.</summary>
    ValueTask SetPropertiesAsync(ElementReference element, string tagName, IReadOnlyDictionary<string, object?> values);

    /// <summary>Clears previously set array-valued properties.</summary>
    ValueTask ClearPropertiesAsync(ElementReference element, string tagName, IEnumerable<string> names);

    /// <summary>
    /// Moves keyboard focus into <paramref name="element"/>: onto the first focusable control it
    /// contains — a field's input, or the button a button-like element renders — or onto the element
    /// itself when it is the focusable thing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Blazor's own <c>ElementReference.FocusAsync</c> focuses the element it is given, and for a
    /// Polaris host that is the wrong element: the host computes to <c>display: contents</c> and the box
    /// that takes typing lives inside a shadow root. This helper crosses that boundary, which is what
    /// makes "the editor appears where the merchant was typing" possible at all.
    /// </para>
    /// <para>
    /// Returns <c>false</c> when nothing focusable was found, so a Polaris change that moves the markup
    /// costs the focus rather than throwing into a component's lifetime.
    /// </para>
    /// </remarks>
    /// <param name="element">The container to focus inside. It may be the field itself, a row around it, or a wrapper.</param>
    /// <param name="selector">
    /// Narrows the search to the first descendant matching this CSS selector. Required whenever the
    /// container holds more than one focusable thing — a grid holds a checkbox per row — because the
    /// first one found is not necessarily the one the caller means.
    /// </param>
    ValueTask<bool> FocusWithinAsync(ElementReference element, string? selector = null);

    /// <summary>
    /// Announces whether what an element controls is open, by writing <c>aria-expanded</c> onto the
    /// <c>&lt;button&gt;</c> it renders inside its own shadow root.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This exists for <c>s-clickable</c>, which is an interactive surface with a real button inside it
    /// but no property for the expanded state — measured from its render function, which reads only the
    /// accessibility label, the command and interest props. <c>s-button</c> needs none of this: it has
    /// <c>accessibilityExpanded</c>, so that wrapper sets a property instead.
    /// </para>
    /// <para>
    /// Returns <c>false</c> when there is no button to write to, so a change in Polaris's markup costs
    /// the announcement rather than throwing into a component's lifetime.
    /// </para>
    /// </remarks>
    /// <param name="element">The element whose shadow button carries the state.</param>
    /// <param name="expanded">Whether the controlled content is open.</param>
    ValueTask<bool> SetAriaExpandedAsync(ElementReference element, bool expanded);

    /// <summary>
    /// Shows a panel over the page, anchored to an element, in the browser's top layer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The top layer is what makes this different from positioning a <c>&lt;div&gt;</c>: a popover is painted
    /// outside every ancestor's clipping, so a panel inside a section — or any other element that
    /// hides its overflow — is not cut off. Measured: an absolutely positioned panel inside an
    /// <c>s-section</c> lost its second row at exactly the section's painted edge, and no z-index
    /// could recover it.
    /// </para>
    /// <para>
    /// Coordinates are set from the anchor's viewport rect by the module, and re-applied while the
    /// page scrolls or the window resizes. Nothing moves in the DOM, so the Blazor renderer keeps
    /// owning the element.
    /// </para>
    /// </remarks>
    /// <param name="panel">The panel to show.</param>
    /// <param name="anchor">The element it is anchored to.</param>
    /// <param name="above">Whether the panel is drawn above the anchor rather than below it.</param>
    /// <returns>False when either element was missing or the module could not load.</returns>
    ValueTask<bool> ShowPopupAsync(ElementReference panel, ElementReference anchor, bool above);

    /// <summary>Hides a panel shown by <see cref="ShowPopupAsync"/> and releases its positioning listeners.</summary>
    /// <param name="panel">The panel to hide. It may already have left the DOM.</param>
    ValueTask HidePopupAsync(ElementReference panel);

    /// <summary>Reports which custom elements are registered and upgraded in the current page.</summary>
    ValueTask<IReadOnlyDictionary<string, PolarisComponentStatus>> GetComponentStatusAsync(IEnumerable<string> tagNames);

    /// <summary>True when the page is running inside the Shopify Admin.</summary>
    ValueTask<bool> IsEmbeddedAsync();
}

/// <summary>Whether a custom element is registered, and whether every instance of it upgraded.</summary>
/// <param name="Defined">True when the custom element has a definition.</param>
/// <param name="Upgraded">True when every matching element in the page is an instance of that definition.</param>
/// <param name="Count">How many matching elements the page contains.</param>
public sealed record PolarisComponentStatus(bool Defined, bool Upgraded, int Count);

/// <summary>Default <see cref="IPolarisInterop"/> over <see cref="IJSRuntime"/>.</summary>
public sealed class PolarisInterop : IPolarisInterop
{
    private readonly IJSRuntime _js;
    private readonly PolarisInteropOptions _options;
    private readonly ILogger<PolarisInterop>? _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly PolarisEventDispatcher _dispatcher = new();
    private IJSObjectReference? _module;
    private DotNetObjectReference<PolarisEventDispatcher>? _dispatcherReference;
    private bool _disposed;

    /// <summary>Creates the interop service.</summary>
    public PolarisInterop(
        IJSRuntime js,
        Microsoft.Extensions.Options.IOptions<PolarisInteropOptions> options,
        ILogger<PolarisInterop>? logger = null)
    {
        _js = js ?? throw new ArgumentNullException(nameof(js));
        _options = options?.Value ?? new PolarisInteropOptions();
        _logger = logger;
    }

    /// <summary>
    /// The object JavaScript calls back into for every forwarded event.
    /// </summary>
    /// <remarks>
    /// Exposed so the routing path — register, dispatch, unregister — can be exercised without a
    /// browser, and so a host can wire its own listeners through the same dispatcher.
    /// </remarks>
    public PolarisEventDispatcher Dispatcher => _dispatcher;

    /// <summary>Handles a DOM event as if JavaScript had forwarded it.</summary>
    public Task DispatchAsync(string handle, string eventName, PolarisEventArgs args)
        => _dispatcher.Dispatch(handle, eventName, args);

    /// <summary>How many component event registrations are currently held.</summary>
    public int RegistrationCount => _dispatcher.RegistrationCount;

    /// <inheritdoc />
    public async ValueTask<bool> EnsureInitializedAsync()
    {
        if (_disposed)
        {
            return false;
        }

        if (_module is not null)
        {
            return true;
        }

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_module is not null)
            {
                return true;
            }

            _dispatcherReference = DotNetObjectReference.Create(_dispatcher);
            _module = await _js.InvokeAsync<IJSObjectReference>("import", VersionedModuleUrl(_options.ModulePath)).ConfigureAwait(false);
            return true;
        }
        catch (JSDisconnectedException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            // JavaScript interop is not available during static server-side prerendering.
            return false;
        }
        catch (JSException ex)
        {
            // A genuine failure to load the module means no events ever reach .NET: every button
            // looks correct and does nothing. That must be visible, not swallowed.
            _logger?.LogError(ex, "Polaris interop module '{Module}' could not be loaded from the browser.", _options.ModulePath);
            return false;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// The module's URL with a build stamp appended, so a deploy cannot be served a stale copy.
    /// </summary>
    /// <param name="path">The configured module path.</param>
    /// <returns>The same path with <c>v=&lt;build id&gt;</c> appended.</returns>
    /// <remarks>
    /// A static web asset is served with an ETag and no <c>Cache-Control</c>, so the browser falls back
    /// to heuristic freshness and may reuse a module from before an update for the rest of its
    /// lifetime — the newer functions are simply absent while the page renders perfectly. Measured on a
    /// deployed app whose App Bridge module had grown new exports: the browser kept the old one, the
    /// calls failed, and the feature degraded silently. The module version id changes on every build,
    /// which is the lifetime wanted: one deploy keeps its cache, the next gets a new URL.
    /// </remarks>
    private static string VersionedModuleUrl(string path)
    {
        var build = typeof(PolarisInterop).Assembly.ManifestModule.ModuleVersionId.ToString("N");
        var separator = path.Contains('?') ? '&' : '?';

        return path + separator + "v=" + build;
    }

    /// <inheritdoc />
    public async ValueTask<string?> EnsureIdAsync(ElementReference element)
    {
        if (!await EnsureInitializedAsync().ConfigureAwait(false))
        {
            return null;
        }

        try
        {
            return await _module!.InvokeAsync<string>("ensureId", element).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsRecoverable(ex))
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async ValueTask AttachEventsAsync(
        ElementReference element,
        string tagName,
        IEnumerable<string> eventNames,
        IReadOnlyDictionary<string, EventCallback<PolarisEventArgs>> registrations)
    {
        if (!_options.EnableEvents || registrations.Count == 0)
        {
            return;
        }

        if (!await EnsureInitializedAsync().ConfigureAwait(false))
        {
            return;
        }

        var handle = Guid.NewGuid().ToString("n");
        _dispatcher.Register(handle, registrations);

        try
        {
            await _module!.InvokeVoidAsync(
                "attachEvents",
                element,
                eventNames.ToArray(),
                _dispatcherReference,
                handle,
                tagName).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsRecoverable(ex))
        {
            _dispatcher.Unregister(handle);
        }
    }

    /// <inheritdoc />
    public async ValueTask DetachEventsAsync(ElementReference element)
    {
        if (_module is null)
        {
            return;
        }

        try
        {
            await _module.InvokeVoidAsync("release", element).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsRecoverable(ex))
        {
            // The circuit or page is gone; there is nothing left to detach.
        }
    }

    /// <inheritdoc />
    public async ValueTask SetPropertiesAsync(ElementReference element, string tagName, IReadOnlyDictionary<string, object?> values)
    {
        if (values.Count == 0 || !await EnsureInitializedAsync().ConfigureAwait(false))
        {
            return;
        }

        try
        {
            await _module!.InvokeVoidAsync("setProperties", element, tagName, values).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsRecoverable(ex))
        {
            // Property assignment is a refinement; the declared attributes still render.
        }
    }

    /// <inheritdoc />
    public async ValueTask ClearPropertiesAsync(ElementReference element, string tagName, IEnumerable<string> names)
    {
        if (!await EnsureInitializedAsync().ConfigureAwait(false))
        {
            return;
        }

        try
        {
            await _module!.InvokeVoidAsync("clearProperties", element, tagName, names.ToArray()).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsRecoverable(ex))
        {
        }
    }

    /// <inheritdoc />
    public async ValueTask<bool> FocusWithinAsync(ElementReference element, string? selector = null)
    {
        if (!await EnsureInitializedAsync().ConfigureAwait(false))
        {
            return false;
        }

        try
        {
            var focused = await _module!.InvokeAsync<bool>("focusWithin", element, selector).ConfigureAwait(false);

            if (!focused)
            {
                // Reported rather than silent: a caller asked for the caret to move, and it did not. The
                // failure has no other symptom — the editor appears, looking right, and the next
                // keystroke goes to the page — so this line is the only place it can be seen.
                _logger?.LogDebug(
                    "Polaris interop: focusWithin found nothing to focus inside '{Selector}'.",
                    selector ?? "(the element itself)");
            }

            return focused;
        }
        catch (Exception ex) when (IsRecoverable(ex))
        {
            _logger?.LogWarning(ex, "Polaris interop: focusWithin could not move focus.");
            return false;
        }
    }

    /// <inheritdoc />
    public async ValueTask<bool> SetAriaExpandedAsync(ElementReference element, bool expanded)
    {
        if (!await EnsureInitializedAsync().ConfigureAwait(false))
        {
            return false;
        }

        try
        {
            return await _module!.InvokeAsync<bool>("setShadowButtonExpanded", element, expanded).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsRecoverable(ex))
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async ValueTask InvokeElementMethodAsync(ElementReference element, string methodName, params object?[] arguments)
    {
        if (!await EnsureInitializedAsync().ConfigureAwait(false))
        {
            return;
        }

        try
        {
            await _module!.InvokeVoidAsync("invokeElementMethod", element, methodName, arguments).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsRecoverable(ex))
        {
        }
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyDictionary<string, PolarisComponentStatus>> GetComponentStatusAsync(IEnumerable<string> tagNames)
    {
        var empty = new Dictionary<string, PolarisComponentStatus>(StringComparer.Ordinal);

        if (!await EnsureInitializedAsync().ConfigureAwait(false))
        {
            return empty;
        }

        try
        {
            var raw = await _module!
                .InvokeAsync<Dictionary<string, StatusDto>>("componentStatus", tagNames.ToArray())
                .ConfigureAwait(false);

            return raw.ToDictionary(
                pair => pair.Key,
                pair => new PolarisComponentStatus(pair.Value.Defined, pair.Value.Upgraded, pair.Value.Count),
                StringComparer.Ordinal);
        }
        catch (Exception ex) when (IsRecoverable(ex))
        {
            return empty;
        }
    }

    /// <inheritdoc />
    public async ValueTask<bool> ShowPopupAsync(ElementReference panel, ElementReference anchor, bool above)
    {
        if (!await EnsureInitializedAsync().ConfigureAwait(false))
        {
            return false;
        }

        try
        {
            return await _module!.InvokeAsync<bool>("showPopup", panel, anchor, above).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsRecoverable(ex))
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async ValueTask HidePopupAsync(ElementReference panel)
    {
        if (!await EnsureInitializedAsync().ConfigureAwait(false))
        {
            return;
        }

        try
        {
            await _module!.InvokeVoidAsync("hidePopup", panel).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsRecoverable(ex))
        {
        }
    }

    /// <inheritdoc />
    public async ValueTask<bool> IsEmbeddedAsync()
    {
        if (!await EnsureInitializedAsync().ConfigureAwait(false))
        {
            return false;
        }

        try
        {
            return await _module!.InvokeAsync<bool>("isEmbedded").ConfigureAwait(false);
        }
        catch (Exception ex) when (IsRecoverable(ex))
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_module is not null)
        {
            try
            {
                await _module.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex) when (IsRecoverable(ex))
            {
            }

            _module = null;
        }

        _dispatcherReference?.Dispose();
        _dispatcherReference = null;
        _dispatcher.Dispose();
        _gate.Dispose();
    }

    private static bool IsRecoverable(Exception ex)
        => ex is JSDisconnectedException
            or TaskCanceledException
            or ObjectDisposedException
            or InvalidOperationException
            or JSException;

    private sealed record StatusDto(bool Defined, bool Upgraded, int Count);
}
