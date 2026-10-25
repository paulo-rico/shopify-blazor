<!-- summary-of: PolarisParagraph.cs -->
<!-- source-sha256: 288059d74c2c -->

# PolarisParagraph.cs

A block of prose, rendered as `<s-paragraph>`, which owns the vertical spacing above and below itself
and may contain inline content such as text runs, links and buttons. Use `PolarisText` instead when the
content is a run inside a line rather than a block.

`Polaris.Blazor.Components` · renders `<s-paragraph>` · base `PolarisContentComponentBase`

## Parameters

| Parameter | Type | Default | What it does |
|---|---|---|---|
| `Tone` | `PolarisTone` | `PolarisTone.Auto` | Semantic colour — neutral, info, success, warning, caution, critical. Always emitted, so `Auto` is a value the wrapper states rather than an omission. |
| `Color` | `PolarisColor` | `PolarisColor.Base` | Contrast: `Base` (standard), `Subdued` (de-emphasised) or `Strong` (high contrast). Styles the whole block. |
| `FontSize` | `PolarisFontSize` | `PolarisFontSize.Auto` | Size from `small-200` up to `large-100`; `Auto` leaves it to the element. |
| `FontWeight` | `PolarisFontWeight` | `PolarisFontWeight.Auto` | Weight from `Base` to `Bold`; `Auto` leaves it to the element. |
| `LineClamp` | `int?` | `null` | Lines after which the paragraph is truncated visually. `null` omits `lineClamp` entirely, which is the only way to say "no truncation". |
| `Dir` | `PolarisTextDirection?` | `null` | Text direction. Null drops the attribute so the paragraph inherits the document's direction; a value overrides it for this block only. |
| `AccessibilityVisibility` | `PolarisAccessibilityVisibility?` | `null` | How assistive technology sees the paragraph: `Visible`, `Exclusive` (announced, not drawn) or `Hidden`. Null drops the attribute and leaves Polaris's default. |

Inherited: `ChildContent` (the prose, from `PolarisContentComponentBase`) plus `Id`,
`AccessibilityLabel`, `ItemTag`, `Slot`, `AdditionalAttributes` (see
[PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md)).

## Behaviour

- **It is a block, and that is the whole distinction from `PolarisText`.** The paragraph owns vertical
  spacing and accepts inline children, so it is the right container for a sentence with a link in the
  middle. Its own properties style the block, not a word inside it.
- **For styling a few words, nest `PolarisText`.** The paragraph's `Tone`, `Color`, `FontSize` and
  `FontWeight` apply to everything between the tags; a single emphasised phrase needs an inline child,
  not a different parameter value.
- **`LineClamp` is visual truncation, not a data limit.** The full text stays in the DOM, so
  `textContent`, a copy, or a screen reader still has it all; only the painting is clipped. Because it
  hides content, a clamped paragraph needs a way to reach the full text — a tooltip on the same element,
  say.
- **`lineClamp` exists on the paragraph and not on `s-text`.** That split is deliberate: clamping and
  block spacing belong together, and an inline run cannot be truncated into a sensible shape.
- **`AccessibilityVisibility.Exclusive` makes the paragraph screen-reader-only** — the recommended
  spelling for information a visual layout conveys but assistive technology would otherwise miss, such
  as the sort order a table communicates with arrows.

## Gotchas

- **`LineClamp = 0` is not "no clamp".** Null omits the attribute; a zero or empty value is a different
  instruction to Polaris, which clamps the paragraph away entirely and leaves an empty-looking block.
  Leave it null when nothing should be truncated.
- **A truncated paragraph is still a block with a heading-less gap.** Removing the clamp is a re-render,
  not a layout fix: the paragraph's own vertical spacing is unchanged either way.
- **Inside a tightly packed layout it leaves gaps nobody asked for.** That spacing is deliberate, and it
  is the usual reason to reach for `PolarisText` or a layout component instead of this one; use
  `PolarisStack` `Gap` for spacing you control rather than a paragraph as a spacer.
- **`fontVariantNumeric` is deprecated on this element** in favour of `PolarisNumber`, which is left to
  that wrapper — the numeric styling is not exposed here at all.
- **It is not a `<p>`.** CSS written against a paragraph element, its `::marker`, margins or
  `:first-line` does not apply to `<s-paragraph>`; the styling lives in Polaris's shadow root.
- **`Dir` on one paragraph overrides direction for that block only.** For a whole right-to-left page
  the direction belongs on a container, not repeated per paragraph.

## See also

- [PolarisNumber.cs.md](PolarisNumber.cs.md) — tabular figures, the replacement for the deprecated
  numeric variant.
- [PolarisHeading.cs.md](PolarisHeading.cs.md) — the block-level title above a paragraph.
- [PolarisLink.cs.md](PolarisLink.cs.md) — the usual inline child of a paragraph.
- [PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md) — attributes, formatting
  and the inherited parameters.
