<!-- summary-of: PolarisImage.cs -->
<!-- source-sha256: 9025133ee218 -->

# PolarisImage.cs

An `<s-image>` that shows a placeholder instead of a broken icon while it loads and whenever it has no
source, and reports load and failure as event callbacks so a page can react to an image that never
arrives.

`Polaris.Blazor.Components` · renders `<s-image>` · base `PolarisElementComponentBase`

## Parameters

| Parameter | Type | Default | What it does |
|---|---|---|---|
| `Src` | `string?` | `null` | The image URL. Absent, the element draws its placeholder — a visible state rather than a broken icon. |
| `SrcSet` | `string?` | `null` | Candidate URLs for a responsive image, in the native `srcset` list syntax (`url 1x, url 2x`). |
| `Sizes` | `string?` | `null` | The `sizes` descriptor matching `SrcSet`. Has no effect without a `SrcSet`. |
| `Alt` | `string?` | `null` | Alternative description. Empty by default, which leaves the image unannounced — set it for any image that carries meaning. |
| `AspectRatio` | `string?` | `null` | Ratio as a number or a fraction: `1.5`, `16/9`. |
| `ObjectFit` | `PolarisImageObjectFit` | `Contain` | `Contain` shows the whole image, letterboxed if needed; `Cover` fills the box and crops the overflow. |
| `Loading` | `PolarisImageLoading` | `Eager` | `Eager` fetches immediately; `Lazy` waits until the image is near the viewport. |
| `AccessibilityRole` | `PolarisImageAccessibilityRole` | `Img` | `Img` (an image with alternative text — pair it with `Alt`), `Presentation` (decorative, announced as nothing) or `None` (removed from the accessibility tree entirely). |
| `InlineSize` | `PolarisImageInlineSize` | `Fill` | Width behaviour. `Fill` fills the available inline space and honours `AspectRatio`; `Auto` sizes from the source image and ignores the ratio. |
| `Border` | `string?` | `null` | Border treatment, for example `base`. |
| `BorderWidth` | `string?` | `null` | Border width. |
| `BorderStyle` | `string?` | `null` | Border style, for example `solid`. |
| `BorderColor` | `string?` | `null` | Border colour. |
| `BorderRadius` | `string?` | `null` | Corner radius, for example `base` or `full`. |

Inherited: `Id`, `AccessibilityLabel`, `ItemTag`, `Slot` and `AdditionalAttributes` from
[PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md). There is no `ChildContent`.

## Events

| Callback | Args | Raised when |
|---|---|---|
| `OnLoad` | `EventCallback<PolarisEventArgs>` | The image has loaded — the moment an `Auto`-sized image finally has a box. |
| `OnError` | `EventCallback<PolarisEventArgs>` | The image failed to load: a 404, a CORS refusal, or a blocked URL. Does not fire for an absent `Src`. |

## Behaviour

- **The placeholder is the missing-image design, not a fallback.** It is shown while the image loads
  *and* whenever `Src` is absent, so a product with no photograph looks deliberate rather than broken.
- **`InlineSize` decides whether the box exists before the bytes do.** With the default `Fill` the
  aspect ratio is honoured, so the space is reserved up front and nothing moves. With `Auto` the ratio
  is ignored and **nothing renders until the image has loaded**, which shows as a layout shift at the
  moment the bytes arrive.
- Border attributes are separate rather than a shorthand here: `Border` is the treatment name, and the
  width, style and colour parameters are the pieces.

## Gotchas

- **`Alt` defaults to empty, and an empty alt is not "no opinion".** An `<img>` with no alternative text
  is announced poorly; set `Alt` for a meaningful image and `AccessibilityRole="Presentation"` for a
  decorative one.
- **`Auto` plus `AspectRatio` is a contradiction.** The ratio is ignored in `Auto`, so the layout shift
  is the only thing the ratio was meant to prevent — keep the default `Fill` when the ratio matters.
- **`OnError` does not fire for a missing `Src`.** There is no request to fail, so a page that relies on
  the error callback to swap in a fallback image will never be called for an item that has no URL.
- **A 404 in the Admin can be a URL the Admin blocked, not a missing file.** The error carries no
  distinction between the two; check the network response rather than the callback.
- Border and radius are applied to the element's box, which is why they work with `Fill` and have little
  visible effect with `Auto` before the image has loaded.

## See also

- [PolarisThumbnail.cs.md](PolarisThumbnail.cs.md) — the small fixed-size image used in tables.
- [PolarisIcon.cs.md](PolarisIcon.cs.md) — the glyph alternative when there is no picture at all.
- [PolarisAvatar.cs.md](PolarisAvatar.cs.md) — a picture treated as a person, with its own fallback.
