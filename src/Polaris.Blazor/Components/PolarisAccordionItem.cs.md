<!-- summary-of: PolarisAccordionItem.cs -->
<!-- source-sha256: 017f0bb8d94f -->

# PolarisAccordionItem.cs

One section of a `PolarisAccordion`: a browser-native `<details>`/`<summary>` disclosure — the summary is the header, carrying a heading, a supporting line, a state badge and optional actions, and the content beneath it is Polaris-boxed. It is composed rather than wrapped because Polaris has no accordion element.

`Polaris.Blazor.Components` · renders `<details>`/`<summary>` · base `ComponentBase`, `IDisposable`

## Parameters

| Parameter | Type | Default | What it does |
|---|---|---|---|
| `Header` | `string?` | `null` | The heading, drawn as a `PolarisHeading` in the summary's light DOM. It is the first id the summary's `aria-labelledby` points at. |
| `HeaderDetails` | `string?` | `null` | A supporting line under the heading, drawn as subdued small `PolarisText`. Also referenced by `aria-labelledby`, so it is announced with the heading rather than skipped. |
| `HeaderMedia` | `RenderFragment?` | `null` | Content to the left of both text lines, in practice a thumbnail, centred against the pair. Stays visible while the section is closed. Carries no accessible name of its own. |
| `HeaderStatus` | `string?` | `null` | A short state badge beside the heading, on the heading's own line, visible while closed. A string plus a tone rather than markup, so it needs no separate label and joins the referenced name. Blank or whitespace renders nothing. |
| `HeaderStatusTone` | `PolarisTone` | `Auto` | Semantic colour of that badge. Ignored when `HeaderStatus` is not set. |
| `HeaderActions` | `RenderFragment?` | `null` | Buttons at the inline end of the header, before the chevron — the capability the old `s-clickable` header could not offer. Wrapped in an element that stops the click and prevents its default, so pressing one runs it without opening the section. Buttons only: the wrapper's `preventDefault` would cancel a link's navigation. |
| `ChildContent` | `RenderFragment?` | `null` | The panel content, wrapped in an `s-box` padded by `ContentPadding`. A closed `<details>` does not render it at all. |
| `Value` | `string?` | `null` | An identifier reported to the accordion's `OpenItemChanged`. Purely a token — it is not rendered. |
| `InitiallyOpen` | `bool` | `false` | Starts open, read once during `OnInitialized`. In an exclusive accordion only the first item asking keeps it. |
| `ContentPadding` | `string?` | `"base"` | Polaris spacing step for the panel's `s-box` padding, passed through verbatim. `None` (or `"none"`) for content that supplies its own, such as a table. |
| `ShowDivider` | `bool` | `true` | Draws an `s-divider` above the section, outside the `<details>` (a `<details>` is "an optional summary followed by flow content"). The stylesheet hides the first one. |

Inherited: none beyond `ComponentBase`; this component does not derive from `PolarisElementComponentBase`, so there is no `Id`, `AccessibilityLabel`, `Slot` or `AdditionalAttributes`. The accordion arrives through a private `[CascadingParameter]`.

## Events

| Callback | Args | Raised when |
|---|---|---|
| `IsOpenChanged` | `EventCallback<bool>` | The open state actually changes — by the merchant's click, by `ToggleAsync()`, or by a sibling opening and collapsing this one. Not raised for the initial `InitiallyOpen` value, and not raised when a set is a no-op. |

## Members

| Signature | What it does |
|---|---|
| `bool IsOpen { get; }` | The current open state. Read-only: no setter, and no `Open` parameter, so a parent cannot force a section open or closed. |
| `Task ToggleAsync()` | Opens a closed section or closes an open one. Inside an accordion it first collapses the siblings, then reports through `OpenItemChanged`. |
| `void Dispose()` | Unregisters from the accordion so a disposed item cannot be collapsed by a sibling; idempotent behind a `_disposed` flag. |

## Behaviour

- **The disclosure is the browser's.** `<details>` owns the open state, the disclosure semantics, the keyboard (Space and Enter), focus, and the fact that a closed panel is not rendered. Nothing here reimplements any of it: no `aria-expanded` interop, no `inert`, no grid-row clipping, no accessible name assembled by hand.
- **The markup, outermost first.** A `div[data-polaris-accordion-item]` holds an optional `PolarisDivider`, then a `<details>` with an `open` attribute present only while open, a `<summary data-polaris-accordion-header>` with Blazor's own `@onclick`, an optional `aria-labelledby`, and the header's two groups; then the `s-box` panel.
- **The summary is the row.** It is `display:flex; justify-content:space-between` from the stylesheet, with the text group first and the actions-plus-chevron group second. There is deliberately no stack wrapped around the two groups: a Polaris host computes to `display: contents`, so a wrapper would be one shrink-wrapped flex item, and measured, the chevron sat about 300px short of the header's edge while a click at the header's centre landed on the action.
- **The accessible name is referenced, not restated.** `aria-labelledby` lists the heading, the supporting line and the status badge — whichever exist. Because it references the drawn elements, the name cannot drift from what is on screen, and the action's own label stays out of it (measured: without it the disclosure read "Variants … Clear all reviews").
- **The action wrapper carries `stopPropagation` and `preventDefault`.** The first keeps the component's own header handler from running; the second stops a browser that does not skip the disclosure activation for interactive content from opening a panel the component would never hear about — which would leave it open while the C# state said closed.
- **State is `open`, and it is the only source of truth.** There is no `data-open` mirror. The click handler flips the same state the browser just flipped, so the re-render's attribute write is a no-op and the two can never disagree.
- **Motion lives in `polaris-accordion.css`** (`::details-content` `block-size`, the chevron rotation), scoped so a library does not change height animation for the whole page. A browser without `::details-content` opens instantly, which is the intended degradation.

## Gotchas

- **`ChildContent` must be named once a section carries `HeaderActions` or `HeaderMedia`.** Razor cannot attribute unnamed content when a component has more than one `RenderFragment` parameter (`RZ9996`), so the panel becomes `<ChildContent>…</ChildContent>`. A section with neither keeps the bare form.
- **`HeaderActions` is for buttons.** The wrapper's `preventDefault` cancels the default action of whatever is inside it, so a link there would silently not navigate and a checkbox would not toggle. An action that navigates belongs at the top of the panel.
- **`InitiallyOpen` is read once, in `OnInitialized`.** Setting it after the first render changes nothing.
- **The only public way to change the state is `ToggleAsync()`.** `SetOpenAsync` is `internal`.
- **Two initially-open items in an exclusive accordion: the later one silently starts collapsed.** The container arbitrates by document order and nothing raises an event.
- **A missing `Header` and `HeaderDetails` leaves the header with no accessible name** — no ids means no `aria-labelledby`, and the summary's content is only the chevron and any action.
- **The header's look is this library's, not Polaris's.** The band, padding and radius come from `polaris-accordion.css`; without it the header still works and shows the browser's default marker beside the chevron. That is the deliberate cost of a summary that can carry an action.
- **`HeaderStatus` is for a short state, not a sentence.** It renders as an `s-badge`, which truncates to one line. Whitespace-only counts as unset.
- **`HeaderMedia` is invisible to assistive technology by design** — the name is built from the text parameters. Give a thumbnail that merely illustrates the title `alt=""`.
- **`ContentPadding` is a raw string, not an enum.** A misspelled step is ignored by Polaris, applying no padding at all.
- **Disposing an open section unregisters it without raising `OpenItemChanged(null)`**, so a page mirroring the open `Value` keeps the stale value until something else toggles.

## See also

- [PolarisAccordion.cs.md](PolarisAccordion.cs.md) — the container, exclusivity and `OpenItemChanged`.
- [PolarisDivider.cs.md](PolarisDivider.cs.md) — the rule drawn above each section.
- [PolarisHeading.cs.md](PolarisHeading.cs.md), [PolarisText.cs.md](PolarisText.cs.md) — the header's two text elements, which are light DOM and therefore nameable.
- [PolarisBadge.cs.md](PolarisBadge.cs.md) — the status badge.
- [PolarisIcon.cs.md](PolarisIcon.cs.md) — the chevron.
- [PolarisBox.cs.md](PolarisBox.cs.md) — the panel's padding.
