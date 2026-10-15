# Templates and CLI — plan for phases 4–6

The immediate deliverables (phases 1–3) are done. This is what is left, in dependency order. It is a
plan, not a description: nothing here is built yet.

## Phase 4 — `dotnet new` template

Use the standard template engine. A custom CLI is not needed for this and phase 5 depends on the
template, not the other way round.

**Repository layout.** A template belongs in its own project that points at content, so the template
can be packed without the sample app becoming part of it:

```
src/Shopify.Blazor.Templates/
├── Shopify.Blazor.Templates.csproj      (PackageType=Template)
├── templates/
│   └── shopify-blazor/
│       ├── .template.config/template.json
│       └── content/…
```

`content/` is a copy of `samples/Shopify.Blazor.SampleApp` with the sample pages reduced and the
credentials replaced by placeholders. Do not hand-maintain a second copy of the shell — copy the
sample and keep the differences to configuration and page set.

**Parameters.** Start with four and no more, until the first template is stable:

| Parameter | Type | Default | Notes |
|---|---|---|---|
| `--name` / `-n` | string | required | Project and namespace name |
| `--framework` | choice | `net10.0` | Only the version the toolkit supports |
| `--include-sample-pages` | bool | `true` | Dashboard, products, components gallery |
| `--include-admin-api-example` | bool | `true` | The `/api/products` endpoint and its service |

Resist adding a hosting-model choice: Interactive Server is the only supported model (ADR 0001), and
a choice that produces an untested combination is worse than no choice.

**Configuration.** Generate placeholders and instructions, never credentials:

- `appsettings.json` with empty `Shopify:ApiKey` / `Shopify:ApiSecret` / `Shopify:AppUrl`
- a README with the getting-started steps and the environment-variable names
- `UserSecretsId` set, so `dotnet user-secrets` works out of the box

**Acceptance.** On a clean machine: `dotnet new install`, `dotnet new shopify-blazor -n MyApp`,
supply credentials, follow the README, `dotnet run`, see the app in a development store. The
generated project must be an ordinary solution that a developer can edit freely — no secret markup,
no generated-and-do-not-edit files.

## Phase 5 — CLI

Only after the template is stable. The CLI is a convenience wrapper over `dotnet new` and the
filesystem; it must never be the only way to do something.

```
shopify-blazor new MyShopifyApp
shopify-blazor add page Products
shopify-blazor add component DataTable
shopify-blazor doctor
```

**`doctor` is the one to build first.** It has the highest value per line and no generation risk:

- the installed .NET SDK satisfies `global.json`
- the project references `Shopify.Blazor` and `Polaris.Blazor`
- `Shopify:ApiKey` / `ApiSecret` / `AppUrl` are present, and `ApiSecret` is not in a committed file
- the Admin API version is a current stable one (compare against the published list)
- the `@rendermode InteractiveServer` directive is present — its absence is a silent failure
  (ADR 0001)
- `/_content/Polaris.Blazor/polaris-interop.js` resolves

**`new`** shells out to `dotnet new shopify-blazor` and then prints the setup steps.

**`add page`** writes a `.razor` page with loading, empty and error states already structured, and
offers to add a navigation entry. It must refuse to overwrite an existing file without an explicit
`--force`.

**`add component`** is *not* the same as phase 6's generation: it inserts an example usage of a
supported component into a page. Reach for it only once the component catalogue exists.

## Phase 6 — Component coverage and generation

**Catalogue first.** Every component already has a machine-readable description:
`tools/generate-component-reference.py` derives the tag name, parameters, types, docs and events
from the source. Extend that script's output into a JSON catalogue and add the fields the plan asks
for that source cannot supply — minimum Polaris version, accessibility requirements, verification
status. It is currently derived from code, so it cannot drift; keep it that way.

**Generation: Razor shells, enums, event callbacks and tests are all mechanical.** The wrapper
pattern is uniform enough that ~80% of a new straightforward component is a template fill, and the
reference generator is already most of the input it needs.

**Do not generate** anything where correctness is not mechanical: event payload adapters, `Properties`
for values that must be set after upgrade, slot wiring, or anything with a non-trivial slot
contract. Those are the parts that need a human, and generating them produces plausible-looking code
that silently does the wrong thing.

**A generator is only worth building when adding a component by hand becomes the bottleneck.** With
the inventory complete and the pattern uniform, that has not happened yet.

## Current component coverage

Complete: all 50 documented top-level elements are wrapped, in 49 classes (`s-text` and `s-paragraph`
share `PolarisText`), plus the 10 composed sub-elements — `s-choice`, `s-option`, `s-option-group`,
`s-grid-item`, `s-list-item`, `s-table-header-row`, `s-table-header`, `s-table-body`, `s-table-row`,
`s-table-cell`. The generated [reference](components.md) marks which are named in tests; all of them
are.

`PolarisChoiceList` was the last notable absence, and it earned its reputation: it is the only
multi-select, and `values` is an array property, which is the path that exposed a latent bug —
`ClearedProperties` was documented and never passed to `IPolarisInterop.ClearPropertiesAsync`, so a
stale selection could never be dropped. That is exactly the kind of silent gap a by-hand wrapper still
catches and a generator would not.
