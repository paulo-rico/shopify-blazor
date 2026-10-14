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
            _module = await _js.InvokeAsync<IJSObjectReference>("import", _options.ModulePath).ConfigureAwait(false);
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
