<!-- summary-of: polaris-accordion.css -->
<!-- source-sha256: d5d2f5591f98 -->

# polaris-accordion.css

The accordion's look and motion: the header band a native `<summary>` would otherwise not have, the
chevron's rotation, one divider suppression, and a 200 ms height transition on the panel where the
browser supports `::details-content`. The state itself — open or closed, and the fact that a closed
panel is not rendered — belongs to the browser's `<details>`, so a page that never loads this file
still opens and closes correctly; it merely loses the band and the animation.

`Polaris.Blazor/wwwroot` · static stylesheet · appearance plus motion, no custom properties

## Selectors

| Selector | What it affects |
|---|---|
| `[data-polaris-accordion-item] > details > summary` | The header band: `display: flex; justify-content: space-between`, 1rem padding, `cursor: pointer`, `list-style: none`, and `background: var(--p-color-bg-surface-secondary, #f7f7f7)`. The summary is the *row* — the component deliberately does not wrap the header's two groups in a stack, because a Polaris host computes to `display: contents` and a wrapper would be one shrink-wrapped flex item. One flat background is the point: an action and the title share it, and there is no hover rule, which is what makes them read as one surface. |
| `summary::-webkit-details-marker` | `display: none`. The default marker would sit beside the component's own chevron. `list-style: none` on the summary covers the engines that use the list-item spelling. |
| `[data-polaris-accordion-item]:first-child > s-divider` | `display: none`. Each section draws a rule above itself, which reads as the divider *between* sections; above the first it would double the accordion's own outer border. Layout, not appearance — the rule belongs to the section and the border to the container. |
| `[data-polaris-accordion-chevron]` | `display: inline-block; vertical-align: middle; transition: transform 200ms …`. The `display` is not cosmetic: `s-icon`'s host is `display: inline`, and a transform does not apply to a non-replaced inline box, so the rotation below would do nothing. |
| `[data-polaris-accordion-item] > details[open] [data-polaris-accordion-chevron]` | `transform: rotate(180deg)` — the only thing that makes the chevron point up while the section is open. `details[open]` is the state, read from the element the browser owns. |
| `@supports selector(::details-content)` on the item and `details::details-content` | `interpolate-size: allow-keywords` on the item (so `block-size: auto` becomes a value a transition can reach) and a `block-size` transition plus `content-visibility … allow-discrete` on the pseudo-element, with `block-size: auto` when open. Scoped to the item rather than to `:root`, so a library does not change height animation for the whole page. |
| `@media (prefers-reduced-motion: reduce)` | Sets `transition: none` on the panel and the chevron. The states are unchanged, so the accordion still opens and closes; it simply snaps. |

## Behaviour

- **Appearance is here now, not in Polaris.** The component's header used to be an `s-clickable`, which
  painted its own band and hover from inside a shadow root. A native summary has no opinion about either,
  so the band is this file's — built from the admin's tokens with the colours they resolve to as
  fallbacks, exactly as the jeweller app's own disclosures are.
- **Still optional by design.** Nothing here is required for the disclosure to work: `<details>` owns
  the open state, the keyboard, the announcement and the non-rendering of a closed panel. A missing
  stylesheet leaves a working disclosure showing the browser's default marker.
- **Loaded automatically.** `polaris-interop.js` lists this file in its `stylesheets` array and injects a
  `<link>` from its own module URL, so a consuming app wires nothing up. Linking it by hand also works,
  and is what the static web asset is for: `_content/Polaris.Blazor/polaris-accordion.css`.
- **Polaris is untouched.** The web components live in shadow roots, so nothing here can (or tries to)
  restyle one. Only the toolkit's own light-DOM wrappers carry these `data-` attributes.

## Gotchas

- **The state is `details[open]`, and there is no `data-open` mirror.** An earlier version kept one, which
  made two ways to describe the same thing; the chevron rule now reads the attribute the browser sets.
- **`::details-content` is the only way to animate a native disclosure, and it is recent.** A browser
  without it reports the `@supports` block as false and the panel snaps — visible in Firefox today. The
  probe accepts that, because a snap is the design and not a failure.
- **`interpolate-size` is inherited.** It is set on `[data-polaris-accordion-item]`, never on `:root`: a
  library that set it globally would change every `height: auto` transition in the consuming app.
- **`:first-child` means the first *element* child.** Anything rendered before the first accordion item
  — a heading, a toolbar, a stray `<div>` — takes the `:first-child` slot, so the first item's divider is
  drawn and the outer border is doubled.
- **The band's background is the surface-secondary token.** The fallback (`#f7f7f7`) is the colour it
  resolves to in the admin today; a Polaris release that renames the token degrades to that grey rather
  than to no band at all.

## See also

- [PolarisAccordionItem.cs.md](../Components/PolarisAccordionItem.cs.md) — the markup these rules select,
  and why the summary is the row.
- [PolarisAccordion.cs.md](../Components/PolarisAccordion.cs.md) — the container and its outer border.
- [polaris-interop.js.md](polaris-interop.js.md) — the loader that injects this stylesheet.
