<!-- summary-of: PolarisAppNav.cs -->
<!-- source-sha256: 4a61b4b706d3 -->

# PolarisAppNav.cs

The app's navigation menu inside the Shopify admin — an App Bridge element that configures the admin's own chrome rather than drawing a menu inside the app.

`Polaris.Blazor.Components` · renders `<s-app-nav>` (App Bridge, not Polaris) · base `PolarisContentComponentBase`

## Parameters

No parameters of its own; the element's contents are the links, supplied as `ChildContent`.

Inherited: `ChildContent` (a `PolarisLink` per menu item) plus `Id`, `AccessibilityLabel`, `ItemTag`, `Slot` and `AdditionalAttributes` from
[PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md).

## Behaviour

- **It is App Bridge, not Polaris.** The element configures part of the admin that *surrounds* the app: on desktop it renders in the admin's left sidebar, on mobile as a dropdown from the title bar. The markup in the app's frame does not contain the menu.
- One `PolarisLink` per item. A link is the whole item — label as child content, `Href` as the destination.
- **The home route needs no link by default.** The app's name in the sidebar already links to `/`, so `rel="home"` is only for a home page that lives somewhere else, and such a link is hidden from the rendered menu because the admin draws the landing page itself.
- Pass `rel="home"` through `AdditionalAttributes`, which captures unmatched attributes: `<PolarisLink Href="/app" rel="home">Home</PolarisLink>`.
- Because the element belongs to the admin's chrome rather than the app's page, a standalone browser session shows no navigation at all — the absence is the correct behaviour, not a broken render.

## Gotchas

- **Exactly one app nav per document, and it must be in the body.** A second one, or one inside an overlay, is not merged or rejected — it simply has no effect.
- **Navigation cannot be nested.** One level only; a submenu placed inside an item silently does nothing, so a hierarchy has to be flattened or moved into the page.
- **An `Href` must be a relative path inside the app.** The admin navigates the app's own frame rather than following a URL, so an absolute or external URL is not a navigation target.
- **A `rel="home"` item disappears from the menu.** Do not add a visible "Home" entry and wonder where it went: the admin owns that entry, and the link exists only to correct which route it points at.
- Nothing here reports the active route; marking the current item is the page's job, not this component's.

## See also

- [PolarisLink.cs.md](PolarisLink.cs.md) — the item type the navigation expects.
- [PolarisAppWindow.cs.md](PolarisAppWindow.cs.md) — the other App Bridge element, for a fullscreen route.
- [PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md) — `AdditionalAttributes` and the shared parameters.
