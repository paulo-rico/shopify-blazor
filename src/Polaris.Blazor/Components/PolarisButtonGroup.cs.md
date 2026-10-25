<!-- summary-of: PolarisButtonGroup.cs -->
<!-- source-sha256: 1c3a27db6fe6 -->

# PolarisButtonGroup.cs

Wraps `<s-button-group>`, the row that collects a primary action and its secondaries. It has one
parameter of its own — the gap — and everything else about it is the slot convention its children
must follow.

`Polaris.Blazor.Components` · renders `<s-button-group>` · base `PolarisSlottedComponentBase`

## Parameters

| Parameter | Type | Default | What it does |
|---|---|---|---|
| `Gap` | `PolarisButtonGroupGap` | `PolarisButtonGroupGap.Base` | Spacing between the buttons. `Base` renders `gap="base"`; `None` renders `gap="none"`. |

Inherited: `Id`, `AccessibilityLabel`, `ItemTag`, `Slot`, `AdditionalAttributes` and `ChildContent` from
the element base, plus two fragment parameters from `PolarisSlottedComponentBase`:
`SupplementalStart` (rendered in the `supplemental-start` slot inside a plain `div`) and `Aside` (the
`aside` column). Both are unvalidated slots, which is why they can be fragments.

## Behaviour

- The buttons are ordinary children in `ChildContent`; the group does not discover or reorder them.
  Order is markup order.
- Which button is primary and which are secondary is decided by the `Slot` parameter on each child,
  not by the group: `<PolarisButton Slot="primary-action">` and
  `<PolarisButton Slot="secondary-actions">`. Polaris validates slot children, so the slot has to sit
  on the button itself — a `div` or a `RenderFragment` wrapper around it is rejected and the button
  simply does not appear in the group's action area.
- `PolarisButtonGroupGap.None` is not merely zero spacing: the XML documents it as forbidding the
  `primary-action` slot, so a group with `Gap="None"` loses its primary action placement.

## Gotchas

- A button placed inside a nested wrapper element loses its slot without any error — the failure looks
  like a styling problem, not a markup one.
- `ChildContent` is rendered directly under the element with no wrapper, so each button is a direct
  child of the `s-button-group` and its `slot` attribute is honoured. `SupplementalStart` and `Aside`
  are each wrapped in a `div` carrying the slot name, which is safe only because Polaris does not
  type-check those two slots.

## See also

- [PolarisButton.cs.md](PolarisButton.cs.md) — the child, and the component that documents `Slot`.
- [PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md) — why slots are parameters.
