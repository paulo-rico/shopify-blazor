<!-- summary-of: App.razor -->
<!-- source-sha256: b7a757623d73 -->

# App.razor

The single HTML document the whole app renders into. It sets up `<head>` with the App Bridge and
Polaris scripts, a stylesheet and the `HeadOutlet`, then puts the router, the reconnection dialog and
three script tags in `<body>` in an order that has to be kept.

`Components` · the root document, rendered by `MapRazorComponents<App>()` · base `ComponentBase`, no parameters

## What it renders

| Position | Element | Why it is there |
|---|---|---|
| head | `<meta charset>`, `<meta viewport>`, `<base href="/">` | A missing `<base href>` breaks routing and every relative asset URL, including `_content/…` and `css/…`. |
| head | `<ShopifyAppBridgeHead />` | Emits the `shopify-api-key` meta tag and the App Bridge and Polaris script tags, in that order, from configuration. It has to be before anything renders. |
| head | `<title>Shopify.Blazor.SampleApp</title>` | The template's project name; the template engine's `sourceName` replacement rewrites it to the generated project's name. |
| head | `<link rel="stylesheet" href="css/reconnect-modal.css" />` | The reconnection dialog's styles as a plain static file, not a scoped `.razor.css`, so it carries no assembly-name dependency and works identically in the sample and in a generated project. |
| head | `<HeadOutlet />` | Emits `PageTitle`, `<meta>` and other head content raised by components. Without it a page's head content is silently dropped. |
| body | `<Routes />` | The router. `Routes.razor` is where `@rendermode InteractiveServer` is applied, so the interactive mode is not visible in this file. |
| body | `<ReconnectModal />` | Deliberately outside the router: it is static markup that JS shows when no circuit exists to render anything. |
| body | `<script src="_framework/blazor.web.js">` | Starts Blazor. It comes after the markup so the components exist when it runs. |
| body | `<script type="module" src="js/reconnect-modal.js">` | Wires the reconnection dialog; it looks its elements up at parse time, so it must follow the dialog. |
| body | `<script type="module" src="js/shopify-fetch.js">` | Only with `include-admin-api-example`: the module `IAppBridgeInterop.FetchAsync` imports. |

## Gotchas

- **The two head scripts are render-blocking on purpose.** They carry no `defer`, `async` or `type`, so
  they have executed before the components below them upgrade. Loading either lazily gives a page of
  unstyled, inert elements with no error and no console message — the markup is correct and no custom
  element is defined.
- **Do not add a second copy of either script here.** Both URLs come from configuration through
  `ShopifyAppBridgeHead`; a hand-placed tag loads the bundle twice and one of the two is stale. The
  Polaris channel is changed in `AppBridgeOptions`, not in this file.
- **`<ReconnectModal />` must stay outside `<Routes />`.** Inside the router it would be re-rendered by
  a circuit that, by definition, may not exist — and the dialog exists to report exactly that.
- **The conditional script uses Razor's own directive syntax**, `@*#if (…)*@ … @*##endif*@`. A bare
  `#if` is emitted literally into the generated markup and then fails to compile, which is one of the
  ways a template breaks while this repository builds.
- **`js/shopify-fetch.js` is excluded with `--include-admin-api-example false`.** Its script tag is the
  only reference to it in this file; adding another one that is not wrapped in the same directive
  produces a module 404 in a generated project, and `FetchAsync` then fails without an exception.
- `<HeadOutlet />` and `HeadContent` are the only way a page changes the head — nothing here
  special-cases a page's title.

## See also

- [Routes.razor.md](Routes.razor.md) — the router this document hosts, and where the render mode lives.
- [Layout/ReconnectModal.razor.md](Layout/ReconnectModal.razor.md) — the dialog markup whose script and stylesheet are loaded here.
- [Layout/MainLayout.razor.md](Layout/MainLayout.razor.md) — the shell every page renders inside.
- [Pages/Error.razor.md](Pages/Error.razor.md) — the page `UseExceptionHandler` points at.
