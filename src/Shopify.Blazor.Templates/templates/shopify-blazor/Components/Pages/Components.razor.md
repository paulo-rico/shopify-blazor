<!-- summary-of: Components.razor -->
<!-- source-sha256: 4de21ffb36b8 -->

# Components.razor

The template's component gallery at `/components`: it renders one instance of every Polaris element the
toolkit wraps, plus a button that asks the browser which of those custom elements are defined, upgraded
and present. A hidden `#polaris-probe-elements` span publishes the same tag list that
`tools/verify-in-browser.mjs` reads, so the gallery's claim is machine-checked rather than aspirational.

`Components/Pages` · `@page "/components"` · no parameters · injects `IPolarisInterop` as `Interop` and `IAppBridgeInterop` as `Bridge`

## Behaviour

The page is one `PolarisStack Gap="PolarisGap.Base"` inside the layout's `PolarisPage`. The template
ships this file only when `include-sample-pages` is true (its default); otherwise `template.json`
excludes it and `MainLayout` omits its nav button. The regions, in render order:

| Region | What it renders |
|---|---|
| Probe span | A `hidden` `<span id="polaris-probe-elements">` whose `data-elements` is `ProbedElements` joined with commas. Not Polaris markup, not visible. |
| "Component gallery" | An intro `PolarisSection` whose `PolarisParagraph` says every control is Shopify's web component rendered by a `Polaris.Blazor` wrapper: no custom CSS, no reimplementation, appearance from the merchant's admin branding. |
| "Actions and feedback" | Inline button row: `Primary` (its label shows `_clicks`), `Secondary`, `Tertiary`, a `Critical` + `Icon="delete"` button with `AccessibilityLabel`, a disabled one and a loading one. Then one badge per tone, a dismissible `Info` banner and a labelled spinner. |
| "Forms" | Product-title text field bound to `_title`; a second field fixed at `DUPLICATE-1` with an `Error`; text area for `_description`; checkbox for `_chargeTax`; switch for `_published`; select over Active/Draft/Archived bound to `_selectedStatus`. A success banner appears once `_lastChange` is set. |
| "Overlays" | Buttons carrying `CommandFor` + `Command` show `gallery-modal` (`Show`) and toggle `gallery-popover` (`Toggle`). The modal closes via a `primary-action` button with `Hide`; the popover holds text and its own `Hide` button. No JavaScript or `@onclick`. |
| "Every component: layout and structure" | `PolarisQueryContainer` named `gallery-query` around a two-column grid (first item spans both columns), a box, a button group using `primary-action`/`secondary-actions` slots, ordered and unordered lists, and an unconditional table with Element/Instances headers and one row. |
| "App Bridge: the admin around the app" | `PolarisAppNav` with Home/Components links, `PolarisAppWindow Id="gallery-window" Src="/components"`, and a button that shows it with `CommandFor`. Present for documentation, deliberately not probed. |
| "Every component: forms" | A choice list with two choices, a select containing an option group, and one each of the email, password, URL, search, number (step 5, 0–100), money (GBP), colour, colour-picker, date, date-picker (two months) and drop-zone (`Multiple`, accepting `.jpg,.png`) fields. |
| "Every component: status, media and text" | Chip, removable clickable chip, icon, avatar, thumbnail, determinate and indeterminate progress bars, a 16/9 cover-fit rounded image, a bold number, subdued inline text, a link, a clickable surface, a menu, a tooltip and an empty state. The menu and tooltip carry ids because their triggers address them by id. |
| "Runtime check" | Explanatory text, a button (`OnClick="CheckAsync"`, `Loading="_checking"`) and, once `_runtimeStatus` is non-null, a table of Element / Registered / Instances / Upgraded with Success/Critical and Success/Warning badges. |

### Page state

| Member | Type | Initial | Role |
|---|---|---|---|
| `_clicks` | `int` | `0` | Count shown in the Primary button's label. |
| `_checking` | `bool` | `false` | Drives `Loading` on the runtime-check button. |
| `_title` | `string` | `"Organic cotton t-shirt"` | The first text field's value. |
| `_description` | `string` | `"Soft, midweight cotton."` | The text area's value. |
| `_chargeTax` | `bool` | `true` | The checkbox's state. |
| `_published` | `bool` | `false` | The switch's state. |
| `_selectedStatus` | `string` | `"draft"` | The select's value. |
| `_lastChange` | `string?` | `null` | Description of the last form change; when non-null the Forms banner renders. Also receives a failure message from the runtime check. |
| `_runtimeStatus` | `IReadOnlyDictionary<string, PolarisComponentStatus>?` | `null` | The runtime check's result; non-null renders the results table. |

### Handlers

| Handler | Wired to | Behaviour |
|---|---|---|
| `CountClickAsync` | Primary button `OnClick` | Increments `_clicks`; a shift-click also toasts `Shift-clicked; count is {n}.` — the modifiers arrive on `PolarisEventArgs`. |
| `DismissedAsync` | Banner `OnDismiss` | Toasts "The banner told C# it was dismissed." with `isError: false`. |
| `TitleChangedAsync` | Title field `OnChange` | `_title = args.Value ?? string.Empty`, then records `title = "…"`. |
| `DescriptionChangedAsync` | Text area `OnChange` | `_description = args.Value ?? string.Empty`, then records it. |
| `TaxChangedAsync` | Checkbox `OnChange` | `_chargeTax = args.Checked ?? false`, then records it. |
| `PublishedChangedAsync` | Switch `OnChange` | `_published = args.Checked ?? false`, then records it. |
| `StatusChangedAsync` | Select `OnChange` | `_selectedStatus = args.Value ?? string.Empty`, then records it. |
| `RecordAsync` | Helper for the four above | Sets `_lastChange` and returns a completed `Task`, so the field's callback completes immediately. |
| `CheckAsync` | Runtime-check button `OnClick` | Sets `_checking`, awaits `Interop.GetComponentStatusAsync(ProbedElements)`, stores the dictionary, catches `JSException` into `_lastChange`, and clears `_checking` in `finally`. |

### Probe list

`ProbedElements` is a `static readonly string[]` of 60 names: the 50 top-level elements and the 10
composed sub-elements `s-choice`, `s-grid-item`, `s-list-item`, `s-option`, `s-option-group`,
`s-table-body`, `s-table-cell`, `s-table-header`, `s-table-header-row` and `s-table-row`. `SampleImage`
is a one-pixel PNG data URI, so the image and thumbnail load with no network request.

## Gotchas

- **The probe list is a contract.** `verify-in-browser.mjs` reads it from the span and fails on a name
  that is undefined, rendered zero times, or not upgraded. A component listed but not rendered fails
  rather than passing vacuously — which is why the layout table is unconditional, and why an earlier
  version's table checks passed on an empty DOM.
- **`s-page` and `s-divider` are in the list but not rendered here.** They come from `MainLayout`
  (`PolarisPage`, `PolarisDivider`), so the probe only passes when the page renders through that
  layout; rendering the page on its own would report both missing.
- **`s-app-nav` and `s-app-window` are deliberately absent from `ProbedElements`.** They are App
  Bridge elements, not Polaris ones, and `app-bridge.js` defines them only after it has connected to
  the admin host. Measured in a browser, they are undefined outside the Admin, so probing them would
  report a failure that says nothing about this toolkit. The markup stays to document their shape.
- **The runtime check must use `Interop`, not `IJSRuntime`.** `componentStatus` is an ES module
  export, so invoking it as a global function throws and the page reports a failure that says nothing
  about the elements.
- **A failed check reports in the wrong place.** `CheckAsync` writes its message to `_lastChange`, and
  the only banner showing `_lastChange` is in the Forms section. "The runtime check failed: …"
  therefore appears above the runtime table, and it also replaces the last form change.
- **An unavailable interop still looks checked.** `GetComponentStatusAsync` returns an empty
  dictionary rather than throwing when interop is unavailable, and the page only tests
  `_runtimeStatus is not null`, so it renders a header-only table with no "nothing came back" state.
- **The modal's slot attribute has to be on the button itself.** Polaris validates slotted children
  and rejects a `div` that merely wraps a button; the `primary-action` button is also a direct child
  of `s-modal`, not nested in the body.
- **A trigger and its target need matching ids.** Polaris addresses a menu or tooltip by id, and a
  tooltip with no id can never be shown; that is why `gallery-menu` and `gallery-tooltip` are not
  decorative. The open/menu/hover buttons point at those ids with `CommandFor` / `InterestFor`.
- **Toasts need the Admin.** `Bridge.ShowToastAsync` is a no-op outside it, so dismissing the banner
  or shift-clicking looks dead in an ordinary browser tab even though the handler ran.
- **The page declares no render mode.** It is interactive only because `Routes.razor` puts
  `@rendermode InteractiveServer` on the `Router`; under a static render every handler here is inert
  while the markup looks perfect.
- Every `OnChange` handler coalesces a missing payload — `args.Value ?? string.Empty`,
  `args.Checked ?? false` — so a null payload silently empties the bound state instead of throwing.

## See also

- [Products.razor.md](Products.razor.md) — the other sample page, and the authenticated request path this gallery does not exercise.
- [PolarisInterop.cs.md](../../../../../Polaris.Blazor/Interop/PolarisInterop.cs.md) — `GetComponentStatusAsync` and the `PolarisComponentStatus` record behind the runtime table.
- [PolarisButton.cs.md](../../../../../Polaris.Blazor/Components/PolarisButton.cs.md) — `CommandFor`, `Command`, `InterestFor` and the button properties used throughout.
- [components.md](../../../../../../docs/components.md) — the generated reference for every component in the gallery.
