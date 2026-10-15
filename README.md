# Shopify Blazor Toolkit

Build embedded Shopify admin apps in C#, Razor and Shopify's own Polaris web components — with no
hand-written JavaScript for ordinary interactions, and no reimplementation of Shopify's design
system.

Status: **proof of concept**, working end to end against the real runtime. See
[What is actually verified](#what-is-actually-verified) before trusting any of it.

## What this is

| Project | Purpose |
|---|---|
| `src/Polaris.Blazor` | Razor components wrapping Shopify's Polaris web components |
| `src/Shopify.Blazor` | Embedded-app authentication, App Bridge 4 interop, Admin API client |
| `samples/Shopify.Blazor.SampleApp` | Reference app: embedded shell, authenticated Admin API call |
| `tests/` | 79 tests: component rendering, session token validation, token exchange, Admin API, embedded framing |

The split is deliberate: `Polaris.Blazor` has **no dependency** on `Shopify.Blazor`, so the
component library can be used on its own.

## How it works

Shopify replaced Polaris React with **Polaris web components** (`<s-page>`, `<s-button>`,
`<s-table>`, …) — framework-agnostic custom elements loaded from a CDN. They are plain HTML, so
React stops being necessary for a Shopify admin UI, and Razor becomes a first-class option.

Three facts drive the design:

1. **You cannot restyle them.** Their internals live in a shadow root and follow the merchant's
   admin branding. Appearance comes from attributes, never from your CSS.
2. **Attributes and properties differ.** A boolean is a *presence* attribute — `disabled="false"`
   is still `true` to a custom element — and `value`/`checked` are not reflected at all.
3. **Custom events are DOM events.** They need a listener; Blazor has no `@onchange` for them.

So a wrapper's whole job is: render the element, emit the right attributes, and attach listeners for
the events you actually subscribe to. That is what `Polaris.Blazor` does, and nothing more.

```razor
<PolarisPage Heading="Products">
    <PolarisSection Heading="Recently updated">
        <PolarisTable Loading="@isLoading">
            <PolarisTableHeaderRow>
                <PolarisTableHeader ListSlot="PolarisTableListSlot.Primary">Title</PolarisTableHeader>
                <PolarisTableHeader Format="PolarisTableHeaderFormat.Currency">Price</PolarisTableHeader>
            </PolarisTableHeaderRow>
            <PolarisTableBody>
                @foreach (var product in products)
                {
                    <PolarisTableRow @key="product.Id">
                        <PolarisTableCell><PolarisText Type="PolarisTextType.Strong">@product.Title</PolarisText></PolarisTableCell>
                        <PolarisTableCell>@product.Price</PolarisTableCell>
                    </PolarisTableRow>
                }
            </PolarisTableBody>
        </PolarisTable>
    </PolarisSection>
</PolarisPage>
```

## Quick start

```sh
dotnet run --project samples/Shopify.Blazor.SampleApp
```

It starts on <http://localhost:3458> and renders the real Polaris components even without
configuration. Outside the Shopify Admin, App Bridge is absent, so the app says so instead of
looking broken.

To use it against a development store you need three things — see
[docs/getting-started.md](docs/getting-started.md):

```sh
export SHOPIFY_API_KEY=...      # Partner Dashboard
export SHOPIFY_API_SECRET=...   # Partner Dashboard — server-side only
export SHOPIFY_APP_URL=https://your-tunnel.ngrok-free.app
```

## What is actually verified

Everything below was run on this machine, not asserted from documentation.

| Check | Result |
|---|---|
| `dotnet test` — both suites | **79 passed, 0 failed** |
| Polaris components upgrade with a shadow root in Chrome | **19 element types, 0 failures** |
| Custom-event → C# round trip in a live browser | **counter 0 → 1 after a real click** |
| Polaris form submits its values | **`{"q":"signet","status":"draft"}`** |
| `commandFor` declarative modal wiring | **present, no JavaScript** |
| `/api/products` with no token | **401** |
| `/api/products` with a malformed token | **401** |
| `/api/products` with an unsigned `alg: none` token | **401** |
| Embeddable by the Admin (the framing policy) | **allowed origin frames it; any other is refused** |

**Not verified:** installation in a real Shopify development store. That needs Partner Dashboard
credentials and a public HTTPS tunnel, so it is the one step a human has to do. The app is built for
it and the authentication path is covered by tests against a real signed-key path, but the last
mile is untested. Treat it as the next task, not as done.

### The verification tooling

`tools/verify-in-browser.mjs` drives headless Chrome over the DevTools Protocol with **no npm
dependencies** (Node 24's built-in `WebSocket` is enough). It is the only way to answer the
questions that matter — whether a custom element actually upgraded, whether a form value actually
reaches the request, whether a click actually reaches C#:

```sh
dotnet run --project samples/Shopify.Blazor.SampleApp &
node tools/verify-in-browser.mjs http://127.0.0.1:3459/components
```

`tools/generate-component-reference.py` regenerates
[docs/components.md](docs/components.md) from the component sources, so the reference cannot drift
from the code.

## The bug that stopped the app being embeddable at all

Worth its own section, because it is the one that breaks installation and says nothing in the log.

Blazor's rendering pipeline requires `app.UseAntiforgery()`. That middleware sets, on every response
that asks for a token:

```
X-Frame-Options: SAMEORIGIN
Content-Security-Policy: frame-ancestors 'self'
```

Both refuse the Admin's iframe. The app therefore **can never be embedded**, while rendering
perfectly in an ordinary browser tab — which is exactly why it is easy to miss. The browser console
says it:

```
Framing 'https://your-app/' violates the following Content Security Policy directive:
"frame-ancestors 'self'". The request has been blocked.
```

`app.UseShopifyEmbeddedFraming()` replaces those headers with a policy that names the Admin origins.
It does **not** weaken clickjacking protection: `X-Frame-Options` cannot express "these origins
only" (`ALLOW-FROM` is dead), so it is removed in favour of `frame-ancestors`, and the app stays
unframeable by anything except the origins you list. Register it **first**, because its `OnStarting`
callback has to run after antiforgery's, and those run last-registered-first.

Verified in Chrome: an origin in the policy frames the app with zero violations; an origin not in
the policy is refused and the document is never even served.

## Three bugs the tests found

Worth reading, because each one is invisible in normal use:

1. **The `dest` claim is a URL, not a host.** Shopify sends
   `"dest": "https://shop.myshopify.com"`, and the first version of the parser only accepted a bare
   host — so *every* session token would have been rejected. Caught by the happy-path test.
2. **`expires_in` was never read.** The source-generated JSON context had no naming policy, so
   Shopify's snake_case `expires_in` silently failed to bind and every token looked non-expiring,
   defeating the cache-lifetime logic with nothing in the log.
3. **A revoked token could never be discarded.** The exchanger has its own cache, and the 401
   handler only cleared the token store. After an app-secret rotation — which revokes every issued
   token — the app would have reused the dead token for ever, and the documented recovery path did
   not work.

A fourth was found by the headless browser rather than by a test: **the library shipped as a plain
`Microsoft.NET.Sdk` project**, so its JavaScript module was never published as a static web asset.
The build succeeded, the file looked present, and the browser got a 404 — every button rendered
perfectly and did nothing.

## Documentation

| Document | Contents |
|---|---|
| [Getting started](docs/getting-started.md) | Shopify app, tunnel, configuration, first authenticated call |
| [Troubleshooting](docs/troubleshooting.md) | Diagnosing a rejected session token, and the framing trap |
| [Component reference](docs/components.md) | All 29 components, generated from source |
| [Authentication](docs/authentication.md) | Session tokens, token exchange, what not to do |
| [ADR 0001](docs/adr/0001-hosting-model-and-integration.md) | Hosting model, integration and interop decisions |
| [Templates plan](docs/templates-plan.md) | Phase 4–6: `dotnet new`, CLI, generation |
| [Progress](docs/PROGRESS.md) | Current state, for whoever picks this up next |

## Security

- A session token is **verified** against the shop's published JWKS before any claim in it is
  believed. No query string, form field or cookie is ever treated as identity.
- The shop domain is validated against a strict `*.myshopify.com` pattern *before* it is
  interpolated into the token-exchange URL, because that request carries the API secret.
- The API secret and the Admin API access token are **server-side only** and never rendered.
- An ID token is never used as an Admin API access token. The exchange happens on the server.
- The app does not weaken iframe, cookie or origin protections to work around embedding.

The tests cover forged signatures, wrong audiences, expired tokens, `alg: none`, cross-shop tokens,
hostile `dest` values, and non-`myshopify.com` domains.

## Requirements

- **.NET 10 SDK** (pinned in `global.json`)
- A browser (for the verification script)
- A Shopify Partner account and a development store, for the parts that need a store

No npm, no bundler and no build step for the UI: Polaris and App Bridge both come from Shopify's
CDN.

## License

MIT — see [LICENSE](LICENSE).
