<!-- summary-of: SessionTokenRejectionLog.cs -->
<!-- source-sha256: 7095883a0ce2 -->

# SessionTokenRejectionLog.cs

Keeps the most recent session token rejections in memory so a developer can read why the last request
failed — a reason the browser only ever sees as a 401. It holds the interface, the entry record and the
bounded implementation.

`Shopify.Blazor.Diagnostics` · the rejection-log contract and its bounded in-memory implementation · no base class

## Members

### `ISessionTokenRejectionLog`

| Member | Signature | What it does |
|---|---|---|
| `Record` | `void Record(AuthenticationFailure failure, string path)` | Adds a rejection for the given request path, timestamped now in UTC. |
| `Recent` | `IReadOnlyList<SessionTokenRejection> Recent(int count = 5)` | The newest rejections first, at most `count` of them. |
| `Clear` | `void Clear()` | Forgets everything recorded so far. |

### `SessionTokenRejection` (sealed record)

| Position | Type | What it carries |
|---|---|---|
| `At` | `DateTimeOffset` | When the rejection happened, UTC. |
| `Path` | `string` | The path that was requested. |
| `Code` | `string` | A stable code for the kind of failure (`session_token_rejected` from the handler). |
| `Failure` | `string` | The validator's failure category, as a string. |
| `Reason` | `string` | The validator's explanation. |
| `Hint` | `string?` | What to do about it, present only when the app opted in to failure detail. |

### `SessionTokenRejectionLog`

| Member | Signature | What it does |
|---|---|---|
| `Record` | `void Record(AuthenticationFailure failure, string path)` | Enqueues an entry, then dequeues from the front until the queue is back within capacity. Throws `ArgumentNullException` for a null `failure`. |
| `Recent` | `IReadOnlyList<SessionTokenRejection> Recent(int count = 5)` | Reverses the queue, takes `count`, and materialises a list. `count <= 0` returns an empty list without touching the lock. |
| `Clear` | `void Clear()` | Empties the queue. |

## Behaviour

The capacity is a private constant of **20** entries. Once it is reached, the oldest entry is dropped
silently on each new record — that bound is deliberate: this is a diagnostic, not an audit trail, and
an attacker who can trigger rejections must not be able to grow it without limit. All three members
take one `Lock`, so the log is safe as a singleton under concurrent requests.

The reason a token was refused is known only on the server: carrying it on the request does not survive
the framework's own authentication handler lifecycle, and putting it in a production 401 body would
tell an unauthenticated caller exactly why its credential failed. A bounded in-memory log gives the
developer the answer without either problem.

## Gotchas

- **It is per process and volatile.** "In memory" means a restart clears it, and two instances behind a
  load balancer keep two different histories — a rejection recorded on one may be invisible on the
  next request.
- **Past 20 rejections the oldest are gone with no indication.** `Recent` cannot say whether it is
  showing everything or the surviving tail of a flood, which matters precisely when a token is failing
  repeatedly.
- **`Recent` returns newest first.** Reading it as a chronological log reverses the order and makes a
  stale failure look like the current one.
- **`Hint` is often `null`.** It is populated only when the handler ran with
  `IncludeFailureDetail` on, which the remarks say should be off in production; the `Reason` is always
  present.
- **Recording does not log.** The entry only reaches `ILogger` through the authentication handler's own
  warning; the log is there for a diagnostics page to read, not to replace the application log.

## See also

- [NullRejectionLog.cs.md](NullRejectionLog.cs.md) — the do-nothing implementation used when nothing is registered.
- [ShopifySessionTokenAuthenticationHandler.cs.md](../Authentication/ShopifySessionTokenAuthenticationHandler.cs.md) — where entries come from.
- [SessionTokenDiagnostics.cs.md](SessionTokenDiagnostics.cs.md) — the on-demand report that explains one token.
- [ShopifySessionTokenValidator.cs.md](../Authentication/ShopifySessionTokenValidator.cs.md) — the categories and reasons recorded here.
