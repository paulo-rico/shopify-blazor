# Progress

What is true **now**. `AGENTS.md` is what is always true; this file changes every session.

Last updated: 2026-10-14 — phases 1–3 (immediate deliverables) complete.

## State

| Area | State |
|---|---|
| Solution, CI, docs | Done. `dotnet test` = 62 passed, 0 failed. |
| `Polaris.Blazor` | 29 components + interop module. Verified in a real browser. |
| `Shopify.Blazor` | Session token validation, token exchange, Admin API client, App Bridge interop. |
| Reference app | Embedded shell, dashboard, products, component gallery. Authenticated `/api/products`. |
| Templates and CLI | **Not started.** Plan in `docs/templates-plan.md`. |
| Real development store | **Not done.** Needs credentials and a tunnel. |

## Verified, with how

| Check | Method | Result |
|---|---|---|
| Component rendering, defaults, booleans, slots, attribute forwarding | bUnit, 29 tests | pass |
| Session token validation (signature, audience, expiry, cross-shop, hostile `dest`) | Real RS256 keys + scripted JWKS, 15 tests | pass |
| Token exchange (RFC 8693 parameters, caching, per-merchant keys, errors) | Scripted HTTP, 11 tests | pass |
| Admin API (GraphQL errors on 200, throttling code, version fall-forward, 401 recovery) | Scripted HTTP, 7 tests | pass |
| Polaris elements upgrade with a shadow root | Headless Chrome, DevTools Protocol | 19 element types, 0 failures |
| Custom event → C# round trip | Real click in headless Chrome | counter 0 → 1 |
| Polaris form submits its values | Real form submission | `{"q":"signet","status":"draft"}` |
| `commandFor` declarative modal wiring | Headless Chrome | present, no JavaScript |
| `/api/products` rejects missing, malformed and `alg: none` tokens | `curl` | 401, 401, 401 |

## Next task, in order

1. **Install in a real development store.** The one unverified step. Needs `SHOPIFY_API_KEY`,
   `SHOPIFY_API_SECRET` and an ngrok tunnel; the steps are in `docs/getting-started.md`. Expect to
   find things a test cannot: the Admin's iframe sandbox, CSP behaviour, and the App Bridge handshake
   in a real host.
2. **Reconnection behaviour.** A dropped SignalR circuit leaves a page whose controls do nothing.
   Verify, then handle.
3. **Phase 4, the `dotnet new` template.** Plan in `docs/templates-plan.md`. Build `doctor` from
   phase 5 alongside it: it is the cheapest way to stop the silent failures recurring.
4. **More components.** `PolarisChoiceList` first — it is the only multi-select and exercises the
   array-property path.

## Known gaps

- **Reconnection** is untested and unhandled.
- **Component coverage** is 29 of ~48. The generated `docs/components.md` marks which components are
  named in a test; the rest have no test.
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
