<!-- summary-of: PolarisBadge.cs -->
<!-- source-sha256: 62c0a0cf2d2a -->

# PolarisBadge.cs

A short status label — `<s-badge>` — with a semantic tone, a contrast level, a size and an optional leading icon. It stays on one line and truncates.

`Polaris.Blazor.Components` · renders `<s-badge>` · base `PolarisContentComponentBase`

## Parameters

| Parameter | Type | Default | What it does |
|---|---|---|---|
| `Tone` | `PolarisTone` | `Auto` | Semantic colour of the badge: `Info`, `Success`, `Warning`, `Critical`, `Caution`, `Neutral`, or `Auto` to let the element choose. |
| `Color` | `PolarisColor` | `Base` | Contrast of the badge's own surface: `Base`, `Subdued` or `Strong`. Independent of `Tone`. |
| `Size` | `PolarisSize` | `Base` | Badge size from the Polaris scale. |
| `Icon` | `string?` | `null` | Name of a predefined admin icon, drawn before the label. `null` drops the attribute. |

Inherited: `ChildContent` is the visible label, plus `Id`, `AccessibilityLabel`, `ItemTag`, `Slot` and `AdditionalAttributes` (see
[PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md)).

## Behaviour

- **The label is `ChildContent`; there is no `Label` parameter.** Whatever is inside the component is the badge's text.
- `Tone`, `Color` and `Size` are always written, even at their defaults, so the element receives `tone="auto"`, `color="base"` and `size="base"` rather than relying on its own defaults. Enum values are lower-cased through `PolarisAttributeValue.EnumValue`.
- `Tone` and `Color` answer different questions: `Tone` picks the semantic palette, `Color` picks how strongly the surface contrasts. A subdued success badge and a strong success badge are the same tone.

## Gotchas

- **It is single-line and truncates.** A long label is clipped, not wrapped, and there is no parameter to change that; use a banner or body text when the message has to be read in full.
- **A badge is not interactive.** There is no click and no remove — a user-supplied label that can be removed belongs to a chip, not here, and reaching for `PolarisBadge` for a merchant's tag loses the affordance silently.
- **`Icon` only accepts names from the predefined admin set.** An unknown name draws nothing at all, with no error and no placeholder, so a missing icon is indistinguishable from one that was never asked for.
- `ChildContent` is optional, so an accidental empty badge renders as a small empty pill rather than as nothing.
- An `Icon` is decorative inside the badge; it is not announced separately, so the label must carry the meaning.

## See also

- [PolarisIcon.cs.md](PolarisIcon.cs.md) — the icon set `Icon` names, and the icon-only versus `"empty"` distinction.
- [PolarisChip.cs.md](PolarisChip.cs.md) — the interactive/removable counterpart for user-supplied labels.
- [PolarisButton.cs.md](PolarisButton.cs.md) — the other small attribute-plus-content wrapper, for event conventions.
