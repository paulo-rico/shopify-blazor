<!-- summary-of: app-bridge.js -->
<!-- source-sha256: b9053257a334 -->

# app-bridge.js

The browser half of App Bridge 4: one ES module that mints a session token, calls the app's own
endpoints with it, and drives the Admin's progress bar, toasts, navigation, current user, granted
scopes and product picker. Every function is guarded, because `window.shopify` exists inside the
Shopify Admin and nowhere else — a page opened directly must still render and be able to say what is
missing rather than die on an undefined global.

`Shopify.Blazor/wwwroot` · ES module at `/_content/Shopify.Blazor/app-bridge.js` · ten exports, no dependencies, no module state

## Members

| Function | Signature | What it does |
|---|---|---|
| `isAvailable` | `(): boolean` | `Boolean(window.shopify)`. True only when the page is embedded in the Admin **and** App Bridge loaded. Synchronous, so a caller can decide before doing any work. |
| `idToken` | `(): Promise<string>` | A freshly minted App Bridge session token (a JWT). Throws — via `requireBridge` — outside the Admin. Tokens live on the order of a minute, so it is fetched per request and never cached. |
| `fetchWithSessionToken` | `(url, options = {})` | Mints a token, then `fetch`es `url` with it as a bearer credential. Returns `{ ok, status, data, error }`. See the envelope below. |
| `loading` | `(isLoading): boolean` | Starts or stops the Admin's header progress bar with `Boolean(isLoading)`. Returns false when App Bridge has no `loading` and nothing was called. |
| `toast` | `(message, options = {}): boolean` | Shows `message` with `{ isError, duration }`. Returns false for a blank/absent message or no `toast` API, and never calls App Bridge in that case. |
| `navigate` | `(url): Promise<boolean>` | Goes through `shopify.navigation.navigate`. Outside the Admin, or without that API, it falls back to `window.location.assign(url)` and returns **false**. |
| `currentUser` | `(): Promise<object\|null>` | The merchant using the app, or null when unavailable or the call rejects. |
| `grantedScopes` | `(): Promise<string[]\|null>` | `shopify.scopes.query()`, or null when the API is missing, the call rejects, or the answer is not an array. |
| `context` | `(): { shop, host, locale, apiKey } \| null` | Synchronous read of `shopify.config`, each field `?? null`; null entirely outside the Admin. |
| `pickProducts` | `(multipleLimit = null): Promise<Array\|null>` | Opens Shopify's product picker and maps the selection. Returns null when there is no picker **or** when the merchant cancelled; see Gotchas. |

Private helpers, listed because their behaviour is visible through the exports: `bridge()` returns
`window.shopify`; `requireBridge(api)` throws an `Error` naming the missing API and pointing at the
`shopify-api-key` meta tag and `app-bridge.js` script; `describeError(status, data)` builds the
human-readable failure text.

## What `fetchWithSessionToken` actually does

- The token is minted **before** the request, deliberately: a long-lived Blazor circuit holding a
  token would present a stale one, so the token is as fresh as the call.
- Headers are `Accept: application/json` and `Authorization: Bearer <token>`, then `options.headers`
  merged last — a caller's header wins, including `Authorization`.
- A body that is neither `null`/`undefined` nor a string is `JSON.stringify`-ed and given
  `Content-Type: application/json`; a string body is sent as-is with no content type.
- The method is `options.method`, else `"GET"` when the body is `undefined`, else `"POST"`.
- The response text is parsed as JSON; if parsing fails the raw text is returned as `data`, and an
  empty body gives `data = null`.
- `error` is null on success, otherwise the first of: `data.error`, `data.title`, `data.detail`, a
  string body shorter than 400 characters, and `"The request failed with HTTP <status>."`.

## Behaviour that is not visible in the signature

- **Two failure styles, on purpose.** `idToken` and `fetchWithSessionToken` throw, because a token is
  the whole point of the call and a silent empty string would be worse. Everything else returns
  `false` or `null`, so a standalone page renders perfectly and simply cannot do the Admin thing.
- The picker request is `{ type: "product", action: "select", multiple: true, variants: true }` plus
  `multipleLimit` when given. `select` rather than App Bridge's default `add`, so the merchant cannot
  create products from inside the picker; `variants: true` because a shop prices pieces, not
  products.
- Each picked product is normalised to `{ id, title, handle, imageUrl, variants[] }`, with
  `imageUrl` from `images[0].originalSrc` and every value `?? null`. Each variant carries
  `{ id, title, price, sku, options }`, where `options` is built from `selectedOptions` as a
  name → value map, and `price` stays a **string** because that is how Shopify states money.

## Gotchas

- **`pickProducts` returns null for two different events.** A cancelled picker resolves `undefined`,
  not an empty array, so "cancelled" and "App Bridge is unavailable" arrive as the same value and
  only an empty list means "chose none and meant it". Treating null as an empty list clears a
  selection the merchant never touched.
- **It is also the only function with no `try`/`catch` around the App Bridge call.** A rejected
  `shopify.resourcePicker` promise propagates to the caller; `currentUser` and `grantedScopes` catch
  and answer null instead.
- **`body: null` does not mean GET.** The JSON branch tests for null but the method default only
  tests `undefined`, so a null body produces a `POST` with no body and no `Content-Type`.
- **A caller-supplied `Authorization` silently replaces the session token.** The merge order is
  intentional (it is the escape hatch for a different credential) but nothing warns.
- **`loading(true)` with no `loading(false)` leaves the bar running in the merchant's admin for the
  rest of the session.** Nothing in JS clears it; the C# side pairs the calls in a `finally`, and any
  new call site must do the same.
- **`toast` drops a `duration` of `0`** because it is added under a truthiness test, and it always
  sends `isError` (false by default). A blank message is refused before App Bridge is touched.
- **`multipleLimit` is only forwarded when truthy**, so `0` is dropped and a negative value is passed
  through unchecked.
- **`context()` is documented as returning `platform`, and returns `apiKey` instead.** No C# or
  template code in this repository imports it — `AppBridgeInterop` invokes the other nine exports by
  name — so it is for direct JS use only.
- **`navigate` returns `false` after starting a real navigation**, so a caller that treats the
  return value as "did not navigate" is wrong when the fallback is what ran.
- **Every export is resolved by name from C#** (`module.InvokeAsync("…")`), so renaming one here
  produces no build error — only a runtime `JSException` on the first call, on a page that otherwise
  renders.

## See also

- [../AppBridge/AppBridgeInterop.cs.md](../AppBridge/AppBridgeInterop.cs.md) — the C# wrapper that imports this module and maps each export onto `IAppBridgeInterop`.
- [../Components/ShopifyAppBridgeHead.cs.md](../Components/ShopifyAppBridgeHead.cs.md) — emits the `shopify-api-key` meta tag and the CDN script this module depends on.
- [../../Polaris.Blazor/wwwroot/polaris-interop.js.md](../../Polaris.Blazor/wwwroot/polaris-interop.js.md) — the other JS module in the toolkit, with the same "guard everything" shape.
