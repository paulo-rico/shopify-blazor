<!-- summary-of: ShopifyAppBridgeHead.cs -->
<!-- source-sha256: 013437909f37 -->

# ShopifyAppBridgeHead.cs

Renders the three head elements an embedded Shopify app requires: the `shopify-api-key` meta tag and
the App Bridge and Polaris script tags, in that order. It takes no parameters and reads every value
from configuration, so the two script URLs change in one place rather than in markup.

`Shopify.Blazor.Components` · renders `<meta>` + two `<script>` tags · base `ComponentBase`

## What it renders

| Order | Element | Value comes from |
|---|---|---|
| 1 | `<meta name="shopify-api-key" content="…">` | `ShopifyOptions.ApiKey` |
| 2 | `<script src="…"></script>` | `AppBridgeOptions.AppBridgeScriptUrl` |
| 3 | `<script src="…"></script>` | `AppBridgeOptions.PolarisScriptUrl` |

Both option objects are injected (`IOptions<ShopifyOptions>` and
`IOptions<AppBridge.AppBridgeOptions>`); nothing is a parameter and nothing is configurable per use.

## Behaviour

It is meant to be placed inside `<HeadContent>`, so the three elements land in the document head
rather than in the body. Both scripts must be present **before** the app's own components render: App
Bridge reads the API key from the meta tag and injects the `shopify` global, and Polaris defines the
custom elements the whole UI is built from.

App Bridge is emitted before Polaris deliberately — Polaris components take their admin behaviour from
the host App Bridge provides. There are no attributes on the script tags, so neither is `defer`red,
`async`ed or given a type: they are ordinary render-blocking scripts, which is what guarantees they
have executed before the components below them upgrade.

## Gotchas

- **Loading either script lazily produces a page of unstyled, inert elements with no error.** Nothing
  throws, nothing logs; the markup is correct and no custom element is defined, so the page looks
  empty or unstyled rather than broken. This component exists so that timing is not left to a
  hand-written tag.
- **A second, hand-placed copy of either script is worse than none.** The URLs already come from
  `AppBridgeOptions`, so the Polaris channel is changed there; two tags load the bundle twice and one
  of the two is the stale one.
- **The API key is public, the rest of the options are not.** The key is written into the page by
  design (App Bridge needs it); nothing else from `ShopifyOptions` may be rendered into markup, which
  is why this component reads no other property.
- Rendering this outside `<HeadContent>` does not fail; it emits the tags into the body, where they
  still load but the meta tag is no longer where App Bridge's own documentation expects it.

## See also

- [ShopifyApp.cs.md](ShopifyApp.cs.md) — the cascading component the scripts make usable.
- [AppBridgeInterop.cs.md](../AppBridge/AppBridgeInterop.cs.md) — the calls guarded by whether these scripts have run.
- [ShopifyOptions.cs.md](../ShopifyOptions.cs.md) — where `ApiKey` is configured.
