<!-- summary-of: BearerTokens.cs -->
<!-- source-sha256: 695178603524 -->

# BearerTokens.cs

The single place the sample reads a bearer token out of an incoming request. The protected endpoint
and the diagnostics endpoint both use it, so neither can interpret the `Authorization` header
differently from the other — and "no credential offered" stays distinguishable from "a credential
was offered and rejected", which a bare `Results.Unauthorized()` throws away.

`Shopify.Blazor.SampleApp.Services` · `public static class` · no state, one method

## Members

| Member | Signature | What it does |
|---|---|---|
| `Read` | `static string? Read(HttpContext context)` | Returns the token with the `Bearer ` prefix and surrounding whitespace removed, or `null` when the request carries no usable one. Throws `ArgumentNullException` if `context` is null. |
| `Prefix` | `private const string` = `"Bearer "` | The scheme and its trailing space. Private, so it exists only as the parsing rule. |

## Behaviour

- The scheme match is `OrdinalIgnoreCase`, so `bearer`, `Bearer` and `BEARER` all work — HTTP treats
  the auth scheme as case-insensitive.
- The header must be **strictly longer** than the prefix: the length check runs before the prefix
  comparison, so a bare `Authorization: Bearer ` (or a header shorter than the prefix) is rejected
  before any substring is taken.
- The remainder after the prefix is trimmed, so whitespace around the token is discarded but
  whitespace *inside* it is left alone. An all-whitespace remainder returns `null` rather than `""`.
- Nothing validates the token. A present-but-invalid token comes back as a string; only failing
  signature verification downstream can reject it. That is what keeps "absent" (`null`) and
  "rejected" (non-null but invalid) separate outcomes for the caller.

## Gotchas

- Returning `null` is the only failure signal — there is no reason, no logging and no exception — so a
  caller that maps `null` straight onto `401` loses the distinction the class exists to preserve. The
  endpoint is supposed to say "no token in the request" while the validator says "that token is not
  acceptable".
- The header is read once through `Request.Headers.Authorization.ToString()`, so the value it sees is
  the header as ASP.NET joined it: several `Authorization` values arrive as one combined string and
  fail the prefix test. A tab or newline after the scheme fails too — the prefix is a literal space.
- `HttpContext` is null-checked but the method never touches anything else on it, so it is safe to
  call before the response has started and safe to call twice.

## See also

- [ProductsService.cs.md](ProductsService.cs.md) — the service the token is ultimately handed to.
- [Products.razor.md](../Components/Pages/Products.razor.md) — the page whose request carries the token.
- [shopify-fetch.js.md](../wwwroot/js/shopify-fetch.js.md) — the browser code that mints and attaches it.
