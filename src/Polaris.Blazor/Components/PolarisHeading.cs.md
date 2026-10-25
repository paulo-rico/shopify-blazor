<!-- summary-of: PolarisHeading.cs -->
<!-- source-sha256: ee7344e42e06 -->

# PolarisHeading.cs

An `<s-heading>` whose semantic level is decided by where it sits in the element tree and cannot be
set, with a font size, a line clamp and an escape hatch that removes the heading semantics while keeping
the appearance.

`Polaris.Blazor.Components` · renders `<s-heading>` · base `PolarisContentComponentBase`

## Parameters

| Parameter | Type | Default | What it does |
|---|---|---|---|
| `FontSize` | `PolarisFontSize` | `Auto` | The visual size. `Auto` lets the element size the heading from its level; any other value overrides that appearance without touching the level. |
| `LineClamp` | `int?` | `null` | Number of lines after which the heading is truncated visually, with an ellipsis. `null` clamps nothing. The number is formatted invariantly. |
| `AccessibilityRole` | `string?` | `null` | Set to `none` to strip the heading semantics while keeping the appearance — for text that looks like a heading inside a section that already has one. |
| `AccessibilityVisibility` | `PolarisAccessibilityVisibility?` | `null` | How assistive technology sees the heading: `visible`, `exclusive` (announced but not drawn) or `hidden` (excluded from the accessibility tree). `null` drops the attribute. |

Inherited: `ChildContent` from [PolarisContentComponentBase.cs.md](../PolarisContentComponentBase.cs.md),
plus `Id`, `AccessibilityLabel`, `ItemTag`, `Slot` and `AdditionalAttributes` from
[PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md).

## Behaviour

- **`FontSize` is always written, including its default `auto`.** `auto` is a real documented value
  asking the element to derive the size from the heading's level, not a way to omit the attribute, so
  there is no "unset" spelling here.
- `LineClamp` is the only parameter that is dropped when unset, and the heading still renders its full
  text when it is `null`.
- The heading's children are ordinary content, so the usual markup — an icon, a link, a badge — is
  allowed inside it, unlike `PolarisEmptyState.Heading`, which is an attribute.

## Gotchas

- **You cannot choose the level.** It follows nesting depth, so a heading inside a card is one level
  deeper than the heading of the section holding the card, whatever size it is drawn at. Picking a
  smaller `FontSize` does not lower the level, and picking a larger one does not raise it.
- **`AccessibilityRole="none"` is the only way to make styled text that is *not* a heading.** Without
  it, a visual title inside an already-headlined section is announced twice by a screen reader.
- **`AccessibilityVisibility` is not a styling escape hatch.** `hidden` removes the heading from
  everyone, sighted users included, and `exclusive` hides it visually while still announcing it — a
  screen-reader-only heading. To keep a *visible* title out of the heading structure, use
  `AccessibilityRole="none"`.
- `LineClamp` truncation is visual only: the full text stays in the DOM and is still read out by
  assistive technology.

## See also

- [PolarisText.cs.md](PolarisText.cs.md) — body text, with no heading semantics.
- [PolarisSection.cs.md](PolarisSection.cs.md) — the container whose `Heading` a heading usually sits under.
- [PolarisParagraph.cs.md](PolarisParagraph.cs.md) — supporting prose under a heading.
