<!-- summary-of: PolarisSuggestFieldInline.cs -->
<!-- source-sha256: fdbfe06e8ba1 -->

# PolarisSuggestFieldInline.cs

A pick-a-known-value-or-type-a-new-one field: a search box over the values already known, a row for
whatever was typed, and either the choice as a removable chip or the text kept in the field itself.
It is the select-shaped sibling of `PolarisSuggestField` — same vocabulary, different shape — and its
list is drawn inline under the field rather than floating over the page.

`Polaris.Blazor.Components` · composed component, base `ComponentBase` (there is no `s-suggest-field-inline`) · renders a `PolarisStack` holding an `s-search-field` and, beneath it, an `s-box` frame of `s-button` rows

## Parameters

| Parameter | Type | Default | What it does |
|---|---|---|---|
| `Label` | `string?` | `null` | Accessible label of the search box, and the caption above the chip when a value is chosen. Without it a chip is unattributed — no caption is drawn at all. |
| `Details` | `string?` | `null` | Help text below the search box, passed straight through. |
| `Placeholder` | `string?` | `null` | Placeholder while the search box is empty. |
| `Value` | `string?` | `null` | The chosen value, owned by the caller. Null or whitespace means nothing is chosen, and in chip mode it is what decides whether a chip or the search box is drawn. |
| `Suggestions` | `IReadOnlyList<string>` | `[]` | The whole known vocabulary, drawn as rows in the order given after filtering by the typed text. Must not be null. |
| `CollectionName` | `string?` | `null` | What the list is called in the add row's accessible name: `Add "Acme" to your suppliers`. Lower case, no article — it continues the sentence. Null shortens it to `Add "Acme"`. |
| `AllowAdd` | `bool` | `true` | Offers the add row for a value the list has not got. On by default: without it the list can never grow. Also suppressed whenever `Disabled`. |
| `Removable` | `bool` | `true` | Gives the chosen chip a remove control, which reports null through `ValueChanged`. Only used in chip mode. |
| `ShowValueAsChip` | `bool` | `true` | True draws the choice as a removable chip and keeps the field as an always-empty search box. False leaves the value in the field, which is how Shopify's own product-type field behaves. |
| `Disabled` | `bool` | `false` | Disables the search box, every row and the add row. It does **not** reach the chosen chip. |
| `EmptyLabel` | `string?` | `null` | Shown when there is nothing to choose and nothing typed. Null draws nothing, which on first use reads as a control that failed to load. |
| `ValueAccessibilityLabelFormat` | `string?` | `null` | `string.Format` pattern replacing the chip's accessible name, with the chosen value as `{0}` — e.g. `"Supplier: {0}"`. Unset leaves the chip's own text as its name. |
| `ValueChanged` | `EventCallback<string?>` | unset | The chosen value; null when the chip is removed. Also every keystroke in field mode — see Events. |
| `SuggestionAdded` | `EventCallback<string>` | unset | The merchant asked for the typed value to be remembered. The trimmed text. |

No inherited parameters: this class derives from `ComponentBase`, so there is no `Id`, `Slot`,
`ItemTag`, `AccessibilityLabel` or `AdditionalAttributes`, and no single element to address with
`commandFor`. The accessible names come from `Label`, `ValueAccessibilityLabelFormat` and
`CollectionName`.

## Events

| Callback | Args | Raised when |
|---|---|---|
| `ValueChanged` | `EventCallback<string?>` | A row is chosen (the row's text), a chip is removed (`null`), and — **only** when `ShowValueAsChip` is false — on every keystroke and every committed change, with the box's text. In chip mode typing raises nothing. |
| `SuggestionAdded` | `EventCallback<string>` | The add row is pressed. The argument is `_query.Trim()`, the value the caller should persist into its own list. |

## Members

| Member | Signature | What it does |
|---|---|---|
| `Matches` | `static IReadOnlyList<string> Matches(IReadOnlyList<string> suggestions, string? query)` | The rows to draw, delegated to `PolarisSuggestField.Matches`: a blank query returns the whole list in order, otherwise the entries that *contain* the trimmed query, case-insensitively. Throws `ArgumentNullException` on a null list. Public so a page can filter by the same rule. |
| `IsNew` | `static bool IsNew(IReadOnlyList<string> suggestions, string? value)` | Whether the typed text is absent from the list, delegated to `PolarisSuggestField.IsNew`: both sides trimmed and compared `OrdinalIgnoreCase`, so retyping "acme" for "Acme" is not new. Blank is never new. Throws on a null list. |

Both are delegated rather than reimplemented, deliberately: two controls offering the same vocabulary
and filtering it differently is a difference nobody would test for, and the merchant meets it as "the
list is wrong on this screen".

## Behaviour

**What is drawn.** A `PolarisStack` with `Gap = Base` wraps everything. In chip mode it holds either the
chosen chip — a subdued caption carrying `Label`, then a `PolarisClickableChip` whose child content is
the value and whose `OnRemove` calls `ClearAsync` — or, when nothing is chosen, the picker. In field
mode the picker is always drawn and the chip never is. The picker is an `s-search-field` followed, when
the list is open, by either an `EmptyLabel` in subdued text or an `s-box` (`Border="base"`,
`BorderRadius="base"`, `Overflow="hidden"`) containing a `PolarisStack` with `Gap = None` and one
tertiary, filling `PolarisButton` per matching row, then the add row: same shape, `Icon = "plus"`, an
accessible name from `CollectionName`, and visible text `Add "…"` with the typed value in typographic
quotes. The one frame is what makes the rows read as a menu hanging off the field rather than as stray
buttons.

**Rows are buttons, not clickable surfaces.** A `PolarisClickable` is not focusable and is not announced
as interactive; a list of choices a keyboard cannot reach is not a list. The add row is a button for the
same reason, and so it reads as an action rather than as another choice.

**The list opens and closes by mode.** `listOpen` is `ShowValueAsChip || !_settled`. In chip mode there
is nothing to settle — the box is a search box and is always ready. In field mode the list is closed
while the box shows a settled value and reopens the moment the merchant types, so a form that loads with
a supplier already set does not open with every supplier in the shop hanging under it.

**The value is adopted when the parameter changes, not when it differs from what was reported.** The
component keeps `_lastParameter` and returns early when `Value` equals it (`Ordinal`). A caller that
applies changes lazily re-renders with its previous value while a new one is in flight; treating that
echo as an instruction is exactly what reset the field mid-typing before this was written down. In chip
mode an adopted value just empties the search box and clears `_settled`. In field mode the incoming text
is compared with the box: equal means it is the echo of what was typed, so nothing moves, while a
genuine arrival sets the text and settles only when it is not blank.

**Typing.** `OnInput` and `OnChange` share one handler, which records the text and clears `_settled` —
listening only to `change` would leave the rows stale while the merchant is still typing. It then raises
`ValueChanged` in field mode only. In chip mode the text is deliberately not reported: the component's
value is the *chosen* supplier, a half-typed name is not a value, and `SuggestionAdded` is the channel
for a new one.

**Choosing and adding.** Choosing a row sets the box to empty in chip mode or to the row's name in field
mode, settles the field only in the latter, then raises `ValueChanged(suggestion)` in both. Adding
trims the text, re-checks `IsNew` and returns silently if it has become known meanwhile, empties the box
in chip mode, settles, and raises `SuggestionAdded` — it never adds to `Suggestions` itself, so exactly
one place owns the vocabulary.

## Gotchas

- **The panel is inline on purpose, and the alternative does not exist in this layer.** Measured in
  Chrome: `s-popover` has no methods at all (`show`, `hide`, `toggle`, `showOverlay` are `undefined`), so
  it cannot be opened from code, and `s-search-field` exposes no `interestFor`, no `commandFor` and no
  slots, so nothing can anchor a panel to it. What does work is a *button* whose `commandFor` opens a
  popover containing a search field — a different control. So this list pushes the page down, needs no
  focus trap and cannot be lost behind a stacking context.
- **No arrow keys and no `aria-activedescendant`.** There is no ARIA combobox in this library and
  inventing one in the interop layer would be interaction Polaris does not ship. Tab reaches the rows.
- **Chip mode hides the search box entirely while a value is chosen**, so there is no search-to-replace:
  clearing is the chip's job (`Removable`), and with `Removable="false"` a chosen value cannot be changed
  from the component at all.
- **`Disabled` does not disable the chip.** Only `Removable` is passed to it, so a disabled field that
  already holds a value can still be cleared.
- **`EmptyLabel` needs an empty list *and* nothing to add.** `AllowAdd` is on by default, so with any
  text typed an empty list shows the add row instead. With `EmptyLabel` null the panel is simply absent.
- **`ValueAccessibilityLabelFormat` goes through `string.Format`** with the invariant culture and exactly
  one argument; a stray `{` throws `FormatException` at render time, and a pattern without `{0}` names
  every chip identically.
- **`Suggestions` must not be null.** The default is `[]`; a page passing null through fails inside the
  helpers while rendering.
- **An async handler written as a non-awaited lambda is fire-and-forget.** `_ => SomeAsyncMethod()` binds
  to the `Action<T>` overload, the `Task` is dropped and an exception vanishes silently. The component's
  own callbacks are method groups or `async _ => await …` for that reason; do the same in
  `SuggestionAdded`.

## See also

- [PolarisSuggestField.cs.md](PolarisSuggestField.cs.md) — the chip-list sibling, and where `Matches` and `IsNew` live.
- [PolarisSearchField.cs.md](PolarisSearchField.cs.md) — the inner field, and why it has no accessory slots.
- [PolarisClickableChip.cs.md](PolarisClickableChip.cs.md) — the chip, and what `OnRemove` reports.
- [PolarisBox.cs.md](PolarisBox.cs.md), [PolarisStack.cs.md](PolarisStack.cs.md) — the frame and the spacing the rows rely on.
