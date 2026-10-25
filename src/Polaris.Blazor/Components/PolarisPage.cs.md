<!-- summary-of: PolarisPage.cs -->
<!-- source-sha256: 4961697ab035 -->

# PolarisPage.cs

The top-level container for one screen: it renders `<s-page>` with the screen's title and width, and
supplies the slots that hold the primary action, an aside column and a banner above the content.

`Polaris.Blazor.Components` · renders `<s-page>` · base `PolarisSlottedComponentBase`

## Parameters

| Parameter | Type | Default | What it does |
|---|---|---|---|
| `Heading` | `string?` | `null` | The page title, rendered as the `heading` attribute. Polaris supplies the heading semantics from it, so the page must not add its own `<h1>`; null renders a page with no title at all and nothing warns. |
| `InlineSize` | `PolarisPageInlineSize` | `PolarisPageInlineSize.Base` | Page width: `Small` (narrow, for focused forms), `Base` (the default) or `Large` (wide, for data-heavy screens). Rendered as `inlineSize="small"`, `"base"` or `"large"`. |

Inherited from the slotted base and the element base:

- `ChildContent` (`RenderFragment?`) — the page body, placed between the supplemental slot and the
  aside column.
- `SupplementalStart` (`RenderFragment?`) — content rendered before the body in the `supplemental-start`
  slot, typically a banner or an informational block. This slot is not type-checked, which is why it is
  a fragment.
- `Aside` (`RenderFragment?`) — the aside column's content. Also unvalidated, so a fragment is safe.
- `Id`, `AccessibilityLabel`, `ItemTag`, `Slot`, `AdditionalAttributes` — the shared element parameters,
  described in [PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md).

## Behaviour

- **The page is the frame, not the content.** Everything a merchant sees between the title and the
  bottom of the screen is `ChildContent`; the page contributes the heading, the width and the slot
  plumbing. It does not lay the body out for you, so a block-level arrangement of the content —
  `PolarisSection` per card, `PolarisStack` for the gaps — is still the page's job.
- **Actions are children carrying a slot, never parameters.** There is no `PrimaryAction` or
  `Actions` fragment: a button joins the action area with
  `<PolarisButton Slot="primary-action">`, because Polaris validates slot children and rejects a
  wrapper element, and a `RenderFragment` cannot carry an attribute.
- **The rendered order is fixed by the base class:** the `supplemental-start` wrapper, then the body,
  then the `aside` wrapper. Markup order inside `ChildContent` cannot move the aside above the body.
- **`InlineSize` is a layout decision with a consequence beyond width** — see the gotcha below.

## Gotchas

- **`InlineSize` other than `Base` silently drops the aside column.** The element renders its `aside`
  slot only at the default (`base`) width; at `Small` or `Large` the fragment is still in the DOM and
  simply never appears, with no error and no fallback placement. A wide data screen that also wants a
  right-hand panel has to choose one.
- **A slot attribute only counts on the page's own children.** A button with `Slot="primary-action"`
  nested inside a `PolarisStack` or a `PolarisSection` in the body is not a page action: Polaris reads
  slots from the host's direct children, so the attribute is ignored and the button renders inline in
  the body — which looks like a working action in the wrong place.
- **Do not add your own heading element.** `Heading` already gives the page its accessible name and its
  place in the heading outline; a hand-written `<h1>` produces two top-level headings and an outline
  that reads as two pages.
- **The heading is text, not markup.** It is an attribute, so an emphasised word, a link or a badge in
  the title has to go elsewhere (a `PolarisSection` heading, or content in the page body).
- **Nothing validates that a page exists.** A body rendered without a `PolarisPage` compiles and shows
  content with no page frame, no title and no padding; the loss is visual and entirely silent.

## See also

- [PolarisSection.cs.md](PolarisSection.cs.md) — the card-like block the page body is usually built
  from.
- [PolarisStack.cs.md](PolarisStack.cs.md) — spacing between the blocks inside the page.
- [PolarisButton.cs.md](PolarisButton.cs.md) — `Slot="primary-action"`, the page's action mechanism.
- [PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md) — `Id`, `Slot` and the
  lifecycle every wrapper follows.
