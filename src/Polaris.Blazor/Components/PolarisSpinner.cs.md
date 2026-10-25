<!-- summary-of: PolarisSpinner.cs -->
<!-- source-sha256: 434bc03925f2 -->

# PolarisSpinner.cs

An indeterminate spinner that spins until it is removed from the tree — there is no progress value
and no way to stop it from inside. Its only option is size.

`Polaris.Blazor.Components` · renders `<s-spinner>` · base `PolarisElementComponentBase`

## Parameters

| Parameter | Type | Default | What it does |
|---|---|---|---|
| `Size` | `PolarisSpinnerSize` | `PolarisSpinnerSize.Base` | `Base` (the default), `Large`, or `Large100` — large at the 100 step of the scale. Rendered as `size="base"`, `"large"` or `"large-100"`. |

Inherited: `Id`, `AccessibilityLabel`, `ItemTag`, `Slot`, `AdditionalAttributes` (see
[PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md)).

## Behaviour

- Indeterminate by construction: the element has no value, percentage or maximum, so it conveys
  "working" and never "how far". Showing a fraction is `PolarisProgress`'s job.
- It renders no text and no label of its own. The only way to name it for assistive technology is the
  inherited `AccessibilityLabel`; the visible context has to come from surrounding markup.
- Removing it from the render tree is the whole "done" signal — the wrapper keeps no state, so
  `@if (loading) { <PolarisSpinner /> }` is the intended use.

## Gotchas

- **Do not put one inside a button.** A button has its own loading state
  ([PolarisButton.cs.md](PolarisButton.cs.md), `Loading`), which keeps the button's own geometry and
  announces itself; a stacked spinner shifts the label and leaves the button clickable.
- **Do not use it for a task with a measurable end.** A spinner that keeps turning while a real
  percentage exists reads as stuck; the progress component's unset `Value` is the correct
  indeterminate form, and a set `Value` with `Max` is the correct determinate one.
- Because it never stops by itself, a spinner left behind after a failed request spins for ever with
  no error visible. Bind its presence to the same flag the request clears in a `finally`.

## See also

- [PolarisProgress.cs.md](PolarisProgress.cs.md) — a bar that can be indeterminate *or* measurable.
- [PolarisButton.cs.md](PolarisButton.cs.md) — `Loading`, the in-button alternative.
- [PolarisTable.cs.md](PolarisTable.cs.md) — `Loading`, for a table that is fetching.
- [PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md)
