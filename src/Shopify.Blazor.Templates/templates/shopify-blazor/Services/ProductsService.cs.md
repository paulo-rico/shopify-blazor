<!-- summary-of: ProductsService.cs -->
<!-- source-sha256: e5f378c434fb -->

# ProductsService.cs

The one authenticated Admin API read the reference app demonstrates. It takes a verified session
token, asks `IShopifyAdminApi` for the shop's ten most recently updated products, and flattens the
GraphQL response into display records — never reading a shop from a query string, never touching an
access token itself, and never exposing a token to its caller.

`Shopify.Blazor.SampleApp.Services` · application service · no base class, no interface

## Members

| Member | Signature | What it does |
|---|---|---|
| `ProductListResult` | `sealed record (string ShopDomain, IReadOnlyList<ProductSummary> Products, string? ApiVersion)` | What `ListAsync` returns: the shop the data came from, the products, and the Admin API version Shopify actually served. |
| `ProductSummary` | `sealed record (string Id, string Title, string Status, string? Price, DateTimeOffset? UpdatedAt)` | One product flattened for display. `Price` is the first variant's price **as a string**, exactly as Shopify returns money. |
| `ProductsService` | `ProductsService(IShopifyAdminApi api, ILogger<ProductsService> logger)` | Takes the API facade and a logger; nothing else is injected. |
| `ListAsync` | `Task<ProductListResult> ListAsync(string sessionToken, CancellationToken cancellationToken = default)` | Runs the built-in query with `first = 10`, parses the response defensively, logs the count, and returns the result. Throws `ShopifyAdminApiException` when authentication fails or Shopify refuses the query. |

## Behaviour

- The query is a private constant: `shop { name myshopifyDomain }` plus
  `products(first: $first, sortKey: UPDATED_AT, reverse: true)` with `id`, `title`, `status`,
  `updatedAt` and `variants(first: 1) { price }`. `sortKey: UPDATED_AT` with `reverse: true` is what
  makes the newest changes come first; the page relies on that ordering rather than sorting again.
- Variables are passed as the anonymous object `new { first = 10 }`. The number of products is
  hard-coded here, not a parameter, so there is no paging or continuation path in this service.
- GraphQL errors come back with HTTP 200, and `QueryAsync` has already turned them into an exception
  by the time this method sees a response — so there is deliberately no second error check to write.
  The null-forgiving `response.Data!.Value` is safe only for that reason.
- Parsing is defensive at every level. A missing `products`, missing `nodes`, or a `nodes` value that
  is not a JSON array yields an empty list rather than throwing.
- The price is read only from the first variant and only when at least one variant node exists; it is
  `GetString()` on the JSON value, never parsed to a number, because money serialises as a string and
  parsing would lose precision on some currencies. No variants means `Price` is null, which the page
  renders as an em dash.
- `title` and `status` fall back to the sentinels `"(untitled)"` and `"UNKNOWN"` when absent, so the
  table never shows a blank cell for a real row. `id` falls back to an empty string instead.
- `updatedAt` is only parsed when the JSON value is a string; otherwise it stays null.
- The shop domain comes from `shop.myshopifyDomain`, falling back to `"unknown"`. It is not the
  request's host and not a query-string value — that is the point of the type.
- `ApiVersionServed` is copied from the response, which is what the page badges as `Admin API …`.
  This is the version Shopify answered with, which can differ from the one requested.
- Exactly one `LogInformation` line per call: the product count and the shop.

## Gotchas

- **The explicit `using Shopify.Blazor;` is load-bearing even though this project does not need it.**
  The file's namespace nests inside `Shopify.Blazor`, so enclosing-namespace lookup would find
  `IShopifyAdminApi` without a using — and the template renames that root namespace, which removes the
  lookup and fails the generated project with "the type or namespace name 'IShopifyAdminApi' could not
  be found". The `#pragma warning disable IDE0005` around it exists to stop the "unnecessary using"
  warning from inviting its deletion.
- **A price read as a decimal would fail at runtime, not at compile time.** `variant.price` arrives as
  a string; converting it here would either throw inside the parse loop or silently round. The record
  carries the string and the caller decides how to present it.
- Only one variant is fetched (`variants(first: 1)`, with no order specified), so the price shown is
  whichever variant Shopify returns first — not necessarily the product's cheapest or its default.
- A malformed response produces a plausible-looking empty result rather than an error: the page cannot
  tell "the shop has no products" from "the JSON shape changed". The log line is the only evidence.
- `ConfigureAwait(false)` on both await points keeps the continuation off the caller's synchronization
  context, so the service can be awaited from a UI-bound caller without a deadlock risk.

## See also

- [BearerTokens.cs.md](BearerTokens.cs.md) — how the endpoint obtained the token passed in.
- [Products.razor.md](../Components/Pages/Products.razor.md) — the page that renders this result.
- [shopify-fetch.js.md](../wwwroot/js/shopify-fetch.js.md) — the browser half of the same request.
