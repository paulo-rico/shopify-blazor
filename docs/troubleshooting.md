# Troubleshooting

Ordered by how often each one is the answer. Start with the diagnostics, then read the entries.

## Diagnose first

Two tools answer most questions without guessing.

```sh
# Is the app installable at all? Checks the page, the framing policy, the API key match, and
# whether the server agrees with the page about which app this is.
node tools/check-admin-install.mjs https://<your-server-hostname>

# What does a session token actually contain, and why was it rejected?
# Development only. Copy the token out of the browser (see below) or send it as a bearer token.
curl 'http://localhost:3458/api/diagnostics/session-token?token=PASTE_TOKEN'
```

To get a token from the browser: open the Admin, then in DevTools run

```js
copy(await shopify.idToken())
```

The diagnostic reports the decoded `aud`, `iss`, `dest` and `exp`, whether they match this app's
configuration, whether the shop's key set is reachable, and the validator's real verdict. It decodes
without verifying, so it is a debugging aid — never a way to grant access.

## Which credentials did the app actually use?

An environment variable overrides user secrets, silently. If the winning value belongs to a
different Shopify app, every token is rejected as being for someone else. The app says which layer
won at startup:

```sh
dotnet run --project samples/Shopify.Blazor.SampleApp 2>&1 | grep -E "ApiKey came from|Shopify app:"
```

```
warn: Shopify:ApiKey came from the SHOPIFY_API_KEY environment variable. …
      Shopify app: ApiKey=1a2b3c… Secret=set (32 characters) AppUrl=…
```

Compare that key with the `shopify-api-key` meta tag in the Admin's page source. They must be the
same. Precedence, later wins: `appsettings.json`, `appsettings.Development.json`, **user secrets**,
**environment variables**.

## Why did the last request get a 401?

The browser only ever sees a 401; the reason is known on the server. In Development the app records
it and exposes it:

```sh
curl http://localhost:3458/api/diagnostics/rejections?count=3
```

```json
[{ "path": "/api/products", "failure": "Invalid",
   "reason": "The shop's signing keys could not be retrieved.",
   "hint": "The aud claim is 'X', but this app's API key is 'Y'. The token was minted for a different Shopify app…" }]
```

The Products page calls this itself and shows the hint, so a failed load explains itself. The same
line is in the server log at warning level:

```
warn: Session token rejected on /api/products (Invalid): The shop's signing keys could not be
      retrieved. The aud claim is 'X', but this app's API key is 'Y'. …
```

## "AuthenticationScheme: ShopifySessionToken was not authenticated"

Several different problems produce that one line. The diagnostic separates them.

**Read the level of the message.** `NoResult` (debug level) means no token was presented at all,
which is normal for page and asset requests. A real rejection is a **warning** that names the
failure and the reason. If you only see the debug line for `/api/products`, the browser never
attached a token — look at the browser console, not the server log.

| Diagnostic says | Cause | Fix |
|---|---|---|
| No token reached the server | The browser called without one, or the page is not embedded | Check the browser console for an App Bridge error |
| `aud` does not match the configured API key | The page's `shopify-api-key` and the server's `Shopify:ApiKey` are **different apps** | Point the Partner Dashboard App URL at this app, or correct `SHOPIFY_API_KEY` |
| Expired | The token was reused instead of fetched per request | Fetch immediately before each call; do not cache |
| `alg` is `none` | An unsigned token | Expected: it is refused by design |
| Signing keys could not be fetched | No outbound HTTPS, or a `dest` claim naming a non-existent shop | Check network egress |
| `kid` not among the shop's keys | The token was signed by a different app's store | Same as the `aud` mismatch above |

The most common real cause is the **audience mismatch**. It happens when the Partner Dashboard App
URL still points at an older app entry — a previous hostname that has since been reused, or a second
app created for the same project — so the Admin loads *that* app's frontend while your server holds a
different app's credentials. Both halves are individually valid; they are just not the same app.

Confirm the two halves:

```sh
curl -s https://<your-server-hostname>/ | grep -o 'shopify-api-key[^>]*'
curl -s https://<your-server-hostname>/api/diagnostics/session-token | python3 -m json.tool | grep configuredApiKey
```

They must be the same key. `check-admin-install.mjs` compares them for you.

## The app shows an empty frame in the Admin

```
Framing '…' violates the following Content Security Policy directive: "frame-ancestors 'self'"
```

The app is refusing to be framed. Blazor's rendering pipeline needs `app.UseAntiforgery()`, and that
middleware sets `X-Frame-Options: SAMEORIGIN` and `frame-ancestors 'self'` on every response that
asks for a token — both of which refuse the Admin's iframe.

Register the framing middleware **first** in the pipeline:

```csharp
var app = builder.Build();

app.UseShopifyEmbeddedFraming();   // before UseStatuscodePages, UseAntiforgery, UseAuthentication

app.UseStatusCodePages();
app.UseStaticFiles();
app.UseAntiforgery();
```

It must be first because its `OnStarting` callback has to run *after* antiforgery has written its
headers, and `OnStarting` callbacks run last-registered-first.

Check the result:

```sh
curl -s -D - -o /dev/null https://<your-server-hostname>/ | grep -i "frame"
# Content-Security-Policy: frame-ancestors https://admin.shopify.com https://*.myshopify.com
# (no X-Frame-Options at all — that is correct)
```

## The app renders, but nothing is clickable

The page is static. In a Blazor Web App, interactivity must be requested explicitly:

```razor
@rendermode InteractiveServer
```

Without it the markup is correct, no handler is attached, and every control is inert. Confirm with
`node tools/verify-in-browser.mjs` — it reports how many elements the interactive renderer has
claimed. Zero means static.

## Buttons look perfect and do nothing

The JavaScript interop module did not load. Look for this in the server log:

```
fail: Polaris.Blazor.Interop.PolarisInterop[0]
      Polaris interop module './_content/Polaris.Blazor/polaris-interop.js' could not be loaded
```

Cause: the library project uses `Microsoft.NET.Sdk` instead of `Microsoft.NET.Sdk.Razor`, so the
module is never published as a static web asset. The build succeeds, the file is in `wwwroot`, and
the browser gets a 404.

```sh
curl -o /dev/null -w '%{http_code}\n' http://localhost:3458/_content/Polaris.Blazor/polaris-interop.js
# must be 200
```

## Pages render as unstyled text

Polaris did not load. Check that all three are in the page head and that the browser can reach
`cdn.shopify.com`:

- `<meta name="shopify-api-key" content="…" />`
- `<script src="…/app-bridge.js"></script>`
- `<script src="…/polaris-1.js"></script>`

## A modal never opens

Shopify only opens a modal in response to a user gesture. Do not call `ShowAsync()` from
`OnInitialized` — use a button with `CommandFor`, which is also the form that cannot fall foul of the
gesture requirement.

## The Admin header shows a progress bar that never stops

`shopify.loading(true)` without a matching `false`. The indicator persists until stopped. Use
`IAppBridgeInterop.WithLoadingAsync`, which clears it in a `finally`.

## "Only Button elements are allowed in the `primary-action` slot"

The slot content is wrapped in something. Polaris validates slot children and rejects a `div` that
merely contains a button, so the `slot` attribute has to go on the component itself:

```razor
<PolarisButton Slot="primary-action" Variant="PolarisButtonVariant.Primary">Save</PolarisButton>
```

## `Shopify:ApiSecret is not configured` at startup

The environment variables are not visible to the process. `AddShopifyBlazor` validates at startup on
purpose, so this fails immediately rather than on the first merchant request. Note that user secrets
and environment variables are read in that order, with environment variables winning.
