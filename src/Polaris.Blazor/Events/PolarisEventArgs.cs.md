<!-- summary-of: PolarisEventArgs.cs -->
<!-- source-sha256: 204706761538 -->

# PolarisEventArgs.cs

The payload every forwarded Polaris DOM event arrives in, normalised for .NET, plus the event-name
constants that keep a component and its test spelling the same string.

`Polaris.Blazor.Events` · two public types, no component · the args an `EventCallback<PolarisEventArgs>` receives

## Members

**`PolarisEventArgs`** — one DOM event, as deserialised from the interop layer. Each property carries the
JSON name the JavaScript sends.

| Member | JSON name | What it carries |
|---|---|---|
| `Type` | `type` | `string?` — the DOM event name: `click`, `input`, `change`, `dismiss`. Filled in by the dispatcher when the payload arrived without one. |
| `Value` | `value` | `string?` — the field value for form components. **Always a string**, even for a numeric field. |
| `Values` | `values` | `string[]?` — selected values for a multi-select such as `s-choice-list`. |
| `Checked` | `checked` | `bool?` — checked state for `s-checkbox` and `s-switch`. |
| `Detail` | `detail` | `Dictionary<string, object>?` — a custom event's `detail` payload, with non-serializable members removed. |
| `Key` | `key` | `string?` — the key pressed, for keyboard-driven events. |
| `AltKey` | `altKey` | `bool` — Alt was held. |
| `CtrlKey` | `ctrlKey` | `bool` — Ctrl was held. |
| `MetaKey` | `metaKey` | `bool` — the platform meta key was held. |
| `ShiftKey` | `shiftKey` | `bool` — Shift was held. |

**`PolarisEvents`** — `static class` of `const string` event names, so a component and its test agree on
spelling.

| Constant | Value | Raised by |
|---|---|---|
| `Click` | `"click"` | Buttons, links and clickable surfaces. |
| `Input` | `"input"` | Form fields, on every keystroke. |
| `Change` | `"change"` | A value committed — blur or Enter; immediately for checkbox and select. |
| `Focus` | `"focus"` | — |
| `Blur` | `"blur"` | — |
| `Dismiss` | `"dismiss"` | `s-banner`, when the merchant closes it. |
| `Show` | `"show"` | `s-modal` and `s-popover`. |
| `Hide` | `"hide"` | `s-modal` and `s-popover`. |
| `Remove` | `"remove"` | Removable chips. |
| `NextPage` | `"nextpage"` | A paginated `s-table`. |
| `PreviousPage` | `"previouspage"` | A paginated `s-table`. |
| `AfterHide` | `"afterhide"` | Once an overlay or dismissible element has finished hiding. |
| `Load` | `"load"` | `s-image`, `s-avatar`, `s-thumbnail` when the resource loads. |
| `Error` | `"error"` | The same three, when it fails to load. |
| `Invalid` | `"invalid"` | `s-date-field`, when the entered value is not a valid date. |
| `ViewChange` | `"viewchange"` | The date components, when the visible month changes. |
| `DropRejected` | `"droprejected"` | `s-drop-zone`, when files do not match `accept`. |

## Behaviour

- **The shape is the event's, not the component's.** `click` carries no value, `input` carries a string, and
  a custom event such as `dismiss` carries neither — which is why every member is nullable (or a plain
  `bool` that defaults to false).
- The type is a plain DTO: settable properties, a parameterless constructor, and nothing defensive. Its one
  piece of behaviour lives in the dispatcher, which substitutes the DOM event name into `Type` when the
  payload arrived without one.
- The constants are the DOM names verbatim, lower case, because they are passed straight to
  `addEventListener` on the element.

## Gotchas

- **A missing value and an empty string are indistinguishable through `Value`.** Both are null-or-"" at the
  far end; only `Detail` or a second event tells them apart.
- **The modifier keys are `bool`, not `bool?`.** "absent" and "false" look the same, which is correct for a
  modifier and misleading if read as an optional field.
- **`Values` is only populated by multi-select components.** A single-select reports `Value`; reading
  `Values` there yields null, not a one-element array.
- **`Value` is a string even for a number.** Converting it is the caller's job, and it must be done with
  the invariant culture or `"1,5"` depends on the server's locale.
- `Type` is the forwarded event's name, so it can disagree with the callback that was invoked only if
  JavaScript forwarded a different name than it registered; do not branch on it inside a specific handler.

## See also

- [../Interop/PolarisEventDispatcher.cs.md](../Interop/PolarisEventDispatcher.cs.md) — routes each payload to the registered
  callback and fills in `Type`.
- [../Interop/PolarisInterop.cs.md](../Interop/PolarisInterop.cs.md) — attaches the DOM listeners that
  produce these events.
- [../PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md) — where a wrapper maps DOM
  event names onto these callbacks.
