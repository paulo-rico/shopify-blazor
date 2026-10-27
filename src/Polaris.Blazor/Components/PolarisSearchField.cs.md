<!-- summary-of: PolarisSearchField.cs -->
<!-- source-sha256: dfa766ff111c -->

# PolarisSearchField.cs

A search box: a text input with a visible label, help text and an `input` event on every keystroke.
It has no icon, prefix, suffix or accessory slot, so a filter built on it cannot add a leading
magnifier or a clear button of its own.

`Polaris.Blazor.Components` · renders `<s-search-field>` · base `PolarisElementComponentBase`

## Parameters

| Parameter | Type | Default | What it does |
|---|---|---|---|
| `Label` | `string?` | `null` | The visible label drawn inside the field's own frame. Fields are self-labelling, so do not add a sibling `<label>`. |
| `Details` | `string?` | `null` | Help text below the field. |
| `Error` | `string?` | `null` | Validation message; a non-null value also puts the field in its error state. Null removes both. |
| `Placeholder` | `string?` | `null` | Placeholder shown while the field is empty. |
| `Value` | `string?` | `null` | The search term. Written as the `value` DOM property after the element upgrades, because Polaris does not reflect it. Null writes nothing at all. |
| `DefaultValue` | `string?` | `null` | First-paint value only, rendered as the `defaultValue` attribute so the field is correct before it upgrades. Changing it after the first render has no effect. |
| `Name` | `string?` | `null` | Form field name, used when a native form submits the field. |
| `Required` | `bool` | `false` | Marks the field required. Presence attribute: `false` removes it rather than writing `"false"`. |
| `Disabled` | `bool` | `false` | Prevents editing. |
| `ReadOnly` | `bool` | `false` | Shows the value but prevents editing; unlike `Disabled` the text stays selectable. |
| `MaxLength` | `int?` | `null` | Maximum accepted length. Omitted when null, formatted with the invariant culture when set. |
| `MinLength` | `int?` | `null` | Minimum accepted length, same treatment. |
| `Autocomplete` | `string?` | `null` | Autofill hint passed straight through to the browser. |
| `LabelAccessibilityVisibility` | `PolarisAccessibilityVisibility?` | `null` | `Visible`, `Exclusive` (announced but not drawn) or `Hidden`. Null drops the attribute and leaves Polaris's own default. |

Inherited: `Id`, `AccessibilityLabel`, `ItemTag`, `Slot`, `AdditionalAttributes` (see
[PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md)).

## Events

| Callback | Args | Raised when |
|---|---|---|
| `OnInput` | `EventCallback<PolarisEventArgs>` | Every keystroke. `args.Value` is the text as it now stands. |
| `OnChange` | `EventCallback<PolarisEventArgs>` | The term is committed — blur or Enter. |
| `OnFocus` | `EventCallback<PolarisEventArgs>` | The field receives focus. |
| `OnBlur` | `EventCallback<PolarisEventArgs>` | The field loses focus. |

## Behaviour

- **The value is split across two mechanisms.** `BuildAttributes` always writes
  `defaultValue = Value ?? DefaultValue`, so a server-rendered or not-yet-upgraded field shows the
  right text; the live value is then written as the `value` DOM property in `OnAfterRenderAsync`.
  The attribute is rebuilt on every parameter pass, so it is not a fallback that survives: it follows
  whichever of the two is non-null at the time.
- When `Value` is null the property dictionary is the shared empty set, so nothing is written and the
  merchant's typing is never clobbered by a null arriving from the server. That is what makes the
  component usable uncontrolled — omit the `Value` binding entirely.
- Booleans go through `PolarisAttributeValue.Format`, so `Required`, `Disabled` and `ReadOnly` are
  presence attributes; enums go through `EnumValue` and become Shopify's lower-case-hyphenated
  spelling.

## Gotchas

- **`OnInput` fires per character.** A query bound straight to it re-queries on every keystroke —
  debounce in C#. Only `OnChange` is the "the merchant has finished typing" signal.
- **There are no accessory slots.** The element is a text input with a magnifier and nothing else:
  no `Icon`, no `Prefix`, no `Suffix`, no `Accessory`, and no slot a wrapper `div` could fill
  (a slot child is rejected unless the element type is valid for it). A design that needs a clear
  button or a filter glyph here cannot have one.
- **Escape clears the browser's own search input.** A native `<input type="search">` wipes its value
  when Escape is pressed, so a control that wants to handle Escape itself cannot be built on this
  wrapper — the clear arrives as an `input` event and overwrites whatever a handler put back. That is
  why the autocomplete is built on `PolarisTextField`, which has no such default action.
- A `Value` that the page never updates still leaves the field usable, because the component keeps no
  copy of the text: unlike [PolarisSuggestField.cs.md](PolarisSuggestField.cs.md) it does not filter
  or hold state, so there is nothing to fall out of step.

## See also

- [PolarisTextField.cs.md](PolarisTextField.cs.md) — the same field with an icon and prefix/suffix.
- [PolarisSelect.cs.md](PolarisSelect.cs.md), [PolarisSuggestField.cs.md](PolarisSuggestField.cs.md)
- [PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md) — attributes, events and `Id`.
