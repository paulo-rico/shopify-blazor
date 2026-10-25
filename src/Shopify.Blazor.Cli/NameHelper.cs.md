<!-- summary-of: NameHelper.cs -->
<!-- source-sha256: 9b3dd0cb078a -->

# NameHelper.cs

Turns a developer-supplied name into the three spellings the toolkit expects: a kebab-case route, a
PascalCase C# class and file name, and a yes/no on whether the name can become both. It exists
because a route spelled differently from the identifier is not a build error — it is a 404, or worse, a
route that shadows another page's, so the spelling is decided in one place.

`Shopify.Blazor.Cli` · `static class` · the naming rules behind `add page`

## Members

| Member | Signature | What it does |
|---|---|---|
| `ToKebabCase` | `static string ToKebabCase(string name)` | `Products` → `products`, `ProductEditor` → `product-editor`, `APIKeys` → `api-keys`. Returns `string.Empty` for null, empty or whitespace. |
| `ToPascalCase` | `static string ToPascalCase(string name)` | `product-editor` and `product editor` both → `ProductEditor`. Returns `string.Empty` for null, empty or whitespace. |
| `IsValidComponentName` | `static bool IsValidComponentName(string name, out string reason)` | Whether the name can become a C# type name and a file name; `reason` explains a refusal and is `string.Empty` on success. |

## Behaviour

**`ToKebabCase`** builds the result character by character from the trimmed input:

- Every non-alphanumeric character becomes a single `-`, never a run of them — an existing dash before
  a separator suppresses the new one — so `Product - Editor` and `Product-Editor` both give
  `product-editor`. A dash is trimmed from both ends afterwards, so a leading or trailing separator
  cannot produce `-product` or `product-`.
- Uppercase is lowercased, and a `-` is inserted before an uppercase letter in exactly two cases: the
  previous character is a lowercase letter or a digit (`ProductEditor`, `Product2Editor`), or the next
  character is lowercase, which splits the last capital off a run (`APIKeys` → `api-keys`,
  `IOStream` → `io-stream`). `API` on its own stays `api`.

**`ToPascalCase`** uppercases the first alphanumeric character after any run of non-alphanumerics and
appends everything else unchanged. It is a word-break fixer, not a case normaliser: `myPage` → `MyPage`,
`product_editor` → `ProductEditor`, but `APIKeys` is already `APIKeys` and `MY-PAGE` becomes `MYPAGE`.

**`IsValidComponentName`** refuses, in order: an empty or whitespace name ("The name is empty."); a name
whose Pascal form is empty or does not start with a letter; and a name containing any character in
`Path.GetInvalidFileNameChars()`. It only decides whether the name is *usable* — it validates the
trimmed name and quotes the supplied one in its reasons.

## Gotchas

- **The two converters do not have a failure signal.** Unusable input returns an empty string, so a
  caller that skips validation builds a route of `/` (the home route) or an empty class name. The
  guard is a separate call, and validation is a step the caller must not skip.
- **A name that starts with a digit gets the wrong explanation.** `2col` produces the Pascal form
  `2col`, fails the `IsLetter` test, and is reported as "does not contain any letters" — the message was
  written for the empty case, and the real problem is that an identifier cannot start with a digit.
- **The invalid-character test is platform-dependent.** `Path.GetInvalidFileNameChars()` on Windows
  includes `< > : " / \ | ? *` and the control characters; on Unix it is only `/` and NUL. A name such
  as `Report: Q3` is refused on Windows and accepted on Linux, where `ToKebabCase` quietly turns the
  colon into a dash.
- **A space in a name passes validation.** Space is not an invalid file-name character, so `My Page` is
  accepted and becomes the class `MyPage` — the space only affects the file name the caller builds.
- **This is not a uniqueness check.** It takes no `IFileSystem`, so it cannot know that a page with the
  same route or class already exists; only the caller can tell whether the name collides.
- **Reasons quote the untrimmed input** while validation runs on the trimmed form, so `'  x  '`-style
  names produce messages with the stray whitespace still in them.

## See also

- [PageTemplate.cs.md](PageTemplate.cs.md) — the only in-repo consumer: route and class name for a page.
- [Commands/AddPageCommand.cs.md](Commands/AddPageCommand.cs.md) — validates before it renders.
- [Commands/AddComponentCommand.cs.md](Commands/AddComponentCommand.cs.md) — the other naming caller.
