<!-- summary-of: PolarisAccordionItem.cs -->
<!-- source-sha256: 258e8f59aef6 -->

# PolarisAccordionItem.cs

One section of a `PolarisAccordion`: a clickable header with heading, optional details, thumbnail and chevron, above a content panel that clips itself shut. It is composed from Polaris elements because no Polaris accordion element exists.

`Polaris.Blazor.Components` · composed section, no Polaris element of its own · base `ComponentBase`, `IDisposable`

## Parameters

| Parameter | Type | Default | What it does |
|---|---|---|---|
| `Header` | `string?` | `null` | The heading text. Rendered as the `s-section` heading inside the header and used to build the header's accessible name. |
| `HeaderDetails` | `string?` | `null` | A supporting line under the heading, rendered as the section's `subheading` — Polaris styles it between the heading and the panel. Also appended to the accessible name. |
| `HeaderMedia` | `RenderFragment?` | `null` | Content to the left of both text lines, in practice a thumbnail. It is centred against the heading and supporting line together, and stays visible while the section is closed. Carries no accessible name of its own. |
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

- **The markup, outermost first.** A `div[data-polaris-accordion-item][data-open="true|false"]` holds an optional `PolarisDivider`; a `div[data-polaris-accordion-header]` carrying Blazor's own `@onclick`; then the panel, a `div[data-polaris-accordion-panel]` with the inline style `display:grid;overflow:hidden;grid-template-rows:1fr|0fr`, an inner `div[data-polaris-accordion-panel-inner]` with `min-height:0;overflow:hidden`, and an `s-box` for the content.
- **Inside the header**, a `PolarisClickable` (`AccessibilityExpanded` expanded/collapsed, `Background="subdued"`, `Padding="base"`) contains an inline `PolarisStack` with `JustifyContent="space-between"` and `AlignItems="center"`. Its first child is the `PolarisSection` heading — or, when `HeaderMedia` is set, a nested inline stack containing media plus heading as **one** child, so the chevron still sits at the far edge and the picture is centred against both lines. The last child is a small, subdued `chevron-down` `PolarisIcon` carrying `data-polaris-accordion-chevron`.
- **Why the handler is Blazor's, not an interop listener.** A listener on the Polaris element is detached and re-attached after every render, and every toggle re-renders the page that binds `OpenItemChanged`; whether a click landed while the listener was attached therefore depended on render history, which produced intermittent "the click does nothing". A click is a composed event, so one `@onclick` on the wrapper handles the heading, the chevron, the padding, and the click a keyboard Enter or Space produces on the real `<button>` inside Polaris's shadow root.
- **State is in the markup, motion is in the stylesheet.** `grid-template-rows` is written inline, so a page that never loads `polaris-accordion.css` still opens and closes — it only stops animating. The stylesheet supplies the 200 ms transition, rotates the chevron from `[data-open="true"]`, hides the first item's divider, and disables transitions under `prefers-reduced-motion: reduce`.
- **A closed panel is `inert` as well as clipped**, so content hidden by `overflow:hidden` leaves the tab order and the accessibility tree instead of being reachable off-screen.
- **The header's accessible name is stated, not derived.** A heading set as an attribute lives in Polaris's shadow root, leaving the clickable surface with no text; the name is built as `Header` plus `". "` plus `HeaderDetails` and passed as `AccessibilityLabel`.
- **Opening inside an accordion batches the changes.** `ToggleAsync` awaits `CollapseOthersAsync` before setting its own state open, so both row changes are part of one render batch and the two transitions run together.
- Nothing in `ToggleAsync` uses `ConfigureAwait(false)` on purpose: it ends in `StateHasChanged`, which must run on the renderer's dispatcher. A continuation resumed on the thread pool throws "The current thread is not associated with the Dispatcher" — and only once a handler genuinely awaits.

## Gotchas

- **`InitiallyOpen` is read once, in `OnInitialized`.** Setting it after the first render changes nothing, and there is no open/closed parameter to bind.
- **The only public way to change the state is `ToggleAsync()`.** There is no `OpenAsync`/`CloseAsync`, and `SetOpenAsync` is `internal`; a parent that wants a specific section open must know whether it is currently closed before toggling.
- **Two initially-open items in an exclusive accordion: the later one silently starts collapsed.** The container arbitrates by document order and nothing raises an event about the dropped request.
- **A missing `Header` and `HeaderDetails` leaves the header with no accessible name.** The visible text lives in the shadow root, so without the parameters the surface is announced as an unlabelled button containing only an icon.
- **`HeaderMedia` is invisible to assistive technology by design** — the name is built from the two text parameters. Give a thumbnail that merely illustrates the title `alt=""`; a meaningful image needs its own description, and duplicating the heading in it is noise.
- **`ContentPadding` is a raw string, not an enum.** A misspelled step is ignored by Polaris, which applies no padding at all, with nothing logged and nothing in the markup to show it.
- **`ShowDivider` defaults to `true`, including on the first item.** The rule above the first section would double the accordion's own outer border; the stylesheet hides `[data-polaris-accordion-item]:first-child > s-divider`. Without that stylesheet the first section has two rules.
- **The `inert` attribute does the accessibility work, not the clipping.** A browser that ignores `inert` still hides the content visually but leaves it focusable and announced.
- **Disposing an open section unregisters it without raising `OpenItemChanged(null)`**, so a page mirroring the open `Value` keeps the stale value until something else toggles.

## See also

- [PolarisAccordion.cs.md](PolarisAccordion.cs.md) — the container, exclusivity and `OpenItemChanged`.
- [PolarisDivider.cs.md](PolarisDivider.cs.md) — the rule drawn above each section.
- [PolarisIcon.cs.md](PolarisIcon.cs.md) — the chevron and its icon-name constraints.
