<!-- summary-of: PolarisAddableSelect.cs -->
<!-- source-sha256: c7fec92d049f -->

# PolarisAddableSelect.cs

A dropdown of the saved values of a directory — a supplier, a warehouse, a courier — with an add
button beside it that opens a dialog holding the values already saved and a field for a new one. It is
deliberately the plainest of the toolkit's three add-a-value shapes: choosing is a real `s-select`, so
keyboard and screen-reader behaviour is native, and adding is one explicit act rather than something
inferred from typed text. The component never persists anything — `OptionAdded` is the caller's cue to
save, and the caller must also add the value to `Options`.

`Polaris.Blazor.Components` · composes `s-grid` + `s-select` + `s-modal` · base `ComponentBase`

## Parameters

| Parameter | Type | Default | What it does |
|---|---|---|---|
| `Label` | `string?` | `null` | Accessible label passed straight to the select. |
| `Details` | `string?` | `null` | Help text below the select. |
| `EmptyOptionLabel` | `string?` | `null` | Label of the "nothing chosen" row, and the only way to clear a value once one is set. **Null omits the row entirely**, so a value can then be set but never unset. |
| `Value` | `string?` | `null` | The chosen value; null means nothing is chosen. The caller owns it — the component only reports changes through `ValueChanged`. |
| `Options` | `IReadOnlyList<string>` | `[]` | The values already saved, offered as `s-option`s in this order. Also the duplicate-check set and the source of the dialog's built-in list. |
| `Disabled` | `bool` | `false` | Disables the select **and** the add button, so the dialog cannot be opened at all. It does not reach the dialog's own OK and Cancel. |
| `DialogButtonLabel` | `string?` | `"Add new"` | Text of the add button. Named for what it opens, because the dialog may add or manage. |
| `DialogHeading` | `string?` | `"Add a new value"` | The dialog's heading, which doubles as its accessible name. |
| `DialogListLabel` | `string?` | `"Already saved"` | Caption above the built-in list. Ignored when `DialogContent` is supplied. |
| `DialogEmptyLabel` | `string?` | `"Nothing has been saved yet."` | Drawn in place of the built-in list when `Options` is empty. Same `DialogContent` condition. |
| `DialogFieldLabel` | `string?` | `"New value"` | Label of the dialog's text field. |
| `DialogFieldDetails` | `string?` | `null` | Help text below that field. |
| `DialogConfirmLabel` | `string?` | `"OK"` | Text of the `primary-action` button. |
| `DialogContent` | `RenderFragment?` | `null` | Caller content at the **top of the dialog body** — an editable grid of saved values, typically. Replaces the caption and the built-in list, which are then not drawn at all. A fragment, not a slot, because the modal body is an ordinary container. |
| `CancelLabel` | `string?` | `"Cancel"` | Text of the `secondary-actions` button. |
| `BlankMessage` | `string?` | `"Enter a value first."` | Error shown on the field when confirm is pressed with nothing typed. |
| `ValueChanged` | `EventCallback<string?>` | — | The selection changed, or the dialog settled on a value. Null means cleared. Pairs with `Value` for `@bind-Value`. |
| `OptionAdded` | `EventCallback<string>` | — | The dialog confirmed a value **not** already in `Options`. Carries the trimmed text. The caller persists it and adds it to its list; the component does not. |
| `DuplicateIgnored` | `EventCallback<string>` | — | The dialog was given a value already in `Options`. Carries the existing spelling. Wire it if the merchant should be told why the list did not grow. |
| `Id` | `string?` | `null` | The dialog's element id, which the add button's `commandFor` addresses. Generated as `polaris-addable-select-{guid}` on first initialisation; a non-empty value supplied later replaces it. |

## Events

| Callback | Args | Raised when |
|---|---|---|
| `ValueChanged` | `string?` — null for "nothing chosen" | The select changes, or the dialog settles on a value (new or duplicate). |
| `OptionAdded` | `string` — the trimmed new value | Confirm pressed with text that no option matches. Raised **before** `ValueChanged`, so a handler that saves and re-queries has done so by the time the select is told what to show. |
| `DuplicateIgnored` | `string` — the existing option's own spelling | Confirm pressed with text that matches an option after trimming and case-folding. Raised only when a delegate is attached. |

## Behaviour

- **The layout** is a two-column `s-grid` (`1fr auto`, gap `base`, `align-items: end`) so the add
  button sits on the select's last line rather than level with its caption. The button carries the
  `plus` icon, `CommandFor` the dialog id and `Command.Show`.
- **Opening is declarative and closing is code.** A modal can only be opened by a user gesture, and
  `commandFor` on a button is the one sanctioned form — the press *is* the open. Saving is not a
  gesture, so it closes through the dialog's own `HideAsync()` (`hideOverlay()` in the element).
  Cancel is declarative too: `commandFor` the dialog with `Command.Hide`.
- **The dialog body** is an `s-stack` (gap `base`) containing, in order: either `DialogContent` or the
  caption plus a read-only `s-unordered-list` of `Options` (or the empty label when there are none);
  then the `s-text-field`. There is deliberately no second chooser inside the dialog — the select
  behind it is where a value is chosen.
- **Confirm decides in one of three ways**, after trimming: empty → `BlankMessage` becomes the
  field's error and the dialog stays open; an option matches (compared after `Trim`, with
  `OrdinalIgnoreCase`) → the dialog closes, `ValueChanged(existing)` fires, then `DuplicateIgnored`;
  otherwise → `OptionAdded(trimmed)`, then `ValueChanged(trimmed)`, then close.
- **A reopened dialog starts clean.** `OnHide` — raised by Cancel, Escape, backdrop click and
  `HideAsync` alike — clears the typed text and any error.
- **The select is handed a sentinel, not null, when nothing is chosen** (only when `EmptyOptionLabel`
  is set). Its own state then reads "no value" instead of going blank, and the visible row is the
  "none" option. `SelectChangedAsync` maps the sentinel — and any blank value — back to `null`, so the
  rest of the app keeps one spelling for "nothing chosen".
- **`EmptyOptionValue`** is the public `const string` (`"__polaris-addable-select:none__"`) that
  option carries. It is public so a caller can compare against it, not so it can be used as a value.

## Gotchas

- **An `s-option` with an empty `value` reports its own label.** Measured in a browser: a "No
  supplier" row with `value=""` handed the caller the literal string `"No supplier"`. That is why the
  sentinel exists; do not "tidy" it back to an empty string, and do not compare the select's reported
  value to `""` — compare to `null` after `ValueChanged`.
- **OK and Cancel must remain direct children of the modal.** Polaris applies a `slot` attribute only
  to the host's own children, so a button nested inside the dialog body's stack silently loses its
  slot and the dialog renders with **no actions at all**, with nothing logged. A test asserts the
  actions are not nested in the body.
- **The component reference capture comes after every attribute.** `AddComponentReferenceCapture` is
  a frame of its own; an `AddAttribute` issued after it throws *at render time* — `Attributes may only
  be added immediately after frames of type Element or Component`.
- **A new value only appears in the select if the caller adds it to `Options`.** The order is
  additive-first for exactly that reason: `OptionAdded` is awaited, the caller re-renders with the
  enlarged list, and only then is `ValueChanged` raised. A handler that saves but does not extend its
  list leaves the select holding a value with no matching option.
- **`Disabled` does not disable the dialog's buttons** — it stops the dialog opening, which is the
  only path to them. A caller that opens a programmatically-shown modal has no guard here.
- **The error clears on the next keystroke**, not on confirm: `OnInput` and `OnChange` share one
  handler that also clears `_error`, so the complaint never survives an edit. Both are wired because a
  paste and a typed character do not necessarily raise the same event.
- **`DialogListLabel` and `DialogEmptyLabel` are dead when `DialogContent` is set.** Passing content
  and expecting the caption too is a silent no-op, by design: the caller has already drawn that list.

## See also

- [PolarisSelect.cs.md](PolarisSelect.cs.md) — the dropdown this composes, and the source of the
  reported `value` on change.
- [PolarisModal.cs.md](PolarisModal.cs.md) — `commandFor` to open, `HideAsync`, and the `OnHide` this
  uses to reset.
- [PolarisTextField.cs.md](PolarisTextField.cs.md) — the dialog's field, including the `Error` the
  blank message lands in.
- [PolarisSuggestField.cs.md](PolarisSuggestField.cs.md) and
  [PolarisSuggestFieldInline.cs.md](PolarisSuggestFieldInline.cs.md) — the other two add-a-value
  shapes, where typing drives the list instead of a dialog.
