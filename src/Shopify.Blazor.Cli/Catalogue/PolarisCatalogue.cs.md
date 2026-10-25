<!-- summary-of: PolarisCatalogue.cs -->
<!-- source-sha256: a1c6c551d3a5 -->

# PolarisCatalogue.cs

The embedded component catalogue that decides what `add component` can write usage for, plus the
lookup and "did you mean" logic over it. The JSON it loads is generated from the Polaris.Blazor
sources by a script the CLI does not own, so the file is only ever read.

`Shopify.Blazor.Cli.Catalogue` · catalogue loader, records and `CatalogueException` · no base class

## Members

| Member | Signature | What it does |
|---|---|---|
| `EmbeddedResourceName` | `const string` = `"Shopify.Blazor.Cli.Catalogue.polaris-catalogue.json"` | Manifest name the file is embedded under; `LoadEmbedded` looks it up by exactly this. |
| `SourcePath` | `const string` = `"src/Shopify.Blazor.Cli/Catalogue/polaris-catalogue.json"` | Repo-relative path the generator should write, quoted in the error when the resource is absent. |
| `GeneratedBy` | `string?` | The generating script's name when the file declares one; `null` otherwise. |
| `Components` | `IReadOnlyList<CatalogueComponent>` | Every component, in file order. One entry per *element*, so a wrapper covering several elements appears more than once. |
| `Find` | `CatalogueComponent? Find(string name)` | Case-insensitive match on the wrapper name after normalisation; `null` when nothing matches. |
| `Suggest` | `IReadOnlyList<string> Suggest(string name, int count = 5)` | Up to `count` known wrapper names nearest to `name`, ordered by edit distance then alphabetically. |
| `Parse` | `static PolarisCatalogue Parse(string json)` | Deserialises and validates; throws `CatalogueException` on invalid JSON or a `null` document. |
| `LoadEmbedded` | `static PolarisCatalogue LoadEmbedded()` | Reads the embedded resource and parses it; throws `CatalogueException` with actionable text when the resource is absent. |

Supporting types in the same file:

| Type | Shape | Meaning |
|---|---|---|
| `CatalogueParameter` | `sealed record (string Name, string? Type)` | One wrapper parameter as Razor spells it, e.g. `Variant` / `PolarisButtonVariant`. `Type` may be null when the generator could not state one. |
| `CatalogueComponent` | `sealed record (string Wrapper, string? Element, string? Namespace, string? Summary, IReadOnlyList<CatalogueParameter> Parameters, IReadOnlyList<string> Events, IReadOnlyList<string> Slots)` | One wrapper: `PolarisButton`, `s-button`, its namespace, a one-sentence summary, and its parameters, events and slots. |
| `CatalogueException` | `sealed class : Exception` | "Missing, unreadable, or the wrong shape" — the only failure type this file throws. Two constructors, with and without an inner exception. |

## Behaviour that is not in the member list

- **`Find` forgives a missing `Polaris` prefix and nothing else.** `Normalise` trims and prepends
  `Polaris` unless the name already starts with it (case-insensitively), so `Button` finds
  `PolarisButton` and `POLARISBUTTON` finds it too. Kebab-case does **not** work: `polaris-button` is
  left alone and matches nothing, and the caller then gets a suggestion list instead of a hit.
- **`Suggest` ranks; it never rejects.** There is no distance threshold, so a name with nothing in
  common still gets up to five names back. Duplicates are removed **before** ranking (exact
  `PolarisButton` spelling, ordinal), because the catalogue is keyed by element and a multi-element
  wrapper would otherwise be offered twice — a "did you mean" list that repeats a name reads as a bug.
  The final ordering is by distance, then wrapper name, so the output is stable across runs.
- **Distance is case-insensitive Levenshtein.** Both sides are already normalised, and each character
  comparison is `char.ToUpperInvariant`, so `button` and `Button` cost nothing to differ.
- **`Parse` cleans up rather than complaining.** Components with a blank `Wrapper` and parameters with
  a blank `Name` are dropped; missing `components`, `parameters`, `events` and `slots` become empty
  collections. A catalogue file with no `components` key parses successfully into zero components.
- **`LoadEmbedded`'s failure message is the feature.** It states the resource name, the path the
  generator should write, and that the file is embedded only when it exists at build time — because
  "the catalogue is empty" and "the build did not embed the catalogue" look identical from outside and
  need different fixes.
- **The JSON property names are pinned with `[JsonPropertyName]` on private mirror records** rather
  than by a naming policy, and there is no source-generated context: the file is written elsewhere, and
  an unnoticed naming-policy change would silently deserialise to zero components.

## Gotchas

- **`Find` returning `null` is not an error path you can skip.** `AddComponentCommand` turns it into a
  suggestion list, and a name that exists in `tools/polaris-elements.json` but not here exits `0` —
  see [../Commands/AddComponentCommand.cs.md](../Commands/AddComponentCommand.cs.md).
- **The catalogue is generated, so a hand-added entry disappears on the next generator run.** Nothing
  in this class writes the file; `SourcePath` is documentation for whoever runs the generator.
- **`GeneratedBy` is optional and can be null**, so it cannot be used to prove the file is current.
- A JSON syntax error becomes a `CatalogueException` carrying the `JsonException` message, but a
  *shape* error (a number where a component array belongs) throws the same `JsonException` type and is
  reported the same way — the message is the only distinction.

## See also

- [../Commands/AddComponentCommand.cs.md](../Commands/AddComponentCommand.cs.md) — the only consumer.
- [../ComponentUsageTemplate.cs.md](../ComponentUsageTemplate.cs.md) — renders the snippet from these
  records.
- [../PolarisElementsReader.cs.md](../PolarisElementsReader.cs.md) — the Shopify-side element spec
  that answers "the element exists but the wrapper does not".
