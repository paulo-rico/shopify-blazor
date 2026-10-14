using Microsoft.AspNetCore.Components;
using Polaris.Blazor.Events;

namespace Polaris.Blazor;

/// <summary>
/// Collects the DOM event → callback mapping a component wants.
/// </summary>
/// <remarks>
/// A component opts in per event rather than publishing every event it has, because an event with
/// no subscriber costs a JavaScript listener and a round trip per occurrence — expensive on
/// Blazor Server, where the round trip is a SignalR message.
/// </remarks>
public readonly struct PolarisEventRegistration
{
    private readonly Dictionary<string, EventCallback<PolarisEventArgs>> _target;

    internal PolarisEventRegistration(Dictionary<string, EventCallback<PolarisEventArgs>> target)
    {
        _target = target;
    }

    /// <summary>
    /// Registers <paramref name="callback"/> for <paramref name="eventName"/>. A callback with no
    /// delegate (the state of an unset <see cref="EventCallback"/>) is ignored, so a component can
    /// pass its parameters through unconditionally.
    /// </summary>
    public void On(string eventName, EventCallback<PolarisEventArgs> callback)
    {
        if (callback.HasDelegate)
        {
            _target[eventName] = callback;
        }
    }

    /// <summary>
    /// Registers a plain .NET handler for <paramref name="eventName"/>.
    /// </summary>
    public void On(string eventName, Func<PolarisEventArgs, Task> handler)
    {
        if (handler is not null)
        {
            _target[eventName] = EventCallback.Factory.Create<PolarisEventArgs>(this, handler);
        }
    }
}
