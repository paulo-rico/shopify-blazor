<!-- summary-of: PolarisSuggestField.cs -->
<!-- source-sha256: c7d4852f3f9a -->

# PolarisSuggestField.cs

A free-text field with the values the shop already uses drawn beneath it as clickable, removable
chips that narrow as the merchant types. It is composed rather than wrapping an element — Polaris has
no combobox, autocomplete or listbox — so the value stays free text and the chips are a vocabulary,
not a constraint.

`Polaris.Blazor.Components` · composed component, base `ComponentBase` (there is no `s-suggest-field`) · renders a `PolarisStack` containing an `s-text-field`, a caption, a grid of `s-clickable-chip`s and an `s-button`

## Parameters

| Parameter | Type | Default | What it does |
|---|---|---|---|
| `Label` | `string?` | `null` | The field's accessible label, passed to the underlying text field. |
| `Details` | `string?` | `null` | Help text below the field. The natural home for the rule the value must obey, since the component validates nothing. |
| `Placeholder` | `string?` | `null` | Placeholder while the field is empty. |
| `Value` | `string?` | `null` | The field's text, owned by the caller. Adopted only when the *parameter* changes — see Behaviour. |
| `Suggestions` | `IReadOnlyList<string>` | `[]` | The known values drawn as chips, in the order given. It is the whole vocabulary; filtering happens inside the component. |
| `SuggestionsLabel` | `string?` | `null` | Caption above the chips, e.g. "Your suppliers". No caption is drawn when null — the right choice when `Details` already explains where the list came from. |
| `CollectionName` | `string?` | `null` | What the list is called in the add button's sentence: `Add "Acme" to your suppliers`. Lower case, no article — it continues the sentence. Null shortens the button to `Add "Acme"`. |
| `AllowAdd` | `bool` | `true` | Offers the add button. On by default, because a free-text field whose list nobody can extend makes the merchant retype the same new value for ever. |
| `Removable` | `bool` | `true` | Gives every chip a remove control. Wire `SuggestionRemoved` to act on it. |
| `Disabled` | `bool` | `false` | Disables the text field **and** suppresses the add button. |
| `SuggestionAccessibilityLabelFormat` | `string?` | `null` | `string.Format` pattern replacing each chip's accessible name, with the suggestion as `{0}` (e.g. `"Use {0}"`). Unset leaves the chip's own text as its name. |

## Events

| Callback | Args | Raised when |
|---|---|---|
| `ValueChanged` | `EventCallback<string?>` | Every keystroke, the committed text on blur or Enter, and when a chip is chosen. The argument is never null — an empty field reports `""`. |
| `SuggestionChosen` | `EventCallback<string>` | A chip is clicked. Raised **after** `ValueChanged` with the same value, so the field already holds it. |
| `SuggestionAdded` | `EventCallback<string>` | The add button is pressed. The argument is the trimmed text. The component does **not** add it to `Suggestions` — the caller does. |
| `SuggestionRemoved` | `EventCallback<string>` | A chip's remove control is used. The field's own text is left untouched. |

## Members

| Member | Signature | What it does |
|---|---|---|
| `Matches` | `static IReadOnlyList<string> Matches(IReadOnlyList<string> suggestions, string? query)` | The suggestions to draw. A blank or whitespace-only query returns the whole list, in the given order, so an untouched field works as a picker. Otherwise it keeps the entries that *contain* the trimmed query, case-insensitively (`OrdinalIgnoreCase`) — "cook" finds "Cookson Gold", and a prefix-only rule would lose it once the second word was typed. Throws `ArgumentNullException` for a null list. |
| `IsNew` | `static bool IsNew(IReadOnlyList<string> suggestions, string? value)` | Whether the typed value is absent from the list, which is what makes the add button appear. Both sides are trimmed and compared case-insensitively, so re-typing "acme" for "Acme" is not offered as a new entry. Blank text is never new. Throws `ArgumentNullException` for a null list. |

Both helpers are public and static, so a page can reuse the same filtering and duplicate rule for its
own display without duplicating the semantics.

## Behaviour

**The value is adopted when the parameter changes, not when it differs from what was reported.** The
component keeps its own `_query` so filtering does not wait for a round trip, and `OnParametersSet`
copies `Value` into it only when the incoming parameter differs from the previous pass. A parent that
applies changes lazily re-renders with its *previous* value while a new one is in flight; treating
that echo as an instruction resets the field mid-typing. A useful consequence: used with **no `Value`
binding at all**, the component is uncontrolled and nothing ever overwrites what the merchant typed.

**Typing and committing share one handler.** `OnInput` and `OnChange` on the inner text field both
call the same method, which updates `_query` and raises `ValueChanged`. Listening only to `change`
would leave the chips stale for the whole time the merchant is still typing.

**What is drawn.** A vertical `PolarisStack` (`Gap = Base`) containing, in order: the text field; the
caption in subdued text when `SuggestionsLabel` is set; a `PolarisGrid` of chips when anything
matches; and a secondary-variant button when there is something to add. The grid uses
`repeat(auto-fill, minmax(9rem, max-content))` with a small gap, so chips keep at least 9rem and wrap
to the next row instead of running off the edge.

**The add button appears only when there is something to add.** `AllowAdd && !Disabled && IsNew(...)`
— never for blank text, never for a name the list already holds, never while disabled. Its wording is
`Add "value"` or `Add "value" to {CollectionName}`, with the value trimmed and wrapped in typographic
quotes. Pressing it raises `SuggestionAdded` with the trimmed text and nothing else: adding to
`Suggestions` is the caller's job, and if the caller does not, the same button simply stays on screen.

**Picking a chip fills the field.** The text is set to the suggestion, then `ValueChanged` and
`SuggestionChosen` are both raised. **Removing a chip only forgets the name** — `SuggestionRemoved`
is raised and the field keeps its text, because the value is free text and a chip deleted from the
list must not silently rewrite the records that already carry it.

## Gotchas

- **Not verified in a real browser.** The tests prove the markup and that every callback is wired;
  they cannot prove what Polaris's chip does when its remove control is pressed. If that press also
  raises `click` on the chip, the field ends up holding the name that was just removed from the list.
  That is harmless — the value is free text and the save path enforces the real rule — but it is the
  first thing to check with `tools/verify-in-browser.mjs` on the sample gallery.
- **There is no `Error`, `Required`, `MaxLength` or validation.** The component cannot show a
  validation message; put the rule in `Details` and enforce it where the value is saved.
- **An async handler written as a non-awaited lambda is fire-and-forget.** `_ => SomeAsyncMethod()`
  binds to the `Action<T>` overload, the returned `Task` is dropped and any exception vanishes. The
  component's own callbacks are written as method groups or `async _ => await …` for exactly this
  reason; do the same in `SuggestionAdded`/`SuggestionRemoved` handlers.
- **`Suggestions` must not be null.** Both helpers throw `ArgumentNullException`; the default is `[]`,
  so this only bites when a page passes a null list through.
- **`SuggestionAccessibilityLabelFormat` goes through `string.Format`** with the invariant culture and
  exactly one argument. A stray `{` throws `FormatException` at render time, and a format with no
  `{0}` names every chip identically.
- **It does not derive from `PolarisElementComponentBase`.** There is no `Id`, `Slot`, `ItemTag` or
  `AdditionalAttributes`, and no single element to address with `commandFor` — the root is a stack.
- Chips are `PolarisClickableChip`s, not plain chips: only a clickable chip is a control the merchant
  can act on.

## See also

- [PolarisTextField.cs.md](PolarisTextField.cs.md) — the inner field, unwrapped.
- [PolarisClickableChip.cs.md](PolarisClickableChip.cs.md) — the chip used for each suggestion.
- [PolarisSuggestFieldInline.cs.md](PolarisSuggestFieldInline.cs.md) — the sibling that draws its panel inline.
- [PolarisGrid.cs.md](PolarisGrid.cs.md), [PolarisStack.cs.md](PolarisStack.cs.md) — the layout it relies on.
