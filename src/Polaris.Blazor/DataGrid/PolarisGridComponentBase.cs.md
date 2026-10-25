<!-- summary-of: PolarisGridComponentBase.cs -->
<!-- source-sha256: 163ee9c9d480 -->

# PolarisGridComponentBase.cs

Base for a component that renders from a shared `PolarisGridState` and redraws when it changes; the
subscription, not the markup, joins the grid, the filter panel and the pager into one grid. The state
belongs to the page, so this class subscribes, redraws and unsubscribes — and never disposes it.

`Polaris.Blazor.DataGrid` · `public abstract class PolarisGridComponentBase : ComponentBase, IDisposable`

## Members

| Member | Signature | Behaviour |
|---|---|---|
| `BoundState` | `protected abstract PolarisGridState? BoundState { get; }` | The state this component listens to. Its own parameter is usually a `[Parameter] PolarisGridState?` the subclass exposes; returning null is legal and simply means "not bound". |
| `OnParametersSet` | `protected override void OnParametersSet()` | Re-subscribes **only when the instance reference changed**. A null previous or current state is handled: it unsubscribes the old one and subscribes the new one if there is one. |
| `OnStateChanged` | `protected virtual void OnStateChanged()` | Called by the state's `Changed` event. If not disposed, dispatches `StateHasChanged` through `InvokeAsync` and discards the returned task. |
| `Dispose` | `public virtual void Dispose()` | Idempotent: detaches the handler, clears the subscription, suppresses finalisation. Guarded by a `_disposed` flag, so a second call does nothing. |

## Behaviour

- **Re-subscribing is guarded by `ReferenceEquals`.** The comment in the source explains why: the page may
  rebuild the whole parameter set on every render, and an unsubscribe/subscribe pair per render would be a
  handler leak even with one well-behaved caller. So a state instance is subscribed to once, for as long
  as it is the instance in force.
- **The redraw is dispatched, not called directly.** The state can be changed from a handler on the
  renderer's dispatcher, from the completion of a load that started on another thread, or from a test, and
  `ComponentBase.StateHasChanged` asserts it is running on the renderer's dispatcher. Awaiting the
  `InvokeAsync` would make no difference: the render is queued either way, and the batch that owns it
  belongs to the renderer.
- **Nothing here talks to a sibling.** The grid, the filter and the pager each hold their own subscription
  and each redraw from the state; the source says so explicitly, and it is also why the state is a plain
  object rather than a component — three components can share it and none of them owns it.

## Gotchas

- **Never dispose the state from a component built on this base.** The page created it and the page
  disposes it; a child that disposed a shared object would take the other two components down with it. The
  remarks in the source say this outright, and `Dispose` here touches only the handler.
- **The subscription is to `Changed`, nothing else.** Mutating the state object by any route that does not
  eventually call `RaiseChanged` — which in practice means writing to `Query` or to the item collection
  directly — redraws no component at all.
- **A `BoundState` that returns a new instance per call re-subscribes on every parameter pass.** The guard
  only catches the same reference, so a state constructed inside a getter defeats it and quietly churns
  handlers.
- **An override of `Dispose` must call `base.Dispose()`.** It is `virtual`, not sealed, and a subclass that
  forgets leaves the handler attached to a page-owned state after the component is gone.
- **This class is `IDisposable`, not `IAsyncDisposable`.** Disposal is synchronous; a subclass needing an
  async teardown has no hook here and has to add its own.

## See also

- [PolarisGridState.cs.md](PolarisGridState.cs.md) — the shared state, its `Changed` event and its
  ownership rules.
- [PolarisDataGrid.cs.md](../Components/PolarisDataGrid.cs.md) — the component that renders the rows.
- [PolarisDataFilter.cs.md](../Components/PolarisDataFilter.cs.md) — a second subscriber to the same state.
- [PolarisDataPager.cs.md](../Components/PolarisDataPager.cs.md) — the third.
