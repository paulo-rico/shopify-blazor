<!-- summary-of: Dashboard.razor -->
<!-- source-sha256: e8472aa2622b -->

# Dashboard.razor

The template's home page: whether it runs inside the Admin, what the sample proves, links to the sample
pages, and a real App Bridge session token minted on demand. Only the token's length is ever rendered.

`Components/Pages` · `@page "/"` · no parameters · injects `IAppBridgeInterop` as `Bridge`

## What it renders

Everything sits in one vertical `PolarisStack` at `PolarisGap.Base`, in four `PolarisSection`s:

| Section | Contents |
|---|---|
| "What this sample proves" | A `PolarisParagraph` describing the split (Polaris UI, Admin API through the server), then an inline stack of three `PolarisBadge`s: Success "Polaris components", Info "App Bridge 4", Neutral "Token exchange". |
| "Where it is running" | Three mutually exclusive states — `_checking` (a `PolarisSpinner` with `AccessibilityLabel="Checking the environment"` beside "Checking the environment…"), `_embedded == true` (Success banner "Embedded in the Shopify Admin"), and otherwise a Warning banner "Standalone browser session" explaining that App Bridge only activates inside the Admin while the elements are still genuine Polaris. |
| "Try it" | Primary link button to `/products`, and a link button to `/components` that also has `OnClick="ShowToastAsync"`. |
| "Request a fresh session token" | Explanation that tokens are short lived and minted on demand, a button "Mint a session token" with `Loading="_minting"`, and — once `_tokenSummary` is set — an Info banner "Token minted" carrying that text. |

## Code

| Member | What it does |
|---|---|
| `OnAfterRenderAsync(bool firstRender)` | First render only: `_embedded = await Bridge.IsAvailableAsync()`, `_checking = false`, then `StateHasChanged()`. Later renders return immediately — the probe never repeats. |
| `ShowToastAsync(PolarisEventArgs)` | `Bridge.ShowToastAsync("Hello from the Shopify Blazor Toolkit.")`; the args are ignored. |
| `MintTokenAsync(PolarisEventArgs)` | Sets `_minting`, then awaits `Bridge.WithLoadingAsync(() => Bridge.GetIdTokenAsync().AsTask())`. A null token sets "App Bridge could not mint a token here; open the app from inside the Admin."; otherwise the summary reports the token's **length** and that this page only ever sees the session token, never an Admin API access token. `_minting` is cleared in a `finally`. |

## Gotchas

- **The page is interactive only because `Routes.razor` applies `@rendermode InteractiveServer`.** A
  static page would render the "Checking the environment…" spinner for ever, because
  `OnAfterRenderAsync` never runs and `_checking` stays `true`.
- **The standalone branch is an `else`, so `null` and `false` are the same thing to it.** That is safe
  only because `_checking` and `_embedded` are assigned in the same pass — the `bool?` is never observed
  as null. Branching on `_embedded` anywhere else must test `is false`, or the page will claim it is
  outside the Admin while the answer is still unknown.
- **The "Show a toast" button is also a link to `/components`.** One click raises the toast *and*
  starts a browser navigation, so a toast that is never seen is the navigation winning the race, not App
  Bridge. Outside the Admin the toast call is a silent no-op.
- **The token is never written to the page.** Only `token.Length` is formatted into the banner, so the
  summary cannot leak a credential into the DOM or a log — keep it that way when editing the text.
- **The null-token message is the normal state outside the Admin**, not an error: `GetIdTokenAsync`
  returns null rather than throwing when App Bridge cannot provide one.
- **`WithLoadingAsync` drives the Admin's header progress bar** and clears it in a `finally`; outside the
  Admin it is a pass-through, so the button is the only feedback there. `Loading="_minting"` is a
  spinner, not a debounce — a second click while minting is possible.
- **The two link buttons have no template conditional**, so a project generated with
  `--include-admin-api-example false` (or `--include-sample-pages false`) still renders a link to
  `/products` (or `/components`), which lands on the router's NotFound page because the target file was
  excluded.

## See also

- [Products.razor.md](Products.razor.md) — where "Load products" goes, and the full request-state page.
- [Error.razor.md](Error.razor.md) — the page a failed request is routed to.
- [../Layout/MainLayout.razor.md](../Layout/MainLayout.razor.md) — the shell, and the other embedded check.
- [../App.razor.md](../App.razor.md) — the document that makes App Bridge exist before this page renders.
