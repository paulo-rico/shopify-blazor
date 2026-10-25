<!-- summary-of: ComponentUsageTemplate.cs -->
<!-- source-sha256: c55f274f8610 -->

# ComponentUsageTemplate.cs

Renders the Razor snippet that `shopify-blazor add component` writes for a catalogue entry. The snippet is
generated from the catalogue rather than stored per component, so it cannot drift from the wrapper's real
parameters — but it is deliberately *prose about* the component, not runnable usage, because the catalogue
records a parameter's type and not a legal value for it.

`Shopify.Blazor.Cli` · `static class` · the snippet generator for one `CatalogueComponent`

## Members

| Member | Signature | What it does |
|---|---|---|
| `Render` | `static string Render(CatalogueComponent component)` | Returns the complete snippet as one string, ending with a newline. Pure: it reads the catalogue record and writes nothing. |

## Behaviour

The snippet is emitted in this order, omitting any step whose data is absent:

1. `@* Usage of <Wrapper>, which renders <s-element>. *@` — always, naming the wrapper and the element it
   renders.
2. `@* <Summary> *@` when the catalogue carries a non-empty summary.
3. `@using <Namespace>` when the entry has a namespace, so the file compiles without relying on an
   `_Imports.razor` that may not import the components.
4. A blank line, `@* A minimal runnable start. Grow it from the parameters below. *@`, and the element
   itself: `<Wrapper />`.
5. If there are non-content parameters: a blank line, two comment lines explaining that no value is
   invented, then one comment per parameter — `Name (Type)` normally, or for anything whose type contains
   `bool`, `Name="true"  (presence attribute: false must omit it, or it is still true)`.
6. If there are named slots other than `default`/`children`: a blank line, two comment lines warning that
   the slot name goes on the child and Polaris rejects a wrapper `<div>`, then one example per slot:
   `<PolarisButton Slot="<slot>">…</PolarisButton>`.
7. If the catalogue lists events: a blank line, a comment naming them, a comment showing
   `On<Event>="Handle<Event>"` for each, and a comment giving the handler shape
   `private void HandleX(PolarisEventArgs args) { }`.

The element name in step 1 is `component.Element`, or a fallback built from the wrapper: strip a leading
`Polaris`, kebab-case the rest, prefix `s-`. So `PolarisAddableSelect` renders as `s-addable-select` when
the catalogue does not state the element.

## Gotchas

- **It never assigns a single parameter.** `<Wrapper />` is the only markup, and every parameter appears
  only inside a comment. That is the point: a generated file full of `Variant="…"` looks like working code
  and fails to compile, and the catalogue cannot know which enum member is legal.
- **Content and callback parameters are filtered out**, so `ChildContent` never appears in the parameter
  list — it is the default slot, and the self-closing element says nothing about it. The predicate drops a
  type containing `RenderFragment`, or starting with `EventCallback` or `Func<`.
- **Boolean detection is a substring test for `bool`.** That catches `bool` and `bool?` alike, so a nullable
  tri-state parameter gets the "presence attribute" advice, which does not tell the reader how to express
  the third state.
- **The content-slot filter is ordinal.** A catalogue slot named `Default` is not recognised as the content
  slot, so it is printed as a named-slot example — plausible-looking and wrong.
- **`Render` does not validate its input.** A `null` component throws `NullReferenceException`, and a
  wrapper name containing markup is copied straight into the output; the catalogue is generated from trusted
  sources, so neither is defended against here.
- **The returned string is not trimmed.** Callers that concatenate snippets get the trailing blank line
  whether they wanted it or not.

## See also

- [Catalogue/PolarisCatalogue.cs.md](Catalogue/PolarisCatalogue.cs.md) — the records this consumes.
- [Commands/AddComponentCommand.cs.md](Commands/AddComponentCommand.cs.md) — decides where the snippet goes and whether it is written.
- [README.md](README.md) — the CLI's own description of `add component`.
