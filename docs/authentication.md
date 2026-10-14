# Authentication

How an embedded Shopify app authenticates, why the flow has the shape it has, and the mistakes that
look like they work.

## The three tokens

Confusing them is the single most common source of insecure Shopify apps, so start here. They are
three different things with three different lifetimes and three different audiences.

| Token | Issued by | Lifetime | Lives where | Used for |
|---|---|---|---|---|
| **Session token** (ID token) | App Bridge, in the browser | ~60 seconds | Browser → your server | Proving which shop and merchant are calling |
| **Access token** | Shopify, via token exchange | ~24 hours | Server only | Calling the Admin API |
| **API secret** | Partner Dashboard | until rotated | Server only | Authenticating the token exchange |

Two rules follow, and breaking either is a vulnerability:

- **A session token is not an access token.** It proves identity; it does not authorise Admin API
  calls. The toolkit never sends one to Shopify.
- **An access token never reaches the browser.** A browser that holds one holds the merchant's
  whole store authorisation.

## The flow

```
Browser (inside the Shopify Admin iframe)
  │
  │  1. shopify.idToken()             App Bridge signs a JWT for this shop
  │
  ├──────────── Authorization: Bearer <session token> ────────────►  Your server
  │                                                                     │
  │                                                     2. Verify the HMAC-SHA256 signature with
  │                                                        your app's API secret (no network call)
  │                                                     3. Check aud == your API key
  │                                                        iss == https://<shop>/admin
  │                                                        dest is a real *.myshopify.com shop
  │                                                        exp/nbf within tolerance
  │                                                                     │
  │                                                     4. POST https://<shop>/admin/oauth/access_token
  │                                                        grant_type=…:token-exchange
  │                                                        subject_token=<session token>
  │                                                        client_secret=<API secret>
  │                                                                     │
  │                                                     5. Cache the access token
  │                                                                     │
  │                                                     6. GraphQL Admin API with
  │  ◄──────────── product data only ─────────────────────  X-Shopify-Access-Token
```

Step 2 in code is `IShopifySessionTokenValidator`; steps 4–6 are `IShopifyTokenExchanger` and
`IAdminGraphQlClient`. Most applications only ever touch `IShopifyAdminApi`, which does all of it
from a session token.

## Using it

Register once:

```csharp
builder.Services.AddShopifyBlazor(builder.Configuration);
```

Protect an endpoint. The `RequireAuthorization()` is what enforces verification — without it the
endpoint is anonymous:

```csharp
app.MapGet("/api/products", async (HttpContext context, ProductsService products, CancellationToken ct) =>
{
    var authorization = context.Request.Headers.Authorization.ToString();

    if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
    {
        return Results.Unauthorized();
    }

    var sessionToken = authorization["Bearer ".Length..].Trim();
    var result = await products.ListAsync(sessionToken, ct);

    return Results.Ok(result);
})
.RequireAuthorization();
```

Call the Admin API:

```csharp
var response = await _api.QueryAsync(sessionToken, """
    query { products(first: 10) { nodes { id title } } }
    """);

var title = response.Data!.Value
    .GetProperty("products").GetProperty("nodes")[0].GetProperty("title").GetString();
```

From the browser, `IAppBridgeInterop.FetchAsync<T>` mints a fresh token per call and attaches it:

```razor
var result = await Bridge.WithLoadingAsync(
    () => Bridge.FetchAsync<ProductListResponse>("/api/products").AsTask());
```

## Why the browser makes the call

It would be simpler to call `IShopifyAdminApi` directly from a Blazor Server event handler — the
C# is already on the server. The browser is used anyway, for two reasons:

1. **The session token is fetched immediately before each request.** A Blazor circuit outlives
   several session tokens. A token captured once at circuit start is stale within a minute, and the
   failure looks like a random 401 rather than an expired credential.
2. **App Bridge features degrade correctly.** The Admin's own progress bar and toasts are wired to
   the same call, and they exist only in the browser.

## Things this deliberately does not do

| Not done | Why |
|---|---|
| Read a shop from the query string | A query string is not proof of identity. Only a signed token is. |
| Use `ShopifyAPI::Context`-style global session state | Session state that is not per-request is how one merchant's request ends up using another's token. |
| Cache the session token | It lives about a minute; a cache is a source of stale tokens. |
| Cache the access token indefinitely | Shopify rotates it on every exchange, and an app-secret rotation revokes it. |
| Trust `dest` before verifying the signature | The domain is read *without* trust, validated as a `*.myshopify.com` host, and only then used to fetch keys and build URLs. |
| Skip the `aud` check | Without it, a session token minted for a *different* app would be accepted here. |
| Accept `alg: none` | Signature verification with `ValidateIssuerSigningKey = true` rejects it. Covered by a test. |

## Token caching and recovery

The access token is cached per shop **and per merchant** for online tokens, because two merchants on
one shop have different tokens and sharing a cache entry would grant one merchant the other's
session. Offline tokens are cached per shop.

When Shopify rejects an access token — an app-secret rotation revokes every issued token — the
handler must discard it from **both** caches:

```csharp
_store.Remove(context.ShopDomain, context.UserId, kind);
_exchanger.Invalidate(context.ShopDomain, context.UserId);
```

Missing the second line is a subtle and total failure: the app keeps presenting the dead token and
every request fails identically, for ever, with no path back. This was a real bug in this toolkit,
found by `AdminApiTests.A_rejected_access_token_is_discarded_so_the_next_call_re_exchanges`.

## Scope and version configuration

Scopes are configured in the Partner Dashboard, not in code. `Shopify:Scopes` is documentation only.
The authoritative list for a store is what Shopify granted, which is why the exchanged token's
`scope` is recorded on `ShopifyAccessToken.Scopes`.

`Shopify:AdminApiVersion` defaults to `2026-10`. Set it to the current stable version from
<https://shopify.dev/docs/api/usage/versioning>. **A retired version does not error**: Shopify falls
forward to the oldest accessible stable release and answers normally, so behaviour changes with
nothing but an `X-Shopify-API-Version` response header to show for it. `AdminGraphQlResponse`
exposes that header as `ApiVersionServed` and the client logs a warning when it differs.

## Testing without a store

`tests/Shopify.Blazor.Tests` shows how to exercise this whole path with no Shopify account:

- `SessionTokenFactory` signs real JWTs with a throwaway RSA key and publishes the matching JWKS.
- `ScriptedHttpMessageHandler` answers the JWKS and token-exchange calls and records every request.

That covers forged signatures, wrong audiences, expiry, cross-shop tokens, hostile `dest` values,
RFC 8693 parameters, cache behaviour, GraphQL errors on HTTP 200, and 401 recovery. It cannot cover
the store itself — that is the one manual step.
