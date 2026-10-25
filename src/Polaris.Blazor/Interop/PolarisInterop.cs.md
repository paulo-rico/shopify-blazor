<!-- summary-of: PolarisInterop.cs -->
<!-- source-sha256: f81823807ce3 -->

# PolarisInterop.cs

The single JavaScript entry point for the component library: loads the ES module, writes DOM properties
attributes cannot carry, attaches and releases event listeners, and moves focus across a shadow boundary —
so no component ever touches `IJSRuntime` itself.

`Polaris.Blazor.Interop` · `IPolarisInterop` + `PolarisInterop` + options and a status record · not a component, registered as a service

## Options — `PolarisInteropOptions`

| Property | Type | Default | What it does |
|---|---|---|---|
| `ModulePath` | `string` | `"./_content/Polaris.Blazor/polaris-interop.js"` | Path of the module that ships with the package, served as a static web asset from the Razor class library so a consuming app does not copy it. |
| `ComponentReadyTimeout` | `TimeSpan` | `TimeSpan.FromSeconds(10)` | Documented as how long to wait for a Polaris custom element to be defined before giving up. **Nothing reads it** — not this file, and nothing else under `src/` — so changing it currently has no effect. |
| `EnableEvents` | `bool` | `true` | When false, DOM events are not forwarded and callbacks never fire. Intended for static rendering. |

## Members — `IPolarisInterop`

| Member | Signature | What it does |
|---|---|---|
| `EnsureInitializedAsync` | `ValueTask<bool> EnsureInitializedAsync()` | Loads the module once. Safe to call repeatedly; returns false rather than throwing when interop is unavailable. |
| `EnsureIdAsync` | `ValueTask<string?> EnsureIdAsync(ElementReference element)` | Gives the element an id if it has none and returns it, for `commandFor` / `interestFor`. Null when interop is unavailable. |
| `AttachEventsAsync` | `ValueTask AttachEventsAsync(ElementReference element, string tagName, IEnumerable<string> eventNames, IReadOnlyDictionary<string, EventCallback<PolarisEventArgs>> registrations)` | Attaches the named DOM events and forwards them to the registrations. Skipped entirely when `EnableEvents` is false or there is nothing to register. |
| `DetachEventsAsync` | `ValueTask DetachEventsAsync(ElementReference element)` | Releases every listener attached to the element. |
| `InvokeElementMethodAsync` | `ValueTask InvokeElementMethodAsync(ElementReference element, string methodName, params object?[] arguments)` | Calls a method on the element itself — `showOverlay` / `hideOverlay` on a modal. Polaris only defines its methods once the element has upgraded, so the call waits. |
| `SetPropertiesAsync` | `ValueTask SetPropertiesAsync(ElementReference element, string tagName, IReadOnlyDictionary<string, object?> values)` | Sets DOM properties attributes cannot carry, after the element upgrades. No-op for an empty dictionary. |
| `ClearPropertiesAsync` | `ValueTask ClearPropertiesAsync(ElementReference element, string tagName, IEnumerable<string> names)` | Clears previously set array-valued properties, so a value the server dropped cannot linger in the element. |
| `FocusWithinAsync` | `ValueTask<bool> FocusWithinAsync(ElementReference element, string? selector = null)` | Moves keyboard focus onto the first field inside the element, or onto the element itself when it is the focusable thing. Returns false when nothing focusable was found. |
| `SetAriaExpandedAsync` | `ValueTask<bool> SetAriaExpandedAsync(ElementReference element, bool expanded)` | Writes `aria-expanded` onto the `<button>` an element renders **inside its own shadow root**. Returns false when there is no button. |
| `GetComponentStatusAsync` | `ValueTask<IReadOnlyDictionary<string, PolarisComponentStatus>> GetComponentStatusAsync(IEnumerable<string> tagNames)` | Reports which custom elements are registered and upgraded on the current page, and how many exist. Empty dictionary when interop is unavailable. |
| `IsEmbeddedAsync` | `ValueTask<bool> IsEmbeddedAsync()` | True when the page is running inside the Shopify Admin. False on any failure. |
| `DisposeAsync` | `ValueTask DisposeAsync()` | Disposes the module reference, the dispatcher reference, the dispatcher and the gate. Idempotent. |

**`PolarisComponentStatus`** — `record PolarisComponentStatus(bool Defined, bool Upgraded, int Count)`:
the element has a definition, every matching element on the page is an instance of it, and how many
matching elements there are.

## Members — `PolarisInterop`

| Member | Signature | What it does |
|---|---|---|
| Constructor | `PolarisInterop(IJSRuntime js, IOptions<PolarisInteropOptions> options, ILogger<PolarisInterop>? logger = null)` | Throws on a null `js`; falls back to a default options instance when `options?.Value` is null. |
| `Dispatcher` | `PolarisEventDispatcher Dispatcher { get; }` | The callback target JavaScript invokes, exposed so the register/dispatch/unregister path can be exercised without a browser. |
| `DispatchAsync` | `Task DispatchAsync(string handle, string eventName, PolarisEventArgs args)` | Handles a DOM event as if JavaScript had forwarded it. |
| `RegistrationCount` | `int RegistrationCount { get; }` | How many component event registrations are currently held. |

## Behaviour

- **One module, loaded once, behind a `SemaphoreSlim(1, 1)`.** The double-check inside the gate means
  concurrent first renders do not import twice; the module reference is cached for the life of the service.
- **One `DotNetObjectReference<PolarisEventDispatcher>` for the whole circuit.** JavaScript receives it at
  attach time along with a fresh GUID handle (`Guid.NewGuid().ToString("n")`), and routes every event
  through the dispatcher.
- **The JavaScript functions it calls**, by name: `ensureId`, `attachEvents` (element, event names,
  dispatcher reference, handle, tag name), `release` (element), `setProperties`, `clearProperties` (element,
  tag name, names), `focusWithin` (element, selector), `setShadowButtonExpanded` (element, expanded),
  `invokeElementMethod` (element, method, arguments), `componentStatus` (tag names) and `isEmbedded`.
- **Failure is data, not an exception.** Almost every method catches "recoverable" exceptions and returns
  null/false/empty: `JSDisconnectedException`, `TaskCanceledException`, `ObjectDisposedException`,
  `InvalidOperationException` (no interop during static prerendering) and `JSException`.
- **One path logs at error level.** A `JSException` while importing the module means no event ever reaches
  .NET — every button looks correct and does nothing — so it is logged as an error naming the module path.
- **`FocusWithinAsync` reports a miss at debug level.** A false result logs "found nothing to focus inside
  '{Selector}'", because the visible symptom is only that the editor appears and the next keystroke goes to
  the page.
- **A failed attach is rolled back.** `AttachEventsAsync` registers the handle with the dispatcher *before*
  calling JavaScript, and unregisters it if the call fails recoverably, so a dead circuit leaves no
  callback behind.
- **`GetComponentStatusAsync` maps a private DTO.** The JavaScript returns a dictionary of
  `{ Defined, Upgraded, Count }` records; they are copied into `PolarisComponentStatus` so the public
  contract does not depend on the interop shape.

## Gotchas

- **`EnsureInitializedAsync` returning false is the normal state for prerendering, and it is silent.**
  Every caller returns null/false rather than throwing, so markup renders and nothing is attached. Several
  methods can therefore "succeed" with no effect: `EnsureIdAsync` returns null, `AttachEventsAsync` returns
  having registered nothing.
- **The module-load failure is visible only through a logger.** The constructor's logger is optional and
  the error is the single trace, so a registration that omits `ILogger<PolarisInterop>` turns "no
  JavaScript shipped" into "the page renders and nothing works" with no log line.
- **`FocusWithinAsync` needs the selector whenever the container holds more than one focusable thing.** The
  first match is not necessarily the one meant: a grid's first focusable element is the select-all checkbox
  in its header, not the cell editor, which is why the grid passes `input, textarea, select, [tabindex]`.
- **`SetAriaExpandedAsync` exists for `s-clickable`, which has no expanded property.** `s-button` has
  `accessibilityExpanded` and should set the property instead. The shadow button is re-created whenever
  Polaris renders, so the attribute has to be re-applied on every render.
- **`ClearPropertiesAsync` is about array-valued properties.** A scalar is overwritten by the next
  `SetPropertiesAsync`; an array the server stopped sending would otherwise keep its last value.
- **`DetachEventsAsync` releases by element, not by handle.** It calls `release(element)` and does not
  `Unregister` the GUID handle it was attached under; the handle map is pruned only on a failed attach and
  cleared as a whole in `DisposeAsync`.
- **`ComponentReadyTimeout` is declared and never read anywhere.** It is an option with no consumer in
  this repository — the JavaScript module cannot see a C# value — so a shorter timeout buys nothing until
  something is wired to it.

## See also

- [PolarisEventDispatcher.cs.md](PolarisEventDispatcher.cs.md) — where forwarded events land.
- [../Events/PolarisEventArgs.cs.md](../Events/PolarisEventArgs.cs.md) — the payload these listeners produce.
- [../PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md) — the caller that queues
  property writes and re-attaches listeners through this interface.
- [polaris-interop.js.md](../wwwroot/polaris-interop.js.md) — the module whose exported functions are
  named above.
