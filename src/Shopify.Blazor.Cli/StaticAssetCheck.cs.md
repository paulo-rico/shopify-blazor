<!-- summary-of: StaticAssetCheck.cs -->
<!-- source-sha256: 0714505a39f4 -->

# StaticAssetCheck.cs

Checks, entirely offline, that `/_content/Polaris.Blazor/polaris-interop.js` will exist when the app is
served. The failure it prevents is the worst one in the toolkit: the app builds, the page renders, and
every control does nothing because the interop module 404'd — logged in one place and nowhere else.

`Shopify.Blazor.Cli` · `static class` plus a helper class, one enum and one record · no base class

## Members

| Member | Signature | What it does |
|---|---|---|
| `StaticAssetOutcome` | `enum` | `Found`, `Indeterminate`, `Missing`. The middle value means the file could not be located offline, so the report says how to confirm it in a browser. |
| `StaticAssetCheckResult` | `sealed record (StaticAssetOutcome Outcome, string? Path, string Reason, string? Hint)` | What was found, where it was found or searched, one line of explanation, and what to do when it is not a clean pass. |
| `StaticAssetCheck.PublicUrl` | `const string` = `"/_content/Polaris.Blazor/polaris-interop.js"` | The URL the browser must be able to fetch. Every hint names it. |
| `StaticAssetCheck.FileName` | `const string` = `"polaris-interop.js"` | The file that has to ship. |
| `StaticAssetCheck.Evaluate` | `static StaticAssetCheckResult Evaluate(IFileSystem fileSystem, IEnvironment environment, ProjectReferences references, string projectDirectory)` | Decides which of the two reference shapes applies and checks the corresponding place on disk. |
| `NuGetPackageRoot.Find` | `static string? Find(IFileSystem fileSystem, IEnvironment environment, string projectDirectory)` | Locates the global packages folder without network access, or null. |

## Which branch runs

1. **A `ProjectReference` to `Polaris.Blazor` wins over a package reference**, even if the referenced
   project is not on disk — the shape the template and the sample app use, where the source project's
   `wwwroot` is what gets served. Found on disk: `Found` when `<project>/wwwroot/polaris-interop.js`
   exists, `Missing` when the project exists but the file does not, and `Indeterminate` when the project
   directory itself is gone (`Restore it, or build once …`).
2. **No reference to `Polaris.Blazor` at all** is `Indeterminate` with no path, saying the asset has no
   source and the project needs the reference.
3. **A `PackageReference`** goes through `NuGetPackageRoot.Find`, then looks for the restored package
   under `<root>/polaris.blazor`, then searches that directory recursively for the file name. A root that
   cannot be located, or a package folder that does not exist, is `Indeterminate` naming the browser
   check; a restored package without the file is `Missing`.

## `NuGetPackageRoot.Find`, in order

1. `NUGET_PACKAGES`, but only when it names an existing directory.
2. `packageFolders` in `obj/project.assets.json` — authoritative, because a restore recorded the folder it
   actually used — taking the first entry that exists.
3. `~/.nuget/packages`, from `HOME` or `USERPROFILE`, when it exists.
4. Otherwise null.

## Gotchas

- **This is a proxy, and it says so.** Only fetching the URL in a browser is conclusive, so every
  non-pass branch names `PublicUrl` rather than claiming the app is broken.
- `Missing` and `Indeterminate` mean different things and are mapped differently by `doctor`: `Missing`
  is a definite FAIL with a fix, `Indeterminate` is a WARN with a way to confirm. Folding them together
  loses the only signal that separates "packed without its web assets" from "I could not look".
- **The package directory is spelled `polaris.blazor`**, because NuGet lower-cases package folders. On a
  case-sensitive filesystem a differently-cased folder reads as "no restored package" — an Indeterminate,
  not a Missing.
- **`DirectoryExists` gates the assets file, not the read.** `obj/project.assets.json` is parsed with
  `JsonDocument`, and only `JsonException` is swallowed; any other read failure propagates out of the
  check.
- **A project reference that exists but has no `wwwroot/polaris-interop.js` is `Missing`, not
  `Indeterminate`** — the difference between "the script did not ship" and "the library moved" is stated
  in the hint, including the reminder that the library must use `Microsoft.NET.Sdk.Razor`.
- The recursive package search takes the **first** match it enumerates, so an old and a new copy in the
  same folder resolve to whichever the filesystem lists first.
- A project that references only `Shopify.Blazor` still needs the interop file if it uses a component,
  but this check reports Indeterminate because it has nothing on disk to look at.

## See also

- [ProjectFileReader.cs.md](ProjectFileReader.cs.md) — produces the `ProjectReferences` this check
  branches on, and resolves the project reference path.
- [Commands/DoctorCommand.cs.md](Commands/DoctorCommand.cs.md) — the `polaris-interop` check that maps
  these outcomes onto PASS, WARN and FAIL.
- [Abstractions/IEnvironment.cs.md](Abstractions/IEnvironment.cs.md) — the environment-variable seam used
  for `NUGET_PACKAGES`, `HOME` and `USERPROFILE`.
- [Abstractions/IFileSystem.cs.md](Abstractions/IFileSystem.cs.md) — the filesystem seam, including the
  recursive enumeration this check depends on.
