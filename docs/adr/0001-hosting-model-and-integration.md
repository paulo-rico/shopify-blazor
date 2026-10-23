# ADR 0001 — Hosting model, Shopify integration and interop conventions

- **Status:** Accepted
- **Date:** 2026-10-14
- **Phase:** 1 (research and technical foundations), with findings from phases 2–3 folded back in

## Context

The plan calls for the toolkit to let .NET developers build embedded Shopify admin apps in C#, Razor
and Shopify's Polaris **web components**, with as little hand-written JavaScript as possible.

Phase 1 asks for decisions on six things before component work starts: hosting model, Shopify
integration mechanism, authentication flow, JavaScript interop conventions, supported app target,
and supported .NET version. This records them, plus the four discoveries from building phases 2 and
3 that changed a decision or would have caused a silent failure.

## Decisions

### 1. Hosting model — Blazor Web App, Interactive Server

A single `Microsoft.NET.Sdk.Web` project with `AddRazorComponents().AddInteractiveServerComponents()`
and `AddInteractiveServerRenderMode()`, with the render mode applied per component tree
(`@rendermode InteractiveServer`).

**Why.** Admin UI is a thin layer over server-side Shopify API calls. Interactive Server keeps the
access token and the API secret on the server with no token plumbing to the client, and avoids
duplicating models and API clients into WebAssembly. The app's own JavaScript stays at zero.

**Cost.** A persistent SignalR circuit per merchant. Reconnection must be handled — a dropped
circuit is a page where clicks stop working.

**Rejected:** Blazor WebAssembly (would put the app's own API surface in the browser and duplicate
auth), Blazor Server on .NET 8-era hosting (superseded by the Web App model), and MVC/Razor Pages
(no component model, so wrappers would be tag helpers or partials and lose type safety).

**`@rendermode` is not optional.** A Blazor Web App is static by default: the markup renders
perfectly, no handler is ever attached, and every interactive element silently does nothing. This
cost real debugging time here; it is the first thing to check when "the component does not work".

### 2. Shopify integration — Polaris web components and App Bridge 4, both from the CDN

```html
<meta name="shopify-api-key" content="…" />
<script src="https://cdn.shopify.com/shopifycloud/app-bridge.js"></script>
<script src="https://cdn.shopify.com/shopifycloud/polaris-2.js"></script>
```

**Why.** Polaris React (`@shopify/polaris`) is deprecated and unmaintained; web components are its
supported successor and are plain custom elements, which is what makes a non-JavaScript framework
viable. Both are CDN-only — there is no npm package for App Bridge 4 — so the app has no bundler, no
`package.json` and no build step.

**`polaris-2.js`, not a pinned `polaris-2.0.js`.** The channel advances within Polaris 2, so
component fixes arrive without a script-tag change. Pin a `polaris-2.x.js` release instead when a
specific runtime must be frozen. There is no equivalent choice for App Bridge: that URL always
serves the current release.

**Why 2 rather than 1.** The 2.0 bundle carries both the current admin theme and Admin Next and
follows the merchant's admin theme, so one URL serves both looks. The element surface did not move
with it — `polaris-1.js` and `polaris-2.js` define the same 74 `s-*` names — so this decision changes
the theme, not the component set. (`polaris-2.0-rc.js` is the superseded release candidate, not a
channel; see [versioning](../versioning.md).)

**Rejected:** npm-installed Polaris React (deprecated), hand-written HTML/CSS approximating Polaris
(defeats the purpose, drifts from the admin's look), and App Bridge 3 packages (`createApp`,
`getSessionToken`, `@shopify/app-bridge-utils` — all removed in v4).

### 3. Authentication — App Bridge session token, verified server-side, exchanged for an access token

The browser mints a session token with `shopify.idToken()` and sends it as `Authorization: Bearer`.
The server verifies the signature against the shop's published JWKS, checks audience, issuer and the
`dest` claim, then exchanges the session token for an Admin API access token.

**Why.** This is the flow Shopify documents for embedded apps, and it is the only one where the
browser never holds an Admin API access token and no query string is ever trusted as identity.

Details, including why the browser makes the call rather than a Blazor event handler, are in
[authentication.md](../authentication.md).

### 4. JavaScript interop — one ES module, and components never touch `IJSRuntime`

All browser integration lives in two ES modules shipped as static web assets
(`polaris-interop.js`, `app-bridge.js`). Components declare which DOM events they want; a single
shared dispatcher routes forwarded events to the owning component's `EventCallback`.

**Why.** Custom elements dispatch DOM events that Blazor has no `@onchange` for, so a listener is
required either way. Centralising it means one place to get `addEventListener`/`removeEventListener`
right, one place to normalise event payloads, and one `<DotNetObjectReference>` per circuit instead
of one per component.

**Consequence for the component library:** wrappers are **plain C# classes**, not `.razor` files.
The Razor compiler injects its own `BuildRenderTree` into every `.razor` component, which silently
replaces a base-class implementation and renders **nothing at all**. A `@inherits`-based wrapper
therefore cannot work. `PolarisElementComponentBase.BuildRenderTree` is the single render
implementation, and consumers still use the components as ordinary Razor elements.

**Rules the wrappers follow:**

| Concern | Rule |
|---|---|
| Enum values | Rendered through `PolarisValueAttribute` to Shopify's exact spelling (`large-100`, `--show`). The .NET member name never reaches the browser. |
| Booleans | Presence attributes. `true` renders bare; `false` omits the attribute entirely. `disabled="false"` is still true to a custom element. |
| Numbers | Formatted with the invariant culture, or a comma decimal separator emits `value="1,5"`. |
| `value` / `checked` / `selected` | Set as DOM **properties** after the element upgrades — they are not reflected — and `value` is also rendered as `defaultValue` so the first paint is correct. |
| Slots | Set with the `Slot` parameter on the child component. Polaris **validates** slot children and rejects a wrapper element, so a `RenderFragment` cannot express a slot: a fragment cannot carry an attribute. |
| Events | A listener is attached only for callbacks that were supplied. Each one costs a listener and, on Server, a circuit round trip per occurrence. |
| Disposal | Listeners are detached in `DisposeAsync`. `[JSInvokable]` dispatch calls carry their own try/catch, so a late event from a torn-down circuit logs instead of throwing. |
| Styling | None. Polaris lives in a shadow root and cannot be restyled; appearance comes from attributes. |

### 5. Supported Shopify app target — full-page embedded admin app only

**Not** checkout UI extensions, POS extensions, customer account extensions or theme app
extensions. Those are restricted surfaces with their own runtimes, and none can host a normal
Blazor application. The embedded admin app is the target the plan names, and the only one this
toolkit claims.

### 6. Supported .NET version — .NET 10, pinned

`global.json` pins `10.0.100` with `rollForward: latestFeature`. .NET 10 is the current LTS-line SDK
on this machine and is what everything is tested against. Nothing in the toolkit uses .NET 10-only
language features, so lowering the target is a `TargetFramework` change plus a test run — but it is
untested and therefore unsupported.

## Findings that changed a decision

Each of these was discovered by building or testing, not by reading documentation. All four were
silent failures, which is why they are recorded here rather than only in a commit.

### A. The `dest` claim is a URL, not a hostname

Shopify sends `"dest": "https://shop.myshopify.com"`. A parser that accepts only a bare host rejects
**every** session token. `ShopDomain.TryNormalize` now accepts a bare name, a bare host and a full
URL, and rejects everything else — including `shop.myshopify.com.evil.example.com` and user-info
forms such as `http://shop.myshopify.com@evil.example.com`. The domain is validated *before* it is
interpolated into the token-exchange URL, because that request carries the API secret.

### B. Source-generated JSON ignores `JsonPropertyName` unless a policy is set

`TokenExchangeJsonContext` had `[JsonPropertyName("expires_in")]` on the property but no
`[JsonSourceGenerationOptions(PropertyNamingPolicy = …)]`. The generated context used the property
name verbatim, `expires_in` never bound, `ExpiresIn` stayed `null`, and every access token looked
non-expiring — defeating the cache-lifetime rule with nothing in the log. The policy is now stated
explicitly and a test asserts the parsed value.

### C. Two caches hold the access token, and a 401 must clear both

`ShopifyTokenExchanger` caches exchanged tokens in `IMemoryCache`; `IShopifyAccessTokenStore` is a
second, application-visible cache. Clearing only the store means that after an app-secret rotation —
which revokes every issued token — the exchanger keeps returning the dead token and every request
fails identically for ever. `IShopifyTokenExchanger.Invalidate` exists for this and is called from
the 401 path.

### D. A library that ships a JavaScript file must use the Razor SDK

With `<Project Sdk="Microsoft.NET.Sdk">`, the build succeeds, `wwwroot/polaris-interop.js` is
present in the source tree, and the browser gets a **404** for `/_content/Polaris.Blazor/…`: only the
Razor SDK produces the static web assets manifest. The symptom is a page where every button renders
perfectly and does nothing. `Polaris.Blazor` and `Shopify.Blazor` use `Microsoft.NET.Sdk.Razor` for
this reason.

## Consequences

**Good.** No npm, no bundler, no build step. Type-safe component parameters with Shopify's exact
values. The browser never holds a credential that can call the Admin API. The component library has
no dependency on the application framework, so it can be adopted alone.

**Costs, accepted.** A SignalR circuit per merchant needs reconnection handling. Polaris cannot be
restyled, so a design that needs custom CSS needs a different component rather than a workaround.
Some Polaris components are unmodelled; `AdditionalAttributes` forwards them, and adding a parameter
is a small, mechanical change. Component wrappers are C# rather than Razor, which is unusual enough
to need this explanation.

## References

- Polaris web components: <https://shopify.dev/docs/api/app-home/latest/web-components>
- App Bridge 4: <https://shopify.dev/docs/api/app-home/apis>
- Token exchange: <https://shopify.dev/docs/apps/build/authentication-authorization/access-tokens>
- API versioning: <https://shopify.dev/docs/api/usage/versioning>
