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
| Shopify Polaris web components | `polaris-2.js` (Polaris 2.0 at the time of writing) | Loaded from Shopify's CDN by `ShopifyAppBridgeHead`; see below |
| Shopify App Bridge | 4 (`app-bridge.js`, unversioned URL) | Shopify serves the current release from that URL |
| .NET | `net10.0` | Pinned in `global.json` and in the template's `--framework` choice |
| Admin API | `2026-10` | `ShopifyOptions.DefaultAdminApiVersion`, overridable per app |
| Shopify app target | **Full-page embedded admin app** | Not checkout, POS, theme or customer-account extensions — see ADR 0001 |
| Blazor hosting model | Blazor Web App, Interactive Server | The only supported model |

## Polaris web components

The library is loaded from Shopify's CDN, not from a NuGet package, so the toolkit cannot pin it for
you. The default is:

```csharp
AppBridgeOptions.PolarisScriptUrl = "https://cdn.shopify.com/shopifycloud/polaris-2.js";
```

`polaris-2.js` tracks the newest stable release **inside** Polaris 2, which is the channel Shopify's
[versioning reference](https://shopify.dev/docs/api/app-home/latest/web-components/versioning) names
for production: it means Shopify can ship bug fixes, accessibility improvements and new components
under a running app without a toolkit release. That is usually what you want. To freeze the release
instead, set the mutable URL to a pinned one:

```csharp
builder.Services.Configure<Shopify.Blazor.AppBridge.AppBridgeOptions>(options =>
{
    // Polaris 2.0, frozen. Bug fixes and new components do not arrive until this changes.
    options.PolarisScriptUrl = "https://cdn.shopify.com/shopifycloud/polaris-2.0.js";
});
```

**Why Polaris 2 and not 1.** A 2.0 bundle carries both the current admin theme and Admin Next and
follows whichever theme the merchant's admin is using, so one bundle serves both looks; there is no
per-theme URL and no flag. That is the whole of the change: measured by diffing the shipped bundles,
`polaris-1.js` (1.1.0) and `polaris-2.js` (2.0.0) define **the same 74 `s-*` element names** — so no
wrapper in this toolkit is orphaned by the move, and `minPolarisVersion` stays at each element's true
floor of 1.1 rather than being bumped to 2.0, which would claim the element was introduced there.

**Do not load `polaris-2.0-rc.js`.** It is a frozen release candidate (2.0.0-rc.3) that
`polaris-2.js` has superseded: it receives no fixes, and its tag names the candidate rather than the
release that shipped. If a source suggested it, the stable channel above is what that source meant.

The component inventory records the Polaris version each wrapper was verified against, per element:
[docs/component-inventory.md](component-inventory.md).

**Keep the wrapper and the runtime in step.** The move to the 2.0 channel was verified with
`tools/verify-in-browser.mjs --assert` against both the component gallery (`/components`, 60 listed
elements) and the dashboard's composed data grid (`/`): every listed element was defined, upgraded and
had a shadow root — 0 undefined, 0 unupgraded — and every behavioural assertion passed, including the
accordion's `aria-expanded` in `s-clickable`'s shadow root, the popover's declarative `commandFor`
open, the range slider's drag-then-`change` re-query, and spreadsheet-style cell editing. Re-run it
after **any** change to the Polaris channel: it asserts the difference between a working control and a
perfectly rendered inert one, which no component test can see.

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
