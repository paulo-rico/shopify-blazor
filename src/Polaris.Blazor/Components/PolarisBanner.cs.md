<!-- summary-of: PolarisBanner.cs -->
<!-- source-sha256: 4dfb7e2598bf -->

# PolarisBanner.cs

An inline message strip — `<s-banner>` — with a heading, a semantic tone, an optional close button and a body supplied as child content. Polaris does not remember a dismissal, so the application must.

`Polaris.Blazor.Components` · renders `<s-banner>` · base `PolarisSlottedComponentBase`

## Parameters

| Parameter | Type | Default | What it does |
|---|---|---|---|
| `Heading` | `string?` | `null` | The banner's title, rendered as a `heading` attribute. `null` leaves the banner with body text only. |
| `Tone` | `PolarisTone` | `Auto` | Semantic colour. `Critical` is the tone for a failure; the default lets the element choose. |
| `Hidden` | `bool` | `false` | Renders the banner hidden while keeping it mounted — for toggling an inline error without re-rendering the surrounding page. `true` writes the bare global HTML `hidden` attribute; `false` removes it. |
| `Dismissible` | `bool` | `false` | Shows a close button. Only meaningful with an `OnDismiss` handler that stops rendering the banner. |

Inherited: `ChildContent` is the message body; `Id`, `AccessibilityLabel`, `ItemTag`, `Slot` and `AdditionalAttributes` come from
[PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md). The class also inherits `SupplementalStart` and `Aside` from
`PolarisSlottedComponentBase` — but see the first gotcha: this component does not render them.

## Events

| Callback | Args | Raised when |
|---|---|---|
| `OnDismiss` | `EventCallback<PolarisEventArgs>` | The merchant clicks the close button. Handle it and stop rendering the banner; this is the only signal that the dismissal happened. |
| `OnAfterHide` | `EventCallback<PolarisEventArgs>` | The banner has finished hiding, after the dismiss animation completes. |

## Behaviour

- **`Hidden` is a real HTML attribute, not a Polaris boolean.** It is written as a bare `hidden` when `true` and omitted when `false`; there is no `hidden="false"` state. The element stays in the tree either way, which is exactly what makes it usable for a validation message that appears and disappears without disturbing the page around it.
- `Dismissible` is written through the same presence-attribute formatting: `true` renders a bare attribute, `false` removes it.
- `Tone` is always written, even at `auto`; `Heading` is written unless it is `null`.
- Dismissal state is not persisted by Polaris. The banner disappearing is a client-side effect only; the next render that mounts it again shows it again, so recording "the merchant closed this" is the application's job.

## Gotchas

- **`SupplementalStart` and `Aside` are inherited but dead.** `PolarisBanner` overrides `BuildChildContent` to return only `ChildContent`, overriding the slot rendering in `PolarisSlottedComponentBase`. A fragment set on either parameter renders nothing, silently — put everything in `ChildContent`.
- **`Dismissible` without `OnDismiss` loses the dismissal.** The close button hides the banner, nothing records it, and the banner returns on the next mount or navigation — which looks like the dismiss button "not working".
- **Removing the banner in `OnDismiss` is the reliable hook.** `OnAfterHide` depends on the element completing its hide; an element yanked from the tree the moment it was dismissed has no hide left to finish, so do cleanup in `OnDismiss` if it matters.
- **`Hidden` does not mean "not rendered".** The element still exists, is still in the accessibility tree as hidden, and comes back with all its state when `Hidden` goes false. Use a conditional render when the content must not be present at all.
- An `OnDismiss` handler on a banner with `Dismissible="false"` never runs; the close button is the only thing that raises it.
- A `null` `Heading` is valid, but it leaves the banner without a title; when the tone is critical, state the outcome in the body instead of relying on colour.

## See also

- [PolarisButton.cs.md](PolarisButton.cs.md) — the action control used inside a banner's body.
- [PolarisEmptyState.cs.md](PolarisEmptyState.cs.md) — the other inline messaging component, for "nothing here yet" rather than a status.
- [PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md) — the shared parameters and the presence-attribute rule `Hidden` and `Dismissible` follow.
