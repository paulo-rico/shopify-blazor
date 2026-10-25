<!-- summary-of: AdminGraphQlClient.cs -->
<!-- source-sha256: d2c8d8a8d6eb -->

# AdminGraphQlClient.cs

Executes GraphQL queries and mutations against a shop's Admin API over `HttpClient`, and reports a
GraphQL failure as a failure. Shopify answers most query problems with `200 OK` and an `errors` array,
so checking only the status code is the classic way to render a failed query as an empty result.

`Shopify.Blazor.AdminApi` · the Admin API transport · `AdminGraphQlClient : IAdminGraphQlClient`

## Members

| Member | Signature | What it does |
|---|---|---|
| `AdminGraphQlError` | `sealed record (string Message, IReadOnlyList<string>? Path, JsonElement? Extensions)` | One GraphQL error from the response. `Path` is Shopify's response path with each segment stringified; `Extensions` is the raw object, when supplied. |
| `AdminGraphQlError.Code` | `string? Code { get; }` | Shopify's machine-readable code read out of `Extensions.code`, e.g. `THROTTLED`. Null unless `Extensions` is an object carrying a **string** `code`. |
| `AdminGraphQlResponse.StatusCode` | `HttpStatusCode` | The HTTP status Shopify returned, including non-200s. |
| `AdminGraphQlResponse.RawBody` | `string` | The untouched response body, for logging and diagnostics. |
| `AdminGraphQlResponse.Data` | `JsonElement?` | The `data` element, or null when the body had none or it was JSON null. |
| `AdminGraphQlResponse.Errors` | `IReadOnlyList<AdminGraphQlError>` | Parsed GraphQL errors; empty when there were none. |
| `AdminGraphQlResponse.ApiVersionServed` | `string?` | The `X-Shopify-API-Version` header Shopify answered with — not necessarily the version requested. |
| `AdminGraphQlResponse.IsSuccess` | `bool` | True only when the status is 200, `Errors` is empty, **and** `Data` is non-null. |
| `AdminGraphQlResponse.DescribeFailure()` | `string` | The errors joined with `; `, or `The Admin API returned HTTP {code}.` when there were none. |
| `AdminGraphQlResponse.Deserialize<T>(JsonSerializerOptions? options = null)` | `T` | Deserializes `Data` as `T`. Throws `InvalidOperationException` when the response failed, and when deserialization yields null. |
| `IAdminGraphQlClient.ExecuteAsync` | `Task<AdminGraphQlResponse> ExecuteAsync(string query, object? variables, ShopifyAccessToken accessToken, string shopDomain, CancellationToken cancellationToken = default)` | Posts one document to `https://{shopDomain}/admin/api/{version}/graphql.json`. |
| `AdminGraphQlClient` | `AdminGraphQlClient(IHttpClientFactory httpClientFactory, IOptionsMonitor<ShopifyOptions> options, ILogger<AdminGraphQlClient> logger)` | The default implementation. |

## Behaviour

- The requested version is read from `ShopifyOptions.AdminApiVersion` on **every call**
  (`IOptionsMonitor.CurrentValue`), so a configuration reload takes effect without restarting. It is
  interpolated into the endpoint along with the caller's `shopDomain`; nothing here validates the
  domain — that is the caller's job.
- The body is a JSON object with `query` and, when variables are supplied, `variables` serialized with
  web defaults. The access token goes in the `X-Shopify-Access-Token` header, not as a bearer token.
- The `HttpClient` comes from the named client `ShopifyHttpClients.AdminApi`, so retry policies, base
  address and timeouts are whatever that registration says, not this class.
- The response body is read in full even on a non-200, and `X-Shopify-API-Version` is captured. If the
  served version differs from the requested one (case-insensitive), a warning is logged: Shopify fell
  forward to an older stable release and is answering normally, so the log is the only signal.
- Parsing never throws. A body that is not valid JSON logs an error and appends a synthetic
  `AdminGraphQlError` ("The Admin API response was not valid JSON."), which makes `IsSuccess` false and
  leaves `Data` null. A body that is valid JSON but not an object is ignored entirely.
- Each error's `message` falls back to "Unknown GraphQL error." when missing or not a string; `path`
  is kept only when it is an array; `extensions` only when it is an object.
- A 401 logs a warning rather than throwing: the token was revoked or expired, and the caller is
  expected to drop its cached token and exchange again instead of showing the merchant an error.
- No exception type of its own exists. Transport failures (DNS, TLS, timeout) propagate from
  `SendAsync`; HTTP and GraphQL failures come back in the response.

## Gotchas

- **A GraphQL failure is not an HTTP failure.** `StatusCode` can be `OK` while the query failed, which
  is why `IsSuccess` checks `Errors` and `Data` as well. `Deserialize<T>` deliberately throws on a
  failed response so that a missed `IsSuccess` check surfaces as an exception rather than as a
  mysterious null — but that means it is not a safe "try it and see" call.
- **`RawBody` is the only place the original response survives.** A body that cannot be parsed yields a
  synthetic error and no `Data`; the parse exception's message goes only to the log, so an HTML gateway
  page reaching this client looks like a generic GraphQL error whose real cause is one log line away.
- **`ApiVersionServed` null means the header was absent**, not that the requested version was served;
  the warning is skipped in that case. A retired version does not error — it falls forward and the only
  signal is that warning.
- **`Data` is null for `"data": null`**, which is how Shopify reports a request that failed before
  resolution. Such a response is never `IsSuccess`, even with an empty `errors` array.
- **`Extensions` is only kept when it is an object.** If Shopify ever sends a string or array there,
  `Code` is null and the raw value is lost.
- Throttling is not retried or delayed anywhere here. A `THROTTLED` error arrives as an ordinary error
  with `Code == "THROTTLED"` and the query simply fails; backoff is the caller's decision.
- `Deserialize<T>` passes the caller's `JsonSerializerOptions` straight through, so the naming policy
  and converters are also the caller's — the client's own web-default options are used only for the
  request payload.

## See also

- [../Internal/ShopDomain.cs.md](../Internal/ShopDomain.cs.md) — why `shopDomain` must be normalised before it reaches the endpoint string.
- [../AppBridge/AppBridgeInterop.cs.md](../AppBridge/AppBridgeInterop.cs.md) — the browser side that supplies the session token this client's caller exchanges for an access token.
