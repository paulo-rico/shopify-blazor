<!-- summary-of: PolarisQueryContainer.cs -->
<!-- source-sha256: cbbe8d77c302 -->

# PolarisQueryContainer.cs

Establishes a CSS container, rendered as `<s-query-container>`, so a descendant can use a responsive
value such as `@container (inline-size > 500px) large, small`. Give it a `ContainerName` to query it by
name from anywhere in the subtree.

`Polaris.Blazor.Components` · renders `<s-query-container>` · base `PolarisContentComponentBase`

## Parameters

| Parameter | Type | Default | What it does |
|---|---|---|---|
| `ContainerName` | `string?` | `null` | The container's name, rendered as the `containerName` attribute, for a named `@container name (...)` query. Null omits it, leaving an anonymous container that only unnamed queries can match. |

Inherited: `ChildContent` — the subtree the queries apply to — from `PolarisContentComponentBase`, plus
`Id`, `AccessibilityLabel`, `ItemTag`, `Slot`, `AdditionalAttributes` (see
[PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md)).

## Behaviour

- **It changes the reference frame for responsive values, not the content.** The container is sized by
  its parent, and a descendant's `@container` rule compares against that box rather than against the
  viewport. So the same component laid out in a wide page column and in a narrow aside can pick
  different sizes from one markup tree, with no media query per layout.
- **Naming is what makes it addressable.** An unnamed container is only reachable by an unnamed
  `@container (...)` query, which always means the nearest ancestor container. `ContainerName="card"`
  lets a deeply nested descendant — or a stylesheet that knows nothing about the DOM between them — ask
  for that specific box.
- **It exposes no dimensions to JavaScript or C#.** The source is explicit: the wrapper cannot be used to
  measure anything from code, so a size that must be known in .NET (for paging a table, say) cannot come
  from here. There is no `Width`, no `ObservedSize` and no event.
- **It renders exactly `ChildContent`**, with one attribute added — no wrapper `div` of its own beyond
  the custom element, no styles, no padding.

## Gotchas

- **A `@container` rule with no container above it never matches.** The query silently applies to
  nothing and the declarations inside it are dropped: a component that shrinks or reflows correctly in
  one page renders at its full-size fallback in another, with no warning and no error in any console.
  That is the failure this wrapper exists to prevent.
- **The container is not the subject of its own query.** A `@container` rule styles the container's
  *descendants*, never the container itself, so `PolarisQueryContainer` cannot be made to resize or
  hide itself on its own width.
- **Nesting matters for unnamed queries.** Two nested containers both match an unnamed `@container
  (...)`; the nearest one wins. A rule written for the outer box will quietly evaluate against the inner
  one the day a component grows its own container — name the container when the query means a specific
  frame.
- **`ContainerName` with nothing querying it is dead weight.** Nothing validates that a stylesheet uses
  the name, and a typo in either place produces a container that is never queried rather than an error.
- **It is not a viewport breakpoint.** Media queries still describe the screen; this describes the slot
  the component was given. Layout that must change with the device rather than the column still needs
  the media query, and the two are not interchangeable.
- **The name is an attribute, not a CSS property set on the host.** A stylesheet that sets
  `container-name` on the element from outside can disagree with `ContainerName`; pick one.

## See also

- [PolarisGrid.cs.md](PolarisGrid.cs.md) — the layout whose columns are the usual reason to query a
  container.
- [PolarisBox.cs.md](PolarisBox.cs.md) — the plain box a container is often wrapped around.
- [PolarisStack.cs.md](PolarisStack.cs.md) — the other layout primitive that changes direction by width.
- [PolarisElementComponentBase.cs.md](../PolarisElementComponentBase.cs.md) — attributes and the
  inherited parameters.
