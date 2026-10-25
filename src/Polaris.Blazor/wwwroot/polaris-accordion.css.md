<!-- summary-of: polaris-accordion.css -->
<!-- source-sha256: 0195ba7e2ff5 -->

# polaris-accordion.css

The accordion's motion and nothing else: a 200 ms transition on the panel's row height, a 200 ms
rotation on the chevron, and one divider suppression. The state itself — open or closed, the clipped
height, `inert` — is written inline by `PolarisAccordionItem`, so a page that never loads this file
still opens and closes correctly and merely stops animating.

`Polaris.Blazor/wwwroot` · static stylesheet · five rules, no custom properties

## Selectors

| Selector | What it affects |
|---|---|
| `[data-polaris-accordion-panel]` | `transition: grid-template-rows 200ms cubic-bezier(0.2, 0.8, 0, 1)`. The panel is a grid whose single row goes from `0fr` to `1fr`; that interpolates to the content's real height with no measurement and no JavaScript, which is why no height is ever hard-coded. |
| `[data-polaris-accordion-item]:first-child > s-divider` | `display: none`. Each section draws a rule above itself, which reads as the divider *between* sections; above the first it would double the accordion's own outer border. Layout, not appearance — the rule belongs to the section and the border to the container. |
| `[data-polaris-accordion-chevron]` | `display: inline-block; vertical-align: middle; transition: transform 200ms …`. The `display` is not cosmetic: `s-icon`'s host is `display: inline`, and a transform does not apply to a non-replaced inline box, so the rotation below would do nothing. |
| `[data-polaris-accordion-item][data-open="true"] [data-polaris-accordion-chevron]` | `transform: rotate(180deg)` — the only thing that makes the chevron point up while the section is open. |
| `@media (prefers-reduced-motion: reduce)` | Sets `transition: none` on the panel and the chevron. The states are unchanged, so the accordion still opens and closes; it simply snaps. |

## Behaviour

- **Optional by design.** Every rule here is motion or a cosmetic suppression. The disclosure works
  from markup alone: `grid-template-rows` and `inert` are inline styles written by the component, so a
  missing stylesheet degrades to a snap rather than to a panel that cannot collapse.
- **Loaded automatically.** `polaris-interop.js` lists this file in its `stylesheets` array and injects
  a `<link>` from its own module URL, so a consuming app wires nothing up. Linking it by hand also
  works, and is what the static web asset is for:
  `_content/Polaris.Blazor/polaris-accordion.css`.
- **Polaris is untouched.** The web components live in shadow roots, so nothing here can (or tries to)
  restyle one. Only the toolkit's own light-DOM wrappers carry these `data-` attributes.

## Gotchas

- **`data-open` must be exactly `"true"`.** The chevron rule is an attribute selector, so `data-open="false"`,
  `data-open=""` and a missing attribute all leave the chevron pointing down. A component that writes a
  .NET `bool` through `ToString()` produces `"True"` and silently loses the rotation.
- **`:first-child` means the first *element* child.** Anything rendered before the first accordion item
  — a heading, a toolbar, a stray `<div>` — takes the `:first-child` slot, so the first item's divider
  is drawn and the outer border is doubled.
- **The transition names one property.** It is `grid-template-rows`, matching the inline style the
  component writes. If the state were ever moved to `height` or `max-height`, this file would animate
  nothing and no test would notice.
- No colours and no custom properties are declared, so there is nothing here to theme; the merchant's
  admin theme supplies everything visible.

## See also

- [PolarisAccordionItem.cs.md](../Components/PolarisAccordionItem.cs.md) — writes the inline state and
  the `data-open` / `data-polaris-accordion-*` attributes these rules select.
- [PolarisAccordion.cs.md](../Components/PolarisAccordion.cs.md) — the container and its outer border.
- [polaris-interop.js.md](polaris-interop.js.md) — the loader that injects this stylesheet.
