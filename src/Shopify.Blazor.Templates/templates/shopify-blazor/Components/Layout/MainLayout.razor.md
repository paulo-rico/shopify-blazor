<!-- summary-of: MainLayout.razor -->
<!-- source-sha256: 80f7acc943e1 -->

# MainLayout.razor

The embedded shell every page renders inside: `ShopifyApp`, a Polaris page heading, a small nav, and a
warning when the app is open outside the Admin. It reports embedded state rather than gating anything, so
every page also renders in an ordinary browser tab.

`Components/Layout` · the app's only layout · base `LayoutComponentBase` (`@inherits LayoutComponentBase`)

## What it renders

| Region | Detail |
|---|---|
| Outer | `<ShopifyApp @ref="_app">` — cascades App Bridge and exposes `IsEmbedded`, which this file reads through the captured component reference. |
| Page | `PolarisPage Heading="Shopify.Blazor.SampleApp"` containing one `PolarisStack` at `PolarisGap.Large`. |
| Standalone banner | Only when `_app?.IsEmbedded == false`: a `PolarisBanner` at `PolarisTone.Warning`, heading "Opened outside the Shopify Admin", explaining that App Bridge is unavailable so session tokens, toasts and Admin navigation are inert — while the Polaris layout itself is real. |
| Nav | Inline `PolarisStack` (`PolarisStackDirection.Inline`, `PolarisGap.Base`) of `PolarisButton`s with `Href`, `Target="PolarisLinkTarget.Self"` and `Variant="PolarisButtonVariant.Tertiary"`: **Dashboard** (`/`) always; **Products** (`/products`) only with `include-sample-pages` *and* `include-admin-api-example`; **Components** (`/components`) only with `include-sample-pages`. |
| Divider | `<PolarisDivider />` between the nav and the page. |
| Body | `@Body` — the routed page. |

## Code

| Member | Type | What it does |
|---|---|---|
| `Bridge` | `[Inject] IAppBridgeInterop` | Injected and never referenced. The embedded check goes through `_app` instead, so removing this injection would change nothing. |
| `_app` | `ShopifyApp?` | The component reference filled by `@ref`; null until the component has rendered. |

## Gotchas

- **`_app?.IsEmbedded == false` is the only state that shows the banner.** `IsEmbedded` is `bool?` and
  starts `null` while the probe is undetermined, so the banner is absent on the first paint and appears
  when `ShopifyApp`'s own post-render probe calls `StateHasChanged`. `null` must not be treated as
  "standalone": doing so renders the warning on the server and contradicts itself one render later.
- **`@ref` on a component is not a DOM reference.** `_app` is the `ShopifyApp` instance, which is what
  makes `IsEmbedded` reachable; `ElementReference` would not expose it.
- **The nav is made of link buttons, not `NavLink`s.** Each click is a browser navigation to a new
  page rather than client-side routing, so the app reloads and no nav item shows an active state. That
  is also why the buttons need `Target="PolarisLinkTarget.Self"` rather than a route parameter.
- **The heading is hard-coded to the template's project name**, not read from configuration — the
  template's `sourceName` replacement is what makes it match a generated project.
- The two conditional nav entries are wrapped in `@*#if (…)*@ … @*##endif*@`; a bare `#if` is copied
  into the output verbatim and then fails to compile in the generated project.
- This layout does not gate anything. It reports whether the app is embedded; it never blocks rendering
  when it is not.

## See also

- [ReconnectModal.razor.md](ReconnectModal.razor.md) — the other piece of the shell, rendered outside the router.
- [../Routes.razor.md](../Routes.razor.md) — where this layout is attached to every route.
- [../Pages/Dashboard.razor.md](../Pages/Dashboard.razor.md) — the page at `/`, which runs the same `IsAvailableAsync` probe for its own banner.
- [../../../../../Shopify.Blazor/Components/ShopifyApp.cs.md](../../../../../Shopify.Blazor/Components/ShopifyApp.cs.md) — the component this file wraps, and the `IsEmbedded` semantics it relies on.
