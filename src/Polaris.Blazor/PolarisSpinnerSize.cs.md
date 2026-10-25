<!-- summary-of: PolarisSpinnerSize.cs -->
<!-- source-sha256: 5fe63d04131a -->

# PolarisSpinnerSize.cs

The three sizes an `s-spinner` accepts: the base size, large, and large at the 100 step of the scale.
Size is the spinner's only option — it has no tone, no label and no value to configure.

`Polaris.Blazor` · enum for `PolarisSpinner.Size` · wire spellings pinned with `[PolarisValue]`

## Members

| Member | Wire value | What it does |
|---|---|---|
| `Base` | `base` | The standard spinner. The default on `PolarisSpinner`. |
| `Large` | `large` | A larger spinner. |
| `Large100` | `large-100` | Large at the 100 step — one step beyond `Large`. |

## Behaviour

- `PolarisSpinner.Size` defaults to `Base` and is written on every render as `size="base"`, so the
  attribute is always present and the spinner never falls back to the element's own default.
- The size is purely visual. It does not change how long the spinner runs, whether it is announced, or
  whether it can be stopped — a spinner is indeterminate by construction and spins until it is removed
  from the tree.
- The names are `base`, `large` and `large-100`, the same scale `PolarisSize` and `PolarisFontSize` use,
  so a large spinner beside large text is stating the same step rather than inventing a size of its own.

## Gotchas

- **`Large100` is the member whose name cannot survive conversion.** The camel-case fallback would emit
  `large100`, and the element ignores a size it does not know without a word; the
  `[PolarisValue("large-100")]` pin is what makes it work. Do not "simplify" the attribute away.
- **There is no `Small`.** The smallest spinner is the base size, so a compact loading indicator inside
  a dense row cannot be asked for — the alternative is not rendering a spinner there at all, or using a
  button's own `Loading` state.
- **`Base` means "default", not "medium".** The wire string is `base`, and the parameter is always
  written, so there is no way to leave the element's own size in charge.
- **It is not `PolarisIconSize` and not `PolarisSize`.** Three separate types with overlapping member
  names, so passing the wrong one is a compile error — the good outcome. Substituting one for another
  through `AdditionalAttributes` cannot work either: the wrapper assigns `size` after the caller's
  attributes, so the parameter wins.
- **A bigger spinner is not more informative.** It conveys "working" and never "how far"; a task with a
  measurable end wants a progress bar, and a spinner that keeps turning beside a real percentage reads as
  stuck.
- **Inside a button, use `PolarisButton.Loading`.** A stacked spinner shifts the label and leaves the
  button clickable, which is the mistake this enum's size cannot fix.

## See also

- [PolarisSpinner.cs.md](Components/PolarisSpinner.cs.md) — the component, its indeterminate nature and
  the "remove it from the tree" completion signal.
- [PolarisIconSize.cs.md](PolarisIconSize.cs.md) — the two-value enum that looks similar and is
  deliberately separate.
- [PolarisButton.cs.md](Components/PolarisButton.cs.md) — `Loading`, the in-button alternative.
- [PolarisEnums.cs.md](PolarisEnums.cs.md) — the shared `PolarisSize` scale these values follow.
