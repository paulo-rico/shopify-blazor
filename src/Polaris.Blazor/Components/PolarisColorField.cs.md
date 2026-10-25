<!-- summary-of: PolarisColorField.cs -->
<!-- source-sha256: ddeece119f45 -->

# PolarisColorField.cs

A colour input with the full field chrome — label, help text, validation message, placeholder — that
accepts hex, RGB or HSL typing and always reports hex. Whatever you store is therefore normalised to
`#RRGGBB`, or to eight digits when `Alpha` is on.

`Polaris.Blazor.Components` · renders `<s-color-field>` · base `PolarisElementComponentBase`

## Parameters

| Parameter | Type | Default | What it does |
|---|---|---|---|
| `Label` | `string?` | `null` | Visible field label. |
| `Details` | `string?` | `null` | Help text below the field. |
| `Error` | `string?` | `null` | Validation message, which also puts the field in its error state. |
| `Placeholder` | `string?` | `null` | Text shown while the field is empty. |
| `Value` | `string?` | `null` | The colour as hex. Written as the `value` DOM property after the element upgrades, and as `defaultValue` in markup so the first paint is already correct. |
| `DefaultValue` | `string?` | `null` | Initial colour only — changing it after the first render has no effect. `Value` wins over it in the markup fallback. |
| `Alpha` | `bool` | `false` | Adds an opacity control and makes the emitted value eight-digit hex (`#RRGGBBAA`). Flipping it changes the length of every value the field reports. |
| `Autocomplete` | `string?` | `null` | Autofill hint passed through to the browser: `on` or `off`. |
| `Name` | `string?` | `null` | Form field name, used when a native form submits the field. |
| `Required` | `bool` | `false` | Marks the field as required. |
| `Disabled` | `bool` | `false` | Prevents editing. |
| `ReadOnly` | `bool` | `false` | Renders the value but prevents editing, whereas `Disabled` takes the control out of use altogether. |
| `LabelAccessibilityVisibility` | `PolarisAccessibilityVisibility?` | `null` | Hides the label from sighted users while keeping it for screen readers (`Exclusive`), or hides it from both (`Hidden`). Null leaves Polaris's default. |

Inherited: `Id`, `AccessibilityLabel`, `ItemTag`, `Slot`, `AdditionalAttributes` (see
[PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md)).

## Events

| Callback | Args | Raised when |
|---|---|---|
| `OnInput` | `EventCallback<PolarisEventArgs>` | Every keystroke and every drag inside the picker. `args.Value` is the hex string in progress. |
| `OnChange` | `EventCallback<PolarisEventArgs>` | The colour is committed. `args.Value` is the normalised hex. |
| `OnFocus` | `EventCallback<PolarisEventArgs>` | The field receives focus. |
| `OnBlur` | `EventCallback<PolarisEventArgs>` | The field loses focus. |

## Behaviour that is not visible in the markup

- **The value is normalised on the way out, not on the way in.** The field accepts `#abc`, `rgb(…)`
  and `hsl(…)`, but what comes back in `OnInput`/`OnChange` is always hex — `#RRGGBB`, or eight digits
  with `Alpha`. Store the event's value rather than the text the merchant typed.
- **`Value` reaches the element twice, by two routes.** In markup it is `defaultValue`, which is what
  the first paint shows before the custom element exists; after upgrade it is written as the `value`
  DOM property. `value` is not reflected by Polaris, so the property write is the one that matters.
- **The label is still in the accessibility tree when `LabelAccessibilityVisibility` is set.** Use it
  for a field sitting under a heading that already names it, not to remove the label outright.

## Gotchas

- **Setting `Value` back to `null` does not clear the field.** The wrapper writes the property only
  when `Value` is non-null and declares no cleared property, so the element keeps the last colour it
  was given. To empty it, write `""`.
- **Booleans are presence attributes.** `Alpha`, `Required`, `Disabled` and `ReadOnly` are formatted
  through `PolarisAttributeValue.Format`, so `false` removes the attribute rather than rendering
  `="false"` — which a custom element would read as true.
- **`DefaultValue` is not a fallback the field re-reads.** Once rendered it is inert; if you need the
  field to follow a changing model, bind `Value` and update it in the event handler.

## See also

- [PolarisColorPicker.cs.md](PolarisColorPicker.cs.md) — the same colour chooser without field chrome.
- [PolarisDateField.cs.md](PolarisDateField.cs.md) — the same label/details/error/visibility shape.
- [PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md) — attributes, properties and interop.
