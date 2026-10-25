<!-- summary-of: ProjectFileReader.cs -->
<!-- source-sha256: 2c27a6357fd6 -->

# ProjectFileReader.cs

Reads the `ProjectReference` and `PackageReference` entries out of a `.csproj` with regular expressions,
so `doctor` can say which toolkit pieces a project actually references. Also resolves a project
reference's relative path by hand, because the obvious `Path.GetFullPath` overload gets it wrong on
Linux.

`Shopify.Blazor.Cli` · `static partial class` plus two records · no base class

## Members

| Member | Signature | What it does |
|---|---|---|
| `ProjectReferences` | `sealed record (IReadOnlyList<string> Projects, IReadOnlyList<string> Packages)` | One project file reduced to the paths of its project references and the ids of its package references, in document order. |
| `ProjectReferences.References` | `bool References(string name)` | True when any project reference's file name, or any package id, equals `name` ignoring case. |
| `ProjectFileReader.ReadReferences` | `static ProjectReferences ReadReferences(IFileSystem fileSystem, string csproj)` | Reads the file (empty text when it does not exist), extracts both lists, trims each value and drops the empty ones. |
| `ProjectFileReader.ReadProjectReferencePath` | `static ProjectReferencePath ReadProjectReferencePath(string reference, string projectDirectory)` | Walks the reference segment by segment, applying `..` manually, and returns the resolved path with the original text. |
| `ProjectReferencePath` | `sealed record (string FullPath, string Original)` | The path the reference resolves to, and the text exactly as written in the project file. |

## Behaviour

- **Both reference forms are accepted on purpose.** The generated app takes project references from the
  template and a released app takes package references; `References` treats them as equivalent so
  neither shape is reported as wrong.
- `Projects` keeps each reference verbatim — `../../src/Shopify.Blazor/Shopify.Blazor.csproj`, with
  either separator — while the name comparison normalises `\` to `/`, keeps the last segment, and drops
  a `.csproj` suffix before comparing. A reference written as a bare name with no extension is compared
  whole.
- `Packages` holds the `Include` value only. A `Version` on the same element is not read, so
  `References("Polaris.Blazor")` is unaffected by which version is pinned.
- A project file that does not exist reads as empty instead of throwing, so the caller reports "the
  project does not reference X" rather than "the project could not be read" — the reader cannot tell the
  difference, and only the caller knows which is worth saying.

## Gotchas

- **`ReadProjectReferencePath` exists because `Path.GetFullPath(path, basePath)` is unsafe here.** On
  Linux that overload treats a path whose first segment is `..` as rooted and ignores the base directory,
  so `../../src/Polaris.Blazor/Polaris.Blazor.csproj` resolves against the filesystem root and a real
  reference silently becomes a missing one. The loop here applies each `..` with `Path.GetDirectoryName`,
  clamping at the root rather than throwing.
- **Nothing strips XML comments.** A commented-out `<ProjectReference Include="…"/>` is still counted, so
  a reference can look present long after it was disabled with a comment.
- **Only double-quoted `Include` attributes on an opening tag are matched.** Single-quoted attributes,
  `Update=`-only references from central package management, and `Exclude=` lists are all invisible.
- **The regexes are not an XML parser** — deliberately: an MSBuild file may carry conditions, comments
  and unevaluated properties that a strict parser rejects, and a `doctor` run that dies on an unreadable
  `.csproj` is worse than one that reports what it could read.
- There is no syntax error to report. A malformed project file yields whatever fragments matched, which
  can be an empty list that reads exactly like "no references".

## See also

- [ProjectLocator.cs.md](ProjectLocator.cs.md) — finds the `.csproj` this reader is handed.
- [StaticAssetCheck.cs.md](StaticAssetCheck.cs.md) — the main consumer, which resolves a Polaris project
  reference down to its `wwwroot`.
- [Abstractions/IFileSystem.cs.md](Abstractions/IFileSystem.cs.md) — the seam used instead of touching
  disk.
