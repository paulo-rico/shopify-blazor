<!-- summary-of: ShopifyServiceCollectionExtensions.cs -->
<!-- source-sha256: 889a45dc4191 -->

# ShopifyServiceCollectionExtensions.cs

The toolkit's composition root: one `AddShopifyBlazor` call binds the `Shopify` settings, applies the
environment-variable overrides the deployment relies on, registers the two HTTP clients, the token
validator, exchanger, store and Admin API client, the App Bridge interop and the session-token
authentication scheme.

`Shopify.Blazor` · `public static class` extension method · no base class

## Members

| Member | Signature | What it does |
|---|---|---|
| `AddShopifyBlazor` | `public static IServiceCollection AddShopifyBlazor(this IServiceCollection services, IConfiguration? configuration = null, Action<ShopifyOptions>? configure = null, bool validateOnStart = true)` | Registers everything below and returns `services` for chaining. |

| Argument | Type | Default | What it does |
|---|---|---|---|
| `services` | `IServiceCollection` | — | The collection to add to. The toolkit's own service registrations use `TryAdd…`, so one made earlier is kept. |
| `configuration` | `IConfiguration?` | `null` | When supplied, binds its `Shopify` section to `ShopifyOptions` and its `Shopify:AppBridge` section to `AppBridgeOptions`. `null` leaves options to the environment variables, the callback and the property defaults. |
| `configure` | `Action<ShopifyOptions>?` | `null` | Inline configuration, applied **after** the configuration section and after the environment-variable layer, so it wins over both. |
| `validateOnStart` | `bool` | `true` | Registers a validator that throws when `ApiKey` or `ApiSecret` is missing. See the Gotchas for when it actually fires. |

### What is registered

| Service | Implementation | Lifetime |
|---|---|---|
| `IShopifyAccessTokenStore` | `InMemoryShopifyAccessTokenStore` | Singleton |
| `IShopifySessionTokenValidator` | `ShopifySessionTokenValidator` | Singleton |
| `IShopifyTokenExchanger` | `ShopifyTokenExchanger` | Singleton |
| `IAdminGraphQlClient` | `AdminGraphQlClient` | Singleton |
| `IShopifyRequestAuthenticator` | `ShopifyRequestAuthenticator` | Scoped |
| `IShopifyAdminApi` | `ShopifyAdminApi` | Scoped |
| `IAppBridgeInterop` | `AppBridgeInterop` | Scoped |
| `ISessionTokenRejectionLog` | `SessionTokenRejectionLog` | Singleton, deliberately — a rejection recorded in one request must be readable from the next |

Plus: `IMemoryCache`, the two named HTTP clients (`TokenExchange`, 15 s; `AdminApi`, 30 s), the
authentication scheme `ShopifyAuthenticationDefaults.Scheme` with
`ShopifySessionTokenAuthenticationHandler`, and `AddAuthorization()`.

## Behaviour

- **Three configuration layers, in this order:** the bound `Shopify` section, then
  `SHOPIFY_API_KEY` / `SHOPIFY_API_SECRET` / `SHOPIFY_APP_URL` (each only when the variable is non-null;
  an empty variable is *not* skipped here), then the caller's `configure` callback. This is the code that
  implements "an environment variable overrides appsettings and user secrets".
- **The scheme is made the default.** `AddAuthentication(scheme)` sets the default authenticate,
  challenge and forbid schemes, which is what lets `[Authorize]` on a page use the session-token handler
  without naming it.
- **The rejection log is a singleton for a reason:** the browser sees only a 401, and the next request —
  possibly a diagnostics page — reads back why the previous one was refused.
- **No middleware and no Razor setup is added.** The method only fills the service collection; the host
  still composes its pipeline and calls `UseAuthentication`/`UseAuthorization` itself.

## Gotchas

- **`validateOnStart` does not validate at startup.** The call is `OptionsBuilder.Validate`, not
  `ValidateOnStart()`, so the check runs when `ShopifyOptions` is first resolved — on the first request
  that needs it — rather than while the host is building. The sample and the template pass
  `validateOnStart: shopifyConfigured`, which is false on an unconfigured checkout precisely so the app
  still starts and can show its diagnostics.
- **`validateOnStart: true` demands the secret, so a WebAssembly host must pass `false`.** A browser
  client legitimately has only the API key; leaving the default makes it fail at first use.
- **`Options.Create(new AppBridgeOptions())` closes `IOptions<AppBridgeOptions>` over a fixed instance,
  and `AppBridgeInterop` takes `IOptions<>` rather than the monitor.** The `Shopify:AppBridge` section
  configured immediately afterwards therefore does not change what the interop sees; set the values on
  `AppBridgeOptions` defaults or register your own `IOptions<AppBridgeOptions>` before this call.
- **All registrations are `TryAdd…`.** A service registered before the call wins, and calling the method
  twice does not duplicate anything.
- **The inline callback can silently defeat the environment.** Because it runs last, a `configure` that
  assigns `ApiKey` overrides `SHOPIFY_API_KEY` — useful in tests, a trap in a container deployment.
- **`AddMemoryCache` is required, not incidental.** `ShopifyTokenExchanger` caches access tokens there;
  removing it or replacing the cache changes how often Shopify is called, not just performance.
- **Nothing here validates `AppUrl`, `Scopes` or the version format**, and nothing checks that the
  registered `IShopifyAccessTokenStore` is shared. A multi-instance deployment that keeps the default
  in-memory store exchanges a token per instance.

## See also

- [ShopifyOptions.cs.md](ShopifyOptions.cs.md) — the options this binds and validates.
- [ShopifyRequestAuthenticator.cs.md](ShopifyRequestAuthenticator.cs.md) — a scoped registration, explained.
- [Authentication/ShopifySessionTokenAuthenticationHandler.cs.md](Authentication/ShopifySessionTokenAuthenticationHandler.cs.md) — the scheme registered here.
- [AppBridge/AppBridgeInterop.cs.md](AppBridge/AppBridgeInterop.cs.md) — `AppBridgeOptions` and the client whose options this configures.
