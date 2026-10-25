<!-- summary-of: PolarisOptionGroup.cs -->
<!-- source-sha256: 96d0fee7232d -->

# PolarisOptionGroup.cs

Groups `PolarisOption` children under one label inside a `PolarisSelect`, rendering
`<s-option-group>` with a `label` attribute and an optional `disabled` that covers every option in the
group.

`Polaris.Blazor.Components` · renders `<s-option-group>` · base `PolarisContentComponentBase`

## Parameters

| Parameter | Type | Default | What it does |
|---|---|---|---|
| `Label` | `string?` | `null` | The group's heading, rendered as the element's `label` attribute. Null omits the attribute entirely and the options appear ungrouped, with no error. |
| `Disabled` | `bool` | `false` | Disables every option in the group. A presence attribute, formatted by the shared helper: `true` renders bare `disabled`, `false` removes it. |

Inherited: `ChildContent` — the `PolarisOption` children — from `PolarisContentComponentBase`, plus
`Id`, `AccessibilityLabel`, `ItemTag`, `Slot` and `AdditionalAttributes` from
[PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md).

## Behaviour

- **It is a container, not a control.** The group holds no value, reports no selection and raises no
  events: the chosen option's `Value` is read by the parent `PolarisSelect`, which reports it through
  its own `OnChange` / `OnInput`. Nothing in this wrapper knows which option is selected.
- **`Label` is a group heading, not the field's label.** Both are written as `label` attributes, but on
  different elements: this one names a subset of the list ("Custom ranges"), while
  `PolarisSelect.Label` names the field the merchant is filling in. A group label does not replace the
  field label, and a field label does not head a group.
- **`Disabled` cascades and the wrapper does not touch the children.** The attribute is set once on
  `<s-option-group>`; Polaris disables the options under it. There is no per-option override on this
  component, so one disabled group cannot contain an option a merchant is still meant to pick.
- **Order is markup order.** Groups and their options render exactly as the Razor loop emits them;
  neither the group nor the select sorts anything.
- **`label` and `disabled` are the element's own attribute spellings** — lower camel case, as Polaris
  declares them — which is why they appear verbatim in the rendered markup.

## Gotchas

- **Nothing validates the children or the parent.** `ChildContent` is an untyped fragment, so a
  `<div>`, a link or a bare `PolarisOption` outside any select compiles and renders; the mistake shows
  up only as unstyled or one-chosen-at-a-time behaviour in the page. Polaris's rule is one direction
  only: an option group belongs between an `s-select` and its `s-option` children.
- **A null `Label` is silent.** The attribute is dropped rather than rendered empty, so the group is
  still a grouping element with no visible heading, and the options simply run on from the previous
  ones. There is no placeholder and no diagnostic.
- **`Disabled=false` is not "enable the options".** It removes the group attribute, so each option
  falls back to its own `PolarisOption.Disabled` — a group is never able to re-enable an option that
  disabled itself.
- **Do not use it to preselect or to read state.** There is no `Value`, `Selected` or `Name` here;
  selection lives on the options and is read by the select.

## See also

- [PolarisSelect.cs.md](PolarisSelect.cs.md) — the parent control that owns the value and the events.
- [PolarisOption.cs.md](PolarisOption.cs.md) — the children this groups, with their own `Value`,
  `Selected` and `Disabled`.
- [PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md) — attributes versus DOM
  properties, and the inherited parameters.
