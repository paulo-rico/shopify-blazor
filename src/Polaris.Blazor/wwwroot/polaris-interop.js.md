<!-- summary-of: polaris-interop.js -->
<!-- source-sha256: df344483fcf8 -->

# polaris-interop.js

The browser half of `IPolarisInterop`: it waits for elements to upgrade, sets DOM properties, forwards
DOM events to .NET and loads the package's stylesheets. It also writes `aria-expanded` into a shadow
button and reports which elements actually registered. It holds no application logic on purpose — if
this file ever decides what a click means, the decision belongs in C#.

`Polaris.Blazor/wwwroot` · ES module loaded from `/_content/Polaris.Blazor/polaris-interop.js` · thirteen exports, no dependencies

## Members

| Function | Signature | What it does |
|---|---|---|
| `ensureStylesheets` | `(): void` | Injects a `<link>` for each of the three stylesheets, skipping any already present (matched on `link[data-polaris-stylesheet="<href>"]`). If `document.head` does not exist yet, defers to `DOMContentLoaded` once. Also called once at module scope, so importing the module is enough. |
| `whenDefined` | `(tagName: string, timeoutMs = 10000): Promise<boolean>` | Resolves `true` once the element is defined, `false` on timeout. An empty tag name resolves `true` immediately; an already-defined element resolves without waiting. |
| `ensureId` | `(element: HTMLElement): string` | Returns the element's id, assigning `polaris-blazor-<n>` from a module counter when it has none. Throws when handed no element. |
| `setProperties` | `(element, tagName, values: Record<string, unknown>): Promise<void>` | Awaits upgrade, then assigns each non-`null`/non-`undefined` value as a DOM **property**. A boolean is written in both forms — property plus attribute presence (`setAttribute(name, "")` / `removeAttribute(name)`) — because Polaris reflects booleans from attribute presence. If any name is in `RENDER_ONLY_PROPERTIES`, calls the element's `queueRender()` once at the end. |
| `focusWithin` | `(element, selector = null): Promise<boolean>` | Finds the first focusable thing at or inside `element` — descending through the light DOM and through **one** shadow boundary, to a depth of 8 — focuses it, and reports whether focus landed. With a `selector`, starts from the first matching descendant instead of the element itself. Retries on `requestAnimationFrame` up to 10 times while the element upgrades. |
| `setShadowButtonExpanded` | `(element, expanded: boolean): Promise<boolean>` | Awaits upgrade, then writes `aria-expanded="true"`/`"false"` onto the `<button>` in the element's shadow root. Returns `false` when there is no such button rather than throwing. |
| `clearProperties` | `(element, tagName, names: string[]): Promise<void>` | Awaits upgrade, then resets each named property: an array-valued property becomes `[]`, anything else becomes `undefined`. |
| `attachEvents` | `(element, eventNames: string[], dotNetRef, handle: string, tagName): Promise<void>` | A no-op without a ref or without event names. Otherwise detaches any previous listeners for the element, awaits upgrade, and adds one listener per name that invokes `Dispatch(handle, eventName, readEvent(event))` on the .NET reference. Registrations are kept in a module-level `Map` keyed by element. |
| `invokeElementMethod` | `(element, methodName: string, args: unknown[]): Promise<unknown>` | Awaits upgrade, then calls the named method on the element with `args` (or none) and returns its result. Throws a named error when the element has no such method. |
| `detachEvents` | `(element): void` | Removes every listener this module attached to the element and forgets the registration. Safe to call twice, and on an element that was never registered. |
| `release` | `(element): void` | Calls `detachEvents` and nothing else — the .NET reference is deliberately **not** disposed here, because the owning component reuses it across re-renders. |
| `componentStatus` | `(tagNames: string[]): Record<string, { defined: boolean; upgraded: boolean; count: number }>` | Per tag name: whether `customElements.get` found a constructor, whether every element of that name in the document is an instance of it (i.e. all upgraded), and how many there are. |
| `isEmbedded` | `(): boolean` | `true` when `window.shopify` exists, i.e. the page is running inside the Shopify Admin. |

## Behaviour

- **Upgrade first, write second.** Every function that touches an element awaits `whenDefined`. Until
  Polaris's CDN bundle upgrades it, the element is a plain `HTMLElement` with none of its properties,
  and assigning `value` would set an expando that the upgrade discards — the field then renders empty
  with nothing logged.
- **The event payload** (`readEvent`, private) is what reaches `PolarisEventArgs`: `type`, `value`
  (the event's own string value, else the target's), `values` (array, else `null`), `checked`
  (`true`/`false`, else `null`), `detail` (the event's `detail` object with functions and DOM nodes
  removed and unserializable entries dropped, or `null`), `key`, and `altKey` / `ctrlKey` / `metaKey` /
  `shiftKey`. Element references and functions cannot cross the interop boundary, so they are dropped
  and replaced by the value, the target's identity and the modifiers.
- **`RENDER_ONLY_PROPERTIES` is the escape hatch for a non-reactive Polaris property.** It currently
  holds only `accessibilityExpanded`: Polaris's render function reads it but nothing schedules a render,
  so writing it alone changes nothing on screen. `queueRender()` is not documented API, so the call is
  defensive — if a future Polaris renames it, the property has still been written and simply takes
  effect on the next render the element does anyway. The state is late rather than wrong.
- **The shadow search is whole-root on purpose.** A shadow root is opaque to `querySelector` from
  outside, and a Polaris shadow root begins with a `<style>`, so descending from `firstElementChild`
  finds nothing. Searching the root directly is what lets the grid's editor receive the caret instead
  of `body`.
- **`document.activeElement` is the outermost host of a focused shadow tree**, so the proof that focus
  arrived is checked on the target's own root, not on the document.

## Gotchas

- **The `whenDefined` timeout is not surfaced.** `setProperties`, `attachEvents` and `clearProperties`
  ignore its return value and carry on. If the CDN bundle never arrives, properties are written to an
  unupgraded element, the upgrade later discards them, and the page renders perfectly with empty fields.
  Only `componentStatus` tells you this happened.
- **`clearProperties` never writes `undefined` over an array.** Polaris reads `s-choice-list.values`
  with array methods, so `undefined` throws `TypeError: Cannot read properties of undefined (reading
  'join')` inside the component, six times per render, while the markup looks right. Arrays are cleared
  to `[]` for that reason and not for tidiness.
- **`attachEvents` detaches and re-attaches every time it is called**, so it is safe to call on every
  parameter pass but not free. A listener added by anything other than this module is not tracked and
  will survive `detachEvents`.
- **The registry is module-level and permanent.** An element from a closed circuit stays in the `Map`
  until `detachEvents`/`release` runs; if a component forgets to dispose, both the listeners and the
  .NET reference leak for the life of the page.
- **A boolean is written twice, so setting the attribute elsewhere un-sets the property.** Writing
  `disabled="false"` by other means is still `true` to the custom element; a `false` here removes the
  attribute, and any later plain attribute write can re-enable it.
- **`ensureStylesheets` is guarded by the href, not by a flag**, because the module can be loaded more
  than once (a second circuit, a hot reload) and a duplicate `<link>` would be a second request for the
  same file.
- **`invokeElementMethod` throws on a missing method**, and there is no capability probe; if Polaris
  removes `showOverlay`, the call site fails at runtime rather than at build time.

## See also

- [PolarisInterop.cs.md](../Interop/PolarisInterop.cs.md) — the C# service that imports this module and
  exposes it to components.
- [PolarisEventDispatcher.cs.md](../Interop/PolarisEventDispatcher.cs.md) — the `Dispatch` method the
  listeners call, and the object that turns the payload into `PolarisEventArgs`.
- [PolarisEventArgs.cs.md](../Events/PolarisEventArgs.cs.md) — the deserialized shape of an event.
- [polaris-accordion.css.md](polaris-accordion.css.md), [polaris-grid.css.md](polaris-grid.css.md),
  [polaris-range-slider.css.md](polaris-range-slider.css.md) — the three files it injects.
