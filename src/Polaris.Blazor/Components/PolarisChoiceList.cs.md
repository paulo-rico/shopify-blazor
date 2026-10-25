<!-- summary-of: PolarisChoiceList.cs -->
<!-- source-sha256: 4ceadde74e9d -->

# PolarisChoiceList.cs

Wraps `<s-choice-list>`, a labelled group of PolarisChoice options that reports its selection as a
string array. The array is written to the element as a DOM property on every render — always, even
when empty — because leaving it unset makes Polaris throw inside itself.

`Polaris.Blazor.Components` · renders `<s-choice-list>` · base `PolarisContentComponentBase`

## Parameters

| Parameter | Type | Default | What it does |
|---|---|---|---|
| `Label` | `string?` | `null` | Visible list label. |
| `Details` | `string?` | `null` | Help text below the list. |
| `Error` | `string?` | `null` | Validation message shown with the list. It does not invalidate anything by itself. |
| `Multiple` | `bool` | `false` | Allows more than one choice. Without it the list is a radio group and selecting one clears the rest. Presence attribute when true. |
| `Values` | `IReadOnlyList<string>?` | `null` | The chosen values, matched against each choice's `Value`. Null and empty both mean nothing selected. Written as the `values` property, never as an attribute. |
| `Disabled` | `bool` | `false` | Prevents interaction with the whole list, not just one option. |
| `Name` | `string?` | `null` | Form field name, for submission. |
| `LabelAccessibilityVisibility` | `PolarisAccessibilityVisibility?` | `null` | `Visible`, `Exclusive` (hidden from sight, still announced) or `Hidden`. Null leaves Polaris's default. |

Inherited: `Id`, `AccessibilityLabel`, `ItemTag`, `Slot`, `AdditionalAttributes` and `ChildContent` —
the `PolarisChoice` children go in `ChildContent`; see
[PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md).

## Events

| Callback | Args | Raised when |
|---|---|---|
| `OnChange` | `EventCallback<PolarisEventArgs>` | The selection is committed; `args.Values` holds every chosen value, in the order the list reports them. |
| `OnInput` | `EventCallback<PolarisEventArgs>` | On every change to the selection as it happens; `OnChange` is the committed value. |

## Behaviour

- `Properties` is `["values"] = Values ?? []`. The empty-array fallback is deliberate and load-bearing:
  the XML remark records that Polaris calls array methods on this property, and an undefined property
  is an uncaught `TypeError: Cannot read properties of undefined (reading 'join')` inside the
  component — found by the browser verification, and invisible to a markup test.
- Because the property is written on every render, the parent's `Values` is authoritative. A handler
  that does not store `args.Values` leaves the list snapping back to the old selection on the next
  render.
- The list is either single-select or multiple-select as a whole; there is no per-option mode. It also
  has no search, filter or lazy loading, so a long catalogue needs a different control (the XML says
  so directly).

## Gotchas

- The `s-choice-list` warns at upgrade when it has no `s-choice` children (`ChoiceList should have at
  least one choice`). Under Interactive Server the parent element is inserted before its children, so
  an empty or conditional list can log that warning on a render that is perfectly correct — do not
  read it as a fault, and do not rely on Polaris reading its children at upgrade time.
- A `Multiple` list reports an array; a single-select list also reports `Values` as an array, of zero
  or one entry. Code that reads `args.Values[0]` without a length check throws when the merchant
  clears the selection.
- Selection is by `Value`, not by label. Two choices with the same value are indistinguishable in
  `Values`, and a choice with no value can never be reported.

## See also

- [PolarisChoice.cs.md](PolarisChoice.cs.md) — one option, and what its `Value` means.
- [PolarisCheckbox.cs.md](PolarisCheckbox.cs.md) — independent boxes rather than a group.
- [PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md) — why `values` is a property.
