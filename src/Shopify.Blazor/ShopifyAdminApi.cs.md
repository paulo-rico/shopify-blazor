<!-- summary-of: ShopifyAdminApi.cs -->
<!-- source-sha256: 763254f9215f -->

# ShopifyAdminApi.cs

The convenience layer most application code should use: pass an App Bridge session token and a GraphQL
document and get the data back. It authenticates and resolves an access token on every call, so a
long-running Blazor circuit can never outlive its authorisation, and a rejected token is discarded from
both caches before the failure is thrown.

`Shopify.Blazor` · `IShopifyAdminApi` + `sealed class ShopifyAdminApi` + `ShopifyAdminApiException` (base `Exception`)

## Members

**`ShopifyAdminApiException`** — everything this facade throws.

| Member | Signature | What it does |
|---|---|---|
| constructor | `ShopifyAdminApiException(string message, string? shopDomain = null, AdminGraphQlResponse? response = null, Exception? inner = null)` | The message becomes `Exception.Message`; the response, when supplied, is what the two computed flags read. |
| `ShopDomain` | `string?` | The shop the call was made against, or `null` when the failure happened before a shop was known (a session-token rejection). |
| `Response` | `AdminGraphQlResponse?` | The raw GraphQL response, or `null` for an authentication failure or a transport failure. |
| `IsTokenRejected` | `bool` | `true` when the response's status is 401 or 403. The caller may retry: the token was discarded, so a retry re-exchanges. |
| `IsAccessDenied` | `bool` | `true` when any GraphQL error's message contains "access denied" (ordinal, case-insensitive) or its `extensions` text contains `ACCESS_DENIED`. |

**`IShopifyAdminApi`**

| Member | Signature | What it does |
|---|---|---|
| `QueryAsync` | `Task<AdminGraphQlResponse> QueryAsync(string sessionToken, string query, object? variables = null, CancellationToken cancellationToken = default)` | Runs the query against the shop that issued the session token and returns the response unparsed. Throws `ShopifyAdminApiException` on authentication failure or GraphQL errors. |
| `QueryAsync<T>` | `Task<T> QueryAsync<T>(string sessionToken, string query, object? variables = null, CancellationToken cancellationToken = default)` | Same call, then deserializes the response's `data` into `T`. |

**`ShopifyAdminApi`** — the default implementation.

| Member | Signature | What it does |
|---|---|---|
| constructor | `ShopifyAdminApi(IShopifyRequestAuthenticator authenticator, IShopifyTokenExchanger exchanger, IShopifyAccessTokenStore store, IAdminGraphQlClient client, IOptionsMonitor<ShopifyOptions> options, ILogger<ShopifyAdminApi> logger)` | The exchanger is used only for `Invalidate`; this class never performs an exchange itself. |
| `QueryAsync` | as above | Authenticates, reads the cached token, executes, and inspects the status before returning. |

## Behaviour

- **Authentication happens on every call.** `IShopifyRequestAuthenticator.AuthenticateAsync(sessionToken, null, …)`
  validates the signature and returns the shop and user; the expected-domain argument is `null`, so the
  shop is taken from the token's own `dest` claim rather than pinned by the caller.
- **The token comes from the store, never from an exchange here.** The authenticator is what exchanges
  and caches (see its remarks); this facade reads `IShopifyAccessTokenStore` with the configured
  `AccessTokenKind`. A miss is a hard failure — `"Authentication succeeded but no access token was
  available."` — not a lazy fetch.
- **A 401 or 403** removes the token from the store *and* calls `IShopifyTokenExchanger.Invalidate`
  before throwing. Both are required: two caches hold the access token, and clearing one leaves the
  revoked token in play for ever. The thrown message says the cache was discarded and invites a retry.
- **Any other unsuccessful response** is thrown with `response.DescribeFailure()` as the message, so the
  GraphQL `errors` text is what the caller logs.
- **`QueryAsync<T>` uses one shared `JsonSerializerOptions(JsonSerializerDefaults.Web)`** — camelCase
  naming, case-insensitive matching — and deserializes only the `data` payload, so `T` is shaped like the
  query's selection and not like the GraphQL envelope.

## Gotchas

- **A missing scope is not a 403.** Shopify answers `200 OK` with an `errors` entry, so `IsTokenRejected`
  is false and the status code looks healthy. Only `IsAccessDenied` recognises it, and retrying cannot
  fix it — re-exchanging yields the same scopes. Check `IsAccessDenied` before offering a retry button.
- **`IsAccessDenied` is a text search, not a typed code.** Shopify rewording "access denied" or moving
  the extension would silently stop it matching, and the failure would fall back to a generic error.
- **An authentication failure carries neither a shop nor a response**, so `ShopDomain`, `Response`,
  `IsTokenRejected` and `IsAccessDenied` are all null/false on it. Distinguish it by `Response is null`.
- **Cancellation is not wrapped.** A cancelled request raises `OperationCanceledException`, not
  `ShopifyAdminApiException`, and the cached token is deliberately left in place.
- **`QueryAsync<T>` will happily return a `T` with default properties** when the query selects no field
  that maps to them; there is no check that anything bound, which is how a typo in a selection set
  becomes an empty object rather than an error.
- **No source-generated serializer context** is used here, so `T` is serialized by reflection. That is
  fine on the sample app and would need a context in a trimmed or AOT deployment.
- The token store miss branch means a store that is not populated (a custom `IShopifyAccessTokenStore`
  that failed to write) looks like an authentication problem even though the session token was valid.

## See also

- [ShopifyAccessTokenStore.cs.md](ShopifyAccessTokenStore.cs.md) — the cache read on every call and
  cleared after a 401.
- [ShopifyRequestAuthenticator.cs.md](ShopifyRequestAuthenticator.cs.md) — the thing that exchanges and
  populates that cache.
- [AdminApi/AdminGraphQlClient.cs.md](AdminApi/AdminGraphQlClient.cs.md) — the transport, `DescribeFailure`
  and `Deserialize`.
- [ShopifyOptions.cs.md](ShopifyOptions.cs.md) — `AccessTokenKind`, which selects the cache entry.
