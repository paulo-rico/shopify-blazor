<!-- summary-of: PolarisEmailField.cs -->
<!-- source-sha256: fafcd888b01d -->

# PolarisEmailField.cs

An email input with the standard field chrome — label, help text, validation message, placeholder,
length limits. Polaris adds no email rule of its own and browser validation varies by vendor, so the
address must be validated again on the server.

`Polaris.Blazor.Components` · renders `<s-email-field>` · base `PolarisElementComponentBase`

## Parameters

| Parameter | Type | Default | What it does |
|---|---|---|---|
| `Label` | `string?` | `null` | Visible field label. |
| `Details` | `string?` | `null` | Help text below the field. |
| `Error` | `string?` | `null` | Validation message, which also puts the field in its error state. |
| `Placeholder` | `string?` | `null` | Text shown while the field is empty. |
| `Value` | `string?` | `null` | The field's value. Written as the `value` DOM property after upgrade and as `defaultValue` in markup so the first paint is correct before the element upgrades. |
| `DefaultValue` | `string?` | `null` | Initial value only — changing it after the first render has no effect. `Value` wins over it in the markup fallback. |
| `Name` | `string?` | `null` | Form field name, used when a native form submits the field. |
| `Required` | `bool` | `false` | Marks the field as required. |
| `Disabled` | `bool` | `false` | Prevents editing. |
| `ReadOnly` | `bool` | `false` | Renders the value but prevents editing. |
| `MaxLength` | `int?` | `null` | Maximum accepted length. Null omits the attribute and leaves the element's default. Pasting can still exceed it. |
| `MinLength` | `int?` | `null` | Minimum accepted length. Null omits the attribute. |
| `Autocomplete` | `string?` | `null` | Autofill hint passed through to the browser, e.g. `email`. |
| `LabelAccessibilityVisibility` | `PolarisAccessibilityVisibility?` | `null` | Hides the label from sighted users while keeping it for screen readers (`Exclusive`), or from both (`Hidden`). |

Inherited: `Id`, `AccessibilityLabel`, `ItemTag`, `Slot`, `AdditionalAttributes` (see
[PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md)).

## Events

| Callback | Args | Raised when |
|---|---|---|
| `OnInput` | `EventCallback<PolarisEventArgs>` | Every keystroke. `args.Value` holds the current text. |
| `OnChange` | `EventCallback<PolarisEventArgs>` | The value is committed: on blur or on Enter. |
| `OnFocus` | `EventCallback<PolarisEventArgs>` | The field receives focus. |
| `OnBlur` | `EventCallback<PolarisEventArgs>` | The field loses focus. |

## Behaviour that is not visible in the markup

- **`Value` reaches the element twice.** As `defaultValue` in markup, so the first render shows the
  model's value before the custom element upgrades, and as the `value` DOM property afterwards, which
  is the copy that is authoritative — Polaris does not reflect that property into an attribute.
- **`OnChange` is the commit signal.** `input` fires per keystroke, so building a validation message on
  `OnInput` scolds the merchant for a half-typed address; use `OnChange` for validation and `OnInput`
  for a live summary.
- **The length limits are attributes, not enforcement.** `MaxLength` and `MinLength` are formatted as
  invariant integers, so a machine with a comma decimal separator cannot produce a broken attribute —
  but a paste can still exceed `MaxLength`, and neither limit is enforced by this wrapper.

## Gotchas

- **Nothing here validates that the text is an email address.** The browser's own check differs between
  vendors, Polaris adds no rule, and this wrapper adds none, so `"a"` is accepted by `s-email-field`.
  Validate in C# and again on the server.
- **Setting `Value` back to `null` does not clear the field.** The property is written only when
  `Value` is non-null and no cleared properties are declared, so the element keeps its last text. Bind
  `""` to empty it.
- **`Required` is a presence attribute.** It renders bare when true and disappears when false, because
  `required="false"` is still required to a custom element.
- **`Error` is display only.** Setting it paints the error state; it does not prevent a form from
  submitting the value, and clearing it removes the styling without re-validating anything.

## See also

- [PolarisDateField.cs.md](PolarisDateField.cs.md) — the same field contract for dates.
- [PolarisColorField.cs.md](PolarisColorField.cs.md) — the same field contract for colours.
- [PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md) — attributes, properties and event wiring.

`PolarisAccessibilityVisibility` (used by `LabelAccessibilityVisibility`) is defined in
`src/Polaris.Blazor/PolarisEnums.cs`.
