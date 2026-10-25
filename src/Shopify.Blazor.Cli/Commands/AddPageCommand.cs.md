<!-- summary-of: AddPageCommand.cs -->
<!-- source-sha256: e0de07f38185 -->

# AddPageCommand.cs

`shopify-blazor add page <Name>` — writes a Razor page with the loading, error, empty and content
states already structured and `@rendermode InteractiveServer` at the top. It can also insert a
navigation entry, and a dry run makes exactly the same decisions as a real run and then writes nothing.

`Shopify.Blazor.Cli.Commands` · the `add page` command · no base class

## Members

| Member | Signature | What it does |
|---|---|---|
| `AcceptedOptions` | `static readonly IReadOnlyList<OptionSpec>` | `path` (value), `project` (value), `nav` (flag), `force` (flag), `dry-run` (flag). |
| `.ctor` | `AddPageCommand(IFileSystem, IEnvironment, IConsole)` | The three seams every filesystem command takes. |
| `Run` | `int Run(ParsedArguments arguments, string currentDirectory)` | The whole command. Returns `Success` (0), `Usage` (2) or `AlreadyExists` (3). |

## Behaviour that is not in the member list

The order of decisions in `Run`:

1. Exactly one positional, or exit 2 with an example of the correct spelling.
2. `NameHelper.IsValidComponentName` — a name that cannot become a C# identifier or a file name is a
   usage error, not a page called `My Page.razor`.
3. The project is located with `--project`, and **when that fails the command retries with `--path` as
   the search origin** — a developer scaffolding into a brand-new directory has no project there yet,
   and the locator walks up from it. Only when both fail is the locator's own message printed (exit 2).
4. The name becomes a class name (`PageTemplate.ClassName`, PascalCase) and a route
   (`PageTemplate.Route`, kebab-case with a leading slash): `add page product-editor` writes
   `ProductEditor.razor` at `/product-editor`.
5. The directory is `--path` resolved against `currentDirectory`, or `<project>/Components/Pages`.
6. An existing file without `--force` stops the command (exit 3) — **this check runs before the
   dry-run branch**, deliberately.
7. A real run writes `PageTemplate.Render(name)` and prints the path, the route and the render-mode
   directive. A dry run prints what it *would* do — "would create" or "would replace", the route, the
   directive and the four states — then `Re-run without --dry-run to write it.`
8. `--nav` calls `NavMenuInserter.Insert(fileSystem, project.Directory, className, route)` and prints
   only `insertion.Message`; without `--nav` the command says so and names the route an entry would
   need.

The resulting page is opinionated on purpose: `@page` with the kebab route, `@rendermode
InteractiveServer`, explicit `@using Polaris.Blazor.Events` and `@using Shopify.Blazor.AppBridge`, an
injected `IAppBridgeInterop`, and `@code` holding `_loading`, `_error` and `_items` with a single
`LoadAsync(PolarisEventArgs)` handler that guards against re-entry and assigns `_error` rather than
swallowing the exception. See [../PageTemplate.cs.md](../PageTemplate.cs.md) for the exact markup.

## Gotchas

- **A dry run can refuse.** The exists-without-`--force` check comes first, so a rehearsal against an
  occupied path exits 3 — which is the point: a run that would be refused is refused rather than
  looking clean and then failing for real.
- **`--path` plays two roles.** It is both the destination directory and the fallback project search
  origin, so a `--path` outside the project still has to sit under a `.csproj` somewhere above it.
- **The navigation result is text only.** `NavInsertionResult` carries `Inserted` and `FilePath`, but
  the command prints `Message` alone and always ends with `ExitCodes.Success` — including when the
  inserter found no shell file and added nothing. Read the line, not the exit code.
- **`--nav` runs after the page is written.** If the insertion point cannot be found, the page exists
  and the message says the entry has to be added by hand; nothing is rolled back.
- **No option short-circuits the render-mode directive.** `PageTemplate` always emits it; there is no
  flag to scaffold a static page, because a static page renders and never runs a handler.
- Nothing is ever deleted, and `--force` replaces only the one page file it was pointed at.

## See also

- [../PageTemplate.cs.md](../PageTemplate.cs.md) — the page source, including the four states.
- [../NavMenuInserter.cs.md](../NavMenuInserter.cs.md) — where the navigation entry goes, and when it
  refuses to guess.
- [../NameHelper.cs.md](../NameHelper.cs.md) — the class-name and route spellings.
- [../ProjectLocator.cs.md](../ProjectLocator.cs.md) — the upward search behind `--project`.
