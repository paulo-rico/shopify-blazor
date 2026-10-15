# Versioning and compatibility

What a version number promises, and which moving parts a version is pinned to.

## The policy

The toolkit follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html), with the standard
pre-1.0 caveat: **while the major version is 0, a minor release may contain a breaking change.** That
is stated here rather than discovered by a consumer, and every breaking change is listed under a
`### Changed` or `### Removed` heading in the [changelog](../CHANGELOG.md).

| Change | Version bump |
|---|---|
| A new component, a new parameter, a new event, new documentation | minor (or patch) |
| A bug fix that does not change the rendered markup or a public signature | patch |
| Renaming or removing a public type or parameter, changing the attribute a wrapper renders, raising the minimum .NET version, or dropping a Shopify app target | major — or minor while 0.x |

Four things are **not** breaking by this policy, and a consumer should not expect a version bump to
warn about them:

1. **A new Polaris element being wrapped.** Shopify adds components; a wrapper for one is additive.
2. **A new parameter on an existing wrapper** with a default that preserves the previous rendering.
3. **Shopify changing a component's behaviour.** The toolkit renders Shopify's elements; their
   behaviour and appearance are Shopify's to change, and the toolkit's own version cannot pin it
   (see Polaris below).
4. **An Admin API version falling out of support**, which Shopify handles by silently falling forward.

## Compatibility matrix

Current, as of `0.1.0-preview.1`:

| Component | Pinned to | Notes |
|---|---|---|
| `Polaris.Blazor`, `Shopify.Blazor` | this repository's version | The two packages version together and are tested together |
| Shopify Polaris web components | `polaris-1.js` (Polaris 1.1 at the time of writing) | Loaded from Shopify's CDN by `ShopifyAppBridgeHead`; see below |
| Shopify App Bridge | 4 (`app-bridge.js`, unversioned URL) | Shopify serves the current release from that URL |
| .NET | `net10.0` | Pinned in `global.json` and in the template's `--framework` choice |
| Admin API | `2026-10` | `ShopifyOptions.DefaultAdminApiVersion`, overridable per app |
| Shopify app target | **Full-page embedded admin app** | Not checkout, POS, theme or customer-account extensions — see ADR 0001 |
| Blazor hosting model | Blazor Web App, Interactive Server | The only supported model |

## Polaris web components

The library is loaded from Shopify's CDN, not from a NuGet package, so the toolkit cannot pin it for
you. The default is:

```csharp
AppBridgeOptions.PolarisScriptUrl = "https://cdn.shopify.com/shopifycloud/polaris-1.js";
```

`polaris-1.js` tracks the newest stable release **inside** Polaris 1, which means Shopify can add
components and attributes under a running app without a toolkit release. That is usually what you
want. To freeze the release instead, set the mutable URL to a pinned one:

```csharp
builder.Services.Configure<Shopify.Blazor.AppBridge.AppBridgeOptions>(options =>
{
    // Polaris 1.1, frozen. Bug fixes and new components do not arrive until this changes.
    options.PolarisScriptUrl = "https://cdn.shopify.com/shopifycloud/polaris-1.1.js";
});
```

The component inventory records the Polaris version each wrapper was verified against, per element:
[docs/component-inventory.md](component-inventory.md).

**Keep the wrapper and the runtime in step.** If a pinned `polaris-1.1.js` is in use, every component
in this toolkit has been verified against 1.1 and nothing else. If you advance to a newer Polaris
release, run `tools/verify-in-browser.mjs` against the component gallery before shipping: it asserts
that each element is defined, upgraded and has a shadow root, which is the difference between a
working control and a perfectly rendered inert one.

## Admin API versions

Shopify ships a stable Admin API version each quarter and retires each after roughly twelve months,
and **a retired version does not error**: Shopify falls forward to the oldest accessible stable
release and answers normally, with only an `X-Shopify-API-Version` response header to show for it.
That is why `ShopifyOptions.AdminApiVersion` is explicit and why `shopify-blazor doctor` warns when
the configured version is older than the newest known one.

Before changing it, read the version table at <https://shopify.dev/docs/api/usage/versioning> and the
release notes for the version you are moving to. The toolkit's own tests assert the client's handling
of the version header rather than the version's value.

## .NET

`net10.0` is the only supported target framework, pinned in `global.json` with
`rollForward: latestFeature`. Raising it is a breaking change for consumers on an older SDK, and it
happens in a release that says so.

The template's `--framework` parameter exists so the choice is explicit in a generated project; it
offers one value on purpose. A choice that produces an untested combination is worse than no choice,
and `tools/verify-template.py` builds every combination the template offers.

## App Bridge

The App Bridge URL is unversioned by design — Shopify serves the current release from it, and there is
no supported channel that pins a version. A change in App Bridge therefore reaches a deployed app
without a toolkit release. Behaviours the toolkit depends on are `shopify.idToken()`, toast, modal,
navigation, the resource picker and the save bar; each is covered by a test or by the browser
verification script, so a break shows up as a failure rather than as a silently inert button.

## Upgrading the toolkit

```sh
# 1. read what changed
#    CHANGELOG.md, and docs/versioning.md if the major or minor version moved

# 2. update the packages (or the template's --toolkit-version, for a generated project)
dotnet add package Polaris.Blazor
dotnet add package Shopify.Blazor

# 3. rebuild and run your own tests
dotnet build && dotnet test

# 4. confirm the UI still upgrades, if you changed the Polaris channel
node tools/verify-in-browser.mjs http://127.0.0.1:3459/components
```

If a page renders output that looks correct but does nothing, the toolkit's
[troubleshooting guide](troubleshooting.md) starts with the two causes that produce exactly that
symptom: a missing render mode, and a Polaris module that did not load.

## Cutting a release

The mechanics live in [`.github/workflows/release.yml`](../.github/workflows/release.yml), triggered
by a `v*` tag. It is the only place that publishes, and it is deliberately separate from CI:

1. Move the `[Unreleased]` entries in the changelog under the new version and date.
2. Confirm the version in `Directory.Build.props` — including the `toolkitVersion` default in
   `.template.config/template.json`, which `tools/verify-template.py` compares against it.
3. Tag and push: `git tag -a v0.1.0-preview.1 -m "0.1.0-preview.1" && git push origin v0.1.0-preview.1`.
4. The workflow builds, tests, packs, checks that both JavaScript-bearing packages actually contain
   their module, publishes to nuget.org **only if the `NUGET_API_KEY` secret is configured**, and
   attaches the packages to a GitHub release.
