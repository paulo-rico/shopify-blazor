<!-- summary-of: PolarisSwitch.cs -->
<!-- source-sha256: 21833f8ce2a3 -->

# PolarisSwitch.cs

A switch for a setting that applies immediately: an on/off control with a label, help text and a
validation message, whose state is written as a DOM property rather than an attribute.

`Polaris.Blazor.Components` · renders `<s-switch>` · base `PolarisElementComponentBase`

## Parameters

| Parameter | Type | Default | What it does |
|---|---|---|---|
| `Label` | `string?` | `null` | Label drawn beside the switch. The control is self-labelling. |
| `Details` | `string?` | `null` | Help text below the switch. |
| `Error` | `string?` | `null` | Validation message; non-null also renders the error state. |
| `Checked` | `bool` | `false` | Whether the switch is on. Written as the `checked` DOM property on **every** render, so the parameter is the single source of truth. |
| `DefaultChecked` | `bool?` | `null` | Initial state only. Emitted as a bare `defaultChecked` presence attribute when `true`; `false` and `null` both emit nothing. |
| `Value` | `string?` | `null` | The string submitted when the switch is on. This is the form payload, not the on/off state. |
| `Name` | `string?` | `null` | Form field name. |
| `Disabled` | `bool` | `false` | Prevents interaction. Presence attribute; `false` removes it. |
| `LabelAccessibilityVisibility` | `PolarisAccessibilityVisibility?` | `null` | `Visible`, `Exclusive` (announced, not drawn) or `Hidden`. Null drops the attribute. |

Inherited: `Id`, `AccessibilityLabel`, `ItemTag`, `Slot`, `AdditionalAttributes` (see
[PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md)).

## Events

| Callback | Args | Raised when |
|---|---|---|
| `OnChange` | `EventCallback<PolarisEventArgs>` | The switch is toggled. `args.Checked` is the new state — this is the value to store. |
| `OnInput` | `EventCallback<PolarisEventArgs>` | On every change to the state, alongside `OnChange`. |

## Behaviour

- Unlike the text-like fields, the state is **unconditionally** written: `Properties` always returns
  a dictionary containing `checked = Checked`, even when it is `false`. There is no null to mean
  "leave the merchant's state alone", so the component is always controlled — the page must keep
  `Checked` up to date.
- The `checked` *attribute* is explicitly nulled in `BuildAttributes`, which is what stops a stale
  attribute from re-checking the control after the upgrade; `defaultChecked` is the first-paint
  mechanism and only ever appears as a bare attribute.
- `Value` exists so a checked switch submits something meaningful in a native form. It is not read
  back into `Checked`, and a null `Value` simply omits the attribute.

## Gotchas

- **The switch does not toggle itself.** If `OnChange` does not write the new `args.Checked` back into
  the parameter (through a parent field, a `ValueChanged`-style callback or a state reload), the next
  render writes the old value straight back and the switch appears to snap off — with no error and no
  warning, because from the component's side the parameter is authoritative.
- **`DefaultChecked` is first-paint only and cannot express `false`.** `false` renders no attribute at
  all, identical to leaving it null, so there is no way to pin "initially off" against an element
  default.
- A switch means *this takes effect now*: there is no Save step to undo it. A setting that only
  applies when a form is submitted belongs in a checkbox, not here.
- `Disabled` blocks the control but the `checked` property is still written, so a page that changes
  `Checked` while disabled still changes what is drawn.

## See also

- [PolarisCheckbox.cs.md](PolarisCheckbox.cs.md) — the deferred-state alternative, for a form field.
- [PolarisSelect.cs.md](PolarisSelect.cs.md), [PolarisSearchField.cs.md](PolarisSearchField.cs.md)
- [PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md) — presence attributes and property writes.
