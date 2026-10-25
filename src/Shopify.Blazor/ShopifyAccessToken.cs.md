<!-- summary-of: ShopifyAccessToken.cs -->
<!-- source-sha256: c13ea45b7ddc -->

# ShopifyAccessToken.cs

One Admin API access token, bound to the shop it was issued for and carrying the scopes it actually
grants. It is the value the token exchanger hands back, the thing `IShopifyAccessTokenStore` caches, and
what `IShopifyAdminApi` presents to the Admin API as a bearer token.

`Shopify.Blazor` · `sealed record` · immutable value, no behaviour beyond one computed property

## Members

| Member | Signature | What it does |
|---|---|---|
| `ShopDomain` | `string` (positional) | The `*.myshopify.com` domain the token belongs to. Already normalised by the time a token exists — it is the shop the exchange was performed against, not raw request input. |
| `AccessToken` | `string` (positional) | The bearer token itself, sent as `Authorization: Bearer …` on every Admin API call. |
| `Scopes` | `IReadOnlyList<string>` (positional) | The scopes the token actually grants, as opposed to the scopes the app asked for. A missing scope is what produces the access-denied error described on `ShopifyAdminApiException`. |
| `ExpiresAt` | `DateTimeOffset?` (positional) | When the token expires, or `null` when it does not. `null` is the normal case for an offline token; an online token carries Shopify's `expires_in` as an absolute instant. |
| `IsExpired` | `bool` (computed, get-only) | `true` when `ExpiresAt` is set and is at or before `DateTimeOffset.UtcNow`. Recomputed on every read — it is not a cached flag, so a token that was live when stored reports expired later without anything writing to it. |

## Behaviour

- **Positional construction only.** There is no parameterless constructor and no initialiser path other
  than `with`, so a token is always complete: a shop, a bearer value, a scope list and an expiry.
- **`ExpiresAt` is nullable rather than defaulted**, which is what keeps "offline token, never expires"
  distinguishable from "expiry unknown". `IsExpired` is `false` in both cases — a `null` expiry can never
  be expired — so a caller cannot use it to detect an online token.
- The store caps its own cache deadline at this expiry, so a token's lifetime is authoritative over
  whatever lifetime the caller asked to cache it for.

## Gotchas

- **`IsExpired` reads the clock, so it is not a stable value.** A component that checks it once and holds
  the answer can present a token that expired in between. Re-read it per use, which is what
  `IShopifyAdminApi` does by validating and re-reading the store on every call.
- **Record equality does not compare `Scopes` element by element.** The positional property is an
  `IReadOnlyList<string>`, so two tokens with identical domains, values, scopes and expiry but separate
  list instances are **not** equal, and `GetHashCode` differs too. Do not use a token as a dictionary
  key or in a set and expect two looks at the same shop to match.
- **The token is a secret held as a plain `string`.** It appears in `ToString()` of the record — the
  compiler-generated one prints `AccessToken=…` — so logging a token, or logging an object that contains
  one, writes the credential to the log. Nothing here redacts it.
- Scopes are whatever Shopify granted. An empty list is possible and means the exchange succeeded with
  no usable permission, which surfaces later as an access-denied GraphQL error rather than here.

## See also

- [ShopifyAccessTokenStore.cs.md](ShopifyAccessTokenStore.cs.md) — caches these and caps its deadline at
  `ExpiresAt`.
- [Authentication/ShopifyTokenExchanger.cs.md](Authentication/ShopifyTokenExchanger.cs.md) — produces
  one from a session token.
- [ShopifyOptions.cs.md](ShopifyOptions.cs.md) — `AccessTokenKind`, which decides whether the token is
  per-shop or per-user and therefore which expiry applies.
