<!-- summary-of: ShopifyConfigurationDiagnostics.cs -->
<!-- source-sha256: fcfb3a236dab -->

# ShopifyConfigurationDiagnostics.cs

Logs which configuration layer supplied the Shopify API key and whether the credentials are usable, so an
app that works in one shell and not another says why. It runs once at startup, makes no network call, and
reports the secret's presence and length but never its value.

`Shopify.Blazor.Diagnostics` · `static class` of `WebApplication` extensions · no base class

## Members

| Member | Signature | What it does |
|---|---|---|
| `LogShopifyConfiguration` | `static void LogShopifyConfiguration(this WebApplication app, IConfiguration configuration)` | Writes the startup summary through a logger named `Shopify.Configuration`, and returns after one error when no API key is configured. Both arguments are null-checked. |

There is one private helper, `DescribeConfigProvider(WebApplication app, string value)`, which names the
provider the key came from; its behaviour is described below. The class has no state and no other members.

## What it writes

| When | Level | Content |
|---|---|---|
| `ApiKey` blank | `Error` | Says session tokens cannot be verified and the app will not work, gives the `dotnet user-secrets` and `SHOPIFY_API_KEY` spellings, then **returns**. |
| Key came from the environment variable | `Warning` | Names the source, warns that an environment variable overrides user secrets, and advises unsetting `SHOPIFY_API_KEY`. |
| Key came from anywhere else | `Information` | `Shopify:ApiKey came from <source>.` |
| Key present | `Information` | `ApiKey` in full; `Secret` as `NOT SET (token exchange will fail)` or `set (N characters)`; `AppUrl` or `(not set)`; `AccessTokenKind`; `AdminApiVersion`. |
| `ApiSecret` blank | `Error` | No session token can be exchanged; gives the user-secrets command. |
| Key present | `Information` | Restates the key the page renders into its `shopify-api-key` meta tag and warns that the Admin must have loaded the app whose Client ID that is. |

A healthy startup writes three `Information` lines; each missing value interrupts the run at a different point.

## Behaviour

The source is decided in this order:

1. `SHOPIFY_API_KEY` is read from the process environment and compared, ordinal, with the effective
   `options.ApiKey`. A match is reported as the environment variable, with the warning.
2. Otherwise `configuration["Shopify:ApiKey"]` is compared with the effective value. A match is handed to
   `DescribeConfigProvider`, which walks `app.Configuration`'s providers and returns the first one holding
   the same value under `Shopify:ApiKey`.
3. Neither match is reported as `an unknown source`.

Provider names are classified by CLR type name: one containing `Secrets` is `the user secrets store`, one
containing `Environment` is the environment variable, and anything else is printed as its type name — so a
committed file appears as `the configuration provider 'JsonConfigurationProvider'`. That distinction is
the point of the method: a key in a committed file and a key in a secret store are different situations,
and only the second is safe to keep.

Read the app-facts line when a token is being rejected. `AppUrl` must be the URL open in the Admin, and the
last line matters because App Bridge mints tokens for whichever Client ID the page was told: a server
holding a different app's key rejects every token as belonging to another app while the log looks healthy.

## Gotchas

- **A missing API key hides a missing secret.** The early return skips the secret check, the app-facts
  line and the meta-tag line, so the only error reported is about the key.
- **Attribution is equality, not presence.** A `SHOPIFY_API_KEY` that is set but is not the effective value
  is never mentioned; the report names the layer whose value matched instead, which is how an inline
  `configure` action that replaced the key leaves the environment variable invisible.
- **Identical values in two layers are attributed to the first match.** The environment check runs first,
  so the same key in user secrets *and* the environment is reported as the environment variable with a
  warning suggesting it be unset; within the provider walk, a key in both `appsettings.json` and user
  secrets is usually attributed to the file, because the JSON provider enumerates first.
- **A caller-supplied `configure` action can beat the environment variable.**
  `ShopifyServiceCollectionExtensions.AddShopifyBlazor` applies `SHOPIFY_API_KEY` before the caller's
  action, so the effective key may not be the environment one — and this log then describes whatever
  layer the value did match.
- **`an unknown source` is nearly unreachable, and `configuration` is the weak fallback.**
  `DescribeConfigProvider` falls back to `configuration` whenever the `Shopify` section exists, which it
  almost always does because `appsettings.json` is loaded, so a value it cannot place is still reported as
  having come from a real source.
- **The `IConfiguration` argument is not the configuration used for attribution.** It is read only for
  `Shopify:ApiKey`; the provider walk runs over `app.Configuration`. Passing anything other than the
  configuration the app was built from can produce a confidently wrong source.
- **It must be called after `builder.Build()`.** It resolves `ILoggerFactory` and
  `IOptions<ShopifyOptions>` from the built container, so an earlier call throws. The sample app and the
  project template call it immediately after building, before any middleware runs.
- **The API key is logged in full, and twice.** That is deliberate — it is already rendered into the page —
  but every log sink then holds it. The secret never appears; only its length is printed, which cannot
  distinguish a truncated secret from another app's secret of the same length.
- **Nothing is validated beyond presence.** No request reaches Shopify, the key and secret are not checked
  for belonging to the same app, `AppUrl` is not checked for `https` or for a repeated scheme, and scopes
  are not mentioned. A key from the wrong app produces a clean report and a page whose every call fails.
- **`AdminApiVersion` is the configured value, not the one Shopify serves.** A retired version falls
  forward to an older stable release without an error, so this line proves only what was asked for.
- **The logger name is the string `Shopify.Configuration`,** not the type name, so a filter on
  `Shopify.Blazor.Diagnostics` will not match these lines.

## See also

- [SessionTokenDiagnostics.cs.md](SessionTokenDiagnostics.cs.md) — the per-token half of the same
  investigation, including the `AppUrl` suspicion check this file does not make.
- [SessionTokenRejectionLog.cs.md](SessionTokenRejectionLog.cs.md) — where a rejected token is recorded for
  the next request to read.
- [ShopifyOptions.cs.md](../ShopifyOptions.cs.md) — the settings quoted in the log, their validation and
  the environment overrides behind them.
- [ShopifyServiceCollectionExtensions.cs.md](../ShopifyServiceCollectionExtensions.cs.md) — where the
  environment override is applied, and therefore why it outranks user secrets.
