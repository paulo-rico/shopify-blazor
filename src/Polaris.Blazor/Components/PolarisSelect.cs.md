<!-- summary-of: PolarisSelect.cs -->
<!-- source-sha256: 5ad338522682 -->

# PolarisSelect.cs

A single-select dropdown: the merchant picks one value from `PolarisOption` children, and `OnChange`
reports it. Multi-select needs a choice list instead — this element chooses exactly one value.

`Polaris.Blazor.Components` · renders `<s-select>` · base `PolarisContentComponentBase`

## Parameters

| Parameter | Type | Default | What it does |
|---|---|---|---|
| `Label` | `string?` | `null` | Visible label inside the field's own frame; do not add a sibling `<label>`. |
| `Details` | `string?` | `null` | Help text below the field. |
| `Error` | `string?` | `null` | Validation message; a non-null value also renders the field in its error state. |
| `Placeholder` | `string?` | `null` | Text shown in the field while nothing is selected. It is not a selectable option. |
| `Value` | `string?` | `null` | The selected option's `Value`. Written as the `value` DOM property after the element upgrades; **no** `defaultValue` attribute is emitted (see Gotchas). |
| `Icon` | `string?` | `null` | Name of a predefined admin icon shown inside the field. |
| `Name` | `string?` | `null` | Form field name, used when a native form submits the field. |
| `Required` | `bool` | `false` | Marks the field required. Presence attribute: `false` removes it. |
| `Disabled` | `bool` | `false` | Prevents interaction with the whole control. |
| `LabelAccessibilityVisibility` | `PolarisAccessibilityVisibility?` | `null` | `Visible`, `Exclusive` (announced, not drawn) or `Hidden`. Null drops the attribute. |

Inherited: `ChildContent` (the `PolarisOption` / `PolarisOptionGroup` children) plus `Id`,
`AccessibilityLabel`, `ItemTag`, `Slot`, `AdditionalAttributes` (see
[PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md)).

## Events

| Callback | Args | Raised when |
|---|---|---|
| `OnChange` | `EventCallback<PolarisEventArgs>` | The selection is committed. `args.Value` is the chosen option's value. |
| `OnInput` | `EventCallback<PolarisEventArgs>` | On every change to the selection, including an intermediate highlight while the merchant arrows through the list. |

## Behaviour

- The options are **children**, not a parameter: there is no `Items`, `Options` or data source. The
  caller's Razor markup owns the list, and each entry is a `PolarisOption` with its own `Value`.
- `Value` is written as a DOM property in `OnAfterRenderAsync`, never as an attribute, because
  Polaris does not reflect the selection. The element's own `value` attribute would freeze at first
  paint.
- `Error` does double duty: it is the message *and* the switch that puts the field into its error
  presentation, so clearing the message clears the styling with it.

## Gotchas

- **A null `Value` clears nothing.** When `Value` is null the shared empty property set is used, so no
  `value` property is written and whatever was selected stays selected. To reset the control the page
  has to write a value that exists in the list — there is no "deselect" through this wrapper.
- **Nothing is preselected in the server-rendered markup.** There is no `defaultValue` attribute (in
  contrast to [PolarisSearchField.cs.md](PolarisSearchField.cs.md) and
  [PolarisTextField.cs.md](PolarisTextField.cs.md)), so a statically rendered page shows the
  placeholder until interop writes the property. If the selection must be right on first paint, mark
  the option with `PolarisOption.DefaultSelected`.
- **Two places can express the selection**: `Value` here and `Selected` on each `PolarisOption`. Both
  are DOM properties written after upgrade and nothing reconciles them, so pick one per control.
- `OnInput` can fire for a highlight that was never committed. Read the value from `OnChange` unless
  the page genuinely wants every intermediate step.
- An empty option `Value` is not "nothing chosen": Polaris falls back to the option's own label, so a
  "none" entry needs an explicit sentinel string and a lookup table on the page.

## See also

- [PolarisOption.cs.md](PolarisOption.cs.md) — a child option, with `Value`, `Selected` and `Disabled`.
- [PolarisSuggestField.cs.md](PolarisSuggestField.cs.md) — free text with a filterable vocabulary.
- [PolarisSwitch.cs.md](PolarisSwitch.cs.md) — the two-state alternative.
- [PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md)
