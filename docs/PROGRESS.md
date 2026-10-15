# Progress

What is true **now**. `AGENTS.md` is what is always true; this file changes every session.

Last updated: 2026-10-15 — phases 1–7 done. The component inventory is complete, the template and CLI
exist and are verified, and the release machinery is prepared but unused.

## State

| Area | State |
|---|---|
| Solution, CI, docs | Done. `dotnet test` = 246 passed, 0 failed across three suites. |
| `Polaris.Blazor` | 59 components — all 50 documented top-level elements plus 10 composed sub-elements — and the interop module. All 60 element names are asserted in a real browser. |
| `Shopify.Blazor` | Session token validation, token exchange, Admin API client, App Bridge interop, embedded framing. |
| Reference app | Embedded shell, dashboard, products, and a gallery that renders every wrapped component. Authenticated `/api/products`. Reconnection dialog wired. |
| `Shopify.Blazor.Templates` | `dotnet new shopify-blazor`, verified by packing, installing, generating and building all four parameter combinations. |
| `Shopify.Blazor.Cli` | `new`, `add page`, `add component`, `configure`, `doctor`. 115 tests; `doctor` fails on a committed secret and on a missing render mode. |
| Component inventory | `docs/component-inventory.{json,md}` generated from a hand-maintained element spec joined with the C# sources, with a drift check. All 60 wrappers are named in a test. |
| Release machinery | `CHANGELOG.md`, `docs/versioning.md`, and a tag-triggered `release.yml` that packs, verifies each package's contents and publishes only if `NUGET_API_KEY` is set. **Nothing is tagged or published.** |
| Real development store | **Not done.** Needs credentials and a public HTTPS server. The app embeds in the Admin; a rejected token is diagnosed rather than guessed at. |

## Verified, with how

| Check | Method | Result |
|---|---|---|
| Component rendering, defaults, booleans, slots, attribute forwarding | bUnit, 81 tests | pass |
| Session token validation (signature, audience, expiry, cross-shop, hostile `dest`) | Real RS256 keys + scripted JWKS, 15 tests | pass |
| Token exchange (RFC 8693 parameters, caching, per-merchant keys, errors) | Scripted HTTP, 11 tests | pass |
| Admin API (GraphQL errors on 200, throttling code, version fall-forward, 401 access-denied, 401 recovery) | Scripted HTTP, 7 tests | pass |
| CLI commands, parser, doctor checks, page generation | xunit with injected seams, 115 tests | pass |
| Wrapper generator and inventory drift | stdlib `unittest`, 46 tests | pass |
| Every generated file matches its generator | `tools/check-drift.py` | 3 of 3 in sync |
| `dotnet new shopify-blazor` produces a project that builds | `tools/verify-template.py`: pack, install, generate, build | 4 of 4 combinations |
| Polaris elements defined, upgraded and present | Headless Chrome + DevTools Protocol, `--assert` | 60 of 60, 0 failures |
| Custom event → C# round trip | Real click in headless Chrome | counter 0 → 1 |
| Polaris form submits its values | Real form submission | `{"q":"signet","status":"draft"}` |
| Static assets the UI needs are served | `fetch` from the page | 200 for all four, including `/_content/Polaris.Blazor/polaris-interop.js` |
| Reconnection dialog present and wired | Headless Chrome | element, stylesheet and script all present |
| `commandFor` declarative modal wiring | Headless Chrome | present, no JavaScript |
| Table rows live inside `s-table-body` | Headless Chrome | yes |
| `/api/products` rejects missing, malformed and `alg: none` tokens | `curl` | 401, 401, 401 |
| The app can be framed by the Admin, and not by anything else | Chrome iframe test both ways | allowed origin frames it; any other is refused |
| Session token diagnostics report the real reason for a rejection | Real RS256 token with a mismatched `aud` | names the audience mismatch |

## Next task, in order

1. **Install in a real development store.** Still the one unverified step. Needs `SHOPIFY_API_KEY`,
   `SHOPIFY_API_SECRET` and a server Shopify can reach over HTTPS; the steps are in
   `docs/getting-started.md`. Expect to find what a test cannot: the Admin's iframe sandbox, CSP
   behaviour, and the App Bridge handshake in a real host.
2. **Induce a circuit loss.** The reconnection dialog is present and its assets are served, but
   nothing has actually killed a circuit and watched the merchant get back. Stop the server mid-page
   and confirm the dialog appears, `Retry` recovers, and a rejected circuit reloads.
3. **Publish.** Tag `v0.1.0-preview.1` when the two above are done; `release.yml` does the rest.
   `CHANGELOG.md` already describes everything since the first commit.
4. **Widen what the wrapper generator accepts.** It refuses 29 of the 60 elements today, each for a
   real reason (DOM properties, the `values` array, validated slots, events with no `PolarisEvents`
   constant). The lever is adding those constants and slot parameters, not loosening the generator:
   see `docs/templates-plan.md`.

## Resolved on 2026-10-15

- **Browser verification was partly vacuous.** The old element list included the six table elements,
  which the gallery rendered only *after* a click — so `found.every(...)` on an empty list reported
  success for elements that were not on the page at all. The probe now counts instances and fails
  when a listed element was never rendered, which is how that was found.
- **Clearing an array-valued property with `undefined` throws inside Polaris.**
  `s-choice-list.values` is read with array methods, so resetting it produced six uncaught
  `TypeError`s per render while the markup looked perfect. `PolarisChoiceList` now always writes an
  array, and the interop layer clears an array property to `[]`. See `AGENTS.md` item 19.
- **`ClearedProperties` never reached the interop layer.** The base class documented "DOM properties
  cleared on every render" and gated its property write on that collection, but only ever called
  `SetPropertiesAsync`. It now calls `ClearPropertiesAsync` first, and a base-class test keeps it.
- **The component gallery's runtime check was broken.** It invoked the interop module's
  `componentStatus` as a global function through `IJSRuntime`; an ES module export is not global, so
  the check always failed with a message about the check rather than about the elements. It goes
  through `IPolarisInterop.GetComponentStatusAsync` now.
- **Four template defects, all invisible to the solution build** (see `AGENTS.md` item 20): content
  one level too deep, so every generated project nested under a stray folder; `.razor` conditionals
  written in the wrong syntax and left in the output; `NU5017` because a template has no assembly and
  a symbols package was still being packed; and implicit enclosing-namespace lookup that broke when
  the template renamed the root namespace (`AGENTS.md` item 21).
- **Two CLI defects found by compiling a generated page rather than asserting it**: an ambiguous
  `LoadAsync` overload pair made every generated page fail with `CS0121`, and the page relied on
  `_Imports.razor` for namespaces that a renamed project does not have.

## Known gaps

- **A real development store** — installation, the Admin handshake, and a genuine session token.
- **A real circuit loss** — the reconnection dialog is asserted present, not exercised.
- **`s-drop-zone`'s `files` property is not modelled.** It is a read-only array of browser `File`
  objects, which cannot cross the interop boundary, so the upload path belongs in JavaScript. The
  wrapper exposes the label, `accept`, `multiple` and the events, and can only reset the zone.
- **`PolarisTable` pagination events** (`nextpage`/`previouspage`) are wired but not exercised,
  because the products page does not paginate.
- **No integration test project.** `tests/Shopify.Blazor.IntegrationTests` was in the plan and was
  not created; `WebApplicationFactory`-based tests of `/api/products` would be the natural home.
- **`doctor` cannot see user secrets.** It reads `appsettings*.json` and the process environment
  only, deliberately: resolving them would mean shelling out for a secret. A setting that lives only
  in user secrets is reported as missing.
- **The CLI's `new` command is verified against a fake process runner**, not a real `dotnet new`
  invocation. The template it calls *is* verified end to end by `tools/verify-template.py`.
- **`Polaris.Blazor.Interop` was not split into its own project.** The interop module lives inside
  `Polaris.Blazor` as one JavaScript file plus a small service. Split it if the interop grows.
- **`Polaris.Blazor.Templates` and `Shopify.Blazor.Cli` are not published.** `dotnet pack` produces
  four packages; nothing is on nuget.org, so the template's `PackageReference` versions resolve only
  against a local feed today.
- **Node/Shopify CLI is unused.** No extensions, so no `package.json` and no bundler anywhere.

## Session notes

- The silent failures worth knowing about are documented in `AGENTS.md`, which now runs to 22 items.
  The newest four came from this session: array clearing, template defects, namespace renaming, and
  Blazor inserting a parent before its children.
- Four files are generated; `tools/check-drift.py` runs every generator and compares. Run it before
  committing, or CI fails.
- The verification scripts and generators are dependency-free on purpose: Python standard library for
  the tooling, Node 24's built-in `WebSocket` for the browser probe, no npm and no test framework.
