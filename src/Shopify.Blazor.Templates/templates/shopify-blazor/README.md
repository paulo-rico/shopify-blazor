# Shopify.Blazor.SampleApp

An embedded Shopify admin app built with **Blazor Web App (Interactive Server)**, Razor and
Shopify's own **Polaris web components**. There is no npm, no bundler and no build step for the UI:
Polaris and App Bridge both come from Shopify's CDN.

## What is already wired up

- **App Bridge 4** loaded from the CDN, with the `shopify-api-key` meta tag rendered from
  configuration.
- **Session-token authentication**: the App Bridge token in the `Authorization` header is verified
  server-side against the shop's published keys, then exchanged for an Admin API access token that
  never leaves the server.
- **An authenticated backend endpoint** (`/api/products`) that calls the Admin API, with Shopify's
  three distinct failures — rejected token, missing scope, refused query — reported separately.
- **Embedded framing**: the app can be framed by the Admin and by nothing else, which is not the
  default and is invisible when wrong. See "If the app will not embed" below.
- **Development-only diagnostics** that explain a rejected token instead of returning a bare 401.

## 1. Create the Shopify app

In the [Partner Dashboard](https://partners.shopify.com) create an app, then set:

| Setting | Value |
|---|---|
| App URL | `https://<your-tunnel>/` |
| Allowed redirection URL(s) | `https://<your-tunnel>/` |
| Embedded | **Yes** |
| Scopes | `read_products,write_products` (at minimum `read_products` for the products page) |

Copy the **Client ID** (the API key) and **Client secret**.

## 2. Configure the credentials

User secrets keep them out of the repository. `UserSecretsId` is already set, so this works as-is:

```sh
dotnet user-secrets set "Shopify:ApiKey"    "<client id>"
dotnet user-secrets set "Shopify:ApiSecret" "<client secret>"
dotnet user-secrets set "Shopify:AppUrl"    "https://<your-tunnel>"
```

Environment variables work too, and are what a deployed app should use:

```sh
export SHOPIFY_API_KEY=<client id>
export SHOPIFY_API_SECRET=<client secret>
export SHOPIFY_APP_URL=https://<your-tunnel>
```

> An environment variable **silently overrides** user secrets. If a key from a different Shopify app
> is set in your shell, every session token is rejected as "another app" and nothing says why. The
> startup log names the layer that supplied the key — read it if tokens are rejected.

Never put a secret in `appsettings.json`. The `.gitignore` here ignores `appsettings.Local.json` and
`.env` for local overrides, and `dotnet user-secrets` writes outside the repository entirely.

## 3. Run it

```sh
dotnet run
```

It starts on <http://localhost:3458>. Without credentials the app still renders — it says on the
Dashboard that Shopify is not configured rather than failing at boot.

## 4. Open it inside the Shopify Admin

The Admin only frames an **HTTPS** URL, so expose the local server with a tunnel:

```sh
ngrok http 3458
```

Put the tunnel hostname in `Shopify:AppUrl` and in the Partner Dashboard, then open the app from
your development store's **Apps** menu.

## Where things live

| Path | What it is |
|---|---|
| `Program.cs` | Composition root: services, framing policy, endpoints |
| `Components/App.razor` | The single HTML document; loads App Bridge and Polaris |
| `Components/Routes.razor` | The router, with the `@rendermode InteractiveServer` that makes the app interactive |
| `Components/Layout/MainLayout.razor` | The embedded shell and navigation |
| `Components/Pages/Dashboard.razor` | Home page; reports whether the app is embedded and configured |
| `Components/Pages/Products.razor` | Loads products through the authenticated endpoint |
| `Components/Pages/Components.razor` | A gallery of every Polaris component the toolkit wraps |
| `Services/ProductsService.cs` | The Admin API call, server-side |
| `wwwroot/js/shopify-fetch.js` | The only JavaScript: mints a session token and calls your own endpoints |
| `appsettings.json` | Non-secret defaults, including the Admin API version |

## The three facts that explain most surprises

1. **A Blazor Web App is static unless a render mode is applied.** Static pages render perfect markup
   and attach no handlers, so every control looks right and does nothing. `Routes.razor` carries
   `@rendermode InteractiveServer` for this reason.
2. **Polaris booleans are presence attributes.** `disabled="false"` is still true to a custom
   element. The wrappers render `true` as a bare attribute and omit `false`.
3. **You cannot restyle Polaris components.** Their internals live in a shadow root and follow the
   merchant's admin branding. Appearance comes from component properties, never from your CSS.

## Toolkit version

This project references:

```xml
<PackageReference Include="Polaris.Blazor" Version="TOOLKIT_PACKAGE_VERSION" />
<PackageReference Include="Shopify.Blazor" Version="TOOLKIT_PACKAGE_VERSION" />
```

To work against a locally built toolkit instead of NuGet, pack it and point a `nuget.config` in this
directory at the output folder:

```sh
# in the shopify-blazor repository
dotnet pack Shopify.Blazor.slnx -c Release -o artifacts
```

```xml
<!-- nuget.config, beside this README -->
<configuration>
  <packageSources>
    <add key="local" value="/absolute/path/to/shopify-blazor/artifacts" />
  </packageSources>
</configuration>
```

## If something does not work

| Symptom | Cause and fix |
|---|---|
| The page renders but no control does anything | The render mode is missing. Check `@rendermode InteractiveServer` on the router. |
| Polaris elements are unstyled or inert | `polaris-1.js` did not load, or App Bridge's meta tag has no API key. Check the browser console and the `<head>`. |
| Every request is 401 | A token arrived and was rejected, or none arrived. In Development, open `/api/diagnostics/session-token` and `/api/diagnostics/rejections`. |
| "Shopify request failed" while the API returned 200 | A missing scope: Shopify answers an unauthorised field with HTTP 200 and an `errors` array. Add the scope and reopen the app. |
| The Admin shows a blank frame | The app is refusing to be framed. `UseShopifyEmbeddedFraming` must be registered **before** `UseAntiforgery`, because `OnStarting` callbacks run last-registered-first. |
| `shopify` is undefined in the browser | App Bridge is not loaded, or the page is open in a plain tab instead of the Admin. App Bridge only exists inside the Admin. |
| Tokens are rejected as "another app" | The API key in use belongs to a different Shopify app. Check the startup log for which configuration layer supplied it. |

## Next steps

- [Toolkit repository](https://github.com/paulo-rico/shopify-blazor) — component reference, ADRs and
  troubleshooting notes
- Add a page: `shopify-blazor add page Settings`
- Add a component example: `shopify-blazor add component DataTable`
- Check the project: `shopify-blazor doctor`
