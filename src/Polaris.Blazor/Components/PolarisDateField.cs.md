<!-- summary-of: PolarisDateField.cs -->
<!-- source-sha256: ae1ba0361b33 -->

# PolarisDateField.cs

A single-date input with a picker and the full field chrome — label, help text, validation message,
placeholder — that can also hold a range. It is the form-shaped date control; the bare calendar is
[PolarisDatePicker.cs.md](PolarisDatePicker.cs.md).

`Polaris.Blazor.Components` · renders `<s-date-field>` · base `PolarisElementComponentBase`

## Parameters

| Parameter | Type | Default | What it does |
|---|---|---|---|
| `Label` | `string?` | `null` | Visible field label. |
| `Details` | `string?` | `null` | Help text below the field. |
| `Error` | `string?` | `null` | Validation message, which also puts the field in its error state. |
| `Placeholder` | `string?` | `null` | Text shown while the field is empty. |
| `Value` | `string?` | `null` | The date, or a `start--end` range. Written as the `value` DOM property after upgrade and as `defaultValue` in markup for the first paint. |
| `DefaultValue` | `string?` | `null` | Initial date only — changing it after the first render has no effect. `Value` wins over it in the markup fallback. |
| `Name` | `string?` | `null` | Form field name, used when a native form submits the field. |
| `View` | `string?` | `null` | Month the picker shows, as `YYYY-MM`. |
| `DefaultView` | `string?` | `null` | Month shown on the first render, as `YYYY-MM`; after that the picker's own navigation takes over. |
| `Allow` | `string?` | `null` | Dates the merchant may choose: a comma-separated list of `YYYY-MM-DD`, `YYYY-MM`, `YYYY`, or a `start--end` range. An omitted range end means "and later": `2025-01-01--`. |
| `Disallow` | `string?` | `null` | Dates the merchant may not choose, in the same syntax as `Allow`. |
| `AllowDays` | `string?` | `null` | Weekdays that may be chosen, as a comma-separated list of full names such as `monday`. |
| `DisallowDays` | `string?` | `null` | Weekdays that may not be chosen, as a comma-separated list of full names. |
| `Required` | `bool` | `false` | Marks the field as required. |
| `Disabled` | `bool` | `false` | Prevents editing. |
| `ReadOnly` | `bool` | `false` | Renders the value but prevents editing. |
| `Autocomplete` | `string?` | `null` | Autofill hint passed through to the browser. |
| `LabelAccessibilityVisibility` | `PolarisAccessibilityVisibility?` | `null` | Hides the label from sighted users while keeping it for screen readers (`Exclusive`), or from both (`Hidden`). Null leaves Polaris's default. |

Inherited: `Id`, `AccessibilityLabel`, `ItemTag`, `Slot`, `AdditionalAttributes` (see
[PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md)).

## Events

| Callback | Args | Raised when |
|---|---|---|
| `OnInput` | `EventCallback<PolarisEventArgs>` | Every keystroke. `args.Value` is the text in progress, which may not be a complete date yet. |
| `OnChange` | `EventCallback<PolarisEventArgs>` | The date is committed (`change`). |
| `OnFocus` | `EventCallback<PolarisEventArgs>` | The field receives focus. |
| `OnBlur` | `EventCallback<PolarisEventArgs>` | The field loses focus. |
| `OnInvalid` | `EventCallback<PolarisEventArgs>` | The entered text is not a valid date. |
| `OnViewChange` | `EventCallback<PolarisEventArgs>` | The visible month changes, for example while paging through the picker. |

## Behaviour that is not visible in the markup

- **A range is one string, not two values.** Two ISO dates joined by `--`, e.g.
  `2025-05-28--2025-05-31`. Splitting on a single `-` mangles it, because the dates contain hyphens
  themselves.
- **`Allow`/`Disallow` are date expressions, not formatted display strings.** They accept a
  day (`YYYY-MM-DD`), a month (`YYYY-MM`), a year (`YYYY`), a `start--end` range, or a comma-separated
  combination, and an empty range end means open-ended — `2025-01-01--` is "that day and later".
- **`AllowDays`/`DisallowDays` take weekday names, not numbers**, spelled out in full (`monday`), and
  are handed to the calendar as the weekday rules it applies.
- **`Value` reaches the element twice.** As `defaultValue` in markup so the first paint is right before
  the custom element upgrades, and as the `value` DOM property afterwards, because Polaris does not
  reflect that property into an attribute.

## Gotchas

- **The allow/disallow rules constrain the picker, not the text.** A merchant can type a disallowed
  date; Polaris does not refuse it and this wrapper does not validate it. Re-check the committed value
  in C# (in `OnChange`) or on the server.
- **`OnInvalid` is the only signal that the text did not parse.** If nothing handles it, a half-typed
  or impossible date looks accepted and `Value` simply stops matching what is on screen.
- **Setting `Value` back to `null` does not clear the field.** The property is only written when
  `Value` is non-null and no cleared properties are declared, so the element keeps its last date. Bind
  `""` to empty it.
- **`DefaultValue` and `DefaultView` are first-render only.** Changing either later re-renders the
  attribute and changes nothing on screen.
- **Booleans are presence attributes.** `Required`, `Disabled` and `ReadOnly` render bare when true and
  are removed when false, because `disabled="false"` still means disabled to a custom element.

## See also

- [PolarisDatePicker.cs.md](PolarisDatePicker.cs.md) — the calendar alone, with `Type` and `VisibleMonths`.
- [PolarisEmailField.cs.md](PolarisEmailField.cs.md) — the same field contract for a different input type.
- [PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md) — attributes, properties and event wiring.

`PolarisAccessibilityVisibility` (used by `LabelAccessibilityVisibility`) is defined in
`src/Polaris.Blazor/PolarisEnums.cs`.
