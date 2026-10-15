# Getting started

From nothing to an embedded Shopify app that makes an authenticated Admin API call.

## 1. Confirm the requirements

```sh
dotnet --version        # 10.x — pinned in global.json
```

No npm, no bundler and no build step: Polaris and App Bridge load from Shopify's CDN.

## 2. Run it before configuring anything

```sh
dotnet run --project samples/Shopify.Blazor.SampleApp
```

Open <http://localhost:3458>. You should see a real Polaris page. A banner will say it is running
outside the Shopify Admin; that is correct and expected — App Bridge only exists inside the Admin.

This is the fastest way to prove the component library works on your machine. If the components
render as unstyled text, see [Troubleshooting](#troubleshooting).

### Or generate your own app

To start from a clean project rather than the sample:

```sh
dotnet new install Shopify.Blazor.Templates
dotnet new shopify-blazor -n MyShopifyApp
cd MyShopifyApp && dotnet run
```

The generated project has the same shell as the sample — App Bridge, the embedded framing policy, a
verified session-token path, one authenticated endpoint — plus a README with the setup steps, and no
sample pages if you pass `--include-sample-pages false`.

Two things help immediately afterwards:

```sh
shopify-blazor doctor          # checks for the failures that do not announce themselves
shopify-blazor add page Orders # a page with loading, empty and error states already structured
```

`doctor` is the one worth running first: it fails on a missing `@rendermode InteractiveServer`, a
non-empty `ApiSecret` in a committed `appsettings*.json`, and a project that does not reference the
toolkit. [The CLI reference](cli-reference.md) covers the rest.

> The packages are not published to nuget.org yet, so a generated project needs a local feed. The
> README the template writes gives the two commands for that.

## 3. Create the Shopify app

You need a Shopify Partner account and a development store.

1. In the Partner Dashboard, **Apps → Create app → Create app manually**.
2. Note the **Client ID** (this is the API key) and the **Client secret** (the API secret).
3. You will fill in the URLs in step 5, once you have a tunnel.

## 4. Start a tunnel

The app must be reachable over public HTTPS, because Shopify loads it inside the Admin:

```sh
ngrok http 3458
```

Note the `https://….ngrok-free.app` hostname it prints. The port must match the sample app's
(default `3458`).

> **Using a free ngrok domain?** Read
> [the interstitial section](troubleshooting.md#the-public-url-shows-ngroks-you-are-about-to-visit-page)
> first. ngrok's free plan serves a warning page to browsers, which means the Admin cannot embed the
> app no matter what the app does. This is worth knowing before you spend an evening debugging
> authentication.

## 5. Configure the app and the Dashboard

Point the Partner Dashboard at your tunnel:

| Setting | Value |
|---|---|
| App URL | `https://<your-tunnel>/` |
| Allowed redirection URL(s) | `https://<your-tunnel>/auth/callback` |
| Embedded | Yes |

Then give the app its credentials. **Never commit these.** The sample reads configuration in this
order, later wins:

1. `appsettings.json` (committed, empty)
2. `appsettings.Development.json` (committed, non-secret)
3. user secrets
4. **environment variables** — use these

```sh
export SHOPIFY_API_KEY=your_client_id
export SHOPIFY_API_SECRET=your_client_secret
export SHOPIFY_APP_URL=https://<your-tunnel>
dotnet run --project samples/Shopify.Blazor.SampleApp
```

Or, for a shell-independent setup:

```sh
cd samples/Shopify.Blazor.SampleApp
dotnet user-secrets set "Shopify:ApiKey" "your_client_id"
dotnet user-secrets set "Shopify:ApiSecret" "your_client_secret"
dotnet user-secrets set "Shopify:AppUrl" "https://<your-tunnel>"
```

The API key is public — it goes into the `shopify-api-key` meta tag so App Bridge can identify the
app. The API secret is a credential and must stay on the server. `AddShopifyBlazor` fails at startup
if it is missing, rather than at the first merchant request.

## 6. Install the app in the store

Open `https://<your-tunnel>/` and choose a development store when Shopify prompts. Because the app
is embedded and uses App Bridge, Shopify's own install and authorisation flow handles the grant; the
app never builds an OAuth redirect of its own.

## 6b. Check which credentials actually won

Configuration precedence causes a class of failure that looks like an authentication bug: an
environment variable silently **overrides** user secrets. If the value that wins belongs to a
different Shopify app, every session token is rejected as being for someone else.

The app logs which layer supplied the key at startup:

```
info: Shopify:ApiKey came from the user secrets store.
      Shopify app: ApiKey=1a2b3c… Secret=set (32 characters) AppUrl=https://… AccessTokenKind=Online AdminApiVersion=2026-10
```

If instead you see:

```
warn: Shopify:ApiKey came from the SHOPIFY_API_KEY environment variable. An environment variable
      overrides user secrets, so if this is not the app whose App URL is open in the Admin, every
      session token will be rejected as belonging to another app.
```

then either unset `SHOPIFY_API_KEY`, or make sure the App URL open in the Admin is that app. Precedence,
later wins:

1. `appsettings.json` (committed, empty in a generated project)
2. `appsettings.Development.json` (committed, non-secret)
3. **user secrets**
4. **environment variables**

The log line also prints the exact value that will be rendered into the page's
`shopify-api-key` meta tag, so you can compare it with the Admin's app at a glance.

## 7. Make an authenticated Admin API call

In the app, open **Products** and press **Refresh**. What happens:

1. The browser calls `shopify.idToken()` — App Bridge mints a short-lived session token.
2. The browser sends it to `/api/products` as `Authorization: Bearer <token>`.
3. The server verifies the HMAC-SHA256 signature using your app's API secret — no network call —
   checks the audience is this app, and confirms the `dest` claim names a real `myshopify.com` shop.
4. The server exchanges the session token for an Admin API **access token**.
5. The server runs the GraphQL query, and only the product data comes back to the browser.

The access token never reaches the browser. The session token never becomes an access token in the
browser.

Verify the guard yourself — all three should be `401`:

```sh
curl -i http://localhost:3458/api/products
curl -i -H 'Authorization: Bearer not-a-jwt' http://localhost:3458/api/products
```

## Troubleshooting

| Symptom | Cause |
|---|---|
| Page renders as unstyled text | Polaris did not load. Check the two CDN `<script>` tags and the `shopify-api-key` meta tag are in the page head, and that the browser can reach `cdn.shopify.com`. |
| Buttons look right and do nothing | Almost always the JavaScript module failed to load. Look for `Polaris interop module … could not be loaded` in the server log. That message exists because this failure is otherwise completely silent. |
| A banner says "Opened outside the Shopify Admin" | Expected unless you are loading the app *inside* the Admin. Being on the ngrok URL in a normal tab is not enough. |
| `Shopify:ApiSecret is not configured` at startup | The environment variables are not visible to the process. |
| `/api/products` returns 502 | The session token was accepted but Shopify refused the query. The response body carries Shopify's message. Usually a missing scope or a retired API version. |
| A modal never opens | Shopify only opens a modal in response to a user gesture. Use a `PolarisButton` with `CommandFor` rather than calling `ShowAsync()` from `OnInitialized`. |
| The admin shows a progress bar that never stops | `shopify.loading(true)` without the matching `false`. Use `IAppBridgeInterop.WithLoadingAsync`, which clears it in a `finally`. |
| `Field 'x' doesn't exist on type 'QueryRoot'` | The configured Admin API version does not match the field. Check `Shopify:AdminApiVersion` against <https://shopify.dev/docs/api/usage/versioning>. |
| A blank frame where the app should be | The app is refusing to be framed — see [Troubleshooting](troubleshooting.md#the-app-shows-an-empty-frame-in-the-admin). |
| `ShopifySessionToken was not authenticated` | Session token rejected. Run the diagnostic in [Troubleshooting](troubleshooting.md#authenticationscheme-shopifysessiontoken-was-not-authenticated); the usual cause is that the page and the server are configured for different apps. |

## After a reboot

Nothing here is a service, so a reboot leaves everything stopped and the app simply needs starting
again. In order:

```sh
# 1. Docker Desktop is a user service that starts with the graphical session. The dev-stack
#    containers are NOT needed by this app, but if Docker is running they publish port 3459, which
#    is fine — this app uses 3458. If Docker is up, the Ruby container comes back automatically.

# 2. Start the app and its tunnel together.
cd /home/paul/aiprojects/shopify-blazor
./bin/shopify-dev
```

`bin/shopify-dev` reads `PORT`, `TUNNEL` and `SHOPIFY_APP_URL` from `.env` (git-ignored), frees the
port, starts the tunnel, starts the app in Development, and prints the URLs. Ctrl+C stops what it
started.

**In the browser you use for the Shopify Admin,** visit the public URL once and click **Visit Site**
on ngrok's warning page — it appears once per browser per endpoint, and the Admin's iframe shares
the browser. Then open the app from the Admin.

### Things that are easy to trip over

| | |
|---|---|
| `SHOPIFY_API_KEY` / `SHOPIFY_API_SECRET` exported in your shell | They **override** user secrets. If they hold another app's values, every session token is rejected as belonging to a different app. The app logs which layer won at startup — check that line first. |
| The public URL must match the Partner Dashboard | The Admin loads whatever App URL the Dashboard holds. If that is a *different* app, App Bridge mints tokens for that app and this server refuses them. |
| Only one ngrok tunnel per reserved domain | ngrok is a single-tunnel agent. The Rails app next door (`shopify-jewellery-costing`) is configured with the **same** reserved domain and now uses port 3459, so the two cannot both be tunnelled at once. Whichever app you are working on owns the domain. |
| The app must run in Development for the diagnostics | `dotnet run` with no `ASPNETCORE_ENVIRONMENT` is Production: HSTS on, and `/api/diagnostics/*` not mapped. `bin/shopify-dev` sets Development for you. |
| A password-protected store is fine | Every dev store is password-locked and it does **not** affect session-token validation, which is local. (An earlier version of this toolkit wrongly depended on a storefront URL, which is what made this look like a problem.) |

### Verify it came up correctly

```sh
curl -s http://localhost:3458/api/diagnostics/session-token | python3 -m json.tool | head -20
```

That reports the API key the server is verifying against. It must equal the `content` of the
`shopify-api-key` meta tag in the page. Then press **Load Products** in the app.

## Next steps

- [Component reference](components.md) — all 59 components
- [Component inventory](component-inventory.md) — slots, events, accessibility notes and verification status per element
- [CLI reference](cli-reference.md) — `doctor`, `add page`, `configure`
- [Versioning](versioning.md) — what a version promises, and what it is pinned to
- [Authentication](authentication.md) — why the flow is shaped this way
- [ADR 0001](adr/0001-hosting-model-and-integration.md) — the architectural decisions
