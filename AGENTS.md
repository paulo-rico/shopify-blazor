# Working notes for the Shopify Blazor Toolkit

Load this before touching the repository. Everything here was learned by breaking it, and none of
these failures raise an error when violated.

**Looking for an API?** Do not read the source. Every file under `src/` has a sibling
`<name>.md` — `PolarisButton.cs.md` beside `PolarisButton.cs` — with its parameters, events, method
signatures and the traps. Start at [`docs/source-summaries.md`](docs/source-summaries.md), which
indexes all of them by directory. See [Source summaries](#source-summaries--read-these-instead-of-the-source).

## Environment on this machine

The DSH file sandbox allows writes **only** under the session workspace. The .NET CLI wants
`~/.dotnet` and `~/.nuget` there, which fails with `Read-only file system`, so every command needs
the CLI home redirected into the workspace:

```sh
cd /home/paul/aiprojects/shopify-blazor
export DOTNET_CLI_HOME=$PWD/.dotnet
export NUGET_PACKAGES=$PWD/.nuget/packages
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
dotnet test
```

Both directories are git-ignored. Without them `dotnet` fails before doing anything.

`.NET 10.0.112` is the only SDK. `global.json` pins `10.0.100` with `rollForward: latestFeature`.

## Commands

```sh
dotnet test                                    # every suite: components, toolkit, CLI
dotnet build                                   # whole solution
dotnet run --project samples/Shopify.Blazor.SampleApp   # http://localhost:3458
bin/shopify-publish                            # publish the folder to upload to the web server
```

Generated files and the tools that own them. Each generator writes a committed file, so each can
drift; `check-drift.py` runs them and compares, without touching the working tree:

```sh
python3 tools/generate-component-reference.py       # docs/components.md
python3 tools/generate-component-inventory.py       # docs/component-inventory.{json,md} + the CLI catalogue
python3 tools/generate-component-inventory.py --check   # same, but exits 1 on drift and writes nothing
python3 tools/generate-summary-index.py             # docs/source-summaries.md, the map of the summaries
python3 tools/check-summaries.py                    # every src file has a summary that matches its source
python3 tools/check-drift.py                        # every generated file, plus the template's pinned version
python3 tools/generate-component.py                 # emit a wrapper from the spec (golden-tested)
python3 tools/tests/run.py                          # the generator tests (stdlib unittest only)
```

Verification that needs a browser — no npm, no test framework, Node 24 only. `--assert` turns the
report into a test: it exits non-zero unless every element the gallery claims is defined, upgraded
and present, the form values reach the request, a click reaches C#, and the static assets are served.

```sh
dotnet run --project samples/Shopify.Blazor.SampleApp &
node tools/verify-in-browser.mjs http://127.0.0.1:3459/components --assert
```

The template is not built by the solution, so it has its own end-to-end check — pack, install,
generate and build every parameter combination:

```sh
python3 tools/verify-template.py
```

## Source summaries — read these instead of the source

Every source file under `src/` has a sibling `<name>.md`: `PolarisButton.cs.md` beside
`PolarisButton.cs`, `polaris-interop.js.md` beside `polaris-interop.js`. Each holds that file's public
surface — every parameter with its type and default, every event, every method signature — plus what
it actually does and the traps it documents. **Read the summary, not the source.** Open the source
only when you are changing it.

- [`docs/source-summaries.md`](docs/source-summaries.md) is the map: every source grouped by
  directory with a one-line purpose and a link. Start there when you know what you want to do but not
  which file does it.
- The first two lines of each summary are `<!-- summary-of: File.cs -->` and
  `<!-- source-sha256: … -->`, the hash of the source it was written against.
- `python3 tools/check-summaries.py` lists summaries that are missing, unmarked or older than their
  source. `check-drift.py` runs it as one of its checks, so CI fails on a stale one.
- On Windows, Visual Studio's built-in `addedExtension` nesting rule puts `PolarisButton.cs.md` under
  `PolarisButton.cs` — turn on **File Nesting** in Solution Explorer (the toolbar button, preset
  *Default*) if the summary files appear flat.
- **A summary that lands in `wwwroot` becomes a static web asset**, and one in `templates/**` becomes
  template content. Both are removed from the item set on purpose: `<Content Remove="wwwroot/**/*.md" />`
  in the two libraries, and the four `templates/**/*.{cs,razor,js,css}.md` globs in
  `Shopify.Blazor.Templates.csproj`. Without them the packages publish
  `/_content/Polaris.Blazor/polaris-grid.css.md` and generate `Dashboard.razor.md` into every new
  project. Check after packing: the only `.md` in the three packages is the templates' own README.

Keeping a summary current is part of changing the file, not an optional extra:

1. Change the source.
2. Update the summary's prose — parameters, defaults, events, gotchas — not just the hash. A summary
   that still describes the old behaviour is worse than none, because it is believed.
3. `python3 tools/check-summaries.py --sha <source>` prints the new marker line; paste it in.
4. `python3 tools/generate-summary-index.py` refreshes `docs/source-summaries.md`.
5. A new source file needs a new summary in the same change; the checker lists it as missing until it
   has one, and `check-drift.py` fails.

The shape, which `PolarisButton.cs.md` and `PolarisElementComponentBase.cs.md` demonstrate:

```markdown
<!-- summary-of: PolarisBadge.cs -->
<!-- source-sha256: 0123456789ab -->

# PolarisBadge.cs

One paragraph: what it is, in sentences that stand alone. Its first sentence becomes the index entry.

`Polaris.Blazor.Components` · renders `<s-badge>` · base `PolarisContentComponentBase`

## Parameters        <!-- Parameter | Type | Default | What it does -->
## Events            <!-- what raises them, what the args carry -->
## Members           <!-- exact signature + behaviour, for non-component classes -->
## Behaviour         <!-- only what the member list cannot convey -->
## Gotchas           <!-- silent failures, ordering rules, quirks -->
## See also          <!-- relative links to sibling summaries -->
```

A summary is a page, not a stub: every public parameter and member, and the behaviour that would
surprise someone. Never paste code, and never invent a member — everything comes from the file.

## The constraints that fail silently

Read these before changing anything in `src/`.

1. **Wrappers must be plain C# classes, never `.razor` files.** The Razor compiler injects its own
   `BuildRenderTree` into every `.razor` component, silently replacing the base-class implementation
   and rendering **nothing**. The build succeeds and the page is empty.
2. **A Blazor Web App is static unless `@rendermode InteractiveServer` is applied.** The markup
   renders perfectly and no handler is ever attached, so every control is inert.
3. **A library shipping a JavaScript file needs `Microsoft.NET.Sdk.Razor`.** With
   `Microsoft.NET.Sdk` the build succeeds, the file is in `wwwroot`, and the browser gets a 404 for
   `/_content/…`. Symptom: buttons render perfectly and do nothing. The failure is logged as
   `Polaris interop module … could not be loaded` — look for it. **Both** `Polaris.Blazor` and
   `Shopify.Blazor` need this, and the trap repeats at pack time — a package can build fine and
   still ship without the file. Verify after packing:

   ```sh
   dotnet pack Shopify.Blazor.slnx -c Release -o artifacts
   python3 - <<'EOF'
   import glob, zipfile
   for package in sorted(glob.glob("artifacts/*.nupkg")):
       js = [n for n in zipfile.ZipFile(package).namelist() if n.endswith(".js")]
       print(package, "->", js or "NO JAVASCRIPT SHIPPED")
   EOF
   ```
4. **Polaris booleans are presence attributes.** `disabled="false"` is still `true` to a custom
   element. `true` must render bare; `false` must remove the attribute.
5. **`value`, `checked`, `selected` and `indeterminate` are not reflected.** They must be set as DOM
   properties after the element upgrades. `value` is also rendered as `defaultValue` for first paint.
6. **Polaris validates slot children.** A `primary-action` slot rejects a `div` that wraps a button,
   so slots cannot be `RenderFragment` parameters — a fragment cannot carry an attribute. Use the
   `Slot` parameter on the child component.
7. **`s-text-area` takes its value from the property, never its child text.** Copying the native
   `<textarea>` habit submits an empty string with no warning.
8. **Numbers must be formatted with `CultureInfo.InvariantCulture`,** or a comma decimal separator
   emits `value="1,5"`.
9. **A source-generated `JsonSerializerContext` ignores naming policy unless it is declared.** Without
   `[JsonSourceGenerationOptions(PropertyNamingPolicy = …)]`, Shopify's `expires_in` never binds and
   every token looks non-expiring.
10. **Session tokens are HS256, signed with the app's API secret — there is no key set.** Shopify's
    own libraries do `JWT.decode(token, api_secret_key, true, algorithm: "HS256")`. Do **not**
    verify them against `https://{shop}/.well-known/jwks.json`: that endpoint belongs to the
    Storefront and Customer Account APIs, and it sits behind the storefront, which every dev store
    password-protects — so RS256 verification could never work, on any store. An earlier version of
    this toolkit made exactly that mistake. The accepted algorithm is pinned, and HMAC-SHA256 needs
    a secret of at least 16 bytes, so a truncated secret fails every token.
11. **The `dest` claim is a URL** (`https://shop.myshopify.com`), not a bare host, and it is
    attacker-supplied. It is validated before being interpolated into the token-exchange URL.
12. **Two caches hold the access token.** A 401 must clear both `IShopifyAccessTokenStore` **and**
    `IShopifyTokenExchanger` (`Invalidate`), or a revoked token is reused for ever.
13. **The anti-forgery middleware makes the app unframeable, and this is invisible.** Blazor's
    rendering pipeline requires `app.UseAntiforgery()`, and that middleware sets
    `X-Frame-Options: SAMEORIGIN` and `Content-Security-Policy: frame-ancestors 'self'` on every
    response that asks for a token. Both refuse the Admin's iframe, so the app can never be embedded
    while looking perfect in a normal browser tab. `app.UseShopifyEmbeddedFraming()` must be
    registered **first** — its `OnStarting` callback has to run after antiforgery's, and
    `OnStarting` callbacks run last-registered-first. Verify with a real browser:
    `Framing '…' violates … "frame-ancestors 'self'"` in the console is this bug.
14. **Development happens on a web server, not through a tunnel.** A tunnel-based setup was tried here
    and abandoned: free tunnel services inject their own browser-warning page into navigations, and the
    arrangement never held up. The app now runs on an ordinary HTTPS server and changes are published
    and copied there — `bin/shopify-publish` builds the folder to upload. Do not reintroduce a tunnel,
    and in particular do not read a browser-warning page as an app bug: if the served HTML is not the
    app's, the URL is not the app's.
15. **An environment variable silently overrides user secrets.** If `SHOPIFY_API_KEY` is set in the
    shell, it wins over `Shopify:ApiKey` in user secrets, and a key belonging to a *different*
    Shopify app makes every session token fail as "another app". Startup logs which layer won. The
    app also runs in **Production** with no `ASPNETCORE_ENVIRONMENT` set, which disables the
    diagnostics endpoints and the rejection hints — set `ASPNETCORE_ENVIRONMENT=Development` locally,
    or on the server if you want them there.
16. **A retired Admin API version does not error.** Shopify falls forward to an older stable release
    and answers normally; only the `X-Shopify-API-Version` header reveals it. Check the version table
    at <https://shopify.dev/docs/api/usage/versioning> before changing `AdminApiVersion`.

17. **A missing scope arrives as HTTP 200 with an `errors` array, not a 403.** Shopify answers an
    unauthorised field with `200 OK` and `extensions.code = "ACCESS_DENIED"`, so nothing about the
    status code says "permission". It looks exactly like a gateway fault, and it is the reason a
    page can report "Shopify request failed" while the server log shows the Admin API returning
    200. Retrying cannot fix it — re-exchanging the session token yields the same scopes — so it is
    reported separately (`ShopifyAdminApiException.IsAccessDenied`), and the granted scopes are
    visible in `/api/diagnostics/session-token` as `grantedScopes`. `read_products` is required by
    the sample app's Products page; a session token that grants less fails there and nowhere else,
    because that route is the only one that calls Shopify.
18. **`pgrep -f <pattern>` matches its own caller.** `pgrep -f` searches full command lines, so a
    pattern also matches any shell whose command line contains that text — a wrapper script, an
    editor task, or the very script doing the killing. A dev script here SIGTERMed itself this way,
    leaving the app running and the port looking permanently busy. Use a character class
    (`Shopify[.]Blazor`) so the pattern matches the target text without containing it. A character
    class is not enough on its own: `pkill -f "Shopify[.]Blazor"` still matched this session's own
    `dotnet run --project samples/Shopify.Blazor.SampleApp` command line and killed it before it
    started. Kill a dev server by port (`fuser -k -n tcp 3459`) or by a recorded pid, not by name.

19. **`clearProperties` must never set an array-valued property to `undefined`.** The interop layer
    resets a property by assigning it, and for `s-choice-list.values` that is an uncaught
    `TypeError: Cannot read properties of undefined (reading 'join')` thrown inside Polaris — six
    times per page render, while the markup looks perfect. An array property is cleared to `[]`
    instead. `PolarisChoiceList` sidesteps it entirely by always writing an array, even an empty one;
    the JS fix is what protects the next component that declares `ClearedProperties`. Only the
    browser verification can see this class of failure: bUnit asserts the interop *call*, not what
    Shopify does with it.

20. **A project template is not compiled by the solution build.** Nothing in `dotnet build` reads
    `templates/**`, so a template can be broken in four ways that all ship silently:
    - its content sits one level too deep and every generated project nests under a stray folder
      (the directory holding `.template.config` **is** the template root);
    - a conditional directive is written in the wrong syntax — a `.razor` file needs
      `@*#if (symbol)` … `##endif*@`, and a bare `#if` is simply left in the output, where Razor then
      fails to compile it;
    - a file that a parameter excludes is still `@using`-ed or linked from a page that remains;
    - `dotnet pack` fails with **NU5017** ("no dependencies nor content") even though the package was
      written correctly, because a template has no assembly — set `<IncludeSymbols>false</IncludeSymbols>`.
    `tools/verify-template.py` packs, installs, generates and builds every parameter combination,
    which is the only thing that catches any of this.

21. **Renaming a root namespace breaks implicit enclosing-namespace lookup.** The sample app's
    `ProductsService` is in `Shopify.Blazor.SampleApp.Services`, so it found `IShopifyAdminApi` by
    walking up to the enclosing `Shopify.Blazor` without a `using`. The template renames that root
    namespace, the walk-up stops working, and the generated project fails to compile with "the type
    or namespace name 'IShopifyAdminApi' could not be found" while this repository builds perfectly.
    Consume the toolkit's namespaces with explicit `using` directives in anything a template copies.

22. **Blazor inserts a parent element before its children, so Polaris sees an incomplete subtree.**
    In plain HTML the parser upgrades an element once its whole subtree is present; under Interactive
    Server the renderer patches the DOM node by node, so `s-table` arrives before its
    `s-table-header-row` and `s-choice-list` before its `s-choice` children. Polaris logs a warning
    about the missing child (`Table component is missing TableHeaderRow`, `ChoiceList should have at
    least one choice`) on every interactive render. The warnings are noise, but the habit they
    demand is real: never rely on a Polaris component reading its children at upgrade time, and read
    them from C# (which knows the tree) rather than from the DOM.

23. **An App Bridge web component is not defined outside the Shopify Admin.** `s-app-nav` and
    `s-app-window` come from `app-bridge.js`, which defines them only once it has connected to the
    admin host — so on a standalone page they are not custom elements at all. This was **measured, not
    assumed**: adding them to the sample gallery's probe list made the browser check report
    `not defined: s-app-nav, s-app-window` while every other assertion passed. They are therefore
    `unit-tested` rather than `browser-verified`, `tools/polaris-elements.json` marks them
    `"browserProbe": false`, and a test holds the exemption set against the gallery's shortfall *and*
    requires each exemption to state its reason in the spec notes. Do not "fix" this by adding them back
    to `ProbedElements`: that list's claim is that Polaris defines the element, and for these two it
    does not.

24. **The resource picker is `IAppBridgeInterop.PickProductsAsync`, and its three traps are the reason it
    is shaped the way it is.** It was added for the products-selection and quote-piece screens, and the
    traps are worth keeping in mind before changing it:
    - **A cancelled picker resolves `undefined`, not an empty array.** So the method returns `null` for
      "cancelled or unavailable" and an empty list for "chose none and meant it" — and those must stay
      distinguishable, because a screen that treats them alike clears a selection the merchant never
      touched. The JS side tests `Array.isArray`, which is the only honest check.
    - **`window.shopify` is undefined outside the Admin**, so the JS returns `null` rather than throwing
      and the screen disables the button with a reason. Verified in a browser: the picker cannot be
      exercised outside the Admin by definition, so no browser assertion covers it.
    - **The action is `select`, not App Bridge's default `add`**, so the merchant cannot create products
      from inside the picker. It is for choosing what the shop costs, not for editing the catalogue.
    `variant.price` arrives as a **string** (that is how Shopify states money), so the DTO carries it as one
    and the caller parses it — binding it to a decimal would fail at runtime rather than at compile time.

25. **Polaris has no accordion, and the two ways to announce an expanded state both need care.**
    `PolarisAccordion`/`PolarisAccordionItem` are the first components here that are *composed*
    rather than wrappers, because there is no element to wrap: the shipped runtime defines 74 tag
    names and none is an accordion, collapsible or details element (checked in `polaris-1.js` and
    re-checked in `polaris-2.js`, and the App Home docs 404 for `details`). Wiring the disclosure
    semantics produced four measured facts, and all four are silent failures:
    - **`s-button` has `accessibilityExpanded`, and it takes `"expanded"` / `"collapsed"` (strings),
      not a boolean.** Polaris derives `aria-expanded` from `"expanded" === value`, so `true` renders
      `aria-expanded="false"` — the *opposite* of the intent, with nothing to see. That is why
      `PolarisButton.AccessibilityExpanded` is a `PolarisAccessibilityExpanded` enum, not a bool.
    - **It is not a reactive property.** The render function reads it and nothing schedules a render,
      so writing it alone changes nothing on screen. `polaris-interop.js` therefore calls the
      element's own `queueRender()` after writing a name from `RENDER_ONLY_PROPERTIES`, defensively:
      if Polaris renames it, the value is applied late rather than wrongly.
    - **`s-clickable` has no equivalent property at all.** It is a real `<button>` inside a shadow
      root with only its accessibility label and command props, so the accordion header's state is
      written onto that button by `IPolarisInterop.SetAriaExpandedAsync`. The attribute is re-applied
      on every render, because Polaris re-creates the button when it renders.
    - **`s-section` takes no styling properties.** Its documented set is `heading`, `subheading`,
      `padding` and `accessibilityLabel`, and it is *not* one of the elements that inherit the box
      styling set (`s-clickable`, `s-grid`, `s-grid-item`, `s-stack` are). `border="base"` and
      `background="subdued"` on a section are dropped without a word — the accordion puts them on the
      `s-clickable` that wraps it instead.
    - **`s-clickable` and `s-box` compute to `display: contents`.** Their host has **no box at all**:
      the border, the radius and the padding are painted by an element inside the shadow root, and the
      host's own `getBoundingClientRect()` is `0×0` at the page origin. Two things follow, and both
      cost an afternoon if forgotten. A **click probe** that measures the host's centre clicks the
      top-left corner of the page and reports "the click does nothing" — measure the shadow `<button>`
      instead. And a **layout assertion** written against the host — a radius, a height, an overflow —
      reads the wrong number: ask the element that paints, `shadowRoot.querySelector(".box")` for a
      box and `shadowRoot.querySelector("button")` for a clickable.
    The click handler is Blazor's own `@onclick` on a wrapper `<div>`, **not** an interop listener on
    the Polaris element. That is deliberate: the base class re-attaches listeners after every render,
    and every toggle re-renders the page that binds `OpenItemChanged`, so a listener on the element was
    the one thing whose presence depended on render history — "the click is hit and miss" with no
    pattern was reported against exactly that. A click is a composed event, so one anywhere in the
    surface — heading, chevron, padding, or the click a keyboard Enter produces on the shadow button —
    reaches a single delegated handler that Blazor installs once.
    The browser check asserts all of it — clipped height, `inert`, `aria-expanded` read out of the
    shadow root, the chevron's position and rotation — because a component test cannot see any of it.
    When composing the next component, the same rule applies: put the *state* in the markup (inline
    `grid-template-rows`, `inert`) and only the *motion* in CSS, so a stylesheet that never arrives
    degrades to a snap rather than to a panel that can never be collapsed.

26. **The data grid is composed, and four of the things that make it work were found in a browser, not
    read in a document.** `s-table` has no sorting, filtering, paging, selection or editing, so
    `PolarisDataGrid<TItem>`, `PolarisDataFilter`, `PolarisDataPager` and `PolarisRangeSlider` are
    built from elements that do, and share one page-owned `PolarisGridState<TItem>`. What follows is
    measured, and each item cost a failing browser run:
    - **A Polaris popover with no inline size lays its slotted content out at zero width.** The panel
      opens, the trigger reports `aria-expanded=true`, the content even has a height — and every
      control inside it is 0px wide, so a slider's track cannot be dragged and a text field is a
      sliver. `PolarisDataFilter` therefore states `InlineSize="320px"` on its panel. Nothing in the
      markup says this is missing.
    - **`s-button` and `s-popover` hosts compute to `display: contents`, like `s-clickable` and
      `s-box`.** Their own box is 0×0 at the page origin, so measuring a click point from the host
      clicks the top-left corner of the window, and measuring "is the panel open?" from the host
      always answers 0. Measure the `<button>` in the shadow root, and open-ness from a control
      *inside* the panel.
    - **Every browser check needs the point re-measured immediately before the click.** The probe
      scrolls the target into view with `scroll-behavior: auto`, waits two animation frames, then
      measures; a point computed earlier is a point measured against a different scroll position, which
      is why the first version of the grid check passed and failed on alternate runs and reported
      "the click does nothing".
    - **A native range input insets its thumb by half the thumb's width.** A handle at the maximum sits
      half a thumb inside the track's right edge, so a drag aimed at the track's end misses the target
      that is the only part of the input receiving pointer events.
    - **`input` is continuous and `change` is the end of the interaction.** Recorded from the page: a
      mouse drag produces four `input` events and exactly one `change`, on the frame the button is
      released (`change`, `pointerup` and `mouseup` all at the same timestamp); one arrow-key press
      produces `input` and `change` together. So a control that re-queries on `input` re-queries per
      pixel, and the only honest "the drag has finished" signal is `change`. Timing alone will not show
      this: the filter debounces by 300ms, so a fast drag looks innocent and a merchant who *holds* a
      handle still for half a second gets the query mid-drag — which is why the probe now pauses with
      the button still down before it samples.
    - **A control must adopt a caller's value when the parameter *changes*, not when it differs from
      what it last reported.** A parent that applies changes lazily (a debounced filter) re-renders with
      its *previous* value while the new one is in flight. Comparing against "the value we reported"
      makes that stale echo look like an instruction and resets the control: measured, the slider's
      handle jumped back on every second key press and a drag had to be released twice.
    - **An `s-stack direction="inline"` does not put two `s-number-field`s on one line.** Measured: the
      pair rendered full width and one above the other, because a Polaris host computes to
      `display: contents` and the boxes that paint live inside shadow roots, so they are not the flex
      items the stack lays out. A grid of real cells (`display:grid;grid-template-columns:minmax(0,1fr)
      minmax(0,1fr)` written inline) cannot be flattened away and holds at 50/50.
    - **An `s-popover` paints no padding, so its slotted content sits flush against its frame.** Measured:
      the `div.popover` inside the popover's shadow root computes to `0px` padding on every side and has
      *the same rect as the slotted panel* (320×300, content and frame identical). A control placed
      straight into a popover therefore puts its own border against the popover's border, which reads as
      a container that has been filled wrongly — and no markup assertion can see it, because the markup
      is exactly what was asked for. The panel states its own inset (`space-400`, 16px, read from
      `var(--p-space-400, 16px)`), and the probe asserts the painted inset. Note that the runtime does
      **not** define `--p-space-*` on `:root` or on the light-DOM panel — those custom properties live
      inside the shadow roots — so the fallback is the value in force today, and a `var()` without one
      would resolve to nothing at all.
    A `s-table-header` **does** accept light-DOM content, so its heading is an `s-button` and sorting is
    a real, keyboard-reachable control — asserted by clicking it in Chrome, because a component test can
    only prove the handler was attached. The grid marks its own `s-section` with `data-polaris-data-grid`
    so the probe can tell it from the gallery's plain `PolarisTable`, and the grid's browser assertions
    are skipped on a page that has none.
    Two more facts came from pointing the demo at a real, **asynchronously loaded** catalogue — the first
    time the components met data that arrived late, which is how they will meet a database:
    - **A control that renders before its data does must follow that data.** The filter panel draws its
      range sliders from the dataset's bounds, and it renders while the grid is still loading, so those
      bounds are a placeholder. A slider that only *clamped* to the domain kept the placeholder for ever:
      both handles parked at the first value of a two-value stand-in domain, showing 4.167% of the track
      and a range filter that matched everything and could not be dragged. `PolarisRangeSlider` now tracks
      the domain it was given and re-opens to a full range when the domain grows (a `null` `Value` means
      "the whole domain", so that is the correct reading), and a test holds it.
    - **A display format is not a field value, and a field's value is a number.** The two numeric fields
      are given a *plain* number, so `PolarisRangeSlider.InputFormat` (`"F2"`, two decimals) is separate
      from `Format` (which may be `"C2"`): a currency symbol and a group separator are not a number, and
      `s-number-field` parses the string it is handed, so `£1,234.57` there is a broken value with
      nothing logged. A domain bound is the cheapest and dearest row, so it is routinely a value nobody
      typed — `4.1666667` as a lower limit reads as a bug, which is why the field format exists at all.
      Two details are invisible as errors: `decimal.ToString("F2")` rounds a halfway value **to even**
      (`4.165` reads "4.16", a penny under the number a merchant writes down), so the rounding is stated
      with `MidpointRounding.AwayFromZero` against a scale parsed out of the format; and only the
      *display* is rounded — the interval the control reports keeps its full precision, so a filter
      never moves a bound nobody touched. Both places a bound is shown go through the one helper
      (`PolarisNumberText`) — the fields and the filter's own chip — because they are read side by side,
      and a chip reading `4.1666667` beside fields reading `4.17` looks like the filter is not what the
      panel said.
    - **A Polaris popover clips its content; it does not scroll it.** With six filter controls the panel
      is capped and the controls below the cap are painted beyond its edge, in space it does not occupy —
      `elementsFromPoint` there returns an empty list, and every drag aimed at one missed. `MaxBlockSize`
      was not the fix: the content has to be *shorter* than Polaris's cap, so the filter panel keeps its
      own scroll box (`max-block-size:300px;overflow-y:auto` on a plain `div`) inside the popover's frame.
    - **A filter that narrows must leave its own criterion out of its own choices.** `NarrowToResult`
      reads each control's options and the range's ends from the rows passing every *other* filter. The
      obvious version — read them from the filtered result — makes a multi-select offer only what is
      already ticked (a second value can never be added) and holds a slider at its own ends (the range can
      never be widened). It also has to re-read on **every** filter change, which is why the state clears
      its narrowed caches in `Refresh` rather than waiting for a new snapshot; a panel built once and never
      refreshed offers values the result no longer holds, with nothing to say why. And a narrowed domain
      can be smaller than a filter applied against a wider one, so the interval handed to
      `PolarisRangeSlider` is clamped into it — a handle drawn outside its own track is a percentage past
      100, not a range.
    The probe grew a `--screenshot <path>` option with all of this: a layout fault and a behaviour fault
    report identically through numbers, and an image answers "what does it look like" in one look.

27. **The grid edits cells like a spreadsheet, and every part of that failed at least once before it
    worked.** `Editable` on the grid plus `Editable` on a column, `EditorType`/`EditorOptions` for the
    control, and `ValueSetter` for a column whose expression is not a settable property. Drafts live in a
    session keyed by row; a save applies them through `PolarisGridState.UpdateItems` and asks
    `SaveChanges` whether the store took them. Six things found by running it, in the order they bit:
    - **`EventCallback.Factory.Create<T>(this, _ => SomeAsyncMethod())` quietly binds to the `Action<T>`
      overload.** The returned `Task` is dropped and the handler is fire-and-forget, so an exception
      inside it goes nowhere: measured, clicking from one cell into another did nothing at all, with no
      error in the page, the console or the server log. Every handler whose method returns `Task` is
      written `async args => await …` for exactly that reason.
    - **A frame's sequence numbers must not go backwards inside one scope, and the renderer does not
      check.** Cell content numbered below the cell's attributes threw
      `NotImplementedException: Unexpected frame type during RemoveOldFrame` — from an
      `ElementReferenceCapture` frame that the diff was then asked to remove. Content after attributes
      means numbering it above them; a capture is emitted *after* an element's children, as the Razor
      compiler emits it, and the grid keeps its one capture on an element it always renders (the
      `data-polaris-grid-root` wrapper it adds when `Editable`) rather than on a cell that comes and goes.
    - **`ElementReference.FocusAsync` on a Polaris host focuses nothing.** The host computes to
      `display: contents`, so the editor appeared correctly with `document.activeElement` on `body` and
      the next keystroke went to the page. `IPolarisInterop.FocusWithinAsync(element, selector)` searches
      *inside* one shadow boundary, and — measured — must search the shadow root **whole**
      (`shadowRoot.querySelector("input, textarea, select, [tabindex]")`) rather than descend from its
      `firstElementChild`: a Polaris shadow root begins with a `<style>`, so the descent found nothing and
      the focus call reported "nothing to focus". It also needs the selector: the grid's first focusable
      thing is the select-all checkbox in the header, not the editor.
    - **`mousedown` opens the editor, not `click`.** Committing the cell the merchant left re-renders the
      row, and a click whose mousedown and mouseup landed on different elements never fires — so clicking
      straight from one edited cell into another silently did nothing.
    - **Records are edited in place unless the column says otherwise.** A compiled property setter
      (including a positional record's `init` accessor, which reflection calls happily) mutates the
      instance, so `PolarisGridRowEdit.Original` is only pristine for a column that supplies a
      `ValueSetter` returning a copy — which is what the sample does (`product with { … }`). Two tests
      sharing one static record fixture each saw the other's edits before this was understood.
    - **A renamed row moves.** The demo sorts by title, so saving a rename re-queries and the row leaves
      the page it was on; an assertion that read "the first row's text" reported a working save as a
      revert. The browser check addresses cells by row key and types a title that sorts first, and waits
      for the page's own "written:" badge rather than reading it once — the callback and the render that
      shows its answer are two separate round trips.
    The probe drives all of it with real input: a click on the cell, `Input.insertText` over a selection
    (which produces the same `input` event a keystroke does), `Input.dispatchKeyEvent` for Enter, Tab and
    Escape, and a click on the save bar's shadow button — then asserts the draft, the dirty mark, the
    save bar, the saved value, and a save the sample's own price rule refuses.

28. **The combo-box question is settled by measurement, and the answer is not what the docs imply.**
    The Shopify Admin's own product-type field floats a suggestion list under a text input. It is built
    with **Polaris React** (`Autocomplete` + `Popover`, whose `active` prop is React state), not with the
    `s-*` web components this toolkit wraps, and the difference was measured in headless Chrome against
    the sample gallery rather than read anywhere:
    - **`s-popover` has no methods at all.** `show`, `hide`, `toggle`, `showOverlay`, `hideOverlay` and
      `toggleOverlay` are all `undefined` on the element. It cannot be opened from code, so a panel cannot
      be driven by focus or by typing.
    - **`s-search-field` exposes no anchor.** No `interestFor`, no `commandFor`, no suffix or accessory
      property, and its shadow root has **no slots**; setting `interestFor` on it does nothing. Nothing
      can hang a panel off a text field.
    - **A button's `commandFor` is the one way in, and it works.** Measured: a trigger `s-button` opened
      its popover as a 320×156 panel positioned below the trigger, focus went into the `s-search-field`
      inside it (`activeElement` = that input), and typing left the panel open with the field's value
      intact. So an overlay picker is reachable as *button-that-opens-a-picker*, which is a different
      control from *input-with-a-list*, and it is why `PolarisSuggestFieldInline` draws its panel inline.
    - **There is no listbox, combobox or autocomplete element** (`s-listbox`, `s-combobox`,
      `s-autocomplete`, `s-option-list`, `s-searchable-list` are all undefined).
    Two followings for any future field: arrow keys and `aria-activedescendant` are ours to write if
    wanted (the interop layer already exists), and a control whose value is the text must tell a caller's
    **echo** from an instruction — a page that binds the field's value echoes every keystroke back, and
    treating that as an instruction settled the field on the first character and closed its list. That
    one was found by driving the gallery in Chrome, not by a component test: bUnit asserted the callbacks,
    and the panel's disappearing was only visible on screen.

29. **The Polaris channel is one constant, and moving it changes the theme, not the component set.**
    Neither app hard-codes a Polaris `<script>` tag: both get it from
    `DesignSystemDefaults.PolarisScriptUrl` through `<ShopifyAppBridgeHead />`, as do the sample and
    every generated project. So "add the script to each project" is the wrong model — a second,
    hand-placed tag would load the bundle twice and one of the two would be the stale one. Edit the
    constant. Measured from the CDN with `HEAD`/`GET`, the channels are:
    - `polaris-2.js` — **v2.0.0**, the stable channel that Shopify's versioning reference names for
      production. This is the default. It advances within Polaris 2, so fixes arrive without a code
      change.
    - `polaris-2.0.js` — v2.0.0, the same bundle frozen. `polaris-2.1.js` is a **404**: there is no
      2.1 to pin yet.
    - `polaris-2.0-rc.js` — **v2.0.0-rc.3**, the superseded release candidate. It is still served,
      which is exactly why it gets picked up by mistake from old notes: it will never receive a fix,
      because its name says "candidate" and the release that shipped is `polaris-2.js`.
    - `polaris-1.js` — v1.1.0, the previous default.
    **What a 2.0 bundle does and does not do.** It carries both the current admin theme and Admin
    Next and follows whichever theme the merchant's admin is using — one URL, no flag, no per-theme
    path. It does **not** restyle markup we wrote ourselves: the library's own stylesheets
    (`polaris-accordion.css`, `polaris-grid.css`, `polaris-range-slider.css`) are ours and are
    untouched by the channel, and a page built from plain `<div>`s looks the same either way.
    **The element surface did not move with the major version.** Diffing the shipped bundles,
    `polaris-1.js` (1.1.0) and `polaris-2.js` (2.0.0) define the *same 74* `s-*` names, and neither
    defines an accordion. That has two consequences worth keeping: no wrapper here is orphaned by the
    move, and `minPolarisVersion` in `tools/polaris-elements.json` stays at each element's true floor
    of **1.1** — bumping it to 2.0 would claim the element was introduced in 2.0, which is false and
    would make the inventory lie about the compatibility it exists to record.
    **The behaviour was re-verified, and it holds.** `tools/verify-in-browser.mjs --assert` was run
    against both the gallery (`/components`, 60 listed elements) and the dashboard's data grid (`/`)
    with `polaris-2.js` loaded: 0 undefined, 0 unupgraded, every element with a shadow root, and all
    39 assertions passing on each page — the accordion's `aria-expanded` written into `s-clickable`'s
    shadow button, the popover opened by declarative `commandFor`, the slider re-querying on `change`
    rather than on `input`, and cell editing with Enter/Tab/Escape and a server-refused save. So the
    1.x property semantics this file documents survive the major version. What did **not** change with
    it: the toolkit's own stylesheets, the 74-name element set, and `minPolarisVersion`'s floor.
    `App Bridge Next: missing required configuration fields: shop` on the console is expected and is
    not a regression — App Bridge only completes configuration inside a real Admin.

30. **An `s-option` with an empty `value` reports its own LABEL, so "nothing chosen" needs a sentinel.**
    Measured in the add dialog: the "No supplier" option carried `value=""`, and once it was chosen the
    select reported `"No supplier"` — the merchant's supplier would have been saved as the words "No
    supplier". Polaris substitutes the label when the value is empty, so
    `PolarisAddableSelect.EmptyOptionValue` is an explicit sentinel and `ValueChanged` reports `null` for
    it. The select is also *given* the sentinel when nothing is chosen, so its own state says "none"
    rather than going blank — a third state nobody asked for. Three smaller things learned alongside it:
    - **A component reference capture is a frame of its own.** `AddComponentReferenceCapture` followed by
      `AddAttribute` throws `Attributes may only be added immediately after frames of type Element or
      Component`, at render time. Every attribute goes first, then the capture, and the sequence numbers
      still have to increase.
    - **A validated slot takes its name from the child, not the parent.** The modal's `primary-action` and
      `secondary-actions` are set with `PolarisButton Slot="…"` inside the modal's own `ChildContent`;
      there is no `PrimaryAction` fragment parameter to set, because a wrapper element would be rejected.
    - **The gallery page is longer than the viewport and its scroll container is not the window.** A CDP
      mouse click at a measured point below the fold lands on nothing — `scrollIntoView` did not move
      `window.scrollY` — so a probe that must press something far down the page clicks the element's own
      shadow `<button>` instead.

31. **A dialog's actions, and an editable grid inside it, fail in ways that look like nothing at all.**
    Found while building `PolarisAddableSelect`, by bisecting the Add-material page after it stopped
    answering requests while spinning at full CPU:
    - **A slot attribute only counts on the HOST's own children.** The modal's buttons were first put
      inside the dialog body's stack; `slot="primary-action"` there is ignored, so the dialog rendered
      with **no OK and no Cancel** and nothing logged. They are now siblings of the body — direct
      children of `s-modal` — and a test asserts that, plus that nothing with a slot is nested in the
      body.
    - **A Polaris component WITH an event handler inside an editable grid's `CellTemplate` hangs the
      page.** Measured by bisection, one variable at a time: plain text in the template rendered;
      `PolarisButton` with no `OnClick` rendered; `PolarisButton` with `OnClick` made every request to
      the page hang while the process burned a core, with no exception and no log line. Deleting a grid
      row therefore goes through `AllowSelection="true"` and a button *below* the grid, never through a
      button in a cell.
    - **`mousedown` opens the grid editor, and it must land on the cell's own
      `[data-polaris-grid-cell]` div.** Dispatching it at the enclosing `s-table-cell` does nothing:
      the handler is on the div, which is also the element carrying `tabindex="0"`. That is how the
      rename path was driven in Chrome.
    - **A page's copy of a grid selection goes stale after a programmatic reload.** The state drops keys
      that no longer exist without raising `SelectionChanged`, so a page that keeps the last
      `SelectionChanged` payload goes on saying "1 selected" with a live Delete button after the row it
      named has been deleted. `PolarisGridState.ClearSelection()` and clearing the page's own copy is the
      fix.

## Resetting the build tree

To force a from-scratch build, remove **only** .NET build output. Do not reach for
`find -name bin`:

```sh
# WRONG: also deletes bin/, and bin/shopify-publish lives there. It has been lost this way once, and
# silently kept out of a commit another time by a blanket `bin/` in .gitignore.
find . -type d \( -name bin -o -name obj \) -prune -exec rm -rf {} +

# RIGHT: only the configuration subdirectories a .NET build creates.
rm -rf ./**/bin/Debug ./**/bin/Release ./**/obj
```

`bin/shopify-publish` is a tracked file in a directory named `bin`, which is exactly the kind of
collision a broad pattern does not warn about.

## Conventions

- `Polaris.Blazor` must not depend on `Shopify.Blazor`. The component library is usable alone.
- Tests assert on rendered markup, not on internal state, and every test carries a comment
  explaining the failure it prevents. A test that cannot say what it would catch is not kept.
- Documentation comments on every public member. The build does not require them, but
  `tools/generate-component-reference.py` reads them to generate `docs/components.md`, so an
  undocumented parameter is an undocumented component.
- New components: add the class in `Components/`, an entry in the test file, then run
  `tools/generate-component-reference.py`.
- Every source file under `src/` carries a sibling `<name>.md` summary, and changing the source means
  changing its summary in the same commit — see
  [Source summaries](#source-summaries--read-these-instead-of-the-source). `tools/check-drift.py`
  fails on a summary whose recorded hash no longer matches its source.
- Commit and push. This machine has no other safety net and sessions do not share state. Pushing
  needs the `gh` token, because the credential helper wants a terminal and fails with
  `could not read Username for 'https://github.com'`:

  ```sh
  TOKEN=$(gh auth token)
  git -c credential.helper= \
      -c "credential.helper=!f() { echo username=x-access-token; echo password=$TOKEN; }; f" \
      push origin main
  ```

## Not verified

Installation in a real Shopify development store. It needs Partner Dashboard credentials and a public
HTTPS server, so it is a human step. See `docs/PROGRESS.md`.
