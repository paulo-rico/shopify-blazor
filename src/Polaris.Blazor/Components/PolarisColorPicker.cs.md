<!-- summary-of: PolarisColorPicker.cs -->
<!-- source-sha256: 75946d36ff50 -->

# PolarisColorPicker.cs

A bare colour picker: the palette without field chrome. There is no label, help text or error state, so
it suits a screen where the picker itself is the content rather than one value in a form. It works in
HSL internally and reports hex.

`Polaris.Blazor.Components` · renders `<s-color-picker>` · base `PolarisElementComponentBase`

## Parameters

| Parameter | Type | Default | What it does |
|---|---|---|---|
| `Value` | `string?` | `null` | The colour as hex. Written as the `value` DOM property after the element upgrades, and as `defaultValue` in markup for the first paint. |
| `DefaultValue` | `string?` | `null` | Initial colour only — changing it after the first render has no effect. |
| `Alpha` | `bool` | `false` | Adds an opacity control and makes the emitted value eight-digit hex (`#RRGGBBAA`). |
| `Name` | `string?` | `null` | Form field name, used when a native form submits the value. |

Inherited: `Id`, `AccessibilityLabel`, `ItemTag`, `Slot`, `AdditionalAttributes` (see
[PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md)).

## Events

| Callback | Args | Raised when |
|---|---|---|
| `OnInput` | `EventCallback<PolarisEventArgs>` | Continuously, on every change while the colour is being dragged. `args.Value` is the hex in progress. |
| `OnChange` | `EventCallback<PolarisEventArgs>` | The colour is committed — the drag or the edit has finished. |

## Behaviour that is not visible in the markup

- **It is the picker, not a field.** There is no `Label`, `Details`, `Error`, `Placeholder`,
  `Required`, `Disabled`, `ReadOnly`, `Autocomplete` or `LabelAccessibilityVisibility` parameter here,
  because the element has none. A colour that is one value in a form wants
  [PolarisColorField.cs.md](PolarisColorField.cs.md), which has all of them.
- **HSL in, hex out.** The element holds the colour in HSL internally; the value it reports is hex, so
  a value round-tripped through it may come back as a different but equivalent string.
- **`OnInput` fires during a drag**, so binding it straight to a save re-saves on every pointer move;
  use `OnChange` for persistence and `OnInput` for a live preview.

## Gotchas

- **Setting `Value` back to `null` does not clear the picker.** The property is written only when
  `Value` is non-null, and no cleared properties are declared, so the element keeps its last colour.
  Write `""` to empty it.
- **`Alpha` changes the shape of the value.** With it off you get six hex digits, with it on eight; a
  parser that assumes one length breaks silently when the parameter is flipped.
- **No focus or blur event is exposed**, even though the element is focusable — a caller that needs to
  know the merchant left the picker has to capture focus elsewhere.

## See also

- [PolarisColorField.cs.md](PolarisColorField.cs.md) — the same palette with label, help text and error state.
- [PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md) — how `value` and `defaultValue` are applied.
