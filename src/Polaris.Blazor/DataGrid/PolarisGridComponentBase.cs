using Microsoft.AspNetCore.Components;

namespace Polaris.Blazor.DataGrid;

/// <summary>
/// Base class for a component that renders from a <see cref="PolarisGridState"/> and has to redraw
/// when it changes.
/// </summary>
/// <remarks>
/// <para>
/// The grid, the filter and the pager are separate components sharing one state, so the only thing
/// that makes them one grid is this subscription: each of them redraws when the state changes, and
/// none of them talks to another directly. That is also why the state is a plain object and not a
/// component — three components can share it, and none of them owns it.
/// </para>
/// <para>
/// A component built on this base never disposes the state. The page created it, and the page
/// disposes it; a child that disposed a shared object would take the other two components down with
/// it.
/// </para>
/// </remarks>
public abstract class PolarisGridComponentBase : ComponentBase, IDisposable
{
    private PolarisGridState? _subscribed;
    private bool _disposed;

    /// <summary>The state this component is currently listening to.</summary>
    protected abstract PolarisGridState? BoundState { get; }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        base.OnParametersSet();

        // Re-subscribing only when the instance really changed matters: the page may rebuild the
        // whole parameter set on every render, and an unsubscribe/subscribe pair per render would be
        // a handler leak with a single, well-behaved caller.
        if (ReferenceEquals(_subscribed, BoundState))
        {
            return;
        }

        if (_subscribed is not null)
        {
            _subscribed.Changed -= OnStateChanged;
        }

        _subscribed = BoundState;

        if (_subscribed is not null)
        {
            _subscribed.Changed += OnStateChanged;
        }
    }

    /// <summary>Redraws after the state changed.</summary>
    /// <remarks>
    /// Dispatched rather than called directly. The state can be changed from anywhere — a handler on
    /// this dispatcher, the completion of a load that started on another thread, or a test — and
    /// <see cref="ComponentBase.StateHasChanged"/> asserts it is running on the renderer's dispatcher.
    /// Awaiting or not makes no difference here: the render is queued, and the batch that owns it is
    /// the renderer's.
    /// </remarks>
    protected virtual void OnStateChanged()
    {
        if (!_disposed)
        {
            _ = InvokeAsync(StateHasChanged);
        }
    }

    /// <summary>Detaches from the state. The state itself is left alone: the page owns it.</summary>
    public virtual void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_subscribed is not null)
        {
            _subscribed.Changed -= OnStateChanged;
            _subscribed = null;
        }

        GC.SuppressFinalize(this);
    }
}
