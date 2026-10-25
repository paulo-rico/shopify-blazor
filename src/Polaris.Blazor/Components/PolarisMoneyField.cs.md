<!-- summary-of: PolarisMoneyField.cs -->
<!-- source-sha256: c4de9b2e3128 -->

# PolarisMoneyField.cs

A money input: a text field with the full field chrome and a currency, whose value is a **string** the
element formats for the merchant's locale. It renders `<s-money-field>` and is the field to use
wherever an amount of money is typed, because the currency decides how many decimals there are.

`Polaris.Blazor.Components` · renders `<s-money-field>` · base `PolarisElementComponentBase`

## Parameters

| Parameter | Type | Default | What it does |
|---|---|---|---|
| `Label` | `string?` | `null` | Visible field label. |
| `Details` | `string?` | `null` | Help text below the field. |
| `Error` | `string?` | `null` | Validation message, which also puts the field into its error presentation. |
| `Placeholder` | `string?` | `null` | Text shown while the field is empty. |
| `Value` | `string?` | `null` | The amount as a string. Written as `defaultValue` in markup for the first paint and as the `value` DOM property after upgrade. Null writes no property, so it clears nothing. |
| `DefaultValue` | `string?` | `null` | Initial amount only, and only used when `Value` is null — changing it after the first render has no effect. |
| `Name` | `string?` | `null` | Form field name, used when a native form submits the field. |
| `Min` | `double?` | `null` | Lowest accepted amount. Rendered as an invariant number, not as money. |
| `Max` | `double?` | `null` | Highest accepted amount, on the same terms. |
| `CurrencyCode` | `string?` | `null` | ISO 4217 code, or `auto` for the shop's own currency. Presentation only — Polaris never converts between currencies. |
| `Required` | `bool` | `false` | Marks the field as required. Presence attribute: false removes it. |
| `Disabled` | `bool` | `false` | Prevents editing and takes the field out of the form's submission. |
| `ReadOnly` | `bool` | `false` | Shows the value but prevents editing; a read-only field still submits. |
| `Autocomplete` | `string?` | `null` | Autofill hint passed through to the browser. |
| `LabelAccessibilityVisibility` | `PolarisAccessibilityVisibility?` | `null` | `Visible`, `Exclusive` (announced, not drawn) or `Hidden`. Null drops the attribute and leaves Polaris's default. |

Inherited: `Id`, `AccessibilityLabel`, `ItemTag`, `Slot`, `AdditionalAttributes` (see
[PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md)).

## Events

| Callback | Args | Raised when |
|---|---|---|
| `OnInput` | `EventCallback<PolarisEventArgs>` | Every keystroke. `args.Value` is the text in progress, which may be partial. |
| `OnChange` | `EventCallback<PolarisEventArgs>` | The amount is committed — on blur or Enter. This is the value to parse and store. |
| `OnFocus` | `EventCallback<PolarisEventArgs>` | The field receives focus. |
| `OnBlur` | `EventCallback<PolarisEventArgs>` | The field loses focus. |

## Behaviour

- **The value never becomes a number here.** The element renders and reports a locale-formatted
  string, so parsing belongs to the caller and must use the currency's own rules rather than a fixed
  scale: a zero-decimal currency such as JPY has no fractional part at all, while most currencies have
  two. `decimal.TryParse` against the invariant culture is the wrong tool for a value that contains a
  merchant's group separators.
- **`Value` reaches the element twice.** As `defaultValue` in the markup — `Value` wins over
  `DefaultValue` — so a statically rendered page already shows the amount, and afterwards as the
  `value` DOM property, because Polaris does not reflect that property back into an attribute.
- **`CurrencyCode` is a display decision.** It chooses the symbol and the number of decimals; the
  amount is stored as typed and no exchange rate is applied anywhere.
- **`Min`/`Max` are plain numbers, not money strings.** They are formatted with the invariant culture
  by the shared attribute helper, so a bound of 12.5 becomes `12.5` on every machine. They bound what
  the merchant may enter; they are not formatted or validated against the currency's scale.

## Gotchas

- **Setting `Value` back to `null` does not empty the field.** No `value` property is written and no
  cleared property is declared, so the element keeps its last amount. Bind `""` to clear it.
- **`Required`, `Disabled` and `ReadOnly` are presence attributes.** A `false` removes the attribute
  rather than rendering `="false"`, which a custom element would read as true; the shared formatter
  applies that rule to every bool the wrapper emits. An entry in `AdditionalAttributes` with the same
  name wins over the parameter — it is formatted the same way, so it can change the value but not the
  rule.
- **`Error` is display only.** Setting it paints the error state; it does not block a form submission,
  and it does not re-run any validation. Any actual rule is the page's.
- **No `Step` and no precision parameter.** The wrapper exposes neither, so the increment the element
  offers is its own default; a fixed minor-unit step is not expressible through this component.
- **`DefaultValue` is not a fallback the field re-reads.** It is one attribute read at upgrade time;
  a model that changes later must be pushed through `Value`.
- **The label may be invisible but is never absent.** `LabelAccessibilityVisibility` moves the label
  into screen-reader-only or hidden territory; use it under a heading that already names the field,
  not to remove the label outright.

## See also

- [PolarisNumberField.cs.md](PolarisNumberField.cs.md) — the same shape for a quantity, with a step and
  prefix/suffix instead of a currency.
- [PolarisRangeSlider.cs.md](PolarisRangeSlider.cs.md) — a price band as a range, and the
  plain-number formatting its fields require.
- [PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md) — how attributes,
  properties and events are wired.
