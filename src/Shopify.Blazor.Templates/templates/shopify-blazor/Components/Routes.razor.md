<!-- summary-of: Routes.razor -->
<!-- source-sha256: 859af4d5624d -->

# Routes.razor

The router, and the one place the app's render mode is stated. Every page renders inside `MainLayout`
through it; a route that matches nothing falls through to a Polaris "not found" page rather than a blank
page.

`Components` · router + layout + `@rendermode InteractiveServer` · base `ComponentBase`, no parameters

## What it renders

| Part | Detail |
|---|---|
| Render mode | `@rendermode InteractiveServer` on the whole file, so the router and every page under it are interactive server components. |
| `<Router AppAssembly="@typeof(Program).Assembly">` | Scans the app assembly for `@page` directives. Pages in other assemblies are not discovered. |
| `<Found>` | `LayoutView` with `Layout.MainLayout` wrapping a `RouteView` whose `DefaultLayout` is the same layout, so the layout applies either way. |
| `<FocusOnNavigate RouteData Selector="s-page" />` | Moves focus to the page's `<s-page>` element after each navigation — the keyboard/screen-reader behaviour the Admin expects. |
| `<NotFound>` | `LayoutView` + `PolarisPage Heading="Not found"` + `PolarisSection` + `PolarisBanner Tone="PolarisTone.Warning"` reading "That page does not exist" with a hint to check the address. |

## Gotchas

- **`@rendermode InteractiveServer` is the only reason any handler in the app works.** A Blazor Web App
  is static by default: the page renders perfectly and no event is ever attached, which looks like a
  styling problem. Removing or overriding this line leaves `MainLayout`'s embedded check and
  `Dashboard`'s `OnAfterRenderAsync` probe never running.
- **The render mode is applied here, not in `Program.cs`.** `AddInteractiveServerRenderMode()` only
  makes the mode available; this directive is what selects it, and it does so for the whole subtree.
- **`Selector="s-page"` silently does nothing on a page that renders no `s-page`.** `PolarisPage` emits
  one, so every page here qualifies; a hand-written page that skips it loses focus handling with no
  warning.
- **`NotFound` is not the error page.** It is the router's fallback for an unmatched URL and renders
  inside the shell; `Error.razor` is a separate route the exception handler middleware points at.
  Neither one catches an exception thrown during rendering.
- The embedded-app framing middleware has to be registered before anti-forgery in `Program.cs`, or the
  interactive page inside this router can never load inside the Admin's iframe — the markup here is
  irrelevant to that failure.

## See also

- [App.razor.md](App.razor.md) — the document that hosts this router and loads the scripts it depends on.
- [Layout/MainLayout.razor.md](Layout/MainLayout.razor.md) — the layout applied by both branches.
- [Pages/Dashboard.razor.md](Pages/Dashboard.razor.md) — the page at `/`, and the one that proves the mode is on.
- [Pages/Error.razor.md](Pages/Error.razor.md) — the other page a failed request can reach.
