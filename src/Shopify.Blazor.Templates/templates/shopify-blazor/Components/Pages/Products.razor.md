<!-- summary-of: Products.razor -->
<!-- source-sha256: ee2d5bf3a2a9 -->

# Products.razor

The template's one authenticated page. At `/products` it asks App Bridge for a fresh session token,
calls this app's own `/api/products` endpoint with it, and renders the answer as a Polaris table —
covering every state a request really has: never made, loading, failed, empty, and populated.

`Components/Pages` · `@page "/products"` · no parameters · injects `IAppBridgeInterop` as `Bridge`

## Behaviour

Everything is inside one vertical `PolarisStack` at `PolarisGap.Base`. What it renders, in order:

| Region | Rendered when | Contents |
|---|---|---|
| Intro | always | `PolarisSection Heading="Products"` holding a `PolarisText` that explains the split: the server calls Shopify, the browser contributes only a freshly minted session token. |
| Toolbar | always | Inline stack with a `Primary` Refresh button (`OnClick="LoadFromButtonAsync"`, `Loading="_loading"`), a `Neutral` badge carrying the shop domain once `_shop` is set, and an `Info` badge reading `Admin API {version}` once `_apiVersion` is set. A null `_shop`/`_apiVersion` drops its badge rather than rendering an empty one. |
| Error banner | `_error is not null` | `PolarisBanner Heading="Could not load products"` at `PolarisTone.Critical`, body a `PolarisParagraph`. |
| Loading | `_loading` | A `PolarisSpinner` (`AccessibilityLabel="Loading products"`) beside `Loading products…`. |
| Never loaded | `_products is null` | "No request has been made yet. Use Refresh to load products." |
| Empty | `_products.Count == 0` | `PolarisEmptyState Heading="No products yet"` with a `subheading`-slotted `PolarisText`. |
| Table | otherwise | `PolarisSection Padding="None"` wrapping `PolarisTable`: headers Title (`ListSlot="Primary"`), Status, Price (`Format="Currency"`), Updated; one `PolarisTableRow` per product, `@key="row.Id"`. |

`LoadAsync` is the whole page's logic:

- It is re-entrancy guarded — a second click while `_loading` returns immediately, so the button's own
  spinner is the only debounce. `_error` is cleared at the start of every attempt.
- The fetch is wrapped in `Bridge.WithLoadingAsync`, which drives the Admin's header progress bar and
  clears it in a `finally`, so a failed request does not leave the merchant's admin spinning.
- `FetchAsync<ProductListResponse>` returns an envelope rather than throwing on an HTTP status. When
  `IsSuccess` is false the banner shows `result.Error`, falling back to "The request failed.".
- A 401 is read as "the session token was refused": the page then asks the server why by calling
  `/api/diagnostics/rejections?count=1` and appends the answer. Only the first rejection is read, and
  the `Hint` is preferred over the `Reason` because the hint names the cause while the reason names
  the symptom. Any failure of that diagnostic yields an empty string, leaving the plain error.
- On success `_shop`, `_apiVersion` and `_products` (`Products ?? []`) are assigned, and a toast
  ("Loaded N products.") fires only when at least one product came back.
- Any exception is caught into `_error`; `_loading` is cleared in `finally` either way.
- `StatusTone` maps the raw status case-insensitively: ACTIVE → Success, DRAFT → Warning, ARCHIVED →
  Neutral, anything else → Info.
- `Rows` returns `[]` while `_products` is null, so the row loop never sees a null list.

## Gotchas

- **The empty state is rendered conditionally because `s-empty-state` cannot detect emptiness on its
  own** (stated in an inline comment). Rendering it unconditionally would put "No products yet" above
  real rows.
- **One cell per header, in the same order.** A missing or extra cell shifts every later column with
  no warning — this is why the four headers and four cells are written as a visible pair.
- The Price header declares `Format="Currency"` while its cell prints `row.Price` verbatim, and the
  Updated cell formats `UpdatedAt` in the page. A null of either renders `—`; the page never parses
  the money string.
- The `OnClick` handler returns `Task` (`LoadFromButtonAsync` discards the `PolarisEventArgs`), so the
  button's `Loading` state really does track the request instead of flashing.
- `ProductListResponse` and `Rejection` are private positional records with no serializer attributes,
  so the endpoint's JSON field names are what bind them: a renamed field arrives as null and shows up
  as a missing badge or an empty table, not as an error.
- The diagnostics call is Development-only on the server side, so in production a 401 shows the plain
  rejection message and nothing more.

## See also

- [ProductsService.cs.md](../../Services/ProductsService.cs.md) — the server half of the same read.
- [shopify-fetch.js.md](../../wwwroot/js/shopify-fetch.js.md) — the module `Bridge.FetchAsync` calls.
- [BearerTokens.cs.md](../../Services/BearerTokens.cs.md) — how an endpoint reads the token this page sends.
