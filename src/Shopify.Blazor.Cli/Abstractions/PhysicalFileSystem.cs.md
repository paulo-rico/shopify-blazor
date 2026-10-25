<!-- summary-of: PhysicalFileSystem.cs -->
<!-- source-sha256: dd71c079193f -->

# PhysicalFileSystem.cs

The real filesystem behind `IFileSystem`, so a command can be tested against a fake disk. Its one
behaviour worth knowing is that every enumeration returns an empty sequence for a directory that does
not exist instead of throwing, and that a write creates its parent directory first.

`Shopify.Blazor.Cli.Abstractions` · `sealed class` · implements `IFileSystem` · no base class

## Members

| Member | Signature | What it does |
|---|---|---|
| `FileExists` | `bool FileExists(string path)` | `File.Exists(path)`. A directory at that path answers `false`. |
| `DirectoryExists` | `bool DirectoryExists(string path)` | `Directory.Exists(path)`. |
| `ReadAllText` | `string ReadAllText(string path)` | `File.ReadAllText(path)` — UTF-8 unless the file carries a byte-order mark. Throws when the file is missing; there is no "try read". |
| `WriteAllText` | `void WriteAllText(string path, string contents)` | Creates the file's parent directory when one is named, then writes the whole text. Overwrites unconditionally — the caller owns the exists-and-`--force` decision. |
| `CreateDirectory` | `void CreateDirectory(string path)` | `Directory.CreateDirectory` — idempotent, and creates any missing parents. |
| `EnumerateFileSystemEntries` | `IEnumerable<string> EnumerateFileSystemEntries(string path)` | Immediate children, files and directories. |
| `EnumerateFiles` | `IEnumerable<string> EnumerateFiles(string path, string searchPattern)` | Files matching the pattern directly in `path`. |
| `EnumerateFilesRecursive` | `IEnumerable<string> EnumerateFilesRecursive(string path, string searchPattern)` | The same pattern, `SearchOption.AllDirectories` below `path`. |
| `EnumerateDirectories` | `IEnumerable<string> EnumerateDirectories(string path)` | Immediate child directories. |

## Behaviour that is not in the member list

- **A missing directory enumerates as empty, never throws.** All four enumeration members test
  `Directory.Exists` first and return `[]`, so `ProjectLocator` can walk upwards without guarding each
  step and `add component` can look for `tools/polaris-elements.json` in directories that may not
  exist.
- **Writing creates the tree.** `WriteAllText` calls `CreateDirectory` on `Path.GetDirectoryName(path)`
  when it is non-empty, which is why `add page --path new/place` and `add component --path dir` succeed
  against a directory nobody created. It also means a mistyped `--path` silently produces a new
  directory tree rather than an error.
- **There is no delete and no move.** `IFileSystem` deliberately has neither, so no command can remove
  a file even by accident; the only destructive operation available is overwriting a whole file.
- **Exceptions are not translated.** `ReadAllText` on a missing file throws
  `FileNotFoundException`/`DirectoryNotFoundException`, and a listing refused by the OS throws
  `UnauthorizedAccessException` or `IOException`. The callers that care (`ProjectLocator`,
  `PolarisElementsReader`) catch those themselves; this class deliberately does not.

## Gotchas

- **The type's own summary promises path normalisation that the code does not perform.** The doc
  comment says "Every path is normalised to a full path so that a later `FileExists` on the same string
  means the same thing", but every method passes its argument straight to `System.IO`. Relative paths
  are therefore resolved against the *process's* current directory, not against the directory the
  caller thinks it is working in — which is why the commands resolve `--path` and `--project` with
  `Path.GetFullPath(…, currentDirectory)` before touching the filesystem.
- **`EnumerateFilesRecursive` follows the whole tree.** Pointed at a repository root it will descend
  into `bin`, `obj` and `.git`; `RenderModeScanner` and `StaticAssetCheck` are where the cost lands.

## See also

- [IFileSystem.cs.md](IFileSystem.cs.md) — the contract, and why it is this narrow.
- [../ProjectLocator.cs.md](../ProjectLocator.cs.md) — the upward walk that depends on "missing
  directory is empty".
- [../Commands/AddPageCommand.cs.md](../Commands/AddPageCommand.cs.md) — writes a page through this
  class, parent directories included.
