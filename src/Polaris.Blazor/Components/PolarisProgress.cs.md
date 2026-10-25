<!-- summary-of: PolarisProgress.cs -->
<!-- source-sha256: 1911e18f3d24 -->

# PolarisProgress.cs

A progress bar, rendered as `<s-progress>`, that is indeterminate while `Value` is unset and determinate
once it is — so leaving `Value` null is the correct way to say "working, end unknown". It renders no
text and no percentage, so both the visible label and the accessible name come from the caller.

`Polaris.Blazor.Components` · renders `<s-progress>` · base `PolarisElementComponentBase`

## Parameters

| Parameter | Type | Default | What it does |
|---|---|---|---|
| `Value` | `double?` | `null` | How far the task has advanced, clamped by Polaris to `0..Max`. Null omits the attribute, which is what makes the bar indeterminate; `0` is a determinate empty bar instead. |
| `Max` | `double?` | `null` | The value at which the task is complete. Polaris falls back to `1` when it is zero or less, so omitting it means every non-zero `Value` reads as complete. |
| `Tone` | `PolarisTone` | `PolarisTone.Auto` | Semantic colour: success for a finished import, critical for one that failed, `Auto` to let the element choose. Always emitted, so `Auto` is a stated value rather than an omission. |

Inherited: `Id`, `AccessibilityLabel`, `ItemTag`, `Slot`, `AdditionalAttributes` (see
[PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md)). `AccessibilityLabel` is not
decoration here — the element renders no text, so it is the only name assistive technology has.

## Behaviour

- **Omitting `value` is the indeterminate state.** The source is explicit about the alternative: writing
  `value="0"` would pin an empty determinate bar instead, which looks like a task that has started and
  stalled. "Working, unknown end" and "nothing done yet" are different renderings, and the difference
  between them is null versus zero.
- **`Max` is part of the measurement, not an optional extra.** With `Value` set and `Max` omitted,
  Polaris falls back to `1`, so a bar progressed in items or bytes shows as full from the first unit. A
  measurable task sets both.
- **Both numbers are formatted through the shared attribute helper with the invariant culture**, so
  `3` renders `"3"` regardless of the server's locale — no comma decimal separator reaches the element.
- **There is no text, percentage, label or value readout.** The element draws the bar only; a "3 of 5"
  caption or a percentage belongs in surrounding markup (`PolarisText`, `PolarisNumber`).
- **It keeps no state and raises no events.** Presence in the tree is the whole signal, so `@if
  (loading) { <PolarisProgress /> }` is the intended use.

## Gotchas

- **`Value = 0` is not "indeterminate".** It is a real value: an empty determinate bar, which is correct
  for a task that has begun. Use null for unknown progress and zero only when zero is the truth.
- **A determinate bar never hides itself.** Nothing marks completion or removes the element at 100%;
  the page decides when it goes away, and one left behind after a failed request sits at its last
  fraction with no error visible.
- **No `AccessibilityLabel` means an unnamed bar.** Assistive technology announces "progress" and
  nothing else — not what is loading, and not how far. Name it ("Importing products"), and keep the
  visible context nearby, because the element draws no caption.
- **Value and Max are `double?`, not `decimal`.** Money-like precision is not the point of a progress
  bar; a percentage computed as a decimal has to be converted and is formatted with the invariant
  culture on the way out. A visible "42%" is markup the page renders itself, kept in step with `Value`.

## See also

- [PolarisSpinner.cs.md](PolarisSpinner.cs.md) — the indeterminate-only alternative for work with no
  measurable end.
- [PolarisButton.cs.md](PolarisButton.cs.md) — `Loading`, the in-button progress state.
- [PolarisNumber.cs.md](PolarisNumber.cs.md) — where a percentage caption belongs, with tabular figures.
- [PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md) — the inherited parameters
  and how attributes are formatted.
