<!-- summary-of: Error.razor -->
<!-- source-sha256: 66f082261cbf -->

# Error.razor

The page the exception handler redirects to: a Polaris "something went wrong" page that deliberately
shows no exception detail and never a stack trace. It has no parameters and no logic of its own.

`Components/Pages` · `@page "/Error"` · no parameters, no `@code` block

## What it renders

| Level | Detail |
|---|---|
| `PolarisPage` | `Heading="Something went wrong"`. |
| `PolarisSection` | No heading, one child. |
| `PolarisBanner` | `Heading="The request could not be completed"`, `Tone="PolarisTone.Critical"`. |
| Body | A `PolarisParagraph` telling the reader to check the server log, and stating that production shows no detail and never a stack trace. |

Nothing in it is conditional, so the page looks the same for every failure. There is no error object, no
`[SupplyParameterFromQuery]` and no route data.

## Gotchas

- **It is reached only through `UseExceptionHandler("/Error", createScopeForErrors: true)`, which
  `Program.cs` registers in non-Development only.** In Development the developer exception page answers
  instead, so an unchanged-looking error page locally means the handler is not the one responding.
- **The page is detail-free by design.** Adding `ex.Message` here would put exception text — file paths,
  SQL, configuration names — in a response any embedded merchant can provoke. The server log is the
  place to look, as the copy says.
- **It is not the router's `NotFound` page.** An unmatched URL renders `Routes.razor`'s "Not found"
  banner inside the layout; this page is a normal route that only the middleware redirects to, and it is
  reachable directly at `/Error`.
- **It inherits `@rendermode InteractiveServer` from `Routes.razor`.** It has no interactive behaviour of
  its own, so the mode buys it nothing — but it also means the page fails the same way the rest of the
  app does if the circuit cannot start.
- `UseStatusCodePages()` in `Program.cs` handles a bare status code; it does not route here, so a 404
  from a missing static asset never shows this page.

## See also

- [Dashboard.razor.md](Dashboard.razor.md) — the page whose failed requests land here.
- [../Routes.razor.md](../Routes.razor.md) — the router, whose `NotFound` branch is a different page from this one.
- [../Layout/MainLayout.razor.md](../Layout/MainLayout.razor.md) — the shell this page renders inside.
- [../../Program.cs.md](../../Program.cs.md) — the `UseExceptionHandler` registration that points at `/Error`.
