<!-- summary-of: PolarisEventDispatcher.cs -->
<!-- source-sha256: b6cb969ba8c7 -->

# PolarisEventDispatcher.cs

Receives forwarded DOM events from JavaScript and routes each to the component that registered it, using
a handle instead of one .NET object reference per component.

`Polaris.Blazor.Interop` · sealed, `IDisposable`, not a component · the callback target `PolarisInterop` hands to JavaScript

## Members

| Member | Signature | What it does |
|---|---|---|
| `RegistrationCount` | `int RegistrationCount { get; }` | How many event registrations are currently held. Used by tests and diagnostics — not by the components. |
| `Register` | `void Register(string handle, IReadOnlyDictionary<string, EventCallback<PolarisEventArgs>> registrations)` | Associates a handle with the callbacks it should invoke. Registering an existing handle **replaces** its entry silently. |
| `Unregister` | `void Unregister(string handle)` | Removes a registration. Safe to call for an unknown handle. |
| `Dispatch` | `[JSInvokable] Task Dispatch(string handle, string eventName, PolarisEventArgs args)` | Invoked from JavaScript for every forwarded DOM event. Looks up the handle, then the event name, then invokes the callback. |
| `Dispose` | `void Dispose()` | Clears the whole registration map. |

## Behaviour

- **One dispatcher serves every component on the circuit.** JavaScript holds a single
  `DotNetObjectReference<PolarisEventDispatcher>` and identifies the target with the handle it was given at
  registration, so the number of .NET references is independent of the number of components on the page —
  which matters on Blazor Server, where every reference is a live object in a long-running circuit.
- **`Dispatch` is tolerant, in three steps.** An unknown handle returns silently; a handle with no callback
  for `eventName`, or a callback whose `HasDelegate` is false, returns silently; otherwise the payload is
  invoked. A missing payload is replaced with an empty `PolarisEventArgs`, and `Type` is filled from
  `eventName` when the payload did not carry one.
- **The callback is awaited.** `InvokeAsync` is awaited with `ConfigureAwait(false)`, so a handler that
  returns a `Task` is not fire-and-forget from the dispatcher's point of view.
- The map is a `ConcurrentDictionary` with an `Ordinal` comparer: handles are GUID strings and event names
  are DOM names, so neither wants a culture-aware comparison.

## Gotchas

- **An unknown handle is dropped on purpose.** The component was disposed between the event firing and the
  call arriving, which is ordinary during navigation. Dropping it silently is correct — but it means a
  dispatch that "does nothing" has no diagnostic at all beyond a `RegistrationCount` that does not include
  the handle.
- **`Register` overwrites.** Two registrations sharing a handle means the first is gone with no error, and
  the handle is a GUID generated per `AttachEventsAsync` call, so that is only reachable by a caller that
  supplies its own.
- **`DetachEventsAsync` on `PolarisInterop` does not come through here.** It releases the DOM listeners by
  element; this map is only pruned per-handle on a failed attach, and cleared as a whole by `Dispose`. Do
  not read `RegistrationCount` as "how many components have a listener attached".
- **`Dispose` does not await or drain in-flight dispatches.** It clears the dictionary, so a dispatch
  already past the lookup still runs its callback.
- The dispatcher never catches an exception from the callback: a throwing handler surfaces to the
  JavaScript caller rather than being swallowed here.

## See also

- [PolarisInterop.cs.md](PolarisInterop.cs.md) — creates the dispatcher, exposes `Dispatcher` and
  `DispatchAsync`, and owns its lifetime.
- [../Events/PolarisEventArgs.cs.md](../Events/PolarisEventArgs.cs.md) — the payload passed through
  untouched apart from `Type`.
- [../PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md) — builds the registration
  dictionary and hands it to `AttachEventsAsync`.
