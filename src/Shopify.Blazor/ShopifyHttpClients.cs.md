<!-- summary-of: ShopifyHttpClients.cs -->
<!-- source-sha256: 44629661c23e -->

# ShopifyHttpClients.cs

The two names the toolkit uses for its named `HttpClient` instances: one for the secret-bearing token
exchange, one for ordinary Admin API calls. Nothing here creates a client — it is only the pair of
strings that registration and the two callers must agree on.

`Shopify.Blazor` · `public static class` · named-client name constants

## Members

| Member | Signature | Value | What it does |
|---|---|---|---|
| `TokenExchange` | `public const string` | `"Shopify.TokenExchange"` | Name of the client used for the RFC 8693 token exchange, the one request whose body carries the app secret. |
| `AdminApi` | `public const string` | `"Shopify.AdminApi"` | Name of the client used for Admin API GraphQL POSTs. |

## Behaviour

- `AddShopifyBlazor` registers both names through `IHttpClientFactory`: a 15-second timeout on
  `TokenExchange` and 30 seconds on `AdminApi`. A client requested under a name nobody registered is
  still returned — unconfigured, with `HttpClient`'s default 100-second timeout — so a mismatch is
  silent rather than an error.
- The separation is deliberate. The exchange is the only call that receives `client_secret` and whose URL
  is built from an attacker-supplied shop domain; keeping it on its own named client means it never
  shares a handler, a default header or a timeout with an ordinary API call.
- The class holds no registration and no base address. The consumers build absolute URLs and ask the
  factory for the client by one of these names.

## Gotchas

- A string literal instead of the constant compiles and runs. A typo produces an unconfigured client and
  a failure that reads like a network fault, so always reference the constant.
- Bypassing the factory (`new HttpClient()`) skips the registration entirely, including the timeouts
  above — the name has no effect on a directly constructed client.

## See also

- [ShopifyServiceCollectionExtensions.cs.md](ShopifyServiceCollectionExtensions.cs.md) — registers both clients and their timeouts.
- [Authentication/ShopifyTokenExchanger.cs.md](Authentication/ShopifyTokenExchanger.cs.md) — the `TokenExchange` consumer.
- [AdminApi/AdminGraphQlClient.cs.md](AdminApi/AdminGraphQlClient.cs.md) — the `AdminApi` consumer.
