<!-- summary-of: EmbeddedFramingExtensions.cs -->
<!-- source-sha256: 547df82d80db -->

# EmbeddedFramingExtensions.cs

Makes the app frameable by the Shopify Admin and by nothing else, by replacing the two headers that
would otherwise refuse the Admin's iframe. It exists because Blazor's rendering pipeline requires
`app.UseAntiforgery()`, which sets `X-Frame-Options: SAMEORIGIN` and `frame-ancestors 'self'` on the
responses that ask for a token — so an embedded app can look perfect in a normal tab and never load
inside the Admin.

`Shopify.Blazor` · middleware extension + options class · call `app.UseShopifyEmbeddedFraming()` first

## Members

| Member | Signature | What it does |
|---|---|---|
| `UseShopifyEmbeddedFraming` | `static IApplicationBuilder UseShopifyEmbeddedFraming(this IApplicationBuilder app, Action<ShopifyEmbeddedFramingOptions>? configure = null)` | Throws `ArgumentNullException` for a null `app`. Builds the options object, runs `configure`, and registers a middleware whose `OnStarting` callback removes `X-Frame-Options` and writes the `Content-Security-Policy` header. Returns the same `IApplicationBuilder`, so it chains. |
| `ShopifyEmbeddedFramingOptions.FrameAncestors` | `IReadOnlyList<string>` (get-only) | The origins the application added — **not** the defaults; they are combined only when the policy is resolved. |
| `ShopifyEmbeddedFramingOptions.AllowFrameAncestor` | `ShopifyEmbeddedFramingOptions AllowFrameAncestor(string origin)` | Adds one origin. Throws `ArgumentException` for null or white space; ignores a duplicate (ordinal, case-insensitive); returns `this`, so calls chain. |
| `ShopifyEmbeddedFramingOptions.Resolve` | `internal IEnumerable<string> Resolve()` | `https://admin.shopify.com`, `https://*.myshopify.com`, then any added origin not already present. Internal: not part of the public surface. |

## Behaviour

- **The policy string is built once, at registration.** `"frame-ancestors " + string.Join(' ', Resolve())`
  is computed before the middleware delegate is created, so mutating the options object afterwards has
  no effect on a running pipeline. Only `configure` — which runs inside the call — can still change it.
- **`OnStarting` is the mechanism, not a header write at the top of the delegate.** Anti-forgery
  registers its own `OnStarting` callback later, during endpoint execution, and `OnStarting` callbacks run
  last-registered-first — so this one runs *after* anti-forgery has written its headers, which is the
  only ordering that lets the framing policy replace them instead of losing to them.
- **Both headers are dealt with.** `X-Frame-Options` is removed outright, because it cannot express an
  allow-list (`ALLOW-FROM` is dead and unsupported); the protection is re-aimed as a `frame-ancestors`
  policy naming the Admin origins. The app stays unframeable by every other site.
- **Only `frame-ancestors` is written**, deliberately, so the policy does not accidentally restrict
  scripts, styles or connections.
- **The default allow-list is short and explicit** — the Admin host and any `*.myshopify.com` store
  subdomain — rather than a wildcard. An origin that fronts the app during development (another machine
  on the network, a proxy) is an ordinary HTTPS origin and is added with `AllowFrameAncestor`.

## Gotchas

- **Registration order is the whole trick, and getting it wrong is silent.** Register this **before**
  anything that writes headers, in particular before `UseAntiforgery`. Registered later, its `OnStarting`
  callback runs *first*, anti-forgery overwrites the framing headers afterwards, and the app is
  unframeable again — with no build error and no server log. The browser console says
  `Framing '…' violates … "frame-ancestors 'self'"`, which reads like a Shopify problem.
- **The CSP header is assigned whole, not merged.** Any policy another component wrote earlier to the
  same `Content-Security-Policy` header is replaced, so an application that needs `default-src` or
  `connect-src` must not expect this middleware to preserve it.
- **`X-Frame-Options` is removed from every response**, not just HTML ones. Framing protection for the
  whole app rests on the CSP; a browser or proxy that ignores `frame-ancestors` gets no protection at all
  from this file.
- **An origin, not a URL.** `AllowFrameAncestor("https://admin.shopify.com/")` or a path is accepted by
  the method and produces a source that never matches, so the app is unframeable with a policy that looks
  correct.
- **`FrameAncestors` is not the effective list.** Reading it back shows only what the application added;
  the Admin defaults appear in `Resolve()` and in the header.
- **This governs who may frame the app, not what the app may frame.** There is no `frame-src` here.

## See also

- [ShopifyServiceCollectionExtensions.cs.md](ShopifyServiceCollectionExtensions.cs.md) — the service
  registrations the pipeline is built around.
- [Components/ShopifyAppBridgeHead.cs.md](Components/ShopifyAppBridgeHead.cs.md) — the head output that
  makes the page an App Bridge app in the first place.
- [ShopifyOptions.cs.md](ShopifyOptions.cs.md) — the app's other configuration, including the API key the
  session token is validated against.
