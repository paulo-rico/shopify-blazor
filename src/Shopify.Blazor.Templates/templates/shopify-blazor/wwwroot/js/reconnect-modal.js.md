<!-- summary-of: reconnect-modal.js -->
<!-- source-sha256: 51c21b215357 -->

# reconnect-modal.js

Turns a lost Blazor circuit into a dialog the merchant can act on. `blazor.web.js` fires
`components-reconnect-state-changed` on `#components-reconnect-modal` with a `detail.state` of
`"show"`, `"hide"`, `"failed"` or `"rejected"`; this file maps each state onto the dialog and wires its
two buttons to `Blazor.reconnect()` and `Blazor.resumeCircuit()`.

`wwwroot/js` · classic script, **no exports** · must load after the modal markup

## What it does

There are no exported functions — this is a plain script, not an ES module, and all wiring is done by
`addEventListener` at load time. Its handlers:

| Function | Arguments | Behaviour |
|---|---|---|
| `handleReconnectStateChanged` | `event` — the framework's `CustomEvent` | `"show"` calls `reconnectModal.showModal()`; `"hide"` calls `close()`; `"failed"` registers `visibilitychange` so a background tab retries when it is looked at again; `"rejected"` calls `location.reload()` immediately. |
| `retry` | none | Removes the `visibilitychange` listener, then awaits `Blazor.reconnect()`. `false` means the server was reached but the circuit is gone, so it tries `Blazor.resumeCircuit()` and reloads if that also fails, otherwise closes the dialog. `true` needs no action here. A thrown error means the server is unreachable, so it re-registers the `visibilitychange` listener and waits. |
| `resume` | none | Awaits `Blazor.resumeCircuit()`; a falsy result reloads the page. If it throws, it swaps the modal's `components-reconnect-paused` class for `components-reconnect-resume-failed`, which the stylesheet renders as a "could not resume" message. |
| `retryWhenDocumentBecomesVisible` | none | Does nothing unless `document.visibilityState === "visible"`, then awaits `retry()`. This is the retry that a backgrounded tab performs on return. |

The three elements it binds are looked up by id at load: `#components-reconnect-modal`,
`#components-reconnect-button` (retry) and `#components-resume-button` (resume). Everything else —
which message is visible, the spinner, the panel animation — is CSS driven by the state classes
documented in [reconnect-modal.css](../css/reconnect-modal.css.md).

## Gotchas

- **It is not a module, and it must not become one.** It is written the way the SDK's own Blazor Web
  App template writes it: no exports, all wiring by `addEventListener`, and the global `Blazor` object
  that `blazor.web.js` installs. That global is only dereferenced inside the handlers, so it needs to
  exist by the time a state event fires — but the three element lookups happen at parse time, which is
  why the load order in the next point matters.
- **The element lookups run at script parse time, with no null checks.** The `<script>` tag must come
  after the modal markup (or be `defer`red), otherwise the first `getElementById` returns `null` and
  the next line throws, leaving the dialog permanently non-functional with a console error only.
- **`"rejected"` gives no message and no choice** — it reloads the page at once. There is no CSS rule
  for it, by design: a rejected circuit cannot be resumed.
- **The retry button's success path is silent.** When `Blazor.reconnect()` returns `true` nothing is
  called, because closing the dialog is left to the framework's own `hide` state; only the resume path
  closes it explicitly. A dialog that stays up after a successful reconnect is this assumption being
  wrong, not a stuck request.
- **The `visibilitychange` listener is removed at the start of every retry and re-added only when the
  attempt throws.** That is why a merchant who clicks Retry while offline does not accumulate
  listeners, and why the tab-visible retry survives after a failed attempt.
- `resume`'s catch only rewrites a class — it never reloads and never closes. The dialog stays open
  showing the resume-failed message, and nothing in this script retries from there.

## See also

- [reconnect-modal.css.md](../css/reconnect-modal.css.md) — the state classes these handlers switch on.
- [shopify-fetch.js.md](shopify-fetch.js.md) — the other browser script the sample ships.
- [Products.razor.md](../../Components/Pages/Products.razor.md) — a page that looks alive but is inert while this dialog is due.
