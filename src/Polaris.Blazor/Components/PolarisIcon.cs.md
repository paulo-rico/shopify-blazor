<!-- summary-of: PolarisIcon.cs -->
<!-- source-sha256: 13c76adf7ff8 -->

# PolarisIcon.cs

An `<s-icon>` chosen by name from the Shopify admin's predefined icon set, coloured by tone, colour and
size, that can also announce a tooltip or popover on hover and focus.

`Polaris.Blazor.Components` · renders `<s-icon>` · base `PolarisElementComponentBase`

## Parameters

| Parameter | Type | Default | What it does |
|---|---|---|---|
| `Type` | `string?` | `null` | Icon name from the admin set, for example `product` or `delete`. An **empty string hides the icon**; `"empty"` renders an invisible icon that reserves its space, which is how a column of icons stays aligned. |
| `Tone` | `PolarisTone` | `Auto` | Semantic colour. `Auto` lets the element choose; `Critical` is what a destructive or failed state uses. Always written, including its default. |
| `Color` | `PolarisColor` | `Base` | Contrast level: `Base`, `Subdued` (de-emphasised, the right choice for a decorative icon) or `Strong`. |
| `Size` | `PolarisIconSize` | `Base` | Icon size. |
| `InterestFor` | `string?` | `null` | Id of a `PolarisTooltip` or `PolarisPopover` shown when the icon is hovered or focused. `null` drops the attribute. |

Inherited: `Id`, `AccessibilityLabel`, `ItemTag`, `Slot` and `AdditionalAttributes` from
[PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md). This component has no
`ChildContent` — an icon has nothing inside it.

## Behaviour

- **Size is a property of the icon, not of the space around it.** The element draws at `Size`; spacing
  and alignment around it come from the container (`PolarisStack`, or the `Icon` parameter on a
  `PolarisButton`).
- `InterestFor` makes the icon an *interest invoker*: Polaris opens the target itself, with no
  JavaScript and no Blazor handler, and closes it on blur or when the pointer leaves.

## Gotchas

- **Only the predefined admin set exists.** There is no path for an arbitrary SVG or an uploaded image;
  a name that is not in the set has nothing to fall back to, so the icon simply is not drawn — no error,
  no placeholder.
- **`""` and `"empty"` mean different things.** The first removes the glyph, the second keeps its box so
  neighbouring content does not shift. Reaching for the wrong one produces either a layout jump or a
  blank space that looks like a bug.
- **`Tone` and `Color` cannot be left unset.** Both are non-nullable enums, so the attribute is always
  written; there is no way to ask the element to apply no colour at all.
- **An icon on its own is silent to a screen reader.** Give it `AccessibilityLabel` when it carries
  meaning, and leave it unlabelled when it is decorative — or use `PolarisButton`'s `Icon` parameter,
  which places the icon inside a labelled control.
- `InterestFor` pointing at an id that is not rendered (a tooltip inside a collapsed section) leaves the
  hover doing nothing, silently.

## See also

- [PolarisButton.cs.md](PolarisButton.cs.md) — the labelled control that usually carries an icon.
- [PolarisThumbnail.cs.md](PolarisThumbnail.cs.md) — an image in the same slot an icon would occupy.
- [PolarisTooltip.cs.md](PolarisTooltip.cs.md) — the usual target of `InterestFor`.
- [PolarisBadge.cs.md](PolarisBadge.cs.md) — a small labelled counterpart for status.
