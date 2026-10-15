# `shopify-blazor` CLI reference

`shopify-blazor` is the toolkit's optional command line tool. It scaffolds a project from the
template, adds a page or a component usage snippet, prints the local-configuration steps, and
diagnoses a project.

**It is a convenience wrapper and never the only way to do anything.** Every command maps onto a
plain `dotnet`, filesystem or configuration action you can take yourself:

| The CLI runs | You can run instead |
|---|---|
| `shopify-blazor new MyApp` | `dotnet new shopify-blazor -n MyApp` |
| `shopify-blazor add page Products` | create `Components/Pages/Products.razor` and paste the template below |
| `shopify-blazor add component Button` | read `docs/components.md`, or the generated `src/Shopify.Blazor.Cli/Catalogue/polaris-catalogue.json` |
| `shopify-blazor configure` | `dotnet user-secrets set "Shopify:ApiKey" …` |
| `shopify-blazor doctor` | read the checks below and look at the project |

The CLI has no hidden state, writes no marker files, and never deletes anything. Everything it
produces is an ordinary file that you can edit freely, and every command can be reproduced by hand
from this document.

## Installing

```sh
dotnet tool install --global Shopify.Blazor.Cli     # provides the `shopify-blazor` command
dotnet tool update  --global Shopify.Blazor.Cli
```

From a checkout of this repository, without installing anything:

```sh
dotnet run --project src/Shopify.Blazor.Cli -- doctor
```

> Running from the repository needs the redirected CLI home described in [AGENTS.md](../AGENTS.md):
> `DOTNET_CLI_HOME=$PWD/.dotnet NUGET_PACKAGES=$PWD/.nuget/packages`.

## Command surface

```
shopify-blazor new <Name> [--include-sample-pages <bool>] [--include-admin-api-example <bool>] [--output <dir>] [--dry-run]
shopify-blazor add page <Name> [--path <dir>] [--project <path>] [--nav] [--force] [--dry-run]
shopify-blazor add component <Name> [--path <file-or-dir>] [--project <path>] [--force] [--dry-run]
shopify-blazor configure [--check] [--project <path>]
shopify-blazor doctor [--project <path>] [--json]
shopify-blazor --help | --version
```

Options may be written as `--option value` or `--option=value`, and `-h` is accepted for `--help`.
Option names are matched case-insensitively. An unknown option, or an option whose value is
missing, is an error with a non-zero exit code — a mistyped `--force` is never silently ignored.

## Exit codes

| Code | Meaning |
|---|---|
| `0` | Success. For `doctor`, nothing FAILed. |
| `1` | A check failed, or `configure --check` found something missing. |
| `2` | The command line was wrong: unknown option, missing value, bad name, no project found. |
| `3` | A file already exists and `--force` was not given. |
| `4` | An external process failed — in practice, `dotnet new`. |
| `5` | A piece of the CLI's own data is missing, for example the generated component catalogue. |

`doctor` exits `0` when nothing FAILs, so it can gate CI directly:

```sh
shopify-blazor doctor --json | jq '.checks[] | select(.status == "FAIL")'
```

`WARN` never changes the exit code. A warning means "this looks worth a look" or "this could not be
decided offline", and a check that cannot decide must not fail a build.

---

## `new`

```sh
shopify-blazor new MyShopifyApp
shopify-blazor new MyShopifyApp --output ./apps
shopify-blazor new MyShopifyApp --include-sample-pages false --include-admin-api-example false
shopify-blazor new MyShopifyApp --dry-run
```

Creates a project by running `dotnet new shopify-blazor`, then prints the setup steps. The CLI does
not scaffold anything itself, so the template is the single source of the project's shape.

| Option | Default | Meaning |
|---|---|---|
| `--include-sample-pages <bool>` | `true` | Include the sample dashboard, products and gallery pages. |
| `--include-admin-api-example <bool>` | `true` | Include the `/api/products` endpoint and its service. |
| `--output <dir>` | current directory | Parent directory for the generated project. |
| `--dry-run` | off | Print the exact `dotnet` command and the directory it would create; write nothing. |

Both template parameters are always passed explicitly, so the generated project does not depend on
which template version happens to be installed.

**It refuses to run when the target directory exists and is not empty** (exit `3`). An existing but
empty directory is fine. This is the difference between scaffolding and merging: the template engine
would otherwise write into whatever is already there.

The generated project needs three values before it will run:

| Setting | Environment variable |
|---|---|
| `Shopify:ApiKey` | `SHOPIFY_API_KEY` |
| `Shopify:ApiSecret` | `SHOPIFY_API_SECRET` |
| `Shopify:AppUrl` | `SHOPIFY_APP_URL` |

and its README (in the generated project) covers the deployment and install steps.

## `add page`

```sh
shopify-blazor add page Products
shopify-blazor add page ProductEditor --path Components/Pages/Admin
shopify-blazor add page Products --nav
shopify-blazor add page Products --force
shopify-blazor add page Products --dry-run
```

Writes `<Name>.razor` — `Components/Pages/<Name>.razor` by default — containing:

- a `@page` directive whose route is the kebab-case form of the name (`Products` → `/products`,
  `ProductEditor` → `/product-editor`, `APIKeys` → `/api-keys`);
- `@rendermode InteractiveServer`, because a Blazor Web App is static without a render mode and
  renders every control inert ([ADR 0001](adr/0001-hosting-model.md));
- four branches — loading (`PolarisSpinner`), error (`PolarisBanner`), empty
  (`PolarisEmptyState`) and content — inside `PolarisPage` and `PolarisSection`.

| Option | Default | Meaning |
|---|---|---|
| `--path <dir>` | the project's `Components/Pages` | Directory for the new file. |
| `--project <path>` | search upwards from the current directory | The project to add the page to. |
| `--nav` | off | Also insert a navigation entry into the app's shell. |
| `--force` | off | Replace the file if it exists. Without this, an existing file is left untouched and the exit code is `3`. |
| `--dry-run` | off | Print what would be written, and write nothing. |

`--dry-run` still refuses an existing file without `--force`, so a rehearsal tells you what the real
run will do.

### `--nav`

The entry is inserted into the first of these that exists:

1. `Components/Layout/NavMenu.razor`
2. `Components/Layout/MainLayout.razor`
3. `Components/NavMenu.razor`

It goes before the first `<PolarisDivider />` when there is one, so it joins the navigation group
rather than landing below it. If the shell already links to the route, nothing is added. If no
navigation file exists at all, the command says so, **succeeds, and adds no entry** — it will not
guess where navigation lives and risk putting the link outside the page chrome.

The entry uses the shell's own idiom: a `PolarisButton` in a Polaris layout, an `<a>` in a plain
`<nav>`.

## `add component`

```sh
shopify-blazor add component Button
shopify-blazor add component PolarisButton --path Components/Pages/ButtonDemo.razor
shopify-blazor add component ChoiceList --path Components/Pages
shopify-blazor add component Button --dry-run
```

Writes a Razor snippet for a wrapper in the generated component catalogue
(`src/Shopify.Blazor.Cli/Catalogue/polaris-catalogue.json`), which is derived from the
Polaris.Blazor sources. The name may be given with or without the `Polaris` prefix.

| Option | Default | Meaning |
|---|---|---|
| `--path <file-or-dir>` | none — the snippet is printed, not written | A `.razor` file to write, or a directory (the file is named `<Wrapper>Usage.razor`). |
| `--project <path>` | the current directory | Root that a relative `--path` is resolved against. |
| `--force` | off | Replace the file if it exists. |
| `--dry-run` | off | Print the snippet and the path it would write; write nothing. |

The catalogue records each parameter's **name and type, but not a legal value for it**, so the
snippet is prose about the component inside Razor comments plus one minimal `<PolarisButton />`.
Inventing `Variant="…"` would produce a file that looks like working code and does not compile.

If the component is unknown, the command fails with the closest known names and the full list. If
`tools/polaris-elements.json` (the Shopify-side element spec, optional) names the element but the
toolkit has no wrapper, the command says exactly that and points at
`tools/generate-component.py`; that case succeeds, because the CLI cannot write a wrapper that does
not exist.

If the catalogue was never generated — it is embedded only when the file exists at build time — the
command fails with exit `5` and a message naming the file to produce.

## `configure`

```sh
shopify-blazor configure
shopify-blazor configure --check
shopify-blazor configure --check --project ./src/MyApp
```

Without flags it prints the setup steps: the Partner Dashboard settings (App URL, redirect URL,
access scopes) and the exact `dotnet user-secrets set …` commands, using the project's own
`UserSecretsId` when it declares one — a command with the wrong id writes to a store the app never
reads.

With `--check` it validates that `Shopify:ApiKey`, `Shopify:ApiSecret` and `Shopify:AppUrl` resolve,
from the environment variable or `appsettings*.json`. It exits non-zero and lists what is missing.
**It never prints a secret value**, only whether one is set and which layer supplied it.

It also fails when a non-empty `ApiSecret` is found in a committed `appsettings*.json`.

```
ok    Shopify:ApiKey       set (the environment variable SHOPIFY_API_KEY)
ok    Shopify:ApiSecret    set (the environment variable SHOPIFY_API_SECRET)
ok    Shopify:AppUrl       set (the environment variable SHOPIFY_APP_URL)

Configuration is complete. Values are never printed.
```

Note the precedence, because it is the cause of a specific confusing failure: an environment
variable overrides user secrets and `appsettings.json`. A key belonging to a *different* Shopify app
makes every session token fail as "another app" while the settings file looks correct. The check
reports the source so that is visible.

## `doctor`

```sh
shopify-blazor doctor
shopify-blazor doctor --project ./src/MyApp
shopify-blazor doctor --json
```

Runs the checks below against the nearest project (searching upwards from the current directory,
stopping at the repository root) and prints one line per check. `--project` names the project
explicitly; `--json` emits the same report in a machine-readable form and prints nothing else.

### Output format

```
$ shopify-blazor doctor --project /tmp/fixture/tests/Shopify.Blazor.FixtureApp
Checking Shopify.Blazor.FixtureApp.csproj in /tmp/fixture/tests/Shopify.Blazor.FixtureApp

PASS  dotnet-sdk        running framework 10.0.12 is at least the feature band global.json pins (10.0.100).
PASS  references        the project references Shopify.Blazor and Polaris.Blazor.
FAIL  configuration     a non-empty Shopify:ApiSecret is committed in appsettings.Development.json.
                        -> An API secret in a committed file is a leaked credential: rotate it in the Partner Dashboard. Keep appsettings.json's value empty and set the real one with 'dotnet user-secrets set "Shopify:ApiSecret" <value>' or the SHOPIFY_API_SECRET environment variable. The value is deliberately not printed here.
WARN  admin-api-version 2025-07 is stable but older than 2026-10.
                        -> Shopify retires a version about a year after it ships and then falls forward silently. Move to 2026-10 when convenient; see https://shopify.dev/docs/api/usage/versioning.
PASS  rendermode        @rendermode InteractiveServer is applied in Components/Routes.razor.
PASS  polaris-interop   found in the referenced project's wwwroot: /home/paul/aiprojects/shopify-blazor/src/Polaris.Blazor/wwwroot/polaris-interop.js

4 passed, 1 warned, 1 failed.
Exit code 1: fix the 1 failure(s) above. See docs/troubleshooting.md.
```

Any check that is not `PASS` is followed by a `->` hint on the next line.

### `--json`

```json
{
  "project": "/tmp/fixture/tests/Shopify.Blazor.FixtureApp/Shopify.Blazor.FixtureApp.csproj",
  "healthy": true,
  "checks": [
    {
      "name": "admin-api-version",
      "status": "WARN",
      "reason": "2025-07 is stable but older than 2026-10.",
      "hint": "Shopify retires a version about a year after it ships and then falls forward silently. Move to 2026-10 when convenient; see https://shopify.dev/docs/api/usage/versioning."
    }
  ]
}
```

`status` is one of `PASS`, `WARN`, `FAIL`; `hint` is `null` for a passing check; `healthy` is
`false` when anything FAILed. The document never contains a secret value.

### The checks

| Check | PASS | WARN | FAIL |
|---|---|---|---|
| `dotnet-sdk` | The running framework is on the same or a later release line as `global.json`'s `sdk.version`, and at least its feature band. | No `global.json`; no readable `sdk.version`; or a same-line version whose feature band cannot be decided from a runtime version. | The running framework's major/minor line is below the pin. |
| `references` | The project file references both `Shopify.Blazor` and `Polaris.Blazor`, as a `ProjectReference` or a `PackageReference`. | — | Either is missing. |
| `configuration` | `Shopify:ApiKey`, `ApiSecret` and `AppUrl` all resolve, and no committed settings file holds a non-empty `ApiSecret`. | — | A setting is missing, or a secret is committed. |
| `admin-api-version` | The version is the newest stable version in the CLI's table. | The version is a stable version that is older than the newest known one, is newer than the table (an out-of-date CLI), is not a `YYYY-MM` name, or is not set at all. | — |
| `rendermode` | `@rendermode InteractiveServer` appears as a live directive in at least one `.razor` file under the project (outside `bin`, `obj`, `node_modules`, `.git`, `.vs`). | — | It appears nowhere. |
| `polaris-interop` | The Polaris.Blazor reference resolves and `polaris-interop.js` is in it: the referenced project's `wwwroot`, or the file inside the restored package in the NuGet cache. | The package folder could not be located offline, or a `PackageReference` has not been restored. | A `ProjectReference`'s `wwwroot` lacks the file, or the restored package does not contain it. |

Two of the checks are worth explaining, because both failures are silent.

**`rendermode`.** A Blazor Web App is static unless a render mode is applied. The markup renders
perfectly and no handler is ever attached, so every button on the page does nothing — with no build
error, no exception and nothing in the console. The check strips Razor `@* … *@` and HTML comments
first, so a commented-out directive does not count. The fix is on the page, or on
`Components/Routes.razor` to apply it to every page.

**`admin-api-version`.** Shopify ships a stable version each quarter named `YYYY-MM` and retires each
one about a year later. Asking for a retired version does **not** fail: Shopify falls forward to an
older stable release and answers normally, with only the `X-Shopify-API-Version` response header to
show for it. The CLI keeps a small hand-maintained table of known stable versions and warns when the
project's is older than the newest entry, naming both. A version *newer* than the table is reported
as unknown, never as wrong, so an out-of-date CLI cannot tell you a working configuration is broken.
See <https://shopify.dev/docs/api/usage/versioning>.

**`polaris-interop`** is the strongest *offline* check available. It proves the file exists on the
path MSBuild will publish it from; it cannot prove the running server serves it. When it cannot
decide it warns and names the URL to open in the browser:
`/_content/Polaris.Blazor/polaris-interop.js` must return JavaScript, not a 404. That 404 is the
failure where the page renders and every control is inert.

## Reproducing each command by hand

```sh
# new
dotnet new install Shopify.Blazor.Templates
dotnet new shopify-blazor -n MyShopifyApp --include-sample-pages true --include-admin-api-example true

# add page — write Components/Pages/Products.razor
cat > Components/Pages/Products.razor <<'RAZOR'
@page "/products"
@rendermode InteractiveServer
<PolarisPage Heading="Products">
    <PolarisStack Gap="PolarisGap.Base">
        @if (_loading) { <PolarisSpinner AccessibilityLabel="Loading" /> }
        else if (_error is not null) { <PolarisBanner Heading="Failed" Tone="PolarisTone.Critical">@_error</PolarisBanner> }
        else if (_items is null || _items.Count == 0) { <PolarisEmptyState Heading="Nothing here yet" /> }
        else { @foreach (var item in _items) { <PolarisText>@item</PolarisText> } }
    </PolarisStack>
</PolarisPage>
RAZOR

# add component — read the generated inventory
python3 tools/generate-component-inventory.py
jq '.components[] | select(.wrapper == "PolarisButton")' src/Shopify.Blazor.Cli/Catalogue/polaris-catalogue.json

# configure
dotnet user-secrets set "Shopify:ApiKey" <client id>
dotnet user-secrets set "Shopify:ApiSecret" <client secret>
dotnet user-secrets set "Shopify:AppUrl" https://<your-server-hostname>

# doctor — the same checks, read from the project
grep -n "Razor" src/Shopify.Blazor/Shopify.Blazor.csproj          # Sdk must be Microsoft.NET.Sdk.Razor
grep -rn "@rendermode InteractiveServer" --include="*.razor" .
grep -n "AdminApiVersion" appsettings.json
ls src/Polaris.Blazor/wwwroot/polaris-interop.js
```

## See also

- [Getting started](getting-started.md) — the manual path through the same steps.
- [Troubleshooting](troubleshooting.md) — the failures `doctor` reports, in more detail.
- [Component reference](components.md) — what `add component` draws its names from.
- [Templates and CLI plan](templates-plan.md) — what is built, and what is not.
