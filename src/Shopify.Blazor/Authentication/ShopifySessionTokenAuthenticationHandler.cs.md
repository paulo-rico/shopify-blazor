<!-- summary-of: ShopifySessionTokenAuthenticationHandler.cs -->
<!-- source-sha256: e0e25f7ba78a -->

# ShopifySessionTokenAuthenticationHandler.cs

The ASP.NET Core authentication scheme that turns an `Authorization: Bearer <App Bridge session
token>` header into a `ClaimsPrincipal`, and refuses every other form of identity. The file also
carries the scheme's constants, its options class and the `AuthenticationFailure` record that explains
a rejection.

`Shopify.Blazor.Authentication` · the `ShopifySessionToken` scheme · base `AuthenticationHandler<ShopifyAuthenticationOptions>`

## Members

### `ShopifyAuthenticationDefaults` (static class)

| Member | Value | What it does |
|---|---|---|
| `Scheme` | `const string` = `"ShopifySessionToken"` | The name to pass to `RequireAuthorization` and any other API that takes a scheme. |
| `ShopDomainClaim` | `const string` = `"shopify:shop_domain"` | Claim holding the canonical shop domain. |
| `UserIdClaim` | `const string` = `"shopify:user_id"` | Claim holding the merchant user id. |

### `ShopifyAuthenticationOptions : AuthenticationSchemeOptions`

| Member | Type | Default | What it does |
|---|---|---|---|
| `ExpectedShopDomain` | `string?` | `null` | When set, a token whose `dest` claim normalises to a different shop is rejected as `WrongShop`. Leave it `null` in an embedded app: the shop is whatever the signed token says, which is why the signature check is the thing that matters. |
| `IncludeFailureDetail` | `bool` | `true` | Gates the audience-mismatch hint: with it on, `Explain` loads this app's API key and, when the token's `aud` differs, states both values and the usual cause (`SHOPIFY_API_KEY` overriding user secrets). The remarks say to leave it off in production, where the reason belongs in the log only — but the default is `true`, and the other hints are produced regardless. |

### `ShopifySessionTokenAuthenticationHandler`

| Member | Signature | What it does |
|---|---|---|
| Constructor | `(IOptionsMonitor<ShopifyAuthenticationOptions>, ILoggerFactory, UrlEncoder, IShopifySessionTokenValidator)` | The three arguments the handler base declares — options, logger factory and URL encoder — plus the validator. The rejection log is **not** a constructor argument; see below. |
| `Rejections` | `private ISessionTokenRejectionLog` | The log for the current request, resolved from `Context.RequestServices` and falling back to `NullRejectionLog.Instance` when nothing is registered. |

### `AuthenticationFailure` (sealed record)

| Member | Type | What it does |
|---|---|---|
| `Code` | `string` | Always `"session_token_rejected"` from this handler. |
| `Failure` | `string` | The validator's failure category name, or `"Invalid"` when it reported none. |
| `Reason` | `string` | The validator's short explanation. |
| `Hint` | `string?` | What to do about it, when the explanation produced one. The audience comparison that yields the longest hint runs only when `IncludeFailureDetail` is on; the other hints are produced regardless. |

## Behaviour

1. **No `Authorization` header at all** → `AuthenticateResult.NoResult()`, logged at debug. Not a
   failure: this is normally the default scheme, so it also runs for pages and static assets, and a
   failure per asset would bury the one line that matters.
2. **Header not starting `Bearer `**, or a bare `Bearer` with nothing after it → `Fail`. The warning
   names only the first space-separated chunk of the header, never the credential.
3. **Header is a bearer token** → `_validator.ValidateAsync(token, Options.ExpectedShopDomain,
   Context.RequestAborted)`. The debug line records the token length and the JWT segment count only.
4. **Validation failed** → builds an `AuthenticationFailure`, records it in the rejection log with the
   request path, and returns `Fail(reason)` with the hint appended when there is one. The rejection
   also appears as a warning in the application log.
5. **Validation succeeded** → a single identity under the scheme name, carrying `shopify:shop_domain`,
   `shopify:user_id`, `ClaimTypes.NameIdentifier` (the same user id) and one `shopify:scope` claim per
   granted scope. The ticket is issued under `ShopifyAuthenticationDefaults.Scheme`.

The `Explain` method orders its hints deliberately: an `aud` mismatch is reported before anything
mechanical, because a token minted for another app is signed by a different shop's keys, so the
mechanical reason reads as "keys could not be retrieved" and points at the network instead of at the
misconfiguration. The audience hint names `SHOPIFY_API_KEY` as the usual culprit, since an environment
variable silently wins over user secrets. Remaining hints cover `Expired` (tokens live about a minute,
so fetch one per request), `WrongShop`, `Missing`, and a default that prints the `dest` claim.

## Gotchas

- **A missing header is `NoResult`, not `Fail`.** This scheme is normally the default, so it runs for
  every page and static asset too; reporting a failure for those fills the log with warnings and hides
  the one line that matters.
- **`IncludeFailureDetail` does not suppress every hint.** It gates only the audience check — and so
  the hint that names the expected API key. The `Expired`, `WrongShop`, `Missing` and default hints are
  returned whether it is on or off, and the default hint quotes the token's `dest` claim. Turning the
  flag off in production therefore shortens the disclosure rather than removing it.
- **Claim decoding for the hint is unverified.** `TryDecodeClaims` base64url-decodes the payload and
  swallows `FormatException` / `JsonException`; those claims are used only to phrase a message and must
  never be treated as validated. Only the validator's verdict is trusted.
- **The rejection log is resolved per request rather than injected.** An authentication scheme is
  constructed by the framework from its options, so its constructor only receives the types the base
  class declares; resolving from `RequestServices` keeps the log a normal singleton. A missing
  registration therefore degrades to a silent no-op instead of a startup error.
- `ExpectedShopDomain` is compared after normalisation, so `https://shop.myshopify.com` and
  `shop.myshopify.com` are the same shop and neither is a mismatch.

## See also

- [ShopifySessionTokenValidator.cs.md](ShopifySessionTokenValidator.cs.md) — the verifier behind every decision here.
- [SessionTokenRejectionLog.cs.md](../Diagnostics/SessionTokenRejectionLog.cs.md) — where a rejection is recorded.
- [SessionTokenDiagnostics.cs.md](../Diagnostics/SessionTokenDiagnostics.cs.md) — the developer-facing report of the same failure.
- [ShopifyTokenExchanger.cs.md](ShopifyTokenExchanger.cs.md) — the next step, once a principal exists.
