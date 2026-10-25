<!-- summary-of: PolarisModal.cs -->
<!-- source-sha256: 12a10172963c -->

# PolarisModal.cs

A dialog: a heading, a body of children, and named slots for its action buttons. It can be opened
declaratively by a button's `CommandFor` or imperatively through `ShowAsync`/`HideAsync`, and it
reports both transitions through `OnShow`/`OnHide`.

`Polaris.Blazor.Components` · renders `<s-modal>` · base `PolarisSlottedComponentBase`

## Parameters

| Parameter | Type | Default | What it does |
|---|---|---|---|
| `Heading` | `string?` | `null` | The dialog's title. It is also the accessible name unless the inherited `AccessibilityLabel` is set; null drops the attribute, leaving an untitled dialog. |
| `Padding` | `PolarisModalPadding` | `Base` | `Base` is the standard inset; `None` removes it, for a dialog that supplies its own layout. |
| `Size` | `PolarisModalSize` | `Base` | Width: `Small`, `Small100`, `Base`, `Large`, `Large100`. |

Inherited from `PolarisSlottedComponentBase`: `ChildContent` (the body), `SupplementalStart` and
`Aside` (unvalidated slot fragments, rarely useful in a dialog). From
`PolarisElementComponentBase`: `Id`, `AccessibilityLabel`, `ItemTag`, `Slot`, `AdditionalAttributes`
— see [PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md). `Id` is what a
declarative trigger points at.

## Events

| Callback | Args | Raised when |
|---|---|---|
| `OnShow` | `EventCallback<PolarisEventArgs>` | The dialog has been shown, whether by a `CommandFor` trigger or by `ShowAsync`. `args.Type` is `show` and carries no payload. |
| `OnHide` | `EventCallback<PolarisEventArgs>` | The dialog has been hidden: an action button, the Escape key, or `HideAsync`. `args.Type` is `hide` and carries no payload. |

## Members

| Member | Signature | What it does |
|---|---|---|
| `ShowAsync` | `Task ShowAsync()` | Opens the dialog through the element's own `showOverlay()` method. |
| `HideAsync` | `Task HideAsync()` | Closes it through `hideOverlay()`. |

Both go through the interop layer, which waits for the custom element to upgrade before calling, so
the returned task completes only once the element has actually run its method.

## Behaviour

- **The declarative route is the safe one.** Shopify only opens a modal in response to a user
  gesture, so give the modal an `Id` and let a `PolarisButton` with `CommandFor` open it — a button
  press is a gesture by construction, whereas a modal opened from a timer or an incoming server event
  can be refused.
- **The action buttons are slotted children, not fragment parameters.** There is no `PrimaryAction`
  or `SecondaryActions` parameter: the buttons are direct children of the modal carrying the inherited
  `Slot` parameter (`primary-action`, `secondary-actions`). A wrapper element cannot carry a slot
  attribute — Polaris validates what goes into those slots and rejects a `div` around a button.
- **`Padding` and `Size` are always emitted**, because their default enum members are pinned to the
  literal `base`. The wrapper therefore states the value even when the caller sets nothing, rather
  than leaving the element's own default in place.
- **The body is content.** Anything can go in `ChildContent` — a paragraph, a stack, a form — but the
  action buttons must sit beside it, not inside it.

## Gotchas

- **`ShowAsync` needs the dialog in the DOM and upgraded.** Calling it before the first render passes
  an `ElementReference` that has not been captured yet; call it from `OnAfterRenderAsync` or from an
  event handler, never from `OnInitialized`.
- **A slot attribute only counts on the modal's own children.** A button with `Slot="primary-action"`
  nested inside the body's stack is ignored: the dialog then renders with no action buttons and
  nothing is logged. The same rule applies to `secondary-actions`.
- **A null `Heading` removes the accessible name too.** Set `AccessibilityLabel` when the design has
  no visible title, or the dialog is announced as an unnamed dialog.
- **Mixing the two open routes is a false economy.** They are two ways to the same state, and with
  both wired one gesture can raise `show` twice — the dialog is toggled by the button and shown again
  by the handler. Pick the declarative route, or own the state entirely in C#.
- **`Padding="None"` is not cosmetic.** The element paints its own inset, so a body that already
  supplies its own layout — a scroll box, an edge-to-edge grid — ends up with both, or needs `None`.

## See also

- [PolarisButton.cs.md](PolarisButton.cs.md) — `CommandFor`/`Command`, the JavaScript-free trigger.
- [PolarisSection.cs.md](PolarisSection.cs.md) — the usual body container for a dialog's content.
- [PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md) — `Id`, `Slot`, and how
  slotted children are wired.
