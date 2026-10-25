<!-- summary-of: PolarisTooltip.cs -->
<!-- source-sha256: 64a8b58c35fd -->

# PolarisTooltip.cs

A text-only overlay that explains one other element, rendered as `<s-tooltip>`; it has no trigger of its
own, so it is placed beside the page and pointed at with `InterestFor` from the element it describes.

`Polaris.Blazor.Components` · renders `<s-tooltip>` · base `PolarisContentComponentBase`

## Parameters

It declares **no parameters of its own** — the whole class is `TagName => "s-tooltip"`. What matters is
inherited:

- `ChildContent` (`RenderFragment?`, from `PolarisContentComponentBase`) — the tooltip text. Text or
  paragraph components only.
- `Id` (`string?`, from [PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md)) — the
  address a trigger's `InterestFor` points at. The wrapper does not generate one, so a tooltip with no
  `Id` cannot be referenced and is never shown.
- `ItemTag`, `Slot`, `AccessibilityLabel`, `AdditionalAttributes` — as for every element wrapper; none of
  them turns the tooltip into a trigger.

## Behaviour

- **The tooltip has no trigger of its own.** The explaining element is the one that reacts, through its
  `InterestFor`, and the id is the only link between the two. So the pairing is written twice:
  `Id="price-help"` on this component and `InterestFor="price-help"` on the button or link that shows it.
- **It is rendered as a text-only overlay, and that is why it takes text.** The source describes the
  element that way and restricts the children to text or paragraph components to match.
- **The association is the id, not the markup.** Nothing in the component pairs itself with a trigger, so
  where the tooltip sits relative to the element it explains creates nothing; `Id` plus the trigger's
  `InterestFor` is the whole mechanism.

## Gotchas

- **A button or a link inside the tooltip is unreachable.** The source is explicit: Polaris renders it as
  a text-only overlay, so an interactive child placed inside can never be used. If the content needs a
  control, it needs a popover instead.
- **Hover and focus only.** The source warns that a tooltip is shown on hover and focus only, so it must
  never be the sole carrier of information a touch user needs — repeat anything essential in the page.
- **A missing `Id` fails silently.** No error, no warning: the trigger's `InterestFor` addresses nothing
  and the tooltip simply never appears. Two tooltips sharing one id make the address ambiguous instead,
  with nothing to say which was meant.
- **The wrapper does not check that the id exists.** Nothing validates that some element actually points
  at this tooltip.

## See also

- [PolarisButton.cs.md](PolarisButton.cs.md) — its `InterestFor` is the usual trigger.
- [PolarisLink.cs.md](PolarisLink.cs.md) — the other common trigger.
- [PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md) — `Id`, `Slot` and the shared
  parameters.
