<!-- summary-of: ShopDomain.cs -->
<!-- source-sha256: 45a967705b2a -->

# ShopDomain.cs

Every value that reaches an outbound URL or a token-cache key goes through here, because a shop
domain is attacker-supplied: it arrives in the `dest` claim of a session token and is then
interpolated into `https://{shop}/admin/oauth/access_token`, a request that carries the app secret. A
crafted domain could otherwise redirect the token exchange — and the secret — to another host.

`Shopify.Blazor.Internal` · `internal static class` · the one place a shop domain is validated

## Members

| Member | Signature | What it does |
|---|---|---|
| `MyshopifySuffix` | `const string` = `".myshopify.com"` | The suffix every shop domain ends with. |
| `TryNormalize` | `static string? TryNormalize(string? value)` | Returns the canonical lower-case `name.myshopify.com`, or `null` when the value is not a usable shop. Never throws. |
| `Normalize` | `static string Normalize(string? value)` | Same, but throws `ArgumentException` instead of returning null, for call sites with no fallback. |

## What is accepted

| Input form | Result |
|---|---|
| `shop` | `shop.myshopify.com` — Shopify itself accepts a bare name in several places. |
| `shop.myshopify.com` | unchanged |
| `https://shop.myshopify.com` | `shop.myshopify.com` — the `dest` claim is a URL, not a host. |
| `http://…`, other schemes | rejected |
| `shop.myshopify.com.evil.example.com` | rejected — the suffix must be at the end. |
| `shop%2f..%2fevil.myshopify.com`, anything with `/ : % @` or whitespace | rejected |
| `127.0.0.1`, IPv6, `user@host` | rejected — `UriHostNameType` must be `Dns`. |

A label longer than 60 characters, one starting or ending with `-`, or one containing `..` is
rejected. `IsValidLabel` is deliberately strict: it is the thing that stops a traversal from reaching
an interpolated URL.

## Gotchas

- Returning `null` is the only failure signal, so a caller that ignores it will build a URL from the
  raw input. Use `Normalize` when there is no sensible fallback.
- The comparison is `Ordinal` and the result is lower-cased; do not feed the raw claim into a cache key
  afterwards or the same shop can appear twice.

## See also

- [ShopifySessionTokenValidator.cs.md](../Authentication/ShopifySessionTokenValidator.cs.md) — reads and validates the `dest` claim.
- [ShopifyTokenExchanger.cs.md](../Authentication/ShopifyTokenExchanger.cs.md) — interpolates the normalised domain into the token-exchange URL.
