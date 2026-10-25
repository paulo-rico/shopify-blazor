<!-- summary-of: ShopifyTokenExchanger.cs -->
<!-- source-sha256: 2ae68613023d -->

# ShopifyTokenExchanger.cs

Exchanges a verified App Bridge session token for an Admin API access token, and caches it per shop and
merchant so the exchange runs about hourly rather than once per request. It also holds the exception
Shopify's refusal is reported as and the response shape the exchange parses.

`Shopify.Blazor.Authentication` · the RFC 8693 token exchange grant · implements `IShopifyTokenExchanger`

## Members

### `ShopifyTokenExchangeException`

| Member | Type | What it carries |
|---|---|---|
| Constructor | `(string message, HttpStatusCode? statusCode = null, string? errorCode = null, Exception? inner = null)` | Builds the exception; every part is optional except the message. |
| `StatusCode` | `HttpStatusCode?` | The HTTP status Shopify answered with, `null` when no response arrived. |
| `ErrorCode` | `string?` | Shopify's OAuth error code, for example `invalid_subject_token`. |

### `IShopifyTokenExchanger`

| Member | Signature | What it does |
|---|---|---|
| `ExchangeAsync` | `Task<ShopifyAccessToken> ExchangeAsync(ShopifySessionToken sessionToken, CancellationToken cancellationToken = default)` | Returns a cached token while it is valid, otherwise performs the exchange. Throws `ShopifyTokenExchangeException` when Shopify refuses. |
| `Invalidate` | `void Invalidate(string shopDomain, string subject)` | Drops the cached token for that shop and merchant so the next call re-exchanges. Also where a 401 is recovered from without a restart. |

### `ShopifyTokenExchanger`

| Member | Signature | What it does |
|---|---|---|
| Constructor | `(IHttpClientFactory, IMemoryCache, IOptionsMonitor<ShopifyOptions>, ILogger<ShopifyTokenExchanger>)` | The named client used is `ShopifyHttpClients.TokenExchange`. |
| `ExchangeAsync` | as above | Validates the options, normalises the shop, consults the cache, then POSTs the exchange. |
| `Invalidate` | as above | Removes **both** the online and offline entries for the shop. |

## Behaviour

1. `options.Validate()` runs first, so a missing API key or secret throws `InvalidOperationException`
   here rather than producing a confusing 400 from Shopify.
2. The shop is passed through `ShopDomain.Normalize`, so it throws `ArgumentException` if the verified
   token somehow carries an unusable domain.
3. The cache key is `shopify:token:{shop}:offline` for offline tokens and
   `shopify:token:{shop}:online:{subject}` for online ones — the merchant's `sub` is part of the key.
4. A cached token is returned only when it is present **and** `IsExpired` is false.
5. Otherwise the exchange is a `POST https://{shop}/admin/oauth/access_token` with form fields
   `client_id`, `client_secret`, `grant_type` = `urn:ietf:params:oauth:grant-type:token-exchange`,
   `subject_token`, `subject_token_type` = `urn:ietf:params:oauth:token-type:id_token`, and
   `requested_token_type` set to Shopify's online or offline access-token type per
   `ShopifyOptions.AccessTokenKind`.
6. The result is cached for `ShopifyOptions.AccessTokenCacheDuration`, shortened to the token's own
   remaining lifetime when that is sooner. `ExpiresAt` stays `null` when Shopify sends `expires_in` of
   zero or less, and scopes are split on commas and spaces.

## Gotchas

- **An already-expired token is returned uncached.** Shopify sends `expires_in: 0` for some grants; a
  token whose expiry has passed is logged as a warning and handed back without being stored, because
  caching it would make every later request present a token that is dead on arrival.
- **The cache lifetime is not the token's lifetime.** Shopify rotates the access token on every
  exchange, so `AccessTokenCacheDuration` is a cache setting; it is always capped by the response's
  own expiry, but never extended by it.
- **Online tokens are per merchant.** That is why the subject belongs in the cache key: two merchants
  on one shop have different access tokens, and sharing an entry would give one merchant the other's
  session. `Invalidate` removes both kinds because a caller reporting a rejected token does not
  necessarily know which kind it was.
- **`Invalidate` is silent for an unusable domain** — it normalises, returns early on `null`, and
  reports nothing. A caller passing a raw claim value can believe it cleared a cache entry that it did
  not.
- **Cancellation is not wrapped.** A network failure becomes
  `ShopifyTokenExchangeException(… before a response was received …, inner: ex)`, but
  `OperationCanceledException` propagates unchanged, so a caller must distinguish a cancelled request
  from a refusal.
- **`expires_in` binds only because the naming policy is declared.** `TokenExchangeJsonContext` carries
  `[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]`; without
  it the generated context uses property names verbatim, `expires_in` silently fails to bind, every
  token looks non-expiring, and the cache never shortens.
- **The exception message is loggable, not returnable.** It never contains the session token or the API
  secret, but it may quote Shopify's own error text, so it should not be sent verbatim to a browser.

## See also

- [ShopifySessionTokenValidator.cs.md](ShopifySessionTokenValidator.cs.md) — produces the `ShopifySessionToken` this consumes.
- [SessionTokenDiagnostics.cs.md](../Diagnostics/SessionTokenDiagnostics.cs.md) — reports `AccessTokenKind` and whether scopes were granted.
- [ShopifyAccessToken.cs.md](../ShopifyAccessToken.cs.md) — the returned record, with `IsExpired`.
- [ShopifyAccessTokenStore.cs.md](../ShopifyAccessTokenStore.cs.md) — the *other* cache a 401 has to clear.
