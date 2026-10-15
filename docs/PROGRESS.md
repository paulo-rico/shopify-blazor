# Progress

What is true **now**. `AGENTS.md` is what is always true; this file changes every session.

Last updated: 2026-10-15 — phases 1–3 complete; the Polaris component inventory is now complete.

## State

| Area | State |
|---|---|
| Solution, CI, docs | Done. `dotnet test` = 115 passed, 0 failed. |
| `Polaris.Blazor` | 59 components — every documented Polaris element — plus the interop module. The first 19 element types are browser-verified; the second pass is covered by unit tests only. |
| `Shopify.Blazor` | Session token validation, token exchange, Admin API client, App Bridge interop. |
| Reference app | Embedded shell, dashboard, products, component gallery. Authenticated `/api/products`. |
| Templates and CLI | **Not started.** Plan in `docs/templates-plan.md`. |
| Real development store | **Not done.** Needs credentials and a tunnel. The app now embeds in the Admin; a rejected token is diagnosed rather than guessed at. |

## Verified, with how

| Check | Method | Result |
|---|---|---|
| Component rendering, defaults, booleans, slots, attribute forwarding | bUnit, 65 tests | pass |
| Session token validation (signature, audience, expiry, cross-shop, hostile `dest`) | Real RS256 keys + scripted JWKS, 15 tests | pass |
| Token exchange (RFC 8693 parameters, caching, per-merchant keys, errors) | Scripted HTTP, 11 tests | pass |
| Admin API (GraphQL errors on 200, throttling code, version fall-forward, 401 recovery) | Scripted HTTP, 7 tests | pass |
| Polaris elements upgrade with a shadow root | Headless Chrome, DevTools Protocol | 19 element types from the first pass, 0 failures |
| Custom event → C# round trip | Real click in headless Chrome | counter 0 → 1 |
| Polaris form submits its values | Real form submission | `{"q":"signet","status":"draft"}` |
| `commandFor` declarative modal wiring | Headless Chrome | present, no JavaScript |
| `/api/products` rejects missing, malformed and `alg: none` tokens | `curl` | 401, 401, 401 |
| The app can be framed by the Admin, and not by anything else | Chrome iframe test both ways | allowed origin frames it; any other is refused |
| Session token diagnostics report the real reason for a rejection | Real RS256 token with a mismatched `aud` | names the audience mismatch |

## Next task, in order

1. **Install in a real development store.** The one unverified step. Needs `SHOPIFY_API_KEY`,
   `SHOPIFY_API_SECRET` and an ngrok tunnel; the steps are in `docs/getting-started.md`. Expect to
   find things a test cannot: the Admin's iframe sandbox, CSP behaviour, and the App Bridge handshake
   in a real host.
2. **Reconnection behaviour.** A dropped SignalR circuit leaves a page whose controls do nothing.
   Verify, then handle.
3. **Phase 4, the `dotnet new` template.** Plan in `docs/templates-plan.md`. Build `doctor` from
   phase 5 alongside it: it is the cheapest way to stop the silent failures recurring.
4. **Browser-verify the second component pass.** The 30 wrappers added on 2026-10-15 are
   unit-tested against their rendered markup, but no browser has upgraded `s-grid`, `s-choice-list`
   or `s-drop-zone` yet. Extend `tools/verify-in-browser.mjs`'s element list and the component
   gallery, then run it.

## Resolved since the first push

- **The app could not be embedded at all.** `UseAntiforgery()` sets `X-Frame-Options: SAMEORIGIN` and
  `frame-ancestors 'self'`, which refuse the Admin's iframe. `UseShopifyEmbeddedFraming()` replaces
  them with a policy naming the Admin origins. See `AGENTS.md` item 12.
- **`Shopify.Blazor` shipped without `app-bridge.js`**, because the package used the plain SDK.
- **Every page request logged an authentication warning**, because the session token scheme is the
  default and reported a failure even when no token was presented. It now returns `NoResult` and logs
  at debug, so a real rejection stands out.
- **`ClearedProperties` never reached the interop layer.** The base class documented "DOM properties
  cleared on every render" and gated its property write on that collection, but only ever called
  `SetPropertiesAsync`. Nothing used it until `PolarisChoiceList` arrived, where a stale `values`
  array keeps options selected that the server has deselected. The base now calls
  `ClearPropertiesAsync` before the write.

## Known gaps

- **Reconnection** is untested and unhandled.
- **Component coverage** is complete: all 50 documented top-level elements, plus 10 composed
  sub-elements, in 59 wrappers (`s-text` and `s-paragraph` share `PolarisText`). Every wrapper is
  named in a test. What is *not* done is browser verification of the second pass — see above.
- **`s-drop-zone`'s `files` property is not modelled.** It is a read-only array of browser `File`
  objects, which cannot cross the interop boundary, so the upload path belongs in JavaScript. The
  wrapper exposes the label, `accept`, `multiple` and the events, and can only reset the zone.
- **`PolarisTable` pagination events** (`nextpage`/`previouspage`) are wired but not exercised,
  because the products page does not paginate.
- **No integration test project.** `tests/Shopify.Blazor.IntegrationTests` was in the plan and was
  not created; `WebApplicationFactory`-based tests of `/api/products` would be the natural home.
- **`Polaris.Blazor.Interop` was not split into its own project.** The interop module lives inside
  `Polaris.Blazor` as one JavaScript file plus a small service, which is simpler than a project
  boundary for that little code. Split it if the interop grows.
- **No NuGet publish.** `dotnet pack` produces both packages; nothing is published to nuget.org yet.
- **Node/Shopify CLI is unused.** No extensions, so no `package.json` and no bundler anywhere.

## Session notes

- The four silent failures worth knowing about are documented in `AGENTS.md`. Three were found by
  tests and one by the headless browser.
- `docs/components.md` is generated. Run `python3 tools/generate-component-reference.py` after
  touching a component.
- The verification script and the doc generator are both dependency-free on purpose: no npm, no
  test framework, nothing to keep updated.
