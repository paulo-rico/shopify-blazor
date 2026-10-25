<!-- summary-of: ShopifyApp.cs -->
<!-- source-sha256: 4afc7f46841e -->

# ShopifyApp.cs

Cascades App Bridge to a subtree and reports whether the page is actually inside the Shopify Admin.
The same URL serves the embedded app and a standalone browser tab, and `window.shopify` exists only
inside the Admin, so every App Bridge call is guarded and `IsEmbedded` is what a page uses to explain
the difference instead of rendering an empty shell.

`Shopify.Blazor.Components` · cascades itself as `CascadingValue<ShopifyApp>` · base `ComponentBase`

## Parameters

| Parameter | Type | Default | What it does |
|---|---|---|---|
| `ChildContent` | `RenderFragment?` | `null` | The subtree that can consume this component through `[CascadingParameter] ShopifyApp`. Rendered inside a `CascadingValue` whose `IsFixed` is `true`. |

## Members

| Member | Signature | What it does |
|---|---|---|
| `AppBridge` | `[Inject] IAppBridgeInterop` | The interop service every call is forwarded to. |
| `IsEmbedded` | `bool?` | `true` inside the Admin, `false` outside it, `null` while that is undetermined — the state during prerendering. |
| `GetIdTokenAsync` | `ValueTask<string?>` | Mints a fresh session token for a call to this app's own backend. |
| `FetchAsync<T>` | `ValueTask<AppBridgeFetchResult<T>> FetchAsync<T>(string url, string method = "GET", object? body = null)` | Calls one of this app's endpoints with a freshly minted token. |
| `ToastAsync` | `ValueTask ToastAsync(string message, bool isError = false)` | Shows an Admin toast. A no-op outside the Admin. |
| `WithLoadingAsync<T>` | `Task<T> WithLoadingAsync<T>(Func<Task<T>> action)` | Runs work with the Admin's header progress bar shown and reliably cleared, including when the action throws. |

## Behaviour

`OnAfterRenderAsync` runs its probe on the **first render only**: it asks `AppBridge.IsAvailableAsync()`
once and, if the answer differs from the cached value, stores it and calls `StateHasChanged()`. Nothing
re-probes on later renders, so a page that becomes embedded later in its lifetime will keep saying it
is not.

The rendered tree is a single `CascadingValue<ShopifyApp>` with `Value` set to this instance and
`IsFixed` set to `true`, wrapping `ChildContent`. Consumers therefore receive the component itself and
call the methods above directly; there is no separate injectable facade for the embedded state.

## Gotchas

- **`null` is not "not embedded".** `IsEmbedded` is `bool?`, and `null` means "not yet known" — which
  is exactly what it is during prerendering, because no browser call can run there. Test
  `IsEmbedded is false` for the standalone case; treating `null` as false renders the "you are not in
  the Admin" branch on the server and then contradicts itself on the first interactive render.
- **The probe happens after the first render, not during it.** A page that branches on `IsEmbedded`
  in its initial markup is rendering before the answer exists, which is why the value has to be
  nullable in the first place.
- **`IsFixed="true"` means the cascaded value never changes identity.** `ShopifyApp` is a stable
  instance for the lifetime of the subtree, so there is no cascading-parameter update to observe when
  the flag flips; the re-render that shows the new value comes from this component's own
  `StateHasChanged`.
- **Outside the Admin, the methods are no-ops rather than errors.** `ToastAsync` does nothing and the
  interop's guarded calls return their failure shape, so a page that ignores `IsEmbedded` fails
  silently — no exception, no console message.

## See also

- [ShopifyAppBridgeHead.cs.md](ShopifyAppBridgeHead.cs.md) — the head tags that make App Bridge exist at all.
- [AppBridgeInterop.cs.md](../AppBridge/AppBridgeInterop.cs.md) — what the forwarded calls actually do.
- [ShopifySessionTokenAuthenticationHandler.cs.md](../Authentication/ShopifySessionTokenAuthenticationHandler.cs.md) — the server side of the token these calls carry.
