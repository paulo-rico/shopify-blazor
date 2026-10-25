<!-- summary-of: ShopifyOptions.cs -->
<!-- source-sha256: d772fde89c46 -->

# ShopifyOptions.cs

Configuration for a Shopify embedded application, bound from the `Shopify` configuration section, with
the API key as the only value that is safe to render into a page. The rest — the secret, the token kind,
the cache lifetimes — is server-side only and must never reach the browser.

`Shopify.Blazor` · `sealed class` options object · `ShopifyAccessTokenKind` enum in the same file

## Members

| Member | Signature | Default | What it does |
|---|---|---|---|
| `SectionName` | `public const string` | `"Shopify"` | Section name used when binding, and the section the CLI's readers look for. |
| `ApiKey` | `string` | `""` | The public API key (Shopify's "client ID"). Written into the `shopify-api-key` meta tag so App Bridge can identify the app — the one property that may be rendered. |
| `AdminApiVersion` | `string` | `DefaultAdminApiVersion` (`"2026-10"`) | The version sent with Admin API requests, in `YYYY-MM` form. |
| `DefaultAdminApiVersion` | `public const string` | `"2026-10"` | The version this build was tested against; also the fallback when nothing is configured. |
| `ApiSecret` | `string` | `""` | The API secret, used as the OAuth `client_secret` during token exchange. Server-side only. |
| `AppUrl` | `string?` | `null` | The public HTTPS address Shopify loads the app at — used for diagnostics and building redirect URLs. |
| `Scopes` | `string?` | `null` | Space- or comma-separated scopes requested at install time, e.g. `read_products,write_products`. Documentation only. |
| `AccessTokenKind` | `ShopifyAccessTokenKind` | `Online` | Whether a session token is exchanged for a per-user token or a store-wide one. |
| `AccessTokenCacheDuration` | `TimeSpan` | 50 minutes | How long an exchanged access token is cached before exchanging again. A cache lifetime, not a token lifetime. |
| `ClockSkew` | `TimeSpan` | 1 minute | Leeway tolerated when checking a session token's lifetime. |
| `RequireSessionToken` | `bool` | `true` | Declared as the switch for requiring a session token. Nothing in the toolkit reads it — see Gotchas. |
| `Validate(bool requireSecret = true)` | `void` | — | Throws when `ApiKey`, or `ApiSecret` when required, is missing or whitespace. Never validates the other properties. |

### `ShopifyAccessTokenKind`

| Value | Meaning |
|---|---|
| `Online` | A per-user token, valid only while the merchant who granted it is signed in. The default, and the safer choice for an embedded admin app. |
| `Offline` | A store-scoped token that outlives the merchant's session. Required for background work such as webhook processing. |

## Behaviour

- **`AdminApiVersion` has no safety net.** Shopify releases a version each quarter and retires each after
  roughly twelve months. A retired version does *not* fail: Shopify falls forward to the oldest accessible
  stable release and answers normally, so a version drift shows up only in the response's
  `X-Shopify-API-Version` header. The default constant is a snapshot of the build, not a pointer to
  whatever is current.
- **`AccessTokenCacheDuration` cannot extend a token.** Shopify rotates the access token on every
  exchange, so this is how often a new one is fetched; the caller's cache deadline is always capped by the
  token's own expiry.
- **`Scopes` changes nothing here.** The authoritative list is the Partner Dashboard and
  `shopify.app.toml`; a value in this property is not requested, granted or verified.
- **`Validate` is a presence check only.** It writes errors naming the configuration key and the
  `SHOPIFY_API_KEY` / `SHOPIFY_API_SECRET` environment variable, and it is what `AddShopifyBlazor` wires
  into options validation. A malformed `AdminApiVersion` or a relative `AppUrl` passes.

## Gotchas

- **`RequireSessionToken` is dead configuration.** A repository-wide search finds this declaration and no
  reader, so setting it to `false` does not make session-token authentication optional — the handler and
  the authenticator are registered regardless. Its documentation also never says what `false` would do.
- **`ApiKey` is exposed; `ApiSecret` never must be.** The type's remarks say everything except the key is
  server-side only, so component code that prints an options object can leak the secret.
- `Validate(requireSecret: false)` exists for Blazor WebAssembly clients, which only need the API key. A
  server host that passes `false` only moves the failure: the exchanger validates the options itself, so
  the same `InvalidOperationException` appears on the first exchange instead of at options resolution.
- `ClockSkew` is passed to the session-token validator, whose summary records that `TimeSpan.Zero` is
  replaced by a built-in leeway — a zero here does not mean zero tolerance.
- **A bare number in configuration is days, not minutes.** The binder uses `TimeSpan.Parse`, so a cache
  duration configured as `50` means fifty days and the token is cached long past the point Shopify rotated
  it; write `00:50:00`.

## See also

- [ShopifyServiceCollectionExtensions.cs.md](ShopifyServiceCollectionExtensions.cs.md) — binds this type and validates it.
- [Authentication/ShopifySessionTokenValidator.cs.md](Authentication/ShopifySessionTokenValidator.cs.md) — consumes `ApiKey`, `ApiSecret` and `ClockSkew`.
- [Authentication/ShopifyTokenExchanger.cs.md](Authentication/ShopifyTokenExchanger.cs.md) — consumes `ApiSecret`, `AccessTokenKind` and `AccessTokenCacheDuration`.
