<!-- summary-of: PolarisTextArea.cs -->
<!-- source-sha256: e21a95674b50 -->

# PolarisTextArea.cs

A multi-line text input with the standard field chrome: label, help text, validation message,
placeholder, a visible height in rows, and length limits. Its value is a **property**, never the
element's child text, so the native `<textarea>content</textarea>` habit silently submits an empty
string.

`Polaris.Blazor.Components` · renders `<s-text-area>` · base `PolarisElementComponentBase`

## Parameters

| Parameter | Type | Default | What it does |
|---|---|---|---|
| `Label` | `string?` | `null` | Visible field label, rendered by the field itself. |
| `Details` | `string?` | `null` | Help text below the field. |
| `Error` | `string?` | `null` | Validation message; setting it also paints the field's error styling. Display only. |
| `Placeholder` | `string?` | `null` | Text shown while the field is empty. |
| `Value` | `string?` | `null` | The field's value. Emitted as `defaultValue` in markup and written as the `value` DOM property after the element upgrades. Null does neither. |
| `DefaultValue` | `string?` | `null` | Initial value only. `Value` wins when both are set; later changes have no effect. |
| `Name` | `string?` | `null` | Form field name, used when a native form submits the field. |
| `Rows` | `int?` | `null` | Visible height in text rows. Null omits the attribute and leaves the element's default height. Formatting is invariant, so a comma-decimal machine cannot break it. |
| `MaxLength` | `int?` | `null` | Maximum accepted length. Null omits the attribute; a paste can still exceed it. |
| `MinLength` | `int?` | `null` | Minimum accepted length. Null omits the attribute. |
| `Required` | `bool` | `false` | Marks the field as required. Presence attribute: true renders bare, false removes it. |
| `Disabled` | `bool` | `false` | Prevents editing. The field keeps its place in the layout. |
| `ReadOnly` | `bool` | `false` | Renders the value and allows selecting it, but prevents editing. |
| `LabelAccessibilityVisibility` | `PolarisAccessibilityVisibility?` | `null` | Hides the label from sighted users while keeping it for screen readers (`Exclusive`), or from both (`Hidden`). Null omits the attribute and the label is visible. |

Inherited: `Id`, `AccessibilityLabel`, `ItemTag`, `Slot`, `AdditionalAttributes` (see
[PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md)). There is **no
`ChildContent`** — this class derives from the element base, not the content base.

## Events

| Callback | Args | Raised when |
|---|---|---|
| `OnInput` | `EventCallback<PolarisEventArgs>` | Every keystroke, while the value is being typed. |
| `OnChange` | `EventCallback<PolarisEventArgs>` | The value is committed — the end of the interaction, which for a multi-line field is leaving the field (Enter inserts a newline rather than committing). |
| `OnFocus` | `EventCallback<PolarisEventArgs>` | The field receives focus. |
| `OnBlur` | `EventCallback<PolarisEventArgs>` | The field loses focus. |

## Behaviour

- **The value reaches the element twice, on purpose.** `defaultValue` goes into the markup so the
  first paint is right before the custom element upgrades; afterwards the `value` DOM property is the
  authoritative copy, because Polaris does not reflect it into an attribute. The wrapper therefore
  builds a one-entry property dictionary from `Value` and falls back to a shared empty set when
  `Value` is null.
- **`Rows` is height, nothing else.** It sets the visible box, not a line limit and not a character
  limit; the field does not grow with its content.
- **`OnInput` is continuous, `OnChange` ends the interaction.** Building a validation message on
  `OnInput` scolds the merchant for a half-typed sentence; validate on `OnChange` and use `OnInput`
  for a live counter.
- The label is part of the field: it is rendered inside the element and associated with the control,
  so a separate `<label>` beside it duplicates the accessible name.
- The four event callbacks are only registered when set, and they are re-mapped on every parameter
  pass — clearing a callback really removes the listener.

## Gotchas

- **Child text is ignored.** `<PolarisTextArea>Some text</PolarisTextArea>` renders an empty field
  that submits `""`, with no warning — the native `<textarea>` reads its children and this element
  does not. Use `Value`.
- **Setting `Value` back to `null` does not clear the field.** The property is written only when
  `Value` is non-null, and no cleared properties are declared, so the element keeps its last text.
  Bind `""` to empty it.
- **`Required` is not enforcement.** It renders a presence attribute and paints the required marker;
  nothing in this wrapper prevents an empty submit. Validate in C# and on the server.
- **`MaxLength` does not stop a paste.** It is an attribute the element applies to typing; pasted
  text can exceed it, so the server must enforce the same limit.
- **`Error` is display only.** Setting it paints the error state; it does not block a submit, and
  clearing it removes the styling without re-validating anything.
- **`Disabled` and `ReadOnly` are different states.** Disabled blocks interaction entirely; read-only
  keeps the value visible, selectable and copyable — use read-only for a value the merchant should be
  able to read out.
- No `Icon`, `Prefix`, `Suffix` or `Autocomplete`: those exist on
  [PolarisTextField.cs.md](PolarisTextField.cs.md), not on the multi-line element.

## See also

- [PolarisTextField.cs.md](PolarisTextField.cs.md) — the single-line counterpart, with icon, prefix
  and suffix.
- [PolarisSuggestField.cs.md](PolarisSuggestField.cs.md) — a text input with a suggestion panel.
- [PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md) — attributes, DOM
  properties and event wiring.
