<!-- summary-of: Program.cs -->
<!-- source-sha256: f404a85b47a0 -->

# Program.cs

The template's server in one file: startup, the middleware order, the Development-only diagnostics and
the one authenticated endpoint. Nothing here is per-page — it decides which services exist, in which
order the pipeline runs, and what a request can reach.

`Shopify.Blazor.SampleApp` · top-level statements, the app's entry point · template file, so sections are wrapped in `//#if` directives

## Startup, in order

| Step | Call | What it does |
|---|---|---|
| 1 | `AddRazorComponents().AddInteractiveServerComponents()` | Registers server-side components and the Interactive Server render mode. The mode itself is applied in `Routes.razor`, not here. |
| 2 | `Configuration["Shopify:ApiKey"]` ?? `SHOPIFY_API_KEY` | The API key, configuration first, environment second. |
| 3 | `shopifyConfigured` | True only when the key **and** the secret (`Shopify:ApiSecret` ?? `SHOPIFY_API_SECRET`) are both non-blank. |
| 4 | `AddShopifyBlazor(configuration, validateOnStart: shopifyConfigured)` | The toolkit's Shopify services. Startup validation is skipped while no credentials exist, so the app runs before the Shopify app has been created; nothing that needs the key works in that state. |
| 5 | `Configure<ShopifyAuthenticationOptions>` | `IncludeFailureDetail` is on in Development only, so a rejected token answers with the reason locally and a bare `Unauthorized` in production. |
| 6 | `AddPolarisBlazor()` | The Polaris JS interop layer; without it components render and nothing they raise reaches C#. |
| 7 | `AddProblemDetails()`, `AddScoped<ProductsService>()` | Standard problem-details bodies; the products service only with `include-admin-api-example`. |
| 8 | `LogShopifyConfiguration(configuration)` | Logs which configuration layer supplied the API key, because an environment variable silently overrides user secrets. |
| 9 | `UseExceptionHandler("/Error", createScopeForErrors: true)` | Non-Development only. `UseHsts()` is present but commented out. |
| 10 | `UseShopifyEmbeddedFraming()` | Allows framing by the Admin and nothing else. **Must be registered before `UseAntiforgery()`.** |
| 11 | `UseStatusCodePages()`, `UseStaticFiles()`, `UseAntiforgery()`, `UseAuthentication()`, `UseAuthorization()` | Error pages, static assets, the anti-forgery middleware Blazor's pipeline requires, then the App Bridge authentication scheme. |
| 12 | `MapRazorComponents<App>().AddInteractiveServerRenderMode()` | Routes requests to `App.razor`. |
| 13 | `app.Run()` | |

## Endpoints

| Route | Mapped when | Auth | What it answers |
|---|---|---|---|
| `GET /api/diagnostics/session-token` | Development only | `.AllowAnonymous()` | Takes the `Authorization` header plus an optional `token` query parameter and returns `SessionTokenDiagnostics.InspectAsync(...)`: what the token contains, and why it was rejected. |
| `GET /api/diagnostics/rejections?count=` | Development only | `.AllowAnonymous()` | `ISessionTokenRejectionLog.Recent(count ?? 5)` — the last refusals as the server saw them, since the browser only ever sees a 401. |
| `GET /api/products` | `include-admin-api-example` only | `.RequireAuthorization()` | Reads the bearer token with `BearerTokens.Read(context)`, calls `ProductsService.ListAsync(sessionToken, ct)`, and returns `{ shop, products, apiVersion }`. |

`/api/products` maps a `ShopifyAdminApiException` onto three deliberately different answers:

| Exception | Status | Title and intent |
|---|---|---|
| `IsTokenRejected` | 401 | "Session rejected" — fetch a fresh token and retry. |
| `IsAccessDenied` | 403 | "Missing permission" — retrying cannot help; the detail names `read_products` and says to reopen the app so the merchant approves the new scope. |
| anything else | 502 | "Shopify request failed" — Shopify refused the query. |

## Gotchas

- **The render mode is not set here.** `MapRazorComponents` only maps the root component; a page that
  never applies a render mode renders perfect markup with no handler attached, which reads as a styling
  bug. `Routes.razor` carries `@rendermode InteractiveServer` for that reason.
- **The framing/anti-forgery order is load-bearing.** Anti-forgery sets `X-Frame-Options` and
  `frame-ancestors 'self'`, which refuse the Admin's iframe, and `OnStarting` callbacks run
  last-registered-first — so `UseShopifyEmbeddedFraming()` has to come first or the app can never be
  embedded, with nothing wrong in a normal browser tab. Embedding anywhere else needs
  `options.AllowFrameAncestor("https://…")` inside that call; nothing else is configurable.
- **A missing credential downgrades the app instead of failing it.** With no key `validateOnStart` is
  false and the app boots into a state where session tokens, the Admin API and App Bridge all do
  nothing; the page explains that rather than the process failing at startup.
- **The API key can come from two places, and the environment wins.** `SHOPIFY_API_KEY` overrides
  `Shopify:ApiKey` from user secrets, and a key belonging to a different Shopify app rejects every
  session token — the line logged by `LogShopifyConfiguration` is the answer to "which key is this?".
- **The diagnostics decode tokens without verifying them** and are mapped only inside the Development
  `if`, so they are a debugging aid and not part of the security surface. Moving them out of the block
  exposes unverified token decoding to production.
- **A missing scope is not a 403 from Shopify.** It arrives as HTTP 200 with an `errors` array, which
  is why the endpoint decides from `IsAccessDenied` rather than from a status code.
- **The 401 branch has a second meaning.** Authorization has already run when the handler executes, so
  reaching it with no bearer token means the handler let the request through without one — a routing or
  scheme fault, not a bad credential, and the detail says exactly that.
- The `//#if (include-admin-api-example)` directive wraps the service registration *and* the endpoint;
  deleting one without the other leaves either a `ProductsService` reference with no type or an endpoint
  with no service.
- `UseHsts()` is commented out, so no `Strict-Transport-Security` header is sent.

## See also

- [Components/App.razor.md](Components/App.razor.md) — the document this app renders and the scripts it loads.
- [Components/Routes.razor.md](Components/Routes.razor.md) — where the interactive render mode is applied.
- [Services/ProductsService.cs.md](Services/ProductsService.cs.md) — the server half of `/api/products`.
- [Services/BearerTokens.cs.md](Services/BearerTokens.cs.md) — the helper that reads the `Authorization` header.
