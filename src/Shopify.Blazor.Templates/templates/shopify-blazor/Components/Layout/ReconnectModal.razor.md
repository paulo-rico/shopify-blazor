<!-- summary-of: ReconnectModal.razor -->
<!-- source-sha256: c0541539e165 -->

# ReconnectModal.razor

The connection-lost dialog, as static markup that `blazor.web.js` drives by toggling classes on it. It
has no parameters, no `@code` block and no render mode, because it has to work precisely when no Blazor
circuit exists to render anything.

`Components/Layout` · static markup, no render mode · a contract with `blazor.web.js`, not a Polaris component

## What it renders

One `<dialog id="components-reconnect-modal" data-nosnippet>` holding a `.components-reconnect-container`:

| Element | Classes | Message |
|---|---|---|
| Spinner | `.components-rejoining-animation` (`aria-hidden="true"`), two empty child `div`s | The two pulsing rings, while an attempt is running. |
| First attempt | `p.components-reconnect-first-attempt-visible` | "Rejoining the server..." |
| Later attempts | `p.components-reconnect-repeated-attempt-visible` | "Rejoin failed... trying again in `<span id="components-seconds-to-next-attempt">` seconds." |
| Failure | `p.components-reconnect-failed-visible` | "Failed to rejoin." / "Please retry or reload the page." |
| Retry | `button#components-reconnect-button.components-reconnect-failed-visible` | "Retry" — the button `reconnect-modal.js` binds to `Blazor.reconnect()`. |
| Paused | `p.components-pause-visible` | "The session has been paused by the server." |
| Resume failed | `p.components-resume-failed-visible` | "Failed to resume the session." / "Please retry or reload the page." |
| Resume | `button#components-resume-button.components-pause-visible.components-resume-failed-visible` | "Resume" — bound to `Blazor.resumeCircuit()`. |

Every one of those is `display: none` until the framework's state class on the dialog reveals it; the
visibility rules live in `wwwroot/css/reconnect-modal.css`.

## Gotchas

- **The ids and class names are the contract, not styling hooks.** `blazor.web.js` toggles
  `components-reconnect-*` on the element whose id is `components-reconnect-modal` and fires
  `components-reconnect-state-changed` on it. Renaming either leaves the dialog either unable to open
  (wrong id) or open and empty (wrong state classes), with nothing logged.
- **The markup is static on purpose.** A component that needs a circuit cannot report a missing
  circuit, so adding a render mode here would make the dialog disappear exactly when it is needed.
- **It must stay outside `<Routes />`** (it is, in `App.razor`): inside the router it is re-rendered by
  the thing that failed.
- **The two buttons appear only in their own states.** Retry carries only
  `components-reconnect-failed-visible` and Resume only the pause/resume-failed pair, so a state without
  a button has no action to offer — the merchant reloads manually. That is the framework's design, not
  an omission here.
- **The script and stylesheet have to follow it.** `js/reconnect-modal.js` looks its three elements up
  at parse time with no null checks, and `css/reconnect-modal.css` supplies the hidden-by-default
  rules; without the stylesheet every message is visible at once when the dialog opens.
- `data-nosnippet` keeps the text out of search-result snippets; it changes nothing at runtime.
- This lives in `Components/Layout` but is not a `LayoutComponentBase` — the folder is its only
  connection to the layout system.

## See also

- [../../wwwroot/js/reconnect-modal.js.md](../../wwwroot/js/reconnect-modal.js.md) — the handlers that open, close and retry this dialog.
- [../../wwwroot/css/reconnect-modal.css.md](../../wwwroot/css/reconnect-modal.css.md) — the state classes that decide which message is visible.
- [../App.razor.md](../App.razor.md) — where this markup, its stylesheet and its script are loaded.
- [MainLayout.razor.md](MainLayout.razor.md) — the shell the routed page renders in while this dialog is due.
