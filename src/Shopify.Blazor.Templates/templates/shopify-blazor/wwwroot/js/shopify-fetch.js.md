<!-- summary-of: shopify-fetch.js -->
<!-- source-sha256: e097896910c3 -->

# shopify-fetch.js

The sample's browser interop module. It mints an App Bridge session token, calls one of the app's own
endpoints with it as a bearer credential, and switches the Admin's header progress bar on and off;
application logic — what a product list means, what to do when a request fails — deliberately stays in
C#, and nothing here talks to Shopify's Admin API.

`wwwroot/js` · ES module · three exports, reached from C# over `IJSRuntime`

## Exported functions

| Function | Arguments | Returns | What it does |
|---|---|---|---|
| `isEmbedded` | none | `boolean` | `Boolean(window.shopify)` — true only inside the Shopify Admin, because App Bridge installs that global there. |
| `call` | `url` (string, endpoint on this origin), `method` (string, default `"GET"`), `body` (unknown, default `null`) | `Promise<{ok, status, data, error}>` | Fetches a fresh token with `window.shopify.idToken()`, sends the request with `Accept: application/json` and `Authorization: Bearer …`, and normalises the response into an envelope. |
| `loading` | `isLoading` (any, coerced with `Boolean`) | `boolean` | Starts or stops the Admin's header progress bar through `window.shopify.loading`. Returns `false` when App Bridge is absent, so the caller can tell the bar was not actually shown. |

`describe(status, data)` is private: it prefers `data.detail`, then `data.title`, and otherwise
returns `The request failed with HTTP {status}.` — the shape an ASP.NET ProblemDetails body has.

## Behaviour of `call`

- **No App Bridge means no request.** Without `window.shopify?.idToken` it returns
  `{ ok: false, status: 0, data: null, error: "App Bridge is not available. Open this page from inside
  the Shopify Admin." }` and never touches `fetch`.
- The token is minted immediately before each call and never cached, because session tokens live about
  a minute and a Blazor circuit can outlive several of them.
- A body is optional: `null` and `undefined` add no `Content-Type` and send no body. Anything else is
  JSON-stringified unless it is already a string, which passes through unchanged — so a caller can
  pre-serialise, and a caller that passes a plain object gets `application/json`.
- The response is read as text first, then parsed. An empty body becomes `null`; a body that is not
  JSON becomes the raw string. So an HTML error page from a proxy arrives as `data`, not as an
  exception.
- `ok` mirrors `response.ok`; `error` is `null` on success and is derived from the body on failure. The
  HTTP status is always carried through, including on success.

## Gotchas

- **`status: 0` is not an HTTP status.** It is the value the "App Bridge is not available" branch
  returns: no request was ever sent, so there is no HTTP status to report. Code that only inspects
  `status` reads it as a server fault.
- **The progress bar persists until it is explicitly stopped.** `loading(true)` has no timeout, so
  every caller must pair the two calls; `IAppBridgeInterop.WithLoadingAsync` is the C# wrapper that
  guarantees the `false` in a `finally`.
- **There is deliberately no `componentStatus` export.** The component library already ships one,
  reached from C# through `IPolarisInterop.GetComponentStatusAsync`; a second copy in the app made it
  easy to call `IJSRuntime.InvokeAsync("componentStatus", …)`, which cannot work for an ES module
  export — it throws "could not find componentStatus" and the page then reports a broken runtime
  check that says nothing about the elements.
- No retry, no 401 handling and no timeout live here. A 401 is returned as data for the caller to
  interpret, which is why the products page inspects the status itself.
- A transport failure (offline, DNS, a refused connection) is **not** turned into an envelope: `fetch`
  rejects and `call` rejects with it, so a caller must handle both a failed envelope and a thrown
  promise.

## See also

- [Products.razor.md](../../Components/Pages/Products.razor.md) — the page that consumes this module through C#.
- [reconnect-modal.js.md](reconnect-modal.js.md) — the other browser script the sample ships.
- [BearerTokens.cs.md](../../Services/BearerTokens.cs.md) — the server-side reader for the token sent here.
