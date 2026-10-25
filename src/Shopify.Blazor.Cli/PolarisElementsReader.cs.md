<!-- summary-of: PolarisElementsReader.cs -->
<!-- source-sha256: 8c926743fbf4 -->

# PolarisElementsReader.cs

Reads `tools/polaris-elements.json` — the Shopify-side element spec — and answers whether a name appears
in it, so `add component` can tell a developer that the wrapper they typed does not exist. The file is
optional by design: a parallel workstream owns it, its schema is not fixed, and a wrong or missing file
can only cost a better error message.

`Shopify.Blazor.Cli` · `static class` + `PolarisElements` record · the tolerant reader for the element spec

## Members

| Member | Signature | What it does |
|---|---|---|
| `RelativePath` | `const string` = `"tools/polaris-elements.json"` | The repository-relative path, also used in messages. |
| `Find` | `static string? Find(IFileSystem fileSystem, string startDirectory)` | The nearest `tools/polaris-elements.json`, walking from `startDirectory` up to eight parents. `null` when none. |
| `Read` | `static PolarisElements Read(IFileSystem fileSystem, string startDirectory)` | Finds and parses the file, collecting every name it recognises. Never throws. |
| `PolarisElements` | `sealed record PolarisElements(bool Exists, IReadOnlyList<string> ElementNames)` | The result: whether the file was there, and every string taken to be an element or component name. |
| `Contains` | `bool Contains(string name)` on the record | Whether `name` appears, compared after stripping decoration and separators. |

## Behaviour

- **Find** tries `<directory>/tools/polaris-elements.json` at the start directory and each parent, at
  most eight levels, stopping when the parent walk reaches an empty path. Eight levels is the whole
  budget: a project nested deeper finds nothing.
- **Read** parses with `JsonCommentHandling.Skip` and `AllowTrailingCommas = true`, so a spec carrying
  JSON5-style comments and trailing commas still loads. A `JsonException` is caught and reported as
  "exists but no names".
- **Collect** walks the whole document. Inside an object, a property whose **name** is `tag`, `tagName`,
  `tag_name`, `element`, `name`, `title`, `component` or `className` (matched case-insensitively) and whose
  value is a non-empty string contributes that string as a name; every other value is recursed into.
  Arrays are recursed item by item. Numbers, booleans and nulls are ignored.
- **Contains** normalises both sides by removing `Polaris`, then `s-`, then `-`, then `_`, and
  lower-casing with the invariant culture. That is why `FancyChart`, `fancy-chart` and `s-fancy-chart`
  all match: a literal comparison would miss the case this type exists for — a developer typing the
  wrapper name they wish existed.

## Gotchas

- **A malformed file answers "no" to everything.** `Read` returns `Exists = true` with an empty name
  list, so `Contains` is false for every name and reads exactly like "the spec does not have that
  element". Only the message a caller writes can tell the two apart.
- **`title` is treated as a name key.** A spec whose `title` holds prose contributes that sentence as an
  element name, so `Contains` can match something that is not an element at all.
- **The strips are substring replacements, not prefix strips.** `s-` is removed wherever it occurs, so a
  name with an `s` immediately before a hyphen loses it: `status-item` and `statu-item` normalise to the
  same string. The same applies to `Polaris` appearing mid-name.
- **Separators are deleted rather than mapped**, so `product-editor` and `producteditor` collide by
  design — which is what makes `FancyChart` and `fancy-chart` match, and what makes two genuinely
  different spellings indistinguishable.
- **A key outside the recognised list is invisible.** The schema is deliberately not modelled, so if the
  spec grows a new name-bearing key those names silently disappear from `Contains` until the list here is
  extended — this is the one place to tighten once the schema settles.
- **The catalogue, not this file, decides whether a component can be added.** The spec only improves the
  message for a name the catalogue already rejected; nothing here gates a write.

## See also

- [Commands/AddComponentCommand.cs.md](Commands/AddComponentCommand.cs.md) — the command that consults it.
- [Catalogue/PolarisCatalogue.cs.md](Catalogue/PolarisCatalogue.cs.md) — the catalogue that actually decides.
- [Abstractions/IFileSystem.cs.md](Abstractions/IFileSystem.cs.md) — the read seam, so the upward walk is testable.
