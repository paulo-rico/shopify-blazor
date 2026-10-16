# Changelog

Notable changes to the Shopify Blazor Toolkit, newest first.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project follows
[Semantic Versioning](https://semver.org/spec/v2.0.0.html), with the pre-1.0 caveat spelled out in
[docs/versioning.md](docs/versioning.md).

**Nothing has been published to NuGet yet and no version has been tagged**, so every entry below is
unreleased. The first planned release is `0.1.0-preview.1`.

## [Unreleased]

### Added

- **`Polaris.Blazor`** — C# wrappers for every documented Shopify Polaris web component: all 50
  top-level elements plus the 10 composed sub-elements they need, one class per element, in 62 classes
  including the 2 App Bridge components. Each wrapper renders the element Shopify defined, with typed
  parameters, named-slot support and forwarded events. Polaris booleans are rendered as presence
  attributes, `value`/`checked`/`selected` and array-valued properties are assigned as DOM properties
  after the element upgrades, and numbers are formatted with the invariant culture.
- **`Polaris.Blazor`** — a dependency-free JavaScript interop module that attaches DOM listeners only
  for the events a page subscribes to, waits for a custom element to be defined before assigning its
  properties, and detaches everything on disposal.
- **`Shopify.Blazor`** — embedded-app infrastructure: App Bridge 4 interop (session tokens, toasts,
  modals, navigation, resource picker, scopes, save bar), session-token validation, RFC 8693 token
  exchange with per-shop caching, an Admin API GraphQL client, the embedded-framing middleware, and
  startup configuration diagnostics.
- **`Shopify.Blazor.Templates`** — a `dotnet new shopify-blazor` template that generates a working
  embedded app: Blazor Web App with Interactive Server, Polaris components, App Bridge, a verified
  authentication path, one authenticated Admin API endpoint, and a README with the setup steps.
  Parameters: `--framework`, `--include-sample-pages`, `--include-admin-api-example`,
  `--toolkit-version`.
- **`Shopify.Blazor.Cli`** — the `shopify-blazor` global tool: `new`, `add page`, `add component`,
  `configure` and `doctor`. It is a convenience wrapper over `dotnet new` and the filesystem, never
  the only way to do something, and it refuses to overwrite a file without `--force`.
- **Reference application** — embedded shell, dashboard, products page that calls the Admin API
  through an authenticated endpoint, and a gallery of every wrapped component.
- **Tooling** — a component reference generator, a component inventory (JSON and Markdown) with a
  drift check, a wrapper generator with golden-file tests, a template verification script that packs
  and builds every parameter combination, and a headless-Chrome verification script.
- **Documentation** — getting started, the component reference, authentication, troubleshooting, a CLI
  reference, a versioning and compatibility policy, and ADR 0001 for the hosting model.

### Changed

- **`s-text` and `s-paragraph` no longer share one wrapper.** `PolarisText` had a `Paragraph` flag that
  switched its element, so a single class had to model two property sets and omitted the half that did
  not apply. The elements genuinely differ: `type` and `interestFor` exist only on `s-text`, and
  `lineClamp` only on `s-paragraph`. There is now a `PolarisParagraph` class for the block element and
  `PolarisText` renders inline text alone. **Breaking** for anyone using `PolarisText Paragraph="true"`
  (use `PolarisParagraph`) or `PolarisText LineClamp` (use `PolarisParagraph LineClamp`), and it is why
  `LineClamp` moved rather than being copied: Polaris documents inline text as never truncating, so the
  attribute was accepted by the DOM and silently ignored.
- **`PolarisText` and `PolarisParagraph` model `dir` and `accessibilityVisibility`.** Both properties
  are documented on both elements and neither was exposed, so an RTL run or a screen-reader-only
  paragraph could not be expressed. A paragraph with `AccessibilityVisibility="Exclusive"` is the
  documented way to give a table's sort order to assistive technology.
- **Development happens on a web server.** A tunnel is no longer part of the workflow: point Shopify
  at a server that can terminate HTTPS, publish with `bin/shopify-publish` (which replaces
  `bin/shopify-dev`) and copy the output there. The documentation, the generated project's README and
  the `shopify-blazor doctor`/`configure` guidance all describe that path, and the tunnel-specific
  request header the sample used to send is gone from its JavaScript.

### Removed

- **`ShopifyEmbeddedFramingOptions.AllowDevelopmentTunnels`.** It existed to add wildcard tunnel hosts
  to the `frame-ancestors` policy, which the web-server workflow does not need. The policy is now the
  Admin origins plus whatever you name with `AllowFrameAncestor(origin)` — a development origin is an
  ordinary HTTPS origin. Dropping it removes two wildcard entries from every app's framing policy,
  which is a tightening rather than a loss.
- **`bin/shopify-dev`**, replaced by `bin/shopify-publish`. The old script started the app locally and
  raised a public tunnel to it, which is no longer how the app is developed or deployed.
- The troubleshooting entry about a tunnel provider's browser-warning page, which cannot occur when
  the URL being loaded is the app's own server.

### Fixed

- **`dotnet pack Shopify.Blazor.slnx` exited 1.** `Directory.Build.props` set `PackageReadmeFile`
  whenever a project had a `README.md` of its own, but nothing put that file in the package, so the CLI
  project failed with `NU5039` ("The readme file 'README.md' does not exist in the package"). Because
  `dotnet pack` carries on through the other projects and only fails at the end, the run wrote the
  libraries and looked successful while returning non-zero — and any script that packs the solution
  stopped before doing what came next. Naming the readme and packing it are now declared together in
  one place, and the CLI package ships its README.
- **A stale `s-choice-list` selection could never be cleared.** The component base class documented
  `ClearedProperties` and gated its property write on it, but never called
  `IPolarisInterop.ClearPropertiesAsync`. Nothing used the collection until `PolarisChoiceList`
  arrived, where a leftover `values` array keeps options selected that the server has deselected, with
  nothing wrong in the markup.
- **Session tokens were verified against an endpoint that can never validate them.** They are HS256
  signed with the app's API secret; the shop's `/.well-known/jwks.json` belongs to the Storefront and
  Customer Account APIs, so RS256 verification could never succeed on any store.
- **`Shopify.Blazor` shipped without `app-bridge.js`.** The project used `Microsoft.NET.Sdk` instead
  of `Microsoft.NET.Sdk.Razor`, so the file was present in `wwwroot`, absent from the package, and
  every App Bridge call failed at runtime.
- **The app could not be embedded in the Admin at all.** Blazor's required `UseAntiforgery()`
  middleware sets `X-Frame-Options` and `frame-ancestors 'self'`, which refuse the Admin's iframe,
  while the app looks perfect in a normal browser tab.
- **A missing access scope was reported as a gateway fault.** Shopify answers an unauthorised field
  with HTTP 200 and an `errors` array, so the failure now surfaces as a permission problem that a
  retry cannot fix, and the granted scopes are visible in diagnostics.
- **`expires_in` never bound**, because a source-generated JSON context had no naming policy, so every
  access token looked non-expiring.
- **A revoked access token could be reused indefinitely**, because a 401 cleared only one of the two
  caches holding it.
- **"No token arrived" and "the token was refused" were indistinguishable**, so the first question
  asked about a 401 had no answer.

### Security

- Session tokens are validated server-side against the app's API secret with the algorithm pinned;
  no query string, form field or cookie is ever treated as identity.
- The `dest` claim is treated as attacker-supplied and validated before it is interpolated into the
  token-exchange URL, which carries the API secret.
- The API secret and every Admin API access token stay server-side; only the public API key is
  rendered into the page.
- Missing scopes are surfaced explicitly (`ShopifyAdminApiException.IsAccessDenied`) rather than being
  retried as if they were transient.
- The app can be framed by the Admin origins in the configured policy and by nothing else; cookie,
  origin and frame protections are tightened rather than weakened to make embedding work.
- Development-only diagnostics are mapped in Development only, and never in a deployed app.
