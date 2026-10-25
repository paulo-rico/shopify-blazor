<!-- summary-of: ShopifySessionTokenValidator.cs -->
<!-- source-sha256: c494be0d789f -->

# ShopifySessionTokenValidator.cs

Verifies an App Bridge session token as an HS256 JWT signed with the app's own API secret, then hands
back the claims as a `ShopifySessionToken`. It accepts no key set, no network call and no unsigned or
differently-signed token, and it reports *why* a token failed rather than throwing.

`Shopify.Blazor.Authentication` · the verifier behind the authentication scheme · implements `IShopifySessionTokenValidator`

## Members

### `ShopifySessionToken` (sealed record)

| Position | Type | What it carries |
|---|---|---|
| `ShopDomain` | `string` | Canonical `name.myshopify.com`, normalised from the `dest` claim. |
| `UserId` | `string` | The numeric merchant user id from `sub`. |
| `ExpiresAt` | `DateTimeOffset` | Expiry; these tokens live on the order of a minute. |
| `IssuedAt` | `DateTimeOffset` | When Shopify issued it, from `iat`. |
| `Scopes` | `IReadOnlyList<string>` | The `scp` claim split on spaces and commas; empty when absent. |
| `RawToken` | `string` | The original serialized JWT, needed as `subject_token` for the exchange. |

### `ShopifySessionTokenValidationFailure` (enum)

| Value | Meaning |
|---|---|
| `Missing` | No token was supplied to the validator. |
| `Invalid` | Malformed, unreadable, wrong signature, wrong audience — or the app is misconfigured. |
| `Expired` | The signature verified but the lifetime window has passed. |
| `WrongShop` | The token is for a different shop than the one the caller expected. |

### `ShopifySessionTokenValidationResult`

| Member | Type | What it does |
|---|---|---|
| `SessionToken` | `ShopifySessionToken?` | The verified token, or `null` on failure. |
| `Failure` | `ShopifySessionTokenValidationFailure?` | Why it failed, or `null` on success. |
| `Reason` | `string?` | A short explanation safe to log; never echoes the token. |
| `Succeeded` | `bool` | `SessionToken is not null`. |
| `Success` / `Fail` | `internal static` factories | The only way a result is built; the constructor is private. |

### `IShopifySessionTokenValidator`

`Task<ShopifySessionTokenValidationResult> ValidateAsync(string? sessionToken, string?
expectedShopDomain = null, CancellationToken cancellationToken = default)` — verifies signature,
audience and lifetime, and checks the shop when one is given. It does not throw for an unusable token;
the result is the whole answer.

### `ShopifySessionTokenValidator`

| Member | Signature | What it does |
|---|---|---|
| Constructor | `(IOptionsMonitor<ShopifyOptions>, ILogger<ShopifySessionTokenValidator>)` | Reads `ApiKey`, `ApiSecret` and `ClockSkew` per call from the monitor. |
| `ValidateAsync` | as above | The implementation. Returns `Task.FromResult` throughout — nothing here awaits. |

## What it checks, in order

1. **Empty or whitespace token** → `Missing`.
2. **`Shopify:ApiKey` not configured** → `Invalid`. Without the audience there is nothing to compare.
3. **`Shopify:ApiSecret` not configured** → `Invalid`. Without the secret no signature can be checked,
   so every token is refused rather than accepted unverified.
4. **The `dest` claim**, read through `ReadJsonWebToken` before anything is verified — and therefore
   treated as untrusted — then normalised through `ShopDomain.TryNormalize`. An unusable value →
   `Invalid`. This is the check that stops an attacker-supplied domain reaching an interpolated URL.
5. **`expectedShopDomain`**, when supplied: an unnormalisable value → `Invalid`; a different shop →
   `WrongShop`. Comparison is `Ordinal` against the normalised forms.
6. **Secret length** → `Invalid` with a configuration message when the secret is under 16 bytes.
7. **The real validation**, with `ValidAlgorithms = [HmacSha256]`, the secret as a symmetric key,
   `ValidAudience = ApiKey`, `ValidateLifetime = true` and `ValidateIssuer = false` — the issuer is
   `https://{shop}/admin`, which the `dest` check already covers. `NameClaimType` is `sub`.
8. **Success** → `exp` (`ValidTo`, falling back to now), `iat`, `sub`, and `scp` split on `' '` and
   `','`. Both timestamps are re-stamped as `DateTimeKind.Utc` before becoming `DateTimeOffset`s.

A failed validation is mapped to a category: `SecurityTokenExpiredException`, or a
`SecurityTokenInvalidLifetimeException` whose message mentions `is after Expires` or `Expired`, becomes
`Expired`; everything else is `Invalid`.

## Gotchas

- **A short secret fails every token.** Microsoft's handler refuses an HS256 key under 128 bits while
  Shopify's own libraries use the secret as-is, so a truncated 32-character client secret would produce
  a "key size" error that reads like a token fault. The class checks the byte length first and reports
  it as a configuration problem, logging that a real client secret is 32 hexadecimal characters.
- **`ClockSkew = TimeSpan.Zero` does not mean no skew.** A zero in `ShopifyOptions.ClockSkew` is
  replaced by a built-in 10-second leeway (matching Shopify's libraries); any other value is used as
  given. There is no way to request strictly zero tolerance.
- **`Expired` is decided partly by the exception's message text.** A lifetime failure arrives as
  `SecurityTokenInvalidLifetimeException` with a message covering both "already expired" and "not valid
  yet", so the classifier looks for `is after Expires` or `Expired` in that message. A reworded message
  would downgrade an expiry to `Invalid`, which sends the reader hunting a signature problem instead of
  fetching a fresh token.
- **Do not add a JWKS fetch.** The class previously verified RS256 against
  `https://{shop}/.well-known/jwks.json`; that endpoint belongs to the Storefront and Customer Account
  APIs and sits behind the storefront password page that every dev store has, so it could never have
  worked. The trust anchor is the app secret alone.
- **The algorithm is pinned.** `ValidAlgorithms` accepts only `HS256`, so an `alg: none` token cannot
  be downgraded into acceptance and a token signed with another algorithm is refused.
- **`ValidateAsync` blocks.** It calls the handler's validation task with `GetAwaiter().GetResult()`
  rather than awaiting it, and the `cancellationToken` parameter is unused — a caller cannot cancel it.
- Failure `Reason` values are safe to log but are built partly from framework exception text, so they
  describe the mechanism rather than a fix; the handler and the diagnostics report turn them into
  advice.

## See also

- [ShopifySessionTokenAuthenticationHandler.cs.md](ShopifySessionTokenAuthenticationHandler.cs.md) — applies a result to the request as claims.
- [ShopifyTokenExchanger.cs.md](ShopifyTokenExchanger.cs.md) — consumes `RawToken` and `ShopDomain`.
- [ShopDomain.cs.md](../Internal/ShopDomain.cs.md) — the `dest` normalisation used in step 4.
- [SessionTokenDiagnostics.cs.md](../Diagnostics/SessionTokenDiagnostics.cs.md) — decodes a token without verifying, and calls this validator for the verdict.
