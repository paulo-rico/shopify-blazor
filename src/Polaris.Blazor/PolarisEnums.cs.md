<!-- summary-of: PolarisEnums.cs -->
<!-- source-sha256: a41366b708e4 -->

# PolarisEnums.cs

Every wire-spelled enum the wrappers share — button emphasis and tone, sizes and gaps, table
alignment, text weight and the declarative command a button sends. Each member is pinned with
`[PolarisValue]`, so the markup does not depend on a C# member name.

`Polaris.Blazor` · 17 enums · converted by `PolarisAttributeValue.EnumValue`

## Buttons and actions

| Enum | Members → wire value | What it controls |
|---|---|---|
| `PolarisButtonVariant` | `Auto` → `auto`, `Primary` → `primary`, `Secondary` → `secondary`, `Tertiary` → `tertiary` | Emphasis. `Primary` is the single most important action on a screen; `Tertiary` is low emphasis or an action inside a table row. There is no `Plain`. |
| `PolarisTone` | `Auto` → `auto`, `Neutral` → `neutral`, `Info` → `info`, `Success` → `success`, `Warning` → `warning`, `Critical` → `critical`, `Caution` → `caution` | Semantic colour. `Critical` is the destructive action; `Caution` is the softer warning and is distinct from `Warning`. |
| `PolarisButtonType` | `Button` → `button`, `Submit` → `submit`, `Reset` → `reset` | Form behaviour. `Button` is "do nothing on its own", not "no attribute". |
| `PolarisLinkTarget` | `Auto` → `auto`, `Blank` → `_blank`, `Self` → `_self`, `Parent` → `_parent`, `Top` → `_top` | Browsing context for an `href`. |
| `PolarisCommand` | `Auto` → `--auto`, `Show` → `--show`, `Hide` → `--hide`, `Toggle` → `--toggle`, `Copy` → `--copy` | The declarative command sent to the element named by `CommandFor`. This is how a modal or popover opens with no JavaScript. |

## Layout and spacing

| Enum | Members → wire value | What it controls |
|---|---|---|
| `PolarisSize` | `Base` → `base`, `Small` → `small`, `Large` → `large`, `Large100` → `large-100`, `Small100` → `small-100`, `Small200` → `small-200`, `Large200` → `large-200`, `Large300` → `large-300`, `Large400` → `large-400` | The box size of a control such as an avatar, thumbnail or badge. `Base` is the standard size. |
| `PolarisGap` | `None` → `none`, `Small` → `small`, `Base` → `base`, `Large` → `large` | Spacing between children of a layout component. No `Auto`. |
| `PolarisStackDirection` | `Block` → `block`, `Inline` → `inline` | The axis an `s-stack` lays its children out **along**: `Block` is vertical, `Inline` is horizontal. |
| `PolarisDividerDirection` | `Inline` → `inline`, `Block` → `block` | The axis an `s-divider` **spans**: `Inline` is a horizontal rule, `Block` is vertical. |
| `PolarisPageInlineSize` | `Small` → `small`, `Base` → `base`, `Large` → `large` | Width of an `s-page`. `Base` is required for the `aside` slot to render. |

## Tables

| Enum | Members → wire value | What it controls |
|---|---|---|
| `PolarisTableHeaderFormat` | `Base` → `base`, `Numeric` → `numeric`, `Currency` → `currency` | Column alignment. `Base` is left; `Numeric` and `Currency` both right-align, and the cell content follows the header. |
| `PolarisTableListSlot` | `Labeled` → `labeled`, `Primary` → `primary`, `Secondary` → `secondary`, `Kicker` → `kicker`, `Inline` → `inline` | How a column appears when the table collapses to a list on a narrow screen. `Primary` is the row's headline, `Kicker` leading metadata, `Inline` shares the primary's line. |

## Text and accessibility

| Enum | Members → wire value | What it controls |
|---|---|---|
| `PolarisFontWeight` | `Auto` → `auto`, `Base` → `base`, `Medium` → `medium`, `Semibold` → `semibold`, `Bold` → `bold` | Weight. `Semibold` is one word on the wire. |
| `PolarisFontSize` | `Auto` → `auto`, `Small200` → `small-200`, `Small100` → `small-100`, `Small` → `small`, `Base` → `base`, `Large` → `large`, `Large100` → `large-100` | Text size. Not interchangeable with `PolarisSize`. |
| `PolarisTextType` | `Generic` → `generic`, `Strong` → `strong`, `Address` → `address`, `Redundant` → `redundant` | The kind of run an `s-text` is. `Redundant` marks text that repeats information already conveyed, for screen readers. |
| `PolarisColor` | `Base` → `base`, `Subdued` → `subdued`, `Strong` → `strong` | Contrast level of an icon or text — not a semantic tone. |
| `PolarisAccessibilityVisibility` | `Visible` → `visible`, `Exclusive` → `exclusive`, `Hidden` → `hidden` | `Exclusive` hides from sighted users but still announces; `Hidden` removes it from assistive technology as well. |

## Defaults, as the components set them

| Enum | Default | Where |
|---|---|---|
| `PolarisButtonVariant` | `Auto` | `PolarisButton.Variant` |
| `PolarisTone` | `Auto` | `PolarisButton`, `PolarisIcon`, `PolarisText`, `PolarisNumber`, `PolarisParagraph`, `PolarisBanner` |
| `PolarisSize` | `Base` | `PolarisBadge.Size`, `PolarisAvatar.Size`, `PolarisThumbnail.Size` |
| `PolarisGap` | `None` | `PolarisStack.Gap`; `RowGap`/`ColumnGap` are nullable and override per axis |
| `PolarisStackDirection` | `Block` | `PolarisStack.Direction` |
| `PolarisDividerDirection` | `Inline` | `PolarisDivider.Direction` — a horizontal rule unless stated otherwise |
| `PolarisPageInlineSize` | `Base` | `PolarisPage.InlineSize` |
| `PolarisTableHeaderFormat` | `Base` | `PolarisTableHeader.Format`, `PolarisGridColumn.Alignment` |
| `PolarisTableListSlot` | `Labeled` | `PolarisTableHeader.ListSlot`, `PolarisGridColumn.ListSlot` |
| `PolarisFontSize`, `PolarisFontWeight` | `Auto` | `PolarisText`, `PolarisNumber`, `PolarisParagraph`, `PolarisHeading.FontSize` |
| `PolarisTextType` | `Generic` | `PolarisText.Type` |
| `PolarisColor` | `Base` | `PolarisText`, `PolarisNumber`, `PolarisParagraph`, `PolarisIcon`, `PolarisDivider`, `PolarisBadge`, `PolarisChip` |
| `PolarisAccessibilityVisibility` | `null` | `AccessibilityVisibility` / `LabelAccessibilityVisibility` are nullable everywhere — unset drops the attribute |
| `PolarisButtonType` | `Button` | `PolarisButton.Type`, `PolarisClickable.Type` |
| `PolarisLinkTarget` | `Auto` | `PolarisButton`, `PolarisLink`, `PolarisClickable` — written only when `Href` is set |
| `PolarisCommand` | `Auto` | `PolarisButton`, `PolarisLink`, `PolarisClickable`, `PolarisClickableChip` — written only when `CommandFor` is set |

## Behaviour

- **Every value is pinned**. `PolarisAttributeValue.EnumValue` prefers `[PolarisValue]` and only falls
  back to lower-casing the first letter, which cannot derive `large-100`, `small-200`, `--toggle` or
  `semibold`. Renaming a member therefore cannot change the markup, and a new member without an
  attribute silently emits `camelCased` nonsense.
- **`Auto` is a real wire value** (`auto`), not "omit": where a component defaults to `Auto` the
  attribute is still written, so Shopify's own choice is stated rather than inherited.
- The components carry these values across as attributes (`tone`, `size`, `gap`, `direction`,
  `format`, `listSlot`, `fontSize`, `fontWeight`, `color`, `type`, `target`, `command`), or as the
  DOM property `accessibilityExpanded` in the one case where a property is needed.

## Gotchas

- **`StackDirection.Inline` and `DividerDirection.Inline` mean different axes.** A stack's value names
  the axis its children flow along (inline = a horizontal row); a divider's names the axis it spans
  (inline = a horizontal line). Reasoning from one to the other gives a rule drawn *across* a row of
  items where a rule *between* them was wanted, with no error to show for it.
- **`PolarisSize.Base` is `base`, not `medium`/`normal`, and there is no `Auto`.** A size is always
  stated. `PolarisFontSize`, `PolarisIconSize` and `PolarisSpinnerSize` are separate types with
  different member sets, so one cannot be substituted for another — that mistake is a compile error
  rather than a silent one.
- **A forgotten `PolarisGap` is zero, not the base step.** `PolarisStack.Gap` defaults to `None`, so
  a vertical stack with no `Gap` puts its children flush against each other; the library's own
  composed layouts pass `Gap="PolarisGap.Base"` explicitly for that reason.
- **`PolarisPageInlineSize` must be `Base` for the `aside` slot to render.** `Small` and `Large` are
  legitimate widths, but with either of them an `aside` child disappears silently.
- **`PolarisTableListSlot` only matters on a narrow screen.** At desktop width the table markup is
  identical whatever the value, so a wrong `Primary` looks correct until the window shrinks.
- **`PolarisLinkTarget` is dropped when `Href` is null** and **`PolarisCommand` when `CommandFor` is
  null** — the wrappers write those attributes conditionally. Setting `Blank` on a button with no
  `href`, or `Show` with no `commandFor`, produces no attribute at all.
- **`PolarisCommand` values begin with `--`.** Hand-writing `command="show"` through
  `AdditionalAttributes` bypasses the enum and is ignored by the element; the strings are `--show`,
  `--hide`, `--toggle`, `--copy`, and `--auto` for the component's own default, usually toggle.
- **`PolarisAccessibilityVisibility.Exclusive` is the screen-reader-only value.** `Hidden` hides the
  content from assistive technology too, so on a label it stops the label being announced at all.
- **`PolarisColor` and `PolarisTone` are different parameters on the same components.** Tone is
  semantic (`Critical` for destructive), colour is contrast (`Subdued` for a decorative icon); neither
  type accepts the other's members.

## See also

- [PolarisAttributeValue.cs.md](PolarisAttributeValue.cs.md) — the conversion these values depend on.
- [Components/PolarisButton.cs.md](Components/PolarisButton.cs.md) — variant, tone, type, target,
  command and inline size in one parameter list.
- [Components/PolarisIcon.cs.md](Components/PolarisIcon.cs.md) — tone, colour and size on a single
  element.
- [Components/PolarisStack.cs.md](Components/PolarisStack.cs.md) — direction and the four gap
  parameters.
