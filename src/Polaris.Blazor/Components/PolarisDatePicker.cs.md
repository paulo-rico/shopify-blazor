<!-- summary-of: PolarisDatePicker.cs -->
<!-- source-sha256: 2f2516e66cf2 -->

# PolarisDatePicker.cs

The calendar on its own: a date picker with no label, help text or error state, for when the calendar is
the content. With `Type="Range"` the value holds two ISO dates separated by `--`.

`Polaris.Blazor.Components` · renders `<s-date-picker>` · base `PolarisElementComponentBase`

## Parameters

| Parameter | Type | Default | What it does |
|---|---|---|---|
| `Value` | `string?` | `null` | The date, or a `start--end` range. Written as the `value` DOM property after upgrade and as `defaultValue` in markup for the first paint. |
| `DefaultValue` | `string?` | `null` | Initial value only — changing it after the first render has no effect. |
| `View` | `string?` | `null` | Month the calendar shows, as `YYYY-MM`. |
| `DefaultView` | `string?` | `null` | Month shown on the first render, as `YYYY-MM`. |
| `Type` | `PolarisDatePickerType` | `Single` | Whether one date (`single`) or a range (`range`) is chosen. A range puts `start--end` in the value. |
| `VisibleMonths` | `PolarisDatePickerVisibleMonths` | `Auto` | How many months are shown at once: `Auto` follows the available width, `One` and `Two` pin it. The enum's values render as the strings `auto`, `1`, `2`. |
| `Allow` | `string?` | `null` | Dates the merchant may choose: a comma-separated list of `YYYY-MM-DD`, `YYYY-MM`, `YYYY`, or a `start--end` range. An omitted range end means "and later": `2025-01-01--`. |
| `Disallow` | `string?` | `null` | Dates the merchant may not choose, in the same syntax as `Allow`. |
| `AllowDays` | `string?` | `null` | Weekdays that may be chosen, as a comma-separated list of full names such as `monday`. |
| `DisallowDays` | `string?` | `null` | Weekdays that may not be chosen, as a comma-separated list of full names. |
| `Name` | `string?` | `null` | Form field name, used when a native form submits the value. |

Inherited: `Id`, `AccessibilityLabel`, `ItemTag`, `Slot`, `AdditionalAttributes` (see
[PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md)).

## Events

| Callback | Args | Raised when |
|---|---|---|
| `OnInput` | `EventCallback<PolarisEventArgs>` | Every keystroke while a date is being typed. |
| `OnChange` | `EventCallback<PolarisEventArgs>` | A date is chosen. |
| `OnFocus` | `EventCallback<PolarisEventArgs>` | The picker receives focus. |
| `OnBlur` | `EventCallback<PolarisEventArgs>` | The picker loses focus. |
| `OnViewChange` | `EventCallback<PolarisEventArgs>` | The visible month changes while paging through the calendar. |

There is no `OnInvalid` parameter here, unlike [PolarisDateField.cs.md](PolarisDateField.cs.md): the
`invalid` event a date field raises for unparseable typed text is not mapped by this wrapper.

## Behaviour that is not visible in the markup

- **A range is one string.** `2025-05-28--2025-05-31`, two ISO dates joined by `--`; split on that
  separator rather than on a hyphen.
- **`Type` and `VisibleMonths` are always written**, as `single`/`range` and `auto`/`1`/`2`, so they
  win over any element default rather than being omitted at their .NET defaults.
- **This is a two-dimensional control driven by a pointer.** The class documents it as awkward by
  keyboard and on a touch screen, and points at `PolarisDateField` as the better choice in a form.

## Gotchas

- **There is no `Required`, `Disabled`, `ReadOnly`, `Error` or `Label`**, and therefore no built-in
  field validation or error styling. Wrap it in your own labelling and error text, or use the field.
- **The allow/disallow rules do not validate anything by themselves** — they grey out cells in this
  calendar. A value that arrives from elsewhere is not checked, so validate the committed date in C#.
- **Setting `Value` back to `null` does not clear the picker.** The property is written only when
  `Value` is non-null and nothing is declared to be cleared; bind `""` to reset it.
- **`VisibleMonths` is written as a string, not a number** (`1`, `2`, `auto`), because the enum members
  are pinned with `PolarisValueAttribute` instead of relying on the name-to-value convention.

## See also

- [PolarisDateField.cs.md](PolarisDateField.cs.md) — the same calendar inside a labelled, validatable field.
- [PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md) — the base that writes `value` as a property.

`PolarisDatePickerType` and `PolarisDatePickerVisibleMonths` are defined in
`src/Polaris.Blazor/PolarisDateEnums.cs`, where each member's `PolarisValue` pins the string above.
