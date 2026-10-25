<!-- summary-of: PolarisNumberField.cs -->
<!-- source-sha256: e0305b249747 -->

# PolarisNumberField.cs

A numeric input: a text field with the standard field chrome, spinner buttons, an optional prefix and
suffix, and a value that stays a **string**. That last part is not laziness — a half-typed number is
text, and the element reports what the merchant has typed so far. It renders `<s-number-field>` and is
for quantities, not identifiers.

`Polaris.Blazor.Components` · renders `<s-number-field>` · base `PolarisElementComponentBase`

## Parameters

| Parameter | Type | Default | What it does |
|---|---|---|---|
| `Label` | `string?` | `null` | Visible field label. |
| `Details` | `string?` | `null` | Help text below the field. |
| `Error` | `string?` | `null` | Validation message, which also puts the field into its error presentation. |
| `Placeholder` | `string?` | `null` | Text shown while the field is empty. |
| `Value` | `string?` | `null` | The number as plain text. Written as `defaultValue` in markup for the first paint and as the `value` DOM property after upgrade. Null writes no property, so it clears nothing. |
| `DefaultValue` | `string?` | `null` | Initial value only, and only used when `Value` is null — changing it after the first render has no effect. |
| `Name` | `string?` | `null` | Form field name, used when a native form submits the field. |
| `Step` | `double?` | `null` | Increment the spinner buttons apply. Null omits the attribute and leaves the element's own step. |
| `Min` | `double?` | `null` | Lowest accepted value, rendered as an invariant number. |
| `Max` | `double?` | `null` | Highest accepted value, on the same terms. |
| `Prefix` | `string?` | `null` | Static text drawn before the value, e.g. a currency symbol. Decoration: it is not part of the value or of what the field reports. |
| `Suffix` | `string?` | `null` | Static text drawn after the value, e.g. a unit. Also decoration only. |
| `InputMode` | `PolarisNumberFieldInputMode` | `Decimal` | Which on-screen keyboard to request — `Decimal` or `Numeric` (digits only). A hint, not a filter. |
| `Required` | `bool` | `false` | Marks the field as required. Presence attribute: false removes it. |
| `Disabled` | `bool` | `false` | Prevents editing and takes the field out of a native form's submission. |
| `ReadOnly` | `bool` | `false` | Shows the value but prevents editing; a read-only field still submits. |
| `Autocomplete` | `string?` | `null` | Autofill hint passed through to the browser. |
| `LabelAccessibilityVisibility` | `PolarisAccessibilityVisibility?` | `null` | `Visible`, `Exclusive` (announced, not drawn) or `Hidden`. Null drops the attribute. |

Inherited: `Id`, `AccessibilityLabel`, `ItemTag`, `Slot`, `AdditionalAttributes` (see
[PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md)).

## Events

| Callback | Args | Raised when |
|---|---|---|
| `OnInput` | `EventCallback<PolarisEventArgs>` | Every keystroke. `args.Value` is the text in progress. |
| `OnChange` | `EventCallback<PolarisEventArgs>` | The number is committed — on blur or Enter. Parse and store this one. |
| `OnFocus` | `EventCallback<PolarisEventArgs>` | The field receives focus. |
| `OnBlur` | `EventCallback<PolarisEventArgs>` | The field loses focus. |

## Behaviour

- **The value is text in both directions.** `Value` is handed to the element as a string and the
  element reports a string, so the server-side parse is unavoidable and must cope with junk, an empty
  field, a lone minus sign and a trailing decimal point. `InputMode` only chooses the keyboard; it
  restricts nothing.
- **Only a plain number belongs in `Value`.** A display string with a currency symbol or a group
  separator is not a number: the element parses what it is given, so a formatted amount becomes a
  broken value with nothing logged. Format for display elsewhere (see `PolarisNumberText`) and keep
  this field's value numeric.
- **`Value` reaches the element twice.** As `defaultValue` in markup — `Value` wins over
  `DefaultValue` — so the first paint is already right, and afterwards as the `value` DOM property,
  because Polaris does not reflect it into an attribute.
- **`Step` is what the spinner buttons add or subtract**, and `Min`/`Max` bound what the field
  accepts. All three are formatted with the invariant culture, so a machine with a comma decimal
  separator cannot emit a broken attribute.
- **`Prefix` and `Suffix` are drawn, not submitted.** They are static text around the field; the value
  the field reports contains neither.

## Gotchas

- **Setting `Value` back to `null` does not empty the field.** No `value` property is written and no
  cleared property is declared, so the element keeps its last number. Bind `""` to clear it.
- **`Required`, `Disabled` and `ReadOnly` are presence attributes.** False removes the attribute
  rather than rendering `="false"`, which a custom element would read as true.
  `AdditionalAttributes` wins over a modelled parameter of the same name, so an entry there replaces
  whatever the parameter emitted.
- **Not for identifiers.** The source's own advice: an identifier that may contain letters, such as a
  SKU, belongs in a text field, not here.
- **`Error` is display only.** It paints the error state; it does not stop a form submitting and does
  not validate anything by itself.
- **`DefaultValue` is read once.** It is an attribute consumed at upgrade; a model that changes later
  must be pushed through `Value`.
- **A `Prefix` is not a label.** The field's name comes from `Label` (or `AccessibilityLabel`); a
  prefix is text drawn inside the field, so a control whose only description is a prefix has no name.

## See also

- [PolarisMoneyField.cs.md](PolarisMoneyField.cs.md) — the same control with a currency instead of
  prefix/suffix.
- [PolarisNumber.cs.md](PolarisNumber.cs.md) — the display-only numeric run for a total or a table
  cell.
- [PolarisRangeSlider.cs.md](PolarisRangeSlider.cs.md) — the numeric range whose two ends are these
  fields, and the formatting rule they share.
