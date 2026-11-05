<!-- summary-of: PolarisAccordionItem.cs -->
<!-- source-sha256: 137b87c82a89 -->

# PolarisAccordionItem.cs

One section of a `PolarisAccordion`: a clickable header with heading, details, thumbnail, an optional state badge and an optional action, above a content panel that clips itself shut. It is composed from Polaris elements because no Polaris accordion element exists.

`Polaris.Blazor.Components` · composed section, no Polaris element of its own · base `ComponentBase`, `IDisposable`

## Parameters

| Parameter | Type | Default | What it does |
|---|---|---|---|
| `Header` | `string?` | `null` | The heading text. Rendered as the `s-section` heading inside the header and used to build the header's accessible name. |
| `HeaderDetails` | `string?` | `null` | A supporting line under the heading, rendered as the section's `subheading` — Polaris styles it between the heading and the panel. Also appended to the accessible name. |
| `HeaderMedia` | `RenderFragment?` | `null` | Content to the left of both text lines, in practice a thumbnail. It is centred against the heading and supporting line together, and stays visible while the section is closed. Carries no accessible name of its own. |
| `HeaderStatus` | `string?` | `null` | A short state badge drawn beside the heading, on the heading's own line, and visible while the section is closed. It goes into the section's `accessory` slot. Its text is appended to the accessible name, so the state is announced. Blank or whitespace renders nothing. |
| `HeaderStatusTone` | `PolarisTone` | `Auto` | Semantic colour of that badge. Ignored when `HeaderStatus` is not set. |
| `HeaderActions` | `RenderFragment?` | `null` | An action at the inline end of the header, immediately before the chevron — in practice a button that decides what the section is about without opening it. Rendered **outside** the clickable surface, and its wrapper stops the click so it does not also toggle the section. |
| `ChildContent` | `RenderFragment?` | `null` | The panel content. Wrapped in an `s-box` whose padding is `ContentPadding`; clipped and `inert` while closed. |
| `Value` | `string?` | `null` | An identifier reported to the accordion's `OpenItemChanged`, so code outside can tell which section is showing. Purely a token — it is not rendered. |
| `InitiallyOpen` | `bool` | `false` | Starts open. Read once during `OnInitialized`; in an exclusive accordion only the first item asking for it keeps it. |
| `ContentPadding` | `string?` | `"base"` | Polaris spacing step for the panel's `s-box` padding, passed through verbatim. `None` (or `"none"`) for content that supplies its own, such as a table. |
| `ShowDivider` | `bool` | `true` | Draws an `s-divider` above the section. With the default every section has one, which reads as rules between sections — and one above the first, which the stylesheet removes. |

Inherited: none beyond `ComponentBase`; this component does not derive from `PolarisElementComponentBase`, so there is no `Id`, `AccessibilityLabel`, `Slot` or `AdditionalAttributes` here. The accordion arrives through a private `[CascadingParameter]`.

## Events

| Callback | Args | Raised when |
|---|---|---|
| `IsOpenChanged` | `EventCallback<bool>` | The open state actually changes — by the merchant's click, by `ToggleAsync()`, or by a sibling opening and collapsing this one. The args carry the new state. Not raised for the initial `InitiallyOpen` value, and not raised when a set is a no-op. |

## Members

| Signature | What it does |
|---|---|
| `bool IsOpen { get; }` | The current open state. Read-only: there is no public setter and no `Open` parameter, so a parent cannot force a section open or closed. |
| `Task ToggleAsync()` | Opens a closed section or closes an open one. When it opens inside an accordion it first collapses the siblings, then reports through the container's `OpenItemChanged`. |
| `void Dispose()` | Unregisters from the accordion so a disposed item cannot be collapsed by a sibling; idempotent because it guards on a `_disposed` flag. |

## Behaviour

- **The markup, outermost first.** A `div[data-polaris-accordion-item][data-open="true|false"]` holds an optional `PolarisDivider`; a `div[data-polaris-accordion-header]` carrying Blazor's own `@onclick` and `display:flex;align-items:stretch`; then the panel, a `div[data-polaris-accordion-panel]` with the inline style `display:grid;overflow:hidden;grid-template-rows:1fr|0fr`, an inner `div[data-polaris-accordion-panel-inner]` with `min-height:0;overflow:hidden`, and an `s-box` for the content.
- **The header is a row of two columns.** The first is the `PolarisClickable` (`AccessibilityExpanded` expanded/collapsed, `Background="subdued"`, `Padding="base"`, `style="flex:1 1 0;min-width:0"`) holding the heading; the second is a `div[data-polaris-accordion-trailing]` carrying `HeaderActions` and then a small, subdued `chevron-down` `PolarisIcon` with `data-polaris-accordion-chevron`. The trailing column repeats the surface's own background and 1rem inset through Polaris's tokens, so the band and the chevron's position look unchanged.
- **Why the surface is not `flex: 1 1 auto`.** `s-clickable` sizes its shadow button to `inline-size: 100%`; a percentage flex basis would claim the whole row. `flex-basis: 0` ignores it and lets the trailing column have its width.
- **Inside the surface**, the `PolarisSection` is the only child, or — when `HeaderMedia` is set — sits in an inline `PolarisStack` (`AlignItems="center"`) with the media first, so a 100px picture stands beside both text lines rather than level with the heading alone.
- **The state badge goes into the section's `accessory` slot.** That slot is the only place Polaris renders content inline after the heading text. As a sibling of the section the badge was pushed to the far edge of the surface instead, because `s-section` is a block that fills the width it is given. Polaris warns if an accessory badge is not `size="base"`/`color="base"`; both are `PolarisBadge`'s defaults, so the tone is what carries the meaning.
- **Why the actions are outside the surface.** `s-clickable` renders a `<button>` in its shadow root whose children are the slotted content, so a button placed inside it would be a button within a button. The `button` role's children are presentational, so such an action would not merely be invalid markup — it would leave the accessibility tree and no screen reader would reach it.
- **Why the handler is Blazor's, not an interop listener.** A listener on the Polaris element is detached and re-attached after every render, and every toggle re-renders the page that binds `OpenItemChanged`; whether a click landed while the listener was attached therefore depended on render history, which produced intermittent "the click does nothing". A click is a composed event, so one `@onclick` on the row handles the heading, the chevron, the padding, and the click a keyboard Enter or Space produces on the real `<button>` inside Polaris's shadow root.
- **State is in the markup, motion is in the stylesheet.** `grid-template-rows` is written inline, so a page that never loads `polaris-accordion.css` still opens and closes — it only stops animating. The stylesheet supplies the 200 ms transition, rotates the chevron from `[data-open="true"]`, hides the first item's divider, and disables transitions under `prefers-reduced-motion: reduce`.
- **A closed panel is `inert` as well as clipped**, so content hidden by `overflow:hidden` leaves the tab order and the accessibility tree instead of being reachable off-screen.
- **The header's accessible name is stated, not derived.** A heading set as an attribute lives in Polaris's shadow root, leaving the clickable surface with no text; the name joins `Header`, `HeaderDetails` and `HeaderStatus` with `". "` — skipping the blank ones, and dropping each part's trailing full stop so a sentence-style supporting line is not announced as "…and alloy.. Alloy weights on 2 of 6 variants" — and is passed as `AccessibilityLabel`. The status is in it because it is the one part of the header a merchant must hear without opening the section: a badge drawn as a `RenderFragment` would be projected into the shadow root and never announced.
- **Opening inside an accordion batches the changes.** `ToggleAsync` awaits `CollapseOthersAsync` before setting its own state open, so both row changes are part of one render batch and the two transitions run together.
- Nothing in `ToggleAsync` uses `ConfigureAwait(false)` on purpose: it ends in `StateHasChanged`, which must run on the renderer's dispatcher. A continuation resumed on the thread pool throws "The current thread is not associated with the Dispatcher" — and only once a handler genuinely awaits.

## Gotchas

- **`InitiallyOpen` is read once, in `OnInitialized`.** Setting it after the first render changes nothing, and there is no open/closed parameter to bind.
- **The only public way to change the state is `ToggleAsync()`.** There is no `OpenAsync`/`CloseAsync`, and `SetOpenAsync` is `internal`; a parent that wants a specific section open must know whether it is currently closed before toggling.
- **Two initially-open items in an exclusive accordion: the later one silently starts collapsed.** The container arbitrates by document order and nothing raises an event about the dropped request.
- **A missing `Header` and `HeaderDetails` leaves the header with no accessible name.** The visible text lives in the shadow root, so without the parameters the surface is announced as an unlabelled button containing only an icon.
- **`HeaderMedia` is invisible to assistive technology by design** — the name is built from the text parameters. Give a thumbnail that merely illustrates the title `alt=""`; a meaningful image needs its own description, and duplicating the heading in it is noise. `HeaderStatus` is the opposite: it is a string precisely so it joins that name.
- **`HeaderStatus` is for a short state, not a sentence.** It renders as an `s-badge`, which truncates to one line; a long status is clipped rather than wrapped, and the fix is to shorten the text, not to widen the badge. Whitespace-only counts as unset, so a page can pass an expression that is sometimes blank without drawing an empty pill.
- **`HeaderActions` is markup, so its text is not in the accessible name — and does not need to be.** The surface's name covers the heading and its status; the action is a separate control with its own button element and its own name. What it must not do is depend on the caller for the click handling: the stopPropagation wrapper belongs to this component.
- **The trailing column's background uses Polaris's stamped token** (`--p-color-bg-surface-secondary-6e076`) with the colour it resolves to as the fallback, because the admin defines only stamped names. A Polaris release that changes the stamp loses theme overrides but not the band: the fallback is the same `#f7f7f7`.
- **The click target is the surface, not the whole row.** Pressing the padding, the heading or the chevron toggles; pressing an action does not, because the wrapper stops the click before the row's handler sees it (and the button's own listener runs first, as the event's target). Hover feedback darkens only the surface, so a header with actions has a still strip at its end.
- **`ContentPadding` is a raw string, not an enum.** A misspelled step is ignored by Polaris, which applies no padding at all, with nothing logged and nothing in the markup to show it.
- **`ShowDivider` defaults to `true`, including on the first item.** The rule above the first section would double the accordion's own outer border; the stylesheet hides `[data-polaris-accordion-item]:first-child > s-divider`. Without that stylesheet the first section has two rules.
- **The `inert` attribute does the accessibility work, not the clipping.** A browser that ignores `inert` still hides the content visually but leaves it focusable and announced.
- **Disposing an open section unregisters it without raising `OpenItemChanged(null)`**, so a page mirroring the open `Value` keeps the stale value until something else toggles.

## See also

- [PolarisAccordion.cs.md](PolarisAccordion.cs.md) — the container, exclusivity and `OpenItemChanged`.
- [PolarisDivider.cs.md](PolarisDivider.cs.md) — the rule drawn above each section.
- [PolarisIcon.cs.md](PolarisIcon.cs.md) — the chevron and its icon-name constraints.
- [PolarisClickable.cs.md](PolarisClickable.cs.md) — the surface, and why it is a button in a shadow root.
- [PolarisBadge.cs.md](PolarisBadge.cs.md) — the status badge's defaults, which the accessory slot requires.
