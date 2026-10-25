<!-- summary-of: PolarisCheckbox.cs -->
<!-- source-sha256: 2c2be0460520 -->

# PolarisCheckbox.cs

Wraps `<s-checkbox>`, a single tick box with a label, help text and validation message. The ticked
state and the mixed state are written as DOM properties after the element upgrades, never as
attributes, because Polaris does not reflect either one.

`Polaris.Blazor.Components` · renders `<s-checkbox>` · base `PolarisElementComponentBase`

## Parameters

| Parameter | Type | Default | What it does |
|---|---|---|---|
| `Label` | `string?` | `null` | The label beside the box. There is no `ChildContent` on this component — the text is a string parameter. |
| `Details` | `string?` | `null` | Help text below the checkbox. |
| `Error` | `string?` | `null` | Validation message shown under the box; it does not itself invalidate anything. |
| `Checked` | `bool` | `false` | Whether the box is ticked. Written as a DOM property after upgrade, on every render. |
| `DefaultChecked` | `bool?` | `null` | Initial checked state only, for first paint. Only `true` emits the attribute; `false` and `null` both drop it. |
| `Indeterminate` | `bool` | `false` | The mixed dash used by a "select all" control whose children are partly ticked. Also a DOM property. |
| `Value` | `string?` | `null` | Value submitted with the form when the box is ticked. |
| `Name` | `string?` | `null` | Form field name. Without one the checkbox does not appear in the submitted form data. |
| `Required` | `bool` | `false` | Marks the checkbox required — it must be ticked to submit. Rendered as a bare presence attribute when true. |
| `Disabled` | `bool` | `false` | Prevents interaction. Rendered as a presence attribute; false removes it. |
| `LabelAccessibilityVisibility` | `PolarisAccessibilityVisibility?` | `null` | `Visible`, `Exclusive` (hidden from sight but still announced) or `Hidden`. Null leaves Polaris's default. |

Inherited: `Id`, `AccessibilityLabel`, `ItemTag`, `Slot`, `AdditionalAttributes` — see
[PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md).

## Events

| Callback | Args | Raised when |
|---|---|---|
| `OnChange` | `EventCallback<PolarisEventArgs>` | The box is toggled; read `args.Checked` (a nullable `bool`, so treat null as "not told"). |
| `OnInput` | `EventCallback<PolarisEventArgs>` | On every change to the box. |
| `OnBlur` | `EventCallback<PolarisEventArgs>` | The checkbox loses focus. |

## Behaviour

- `Checked` and `Indeterminate` are the only two properties in `Properties`; `BuildAttributes` writes
  both attribute names as `null` on purpose, so they can never be emitted as `checked`/`indeterminate`
  attributes. The visible state therefore arrives only after the element has upgraded and
  `OnAfterRenderAsync` has written the properties.
- `DefaultChecked` is the one that *is* an attribute: it is what the browser paints before the
  component's property write lands, and Polaris does not re-read it afterwards.
- `Required` and `Disabled` go through the boolean formatter, so `true` is a bare attribute and
  `false` is no attribute — `disabled="false"` would otherwise be read as true by a custom element.

## Gotchas

- If the interop module fails to load, the markup still renders perfectly and `Checked` never takes
  effect: the tick is a property write, and the failure is logged by the interop layer rather than
  raised, so nothing in the markup shows the miss.
- Changing `DefaultChecked` after the first render achieves nothing — it is initial state only. Drive
  the box with `Checked` from then on.
- Use a checkbox when the choice is submitted explicitly, and a switch for a setting that applies
  immediately (the XML remark). Toggling a checkbox and acting on `OnChange` gives immediate behaviour
  while looking like an explicit-submit control, which is usually the wrong signal to a merchant.
- `Error` is only a string: setting it does not mark the field invalid or block a submit; the form
  validation, if any, is yours.

## See also

- [PolarisChoiceList.cs.md](PolarisChoiceList.cs.md) — several options rather than one.
- [PolarisChoice.cs.md](PolarisChoice.cs.md) — one option inside that list.
- [PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md) — how properties differ from attributes.
