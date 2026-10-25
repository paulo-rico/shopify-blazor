<!-- summary-of: polaris-grid.css -->
<!-- source-sha256: 7dc3ddbd6d20 -->

# polaris-grid.css

The editable data grid's own affordances — a pointer cursor, a focus ring, the dirty mark on an
unsaved draft, and a minimum width for an open editor — none of which Polaris draws. Polaris has no
editable table, so none of these states has an element to attach to and no stylesheet but this one
paints them.

`Polaris.Blazor/wwwroot` · static stylesheet · five rules, no colours, no custom properties

## Selectors

| Selector | What it affects |
|---|---|
| `[data-polaris-grid-cell]` | `cursor: pointer; border-radius: 2px; outline-offset: -2px`. Without the cursor a clickable cell reads as ordinary text. The negative offset draws a ring *inside* the cell so the table's own clipping cannot cut it off. |
| `[data-polaris-grid-cell]:focus-visible` | `outline: 2px solid currentColor` — the ring for a keyboard-focused cell. The browser's default ring is deliberately not suppressed, only offset by the rule above. |
| `[data-polaris-grid-cell][data-polaris-grid-dirty]` | `text-decoration: underline dotted; text-underline-offset: 3px`. A committed draft that has not been saved is underlined rather than coloured: legible to a merchant who cannot distinguish colours, and still distinguishable from a saved value, which is plain. |
| `[data-polaris-grid-editor]` | `min-width: 8rem`. A failed conversion keeps the editor open and Polaris draws the message; without this the message stretches the row to the width of the sentence. |
| `@media (prefers-reduced-motion: reduce)` | `transition: none` on a cell. Inert today — this file declares no transition — and present so any future one is honoured. |

## Behaviour

- **No colours are declared anywhere.** A token such as `--p-color-text` is not defined on `:root` or on
  light-DOM content — those custom properties live inside Polaris's shadow roots and do not inherit out
  of them — so a hard-coded palette would look right in the Admin and wrong everywhere else.
  `currentColor` is the merchant's own text colour, whatever the theme has decided that is.
- **Everything visible in the grid is still a Polaris element.** This file only paints the states Polaris
  does not have; the table, rows and cells remain `s-table`, `s-table-row` and `s-table-cell`.
- **Loaded automatically.** `polaris-interop.js` injects it from the module URL; linking
  `_content/Polaris.Blazor/polaris-grid.css` by hand also works. Without it the grid still edits — the
  states are simply invisible, which is why the file is easy to lose and hard to miss.

## Gotchas

- **`[data-polaris-grid-dirty]` is a presence selector.** Writing the attribute with the value `false`
  still matches and still underlines the cell; the attribute has to be removed to clear the mark, the
  same way a Polaris boolean does.
- **The cursor is the only affordance on an editable cell.** A cell that is editable but not marked
  dirty, or one whose column forgot `Editable`, looks exactly like read-only text.
- **Only `:focus-visible` shows a ring.** A cell clicked with a mouse gets no ring, which is deliberate —
  the editor is opening — but it also means a click that fails to open an editor looks like nothing
  happened.
- **`outline-offset: -2px` is a two-pixel budget.** A thicker ring or a positive offset would be clipped
  by the table cell's own overflow and disappear.

## See also

- [PolarisDataGrid.cs.md](../Components/PolarisDataGrid.cs.md) — writes the cell, dirty and editor
  attributes these rules select.
- [PolarisGridColumn.cs.md](../Components/PolarisGridColumn.cs.md) — `Editable`, `EditorType` and
  `ValueSetter`, which decide whether a cell can be clicked into at all.
- [polaris-interop.js.md](polaris-interop.js.md) — the loader that injects this stylesheet.
