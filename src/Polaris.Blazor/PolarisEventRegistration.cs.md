<!-- summary-of: PolarisEventRegistration.cs -->
<!-- source-sha256: f55e84076d86 -->

# PolarisEventRegistration.cs

The per-render set of DOM events a wrapper opts into, so that only events with a subscriber cost a
JavaScript listener and a SignalR round trip on Blazor Server. `ConfigureEvents` fills it once per
parameter pass.

`Polaris.Blazor` · `readonly struct` · handed to `PolarisElementComponentBase.ConfigureEvents`

## Members

| Member | Signature | What it does |
|---|---|---|
| `On` (callback) | `public void On(string eventName, EventCallback<PolarisEventArgs> callback)` | Records the callback under the DOM event name. A callback with no delegate — an unset `EventCallback` — is ignored, so a wrapper can pass every parameter through unconditionally. |
| `On` (handler) | `public void On(string eventName, Func<PolarisEventArgs, Task> handler)` | Wraps a plain .NET handler in `EventCallback.Factory.Create<PolarisEventArgs>` and records it. A `null` handler is ignored. |
| constructor | `internal PolarisEventRegistration(Dictionary<string, EventCallback<PolarisEventArgs>> target)` | Internal on purpose: only the base class creates a registration, by handing it the dictionary it just allocated. |

## Behaviour

- **The lifecycle is one pass deep.** `PolarisElementComponentBase.OnParametersSet` creates a fresh
  `Dictionary<string, EventCallback<PolarisEventArgs>>` with `StringComparer.Ordinal`, wraps it in a
  registration and calls `ConfigureEvents`. `OnAfterRenderAsync` then passes the dictionary — names
  and callbacks — to `IPolarisInterop.AttachEventsAsync`, which registers a listener per name.
- **Opting in is the point.** A component publishes only the events it declares as parameters, so a
  button with no `OnBlur` binding gets no blur listener. Attaching all of an element's events would
  cost a JavaScript listener and a round trip for every event the page ignores.
- **Last call wins per event name.** `On` assigns into the dictionary, so registering the same name
  twice replaces the earlier callback rather than throwing or adding a second listener.
- Event names come from the `PolarisEvents` constants in `Events/PolarisEventArgs.cs`, whose values
  are the browser's own all-lower-case names: `click`, `input`, `change`, `focus`, `blur`, `dismiss`,
  `show`, `hide`, `remove`, `nextpage`, `previouspage`, `afterhide`, `load`, `error`, `invalid`,
  `viewchange`, `droprejected`. The constant names are camelCase (`NextPage`), the values are not.

## Gotchas

- **Nothing validates the event name.** It goes straight to `addEventListener`, so a name the browser
  never emits (for example `Click` with a capital, or the element's own property name instead of the
  DOM event) registers a listener that never fires — no error, no warning.
- **The map is rebuilt every pass, but the base re-attaches only when it is non-empty.** If every
  subscriber disappears between renders, the listeners attached on the previous render stay attached
  until `DisposeAsync` detaches them; the callbacks in the browser still point at the older map.
- **An unset callback is skipped silently by design**, so this is safe:
  `events.On(PolarisEvents.Blur, OnBlur)` whether or not the page bound `OnBlur`. The flip side is
  that a wrapper which forgets to call `On` for a parameter it exposes never fails — the event simply
  does not arrive.
- **Only `EventCallback` and `Func<…, Task>` overloads exist.** There is no `Action` overload, so an
  async handler that returns `Task` cannot be bound to the `Action<T>` overload and silently dropped
  the way `EventCallback.Factory.Create<T>(this, _ => SomeAsyncMethod())` can be elsewhere in this
  codebase.
- The struct holds only a reference to the dictionary the base allocated, so passing it by value into
  `ConfigureEvents` is harmless: every `On` call writes into that one dictionary, which the base reads
  immediately afterwards. The second overload also passes the struct as the `EventCallback` receiver,
  which is irrelevant unless a handler implements `IHandleEvent`.
- An exception thrown by a handler propagates out of the interop call into the renderer, as with any
  Blazor event callback.

## See also

- [PolarisElementComponentBase.cs.md](PolarisElementComponentBase.cs.md) — creates the dictionary,
  calls `ConfigureEvents`, and attaches the result after render.
- [Events/PolarisEventArgs.cs.md](Events/PolarisEventArgs.cs.md) — the `PolarisEvents` name constants
  and the args a handler receives.
- [Components/PolarisButton.cs.md](Components/PolarisButton.cs.md) — a three-event registration in
  practice (`click`, `blur`, `focus`).
