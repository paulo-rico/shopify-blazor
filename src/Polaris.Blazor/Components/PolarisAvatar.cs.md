<!-- summary-of: PolarisAvatar.cs -->
<!-- source-sha256: 9659a0e31f4b -->

# PolarisAvatar.cs

A compact profile image — `<s-avatar>` — with initials as its built-in fallback, sized from the Polaris scale and reporting when the image loads or fails.

`Polaris.Blazor.Components` · renders `<s-avatar>` · base `PolarisElementComponentBase`

## Parameters

| Parameter | Type | Default | What it does |
|---|---|---|---|
| `Initials` | `string?` | `null` | Up to two characters shown when there is no image, and the fallback when `Src` cannot be used. More than two may truncate. |
| `Src` | `string?` | `null` | Image URL. When it fails to load, or the format is unsupported, the avatar falls back to `Initials`. A cross-origin URL needs CORS headers or the image is not usable. |
| `Alt` | `string?` | `null` | Alternative description of the image, for assistive technology. |
| `Size` | `PolarisSize` | `Base` | Avatar size. Polaris documents the small-through-large steps for this element. |

Inherited: `Id`, `AccessibilityLabel`, `ItemTag`, `Slot` and `AdditionalAttributes` (see
[PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md)). There is no `ChildContent` — the avatar's content is the image or the initials, not child markup.

## Events

| Callback | Args | Raised when |
|---|---|---|
| `OnLoad` | `EventCallback<PolarisEventArgs>` | The image at `Src` loads. |
| `OnError` | `EventCallback<PolarisEventArgs>` | The image at `Src` fails to load — the same condition that makes the element fall back to `Initials`. |

## Behaviour

- All four attributes are written on every render. `Initials`, `Src` and `Alt` are dropped when `null` (the base class omits null attributes); `Size` is always written, lower-cased through `PolarisAttributeValue.EnumValue`.
- `Src` and `Initials` are not alternatives at the attribute level — both are rendered when both are set. The element chooses: the image when it can display it, the initials when it cannot.
- The fallback is automatic and silent. `OnError` is how the application hears that the intended image did not arrive, for example to replace a broken URL or to log it.

## Gotchas

- **A cross-origin image without CORS headers is not usable, and the avatar does not say so** — it simply shows the initials instead. The broken URL looks exactly like a deliberately image-less avatar.
- **An unsupported image format also falls back to initials.** Success in `OnLoad` therefore means "the element accepted the resource", not "the pixels match what you expected".
- **More than two initials may truncate**, so do not rely on three-letter monograms fitting; compute the initials yourself if the rule matters.
- **A `null` `Alt` leaves the image undescribed.** Next to a visible name that is correct; an avatar standing alone as the only identifier needs `Alt`, or `AccessibilityLabel` on the element.
- With neither `Src` nor `Initials` the avatar has nothing to show, and nothing warns about it.
- Only the small-through-large steps are documented for the avatar; the numbered `PolarisSize` steps compile, but they are not part of that documented range.

## See also

- [PolarisThumbnail.cs.md](PolarisThumbnail.cs.md) — the same image-and-fallback idea for product media.
- [PolarisImage.cs.md](PolarisImage.cs.md) — a general image element with the same `load`/`error` events.
- [PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md) — the inherited parameters and attribute formatting.
