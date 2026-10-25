<!-- summary-of: PolarisThumbnail.cs -->
<!-- source-sha256: 43f1e5232b98 -->

# PolarisThumbnail.cs

A small preview of content — a product photo, an uploaded file, a piece of media — drawn as
`<s-thumbnail>` with an optional alt text and a fixed size step. Use `PolarisImage` instead when the
picture is content in its own right rather than an identifier shown beside something else.

`Polaris.Blazor.Components` · renders `<s-thumbnail>` · base `PolarisElementComponentBase`

## Parameters

| Parameter | Type | Default | What it does |
|---|---|---|---|
| `Src` | `string?` | `null` | The image URL, written as `src`. Null omits the attribute, so the element receives no source; this wrapper substitutes no fallback image of its own. |
| `Alt` | `string?` | `null` | Alternative description, written as `alt`. Null omits the attribute entirely rather than writing an empty one, so an undescribed thumbnail has no `alt` at all. |
| `Size` | `PolarisSize` | `PolarisSize.Base` | The size step, rendered as `size` (`base`, `small`, `large`, `small-100` … `large-400`). |

Inherited from [PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md): `Id`,
`AccessibilityLabel`, `ItemTag`, `Slot`, `AdditionalAttributes`. There is **no** `ChildContent` — the
element takes no children, so a caption belongs in a sibling component such as `PolarisText`.

## Events

| Callback | Args | Raised when |
|---|---|---|
| `OnLoad` | `EventCallback<PolarisEventArgs>` | The image has loaded. `args.Type` is `"load"`; the event carries no value. |
| `OnError` | `EventCallback<PolarisEventArgs>` | The image failed to load — a 404, a CORS refusal, or a URL the Admin blocked. `args.Type` is `"error"`. |

## Gotchas

- **`Size` is always rendered, so it can never be "unset".** A non-nullable enum with a `Base` default
  means `size="base"` is emitted even when the caller never mentions the parameter. The wrapper therefore
  always states a size and can never fall back on the element's own default — which matters only if the
  two ever disagree.
- **An absent `Src` raises neither event.** There is no request to succeed or fail, so a page that waits
  for `OnError` before swapping in a placeholder image is never called for an item with no URL at all.
- **`AdditionalAttributes` wins over the modelled parameters.** The base class adds it last, so an entry
  named `src`, `alt` or `size` silently overrides `Src`, `Alt` and `Size`.
- **`Alt` is not the same thing as `AccessibilityLabel`.** The latter names the custom element's host,
  the former is the `alt` attribute of the image inside its shadow root; a thumbnail needs `Alt`.

## See also

- [PolarisImage.cs.md](PolarisImage.cs.md) — the same picture at its own aspect ratio, with a placeholder
  and `srcset` support.
- [PolarisIcon.cs.md](PolarisIcon.cs.md) — a glyph, when there is no picture at all.
- [PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md) — how the attributes,
  properties and event listeners are wired.
