<!-- summary-of: reconnect-modal.css -->
<!-- source-sha256: 12757ccd4fe6 -->

# reconnect-modal.css

The appearance and state machine of Blazor's connection-lost dialog. It styles five mutually
exclusive message states, a modal panel with a slide-up entrance, and the two-ring "rejoining"
spinner; it is deliberately plain `<dialog>` CSS rather than Polaris, because the dialog has to be
visible while the Blazor circuit — and therefore every Polaris control's state — is gone.

`wwwroot/css` · plain stylesheet, no custom properties · contract with `blazor.web.js`

## Classes and what they affect

`blazor.web.js` sets the state class on `#components-reconnect-modal` and the message markers are its
children, so nearly every rule is a descendant selector keyed on both. In the table, `…` stands for
`#components-reconnect-modal`.

| Selector | Effect |
|---|---|
| `.components-reconnect-first-attempt-visible`, `.components-reconnect-repeated-attempt-visible`, `.components-reconnect-failed-visible`, `.components-pause-visible`, `.components-resume-failed-visible`, `.components-rejoining-animation` | Hidden by default (`display: none`). This is the base state: nothing shows until a state class reveals it. |
| `#components-reconnect-modal.components-reconnect-show .components-reconnect-first-attempt-visible` and `… .components-rejoining-animation` | Shown while the first reconnection attempt runs. |
| `… .components-reconnect-paused .components-pause-visible` | Shown when the circuit was paused (the tab slept, for instance). |
| `… .components-reconnect-resume-failed .components-resume-failed-visible` | Shown when resuming a paused circuit failed. |
| `#components-reconnect-modal.components-reconnect-retrying` plus `… .components-reconnect-repeated-attempt-visible` and `… .components-rejoining-animation` | Shown on every later attempt: the panel itself, the "still trying" message, and the spinner. |
| `#components-reconnect-modal.components-reconnect-failed` plus `… .components-reconnect-failed-visible` | Shown when reconnection gave up. |
| `#components-reconnect-modal` | The box: white, `20rem` wide, `margin: 20vh auto`, `2rem` padding, no border, `0.5rem` radius, a soft shadow, and `opacity: 0` with `transition: display .5s allow-discrete, overlay .5s allow-discrete` plus the fade-out animation. |
| `#components-reconnect-modal[open]` | The entrance: slide up 1.5s on a springy curve after a 0.3s delay, fading in over 0.5s, both with `animation-fill-mode: both`. |
| `#components-reconnect-modal::backdrop` | Dims the page to `rgba(0,0,0,.4)` and fades in over 0.5s. |
| `.components-reconnect-container` | Flex column, centred, `1rem` gap — the stack every message sits in. |
| `#components-reconnect-modal p` | Zero margin, centred text. |
| `#components-reconnect-modal button`, `:hover`, `:active` | Flat button: no border, `#6b9ed2` background, white text, `4px 24px` padding, `4px` radius; hover darkens to `#3b6ea2`, active returns to the original blue. |
| `.components-rejoining-animation` and its `div`, `div:nth-child(2)` | An `80×80` positioning box; each child is an absolutely positioned `3px` `#0087ff` ring that scales from a point to the full box, the second offset by `-0.5s` so the pair pulses continuously. |

## Keyframes

- `components-reconnect-modal-slideUp` — `translateY(30px) scale(.95)` to `translateY(0)`.
- `components-reconnect-modal-fadeInOpacity` — opacity 0 to 1.
- `components-reconnect-modal-fadeOutOpacity` — opacity 1 to 0, which is why the panel is invisible
  until `[open]` overrides it.
- `components-rejoining-animation` — a ring starts at the `40px` centre with zero size and zero
  opacity, becomes opaque at 5%, and expands to the full `80px` box at 100% while fading out. The
  0–4.9% hold is what keeps the two rings from starting on top of each other.

## Gotchas

- **Everything is hidden until a state class says otherwise.** If the framework's class names change,
  every message stays at `display: none` and the dialog opens as an empty white panel with a backdrop,
  with no error anywhere. If the modal element loses its id, the script cannot find it at all and
  nothing opens.
- **`components-reconnect-retrying` and `components-reconnect-failed` are set on the modal itself, not
  only on a child.** That is why those two states have both a standalone rule (to display the panel)
  and a descendant rule (to display the message). Deleting the standalone rule leaves the retrying and
  failed states invisible even though the markers would match.
- Without this stylesheet the hidden-by-default rule is gone, so when the dialog opens all five
  messages are visible at once. The failure looks like a layout bug rather than a missing file.
- The colours are literals — `#6b9ed2`, `#3b6ea2`, `#0087ff`, `rgba(0,0,0,.4)` — and there are no
  custom properties to retheme. The panel will not follow the Admin's theme, which is accepted because
  it appears when the app is not talking to the admin anyway.
- The animations depend on the element being a `<dialog>` opened with `showModal()`: `::backdrop` and
  the `overlay`/`display` discrete transitions do nothing on a plain `<div>`.
- `.components-reconnect-paused` and `.components-reconnect-resume-failed` are also written by the
  page's script (it swaps one class for the other), so the two names are shared between the stylesheet
  and [reconnect-modal.js](../js/reconnect-modal.js.md).

## See also

- [reconnect-modal.js.md](../js/reconnect-modal.js.md) — the states and the retry/resume buttons these classes dress.
- [Products.razor.md](../../Components/Pages/Products.razor.md) — the page that becomes inert when the circuit drops.
