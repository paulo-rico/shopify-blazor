<!-- summary-of: PolarisAppWindow.cs -->
<!-- source-sha256: bb5acc00ebb6 -->

# PolarisAppWindow.cs

A fullscreen window hosted by the Shopify admin that loads one of the app's own routes — an App Bridge element for a workflow that genuinely needs the whole screen.

`Polaris.Blazor.Components` · renders `<s-app-window>` (App Bridge, not Polaris) · base `PolarisElementComponentBase`

## Parameters

| Parameter | Type | Default | What it does |
|---|---|---|---|
| `Src` | `string?` | `null` | The route inside this app that the window displays, as a relative path such as `/products/select`. It is loaded in an iframe, so it must be a route this app serves; a `null` value drops the attribute and the window has nothing to show. |

Inherited: `Id`, `AccessibilityLabel`, `ItemTag`, `Slot` and `AdditionalAttributes` (see
[PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md)). The element has no `ChildContent`; its content is the route named by `Src`.

## Events

| Callback | Args | Raised when |
|---|---|---|
| `OnShow` | `EventCallback<PolarisEventArgs>` | The window opens. |
| `OnHide` | `EventCallback<PolarisEventArgs>` | The window closes — including when the merchant leaves through the admin's own top bar. |

## Members

| Signature | What it does |
|---|---|
| `ValueTask ShowAsync()` | Asks the element to show itself with the content at `Src`, through interop (`show`). Completes when the request has been sent, not when the window is visible. |
| `ValueTask HideAsync()` | Asks the element to close (`hide`). |
| `ValueTask ToggleAsync()` | Opens when closed and closes when open (`toggle`). |

## Behaviour

- **The window belongs to the admin.** The content named by `Src` is loaded in an iframe covering the screen, with a top bar the admin controls so a merchant can always leave. That top bar is the reason a fullscreen window cannot trap someone.
- **Prefer `PolarisModal` for an ordinary dialog.** A Polaris modal decides for itself whether to render inside the app or as a fullscreen overlay and needs no App Bridge API; this element is for the cases where that is not enough — a multi-step editor, a bulk editor, a long form.
- **There are two ways to open it, and they are not interchangeable.** Declaratively, give the window an `Id` and point a button at it with `command="--show"` and `commandFor="that-id"`; or from C# with `ShowAsync`, `HideAsync` and `ToggleAsync`, which reach the element's own methods through interop.
- **A save bar for a form inside the window must be on that form** (`data-save-bar`), not on the parent page. A save bar on the parent belongs to the page and renders behind the window, where Save and Discard cannot be clicked.
- `Src` is written as an attribute on every render; it is not a one-time navigation.

## Gotchas

- **The wrapper does not generate an `Id` for this element** (it does not set `RequiresId`), so declarative opening requires you to supply one. Without it, `commandFor` addresses nothing and the button does nothing, silently.
- **The trigger button's `Command` default is `Auto`, so spell out `--show`.** `commandFor` alone is not enough; the command decides whether the target is shown, hidden or toggled.
- **`Src` is loaded in an iframe**, so an external URL is not a shortcut for embedding another site — this element is for a route the app itself serves.
- **Call the imperative methods only after the first render.** The element reference is invalid before then, and the interop call has no element to act on.
- **The unsaved-changes guard belongs to closing, not showing.** The XML remark attached to `ShowAsync` talks about changes being confirmed "before it closes"; what it describes is the save-bar prompt on the close path, so do not expect opening to prompt.
- **Outside the admin there is no host to open the window in.** The element is part of the admin's chrome, so a standalone browser session gets no window however the methods are called.

## See also

- [PolarisButton.cs.md](PolarisButton.cs.md) — its `CommandFor` and `Command` are the declarative way to open this window.
- [PolarisAppNav.cs.md](PolarisAppNav.cs.md) — the other App Bridge element, for the admin's navigation menu.
- [PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md) — the inherited parameters and the element reference the imperative methods use.

`PolarisModal` is the lighter choice for an ordinary dialog; its summary is written alongside the modal's own source file.
