<!-- summary-of: NullRejectionLog.cs -->
<!-- source-sha256: a138cc10a0bc -->

# NullRejectionLog.cs

The do-nothing `ISessionTokenRejectionLog`, used when no rejection log has been registered. It discards
every rejection and always reports that there are none, so the authentication handler can record
unconditionally without a null check.

`Shopify.Blazor.Diagnostics` · `internal sealed class` · implements `ISessionTokenRejectionLog`

## Members

| Member | Signature | What it does |
|---|---|---|
| `Instance` | `public static readonly NullRejectionLog` | The single shared instance; there is no constructor call to repeat. |
| `Record` | `void Record(AuthenticationFailure failure, string path)` | Discards both arguments. |
| `Recent` | `IReadOnlyList<SessionTokenRejection> Recent(int count = 5)` | Always returns an empty list, whatever `count` is. |
| `Clear` | `void Clear()` | Does nothing — there is nothing to forget. |

## Behaviour

It is a null-object, not a fallback store: the point is that `ISessionTokenRejectionLog` is optional,
so a host that never registers one still gets a working authentication handler. The handler reaches
for it when `Context.RequestServices` does not resolve the interface.

## Gotchas

- **"No rejections" and "no log registered" are the same answer.** `Recent` returns an empty list in
  both cases, so a diagnostics page showing the last rejections cannot tell an app that has recorded
  nothing from an app that is not recording. Register the real
  [SessionTokenRejectionLog.cs.md](SessionTokenRejectionLog.cs.md) if the distinction matters.
- It is `internal`, so an application cannot register or resolve it deliberately; it only ever appears
  as the handler's fallback.
- Nothing is logged to `ILogger` either, so a rejection recorded here leaves no trace at all beyond
  the handler's own warning line.

## See also

- [SessionTokenRejectionLog.cs.md](SessionTokenRejectionLog.cs.md) — the real bounded implementation and the interface.
- [ShopifySessionTokenAuthenticationHandler.cs.md](../Authentication/ShopifySessionTokenAuthenticationHandler.cs.md) — the only caller.
- [SessionTokenDiagnostics.cs.md](SessionTokenDiagnostics.cs.md) — the developer-facing report that a page normally shows instead.
