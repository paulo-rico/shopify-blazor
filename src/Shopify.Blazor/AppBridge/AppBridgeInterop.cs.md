<!-- summary-of: AppBridgeInterop.cs -->
<!-- source-sha256: a2d53e5326fd -->

# AppBridgeInterop.cs

Every App Bridge 4 feature the toolkit uses, reached from C# over `IJSRuntime`. That covers session
tokens, authenticated calls to the app's own endpoints, the Admin's progress bar and toasts,
navigation, the merchant's identity and granted scopes, Shopify's product picker and the admin's contextual save bar; every method
degrades to `false`, `null` or a failed result instead of throwing, so a page that runs outside the
Admin — where `window.shopify` does not exist — still renders and can say why.

`Shopify.Blazor.AppBridge` · App Bridge 4 interop · implements `IAppBridgeInterop`, `IAsyncDisposable`

## Members

| Member | Signature | What it does |
|---|---|---|
| `IsAvailableAsync` | `ValueTask<bool>` | True when the page is embedded in the Admin and App Bridge loaded. |
| `GetIdTokenAsync` | `ValueTask<string?>` | A freshly minted session token, or null. A **session** token for the app's own backend — never an Admin API access token, and never exchanged in the browser. |
| `FetchAsync<T>` | `ValueTask<AppBridgeFetchResult<T>>(string url, string method = "GET", object? body = null)` | Calls one of the app's own endpoints with a fresh session token and deserializes the body as `T`. |
| `SetLoadingAsync` | `ValueTask` | Starts or stops the Admin's header progress bar. |
| `WithLoadingAsync<T>` | `Task<T>` | Runs an action with the progress bar shown and clears it in a `finally`, so a thrown action cannot leave the bar running. |
| `ShowToastAsync` | `ValueTask(string message, bool isError = false, int? durationMs = null)` | Shows a toast; a no-op outside the Admin or when the message is blank. |
| `NavigateAsync` | `ValueTask(string url)` | Navigates within the Admin, or changes `location` outside it. Throws `ArgumentException` for a null or whitespace URL — the only member that validates before interop. |
| `GetCurrentUserAsync` | `ValueTask<ShopifyAppUser?>` | The merchant using the app, or null outside the Admin. |
| `GetGrantedScopesAsync` | `ValueTask<IReadOnlyList<string>?>` | The scopes currently granted, or null when unavailable. |
| `PickProductsAsync` | `ValueTask<IReadOnlyList<ShopifyPickedProduct>?>(int? multipleLimit = null, CancellationToken cancellationToken = default)` | Opens Shopify's product picker. Returns the picked products, an empty list when the merchant chose none, or **null** when the picker is cancelled or unavailable. |
| `ShowSaveBarAsync` | `ValueTask<bool>(string id)` | Asks the Admin to show the contextual save bar belonging to the `<ui-save-bar>` with this id. False outside the Admin, for a blank id, or when App Bridge cannot find the element. |
| `HideSaveBarAsync` | `ValueTask<bool>(string id)` | Hides it again. The caller hides when the form stops being dirty and on teardown, because the bar outlives the component that asked for it. |
| `DisposeAsync` | `ValueTask` | Idempotent. Disposes the imported JS module (swallowing a dead circuit) and the import semaphore; after it every call returns null/false. |

## Supporting types

| Type | Shape | Notes |
|---|---|---|
| `AppBridgeOptions` | `ModulePath` (default `"./_content/Shopify.Blazor/app-bridge.js"`), `PolarisScriptUrl` (default `DesignSystemDefaults.PolarisScriptUrl`), `AppBridgeScriptUrl` (default `DesignSystemDefaults.AppBridgeScriptUrl`) | Registered through `IOptions`; the module path is the package's own static web asset. |
| `DesignSystemDefaults` | `AppBridgeScriptUrl = "https://cdn.shopify.com/shopifycloud/app-bridge.js"`, `PolarisScriptUrl = "https://cdn.shopify.com/shopifycloud/polaris-2.js"` | App Bridge is unversioned by design; `polaris-2.js` tracks the newest stable release in Polaris 2, while pinning `polaris-2.0.js` freezes it. |
| `AppBridgeFetchResult<T>` | `sealed record (bool IsSuccess, int StatusCode, T? Data, string? Error)` plus `static Failed(string error)` | `Failed` is the "never reached the server" shape: `IsSuccess` false, **status 0**, null data. |
| `ShopifyPickedProduct` | `sealed record (string? Id, string? Title, string? Handle, string? ImageUrl, IReadOnlyList<ShopifyPickedVariant> Variants)` | The variant list matters: a shop costs pieces, not products, so a product whose variants were not selected has nothing to price. |
| `ShopifyPickedVariant` | `sealed record (string? Id, string? Title, string? Price, string? Sku, IReadOnlyDictionary<string, string> Options)` | `Price` is a **string**, because that is how Shopify states money. |
| `ShopifyAppUser` | `sealed record (string? Id, string? FirstName, string? LastName, string? Email, string? Locale)` plus `DisplayName` | `DisplayName` joins first and last name, then falls back to email, then id, then `"Unknown user"`. |

## Behaviour

- The JS module is imported once, lazily, on the first call, behind a `SemaphoreSlim` with a
  double-check inside the lock — concurrent first calls produce one `import`. A *failed* import caches
  nothing, so a later call on a live circuit can succeed.
- `ModuleAsync` returns null after disposal, when prerendering, or when the circuit has already gone.
  Every public method treats null as "App Bridge is unavailable" and returns its empty value.
- The exception filters are precise: `JSDisconnectedException` and `JSException` are swallowed by every
  method, and the import additionally swallows `InvalidOperationException` (prerendering). Nothing here
  logs, so an unavailable page has no diagnostic trail of its own.
- `FetchAsync` invokes the module's `fetchWithSessionToken` with `{ method, body }` and expects an
  envelope of `Ok`, `Status`, `Data` (a `JsonElement`) and `Error`. A non-OK envelope returns the status
  with the envelope's error, falling back to "The request failed.".
- On success, `Data` is deserialized with web defaults unless it is JSON null/undefined, in which case
  `Data` stays default — except when `T` is `JsonElement` or `object`, where the raw element is
  returned instead.
- A JS exception during the fetch becomes `Failed("App Bridge could not provide a session token. Open
  this page from inside the Shopify Admin.")`, which is the usual cause rather than a transport fault.
- `ShowToastAsync` returns without calling JS when the message is null/whitespace; it passes
  `{ isError, duration = durationMs }`, so a null duration leaves the duration to App Bridge.
- `PickProductsAsync` maps the JS DTOs with defensive fallbacks: a missing variant array becomes empty,
  a missing options map becomes an empty dictionary. The `CancellationToken` is passed into
  `InvokeAsync`, so cancelling cancels the JS promise.
- `ShowSaveBarAsync` and `HideSaveBarAsync` refuse a blank id before touching JS and otherwise return
  whatever the module reports. The module answers **false** outside the Admin rather than throwing, so
  these two are not how a page learns whether it is embedded — `IsAvailableAsync` is.
- The save bar pair is a bare ask, not state: the element it names is rendered by
  `Shopify.Blazor.Components.ShopifySaveBar`, which owns the id, the two buttons and the rule that a
  show and a hide are sent only when the bar's visibility actually changes.

## Gotchas

- **Null and empty mean different things from the picker, and the difference is the merchant's.** A
  cancelled picker resolves `undefined` rather than an empty array, so "cancelled" arrives as null — the
  same value as "App Bridge unavailable". A screen that treats null like an empty list clears a
  selection the merchant never touched; a screen that treats it as "unavailable" reports the wrong
  reason for a dialog the merchant dismissed.
- **The picker's action is `select`, not App Bridge's default `add`.** The merchant cannot create
  products from inside it, which is intentional: this is for choosing what the shop costs, not for
  editing the catalogue.
- **`variant.price` is a string.** Binding it to a `decimal` fails at runtime rather than at compile
  time, so the DTO keeps the string and the caller parses it.
- **`IsSuccess` can be true with null `Data`.** Any successful response whose body is empty (a `204`,
  for instance) deserializes to default and still reports success, so a caller must tolerate null data
  on the happy path.
- **Status 0 means the request never left the browser.** Every `Failed(...)` result uses it — the
  unavailable module, a null envelope, and the "no session token" catch. Do not map it onto an HTTP
  error or retry it as one.
- **A project that does not ship the module degrades silently.** `ModulePath` points at
  `./_content/Shopify.Blazor/app-bridge.js`; a 404 there makes every method return its empty value with
  no exception — buttons render, requests never happen, and only the browser network log shows why.
- **`AppBridgeOptions.PolarisScriptUrl` is the Polaris channel.** `polaris-2.js` tracks the newest
  stable release inside Polaris 2, so fixes arrive without a code change; pinning `polaris-2.0.js`
  freezes the bundle and makes the move deliberate. There is no equivalent pin for App Bridge — that
  URL always serves the current release.
- Calls after `DisposeAsync` return empty values, and the semaphore is disposed with the service, so
  the service must not be disposed while a first import is still in flight.

## See also

- [../AdminApi/AdminGraphQlClient.cs.md](../AdminApi/AdminGraphQlClient.cs.md) — the server-side GraphQL transport an endpoint uses after verifying the session token.
- [../Internal/ShopDomain.cs.md](../Internal/ShopDomain.cs.md) — validation of the domain a session token claims.
