<!-- summary-of: PageTemplate.cs -->
<!-- source-sha256: f535d7cc3155 -->

# PageTemplate.cs

The Razor source `shopify-blazor add page` writes: a kebab-case route, an explicit
`@rendermode InteractiveServer`, and four branches — loading, error, empty and content — that are
present so a generated page cannot silently render nothing. It is opinionated on purpose, because a page
with no loading flag, no empty branch and a swallowed exception looks perfect and tells the developer
nothing.

`Shopify.Blazor.Cli` · `static class` of pure string builders · the page generator behind `add page`

## Members

| Member | Signature | What it does |
|---|---|---|
| `RenderModeDirective` | `const string` = `"@rendermode InteractiveServer"` | The render mode every page must state, interpolated into the output so the constant and the file cannot drift. |
| `Route` | `static string Route(string name)` | `"/"` + the kebab-case name, leading slash included. |
| `ClassName` | `static string ClassName(string name)` | The PascalCase name, used for both the class and the file name. |
| `Render` | `static string Render(string name)` | The complete `.razor` file as one string. Writes nothing itself. |

## The file it generates

1. **Directives.** `@page "<route>"`, then `@rendermode InteractiveServer`, then two comment blocks
   explaining why the render mode is explicit (a Blazor Web App is static without it, so the markup
   renders and every handler is never attached) and why every namespace is named rather than left to
   `_Imports.razor` or enclosing-namespace lookup.
2. **Imports and injection.** `@using Polaris.Blazor.Events`, `@using Shopify.Blazor.AppBridge`,
   `@inject IAppBridgeInterop Bridge`.
3. **A comment** naming the class and the command that generated it, and telling the reader to keep the
   four branches, because `doctor` looks for the same rules.
4. **Markup.** `PolarisPage Heading="<ClassName>"` wrapping a `PolarisStack Gap="PolarisGap.Base"`:
   - A first section with a `PolarisButton` labelled Refresh — `Variant` primary, `OnClick="LoadAsync"`,
     `Loading="_loading"`.
   - `@if (_loading)`: an inline `PolarisStack` holding a `PolarisSpinner` with
     `AccessibilityLabel="Loading <ClassName>"` and a `PolarisText` reading "Loading <ClassName>…".
   - `else if (_error is not null)`: a `PolarisBanner Heading="Could not load <ClassName>"
     Tone="PolarisTone.Critical"` containing `PolarisParagraph>@_error`.
   - `else if (_items is null || _items.Count == 0)`: a `PolarisEmptyState Heading="Nothing here yet"`
     with a `PolarisText Slot="subheading"`.
   - `else`: a `foreach` over `_items` printing each as a `PolarisText`.
5. **`@code`.** Fields `_loading`, `_error` and `_items` (an `IReadOnlyList<string>?`); a single
   `LoadAsync(PolarisEventArgs args)` that awaits `ReloadAsync()`; and `ReloadAsync`, which returns
   immediately if `_loading`, sets `_loading`, clears `_error`, `await Task.Yield()`, assigns
   `_items = []`, catches any exception into `_error = ex.Message`, and clears `_loading` in a `finally`.
6. **A TODO comment** showing the shape of the real call —
   `Bridge.WithLoadingAsync(() => Bridge.FetchAsync<MyResponse>("/api/<route>").AsTask())` — and pointing
   at the sample app's Products page, because the server owns the Shopify credentials and the browser
   asks the app's own endpoint.

## Gotchas

- **One `LoadAsync` overload only.** Adding a parameterless overload beside the `PolarisEventArgs` one
  makes `OnClick="LoadAsync"` ambiguous between `Func<Task>` and `Func<T, Task>` — a Razor compile
  error, so every generated page would fail to build.
- **The branch order is load-bearing.** Loading is tested first, then error, then empty. Swap empty
  above error and a failed load shows "Nothing here yet" underneath the failure banner; that is exactly
  the silent lie the shape exists to prevent.
- **The empty state is conditional for a reason.** An `PolarisEmptyState` cannot detect emptiness on its
  own, so an unconditional one sits beside a full table.
- **A null `_items` reads as empty.** The condition is `_items is null || _items.Count == 0`, so the
  stub's initial state renders the empty branch — a freshly generated page never shows the content
  branch until the real call fills the list.
- **`Task.Yield()` is what lets the loading branch render.** Without an await before the work, a
  synchronous stub finishes inside the same render and the spinner never appears.
- **`Route` and `ClassName` do not validate anything.** An empty name yields the route `/` and an empty
  class name, so `NameHelper.IsValidComponentName` must have been called first.
- **The exception is deliberately assigned, not swallowed.** A swallowed exception is indistinguishable
  from an empty result, and the empty state would then lie about what happened.
- **Nothing here contacts Shopify.** The generated page calls this app's own endpoint; the TODO says so
  explicitly so a generated project does not acquire a client secret in the browser.

## See also

- [NameHelper.cs.md](NameHelper.cs.md) — the route and class-name rules this uses.
- [Commands/AddPageCommand.cs.md](Commands/AddPageCommand.cs.md) — decides the path, honours `--force` and `--nav`.
- [NavMenuInserter.cs.md](NavMenuInserter.cs.md) — where the page's route becomes a navigation entry.
