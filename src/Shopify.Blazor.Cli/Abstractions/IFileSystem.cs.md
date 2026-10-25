<!-- summary-of: IFileSystem.cs -->
<!-- source-sha256: 50f7a0967a37 -->

# IFileSystem.cs

The narrow, synchronous slice of the filesystem the CLI needs, so a command can be tested without
touching a real disk. There is no delete and no move in it: the CLI must never remove anything.

`Shopify.Blazor.Cli.Abstractions` · `interface` · implemented by `PhysicalFileSystem`

## Members

| Member | Signature | What it does |
|---|---|---|
| `FileExists` | `bool FileExists(string path)` | Whether a file exists there. |
| `DirectoryExists` | `bool DirectoryExists(string path)` | Whether a directory exists there. |
| `ReadAllText` | `string ReadAllText(string path)` | Reads a UTF-8 text file. |
| `WriteAllText` | `void WriteAllText(string path, string contents)` | Writes UTF-8 text, creating parent directories as needed. |
| `CreateDirectory` | `void CreateDirectory(string path)` | Creates the directory and any missing parents. |
| `EnumerateFileSystemEntries` | `IEnumerable<string> EnumerateFileSystemEntries(string path)` | Immediate child paths, files and directories; an empty sequence when the directory does not exist. |
| `EnumerateFiles` | `IEnumerable<string> EnumerateFiles(string path, string searchPattern)` | Files matching the pattern **directly** in `path` — not recursively. |
| `EnumerateFilesRecursive` | `IEnumerable<string> EnumerateFilesRecursive(string path, string searchPattern)` | The same pattern in `path` and everything below it. |
| `EnumerateDirectories` | `IEnumerable<string> EnumerateDirectories(string path)` | Immediate child directories. |

## Behaviour

- **Every method here exists because a command calls it.** A broad `System.IO` mirror would make the fake
  harder to trust than the code it stands in for, so the surface stays at exactly what the commands use.
- **Nothing deletes, moves or copies.** The only destructive operation available is overwriting a whole
  file, which means a command cannot remove a source file even by accident.
- `EnumerateFiles` and `EnumerateFilesRecursive` are two members rather than one with a search-option
  parameter, so a caller cannot accidentally recurse. `ProjectLocator` wants the shallow one; `StaticAssetCheck`
  wants the deep one.
- `WriteAllText` creating parents is what allows `add page --path new/place`; `CreateDirectory` is exposed
  separately for a caller that needs the directory without a file.
- The interface is why the checks can be faked in tests: `ProjectLocator`, `ProjectFileReader`,
  `RenderModeScanner` and `StaticAssetCheck` all take it as a parameter and never call `System.IO` to
  read — path arithmetic still comes from `Path`, which touches no disk.

## Gotchas

- **A missing directory is only promised to enumerate as empty for `EnumerateFileSystemEntries`.** That
  is the documented contract of that one member; other callers still guard, which is why `ProjectLocator`
  wraps its listings in a `try`/`catch` for `IOException` and `UnauthorizedAccessException`.
- **Nothing is normalised to a full path by this interface.** A relative path is resolved by the
  implementation against the *process's* current directory, so a command must resolve `--path` and
  `--project` itself before calling — which the commands do, through `IEnvironment.CurrentDirectory`.
- **`ReadAllText` has no "try read" sibling.** A caller that wants to tolerate a missing file must test
  `FileExists` first; `ProjectFileReader` does exactly that and treats absence as empty text.
- **`EnumerateFilesRecursive` follows the whole tree**, including `bin`, `obj` and `.git` when pointed at
  a repository root. The cost lands on `StaticAssetCheck`, and the ignorance of build directories is why
  `RenderModeScanner` has its own walk instead.

## See also

- [PhysicalFileSystem.cs.md](PhysicalFileSystem.cs.md) — the real implementation and what it adds.
- [IEnvironment.cs.md](IEnvironment.cs.md) — the sibling seam, for the process environment.
- [../ProjectLocator.cs.md](../ProjectLocator.cs.md) — the upward walk built on shallow enumeration.
- [../StaticAssetCheck.cs.md](../StaticAssetCheck.cs.md) — the deep enumeration's one consumer.
