<!-- summary-of: PolarisServiceCollectionExtensions.cs -->
<!-- source-sha256: 698afe8447bc -->

# PolarisServiceCollectionExtensions.cs

The one registration an app needs to use the Polaris components: `AddPolarisBlazor()` puts the JavaScript
interop service the wrappers inject into dependency injection. It registers nothing else, so the
component library can be adopted without the rest of the toolkit.

`Polaris.Blazor` · `static class` of `IServiceCollection` extensions · registers `IPolarisInterop`

## Members

| Member | Signature | What it does |
|---|---|---|
| `AddPolarisBlazor` | `static IServiceCollection AddPolarisBlazor(this IServiceCollection services, Action<PolarisInteropOptions>? configure = null)` | Registers options for the interop layer, applies `configure` when it is supplied, and adds `IPolarisInterop` → `PolarisInterop` as scoped unless an `IPolarisInterop` is already registered. Returns the same collection for chaining. |

What ends up in the collection:

| Service | Lifetime | What it is |
|---|---|---|
| `IOptions<PolarisInteropOptions>` | options | Added with `AddOptions`, so `PolarisInterop`'s constructor can take it. The options hold `ModulePath` (default `./_content/Polaris.Blazor/polaris-interop.js`), `ComponentReadyTimeout` (default ten seconds) and `EnableEvents` (default true). |
| `IPolarisInterop` → `PolarisInterop` | scoped, first-wins | The single JavaScript entry point: loads the ES module, waits for an element to upgrade, writes DOM properties, attaches and detaches listeners, and calls element methods such as `showOverlay`. |

## Behaviour

- **The library has no other service dependencies, by design.** Its own remark says it wraps Shopify's
  components and nothing else, so an app can take the components without the Shopify authentication,
  Admin API or App Bridge half of the toolkit — and that is why this method does not call into
  `AddShopifyBlazor`: the two are registered separately, and the sample app and the project template
  call both.
- **Scoped, not singleton.** `PolarisInterop` holds the loaded module reference, a
  `DotNetObjectReference` to its event dispatcher, and a semaphore guarding module load — state that
  belongs to one circuit, not to the process. It also implements `IAsyncDisposable`, so the dispatcher
  is released when the circuit ends.
- **The event dispatcher is not in DI.** `PolarisEventDispatcher` is constructed inside `PolarisInterop`
  and exposed through its `Dispatcher` property; there is no registration to resolve or replace.
- **`configure` is optional and additive.** `services.Configure(configure)` is called only when the
  action is not null; with a null argument the defaults above are exactly what is in force.
- `AddPolarisBlazor` returns the collection, so it can sit in a chain with the framework's own
  registrations. Registration is all it does: nothing is loaded, connected or validated until a
  component first renders and asks for the interop.

## Gotchas

- **`TryAddScoped` is first-wins, in both directions.** An `IPolarisInterop` already registered — a test
  double, or an app's own implementation — is kept and the default `PolarisInterop` is never
  constructed; one registered *after* this call is ignored, because the descriptor from here is already
  in the collection. Either way there is no error, only a component talking to a service the caller did
  not expect.
- **Registering the service is not the same as shipping the JavaScript.** `ModulePath` points into the
  Razor class library's static web assets; a package that built with `Microsoft.NET.Sdk` instead of
  `Microsoft.NET.Sdk.Razor` has the file in `wwwroot` and serves a 404 for it, so the markup renders
  perfectly and every control does nothing. The only symptom is the log line about the Polaris interop
  module failing to load.
- **A bad `ModulePath` fails late.** Nothing validates the string at startup, so a typo surfaces on the
  first render, as a component that never initialises rather than as an exception at boot.
- **`ComponentReadyTimeout` too low turns a slow CDN into a broken page.** The interop waits that long
  for the custom element to be defined before giving up, so a ten-second default is a floor for a cold
  load, not a generous one.
- **`EnableEvents = false` means callbacks never fire.** The remark says it is for static rendering, and
  it is worth taking literally: it does not disable click handling only, it stops DOM events being
  forwarded at all, so every `EventCallback` in the tree stays silent.
- **Calling it twice is safe but not idempotent for configuration.** The service registrations are
  try-adds, yet each non-null `configure` action is appended, and all of them run in registration order —
  so two calls both take effect, with the later one winning per property.

## See also

- [PolarisInterop.cs.md](Interop/PolarisInterop.cs.md) — the interface and implementation registered
  here, including what it waits for and what it writes.
- [PolarisEventDispatcher.cs.md](Interop/PolarisEventDispatcher.cs.md) — the routing object the interop
  owns, which this method deliberately does not register.
- [PolarisElementComponentBase.cs.md](PolarisElementComponentBase.cs.md) — the `[Inject]`ed `Interop`
  every wrapper uses, and therefore the reason the registration exists.
