# Changelog

Notable changes to the Shopify Blazor Toolkit, newest first.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project follows
[Semantic Versioning](https://semver.org/spec/v2.0.0.html), with the pre-1.0 caveat spelled out in
[docs/versioning.md](docs/versioning.md).

**Nothing has been published to NuGet yet and no version has been tagged**, so every entry below is
unreleased. The first planned release is `0.1.0-preview.1`.

## [Unreleased]

### Added

- **`PolarisDataGrid<TItem>`, `PolarisGridColumn<TItem>`, `PolarisDataFilter`, `PolarisDataPager` and
  `PolarisRangeSlider`** — sorting, filtering, paging, selection and a dual-handle range control for
  local (in-memory) data, composed from Polaris elements because `s-table` has none of it: the runtime
  defines no sortable header, no filter, no pagination and no slider. A column heading is an `s-button`,
  a selected row is an `s-checkbox` in an `s-table-cell`, the filter panel is an `s-popover` opened
  declaratively with `commandFor`, and the slider is two native `<input type="range">` elements over
  one track. The four components share one `PolarisGridState<TItem>` that the page creates and owns;
  each mutation recomputes once and notifies once, so a filter change reaches the grid, the chips and
  the pager in the same render.
- **`Polaris.Blazor.DataGrid`** — the query layer, with no renderer in it: filter and sort descriptors,
  a column definition whose value accessor is a delegate compiled once from its `Expression` (never
  reflection per cell), a query engine whose order is the whole contract (filter → count → stable sort
  → page), and the shared state. Filters AND across columns and OR inside a multi-select; text compares
  case-insensitively, numbers numerically and dates chronologically; empty values sort last ascending
  and first descending; a filter whose value cannot be read as the column's type is dropped rather
  than applied, so it cannot look like an empty dataset.
- **`PolarisRangeSlider`** — a reusable range control with no knowledge of grids, emitting a
  `PolarisNumberRange`. Handles cannot cross, a collapsed interval raises the handle that can still
  move (otherwise the control can be left with no way to widen it), values are formatted with the
  invariant culture, and the interval is written inline as two custom properties so a missing
  stylesheet costs appearance rather than behaviour.
- **`polaris-range-slider.css`** — the slider's track and thumbs, the library's second stylesheet and
  the first that draws a control rather than a transition. Loaded by the interop module like the
  accordion's, so a consuming app wires nothing up.
- **`PolarisAccordion` and `PolarisAccordionItem`** — the first components that are composed rather
  than wrappers, because Polaris 1 has no accordion (measured against the runtime, not assumed). A
  section is an `s-button` header, an `s-box` for the content and an `s-icon` chevron; opening one
  section collapses the open one in the same render, so both transitions run together. The panel is
  clipped by `grid-template-rows: 0fr` → `1fr` — no measurement, no JavaScript — and a closed panel is
  `inert`, so clipped content leaves the tab order and the accessibility tree. The motion itself is
  the library's only stylesheet, loaded by the interop module, because `prefers-reduced-motion`
  cannot be expressed inline; without it the panels still open and close, they simply stop animating.
- **`PolarisButton.AccessibilityExpanded`** — announces the expanded state of what a button controls
  as `aria-expanded` on the `<button>` inside Polaris's shadow root. Polaris's
  `accessibilityExpanded` property is undocumented and two things about it were only discoverable in
  a browser: it takes the string `"expanded"`, not a boolean, and it does not schedule a render, so
  the interop module asks the element to render after writing it.
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
  through an authenticated endpoint, and a gallery of every wrapped component. The dashboard opens with
  the data grid running on live data: a `ProductCatalogue` service answers asynchronously with 150
  products (eight types, five vendors, three statuses, generated deterministically), so the grid's own
  loading state, a reload that keeps the page and the filters, a 150-row pager, filter chips and a
  range filter on price and inventory are all visible rather than described.
- **Tooling** — a component reference generator, a component inventory (JSON and Markdown) with a
  drift check, a wrapper generator with golden-file tests, a template verification script that packs
  and builds every parameter combination, and a headless-Chrome verification script.
- **Documentation** — getting started, the component reference, authentication, troubleshooting, a CLI
  reference, a versioning and compatibility policy, and ADR 0001 for the hosting model.

### Changed

- **`PolarisRangeSlider` shows its two typed values to two decimal places** — the Min and Max fields,
  via a new `InputFormat` parameter (`"F2"` by default; an empty string shows the raw value). A range
  filter's domain is the cheapest and dearest row in the data rather than a number anybody typed, so a
  lower bound of `4.1666667m` was being shown verbatim and read as a bug rather than as a limit. It is
  separate from `Format` on purpose: that one is a *display* format and may be `C2`, and a currency
  symbol or group separator is not a number — `s-number-field` parses the value it is given. Only the
  display is rounded; the reported interval keeps its full precision unless the merchant retypes it, so
  a filter never moves a bound nobody touched. The rounding is stated rather than left to the format
  string, because `decimal.ToString` rounds a halfway value to even and a control that shows money
  rounds halves away from zero: a `4.165` bound reads `4.17`, not `4.16`.
- **The default Polaris channel is now Polaris 2 (`polaris-2.js`), up from Polaris 1 (`polaris-1.js`).**
  A 2.0 bundle carries both the current admin theme and Admin Next and follows whichever theme the
  merchant's admin is using, so one URL serves both looks with no flag and no per-theme path. Nothing
  else about the toolkit changes: measured by diffing the shipped bundles, `polaris-1.js` (1.1.0) and
  `polaris-2.js` (2.0.0) define the **same 74 `s-*` element names**, so no wrapper is orphaned and
  `minPolarisVersion` stays at each element's true floor of 1.1 rather than being bumped to 2.0.
  Consumers who load Polaris through `<ShopifyAppBridgeHead />` get this by updating; anyone who set
  `AppBridgeOptions.PolarisScriptUrl` explicitly keeps their own URL. Note that `polaris-2.0-rc.js` is
  **not** the channel — it is the superseded release candidate (2.0.0-rc.3), which receives no fixes.
  The element *names* are provably unchanged, and the behaviour was re-verified rather than assumed:
  `tools/verify-in-browser.mjs --assert` passes against both the component gallery (60 elements) and
  the composed data grid on `polaris-2.js`, with 0 undefined and 0 unupgraded elements and all 39
  assertions green on each page.
- **`check-admin-install.mjs` recognises any Polaris channel.** It looked for the literal string
  `polaris-1`, so an app correctly configured for Polaris 2 reported its design system as MISSING.

- **The accordion rounds its own corners and the sections round none.** One clipped, rounded `s-box`
  wraps the whole list, so it reads as a single card; a radius on each header made it a stack of
  separate cards. The first section's divider is dropped by the stylesheet, because above it sits the
  container's border.
- **The accordion's click no longer depends on an interop listener.** The header is handled by
  Blazor's own `@onclick` on a wrapper element, and the Polaris surface is visual only. The listener
  it replaces was attached to the element and re-attached after every render — and every toggle
  re-renders the page that binds `OpenItemChanged` — so whether it was in place when a click arrived
  depended on render history. That is the shape of an intermittent fault with no pattern, which is
  how it was reported. A click is a composed event, so one anywhere in the surface reaches the single
  delegated handler. **Fixed** with it: `element.click()` in the browser check was exercising
  Polaris's untrusted-event path rather than a merchant's real click; the check now dispatches a real
  mouse event on the shadow button.
- **The accordion's header is a `s-clickable` surface holding an `s-section`, not a button.**
  A heading with a line of supporting text under it does not fit in a button's single truncating line,
  so the header became a surface: `s-section` carries the heading and a new `HeaderDetails` string as
  its `subheading`, and the chevron sits at the right-hand end of a row. Three things had to move with
  it, each because Polaris forced it: the border and background are on the surface, because
  `s-section` takes no styling properties and silently drops them; the accessible name is stated
  explicitly, because a heading given as an attribute lives in Polaris's shadow root and leaves the
  surface with no text; and `aria-expanded` is written onto the shadow button by
  `IPolarisInterop.SetAriaExpandedAsync`, because `s-clickable` has no property for the state. The
  `HeaderContent` fragment is gone with the button — a surface cannot lay out an arbitrary fragment
  inside a shadow-rendered button, and a parameter that no longer renders is worse than no parameter.
- **`PolarisClickable` can hold content.** It derived from the element base class and so had no
  `ChildContent`: content written inside one had nowhere to go, and the sample gallery's own clickable
  surface was empty. It is a content component now.
- **`PolarisClickable.AccessibilityExpanded`** — announces the expanded state of what the surface
  controls, through the interop layer as described above.
- **The interop module asks Polaris to render after writing a property it does not react to.** Most
  Polaris properties are accessors that schedule a render; `accessibilityExpanded` is read by the
  render function and nothing more, so writing it changed nothing on screen — the state was announced
  wrongly, silently and only to assistive technology. `setProperties` now calls the element's own
  `queueRender()` after writing a property from that (currently one-entry) set, defensively: if the
  name ever changes, the value is merely applied later rather than wrongly.
- **The interop module also loads this package's stylesheets**, so a consuming app has nothing to wire
  up. That is `polaris-accordion.css`, which carries the accordion's transitions and its
  `prefers-reduced-motion` override, and `polaris-range-slider.css`, which draws the slider's track and
  thumbs.
- **`PolarisGridState.SetLoading` and `SetError`** replace the settable `IsLoading` and `ErrorMessage`
  properties. Those two were the only parts of the state that could change without notifying a
  component, so a page that set them left the grid showing the previous state — a spinner that never
  appeared, or a failed load that looked like an empty catalogue. Using the components against a real,
  asynchronously loaded dataset is what surfaced it.
- **The filter panel scrolls, because a Polaris popover clips rather than scrolls.** Measured: with six
  filters the panel caps its height and paints the controls below the cap outside its own box, where
  nothing can reach them — `elementsFromPoint` there returns an empty list. A viewport-relative
  `MaxBlockSize` is not a fix, because the content has to be shorter than Polaris's cap; the panel now
  keeps its own scroll box inside the popover's frame.

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

- **`PolarisRangeSlider` reported every pixel of a drag, so a range filter re-queried the data while the
  mouse was still down.** Measured in a browser: `input` fires continuously through a drag and `change`
  fires once, on release — so `input` now only previews (this control moves its own interval and
  re-renders) and `change` is where `ValueChanged` is raised. A keyboard user is unaffected: one key
  press fires both events at the same instant, and a key press is a finished change rather than a drag.
  The browser check holds both halves — the fill follows the thumb while the button is down, and the
  result text has not moved until it comes up.
- **`PolarisRangeSlider`'s handles were pulled back by a stale value from the caller.** The component
  adopted any `Value` that differed from the last one it had reported, which a lazily-applying parent
  defeats: with the filter's 300ms debounce the parent still holds the previous interval while the new
  one is in flight, so that echo was read as an instruction. Every second key press undid the one
  before it (measured: 45.95 -> 66.95 -> 66.95 -> 87.95) and a drag had to be released twice. A caller
  value is now adopted when it *changes*; the same parameter arriving again is not a new instruction.
- **`PolarisRangeSlider.Label` was never displayed.** It was written as the group's `aria-label` and
  nowhere else, so in a filter panel the range control was the only control with no caption on screen —
  which is how it was reported as "the title doesn't seem to be displaying". The title is now rendered
  above the track (hidden from assistive technology, because the group already carries the same text as
  its name).
- **The slider's two numeric fields sat one above the other at full width instead of side by side.**
  Measured: an `s-stack direction="inline"` around them does not lay them out in a row, because a
  Polaris host computes to `display: contents` and its contents are not the flex items the stack
  positions. They are now a grid of two equal columns, stated inline on a real element so no framework
  internals can flatten it away.
- **The filter panel's controls sat flush against the popover's edge.** Measured in a browser: an
  `s-popover` paints no padding of its own — the `div.popover` inside its shadow root computes to `0px`
  on every side and shares its rect with the slotted panel — so a text field's own border touched the
  popover's border and the panel read as a container filled wrongly. The panel now states Polaris's
  `space-400` inset (`var(--p-space-400, 16px)`, with the fallback in force because the runtime defines
  that token inside its shadow roots rather than on `:root`), inside the box so the height cap still
  caps. Held by a component test on the markup and by a browser assertion on the painted inset, because
  a layout fault renders perfectly.
- **A range slider that rendered before its data did kept a placeholder domain for ever.** The filter
  builds its sliders from the dataset's numeric bounds, and it renders while the grid is still loading,
  so on the first pass those bounds are a stand-in `0..1`. The slider clamped its interval into that
  domain and never widened it again: both handles sat at the first value, the highlighted interval was
  4.167% of the track, and a range filter that could neither be dragged nor exclude anything looked
  exactly like a stylesheet fault. It now follows its domain — `Value` being `null` means "the whole
  range", so a domain that grows re-opens the slider. Found by pointing the dashboard at a real,
  asynchronously loaded catalogue; held by a test.
- **The generated component reference silently dropped any parameter whose attribute list was not
  exactly `[Parameter]`.** `[Parameter, EditorRequired]` and a separate `[EditorRequired]` line between
  the doc comment and the property both failed the pattern, so a component could document a parameter
  in its source and be missing it from `docs/components.md` — the file whose whole purpose is that a
  parameter in code and a parameter in the docs cannot disagree. Both generators now match the
  attribute list loosely and accept further attribute lines.
- **`shopify-blazor doctor` reported "no `@rendermode` directive was found" for every project with
  Windows line endings.** The scanner anchored the directive with `[ \t]*$`, and a CRLF line ends with
  `\r` before the `\n` that multiline `$` anchors to, so the match failed — a false FAIL, exit code 1,
  on a project that was correct. This is the check the CLI's documentation calls its most valuable
  one, and a wrong verdict from it trains the reader to ignore it. The regex now allows the `\r`, and
  a test holds a CRLF file to the same expectation as an LF one.
- **One drift test failed on a Windows checkout.** It compared the tool's output against
  forward-slash paths, which Windows prints with backslashes; the separator is now normalised first.
  It said nothing about whether the files were in sync.
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
