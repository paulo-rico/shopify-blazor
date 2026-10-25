<!-- summary-of: ShopifyRequestAuthenticator.cs -->
<!-- source-sha256: 7727a75cfc8c -->

# ShopifyRequestAuthenticator.cs

Turns an App Bridge session token into the shop and merchant a request is acting for, and makes sure an
Admin API access token for that shop and merchant is in the store before returning. It is the single
entry point protected endpoints should use, so every one of them gets the same validation, exchange and
caching treatment.

`Shopify.Blazor` · `IShopifyRequestAuthenticator` + `sealed class ShopifyRequestAuthenticator` + two records

## Members

### `ShopifyShopContext`

| Position | Type | What it carries |
|---|---|---|
| `ShopDomain` | `string` | The canonical `name.myshopify.com` domain the token was issued for. |
| `UserId` | `string` | The merchant's user id, taken from the session token's `sub` claim. |

### `ShopifyAuthenticationResult`

| Member | Type | What it does |
|---|---|---|
| `Context` | `ShopifyShopContext?` | The authenticated shop and user, or `null` on failure. |
| `Failure` | `ShopifySessionTokenValidationFailure?` | The validator's category (`Missing`, `Invalid`, `Expired`, `WrongShop`), or `null` on success. |
| `Reason` | `string?` | A short explanation safe to log. Never carries the token itself. |
| `Succeeded` | `bool` (computed) | `Context is not null`. |
| `Success` / `Fail` | `internal static` factories | How the implementation builds a result; the public record constructor is still available. |

### `IShopifyRequestAuthenticator`

| Member | Signature | What it does |
|---|---|---|
| `AuthenticateAsync` | `Task<ShopifyAuthenticationResult> AuthenticateAsync(string? sessionToken, string? expectedShopDomain = null, CancellationToken cancellationToken = default)` | Validates the token, optionally pins the expected shop, ensures an access token is cached, and describes the outcome. Does not throw for a bad token. |

### `ShopifyRequestAuthenticator`

| Member | Signature | What it does |
|---|---|---|
| Constructor | `(IShopifySessionTokenValidator validator, IShopifyTokenExchanger exchanger, IShopifyAccessTokenStore store, IOptionsMonitor<ShopifyOptions> options)` | The four collaborators; options are re-read on every call through `CurrentValue`. |
| `AuthenticateAsync` | as above | Phase 1 validates; phase 2 consults the access-token store and exchanges on a miss. |

## Behaviour

A successful call does five things, in this order:

1. `IShopifySessionTokenValidator.ValidateAsync` runs with the same `expectedShopDomain` and cancellation
   token. Signature, audience and lifetime are checked there; a failure is returned as
   `ShopifyAuthenticationResult.Fail`, with `Invalid` and `"Session token validation failed."` substituted
   when the validator supplies no category or reason.
2. The configured `ShopifyOptions.AccessTokenKind` selects which cache entry is relevant.
3. `IShopifyAccessTokenStore.TryGet(shop, user, kind, out _)` is consulted. The retrieved token is
   deliberately discarded — this class only needs to know an entry exists.
4. On a miss the token is exchanged and stored for `AccessTokenCacheDuration`, shortened to the token's own
   remaining lifetime when that is sooner. A lifetime of zero or less is not stored at all.
5. The context is returned. **No access token is returned**; a caller that needs one reads the store.

## Gotchas

- **The success result carries no token, and the miss branch is not the caller's.** `ShopifyAdminApi`
  authenticates and then reads `IShopifyAccessTokenStore` itself; a caller that looks for the token on this
  result will not find it.
- **A store hit makes no network call at all.** Authentication therefore cannot detect a token that
  Shopify has revoked — the revocation appears as a 401 on the next Admin API call, and recovery means
  clearing `IShopifyAccessTokenStore` *and* `IShopifyTokenExchanger`, the two caches (AGENTS.md #12).
- **An already-expired token is exchanged but not stored.** If `ExpiresAt` has passed, the computed
  lifetime is zero, `Set` is skipped, and the call still reports success — so every subsequent request
  repeats the exchange, and the store never holds a token with no life left in it.
- **A store miss exchanges even when the exchanger's cache would hit.** That is cheap (the exchanger
  returns its cached token without HTTP) but it repopulates this store, which is how the two caches stay
  in step.
- **`expectedShopDomain: null` trusts the token's own `dest` claim.** Pass the shop when the route names
  one; that is what turns a token for another shop into `WrongShop` instead of a valid session.
- **The exchange is the only step that can throw.** A malformed token is an ordinary result; a Shopify
  refusal inside `ExchangeAsync` arrives as that layer's exception, and cancellation propagates unwrapped.
- **Two concurrent callers can both miss and both exchange.** `TryGet` is not a pure query — it removes
  expired entries as a side effect — and there is no lock around the exchange, so the last `Set` wins and
  nothing breaks, but "one exchange per shop" is not guaranteed.
- `TryGet` is keyed with the configured kind, so changing `AccessTokenKind` at runtime makes existing
  entries invisible rather than stale — and an online entry is keyed by user as well as shop.

## See also

- [Authentication/ShopifySessionTokenValidator.cs.md](Authentication/ShopifySessionTokenValidator.cs.md) — produces the failure categories this returns.
- [ShopifyAccessTokenStore.cs.md](ShopifyAccessTokenStore.cs.md) — the store this checks and populates.
- [Authentication/ShopifyTokenExchanger.cs.md](Authentication/ShopifyTokenExchanger.cs.md) — the exchange and the other cache.
- [ShopifyAdminApi.cs.md](ShopifyAdminApi.cs.md) — the facade that calls this and then reads the store.
