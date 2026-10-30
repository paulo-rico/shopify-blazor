<!-- summary-of: ShopifySaveBar.cs -->
<!-- source-sha256: a4140511b008 -->

# ShopifySaveBar.cs

Shopify's contextual save bar as a Blazor component: while a form has unsaved changes it draws
"Unsaved changes" with **Discard** and **Save** across the admin's top toolbar — the same bar the
admin's own product editor shows — and reports the two presses as ordinary Blazor callbacks.

`Shopify.Blazor.Components` · renders `<ui-save-bar>` plus two native `<button>`s · implements `IAsyncDisposable`

It is an **App Bridge** component, not a Polaris one. The bar belongs to the chrome around the app,
so no markup in the page can be the bar: the component renders the `<ui-save-bar>` element App Bridge
reads, then asks App Bridge to show or hide it. Outside the Admin the element is not rendered at all
and the asks are never made, which is why a page keeps its own Save control as the standalone fallback.

## Parameters

| Parameter | Type | Default | What it does |
|---|---|---|---|
| `Id` | `string?` | `null` | The element's `id`, which is how App Bridge names the bar. A unique id is generated when null, and that is the normal case — the id is an implementation detail of the interop call. |
| `Visible` | `bool` | `false` | Whether the form has unsaved changes. Each change of this value sends exactly one show or hide. |
| `Saving` | `bool` | `false` | Marks the Save button `loading` (a bare attribute, which is what App Bridge reads) and disables Discard. |
| `DiscardConfirmation` | `bool` | `false` | Adds App Bridge's own confirm-before-discard dialog. The app never sees the answer. |
| `SaveLabel` | `string` | `"Save"` | The primary button's text. |
| `DiscardLabel` | `string` | `"Discard"` | The second button's text. |
| `OnSave` | `EventCallback` | — | Raised when the merchant presses Save in the bar. The bar stays visible until `Visible` becomes false. |
| `OnDiscard` | `EventCallback` | — | Raised when the merchant presses Discard. The page does the discarding; the bar resets nothing. |

Injected: `IAppBridgeInterop AppBridge`.

## Behaviour

- **The markup is a carrier, not the bar.** `<ui-save-bar>` renders nothing on its own; App Bridge
  reads its `id`, its `discardConfirmation` attribute and its two children, and draws the bar in the
  admin. The primary action is the child carrying `variant="primary"`; the other child is Discard.
- **Nothing is rendered until App Bridge says it is there.** Availability is asked once in
  `OnAfterRenderAsync` — not in `OnInitializedAsync`, where JS interop is unavailable during
  prerendering and the interop layer answers false for ever — and the element and its buttons are
  drawn only after a true answer, one render later. A false answer draws nothing at all.
- **Visibility is the caller's decision.** There is no form scanning here. `OnAfterRenderAsync`
  compares `Visible` with what it last sent and calls `ShowSaveBarAsync` or `HideSaveBarAsync` once
  per change — never once per render, because the component re-renders whenever its parent does.
- **The buttons are native and stay in this document.** App Bridge forwards a click by calling
  `click()` on the light-DOM button, so `OnSave` and `OnDiscard` are ordinary Blazor event handlers.
  They are deliberately not `s-button`s: App Bridge derives the bar's two actions from the element's
  own children.
- **Discard is refused while `Saving`.** The button is disabled in the markup and the handler returns
  without raising `OnDiscard` as well, because a discard that raced a save would load the stored row
  over a write still in flight.
- **The bar is hidden on teardown.** `DisposeAsync` hides a bar it showed, because the bar is the
  admin's and survives the component that asked for it.

## Gotchas

- **A page that never flips `Visible` gets no bar and no error.** Nothing validates that the parameter
  is ever true, and there is no fallback rendering, so the only symptom is the absence of the bar.
- **Outside the Admin nothing is rendered at all, and no call is made.** That is the intended
  degradation (`window.shopify` does not exist there), so a page must keep a Save control of its own.
  The render gate is not cosmetic: App Bridge defines `ui-save-bar` only once it has connected to the
  admin, and an undefined element renders no shadow root — so its two buttons would appear as ordinary
  buttons at the top of the page, with nothing in the console to explain them. The component does not
  render an inline substitute.
- **`Discard` is an intent, not an undo.** Nothing in the bar resets a control: the page's values are
  page state, and the only honest discard is the page reloading what it loaded. A page that raises
  nothing on `OnDiscard` hides the bar on the next render while leaving the edits in place.
- **`Saving` is not the same as `Visible`.** The bar stays up while a save is in flight — it is the
  bar's own indicator — and it is the caller's reload that moves the baseline and hides it.
- **The bar belongs to the document, not the page.** It is found by `id` through the whole document,
  so two instances need distinct ids; the generated id makes that automatic, and the last one asked
  for wins.
- **The element is schema-checked, so its children carry nothing extra.** App Bridge validates the
  `ui-save-bar` subtree against its own schema: `button` children, and only the attributes `variant`
  (the single value `primary`), `disabled` and `loading`. Everything else — including `type="button"`,
  which is ordinary markup everywhere else — is logged to the console as an unexpected attribute. That
  is why the buttons are written without one, and why a future parameter must not be forwarded as an
  attribute without checking the schema first.
- **App Bridge swallows a missing element.** `shopify.saveBar.show(id)` throws internally when the id
  is not in the DOM, and the interop turns that into `false`. A wrong id is therefore silent.
- **This cannot be exercised outside the Admin.** The element and both buttons render in a component
  test, but the bar itself is drawn by App Bridge, so its appearance and placement are untested here.

## See also

- [../AppBridge/AppBridgeInterop.cs.md](../AppBridge/AppBridgeInterop.cs.md) — `ShowSaveBarAsync` and
  `HideSaveBarAsync`, the two calls this component makes.
- [../wwwroot/app-bridge.js.md](../wwwroot/app-bridge.js.md) — the JS exports behind them, and
  the guard that returns false outside the Admin.
- [ShopifyAppBridgeHead.cs.md](ShopifyAppBridgeHead.cs.md) — loads the App Bridge script that defines
  the element and the API.
- [ShopifyApp.cs.md](ShopifyApp.cs.md) — the cascaded `IsEmbedded`, for a page that wants to explain
  the missing bar rather than only keep its own Save button.
