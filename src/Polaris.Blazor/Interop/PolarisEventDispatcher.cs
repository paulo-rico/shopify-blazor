using System.Collections.Concurrent;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Polaris.Blazor.Events;

namespace Polaris.Blazor.Interop;

/// <summary>
/// Receives forwarded DOM events and routes them to the component that registered them.
/// </summary>
/// <remarks>
/// One dispatcher instance serves every component on the circuit: JavaScript holds a single
/// <see cref="DotNetObjectReference{T}"/> and identifies the target with the handle it was given
/// at registration. The number of .NET references is therefore independent of the number of
/// components on the page, which matters on Blazor Server where every reference is a live object
/// in a long-running circuit.
/// </remarks>
public sealed class PolarisEventDispatcher : IDisposable
{
    private readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, EventCallback<PolarisEventArgs>>> _registrations =
        new(StringComparer.Ordinal);

    /// <summary>How many event registrations are currently held. Used by tests and diagnostics.</summary>
    public int RegistrationCount => _registrations.Count;

    /// <summary>Associates a handle with the callbacks it should invoke.</summary>
    public void Register(string handle, IReadOnlyDictionary<string, EventCallback<PolarisEventArgs>> registrations)
        => _registrations[handle] = registrations;

    /// <summary>Removes a registration. Safe to call for an unknown handle.</summary>
    public void Unregister(string handle)
        => _registrations.TryRemove(handle, out _);

    /// <summary>
    /// Invoked from JavaScript for every forwarded DOM event.
    /// </summary>
    /// <param name="handle">The registration handle.</param>
    /// <param name="eventName">The DOM event name.</param>
    /// <param name="args">The normalized event payload.</param>
    [JSInvokable]
    public async Task Dispatch(string handle, string eventName, PolarisEventArgs args)
    {
        if (!_registrations.TryGetValue(handle, out var registrations))
        {
            // The component was disposed between the event firing and this call arriving, which
            // is ordinary during navigation. Dropping it silently is correct.
            return;
        }

        if (!registrations.TryGetValue(eventName, out var callback) || !callback.HasDelegate)
        {
            return;
        }

        var payload = args ?? new PolarisEventArgs();
        payload.Type ??= eventName;

        await callback.InvokeAsync(payload).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Dispose() => _registrations.Clear();
}
