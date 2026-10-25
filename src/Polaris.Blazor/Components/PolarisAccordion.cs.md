<!-- summary-of: PolarisAccordion.cs -->
<!-- source-sha256: 0cde3c2b4cd5 -->

# PolarisAccordion.cs

A set of collapsible sections that behaves as one accordion: opening a section collapses the one that was open, unless `AllowMultiple` says otherwise.

`Polaris.Blazor.Components` · composed component, no Polaris element of its own · base `ComponentBase`

## Parameters

| Parameter | Type | Default | What it does |
|---|---|---|---|
| `ChildContent` | `RenderFragment?` | `null` | The sections. Each is expected to be a `PolarisAccordionItem`; anything else renders but takes no part in the open/closed behaviour. |
| `AllowMultiple` | `bool` | `false` | `false` means one section open at a time: opening one collapses the others. `true` lets any number stay open and turns off all exclusivity, including the initial-open arbitration. |

## Events

| Callback | Args | Raised when |
|---|---|---|
| `OpenItemChanged` | `EventCallback<string?>` | After a section is toggled. The args carry that item's `Value` when it opened, and `null` when it closed. Only the item the merchant acted on reports through this callback — siblings collapsed by exclusivity do not each raise it. |

## Behaviour

- **It is composed, not a wrapper.** Polaris has no accordion, collapsible or details element, so the container renders a `PolarisBox` (`Border="base"`, `BorderRadius="base"`, `Overflow="hidden"`) around a `PolarisStack` (`Gap=None`), and cascades itself to the items. The single rounded box is what makes the sections read as one card; no section draws a radius or an outer border of its own.
- The `s-box` earns its place because it inherits Polaris's box styling set, while `s-section` inside each item does not — border and radius there would be dropped without a word.
- **State belongs to the items; this container only arbitrates.** Each item owns its `_isOpen`; the accordion collapses the others when one opens, so both row changes land in the same render batch and the two transitions run together instead of one after the other.
- In exclusive mode `CanOpenInitially` gives the open slot to the **first** item that asks, during `OnInitialized` — in practice document order. An item that is unregistered while holding the slot clears it.
- `CascadingValue.IsFixed` is `true`, so re-rendering the container does not re-render every section: each item is the only thing that needs to know its own state changed.
- Registration happens in each item's `OnInitialized` and is undone in `Dispose`; an accordion with no container still opens and closes, it just cannot enforce exclusivity.

## Gotchas

- **Two sections with `InitiallyOpen="true"` are not an error — the second silently starts collapsed.** Nothing is logged, and there is no callback to say the request was dropped, which is why the guard exists: two open sections in a one-open accordion is a state the next click would silently correct.
- **`OpenItemChanged` reports one transition, not N.** When opening section A closes section B, B's `IsOpenChanged` fires with `false` but A's `OpenItemChanged` reports only A. Code that mirrors the accordion's state must apply "the named section is open, everything else is closed" rather than treating each report as a delta.
- **Removing an open item does not raise `OpenItemChanged(null)`.** An external copy of the open `Value` goes stale until the merchant toggles something; clear it yourself when you remove a section.
- `AllowMultiple` is read when a toggle happens, so changing it at runtime takes effect on the next click. Turning it on does not retroactively allow a second open section; turning it off does not collapse anything until the next open.
- Children are not type-checked: a plain element inside `ChildContent` renders in the stack and simply never responds to the accordion, because it never registered.
- The accordion draws no separators itself — every item has a divider on top by default, and the stylesheet hides the first one so it does not double the container's border.

## See also

- [PolarisAccordionItem.cs.md](PolarisAccordionItem.cs.md) — the section, its header markup and its `ToggleAsync`.
- [PolarisDivider.cs.md](PolarisDivider.cs.md) — the rule each item draws above itself.
- [PolarisIcon.cs.md](PolarisIcon.cs.md) — the chevron the item uses as its visible affordance.
