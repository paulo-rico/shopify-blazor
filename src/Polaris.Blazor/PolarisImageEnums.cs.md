<!-- summary-of: PolarisImageEnums.cs -->
<!-- source-sha256: fba9d54d02b0 -->

# PolarisImageEnums.cs

Four enums that set how an `<s-image>` fits its box, when the browser fetches it, how wide it renders,
and whether assistive technology announces it. `PolarisImage` writes all four onto the element on every
render, so each is a stated choice rather than an inherited default.

`Polaris.Blazor` · four enums for `s-image` · wire spellings pinned with `[PolarisValue]`

## Members

**`PolarisImageObjectFit`** — crop behaviour (`objectFit` attribute)

| Member | Wire value | What it does |
|---|---|---|
| `Contain` | `contain` | The whole image is visible, letterboxed if the box and the image disagree about shape. The default on `PolarisImage`. |
| `Cover` | `cover` | Fills the box and crops the overflow — the choice for a tile that must keep its shape whatever the source is. |

**`PolarisImageLoading`** — fetch timing (`loading` attribute)

| Member | Wire value | What it does |
|---|---|---|
| `Eager` | `eager` | Fetches immediately, as the markup is parsed. The default. |
| `Lazy` | `lazy` | Defers the request until the image is near the viewport. |

**`PolarisImageInlineSize`** — width behaviour (`inlineSize` attribute)

| Member | Wire value | What it does |
|---|---|---|
| `Auto` | `auto` | Sized by the source image, which ignores the aspect ratio. Nothing renders until the image has loaded. |
| `Fill` | `fill` | Fills the available inline space and honours the aspect ratio. The default on `PolarisImage`. |

**`PolarisImageAccessibilityRole`** — how assistive technology treats it (`accessibilityRole` attribute)

| Member | Wire value | What it does |
|---|---|---|
| `None` | `none` | Removed from the accessibility tree entirely. |
| `Presentation` | `presentation` | Decorative; announced as nothing. |
| `Img` | `img` | An image with an alternative description. The default — pair it with `Alt`. |

## Behaviour

- The four parameters that carry these values (`ObjectFit`, `Loading`, `AccessibilityRole`,
  `InlineSize`) are non-nullable, so the library writes the attribute on every render. There is no way
  from C# to leave the element's own default in charge.
- `Fill` is what makes `AspectRatio` worth setting: the ratio is honoured, so the box exists before the
  bytes do and nothing moves when the image arrives. `Auto` ignores the ratio and renders nothing
  until the image has loaded, which appears as a layout shift at the moment it does — the fault is
  invisible in the markup and visible only on the page.
- The enum names are not the attribute names: `ObjectFit` → `objectFit`, `AccessibilityRole` →
  `accessibilityRole`, `InlineSize` → `inlineSize`. That mapping, and the wire strings, are the only
  things this file contributes.

## Gotchas

- **The `Auto` comment contradicts itself.** It reads "Sized by the source image, which ignores the
  aspect ratio. The default is fill." — `Fill` is the *component's* default, not `Auto`'s, and `Auto`
  is never a default anywhere. Read on its own, the sentence attributes `fill`'s default to `auto`.
- **`Contain` and `Cover` are the only fits.** There is no `fill`, `none` or `scale-down`, and no
  `Auto` that lets the browser decide, so a box whose shape differs from the image's will letterbox and
  `Cover` is the only way to fill it instead.
- **`Img` is the default while `Alt` is not set.** `PolarisImage.Alt` defaults to `null`, and the
  enum's own remark asks for `Alt` to be paired with `Img` because an `img` with no alternative text
  is announced poorly. The default combination is exactly the one the remark warns about; a decorative
  image wants `Presentation` or `None`.
- **`None` and `Presentation` are both silent, for different reasons.** `None` removes the image from
  the accessibility tree; `Presentation` keeps the node and marks it decorative. A screen-reader check
  cannot tell them apart, so pick on intent: `None` for something that is not an image at all,
  `Presentation` for a picture that carries no information.
- **The `[PolarisValue]` pins here are insurance, not a correction.** Unlike `large-100` or
  `small-200`, every member of these four enums is a single lower-case word, so the camel-case fallback
  in [PolarisAttributeValue.cs.md](PolarisAttributeValue.cs.md) would produce the same strings today.
  The pin is what keeps a future `ScaleDown` or `AutoFit` from becoming silently wrong markup.
- **`Loading` says nothing about an image with no source.** The placeholder is drawn while the image
  loads *and* whenever `Src` is absent, so `Eager` on a missing URL never triggers a fetch — there is
  nothing for the hint to eager-load.
- **An unstyled `s-image` is not a broken icon.** The element draws its own placeholder while it loads
  and whenever `Src` is absent, so a missing image is a visible state rather than a browser's broken
  icon; the difference between "deliberately empty" and "failed" is only visible through `OnError`.

## See also

- [PolarisImage.cs.md](Components/PolarisImage.cs.md) — the component, where these four are parameters
  alongside `Src`, `Alt`, `AspectRatio` and the border pieces.
- [PolarisAttributeValue.cs.md](PolarisAttributeValue.cs.md) — how a member and its `[PolarisValue]`
  become the attribute string, and why the camel-case fallback is not enough on its own.
- [PolarisEnums.cs.md](PolarisEnums.cs.md) — the shared enums (`PolarisTone`, `PolarisSize`) this file
  deliberately sits beside rather than reusing.
