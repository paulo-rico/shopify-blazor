<!-- summary-of: PolarisStylesheets.cs -->
<!-- source-sha256: 174884b124e2 -->

# PolarisStylesheets.cs

The three stylesheets this package ships, rendered as `<link>` elements so a page can put them in its document head. The interop module loads the same files, but only once the Blazor circuit starts — and a full page load paints long before that.

`Polaris.Blazor.Components` · renders three `<link>` elements · base `ComponentBase`

## Parameters

None. The file list is fixed: `polaris-accordion.css`, `polaris-range-slider.css`, `polaris-grid.css`.

## Behaviour

- **Why a component and not just the module's injection.** `polaris-interop.js` imports the stylesheets when it is imported, which happens on the circuit's first interop call. Measured on a deployed product editor: at 3.5 s the page had rendered its 53 `<details>` sections with the accordion's rules absent, the circuit replaced that DOM at 4.5 s, and the stylesheet arrived at 6.8 s — 15–20 seconds on a slower connection. The markup was correct throughout; what a merchant saw was a correctly built page with no header band, no padding and a default disclosure marker, which reads as the app having lost its styling.
- In the head, those same rules are part of the first paint. The module's injection stays as a fallback for an app that renders no head links, and it now skips a file that is already linked (matched on the file name, because the head's href is relative and the module's is absolute and stamped).
- **Each link is stamped** with `PolarisStaticAssets.Url`, the build's module version id. `data-polaris-stylesheet` carries the same href, which is what the interop module used to match on.

## Gotchas

- **It has to be rendered inside `<head>`**, next to `ShopifyAppBridgeHead`. Rendered in the body it still loads, but the point — first paint — is lost, and the browser may block on a body stylesheet in a different order than a head one.
- **Rendering it is not optional in practice.** An app that leaves it out keeps the old behaviour: styled only after the circuit starts. Nothing logs or fails; the page simply appears unstyled for seconds.
- The three filenames are a fixed array, so a new stylesheet in `wwwroot` has to be added here as well as to `polaris-interop.js`'s list.
- Two links to one file would be two requests; the module's guard prevents that only when it can see the head's link, which is why the guard matches on the file name rather than the exact href.

## See also

- [../PolarisStaticAssets.cs.md](../PolarisStaticAssets.cs.md) — the stamp and the URL shape.
- [../wwwroot/polaris-accordion.css.md](../wwwroot/polaris-accordion.css.md), [../wwwroot/polaris-grid.css.md](../wwwroot/polaris-grid.css.md), [../wwwroot/polaris-range-slider.css.md](../wwwroot/polaris-range-slider.css.md) — what the three files are for.
- [../wwwroot/polaris-interop.js.md](../wwwroot/polaris-interop.js.md) — the fallback injection and the file-name guard.
