<!-- summary-of: PolarisOption.cs -->
<!-- source-sha256: bb93f4583b43 -->

# PolarisOption.cs

One choice inside a `PolarisSelect`: it carries the value the select reports, the label the merchant
reads, and its own selected and disabled state. It renders `<s-option>` and is only valid inside a
select.

`Polaris.Blazor.Components` · renders `<s-option>` · base `PolarisContentComponentBase`

## Parameters

| Parameter | Type | Default | What it does |
|---|---|---|---|
| `Value` | `string?` | `null` | The value submitted and reported when this option is chosen. Null omits the attribute, leaving the element with no value of its own. |
| `Selected` | `bool` | `false` | Marks the option selected. Not reflected by Polaris, so it is written as the `selected` DOM property after every render. |
| `DefaultSelected` | `bool` | `false` | Renders `defaultSelected` in the markup, which marks the option selected on first paint only; later changes have no effect. |
| `Disabled` | `bool` | `false` | Prevents choosing this option. Presence attribute: false removes it, so it stays visible in the list. |

Inherited: `ChildContent` (the option's label, from `PolarisContentComponentBase`) plus `Id`,
`AccessibilityLabel`, `ItemTag`, `Slot`, `AdditionalAttributes` (see
[PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md)). The label is content, not
a parameter — what the merchant reads is what sits between the tags.

## Behaviour

- **`Selected` has to be a property write.** Polaris does not reflect the selection into an attribute,
  so the wrapper declares `selected` in its `Properties` dictionary and the base class applies it after
  the element upgrades. The attribute form would freeze at first paint.
- **`DefaultSelected` is the only thing that survives a static render.** It is emitted in the markup,
  so a page rendered before interop runs shows the right option; `Selected` corrects it afterwards.
- **The option is dumb about the select.** It holds no reference to its parent and raises no events:
  the chosen value is read by the `PolarisSelect` and reported through the select's `OnChange` /
  `OnInput`.
- **`Value` is the contract, not the label.** The value the page stores comes from here, so it must be
  stable and meaningful even if the label is reworded or translated.

## Gotchas

- **`selected` is written on every render.** The properties dictionary is never empty, so each render
  re-asserts the parameter: leaving `Selected` at its default means a page re-render hands the element
  `selected=false` for the option the merchant just picked, and the choice can appear to revert. Either
  bind `Selected` to the same model the select uses, or drive selection only through
  `PolarisSelect.Value` and leave `Selected` alone.
- **Two places express the selection and nothing reconciles them.** `Selected` here and `Value` on the
  parent select are both DOM properties written after upgrade; pick one per control.
- **An empty `Value` reports the label instead.** Polaris substitutes the option's own text when the
  value is empty, so a "none" entry saved as `value=""` writes the words "No supplier" into the
  merchant's data. Use an explicit sentinel string and map it back to null. Omitting `Value` entirely
  leaves the element with no value of its own to report.
- **`Disabled` is a presence attribute**, formatted through the shared helper, so `false` removes it
  rather than rendering `="false"` — which a custom element would read as true.
- **A `Slot` on an option has no meaning.** Options are ordinary children of the select (or of an
  option group inside it), not slotted content.

## See also

- [PolarisSelect.cs.md](PolarisSelect.cs.md) — the parent control, its `Value` and its events.
- [PolarisChoiceList.cs.md](PolarisChoiceList.cs.md) — the multi-select alternative, which uses choices
  rather than options.
- [PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md) — attributes versus DOM
  properties, and the inherited parameters.
