<!-- summary-of: PolarisIconSize.cs -->
<!-- source-sha256: fb36ee69cd66 -->

# PolarisIconSize.cs

The two sizes an `s-icon` accepts: small, and the base size that every icon gets unless told
otherwise. It is the `size` attribute on the icon element.

`Polaris.Blazor` · enum for `PolarisIcon.Size` · wire spellings pinned with `[PolarisValue]`

## Members

| Member | Wire value | What it does |
|---|---|---|
| `Small` | `small` | The smaller glyph, used where an icon sits inline with text or inside a control. |
| `Base` | `base` | The standard icon size. The default on `PolarisIcon`. |

## Behaviour

- Exposed as `PolarisIcon.Size`, defaulting to `Base`, and always written as `size="…"` — the
  attribute is never omitted.
- The library uses `Small` for the `chevron-down` that `PolarisAccordionItem` renders in its header,
  so the disclosure marker is drawn smaller than the heading text beside it.

## Gotchas

- **There is no `Large` and no `Auto`.** Only two sizes exist, and a size is always stated rather than
  left to the element, so an icon cannot be asked for a larger glyph through this parameter at all.
- **`Base` means "default", not "medium" or "normal".** The wire value is `base`, which is easy to
  mistype if you are writing the string by hand into `AdditionalAttributes`.
- **It is not `PolarisSize`.** `PolarisIcon.Size` is typed to this enum, and the nine-value
  `PolarisSize` (with `large-100`, `small-200` and so on) is a different type — one cannot be passed
  for the other, which is a compile error rather than a silent wrong size.

## See also

- [PolarisIcon.cs.md](Components/PolarisIcon.cs.md) — the element this sizes, alongside `Type`,
  `Tone`, `Color` and `InterestFor`.
- [PolarisEnums.cs.md](PolarisEnums.cs.md) — the shared enums, including `PolarisSize` and
  `PolarisFontSize` that this one is deliberately separate from.
- [PolarisAccordionItem.cs.md](Components/PolarisAccordionItem.cs.md) — the in-library use of
  `Small`.
