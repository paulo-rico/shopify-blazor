<!-- summary-of: PolarisText.cs -->
<!-- source-sha256: d9ebad012fc7 -->

# PolarisText.cs

An inline run of text: the element that emphasises or de-emphasises part of a sentence, via a semantic
type, a tone, a colour and a font size or weight. It has no spacing and no truncation of its own, so it
is not the component for a block of prose.

`Polaris.Blazor.Components` · renders `<s-text>` · base `PolarisContentComponentBase`

## Parameters

| Parameter | Type | Default | What it does |
|---|---|---|---|
| `Type` | `PolarisTextType` | `Generic` | Semantic type of the run: `Generic`, `Strong` (emphasised, as if bold — use for a label inside a sentence), `Address` (address text), `Redundant` (text that repeats information already conveyed, for screen readers). |
| `Tone` | `PolarisTone` | `Auto` | Semantic colour: `Neutral`, `Info`, `Success`, `Warning`, `Critical`, `Caution`, or `Auto` to let the element choose. |
| `Color` | `PolarisColor` | `Base` | Contrast: `Base` (standard), `Subdued` (de-emphasised — metadata beside a value), `Strong` (high contrast). |
| `FontSize` | `PolarisFontSize` | `Auto` | Size, from `Small200` and `Small100` through `Base` to `Large100`. `Auto` leaves it to the element. |
| `FontWeight` | `PolarisFontWeight` | `Auto` | Weight: `Base`, `Medium`, `Semibold`, `Bold`, or `Auto`. |
| `InterestFor` | `string?` | `null` | Id of a tooltip or popover shown on hover/focus of this run — how explanatory text is wired to the tooltip that explains it. |
| `Dir` | `PolarisTextDirection?` | `null` | Text direction: `Ltr`, `Rtl` or `Auto`. **Null omits `dir`**, leaving the element's own default; that unset state is why the type is nullable. |
| `AccessibilityVisibility` | `PolarisAccessibilityVisibility?` | `null` | How assistive technology sees the run: `Visible`, `Exclusive` (hidden from sighted users, still announced), `Hidden` (hidden from both). Null omits the attribute. |

Inherited: `ChildContent` — the words — from
[PolarisContentComponentBase.cs.md](../PolarisContentComponentBase.cs.md), plus `Id`,
`AccessibilityLabel`, `ItemTag`, `Slot` and `AdditionalAttributes` from
[PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md).

## Behaviour

- **Inline, deliberately.** The run flows with the words around it and adds no margins, no line clamp
  and no block layout. A gap between two runs comes from the content or from a layout component, not
  from this one. For a block of prose with its own spacing, use `PolarisParagraph`, which is a
  different element.
- **The two are separate wrappers because the elements differ.** `lineClamp` exists on
  `s-paragraph` and not here, and Polaris documents inline text as having no truncation at all.
- **The five enum attributes are written on every render**; only `Dir` and `AccessibilityVisibility`
  are conditional. So `Tone="Auto"` is an explicit `tone="auto"` value, not an omission.
- **`Exclusive` is the supported way to give screen readers wording sighted users do not need** —
  no visually-hidden CSS of your own, and the text stays real text.
- **`Type="Redundant"`** marks a run whose content repeats information already conveyed, which is how
  you give screen readers a fuller phrasing of a visual statement.
- Polaris deprecates `fontVariantNumeric` on this element in favour of `PolarisNumber`; that wrapper
  is where tabular figures belong.

## Gotchas

- **There is no line clamp here.** `lineClamp` is an `s-paragraph` attribute; passing it through
  `AdditionalAttributes` renders an attribute the element does not read, so the text is not clamped
  and nothing complains.
- **`Dir="Auto"` is not "unset".** `Auto` writes `dir="auto"` (the browser decides from the content),
  which differs from omitting the attribute; leave `Dir` null when the surrounding element's
  direction should apply.
- **These attributes override the element's own defaults silently.** Every enum parameter has a
  concrete default that is rendered, so the run always states its colour, size and weight — the
  element's defaults never apply and an inherited styling context is not what you get.
- `InterestFor` is a property of `s-text` and not of `s-paragraph`; a tooltip wired to a paragraph
  this way does nothing.
- A `Slot` on inline text is inherited but has no meaning: text is content, not slotted content.

## See also

- [PolarisHeading.cs.md](PolarisHeading.cs.md) — block-level headings, with their own size scale.
- [PolarisNumber.cs.md](PolarisNumber.cs.md) — the replacement for the deprecated numeric variant.
- [PolarisLink.cs.md](PolarisLink.cs.md) — content that navigates, with its own tone and target.
- [PolarisTextField.cs.md](PolarisTextField.cs.md) — `Details` and `Error` are field chrome, not runs.
