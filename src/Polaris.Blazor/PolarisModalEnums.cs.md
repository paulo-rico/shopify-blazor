<!-- summary-of: PolarisModalEnums.cs -->
<!-- source-sha256: 3daca3147d5f -->

# PolarisModalEnums.cs

The two `s-modal` options: how much padding the dialog's content gets, and how wide it is. Both are
written onto the element on every render, so a modal always states its own size instead of inheriting
one, and `Base` is the default for each.

`Polaris.Blazor` · two enums for `s-modal` · wire spellings pinned with `[PolarisValue]`

## Members

**`PolarisModalPadding`** — inset around the content (`padding` attribute)

| Member | Wire value | What it does |
|---|---|---|
| `Base` | `base` | The standard inset. The default on `PolarisModal`. |
| `None` | `none` | No padding, for a modal that supplies its own layout — a full-bleed table or a custom frame. |

**`PolarisModalSize`** — dialog width (`size` attribute)

| Member | Wire value | What it does |
|---|---|---|
| `Small` | `small` | The narrow dialog. |
| `Small100` | `small-100` | Small, at the 100 step — one step beyond `Small` on the same scale. |
| `Base` | `base` | The default width. |
| `Large` | `large` | The wide dialog, for a form or a table that needs the room. |
| `Large100` | `large-100` | Large, at the 100 step — one step beyond `Large`. |

## Behaviour

- `PolarisModal.Padding` defaults to `PolarisModalPadding.Base` and `Size` to
  `PolarisModalSize.Base`; both are converted with `PolarisAttributeValue.EnumValue`, so `padding` and
  `size` are present on every modal whether or not the caller set them.
- `Size` changes the width and nothing else: it does not change the padding, the heading treatment or
  where the dialog is placed.
- `Padding.None` is the "the body owns its edges" option — a modal holding a table or a grid wants the
  child to paint to the frame, which is the same reasoning behind
  [PolarisSectionPadding.cs.md](PolarisSectionPadding.cs.md)'s `None`.
- Neither enum opens or closes anything. A modal is shown by a `commandFor` button or by `ShowAsync`,
  not by a parameter here.

## Gotchas

- **The default is `Base`, not `Small`.** A dialog that has never been given a size gets the standard
  width, so making dialogs narrower across an app is a change at every call site, not a change of
  default.
- **`Small100` and `Large100` are the members whose names do not survive conversion.** The camel-case
  fallback would emit `small100` and `large100`, and the element ignores an unknown size without a
  word; only `[PolarisValue("small-100")]` and `[PolarisValue("large-100")]` make them work. This is
  the reason these enums pin every member rather than relying on the convention.
- **There is no `Auto`, no `Full` and no numeric width.** Width is one of exactly five names, so a
  dialog sized to its content, or to a percentage, cannot be asked for through `Size`.
- **`Padding.None` does not add a replacement inset.** The content then sits flush against the modal's
  frame, so a modal whose body does not space itself looks like a container that was filled wrongly —
  and nothing in the markup says the inset is missing.
- **Both parameters are non-nullable**, so an unset modal still renders `padding="base" size="base"`.
  `AdditionalAttributes` is not an escape hatch either: `PolarisModal` calls `base.BuildAttributes`
  first and then assigns `padding` and `size`, so a same-named entry in the caller's dictionary is
  overwritten by the enum value. The component's list of sizes is the list.

## See also

- [PolarisModal.cs.md](Components/PolarisModal.cs.md) — the component, plus `Heading`, `ShowAsync`,
  `HideAsync`, the `primary-action` slot rule and the `OnShow`/`OnHide` callbacks.
- [PolarisSectionPadding.cs.md](PolarisSectionPadding.cs.md) — the sibling two-value padding enum, whose
  `None` has the same purpose.
- [PolarisButton.cs.md](Components/PolarisButton.cs.md) — `CommandFor`, the declarative way to open a
  modal without touching these enums.
- [PolarisEnums.cs.md](PolarisEnums.cs.md) — the shared enums, including `PolarisSize`, which these
  modal widths are not.
