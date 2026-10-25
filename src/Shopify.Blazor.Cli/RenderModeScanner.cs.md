<!-- summary-of: RenderModeScanner.cs -->
<!-- source-sha256: fa5abbbbe5ce -->

# RenderModeScanner.cs

Searches a project's `.razor` files for `@rendermode InteractiveServer`. This is the highest-value check
the CLI runs: without a render mode a Blazor Web App renders perfectly and no handler is ever attached,
so every button on the page is inert and nothing — not the build, not a test, not the console — says so.

`Shopify.Blazor.Cli` · `static partial class` plus one record · no base class

## Members

| Member | Signature | What it does |
|---|---|---|
| `RenderModeScan` | `sealed record (int FilesScanned, IReadOnlyList<string> FilesWithDirective)` | How many `.razor` files were read, and the paths of those carrying the directive. |
| `RenderModeScan.Found` | `bool Found { get; }` | True when at least one file carries the directive. |
| `RenderModeScanner.Directive` | `const string` = `"@rendermode InteractiveServer"` | The exact text searched for, for messages and tests. |
| `RenderModeScanner.Scan` | `static RenderModeScan Scan(IFileSystem fileSystem, string projectDirectory)` | Walks every `.razor` file under the directory, counts them, and collects the ones that carry the directive. |
| `RenderModeScanner.HasDirective` | `static bool HasDirective(string content)` | Tests one file's text, after comments have been stripped. |
| `RenderModeScanner.RazorFiles` | `static IEnumerable<string> RazorFiles(IFileSystem fileSystem, string projectDirectory)` | Lazily yields every `.razor` file below the directory, skipping build output. |

## What counts as the directive

- It must be **alone on its line**: only spaces and tabs may precede it, and only spaces, tabs and an
  optional carriage return may follow. So `@rendermode InteractiveServer` inside a longer line of markup
  or C# does not count, and neither does anything after it on the same line.
- **The attribute form does not count.** `@rendermode="InteractiveServer"` — what a component or a
  `<Routes>` element carries — fails the match because `=` follows the name instead of whitespace.
- **Only `InteractiveServer` counts.** `InteractiveWebAssembly`, `InteractiveAuto`, and
  `@rendermode` followed by a field or variable are all reported as no render mode.
- **Comments are stripped first**, both `@* … *@` and `<!-- … -->`, so a commented-out directive cannot
  be mistaken for a live one. This matters concretely: the template plan quotes a directive inside a
  Razor comment, and treating that as real is exactly the false pass the check exists to prevent.

## Behaviour

- `RazorFiles` is an iterative stack walk: it yields the files in a directory, then pushes its
  subdirectories, skipping `bin`, `obj`, `node_modules`, `.git` and `.vs` by name, case-insensitively.
  **The order is not sorted** — subdirectories are pushed in enumeration order and popped last-in
  first — so nothing may depend on which file is listed first.
- Everything else is descended, including hidden directories such as `.template`, so a template copy of
  a real page is scanned too.
- `Scan` reads each file once and increments the count before the directive test, so `FilesScanned`
  includes files that have no directive. It does not catch a read failure: an unreadable `.razor` throws.

## Gotchas

- **The trailing `\r?` in the match is load-bearing.** A CRLF file ends that line with `\r`, and
  `[ \t]*$` cannot match it — multiline `$` anchors before the `\n`, not before the `\r`. Without the
  optional `\r`, every CRLF project was reported as having no render mode at all, so `doctor` FAILed a
  project that was perfectly fine.
- **`FilesScanned == 0` is a different failure from `Found == false`.** No `.razor` files at all means
  the directory is wrong (or the app is somewhere else), not that the directive is missing; `doctor`
  prints the count for exactly that reason.
- **The comment strip is a plain regex replace with no nesting awareness.** An `@*` inside a string
  literal or in prose pairs with the next `*@` and can blank out a real directive between them.
- **A `.razor` file under a directory literally named `obj` is skipped** even when it is hand-written
  source, because the skip is by directory name only.
- The directive is matched case-sensitively, as Razor requires.

## See also

- [Commands/DoctorCommand.cs.md](Commands/DoctorCommand.cs.md) — turns this scan into the `rendermode`
  check, printing the file that proves a pass.
- [Abstractions/IFileSystem.cs.md](Abstractions/IFileSystem.cs.md) — the seam whose "missing directory
  enumerates as empty" contract lets the walk run unguarded.
- [StaticAssetCheck.cs.md](StaticAssetCheck.cs.md) — the other check that guards a failure where the
  page looks perfect and does nothing.
