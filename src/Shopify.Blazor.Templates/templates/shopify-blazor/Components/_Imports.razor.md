<!-- summary-of: _Imports.razor -->
<!-- source-sha256: 0ec8fe425a45 -->

# _Imports.razor

The whole project's `@using` set in one file, so no page or component needs a directive of its own. It
imports the Blazor namespaces, the two toolkit namespaces, the event types, and the app's own
`Shopify.Blazor.SampleApp.*` namespaces.

`Components` · global `@using` directives for every component · not a component itself, nothing rendered

## What it imports

| Namespace | What it makes available here |
|---|---|
| `System.Net.Http` | `HttpClient` and friends, for a page that calls something directly. |
| `Microsoft.AspNetCore.Components.Forms` | `EditForm`, `InputText` and the validation components. |
| `Microsoft.AspNetCore.Components.Routing` | `Router`, `RouteView`, `LayoutView`, `FocusOnNavigate` — what `Routes.razor` uses — plus `NavLink`. |
| `Microsoft.AspNetCore.Components.Web` (static-imported as `RenderMode`) | `RenderMode`'s members directly, so `Routes.razor` can write `@rendermode InteractiveServer` instead of `RenderMode.InteractiveServer`. |
| `Microsoft.AspNetCore.Components.Web` | The web component set: `HeadOutlet`, `PageTitle`, `SectionOutlet`, and the DOM event argument types. |
| `Microsoft.JSInterop` | `IJSRuntime` and the interop attributes, for a page that calls JS itself. |
| `Polaris.Blazor` | The base classes and shared types (`PolarisTone`, the enums, the interop interface). |
| `Polaris.Blazor.Components` | The wrapper components: `PolarisPage`, `PolarisSection`, `PolarisStack`, `PolarisButton` and the rest. |
| `Polaris.Blazor.Events` | `PolarisEventArgs`, the argument of every Polaris callback — without it an `OnClick` handler in a page would need a fully qualified type. |
| `Shopify.Blazor` | The Shopify services, options and exceptions (`ShopifyOptions`, `ShopifyAdminApiException`). |
| `Shopify.Blazor.Components` | `ShopifyApp`, `ShopifyAppBridgeHead`. |
| `Shopify.Blazor.SampleApp`, `.Components`, `.Components.Layout`, `.Services` | The app's own root namespace, its components, its layout, and `BearerTokens`/`ProductsService`. |

## Gotchas

- **`@using static Microsoft.AspNetCore.Components.Web.RenderMode` is what makes `@rendermode
  InteractiveServer` compile.** Without it the line in `Routes.razor` fails as an unknown identifier,
  and the fix looks like it should be a namespace import.
- **The app namespaces are renamed because they contain the template's `sourceName`, and only those
  are.** A namespace added here with a different root is left untouched by generation, and the mismatch
  shows up as "the type or namespace could not be found" in the generated project only — this repository
  still builds, because its own namespace is the literal one.
- **The toolkit's namespaces are imported explicitly even though the app's root sits under
  `Shopify.Blazor`.** Implicit enclosing-namespace lookup would find `IShopifyAdminApi` in this
  repository, but a generated project renames the root and the walk-up then stops matching, so anything
  a template copies must spell the toolkit namespaces out.
- **This file is not conditional, so every import must resolve under every template parameter
  combination.** `Services` survives `--include-admin-api-example false` because `BearerTokens.cs` stays
  in it; an import of a namespace whose last file is excluded would break generation with no warning
  before `dotnet build`.
- A missing import surfaces as a Razor compile error naming the component, not the file — the component
  is in `Polaris.Blazor.Components` and the fix belongs here, not in the page.

## See also

- [App.razor.md](App.razor.md) — the document that loads the scripts these namespaces' components need.
- [Routes.razor.md](Routes.razor.md) — the file that depends on the static `RenderMode` import.
- [Pages/Dashboard.razor.md](Pages/Dashboard.razor.md) — a page using `PolarisEventArgs` and `IAppBridgeInterop` without any `@using` of its own.
