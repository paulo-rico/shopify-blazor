<!-- summary-of: PolarisTextField.cs -->
<!-- source-sha256: 1533d92c745b -->

# PolarisTextField.cs

A single-line text input with the usual field chrome — label, help text, error message, placeholder,
optional icon, prefix and suffix, and length limits. Its value is a DOM property, not an attribute, so
a value the server changes after the first render still reaches the element.

`Polaris.Blazor.Components` · renders `<s-text-field>` · base `PolarisElementComponentBase`

## Parameters

| Parameter | Type | Default | What it does |
|---|---|---|---|
| `Label` | `string?` | `null` | Visible field label. The field is self-labelling: do not add a sibling `<label>`, which would give the control two accessible names. |
| `Details` | `string?` | `null` | Help text below the field. |
| `Error` | `string?` | `null` | Validation message. Setting it also renders the field in its error state. Display only. |
| `Placeholder` | `string?` | `null` | Text shown while the field is empty. |
| `Value` | `string?` | `null` | The field's value, written as the `value` **DOM property** after upgrade because Polaris does not reflect it. Null writes no property, so it does not clear the field. |
| `DefaultValue` | `string?` | `null` | Initial value only — changing it after the first render has no effect. `Value` wins when both are set. |
| `Name` | `string?` | `null` | Form field name, used when a native form submits the field. |
| `Icon` | `string?` | `null` | Name of a predefined admin icon shown inside the field. A name, not a URL or markup. |
| `Prefix` | `string?` | `null` | Static text before the value. Decoration: **not** part of the submitted value. |
| `Suffix` | `string?` | `null` | Static text after the value. Not part of the submitted value. |
| `Required` | `bool` | `false` | Marks the field as required. Presence attribute: true renders bare, false removes it. |
| `Disabled` | `bool` | `false` | Prevents editing. |
| `ReadOnly` | `bool` | `false` | Renders the value but prevents editing. |
| `MaxLength` | `int?` | `null` | Maximum accepted length. Null omits the attribute; pasting can still exceed it, so validate server-side. |
| `MinLength` | `int?` | `null` | Minimum accepted length. Null omits the attribute. |
| `Autocomplete` | `string?` | `null` | Autofill hint passed straight to the browser, e.g. `email` or `postal-code`. |
| `LabelAccessibilityVisibility` | `PolarisAccessibilityVisibility?` | `null` | Hides the label from sighted users while keeping it for screen readers (`Exclusive`), or from both (`Hidden`). Null omits the attribute and the label is visible. |

Inherited: `Id`, `AccessibilityLabel`, `ItemTag`, `Slot`, `AdditionalAttributes` (see
[PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md)). There is no
`ChildContent`: the text comes from `Value`, never from the element's children.

## Events

| Callback | Args | Raised when |
|---|---|---|
| `OnInput` | `EventCallback<PolarisEventArgs>` | Every keystroke. `args.Value` holds the current text. |
| `OnChange` | `EventCallback<PolarisEventArgs>` | The value is committed: on blur or on Enter. |
| `OnFocus` | `EventCallback<PolarisEventArgs>` | The field receives focus. |
| `OnBlur` | `EventCallback<PolarisEventArgs>` | The field loses focus. |

## Behaviour

- **`Value` is written twice, for two different moments.** As `defaultValue` in the markup, so the
  first paint shows the model's value before the custom element upgrades, and as the `value` DOM
  property afterwards, which is the authoritative copy. The markup fallback is `Value ?? DefaultValue`.
- **Only when `Value` is non-null.** A null `Value` yields an empty property set, so nothing is
  written at all — that is what makes `DefaultValue` the initial-value-only parameter.
- **`OnChange` is the commit signal, `OnInput` is the live one.** `input` fires per keystroke, so
  validating on `OnInput` scolds the merchant for a half-typed value; validate on `OnChange` and use
  `OnInput` for a live summary or a preview.
- **`Prefix` and `Suffix` are labels, not values.** They sit inside the field's frame and are not
  included in what a form submits, so a currency symbol there is cosmetic — the model still holds the
  bare number.
- **`Error` paints a state, it does not enforce one.** The field has no validation model; the page
  decides when the message is present.
- The four callbacks are mapped on every parameter pass and only registered when set.

## Gotchas

- **Setting `Value` back to `null` does not clear the field.** No property is written and no cleared
  property is declared, so the element keeps its last text. Bind `""` to empty it.
- **`MaxLength` does not stop a paste.** It is an attribute the element applies to typing; pasted text
  can exceed it and nothing truncates it afterwards.
- **`Required` is not enforcement.** It renders a presence attribute and paints the required marker;
  nothing here blocks an empty submit, and a whitespace-only value is as valid as any other.
- **A `DefaultValue` changed later is silently ignored.** Only the first render's `defaultValue`
  reaches the markup; after that the property is authoritative, so use `Value` for anything reactive.
- **A sibling `<label>` duplicates the accessible name.** The label belongs on this component; a
  second one makes screen readers announce two names for one control.
- **`Icon` is an admin icon name.** A path, URL or inline SVG in that parameter renders nothing.
- **This is single-line.** There is no `Rows` and no multi-line mode; Enter commits the value rather
  than inserting a break, so use [PolarisTextArea.cs.md](PolarisTextArea.cs.md) for prose.

## See also

- [PolarisTextArea.cs.md](PolarisTextArea.cs.md) — the multi-line counterpart, whose value is also a
  property rather than child text.
- [PolarisEmailField.cs.md](PolarisEmailField.cs.md) — the same contract with an email input.
- [PolarisMoneyField.cs.md](PolarisMoneyField.cs.md) — a numeric field, for money the merchant should
  not be able to type a currency symbol into.
- [PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md) — attributes versus DOM
  properties, and the inherited parameters.
