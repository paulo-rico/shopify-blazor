<!-- summary-of: PolarisLink.cs -->
<!-- source-sha256: ab93327e32b8 -->

# PolarisLink.cs

An `<s-link>` for navigating to another page: a destination, a tone, a browsing target, and optional
declarative command and interest targets that let a link open a modal, a popover or a tooltip with no
JavaScript.

`Polaris.Blazor.Components` · renders `<s-link>` · base `PolarisContentComponentBase`

## Parameters

| Parameter | Type | Default | What it does |
|---|---|---|---|
| `Href` | `string?` | `null` | Destination URL. Without one the link renders as plain text — no anchor, no pointer, no navigation. |
| `Tone` | `PolarisTone` | `Auto` | Semantic colour. `Critical` is the only tone Polaris documents for a link; the others are not documented, so treat them as inherited from the shared tone enum rather than as supported values. |
| `Target` | `PolarisLinkTarget` | `Auto` | Browsing context: `Auto` (the element decides), `Blank` (`_blank`), `Self` (`_self`), `Parent`. Dropped entirely when `Href` is `null`. |
| `Download` | `string?` | `null` | Marks the destination as a download; the value is the suggested filename. Unreliable when the file is on another origin. |
| `Lang` | `string?` | `null` | Language of the linked resource, as a BCP 47 tag (`en-GB`). |
| `CommandFor` | `string?` | `null` | Id of the element this link controls — the declarative way for a link to open an `s-modal` or `s-popover`. |
| `Command` | `PolarisCommand` | `Auto` | The command sent to `CommandFor`: `Auto`, `Show`, `Hide`, `Toggle`. Dropped when `CommandFor` is `null`. |
| `InterestFor` | `string?` | `null` | Id of a tooltip or popover shown when the link is hovered or focused. |
| `ChildContent` | `RenderFragment?` | `null` | The link text. A link with no content is an invisible hit area. |

Inherited: `Id`, `AccessibilityLabel`, `ItemTag`, `Slot` and `AdditionalAttributes` from
[PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md).

## Events

| Callback | Args | Raised when |
|---|---|---|
| `OnClick` | `EventCallback<PolarisEventArgs>` | The link is clicked, **before** the Admin decides whether to allow the navigation. Modifier keys are on the args, so a Ctrl-click can be told from a plain one. |

## Behaviour

- **A link with no `Href` is text.** This is the intended way to render a row of records where some
  have a destination and some do not: the others degrade to plain text rather than to a dead link.
- **`Target` and `Command` are dropped when their owner is absent.** Setting `Target` alone does
  nothing at all, because there is no navigation for it to describe; the same is true of `Command`
  without `CommandFor`.
- **`Target="Blank"` adds `rel="noopener noreferrer"` for you.** Polaris does it when it renders the
  anchor, so there is no separate parameter for the rel attribute and no way to omit it.
- `CommandFor` is the same declarative mechanism `PolarisButton` uses: the target element carries an
  `Id`, and Polaris opens or closes it without a Blazor handler participating.

## Gotchas

- **A link is not a button.** There is no loading state and no disabled state, so a link cannot show
  progress or prevent a double submit — for a form submission or a prominent action, use
  `PolarisButton`. This is the most common wrong choice in an admin page: a "Save" that is an `s-link`
  looks right and gives the merchant no feedback at all.
- **A link to an external page may be intercepted by the Admin.** Inside the embedded iframe a link is
  not a reliable way to reach an arbitrary destination, so a link that must leave the Admin should be
  verified in a browser rather than assumed to work.
- **`OnClick` is not a navigation confirmation.** It runs before the Admin decides whether to allow the
  move, so a handler that records "the merchant went there" can record a navigation that never happened.
- **`Download` is best-effort.** Cross-origin files are commonly opened instead of saved, and the
  suggested filename is ignored.
- A link with `CommandFor` still has its `Href` if both are set — the click can both navigate and send
  the command, which is almost never what is wanted. Set one or the other.

## See also

- [PolarisButton.cs.md](PolarisButton.cs.md) — the action control, with loading and disabled states.
- [PolarisText.cs.md](PolarisText.cs.md) — text that must not look navigable.
- [PolarisTooltip.cs.md](PolarisTooltip.cs.md) — the usual target of `InterestFor`.
- [PolarisBanner.cs.md](PolarisBanner.cs.md) — where a link inside prose is routinely used.
