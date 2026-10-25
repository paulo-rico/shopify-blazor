<!-- summary-of: ProjectLocator.cs -->
<!-- source-sha256: ddec65c052e7 -->

# ProjectLocator.cs

Finds the one project file a command should act on by walking upwards from a starting path, so
`shopify-blazor doctor` works from a page's own directory. It never guesses: two `.csproj` files in one
directory is an error naming both, and the walk stops at the first repository root.

`Shopify.Blazor.Cli` · `sealed class` plus two records · no base class

## Members

| Member | Signature | What it does |
|---|---|---|
| `ProjectFile` | `sealed record (string FilePath, string Directory)` | A discovered project: the full path of the `.csproj` and the directory holding it. |
| `ProjectFile.FileName` | `string FileName { get; }` | The bare file name, for messages. |
| `ProjectSearchResult` | `sealed record (ProjectFile? Project, string? Error)` | Exactly one of the two is set by the factories. |
| `ProjectSearchResult.Found` | `bool Found { get; }` | `Project is not null`. |
| `ProjectSearchResult.Ok` | `static ProjectSearchResult Ok(ProjectFile project)` | A found project, no error. |
| `ProjectSearchResult.Fail` | `static ProjectSearchResult Fail(string error)` | No project, one message saying why. |
| `ProjectLocator..ctor` | `ProjectLocator(IFileSystem fileSystem)` | Takes the filesystem seam; it is the class's only state. |
| `ProjectLocator.Find` | `ProjectSearchResult Find(string? startPath, string currentDirectory)` | The search. `startPath` may be a directory, a project file, or null/whitespace meaning "start at `currentDirectory`". |

## How `Find` decides

1. **An explicit project file wins outright** when `startPath` ends in `.csproj` and exists — the
   developer already chose. Its directory is the parent.
2. Otherwise the starting directory is `startPath` if it is a directory, else its parent. If neither
   applies, the result is a failure naming `startPath`.
3. From there, each directory is listed for `*.csproj`: **exactly one** is the answer, **more than one**
   is a failure listing the names plus `Pass --project <path> to choose one.`, **none** moves to the
   parent.
4. Walking stops at a directory holding `.git`, `.hg`, `.svn` (file or directory) or a `*.sln`,
   `*.slnx`, `*.slnf` file. The final failure names the directory the search started from.

## Behaviour

- **The candidate check happens before the boundary check**, so a repository root that contains exactly
  one `.csproj` is still found; the boundary only prevents walking *above* it.
- **`global.json` is deliberately not a boundary.** It usually sits above several projects, so treating
  it as one would stop a search that was about to succeed one level further up.
- **A directory the filesystem refuses to list ends the walk without failing it.** In `Find` the
  exception breaks the loop, so the report is the ordinary "no project file was found"; in the boundary
  probe the same exception counts as a root, because a parent that cannot be listed is a boundary in
  practice. Both are silent about the permission problem — pass `--project` to get past it.
- The boundary markers are globbed only when they contain `*`, which is how `*.sln` is recognised as any
  file with that suffix.

## Gotchas

- **A mistyped `--project` does not fail.** If `startPath` ends in `.csproj` but does not exist, step 1
  is skipped, the path is not a directory, so the walk begins at its parent — and can return a
  *different* project from the directory the typo pointed into.
- **The walk cannot escape into a parent checkout.** Run from a nested clone inside another repository
  and the search fails at the inner root rather than finding the outer project.
- `Find` resolves a relative `startPath` with `Path.GetFullPath(startPath, currentDirectory)` — the same
  overload [ProjectFileReader.cs.md](ProjectFileReader.cs.md) documents as treating a leading `..` as
  rooted on Linux. A `..`-leading `startPath` is therefore not reliably relative to `currentDirectory`.
- **Two projects in one directory is always an error**, even when one is obviously the app; there is no
  "prefer the one named after the directory" rule to fall back on.
- `ProjectSearchResult` is a record with two nullable parts, so one can be built directly in a state
  neither factory produces: `Found == false` with a null `Error`. Read `Error` defensively.

## See also

- [ProjectFileReader.cs.md](ProjectFileReader.cs.md) — what happens to the project file once found.
- [Abstractions/IFileSystem.cs.md](Abstractions/IFileSystem.cs.md) — the seam the walk depends on,
  including its "a missing directory enumerates as empty" contract.
- [Commands/DoctorCommand.cs.md](Commands/DoctorCommand.cs.md) — resolves `--project` through this class
  and reports the failure text when it finds nothing.
