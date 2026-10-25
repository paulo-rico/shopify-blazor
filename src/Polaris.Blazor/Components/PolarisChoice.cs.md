<!-- summary-of: PolarisChoice.cs -->
<!-- source-sha256: 5946f3bdd0db -->

# PolarisChoice.cs

Wraps `<s-choice>`, one option in a PolarisChoiceList. Its visible label is its child content, its
`Value` is what the list reports when the option is chosen, and selecting it is a DOM property write
rather than an attribute.

`Polaris.Blazor.Components` · renders `<s-choice>` · base `PolarisContentComponentBase`

## Parameters

| Parameter | Type | Default | What it does |
|---|---|---|---|
| `Value` | `string?` | `null` | The string reported in `args.Values` of the parent list when this option is selected. The label is not used as the value. |
| `Selected` | `bool` | `false` | Whether the option is selected. Written as a DOM property after upgrade; it is never rendered as a `selected` attribute. |
| `DefaultSelected` | `bool` | `false` | Marks the option selected on first render only, as a bare `defaultSelected` attribute when true. |
| `Disabled` | `bool` | `false` | Prevents choosing this option while leaving the rest of the list usable. Presence attribute when true. |

Inherited: `Id`, `AccessibilityLabel`, `ItemTag`, `Slot`, `AdditionalAttributes` and `ChildContent` —
the child content is the visible label; see
[PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md).

## Behaviour

- Only `selected` is in `Properties`; `value`, `disabled` and `defaultSelected` are attributes. The
  split matters on first paint: the attribute is what the browser has before the element upgrades and
  the property write lands, so a pre-selected list looks unselected for that instant unless
  `DefaultSelected` is set too.
- The label being child content means a choice can contain more than text. Supporting text goes in the
  `details` slot and trailing content (a badge, a price) in the `secondary-content` slot, both set
  with `Slot` on the child component. This component has no `Details` parameter — unlike
  [PolarisCheckbox.cs.md](PolarisCheckbox.cs.md).
- `Value` is optional as far as the wrapper is concerned, but a choice with no value reports nothing
  useful to the list's `args.Values`; give every choice a stable value.

## Gotchas

- It is only valid inside a [PolarisChoiceList.cs.md](PolarisChoiceList.cs.md). On its own it still
  renders, but there is no group to report a selection to and nothing warns about the mistake.
- `Selected` is written from the parameter on every render, so a parent that reads the list's
  `args.Values` but does not update its child's `Selected` will see the option snap back. Treat the
  parent's collection as the single source of truth.
- Slots are validated by Polaris. A `div` wrapping the badge for `secondary-content` is rejected —
  put `Slot="secondary-content"` on the badge component itself.

## See also

- [PolarisChoiceList.cs.md](PolarisChoiceList.cs.md) — the parent that reports the selected values.
- [PolarisCheckbox.cs.md](PolarisCheckbox.cs.md) — a single independent box.
- [PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md) — attributes versus properties.
