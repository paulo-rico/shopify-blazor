<!-- summary-of: NavMenuInserter.cs -->
<!-- source-sha256: 4ab74b320b44 -->

# NavMenuInserter.cs

Adds one navigation entry to an app's shell file, choosing the insertion point by structure rather than
rewriting the file — before the first `<PolarisDivider />` if there is one, otherwise before the last
closing tag that starts its own line. When neither exists it writes nothing and says so, because an
entry appended to the bottom of a layout can land outside the page chrome and look like it silently
failed.

`Shopify.Blazor.Cli` · `static class` + `NavInsertionResult` record · driven by `add page --nav`

## Members

| Member | Signature | What it does |
|---|---|---|
| `CandidateFiles` | `static readonly IReadOnlyList<string>` | The shell files searched, in order: `Components/Layout/NavMenu.razor`, `Components/Layout/MainLayout.razor`, `Components/NavMenu.razor`. |
| `Insert` | `static NavInsertionResult Insert(IFileSystem fileSystem, string projectDirectory, string label, string href)` | Inserts the entry into the first candidate that exists. Reads, splices and writes the whole file. |
| `Render` | `static string Render(string shellContent, string label, string href, string indentation)` | Builds the entry line, in the shell's own idiom. Ends with a newline. |
| `NavInsertionResult` | `sealed record NavInsertionResult(bool Inserted, string? FilePath, string Message)` | `Inserted` — whether the file changed; `FilePath` — the file that was changed, when one was; `Message` — a line for the developer. |

## Behaviour

- **Choosing the file.** The candidates use forward slashes in the constant and are translated with
  `Path.DirectorySeparatorChar` before `Path.Combine`, then the first that exists wins. None existing is
  the only outcome that returns `FilePath = null`.
- **Refusing a duplicate.** An ordinal substring test for `Href="<href>"` or `href="<href>"` runs first;
  a hit returns `Inserted = false` with the message "Navigation already links to …", so re-running
  `add page --nav` for the same page is safe and adds nothing.
- **Finding the point.** The line start of the first `<PolarisDivider` wins; failing that, the code walks
  backwards through `</` occurrences for a line whose only content before the tag is whitespace, and
  takes that line's start.
- **Indenting.** The entry copies the literal leading spaces or tabs of the line it is inserted before,
  falling back to eight spaces when that line has no indentation.
- **Rendering the entry.** If the shell mentions `<PolarisPage` or `<PolarisButton` anywhere, the entry is
  a `PolarisButton` with `Href`, `Target="PolarisLinkTarget.Self"` and
  `Variant="PolarisButtonVariant.Tertiary"`; otherwise it is a plain `<a href="…">label</a>`.
- The message names only the file's base name, while `FilePath` is the full combined path.

## Gotchas

- **`Inserted = false` does not mean `FilePath` is null.** Three of the four non-inserting outcomes — a
  duplicate, an unfindable insertion point — carry the candidate path, so a caller that treats a null
  path as the only "nothing done" signal will report the wrong thing.
- **An insertion index of 0 is refused.** A shell whose very first line holds the divider, or whose only
  own-line closing tag is on the first line, is treated as having no safe place to insert rather than
  being prefixed.
- **The inserted line always ends in `\n`.** On a CRLF checkout the new entry is the one LF-only line in
  the file, which shows up as a mixed-ending diff and can trip an editor's normalisation later.
- **The duplicate check is a raw substring match.** `Href='/x'` with single quotes, a differently cased
  attribute name, or an attribute written with extra spaces all fail to match — and a **commented-out**
  `<a href="/x">` does match, so the entry is skipped even though nothing visible links there.
- **The Polaris-versus-`<a>` decision is a whole-file substring test.** A shell that mentions
  `<PolarisButton` only inside a Razor comment still receives component markup.
- **Neither `label` nor `href` is escaped or validated.** A double quote in either breaks the generated
  attribute and produces markup that renders wrong without any error.
- **The file is rewritten from the copy read at the start**, so an edit made between the read and the
  write is lost — the whole file goes through `WriteAllText`, not a targeted patch.

## See also

- [Abstractions/IFileSystem.cs.md](Abstractions/IFileSystem.cs.md) — the read/write seam this goes through.
- [Commands/AddPageCommand.cs.md](Commands/AddPageCommand.cs.md) — where `--nav` is honoured.
- [PageTemplate.cs.md](PageTemplate.cs.md) — the page whose route becomes the `href` here.
