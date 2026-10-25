<!-- summary-of: PolarisSection.cs -->
<!-- source-sha256: 8e81330d269c -->

# PolarisSection.cs

A labelled group inside a page: a heading, a subheading and padding around arbitrary content. The
heading level follows the section's nesting depth automatically, so a section inside a section gets
the correct heading rank without the caller choosing one.

`Polaris.Blazor.Components` · renders `<s-section>` · base `PolarisSlottedComponentBase`

## Parameters

| Parameter | Type | Default | What it does |
|---|---|---|---|
| `Heading` | `string?` | `null` | Section title. The element renders it as its own heading, at the rank implied by how deeply the section is nested. |
| `Subheading` | `string?` | `null` | Supporting text below the heading. |
| `Padding` | `PolarisSectionPadding` | `PolarisSectionPadding.Base` | `Base` for the standard inset, `None` for a section whose child supplies its own — a table, typically. Non-nullable, so the attribute is written on every render. |

Inherited from `PolarisSlottedComponentBase`:
`ChildContent` (the section body), `SupplementalStart` (the `supplemental-start` slot), `Aside` (the
`aside` column, where the element has one). From
[PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md): `Id`, `AccessibilityLabel`,
`ItemTag`, `Slot`, `AdditionalAttributes`.

## Behaviour

- `Heading` and `Subheading` are **attributes**, not content. The element draws the heading itself;
  `ChildContent` is the body under it, so a `PolarisHeading` inside it produces a second heading.
- `Padding` is sent through `PolarisAttributeValue.EnumValue`, giving the documented strings `base`
  and `none`. Because the parameter is non-nullable the attribute is never omitted — there is no way
  to say "let Polaris decide" from this wrapper.
- The body is the default slot; the two fragments the base class adds are wrapped in a plain `div`
  carrying that slot's name. Those two slots are the ones Polaris does not type-check, which is why
  they can be fragments at all.

## Gotchas

- **A named slot cannot be filled with a fragment.** Anything going into a validated slot such as
  `primary-action` must be the child component itself with `Slot="…"` set, because Polaris rejects a
  wrapper element — this is the reason the base class offers only the two unvalidated slots.
- **`Padding="None"` is the fix for a section that looks double-inset.** Wrapping a table in a
  default-padded section puts the section's padding outside the table's own cell padding, and the
  table then sits in from the card edge with nothing to explain it.
- The heading rank is decided by nesting, so the visual order of the page and the document outline
  cannot drift apart — but it also means you cannot force a rank here. If a specific `<h2>` is
  required, use [PolarisHeading.cs.md](PolarisHeading.cs.md) instead.

## See also

- [PolarisStack.cs.md](PolarisStack.cs.md) — the usual child, for spacing the section's rows.
- [PolarisHeading.cs.md](PolarisHeading.cs.md), [PolarisTable.cs.md](PolarisTable.cs.md)
- [PolarisSlottedComponentBase.cs.md](../PolarisSlottedComponentBase.cs.md) — `SupplementalStart`, `Aside` and the slot rule.
