<!-- summary-of: PolarisAutocomplete.cs -->
<!-- source-sha256: 5fd875384eae -->

# PolarisAutocomplete.cs

A text field that suggests the items matching what has been typed and reports the item the merchant
chooses. It is the generic, single-selection autocomplete: local filtering by default, an optional
asynchronous provider, custom row rendering, and a keyboard path that moves real focus onto the rows
rather than inventing a combobox ARIA state this layer cannot write.

`Polaris.Blazor.Components` · composed component, base `ComponentBase` (there is no `s-autocomplete`) · renders a `PolarisStack` holding an `s-text-field` and, when open, an `s-box` of `s-button` rows

## Parameters

| Parameter | Type | Default | What it does |
|---|---|---|---|
| `Items` | `IReadOnlyList<TItem>` | `[]` | The whole list to filter, in the caller's order. Ignored while `SearchFunction` is set. Must not be null. |
| `Value` | `TItem?` | `null` | The chosen item, owned by the caller. Adopted when the *parameter* changes — see Behaviour. |
| `ValueChanged` | `EventCallback<TItem?>` | unset | The chosen item, or `default` when the field is cleared. Raised only for a selection, never for typing. |
| `TextSelector` | `Func<TItem, string>` | `item.ToString()` | The text shown for an item and matched against the term. Set it: for a record the default prints the whole record. |
| `FilterFunction` | `Func<TItem, string, bool>?` | null | Replaces the built-in contains rule. Receives the trimmed term. |
| `SearchFunction` | `Func<string, CancellationToken, Task<IReadOnlyList<TItem>>>?` | null | An asynchronous provider. When set it owns the list and `Items` is not filtered on top of it. |
| `ItemTemplate` | `RenderFragment<TItem>?` | null | Replaces a row's bare text. Must be inline content — the row is a button. |
| `Label` | `string?` | null | The field's visible label; the field is self-labelling, so do not add a sibling label. |
| `Details` | `string?` | null | Help text below the field. |
| `Placeholder` | `string?` | null | Placeholder while the field is empty. |
| `MinimumLength` | `int` | `1` | How many characters open the list. It gates **opening**, not filtering: an explicitly opened list (ArrowDown) still shows the matches, and a blank term matches everything. |
| `MaxSuggestions` | `int?` | `10` | How many rows are drawn. Null draws every match. The cap is applied after filtering. |
| `DebounceMilliseconds` | `int` | `250` | How long `SearchFunction` waits after the last keystroke. Zero asks immediately. No effect on local filtering. |
| `AllowCustomValue` | `bool` | `false` | Accepts text that is not an item, reporting it through `CustomValueEntered` instead of putting the chosen item's text back. |
| `CustomValueEntered` | `EventCallback<string>` | unset | The trimmed text of a commit that matched nothing, when `AllowCustomValue` is set. |
| `Disabled` | `bool` | `false` | Disables the field and every row, and stops the handlers acting on events that arrive anyway. |
| `ReadOnly` | `bool` | `false` | Shows the chosen text without allowing edits or the list. |
| `NoResultsContent` | `RenderFragment?` | null | Drawn in the panel when nothing matches. Null draws nothing, which reads as a control that failed to load. |
| `LoadingLabel` | `string?` | `"Searching…"` | The sentence beside the spinner while an asynchronous search runs. |

No inherited parameters: this class derives from `ComponentBase`, so there is no `Id`, `Slot`,
`ItemTag`, `AccessibilityLabel` or `AdditionalAttributes`, and no single element to address with
`commandFor`.

## Events

| Callback | Args | Raised when |
|---|---|---|
| `ValueChanged` | `EventCallback<TItem?>` | A row is clicked, tapped, activated with Enter/Space, or taken by Enter or an exact-match commit in the field; and with `default` when the text is cleared and committed. |
| `CustomValueEntered` | `EventCallback<string>` | A commit finds no item, the text is not blank and `AllowCustomValue` is set. The argument is trimmed. |

Typing raises neither. A half-typed term is not a value, which is the separation the whole component is
built around.

## Members

| Member | Signature | What it does |
|---|---|---|
| `Matches` | `static IReadOnlyList<TItem> Matches(IReadOnlyList<TItem> items, string? query, Func<TItem, string> textSelector, Func<TItem, string, bool>? filter = null)` | The matching items in the given order: everything for a blank or whitespace term, otherwise the items the filter accepts (the default is `Contains`, `OrdinalIgnoreCase`, on the trimmed term). Throws `ArgumentNullException` for a null list or selector. Public so a page can fill a list by exactly the rule the field filters it by. |

## Behaviour

**The value is adopted when the parameter changes, not when it differs from what was typed.** The
component keeps its own `_query` and copies `Value`'s text into it only when the incoming parameter
differs from the previous pass. A caller that applies a selection lazily re-renders with its previous
value while a new one is in flight; treating that echo as an instruction resets the field mid-typing, the
failure the suggest field and the range slider both paid for. Typing never raises `ValueChanged`.

**The list is drawn inline.** Polaris cannot open a popover from code — `s-popover` has no methods at all,
measured in Chrome — and a text field's documented properties offer nothing to anchor one to (no
`interestFor`, no `commandFor`, no slots; measured on the search field, which is the closest sibling).
The panel is the sibling suggest field's shape: a bordered `s-box` under the field, never a floating layer
that an ancestor's overflow or a stacking context can swallow.

**The field is an `s-text-field`, not an `s-search-field`.** A browser clears an
`<input type="search">` when Escape is pressed; in Chrome that arrived as an `input` event *after* the
component had restored the chosen text on Escape, leaving the field blank with the value still set. The
text field has no such default action, and its `Icon="search"` keeps the magnifier. This was found by the
browser check, not by a component test — the markup was identical either way.

**The keyboard is real focus, not a highlight.** ArrowDown/ArrowUp in the field open the list and move
focus onto the first/last row; on a row they move to the next/previous one, and ArrowUp on the first row
returns to the field. Enter and Space on a row are the browser's own click on a real button. Escape closes
the list, puts the chosen item's text back and returns focus to the field. There is deliberately no
`aria-activedescendant`: it would have to be written onto the input inside the search field's shadow root,
which no light-DOM attribute reaches, so the alternative was invented ARIA — a real focus ring and a
screen reader announcing the row's own text is the honest version. The price is that typing does not
reach the field while a row is focused; Escape takes the merchant back.

**A row is chosen on `mousedown`, and again on `click` — and Enter and Space are handled on the row's
keydown.** The mousedown precedes the focus change, so the choice is reported before the field's blur can
close the panel and remove the button the merchant is pressing. The click handler is the browser's own
activation of a focused row button; the keydown handler is there because a synthetic Enter does not always
produce that click, and one keyboard path should not depend on it. All three end in `SelectAsync`, which
refuses a row while the panel is shut — that is what makes the second and third calls no-ops instead of
duplicate reports. A *commit* passes `fromField: true` and is allowed to choose with the panel already
closed, which is the case when the merchant blurs over an exact name.

**Blur closes the list after 150 ms, and focus inside the component cancels it.** An immediate close would
remove the row the browser is about to focus on the way from the field into the list, which is the classic
"the arrow key does nothing" fault. Clicking away therefore closes after a delay short enough to read as
immediate, while a genuine click on a row has already selected before it can matter.

**The blur that moves focus onto a row is not a commit.** The browser reports the field's `change` as
focus leaves it, and treating that as a commit restored the chosen text and closed the list the arrow key
had just opened — measured in Chrome, where `document.activeElement` came back as `BODY` because the
button focus was headed for had been removed. `_movingFocus` is set immediately before the programmatic
focus and cleared by the focus event that follows, so only a blur that goes *outside* the component
commits.

**A commit is one of four answers.** A blank term clears the field and reports `default` — deleting the
text is the clear gesture, because the field has no accessory slot for a button. An exact match
(trimmed, `OrdinalIgnoreCase` on `TextSelector`) is chosen. With `AllowCustomValue`, any other text is
reported through `CustomValueEntered` and left in the field. Otherwise the field is put back to the chosen
item's text, so a selection-only screen can never show a name the caller was not told about.

**Enter in the field prefers an exact match, then the first match.** The `change` event that follows a
selection is suppressed for one commit, so the value is reported once rather than twice.

**An asynchronous search is cancellable and its stale results are dropped.** Each keystroke cancels the
previous call, the spinner is rendered *before* the first await (otherwise a handler's single render
arrives only when the panel is already showing the result), and a result whose token was cancelled is
ignored. Exceptions other than cancellation are left to the circuit: a search that fails silently would
render as "no matches" when the truth is that nobody asked.

## Gotchas

- **`TItem` that is a value type has no "nothing chosen" state.** `Value`'s `default` is then a real
  item — the component can display it and report it like any other. Use a reference type (or a nullable
  wrapper) when the empty state has to be distinguishable.
- **`Items` must not be null.** The default is `[]`; a null passed through fails inside `Matches` while
  rendering.
- **`TextSelector`'s default is `ToString()`**, which for a record prints every property. The control
  works, but the field and the rows show the record's debug spelling.
- **`ItemTemplate` lands inside a button.** Text, `PolarisText` and `PolarisIcon` are fine; a block
  element renders but nests a block in a button, which HTML does not allow.
- **Focus moves into the rows, so typing stops until Escape.** This is the deliberate trade for real,
  announced focus. A screen that needs type-through arrowing is asking for the `aria-activedescendant`
  combobox this layer cannot build.
- **Arrow keys are not consumed.** The interop layer has no `preventDefault`, so ArrowUp/ArrowDown also
  move the caret and can scroll the page while they move focus. There is nothing in the markup to fix
  this with; it is a property of the event forwarding.
- **The element reference capture must be overwritten, never cleared.** Clearing the map at the start of
  a render wiped the references of rows the renderer reused, and the arrow keys then had nothing to focus
  — found by a failing keyboard test, not visible in the markup.
- **`MaxSuggestions` is a drawing cap.** Dropped rows still match, so a large catalogue should be narrowed
  by typing rather than relying on the cap to be the whole answer.
- **No `Id`, no `Slot`, no `AdditionalAttributes`.** The root is a stack of components; there is no single
  element to address.
- **Focusing a row needed `focusWithin` to know about buttons.** Its selector was `input, textarea,
  select, [tabindex]`, so focusing a wrapper around an `s-button` found nothing and the arrow key
  silently did nothing — the list opened and focus stayed in the field. The browser check caught it; the
  component tests only prove the interop call was made. `button` is in the selector now.
- **Verified in Chrome**, through the sample gallery and `tools/verify-in-browser.mjs --assert`: typing
  opens and filters the list, ArrowDown puts `document.activeElement` on the row's `s-button`, Enter
  chooses it and returns focus to the field, Escape closes and restores the chosen text, and the C# value
  arrives with it. Not verified inside a real Shopify Admin.

## See also

- [PolarisTextField.cs.md](PolarisTextField.cs.md) — the field, and its `OnKeyDown`.
- [PolarisButton.cs.md](PolarisButton.cs.md) — the row, and its `OnKeyDown`.
- [PolarisSearchField.cs.md](PolarisSearchField.cs.md) — the search field this is deliberately not built on.
- [PolarisSuggestField.cs.md](PolarisSuggestField.cs.md) — the free-text sibling, where `Matches` semantics
  started.
- [PolarisSuggestFieldInline.cs.md](PolarisSuggestFieldInline.cs.md) — the non-generic picker, and the
  measured reasons its panel is inline.
- [PolarisBox.cs.md](PolarisBox.cs.md), [PolarisStack.cs.md](PolarisStack.cs.md) — the frame and the spacing.
