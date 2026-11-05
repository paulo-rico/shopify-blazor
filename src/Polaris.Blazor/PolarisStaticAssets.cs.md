<!-- summary-of: PolarisStaticAssets.cs -->
<!-- source-sha256: c37dd63db5fc -->

# PolarisStaticAssets.cs

The single source of the build stamp every URL this package hands out carries: the module import's and the stylesheet links'. A static web asset is served with an `ETag` and no `Cache-Control`, so without a changing URL a browser can keep a file from before a deploy for hours.

`Polaris.Blazor` · public static class · no dependencies

## Members

| Signature | What it does |
|---|---|
| `static string Version { get; }` | The assembly's `ManifestModule.ModuleVersionId` as 32 lowercase hex characters. Computed once per process; it changes on every build, which is the cache lifetime wanted — one deploy keeps its cache, the next gets new URLs. |
| `static string Url(string fileName)` | `_content/Polaris.Blazor/<fileName>?v=<Version>`. Root-relative, so it resolves against the app's `<base href>`. |

## Behaviour

- **The stamp is a module version id, not a package version.** Two builds of the same version still get different stamps, so a deploy that changes one line cannot be styled with the previous build's CSS. A package version would change only on a release, which is far too coarse for a cache key.
- Every URL this package emits goes through it: `PolarisInterop.VersionedModuleUrl` for the ES module, and `PolarisStylesheets` for the three head links. Both reading one value is what keeps a module and its stylesheet from two different builds being used together.

## Gotchas

- **`Version` is per process, not per request.** That is deliberate — the assembly cannot change under a running process — but it means a deploy must recycle the app before the new stamp is served (IIS does this on upload).
- **A relative URL does not inherit a base's query.** `new URL("./x.css", import.meta.url)` drops the module's `?v=`, which is why `polaris-interop.js` copies the stamp onto each stylesheet itself rather than relying on the base.
- The value is opaque and unordered: never compare two stamps to decide which build is newer.

## See also

- [Components/PolarisStylesheets.cs.md](Components/PolarisStylesheets.cs.md) — the head links that use it.
- [Interop/PolarisInterop.cs.md](Interop/PolarisInterop.cs.md) — the module import, stamped with the same value.
