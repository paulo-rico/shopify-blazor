<!-- summary-of: SessionTokenDiagnostics.cs -->
<!-- source-sha256: 28f8f31369f1 -->

# SessionTokenDiagnostics.cs

Decodes a session token without verifying it and turns a rejection into a specific diagnosis: which
claim is wrong, which setting is missing, and what to do about it. It builds the `TokenDiagnosticReport`
that the development-only diagnostics endpoint serialises to JSON, and the verdict in it comes from the
real validator rather than from this file's own unverified reading.

`Shopify.Blazor.Diagnostics` · static report builder plus its DTO · no base class

## Members

### `SessionTokenDiagnostics` (static class)

| Member | Signature | What it does |
|---|---|---|
| `InspectAsync` | `static Task<TokenDiagnosticReport> InspectAsync(IHttpClientFactory handler, IOptionsMonitor<ShopifyOptions> options, IShopifySessionTokenValidator validator, string? authorizationHeader, string? fallbackToken = null)` | Inspects one token and returns the report. An unreadable token becomes a verdict in the report rather than an exception. |

Private helpers, listed because their behaviour leaks into the report: `Hint` (orders the advice),
`Mask` (shortens the API key), `ReadJson` (base64url-decodes a JWT segment), and three claim readers.

### `TokenDiagnosticReport`

**What arrived**

| Property | Type | What it says |
|---|---|---|
| `AuthorizationHeaderPresent` | `bool` | The request carried an `Authorization` header at all. |
| `HeaderLooksLikeBearer` | `bool` | That header starts with `Bearer ` (case-insensitive). |
| `TokenLength` | `int?` | Token length, or `null` when none arrived. |
| `JwtSegments` | `int` | Dot-separated parts; 3 is a JWT. |
| `Header` | `Dictionary<string, object?>?` | The decoded JWT header, unverified. |
| `Claims` | `Dictionary<string, object?>?` | The decoded payload, unverified. |

**What the token contains**

| Property | Type | What it says |
|---|---|---|
| `TokenAlgorithm` | `string?` | The `alg` header. |
| `TokenKeyId` | `string?` | The `kid` header, used to select a signing key. |
| `ClaimShopDomain` | `string?` | The raw `dest` claim. |
| `ClaimIssuer` | `string?` | The `iss` claim. |
| `ClaimAudience` | `string?` | The `aud` claim, which must equal the API key. |
| `ClaimSubject` | `string?` | The `sub` claim, the merchant user id. |
| `ClaimsMatchApp` | `bool?` | Whether `aud` equals the configured API key, ordinal. |
| `ExpiresAt` | `DateTimeOffset?` | From `exp`. |
| `ExpiresInSeconds` | `long?` | Seconds until expiry; negative means already expired. |

**What this app is configured with**

| Property | Type | What it says |
|---|---|---|
| `ConfiguredApiKey` | `string?` | The API key, **masked** — first four and last four characters plus the length. |
| `ConfiguredAppUrl` | `string?` | `Shopify:AppUrl` as configured. |
| `AppUrlLooksSuspicious` | `bool` | True when the URL is not a plain `https://` one, or repeats the scheme (`https://http…`). |
| `AccessTokenKind` | `string?` | `Online` or `Offline`, as a string. |
| `AdminApiVersion` | `string?` | The Admin API version this app asks for. |
| `ExpectedAlgorithm` | `string?` | Always `"HS256"`. |
| `SignatureCheckPossible` | `bool` | True when an API secret is configured, so a signature *can* be checked. |
| `SignatureStatus` | `string?` | One of: no secret configured, no `alg` header, `ok: HS256 with the API secret`, or the encountered algorithm named as wrong. |

**The verdict**

| Property | Type | What it says |
|---|---|---|
| `GrantedScopes` | `IReadOnlyList<string>` | The `scp` scopes, straight from the real validator's result; empty on failure. |
| `Validated` | `bool` | The real validator accepted the token. |
| `ValidationFailure` | `string?` | The failure category name. |
| `ValidationReason` | `string?` | The validator's explanation. |
| `Verdict` | `string?` | One line: accepted for which shop and user, or rejected with the category. |
| `Hint` | `string?` | What to do about it. |

## Behaviour

The report is assembled in the order a developer would investigate it. A missing token short-circuits
immediately with "No token reached the server." A value that is not three dot-separated segments
short-circuits as "The value is not a JWT.", and one whose segments will not base64url-decode or parse
short-circuits as "The token could not be decoded as JSON." Only after that are claims, algorithm and
expiry read, and only then does `validator.ValidateAsync(token)` run — with **no** expected shop, so
the verdict is the real one and matches what the authentication handler would decide.

`Hint` ordering is deliberately not the order the checks run. A wrong audience is reported first,
because a token minted for another app goes to the *other* app's shop for its keys, so every subsequent
failure looks like a key or network problem. Then: expired (`exp` negative or the validator said so),
wrong shop, missing, no API secret configured, wrong algorithm, and finally a general "signature,
audience or lifetime failed" message that tells the reader to compare the decoded claims.

## Gotchas

- **The first `IHttpClientFactory` argument is unused.** It is documented as fetching the shop's key
  set, which was true when validation used RS256 against `/.well-known/jwks.json`; with HS256 there is
  nothing to fetch, and the parameter survives only in the signature.
- **`fallbackToken` wins over the header.** The token is taken from `fallbackToken` first, and the
  `Authorization` header is only mined when that is empty — so passing both inspects the fallback and
  reports the header as merely present.
- **Decoding is unverified, by design.** The claims are shown to a developer and never trusted for
  access; the endpoint serving this must be development-only, and a passing diagnostic grants nothing.
  Do not reuse `InspectAsync` as an authorisation check.
- **The API key is masked, but the `aud` claim is not.** `Mask` prints four characters at each end plus
  the length (all asterisks when the value is eight characters or shorter), while `ClaimAudience` and
  the hints quote the token's audience in full. The report is for a developer's eyes only.
- **`AppUrlLooksSuspicious` is narrow.** It only catches a non-`https://` scheme or a repeated one, so
  a trailing slash or a stale host is not flagged — those failures show up as an `aud` mismatch
  instead.
- **`GrantedScopes` is empty on failure.** It is read from the validator's `SessionToken`, which is
  `null` when the token was rejected, so a report cannot show scopes for a token that did not verify —
  which is exactly the case where a merchant is likely to ask "what scopes do I have?".
- A missing scope does not appear as a failure here. It surfaces later as an Admin API `ACCESS_DENIED`
  at HTTP 200, which is why `GrantedScopes` sits beside the verdict.

## See also

- [ShopifySessionTokenValidator.cs.md](../Authentication/ShopifySessionTokenValidator.cs.md) — the verdict this reports is that class's.
- [SessionTokenRejectionLog.cs.md](SessionTokenRejectionLog.cs.md) — the server-side record of the same rejections.
- [ShopifyConfigurationDiagnostics.cs.md](ShopifyConfigurationDiagnostics.cs.md) — the configuration half of the same investigation.
- [ShopifyOptions.cs.md](../ShopifyOptions.cs.md) — every configured value quoted in the report.
