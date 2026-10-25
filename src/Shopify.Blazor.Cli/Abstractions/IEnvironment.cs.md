<!-- summary-of: IEnvironment.cs -->
<!-- source-sha256: 8df5ca27c7d1 -->

# IEnvironment.cs

A three-member seam over the process environment — environment variables, the framework description and
the current directory — so `configure --check` and `doctor` can be tested without mutating the test
runner's own environment. `SystemEnvironment` is the real implementation the CLI uses.

`Shopify.Blazor.Cli.Abstractions` · `interface` plus a `sealed class` implementation · no base class

## Members

| Member | Signature | What it does |
|---|---|---|
| `GetEnvironmentVariable` | `string? GetEnvironmentVariable(string name)` | The variable's value, or null when it is unset. |
| `FrameworkDescription` | `string FrameworkDescription { get; }` | A human-readable name for the running framework, e.g. `.NET 10.0.1`. A description, not a parsed version. |
| `CurrentDirectory` | `string CurrentDirectory { get; }` | The process's current working directory. |
| `SystemEnvironment.GetEnvironmentVariable` | `string? GetEnvironmentVariable(string name)` | `Environment.GetEnvironmentVariable`. |
| `SystemEnvironment.FrameworkDescription` | `string FrameworkDescription { get; }` | `RuntimeInformation.FrameworkDescription`. |
| `SystemEnvironment.CurrentDirectory` | `string CurrentDirectory { get; }` | `Directory.GetCurrentDirectory()`, read on every access. |

## Behaviour

- The interface exists for a specific reason: **an environment variable silently overrides user secrets**,
  so a check that reads one has to be drivable with a fake. A test that set a real variable would leak
  into every other test in the process, and the failure would look like an unrelated test breaking.
- `CurrentDirectory` is read at the moment of the call, not captured in a constructor, so a command sees
  the directory the process is in when it runs.
- `FrameworkDescription` is deliberately unparsed here. [SdkVersionCheck.cs.md](../SdkVersionCheck.cs.md)
  is the only thing that interprets it, which keeps this interface free of version semantics.
- `Program.cs` builds one `SystemEnvironment` and passes it to every command, so all of them agree about
  the environment they are inspecting.

## Gotchas

- **`GetEnvironmentVariable` returning null and returning an empty string are different answers**, and
  callers test both: `StaticAssetCheck` treats an empty `NUGET_PACKAGES` as unset, while a value it
  cannot find falls through to the next candidate.
- **Nothing here is case-normalised.** On Windows variable names are case-insensitive at the OS level and
  on Linux they are not, so a fake must match the case the caller asks for.
- `FrameworkDescription` is free-form text from the runtime, not a `Version`; anything comparing it must
  parse it, and `.NET Framework` descriptions parse far more successfully than the doc comment on
  `SdkVersionCheck.ParseFrameworkVersion` claims.
- This interface does not expose the machine name, user, OS or command line — deliberately, so a fake has
  three things to implement and none of them can quietly become state.

## See also

- [IFileSystem.cs.md](IFileSystem.cs.md) — the sibling seam, for disk.
- [PhysicalFileSystem.cs.md](PhysicalFileSystem.cs.md) — the real `IFileSystem`; this file is its
  counterpart for the environment.
- [../SdkVersionCheck.cs.md](../SdkVersionCheck.cs.md) — parses `FrameworkDescription`.
- [../StaticAssetCheck.cs.md](../StaticAssetCheck.cs.md) — reads `NUGET_PACKAGES`, `HOME` and
  `USERPROFILE` through it.
